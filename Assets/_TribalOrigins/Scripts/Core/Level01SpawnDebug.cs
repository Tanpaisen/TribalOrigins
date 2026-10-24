using System.Collections;
using UnityEngine;

namespace TribalOrigins.Core
{
    /// <summary>
    /// Visual validation of the initial entities in THQ Prehistoric Tribes v0.0.20,
    /// internal mission 0 (Outside). No THQ artwork or extracted map is redistributed.
    /// This is ONLY a temporary debug overlay, not the finished NPC/building system.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01DebugMap))]
    public sealed class Level01SpawnDebug : MonoBehaviour
    {
        private struct SourceVillager
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Age;

            public SourceVillager(int x, int y, int age)
            {
                X = x;
                Y = y;
                Age = age;
            }
        }

        // Source x/y positions and age seeds, in the game's top-to-bottom map space.
        // Villager #1 is the chief; the original gender-generation routine is NOT
        // inferred from these source records.
        private static readonly SourceVillager[] Villagers =
        {
            new SourceVillager(4, 11, 35),
            new SourceVillager(3,  8, 20),
            new SourceVillager(5,  9, 25),
            new SourceVillager(5,  8, 17),
            new SourceVillager(6,  8, 22),
            new SourceVillager(3,  9, 27),
            new SourceVillager(4,  8, 17),
            new SourceVillager(4,  9, 22),
            new SourceVillager(6,  9, 15)
        };

        private const int ChiefHutSourceX = 6;
        private const int ChiefHutSourceY = 11;

        [SerializeField] private bool focusCameraOnStartingVillage = true;
        [SerializeField] private float cameraOrthographicSize = 7f;
        [SerializeField] private bool showSourceCoordinateLabels = false;

        private Texture2D unitTexture;
        private Texture2D hutTexture;
        private Sprite unitSprite;
        private Sprite hutSprite;
        private Transform generatedRoot;

        // The map's own Start() creates the Tilemap and frames the whole map.
        // Wait a frame so our markers are only created after that succeeds.
        private IEnumerator Start()
        {
            yield return null;

            Transform terrain = transform.Find("Generated_Terrain_64x48");
            if (terrain == null)
            {
                Debug.LogError("[TribalOrigins] Start markers not created: terrain missing. Fix Level01DebugMap first.", this);
                yield break;
            }

            var parent = new GameObject("Generated_Initial_Entities_DEBUG");
            parent.transform.SetParent(transform, false);
            generatedRoot = parent.transform;

            unitSprite = CreateIconSprite(false, "DebugVillagerMarker", out unitTexture);
            hutSprite = CreateIconSprite(true, "DebugChiefHutMarker", out hutTexture);

            CreateMarker("ChiefHut_Source_6_11", ChiefHutSourceX, ChiefHutSourceY,
                hutSprite, new Color(0.25f, 0.85f, 0.35f, 1f), "H", 90);

            for (int i = 0; i < Villagers.Length; i++)
            {
                SourceVillager villager = Villagers[i];
                bool chief = i == 0;
                Color tint = chief
                    ? new Color(1f, 0.75f, 0.15f, 1f)
                    : new Color(0.25f, 0.90f, 1f, 1f);
                CreateMarker($"Villager_{i + 1:00}_Age_{villager.Age}" + (chief ? "_Chief" : ""),
                    villager.X, villager.Y, unitSprite, tint,
                    (i + 1).ToString(), 100);
            }

            if (focusCameraOnStartingVillage)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    mainCamera.orthographic = true;
                    mainCamera.orthographicSize = Mathf.Max(3f, cameraOrthographicSize);
                    mainCamera.transform.position = new Vector3(10f, 36.5f, -10f);
                }
                else
                {
                    Debug.LogWarning("[TribalOrigins] Main Camera not found: cannot focus on starting village.", this);
                }
            }

            Debug.Log("[TribalOrigins] Spawn DEBUG verified: 9 starting villagers (chief = #1), " +
                "1 Chief's Hut. Source coordinates mapped using UnityY = 47 - SourceY. " +
                "Markers are placeholders, NOT THQ sprites or verified building footprints.", this);
        }

        private void CreateMarker(string objectName, int sourceX, int sourceY,
            Sprite sprite, Color tint, string numberOrLetter, int sortingOrder)
        {
            Vector3Int tileCell = Level01DebugMap.SourceCellToUnityCell(sourceX, sourceY);
            var marker = new GameObject(objectName);
            marker.transform.SetParent(generatedRoot, false);
            marker.transform.localPosition = new Vector3(tileCell.x + 0.5f, tileCell.y + 0.5f, -0.3f);

            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;

            var labelObject = new GameObject("ID_Label");
            labelObject.transform.SetParent(marker.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = numberOrLetter;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.10f;
            label.color = Color.black;
            MeshRenderer textRenderer = labelObject.GetComponent<MeshRenderer>();
            if (textRenderer != null) textRenderer.sortingOrder = sortingOrder + 1;

            if (showSourceCoordinateLabels)
            {
                var coordinates = new GameObject("Source_XY_Label");
                coordinates.transform.SetParent(marker.transform, false);
                coordinates.transform.localPosition = new Vector3(0f, -0.57f, -0.05f);
                TextMesh coordinateText = coordinates.AddComponent<TextMesh>();
                coordinateText.text = $"({sourceX},{sourceY})";
                coordinateText.anchor = TextAnchor.MiddleCenter;
                coordinateText.alignment = TextAlignment.Center;
                coordinateText.fontSize = 48;
                coordinateText.characterSize = 0.075f;
                coordinateText.color = Color.white;
                MeshRenderer coordinatesRenderer = coordinates.GetComponent<MeshRenderer>();
                if (coordinatesRenderer != null) coordinatesRenderer.sortingOrder = sortingOrder + 1;
            }
        }

        private static Sprite CreateIconSprite(bool square, string spriteName, out Texture2D texture)
        {
            const int size = 32;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + "_Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color outline = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color fill = Color.white;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) - size * 0.5f;
                    float dy = (y + 0.5f) - size * 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inside = square ? (x >= 3 && x < 29 && y >= 3 && y < 29) : radius <= 14f;
                    bool inner = square ? (x >= 6 && x < 26 && y >= 6 && y < 26) : radius <= 11.5f;
                    texture.SetPixel(x, y, !inside ? Color.clear : (inner ? fill : outline));
                }
            }
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
            sprite.name = spriteName;
            return sprite;
        }

        private void OnDestroy()
        {
            if (unitSprite != null) Destroy(unitSprite);
            if (hutSprite != null) Destroy(hutSprite);
            if (unitTexture != null) Destroy(unitTexture);
            if (hutTexture != null) Destroy(hutTexture);
        }
    }
}
