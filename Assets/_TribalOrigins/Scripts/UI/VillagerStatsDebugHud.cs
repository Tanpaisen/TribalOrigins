using System;
using System.Collections.Generic;
using TribalOrigins.Units;
using UnityEngine;
using UnityEngine.UI;

namespace TribalOrigins.UI
{
    /// <summary>
    /// M2.2 - compact, parchment-coloured RTS villager HUD inspired by the layout of
    /// Prehistoric Tribes (THQ). Original procedurally drawn graphics: no THQ assets.
    ///
    /// The HUD is a real Screen Space Canvas, not IMGUI/OnGUI; it never creates
    /// villagers, consumes world mouse clicks or alters movement/combat logic.
    /// Current numbers are M2.1 prototype data until verified against THQ 0.0.20.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerStatsDebugHud : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private bool showPanel = true;
        [SerializeField] private Level01AStarControls aStarControls;
        [SerializeField] private Font optionalFont;
        [SerializeField] private Sprite optionalPortrait;
        [SerializeField] private bool showPrototypeNotice = true;

        private const int SkillCount = 9;
        private readonly List<UnityEngine.Object> generatedAssets = new List<UnityEngine.Object>();

        private GameObject canvasObject;
        private GameObject panelObject;
        private Image portraitImage;
        private Image chiefMark;
        private Text chiefMarkText;
        private Text nameLabel;
        private Text identityLabel;
        private Text moodLabel;
        private Text jobLabel;
        private Text actionLabel;
        private Text protectionLabel;
        private Text prototypeLabel;
        private readonly Text[] statValues = new Text[4];
        private readonly RectTransform[] statFills = new RectTransform[4];
        private readonly Text[] skillValues = new Text[SkillCount];
        private VillagerData currentVillager;
        private Sprite genericPortrait;

        private static readonly Color Ink = Rgb(59, 41, 27);
        private static readonly Color FadedInk = Rgb(109, 76, 45);
        private static readonly Color DarkBorder = Rgb(81, 55, 28);
        private static readonly Color SubtleDivider = new Color(0.52f, 0.37f, 0.18f, 0.6f);
        private static readonly Color HealthFill = Rgb(174, 38, 33);
        private static readonly Color FoodFill = Rgb(180, 112, 44);
        private static readonly Color EnergyFill = Rgb(29, 133, 139);
        private static readonly Color HappinessFill = Rgb(191, 157, 31);
        private static readonly Color BarBack = Rgb(122, 98, 66);

        private static readonly string[] SkillNames =
        {
            "MELEE", "RANGED", "SCOUT", "GATHER", "SMITH", "COOK",
            "DANCE", "MAGIC", "BUILD"
        };

        private void Awake()
        {
            if (aStarControls == null)
                aStarControls = GetComponent<Level01AStarControls>();

            if (aStarControls == null)
            {
                Debug.LogError("[TribalOrigins] M2.2: Add this HUD to WorldMap with Level01AStarControls.", this);
                enabled = false;
                return;
            }

            Font font = optionalFont;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Debug.LogError("[TribalOrigins] M2.2: UI font not available. Assign Optional Font in Inspector.", this);
                enabled = false;
                return;
            }

