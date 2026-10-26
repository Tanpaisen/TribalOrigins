using System;
using System.Collections;
using System.Collections.Generic;
using TribalOrigins.Core;
using TribalOrigins.Units;
using UnityEngine;

namespace TribalOrigins.AI
{
    /// <summary>
    /// M2.4B PROTOTYPE: autonomously seeks the TWO M2.4A TEST facilities, walks using
    /// the already-installed M1.10 A*, recovers Food/Rest, and resumes a previous route.
    /// This is NOT a verified THQ algorithm: no actual stock, cook, builder, occupancy
    /// animation or player-built construction exists yet.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01AStarControls))]
    [RequireComponent(typeof(Level01PrototypeFacilities))]
    [RequireComponent(typeof(Level01VillagerNeedsPrototype))]
    public sealed class Level01AutoNeedsAI : MonoBehaviour
    {
        private const string SpawnRootName = "Generated_Initial_Entities_DEBUG";
        private const int Population = 9;
        private enum NeedKind { None, Food, Rest }
        private enum Phase { Idle, Travelling, Recovering }

        // VERIFIED IN THQ 0.0.20: decompiled_original/b.java n(), lines 3282-3330.
        // The need threshold is 20 when happiness > 5, otherwise 50.
        // Exhaustion takes priority over hunger. Ages below 12 do not make these choices.
        private const int NeedIfHappy = 20;
        private const int NeedIfMiserable = 50;
        private const int MiserableHappiness = 5;
        private const int AdultAge = 12;

        // VERIFIED IN THQ 0.0.20: /a records 30 (effect goals) and 31
        // (effect property). Eat actions 70/71 -> Food 99; Rest actions
        // 65 (Hut) and 79 (outdoors) -> Energy 99. These are NOT
        // recovery rates: the time between increments is still prototype.
        private const int FoodActionGoal = 99;
        private const int RestActionGoal = 99;

        [Header("PROTOTYPE facility service")]
        [SerializeField, Min(0.1f)] private float recoveryTickSeconds = 0.5f;
        [SerializeField, Range(1, 30)] private int foodPerTick = 6;
        [SerializeField, Range(1, 30)] private int restPerTick = 5;
        [SerializeField, Range(1, 9)] private int kitchenPrototypeCapacity = 2;
        [SerializeField, Range(1, 9)] private int hutPrototypeCapacity = 4;
        [SerializeField, Min(0.5f)] private float retryDelaySeconds = 3f;
        [SerializeField, Min(1f)] private float manualOrderGraceSeconds = 12f;
        [SerializeField] private bool logStateChanges = true;

        private sealed class Plan
        {
            public VillagerData Villager;
            public NeedKind Need;
            public Phase Stage;
            public VillagerAction PreviousAction;
            public bool HasPreviousDestination;
            public Vector2Int PreviousDestination;
            public float RecoveryAccumulator;
            public float RetryAfter;
            public float PlayerGraceUntil;
        }

        private readonly List<Plan> plans = new List<Plan>(Population);
        private Level01AStarControls navigation;
        private Level01PrototypeFacilities facilities;
        private bool ready;

        private IEnumerator Start()
        {
            navigation = GetComponent<Level01AStarControls>();
            facilities = GetComponent<Level01PrototypeFacilities>();
            if (navigation == null || facilities == null)
            {
                Debug.LogError("[TribalOrigins] M2.4B: requires A* controller and test facilities on WorldMap.", this);
                yield break;
            }
            // Components create the map, initial units and VillagerData in Start coroutines.
            for (int frame = 0; frame < 180; frame++)
            {
                Transform root = transform.Find(SpawnRootName);
                if (navigation.IsNavigationReady && root != null)
                {
                    VillagerData[] villagers = root.GetComponentsInChildren<VillagerData>(true);
                    if (villagers.Length == Population && Array.TrueForAll(villagers,
                        v => v != null && v.Initialized))
                    {
                        Array.Sort(villagers, (a, b) => a.Id.CompareTo(b.Id));
                        foreach (VillagerData v in villagers)
                            plans.Add(new Plan { Villager = v });
                        navigation.ManualMoveOrderAccepted += OnManualMove;
                        ready = true;
                        Debug.Log("[TribalOrigins] M2.4B READY: 9 EXISTING villagers; " +
                            "need <= (happiness>5 ? 20 : 50); REST wins a tie; " +
                            "EAT->99, SLEEP->99; restores prior valid route. " +
                            "Service speed/inventory are still PROTOTYPE.", this);
                        if (!facilities.ActiveForNavigation)
                            Debug.LogWarning("[TribalOrigins] M2.4B: TEST Hut/Kitchen are OFF. " +
                                "Enable 'Spawn Test Hut And Kitchen' outside Play Mode.", this);
                        yield break;
                    }
                }
                yield return null;
            }
            Debug.LogError("[TribalOrigins] M2.4B initialization failed: need 9 VillagerData and M1.10 READY.", this);
        }

