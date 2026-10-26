using System;
using UnityEngine;

namespace TribalOrigins.Units
{
    // Gameplay categories for Tribal Origins, NOT Java ME numeric IDs.
    public enum VillagerGender { Unverified = 0, Male = 1, Female = 2 }
    public enum VillagerLifeStage { Child = 0, Adult = 1, Elder = 2 }
    public enum VillagerSkill
    {
        Melee = 0, Ranged = 1, Scouting = 2,
        Harvesting = 3, Weaponsmithing = 4, Cooking = 5,
        Djing = 6, Witchcraft = 7, Building = 8
    }
    public enum VillagerJob
    {
        Unassigned = 0, Builder = 1, GatherFood = 2, GatherWood = 3,
        GatherStone = 4, GatherBones = 5, Cook = 6, Armourer = 7,
        Fighter = 8, Scout = 9, Shaman = 10, DJ = 11
    }
    public enum VillagerAction
    {
        Idle = 0, Moving = 1, Working = 2, Eating = 3,
        Resting = 4, Courting = 5, Fighting = 6, Dead = 7
    }

    /// <summary>
    /// M2.1 data on ONE of the 9 already-created M1.7 markers.
    /// No spawning, tick-based aging or automatic need decay here.
    /// Game-specific numerical defaults below are PROTOTYPES, not proven THQ values.
    /// Source: tribes-remake: Systems/Entities/EntityNumbers.cs, UnitAges.Adult=12, Elder=35;
    /// World/Unit.cs; client/src/Match/Hud/UnitCard.cs (nine skills).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerData : MonoBehaviour
    {
        public const int AdultAge = 12;
        public const int ElderAge = 35;

        [Header("Identity / Level 1 source seed")]
        [SerializeField] private int villagerId;
        [SerializeField] private string displayName = "";
        [SerializeField, Min(0)] private int age;
        [SerializeField] private VillagerGender gender = VillagerGender.Unverified;
        [SerializeField] private bool isChief;

        [Header("Health & needs - PROTOTYPE defaults")]
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int health = 100;
        [SerializeField, Range(0, 100)] private int satiety = 100;
        [SerializeField, Range(0, 99)] private int energy = 99;
        [SerializeField, Range(0, 99)] private int happiness = 70;

        [Header("Nine gameplay skills - values initially UNVERIFIED")]
        [SerializeField, Range(0, 99)] private int melee;
        [SerializeField, Range(0, 99)] private int ranged;
        [SerializeField, Range(0, 99)] private int scouting;
        [SerializeField, Range(0, 99)] private int harvesting;
        [SerializeField, Range(0, 99)] private int weaponsmithing;
        [SerializeField, Range(0, 99)] private int cooking;
        [SerializeField, Range(0, 99)] private int djing;
        [SerializeField, Range(0, 99)] private int witchcraft;
        [SerializeField, Range(0, 99)] private int building;

        [Header("Combat & temporary defense effect")]
        [SerializeField] private bool protectedStatus;
        [SerializeField, Min(0)] private int protectionTicks;
        [SerializeField] private int equippedWeaponId = -1; // -1 = unknown/unassigned prototype

        [Header("Job and current action are independent")]
        [SerializeField] private VillagerJob currentJob = VillagerJob.Unassigned;
        [SerializeField] private VillagerAction currentAction = VillagerAction.Idle;

        [Header("Diagnostics")]
        [SerializeField] private bool initialized;

        public int Id => villagerId;
        public string DisplayName => displayName;
        public int Age => age;
        public VillagerGender Gender => gender;
        public VillagerLifeStage LifeStage => age < AdultAge ? VillagerLifeStage.Child :
            (age >= ElderAge ? VillagerLifeStage.Elder : VillagerLifeStage.Adult);
        public bool IsAdult => IsAdultAge(age);
        public bool IsChief => isChief;
        public bool IsAlive => health > 0;
        public int Health => health;
        public int MaxHealth => maxHealth;
        public int Satiety => satiety;
        public int Energy => energy;
        public int Happiness => happiness;
        public int EquippedWeaponId => equippedWeaponId;
        public bool IsProtected => protectedStatus && protectionTicks > 0;
        public int ProtectionTicks => protectionTicks;
        public VillagerJob Job => currentJob;
        public VillagerAction Action => currentAction;
        public bool Initialized => initialized;

        public static bool IsAdultAge(int years) => years >= AdultAge;

        /// <summary>Used by existing M1.7/M2.1 bridge; safely initializes each marker only once.</summary>
        public void InitializeFromSourceSeed(int id, int sourceAge, bool chief,
            int prototypeMaxHealth, int prototypeSatiety,
            int prototypeEnergy, int prototypeHappiness)
        {
            if (initialized)
                throw new InvalidOperationException("Already initialized: " + name);
            if (id < 1 || id > 9 || sourceAge < 0 || (chief && sourceAge < AdultAge))
                throw new ArgumentOutOfRangeException(nameof(id), "Invalid level 1 debug seed.");

            villagerId = id;
            displayName = "Villager " + id.ToString("00");
            age = sourceAge;
            gender = VillagerGender.Unverified; // No fabricated gender/portrait mapping.
            isChief = chief;
            maxHealth = Mathf.Max(1, prototypeMaxHealth);
            health = maxHealth;
            satiety = Mathf.Clamp(prototypeSatiety, 0, 100);
            energy = Mathf.Clamp(prototypeEnergy, 0, 99);
            happiness = Mathf.Clamp(prototypeHappiness, 0, 99);
            melee = ranged = scouting = harvesting = weaponsmithing = cooking = djing = witchcraft = building = 0;
            equippedWeaponId = -1;
            protectedStatus = false;
            protectionTicks = 0;
            currentJob = VillagerJob.Unassigned;
            currentAction = VillagerAction.Idle;
            initialized = true;
        }

        public void SetDisplayName(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) displayName = value.Trim();
        }

