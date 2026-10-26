using System;
using System.Collections;
using System.Collections.Generic;
using TribalOrigins.Core;
using UnityEngine;

namespace TribalOrigins.Units
{
    /// <summary>
    /// M2.3 PROTOTYPE: updates hunger/energy/happiness on a fixed logical tick.
    /// Does not spawn units, steal input, override A* movement, damage health, or
    /// claim its timings match the original THQ game. Replace the rates with
    /// verified rule data in the later simulation milestone.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01VillagerDataBootstrap))]
    public sealed class Level01VillagerNeedsPrototype : MonoBehaviour
    {
        private const string SpawnRootName = "Generated_Initial_Entities_DEBUG";
        private const int ExpectedVillagers = 9;

        [Header("Prototype simulation (NOT THQ numeric rates)")]
        [SerializeField] private bool runSimulation = true;
        [SerializeField, Min(0.1f)] private float secondsPerTick = 1f;
        [SerializeField, Min(1f)] private float hungerSecondsIdle = 8f;
        [SerializeField, Min(1f)] private float hungerSecondsMoving = 5f;
        [SerializeField, Min(1f)] private float energySecondsIdle = 14f;
        [SerializeField, Min(1f)] private float energySecondsMoving = 3f;
        [SerializeField, Min(1f)] private float lowNeedsMoodSeconds = 5f;

        [Header("Thresholds and diagnostics (prototype)")]
        [SerializeField, Range(0, 100)] private int lowFoodThreshold = 25;
        [SerializeField, Range(0, 99)] private int lowEnergyThreshold = 20;
        [SerializeField] private bool logFirstLowNeedWarning = true;
        [SerializeField] private bool printReadyLog = true;

        private sealed class State
        {
            public VillagerData Villager;
            public float HungerTime;
            public float EnergyTime;
            public float MoodTime;
            public bool LoggedLowFood;
            public bool LoggedLowEnergy;
        }

        private readonly List<State> states = new List<State>(ExpectedVillagers);
        private float accumulated;
        private bool ready;

        private IEnumerator Start()
        {
            // M1.7 creates markers at Start and M2.1 adds VillagerData after that.
            // Wait for both instead of creating a second villager population.
            for (int attempt = 0; attempt < 120; ++attempt)
            {
                Transform root = transform.Find(SpawnRootName);
                if (root != null)
                {
                    VillagerData[] villagers = root.GetComponentsInChildren<VillagerData>(true);
                    if (villagers.Length == ExpectedVillagers)
                    {
                        bool allInitialized = true;
                        foreach (VillagerData v in villagers)
                            if (!v.Initialized) allInitialized = false;
                        if (allInitialized)
                        {
                            Array.Sort(villagers, (a, b) => a.Id.CompareTo(b.Id));
                            var ids = new HashSet<int>();
                            foreach (VillagerData v in villagers)
                            {
                                if (v.Id < 1 || v.Id > ExpectedVillagers || !ids.Add(v.Id))
                                {
                                    Debug.LogError("[TribalOrigins] M2.3 duplicate/invalid villager ID", this);
                                    yield break;
                                }
                                states.Add(new State { Villager = v });
                            }
                            ready = true;
                            if (printReadyLog)
                                Debug.Log("[TribalOrigins] M2.3 NEEDS PROTOTYPE READY: 9 existing villagers, " +
                                    "logical tick=" + secondsPerTick + "s. Hunger/energy/mood are TEST rates; " +
                                    "no HP loss; M2.4B handles optional test eating/resting when enabled.", this);
                            yield break;
                        }
                    }
                }
                yield return null;
            }
            Debug.LogError("[TribalOrigins] M2.3 could not find 9 initialized VillagerData. " +
                "Check M1.7 spawn and M2.1 bootstrap.", this);
        }

        private void Update()
        {
            if (!ready || !runSimulation) return;
            // A logical tick is used instead of subtracting a full point every rendered frame.
            accumulated += Time.deltaTime;
            int iterations = 0;
            float step = Mathf.Max(0.1f, secondsPerTick);
            while (accumulated >= step && iterations++ < 8)
            {
                accumulated -= step;
                AdvanceOneTick(step);
            }
        }