            BuildCanvas(font);
            panelObject.SetActive(false);
            Debug.Log("[TribalOrigins] M2.2 ORIGINAL-STYLE HUD READY: Canvas, 4 live stats, 9 skills, age >= 12.", this);
        }

        private void LateUpdate()
        {
            if (panelObject == null)
                return;

            Transform selected = aStarControls != null ? aStarControls.SelectedVillager : null;
            if (!showPanel || selected == null)
            {
                currentVillager = null;
                if (panelObject.activeSelf) panelObject.SetActive(false);
                return;
            }

            VillagerData data = selected.GetComponent<VillagerData>();
            if (data == null || !data.Initialized)
            {
                currentVillager = null;
                if (panelObject.activeSelf) panelObject.SetActive(false);
                return;
            }

            if (!panelObject.activeSelf) panelObject.SetActive(true);
            if (currentVillager != data)
            {
                currentVillager = data;
                portraitImage.sprite = optionalPortrait != null ? optionalPortrait : genericPortrait;
            }

            // M1.10 sets Action=Moving when issuing an A* route, then Idle at arrival.
            // The labels update every rendered frame while selected, without changing
            // the original villager's source values.
            Refresh(data);
        }

        // Compact 16:9 layout, designed around 760 reference pixels of width.
        // Keeps the parchment bar readable when Game View is docked in the Editor.
        private void BuildCanvas(Font font)
        {
            canvasObject = new GameObject("M2_2_VillagerHudCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(null, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(760f, 428f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // Avoid tiny text when Game View is short.

            // No GraphicRaycaster: A* owns gameplay mouse input.
            panelObject = new GameObject("OriginalStyle_VillagerPanel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 1f);
            panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -8f);
            panel.sizeDelta = new Vector2(738f, 158f);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = CreateParchment();
            panelImage.raycastTarget = false;

            // Region 1: Portrait and identity (x 0..241).
            AddSolid(panel, "PortraitFrame", 11f, 20f, 76f, 96f, DarkBorder);
            AddSolid(panel, "PortraitInset", 14f, 23f, 70f, 90f, Rgb(113, 98, 62));
            genericPortrait = CreateFace();
            portraitImage = AddImage(panel, "VillagerPortrait", genericPortrait,
                16f, 25f, 66f, 86f, Color.white);
            chiefMark = AddSolid(panel, "ChiefMark", 65f, 20f, 21f, 19f,
                Rgb(132, 96, 26));
            chiefMarkText = CreateText(panel, "ChiefMarkText", font, "C", 12,
                FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, 65f, 20f, 21f, 19f);

            nameLabel = CreateText(panel, "Name", font, "VILLAGER", 17,
                FontStyle.Bold, Ink, TextAnchor.MiddleLeft, 94f, 15f, 136f, 25f);
            nameLabel.resizeTextForBestFit = true;
            nameLabel.resizeTextMinSize = 11;
            nameLabel.resizeTextMaxSize = 17;
            identityLabel = CreateText(panel, "Identity", font, "AGE 12 / ?", 12,
                FontStyle.Bold, Ink, TextAnchor.MiddleLeft, 94f, 40f, 139f, 19f);
            moodLabel = CreateText(panel, "Mood", font, "MOOD --", 12,
                FontStyle.Bold, FadedInk, TextAnchor.MiddleLeft, 94f, 61f, 139f, 18f);
            jobLabel = CreateText(panel, "Job", font, "JOB: UNASSIGNED", 11,
                FontStyle.Bold, Ink, TextAnchor.MiddleLeft, 94f, 85f, 139f, 17f);
            actionLabel = CreateText(panel, "Action", font, "ACTION: IDLE", 11,
                FontStyle.Bold, Ink, TextAnchor.MiddleLeft, 94f, 108f, 139f, 17f);
            protectionLabel = CreateText(panel, "Protection", font, "DEFENSE: NORMAL", 11,
                FontStyle.Bold, DarkBorder, TextAnchor.MiddleLeft, 14f, 134f, 215f, 17f);

            AddSolid(panel, "Divider1", 240f, 12f, 2f, 136f, SubtleDivider);
            AddSolid(panel, "Divider2", 480f, 12f, 2f, 136f, SubtleDivider);

            // Region 2: Four live bars with legible labels and values (x 250..472).
            AddVitalsRow(panel, font, 0, "HP", HealthFill, 254f, 19f);
            AddVitalsRow(panel, font, 1, "FOOD", FoodFill, 254f, 45f);
            AddVitalsRow(panel, font, 2, "REST", EnergyFill, 254f, 71f);
            AddVitalsRow(panel, font, 3, "HAPPY", HappinessFill, 254f, 97f);
            prototypeLabel = CreateText(panel, "PrototypeNotice", font,
                "VALUES: PROTOTYPE", 11, FontStyle.Bold, FadedInk,
                TextAnchor.MiddleLeft, 254f, 133f, 218f, 18f);
            prototypeLabel.gameObject.SetActive(showPrototypeNotice);

            // Region 3: Skill icon, abbreviated name, numeric score.
            // Two columns x five rows, matching the original's dense RTS HUD.
            CreateText(panel, "SkillsHeader", font, "9 SKILLS", 12,
                FontStyle.Bold, FadedInk, TextAnchor.MiddleLeft,
                495f, 7f, 200f, 16f);
            for (int i = 0; i < SkillCount; ++i)
            {
                int col = i % 2;
                int row = i / 2;
                float x = 495f + 114f * col;
                float y = 26f + 25f * row;
                AddImage(panel, "SkillIcon_" + i, CreateSkillIcon((VillagerSkill)i),
                    x, y, 18f, 18f, Color.white);
                CreateText(panel, "SkillName_" + i, font, SkillNames[i], 11,
                    FontStyle.Bold, Ink, TextAnchor.MiddleLeft,
                    x + 22f, y - 1f, 59f, 20f);
                skillValues[i] = CreateText(panel, "SkillValue_" + i, font, "0", 12,
                    FontStyle.Bold, DarkBorder, TextAnchor.MiddleRight,
                    x + 82f, y - 1f, 27f, 20f);
            }
        }

        private void AddVitalsRow(Transform parent, Font font, int index, string label,
            Color fillColour, float x, float y)
        {
            CreateText(parent, label + "_Label", font, label, 13,
                FontStyle.Bold, Ink, TextAnchor.MiddleLeft, x, y, 51f, 20f);
            AddSolid(parent, label + "_Back", x + 53f, y + 4f, 106f, 13f, BarBack);
            AddSolid(parent, label + "_Track", x + 55f, y + 6f, 102f, 9f,
                Rgb(217, 200, 149));
            Image fill = AddSolid(parent, label + "_Fill", x + 55f, y + 6f,
                102f, 9f, fillColour);
            statFills[index] = fill.rectTransform;
            statValues[index] = CreateText(parent, label + "_Value", font,
                "0", 12, FontStyle.Bold, Ink, TextAnchor.MiddleRight,
                x + 165f, y, 45f, 20f);
        }

        private void Refresh(VillagerData v)
        {
            Set(nameLabel, v.DisplayName.ToUpperInvariant());
            Set(identityLabel, "AGE " + v.Age + "  /  " + GenderCode(v.Gender) +
                "  /  " + StageCode(v.LifeStage));
            Set(moodLabel, "MOOD " + v.Happiness + "   " + (v.IsChief ? "CHIEF" : "VILLAGER"));
            Set(jobLabel, "JOB: " + v.Job.ToString().ToUpperInvariant());
            Set(actionLabel, "ACTION: " + v.Action.ToString().ToUpperInvariant());
            chiefMark.enabled = v.IsChief;
            chiefMarkText.enabled = v.IsChief;
            SetStat(0, v.Health, v.MaxHealth);
            SetStat(1, v.Satiety, 100);
            SetStat(2, v.Energy, 99);
            SetStat(3, v.Happiness, 99);
            for (int i = 0; i < SkillCount; ++i)
                Set(skillValues[i], v.GetSkill((VillagerSkill)i).ToString());
            Set(protectionLabel, v.IsProtected
                ? "DEFENSE: PROTECTED (" + v.ProtectionTicks + ")"
                : "DEFENSE: NORMAL");
        }

        private void SetStat(int index, int amount, int maximum)
        {
            float fraction = maximum > 0 ? Mathf.Clamp01(amount / (float)maximum) : 0f;
            // The fill starts at a left/top anchor, so only the right edge moves.
            RectTransform t = statFills[index];
            Vector2 size = t.sizeDelta;
            float desired = 102f * fraction;
            if (!Mathf.Approximately(size.x, desired))
                t.sizeDelta = new Vector2(desired, size.y);
            Set(statValues[index], amount.ToString());
        }

        private static void Set(Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        private static string GenderCode(VillagerGender gender)
        {
            switch (gender)
            {
                case VillagerGender.Male: return "M";
                case VillagerGender.Female: return "F";
                default: return "?";
            }
        }

        private static string StageCode(VillagerLifeStage stage)
        {
            switch (stage)
            {
                case VillagerLifeStage.Child: return "CHILD";
                case VillagerLifeStage.Adult: return "ADULT";
                default: return "ELDER";
            }
        }

        private static RectTransform Rect(Transform parent, GameObject child,
            float x, float y, float w, float h)
        {
            child.transform.SetParent(parent, false);
            RectTransform r = child.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static Text CreateText(Transform parent, string name, Font font,
            string value, int size, FontStyle fontStyle, Color colour,
            TextAnchor alignment, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            Rect(parent, go, x, y, w, h);
            Text label = go.GetComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.fontStyle = fontStyle;
            label.color = colour;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static Image AddSolid(Transform parent, string name,
            float x, float y, float w, float h, Color colour)
        {
            GameObject go = new GameObject(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            Rect(parent, go, x, y, w, h);
            Image image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite,
            float x, float y, float w, float h, Color tint)
        {
            Image image = AddSolid(parent, name, x, y, w, h, tint);
            image.sprite = sprite;
            image.preserveAspect = false;
            return image;
        }

        private Sprite CreateParchment()
        {
            const int width = 512;
            const int height = 80;
            Texture2D t = NewTexture(width, height);
            Color32 light = new Color32(228, 202, 135, 255);
            Color32 dark = new Color32(186, 148, 77, 255);
            Color32 edge = new Color32(98, 72, 39, 255);
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    uint hash = (uint)(x * 374761393u + y * 668265263u);
                    hash ^= hash >> 13;
                    hash *= 1274126177u;
                    byte noise = (byte)((hash >> 19) & 15u);
                    int ragged = 3 + ((x / 7) % 4);
                    if (y < ragged && y < 5 && ((x / 5) % 11 == 2)) ragged += 1;
                    if (y < ragged)
                    {
                        t.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    float blend = Mathf.InverseLerp(0f, height, y) * 0.28f;
                    Color result = Color.Lerp(light, dark, blend);
                    result *= 0.95f + noise * 0.005f;
                    if (x < 3 || x > width - 4 || y >= height - 3 || y < ragged + 2)
                        result = edge;
                    t.SetPixel(x, y, result);
                }
            }
            t.Apply(false, false);
            return SpriteFrom(t, 100f);
        }

        private Sprite CreateFace()
        {
            Texture2D t = NewTexture(32, 32);
            Color32 skin = new Color32(202, 150, 90, 255);
            Color32 hair = new Color32(51, 41, 27, 255);
            Color32 shadow = new Color32(137, 86, 51, 255);
            Color32 eyes = new Color32(34, 28, 24, 255);
            Color32 shirt = new Color32(91, 108, 60, 255);
            FillRect(t, 0, 0, 32, 32, new Color32(125, 113, 76, 255));
            FillRect(t, 2, 2, 28, 28, new Color32(172, 161, 105, 255));
            FillRect(t, 7, 1, 18, 7, hair);
            FillRect(t, 5, 7, 22, 17, hair);
            FillRect(t, 7, 8, 18, 17, skin);
            FillRect(t, 7, 8, 3, 13, shadow);
            FillRect(t, 11, 16, 3, 2, eyes);
            FillRect(t, 20, 16, 3, 2, eyes);
            FillRect(t, 14, 11, 7, 2, shadow);
            FillRect(t, 12, 6, 14, 4, hair);
            FillRect(t, 8, 0, 17, 5, shirt);
            FillRect(t, 13, 7, 7, 2, shadow);
            t.Apply(false, false);
            return SpriteFrom(t, 32f);
        }

        private Sprite CreateSkillIcon(VillagerSkill skill)
        {
            Texture2D t = NewTexture(16, 16);
            Color32 brown = new Color32(75, 50, 32, 255);
            Color32 iron = new Color32(110, 116, 117, 255);
            Color32 pale = new Color32(224, 215, 173, 255);
            Color32 green = new Color32(52, 106, 49, 255);
            Color32 red = new Color32(166, 55, 39, 255);
            switch (skill)
            {
                case VillagerSkill.Melee:
                    Line(t, 3, 3, 12, 12, iron, 2); Line(t, 2, 2, 5, 5, pale, 1);
                    Line(t, 9, 5, 13, 1, brown, 2); break;
                case VillagerSkill.Ranged:
                    Line(t, 5, 2, 2, 7, brown, 2); Line(t, 2, 7, 5, 13, brown, 2);
                    Line(t, 5, 2, 5, 13, iron, 1); Line(t, 3, 8, 14, 8, pale, 1); break;
                case VillagerSkill.Scouting:
                    Line(t, 2, 8, 7, 12, brown, 1); Line(t, 7, 12, 13, 8, brown, 1);
                    Line(t, 2, 8, 7, 4, brown, 1); Line(t, 7, 4, 13, 8, brown, 1);
                    FillRect(t, 7, 7, 3, 3, red); break;
                case VillagerSkill.Harvesting:
                    FillRect(t, 7, 1, 2, 13, brown); FillRect(t, 4, 7, 9, 4, green);
                    FillRect(t, 5, 10, 7, 3, green); FillRect(t, 6, 13, 5, 2, green); break;
                case VillagerSkill.Weaponsmithing:
                    FillRect(t, 3, 10, 10, 3, iron); FillRect(t, 7, 3, 2, 9, brown);
                    FillRect(t, 2, 3, 8, 3, iron); break;
                case VillagerSkill.Cooking:
                    FillRect(t, 3, 6, 10, 7, brown); FillRect(t, 5, 3, 6, 3, iron);
                    FillRect(t, 2, 7, 2, 3, iron); FillRect(t, 12, 7, 2, 3, iron); break;
                case VillagerSkill.Djing:
                    FillRect(t, 10, 4, 2, 9, brown); FillRect(t, 7, 10, 5, 3, red);
                    FillRect(t, 8, 13, 4, 2, red); FillRect(t, 10, 4, 4, 2, brown); break;
                case VillagerSkill.Witchcraft:
                    Line(t, 8, 2, 8, 13, red, 1); Line(t, 3, 8, 13, 8, red, 1);
                    Line(t, 4, 4, 12, 12, red, 1); Line(t, 4, 12, 12, 4, red, 1); break;
                case VillagerSkill.Building:
                    FillRect(t, 2, 2, 12, 4, brown); FillRect(t, 2, 8, 12, 5, brown);
                    FillRect(t, 8, 2, 1, 4, pale); FillRect(t, 6, 8, 1, 5, pale); break;
            }
            t.Apply(false, false);
            return SpriteFrom(t, 16f);
        }

        private static void FillRect(Texture2D tex, int x, int y, int width, int height, Color32 colour)
        {
            for (int j = y; j < y + height; ++j)
                for (int i = x; i < x + width; ++i)
                    if (i >= 0 && j >= 0 && i < tex.width && j < tex.height)
                        tex.SetPixel(i, j, colour);
        }

        private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, Color32 colour, int thickness)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                FillRect(tex, x0, y0, thickness, thickness, colour);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private Texture2D NewTexture(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            generatedAssets.Add(tex);
            return tex;
        }

        private Sprite SpriteFrom(Texture2D tex, float ppu)
        {
            Sprite sprite = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), ppu);
            generatedAssets.Add(sprite);
            return sprite;
        }

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
            foreach (UnityEngine.Object asset in generatedAssets)
                if (asset != null) Destroy(asset);
            generatedAssets.Clear();
        }
    }
}