        private void Update()
        {
            if (!ready || !facilities.ActiveForNavigation) return;
            foreach (Plan plan in plans)
            {
                VillagerData v = plan.Villager;
                if (v == null || !v.Initialized || !v.IsAlive) continue;
                if (plan.Stage == Phase.Travelling)
                {
                    if (navigation.HasActiveRoute(v.transform)) continue;
                    Vector2Int approach = Approach(plan.Need);
                    Vector3 expected = new Vector3(approach.x + 0.5f, approach.y + 0.5f,
                        v.transform.position.z);
                    if ((v.transform.position - expected).sqrMagnitude > 0.035f)
                    {
                        Abort(plan, "route ended before reaching access cell");
                        continue;
                    }
                    plan.Stage = Phase.Recovering;
                    plan.RecoveryAccumulator = 0f;
                    v.SetAction(plan.Need == NeedKind.Food ? VillagerAction.Eating : VillagerAction.Resting);
                    Log("START " + plan.Need.ToString().ToUpperInvariant() + ": " + v.DisplayName +
                        " at " + approach + "; job retained=" + v.Job);
                    continue;
                }
                if (plan.Stage == Phase.Recovering)
                {
                    plan.RecoveryAccumulator += Time.deltaTime;
                    int loops = 0;
                    while (plan.RecoveryAccumulator >= Mathf.Max(0.1f, recoveryTickSeconds) && loops++ < 8)
                    {
                        plan.RecoveryAccumulator -= Mathf.Max(0.1f, recoveryTickSeconds);
                        if (plan.Need == NeedKind.Food)
                            v.SetNeeds(Mathf.Min(FoodActionGoal, v.Satiety + foodPerTick), v.Energy, v.Happiness);
                        else
                            v.SetNeeds(v.Satiety, Mathf.Min(RestActionGoal, v.Energy + restPerTick), v.Happiness);
                    }
                    if ((plan.Need == NeedKind.Food && v.Satiety >= FoodActionGoal) ||
                        (plan.Need == NeedKind.Rest && v.Energy >= RestActionGoal))
                        Finish(plan);
                    continue;
                }

                if (Time.time < plan.PlayerGraceUntil || Time.time < plan.RetryAfter) continue;
                if (v.Age < AdultAge) continue;
                NeedKind need = ChooseNeed(v.Satiety, v.Energy, v.Happiness);
                if (need == NeedKind.None) continue;
                if (ReservedCount(need) >= (need == NeedKind.Food ?
                        kitchenPrototypeCapacity : hutPrototypeCapacity)) continue;

                Vector2Int door = Approach(need);
                VillagerAction oldAction = v.Action;
                bool hadRoute = navigation.TryGetCurrentDestination(v.transform, out Vector2Int formerGoal);
                if (!navigation.TryOrderAutoMovement(v.transform, door))
                {
                    plan.RetryAfter = Time.time + retryDelaySeconds;
                    Log("NO ROUTE to " + need + " for " + v.DisplayName +
                        ": retry later, no recovery granted.");
                    continue;
                }
                plan.PreviousAction = oldAction;
                plan.HasPreviousDestination = hadRoute && formerGoal != door;
                plan.PreviousDestination = formerGoal;
                plan.Need = need;
                plan.Stage = Phase.Travelling;
                plan.RecoveryAccumulator = 0f;
                Log("SEEK " + need.ToString().ToUpperInvariant() + ": " + v.DisplayName +
                    " -> " + door + (plan.HasPreviousDestination ? " (previous route saved)" : ""));
            }
        }

        private static NeedKind ChooseNeed(int food, int energy, int happiness)
        {
            int threshold = happiness > MiserableHappiness ? NeedIfHappy : NeedIfMiserable;
            // THQ 0.0.20 calls the rest branch first, then hunger.
            if (energy <= threshold) return NeedKind.Rest;
            if (food <= threshold) return NeedKind.Food;
            return NeedKind.None;
        }

