using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TribalOrigins.Units;
using UnityEngine;

namespace TribalOrigins.Core
{
    /// <summary>
    /// M2.1 COMPLETE: attach VillagerData to the NINE already-generated M1.7 markers.
    /// No duplicate villagers are created and M1.10 A* remains in control of movement.
    /// This is a temporary bridge until we replace the debug-spawn system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01SpawnDebug))]
    public sealed class Level01VillagerDataBootstrap : MonoBehaviour
    {
        private const string SpawnRootName = "Generated_Initial_Entities_DEBUG";
        private const int ExpectedVillagers = 9;

        [Header("M2.1 PROTOTYPE defaults, not verified original THQ stats")]
        [SerializeField, Min(1)] private int defaultMaxHealth = 100;
        [SerializeField, Range(0, 100)] private int defaultSatiety = 100;
        [SerializeField, Range(0, 100)] private int defaultEnergy = 100;
        [SerializeField, Range(0, 100)] private int defaultHappiness = 70;

        [Header("Diagnostics")]
        [SerializeField] private bool printSummary = true;

        private struct Record
        {
            public Transform Transform;
            public int Id;
            public int Age;
            public bool IsChief;
        }

        private IEnumerator Start()
        {
            // Verified from tribes-remake EntityNumbers.cs: Adult=12, Elder=35.
            // Defense is Protected status, not a tenth trained skill.
            if (VillagerData.IsAdultAge(11) || !VillagerData.IsAdultAge(12) ||
                VillagerData.ComputeProtectedDamage(9, true) != 4 ||
                VillagerData.ComputeProtectedDamage(1, true) != 1)
            {
                Debug.LogError("[TribalOrigins] M2.1 RULE SELF-TEST FAILED", this);
                yield break;
            }
            Debug.Log("[TribalOrigins] M2.1 RULE SELF-TEST PASS: adult>=12, protection halves damage", this);
            yield return InitializeExistingMarkers();
        }

        private IEnumerator InitializeExistingMarkers()
        {
            Transform spawnRoot = null;
            // Level01SpawnDebug builds the scene after waiting one frame.
            for (int attempt = 0; attempt < 60; attempt++)
            {
                spawnRoot = transform.Find(SpawnRootName);
                if (spawnRoot != null && spawnRoot.childCount >= ExpectedVillagers + 1)
                    break; // Includes the Chief's Hut debug marker.
                yield return null;
            }

            if (spawnRoot == null || spawnRoot.childCount < ExpectedVillagers + 1)
            {
                Debug.LogError("[TribalOrigins] M2.1 FAILED: initial spawn markers not ready. " +
                    "Check Level01SpawnDebug and the M1.6 terrain first.", this);
                yield break;
            }

            var records = new List<Record>();
            var seenIds = new HashSet<int>();
            int chiefCount = 0;
            foreach (Transform child in spawnRoot)
            {
                if (!child.name.StartsWith("Villager_", StringComparison.Ordinal))
                    continue;

                if (!TryParseDebugName(child.name, out int id, out int age, out bool chief))
                {
                    Debug.LogError("[TribalOrigins] M2.1 FAILED: cannot parse " + child.name, this);
                    yield break;
                }
                if (id < 1 || id > ExpectedVillagers || !seenIds.Add(id))
                {
                    Debug.LogError("[TribalOrigins] M2.1 FAILED: duplicate/out-of-range villager ID " + id, this);
                    yield break;
                }
                if (chief) chiefCount++;
                records.Add(new Record { Transform = child, Id = id, Age = age, IsChief = chief });
            }

            if (records.Count != ExpectedVillagers || chiefCount != 1)
            {
                Debug.LogError("[TribalOrigins] M2.1 FAILED: expected 9 villagers and exactly 1 chief; got " +
                    records.Count + " villagers, " + chiefCount + " chiefs.", this);
                yield break;
            }

            // Validate 9 unique seeds, no duplicate character, and the chief must be an adult.
            foreach (Record record in records)
            {
                if (record.IsChief && !VillagerData.IsAdultAge(record.Age))
                {
                    Debug.LogError("[TribalOrigins] M2.1 FAILED: child cannot be chief", this);
                    yield break;
                }
            }
            // Validate ALL records before adding components to any object.
            foreach (Record record in records)
            {
                var data = record.Transform.GetComponent<VillagerData>();
                if (data == null) data = record.Transform.gameObject.AddComponent<VillagerData>();
                if (!data.Initialized)
                    data.InitializeFromSourceSeed(record.Id, record.Age, record.IsChief,
                        defaultMaxHealth, defaultSatiety, defaultEnergy, defaultHappiness);
            }

            if (printSummary)
                Debug.Log("[TribalOrigins] M2.1 COMPLETE READY: 9 VillagerData; adult threshold=12; " +
                    "9 skill fields; Protected effect; chief=#1; genders/skill values unverified; " +
                    "needs are PROTOTYPE values, no automatic decay; M1.10 A* preserved.", this);
        }

        // Name format comes from the M1.7 temporary debug spawner:
        // Villager_01_Age_35_Chief or Villager_02_Age_20
        private static bool TryParseDebugName(string name, out int id, out int age, out bool chief)
        {
            id = -1;
            age = -1;
            chief = false;
            string[] pieces = name.Split('_');
            if (pieces.Length < 4 || pieces.Length > 5 ||
                pieces[0] != "Villager" || pieces[2] != "Age" ||
                !int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id) ||
                !int.TryParse(pieces[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out age))
                return false;

            chief = pieces.Length == 5 && pieces[4] == "Chief";
            return id >= 1 && id <= ExpectedVillagers && age >= 0 && (!chief || id == 1) &&
                   (pieces.Length != 5 || chief) && (id != 1 || chief);
        }
    }
}