        public void SetVerifiedGender(VillagerGender value)
        {
            if (value == VillagerGender.Unverified) return;
            gender = value;
        }

        public void SetJob(VillagerJob job)
        {
            if (IsAlive) currentJob = job;
        }

        public void SetAction(VillagerAction action)
        {
            if (IsAlive) currentAction = action;
        }

        public void SetNeeds(int newSatiety, int newEnergy, int newHappiness)
        {
            if (!initialized) return;
            satiety = Mathf.Clamp(newSatiety, 0, 100);
            energy = Mathf.Clamp(newEnergy, 0, 99);
            happiness = Mathf.Clamp(newHappiness, 0, 99);
        }

        public int GetSkill(VillagerSkill skill)
        {
            switch (skill)
            {
                case VillagerSkill.Melee: return melee;
                case VillagerSkill.Ranged: return ranged;
                case VillagerSkill.Scouting: return scouting;
                case VillagerSkill.Harvesting: return harvesting;
                case VillagerSkill.Weaponsmithing: return weaponsmithing;
                case VillagerSkill.Cooking: return cooking;
                case VillagerSkill.Djing: return djing;
                case VillagerSkill.Witchcraft: return witchcraft;
                case VillagerSkill.Building: return building;
                default: throw new ArgumentOutOfRangeException(nameof(skill));
            }
        }

        /// <summary>Prototype skill setter for later training and debug tools.</summary>
        public void SetSkill(VillagerSkill skill, int value)
        {
            int v = Mathf.Clamp(value, 0, 99);
            switch (skill)
            {
                case VillagerSkill.Melee: melee = v; break;
                case VillagerSkill.Ranged: ranged = v; break;
                case VillagerSkill.Scouting: scouting = v; break;
                case VillagerSkill.Harvesting: harvesting = v; break;
                case VillagerSkill.Weaponsmithing: weaponsmithing = v; break;
                case VillagerSkill.Cooking: cooking = v; break;
                case VillagerSkill.Djing: djing = v; break;
                case VillagerSkill.Witchcraft: witchcraft = v; break;
                case VillagerSkill.Building: building = v; break;
                default: throw new ArgumentOutOfRangeException(nameof(skill));
            }
        }

        // The reference implementation models defense as Protected status, not a tenth skill.
        // Damage reduction in the remake's CellDamage.cs is halve(d) = d > 1 ? d / 2 : d.
        public static int ComputeProtectedDamage(int rawDamage, bool isProtected)
        {
            if (rawDamage <= 0) return 0;
            return isProtected && rawDamage > 1 ? rawDamage / 2 : rawDamage;
        }

        public void ApplyProtection(int durationInSimulationTicks)
        {
            if (!initialized || !IsAlive) return;
            protectionTicks = Mathf.Max(0, durationInSimulationTicks);
            protectedStatus = protectionTicks > 0;
        }

        /// <summary>Call ONE time per future simulation tick, not in Update every rendered frame.</summary>
        public void AdvanceProtectionTick()
        {
            if (protectionTicks > 0) protectionTicks--;
            if (protectionTicks <= 0)
            {
                protectionTicks = 0;
                protectedStatus = false;
            }
        }

        /// <summary>For future combat tests; excludes attacker's skill/range/equipment formulas.</summary>
        public int ReceiveDamage(int rawDamage)
        {
            if (!initialized || !IsAlive) return 0;
            int damage = ComputeProtectedDamage(rawDamage, IsProtected);
            health = Mathf.Max(0, health - damage);
            if (health == 0)
            {
                currentAction = VillagerAction.Dead;
                protectedStatus = false;
                protectionTicks = 0;
            }
            return damage;
        }

        public void Heal(int amount)
        {
            if (!initialized || !IsAlive || amount <= 0) return;
            health = Mathf.Min(maxHealth, health + amount);
        }

        // Age is intentionally not incremented automatically in M2.1.
        public void SetAgeForTesting(int years)
        {
            age = Mathf.Max(0, years);
        }
    }
}
