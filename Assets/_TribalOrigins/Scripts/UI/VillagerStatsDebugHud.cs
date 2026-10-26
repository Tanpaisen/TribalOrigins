using TribalOrigins.Units;
using UnityEngine;

namespace TribalOrigins.UI
{
    /// <summary>
    /// Optional read-only debug panel for the person selected by M1.10 A*.
    /// No new mouse handler, no movement override and no THQ art assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerStatsDebugHud : MonoBehaviour
    {
        [SerializeField] private bool showPanel = true;
        [SerializeField] private Level01AStarControls aStarControls;
        [SerializeField] private float panelWidth = 295f;

        private void Awake()
        {
            if (aStarControls == null) aStarControls = GetComponent<Level01AStarControls>();
            if (aStarControls == null)
                Debug.LogWarning("[TribalOrigins] VillagerStatsDebugHud must be on WorldMap with Level01AStarControls.", this);
        }

        private void OnGUI()
        {
            if (!showPanel || aStarControls == null) return;
            Transform selected = aStarControls.SelectedVillager;
            if (selected == null) return;

            VillagerData v = selected.GetComponent<VillagerData>();
            if (v == null || !v.Initialized) return;

            float width = Mathf.Min(Mathf.Max(panelWidth, 220f), Mathf.Max(220f, Screen.width - 16f));
            const float height = 295f;
            Rect panel = new Rect(Mathf.Max(8, Screen.width - width - 8),
                Mathf.Max(90, Screen.height - height - 8), width, height);
            GUI.Box(panel, "TRIBAL ORIGINS  |  VILLAGER DEBUG");
            GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 24, panel.width - 20, panel.height - 32));
            GUILayout.Label(v.DisplayName + " | ID " + v.Id + (v.IsChief ? " | CHIEF" : ""));
            GUILayout.Label("Age: " + v.Age + "  |  Stage: " + v.LifeStage + "  |  " + v.Gender);
            GUILayout.Label("HP: " + v.Health + "/" + v.MaxHealth +
                            " | Food: " + v.Satiety + " | Energy: " + v.Energy);
            GUILayout.Label("Happiness: " + v.Happiness +
                            " | Defense: " + (v.IsProtected ? "Protected (" + v.ProtectionTicks + ")" : "Normal"));
            GUILayout.Label("Job: " + v.Job + " | Action: " + v.Action);
            GUILayout.Space(5f);
            GUILayout.Label("9 Skills (prototype values)");
            DrawTwo("Melee", v.GetSkill(VillagerSkill.Melee), "Ranged", v.GetSkill(VillagerSkill.Ranged));
            DrawTwo("Scouting", v.GetSkill(VillagerSkill.Scouting), "Harvesting", v.GetSkill(VillagerSkill.Harvesting));
            DrawTwo("Weaponsmith", v.GetSkill(VillagerSkill.Weaponsmithing), "Cooking", v.GetSkill(VillagerSkill.Cooking));
            DrawTwo("DJ", v.GetSkill(VillagerSkill.Djing), "Witchcraft", v.GetSkill(VillagerSkill.Witchcraft));
            GUILayout.Label("Building: " + v.GetSkill(VillagerSkill.Building));
            GUILayout.EndArea();
        }

        private static void DrawTwo(string leftName, int leftValue, string rightName, int rightValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(leftName + ": " + leftValue, GUILayout.Width(144f));
            GUILayout.Label(rightName + ": " + rightValue);
            GUILayout.EndHorizontal();
        }
    }
}