        private void AdvanceOneTick(float elapsedSeconds)
        {
            foreach (State s in states)
            {
                VillagerData v = s.Villager;
                if (v == null || !v.Initialized || !v.IsAlive) continue;

                // M2.4B owns the recovery phase. Do not consume idle/movement rates
                // during Eating/Resting: otherwise M2.3 and M2.4B fight each other.
                if (v.Action == VillagerAction.Eating || v.Action == VillagerAction.Resting)
                {
                    s.HungerTime = 0f;
                    s.EnergyTime = 0f;
                    s.MoodTime = 0f;
                    continue;
                }

                bool moving = v.Action == VillagerAction.Moving;
                int food = v.Satiety;
                int energy = v.Energy;
                int mood = v.Happiness;

                // We do not simulate food magically appearing or rest automatically
                // restoring energy before Kitchen and Hut have been implemented.
                s.HungerTime += elapsedSeconds;
                float hungerRate = moving ? hungerSecondsMoving : hungerSecondsIdle;
                while (s.HungerTime >= hungerRate)
                {
                    s.HungerTime -= hungerRate;
                    food = Mathf.Max(0, food - 1);
                }

                s.EnergyTime += elapsedSeconds;
                float energyRate = moving ? energySecondsMoving : energySecondsIdle;
                while (s.EnergyTime >= energyRate)
                {
                    s.EnergyTime -= energyRate;
                    energy = Mathf.Max(0, energy - 1);
                }

                if (food <= lowFoodThreshold || energy <= lowEnergyThreshold)
                {
                    s.MoodTime += elapsedSeconds;
                    while (s.MoodTime >= lowNeedsMoodSeconds)
                    {
                        s.MoodTime -= lowNeedsMoodSeconds;
                        mood = Mathf.Max(0, mood - 1);
                    }
                }
                else s.MoodTime = 0f;

                // SetNeeds already clamps Food to 0..100, Energy/Mood to 0..99.
                if (food != v.Satiety || energy != v.Energy || mood != v.Happiness)
                    v.SetNeeds(food, energy, mood);

                if (logFirstLowNeedWarning)
                {
                    if (food <= lowFoodThreshold && !s.LoggedLowFood)
                    {
                        Debug.Log("[TribalOrigins] M2.3 LOW FOOD: " + v.DisplayName +
                            " (" + food + "). M2.4B may seek a TEST Kitchen if enabled.", this);
                        s.LoggedLowFood = true;
                    }
                    if (energy <= lowEnergyThreshold && !s.LoggedLowEnergy)
                    {
                        Debug.Log("[TribalOrigins] M2.3 LOW ENERGY: " + v.DisplayName +
                            " (" + energy + "). M2.4B may seek a TEST Hut if enabled.", this);
                        s.LoggedLowEnergy = true;
                    }
                }
            }
        }

        [ContextMenu("M2.3 TEST: Villager 01 low food & energy")]
        private void TestLowNeeds()
        {
            if (!Application.isPlaying || !ready)
            {
                Debug.LogWarning("[TribalOrigins] Enter Play Mode and wait for M2.3 READY first.", this);
                return;
            }
            State first = states.Find(s => s.Villager != null && s.Villager.Id == 1);
            if (first == null) return;
            first.Villager.SetNeeds(15, 12, first.Villager.Happiness);
            first.LoggedLowFood = false;
            first.LoggedLowEnergy = false;
            Debug.Log("[TribalOrigins] M2.3 TEST: set Villager 01 Food=15, Rest=12 in Play Mode.", this);
        }

        [ContextMenu("M2.3 TEST: Reset needs to prototype defaults")]
        private void ResetNeeds()
        {
            if (!Application.isPlaying || !ready)
            {
                Debug.LogWarning("[TribalOrigins] Enter Play Mode and wait for M2.3 READY first.", this);
                return;
            }
            foreach (State s in states)
            {
                if (s.Villager == null) continue;
                s.Villager.SetNeeds(100, 99, 70);
                s.HungerTime = s.EnergyTime = s.MoodTime = 0;
                s.LoggedLowFood = s.LoggedLowEnergy = false;
            }
            Debug.Log("[TribalOrigins] M2.3 TEST: restored all 9 prototype needs.", this);
        }
    }
}