        private static Vector2Int Approach(NeedKind need)
        {
            return need == NeedKind.Food ? Level01PrototypeFacilities.KitchenApproach :
                Level01PrototypeFacilities.HutApproach;
        }

        private int ReservedCount(NeedKind need)
        {
            int count = 0;
            foreach (Plan p in plans)
                if (p.Need == need && p.Stage != Phase.Idle) count++;
            return count;
        }

        private void Finish(Plan plan)
        {
            VillagerData v = plan.Villager;
            Log("DONE " + plan.Need.ToString().ToUpperInvariant() + ": " + v.DisplayName +
                " Food=" + v.Satiety + " Rest=" + v.Energy);
            bool restorePath = plan.HasPreviousDestination;
            Vector2Int previousGoal = plan.PreviousDestination;
            VillagerAction oldAction = plan.PreviousAction;
            ClearPlan(plan);
            if (restorePath && navigation.TryResumeMovement(v.transform, previousGoal))
            {
                Log("RESUME A* DESTINATION: " + v.DisplayName + " -> " + previousGoal);
                return;
            }
            // A real job task executor does not exist yet; preserve the assigned Job and
            // restore the earlier Work *state* only (no invented harvesting/production).
            v.SetAction(oldAction == VillagerAction.Working ? VillagerAction.Working : VillagerAction.Idle);
            plan.RetryAfter = Time.time + 0.25f;
        }

        private void Abort(Plan plan, string reason)
        {
            VillagerData v = plan.Villager;
            navigation.CancelAutoMovement(v.transform);
            v.SetAction(VillagerAction.Idle);
            ClearPlan(plan);
            plan.RetryAfter = Time.time + retryDelaySeconds;
            Log("ABORT: " + v.DisplayName + " (" + reason + "); needs unchanged.");
        }

        private static void ClearPlan(Plan plan)
        {
            plan.Stage = Phase.Idle;
            plan.Need = NeedKind.None;
            plan.HasPreviousDestination = false;
            plan.RecoveryAccumulator = 0f;
        }

        private void OnManualMove(Transform unit)
        {
            foreach (Plan plan in plans)
            {
                if (plan.Villager == null || plan.Villager.transform != unit) continue;
                // The controller ALREADY accepted and installed the player's new route.
                // Never cancel/replace it here; just abandon the AI service/ownership.
                if (plan.Stage != Phase.Idle)
                {
                    Log("MANUAL OVERRIDE: " + plan.Villager.DisplayName +
                        " (AI recovery interrupted, player route retained)");
                    ClearPlan(plan);
                }
                plan.PlayerGraceUntil = Time.time + manualOrderGraceSeconds;
                return;
            }
        }

        private void OnDisable()
        {
            if (navigation != null) navigation.ManualMoveOrderAccepted -= OnManualMove;
            foreach (Plan p in plans)
            {
                if (p.Villager == null || p.Stage == Phase.Idle) continue;
                if (navigation != null) navigation.CancelAutoMovement(p.Villager.transform);
                if (p.Villager.IsAlive &&
                    (p.Villager.Action == VillagerAction.Eating ||
                     p.Villager.Action == VillagerAction.Resting))
                    p.Villager.SetAction(VillagerAction.Idle);
                ClearPlan(p);
            }
        }

        [ContextMenu("M2.4B TEST: Villager 01 Food=15, Rest=12")]
        private void DebugMakeChiefHungryAndTired()
        {
            if (!Application.isPlaying || !ready)
            {
                Debug.LogWarning("[TribalOrigins] M2.4B TEST requires Play Mode and M2.4B READY.", this);
                return;
            }
            foreach (Plan p in plans)
                if (p.Villager.Id == 1)
                {
                    p.Villager.SetNeeds(15, 12, p.Villager.Happiness);
                    p.RetryAfter = 0f;
                    p.PlayerGraceUntil = 0f;
                    Debug.Log("[TribalOrigins] M2.4B TEST: chief Food=15, Rest=12. AI will seek facilities.", this);
                    return;
                }
        }

        private void Log(string message)
        {
            if (logStateChanges) Debug.Log("[TribalOrigins] M2.4B " + message, this);
        }
    }
}
