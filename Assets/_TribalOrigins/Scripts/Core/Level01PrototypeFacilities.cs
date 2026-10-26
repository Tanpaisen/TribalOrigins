using System.Collections;
using System.Collections.Generic;
using TribalOrigins.AI;
using UnityEngine;

namespace TribalOrigins.Core
{
    /// <summary>
    /// M2.4A: TEMPORARY Hut/Kitchen facility markers for testing future needs AI.
    /// THQ Mission 0 does NOT start with these buildings. Switch this debug option OFF
    /// to restore the accurate start (9 villagers + one Chief's Hut).
    /// Positions are invented TEST positions, not decoded THQ building locations.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01DebugMap))]
    public sealed class Level01PrototypeFacilities : MonoBehaviour
    {
        // Unity grid cells, NOT JAR source coords. These positions do not overlap
        // the nine mission-0 villagers or M1.10's temporary Chief's Hut block.
        public static readonly RectInt HutFootprint = new RectInt(11, 35, 2, 2);
        public static readonly RectInt KitchenFootprint = new RectInt(15, 35, 4, 3);
        public static readonly Vector2Int HutApproach = new Vector2Int(11, 37);
        public static readonly Vector2Int KitchenApproach = new Vector2Int(16, 38);

        [Header("TEMPORARY facility test (NOT original starting buildings)")]
        [SerializeField] private bool spawnTestHutAndKitchen = false;
        [SerializeField] private bool logPathChecks = true;

        public bool ActiveForNavigation => enabled && gameObject.activeInHierarchy && spawnTestHutAndKitchen;

        private Transform generatedRoot;
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();

        private IEnumerator Start()
        {
            if (!spawnTestHutAndKitchen)
            {
                Debug.Log("[TribalOrigins] M2.4A test buildings OFF. Mission 0 still starts only with Chief's Hut.", this);
                yield break;
            }
            // M1.6 generates the Tilemap in Start(). We only draw our test markers afterward.
            Transform terrain = null;
            for (int frame = 0; frame < 90; frame++)
            {
                terrain = transform.Find("Generated_Terrain_64x48");
                if (terrain != null) break;
                yield return null;
            }
            if (terrain == null)
            {
                Debug.LogError("[TribalOrigins] M2.4A: M1.6 terrain was not generated.", this);
                yield break;
            }

            var root = new GameObject("M2_4A_TestFacilities_NOT_OriginalSpawn");
            root.transform.SetParent(transform, false);
            generatedRoot = root.transform;
            CreateFacility("TEST_Hut", "HUT", HutFootprint,
                new Color(0.65f, 0.42f, 0.22f, 1f), HutApproach, "SLEEP");
            CreateFacility("TEST_Kitchen", "KITCHEN", KitchenFootprint,
                new Color(0.90f, 0.57f, 0.24f, 1f), KitchenApproach, "EAT");

            if (logPathChecks)
                CheckAccessPaths();
            Debug.Log("[TribalOrigins] M2.4A TEST BUILDINGS READY: 1 Hut (2x2), " +
                "1 Kitchen (4x3), access cells marked SLEEP and EAT. " +
                "Debug markers only: no construction cost, food stock, eating or sleeping AI yet.", this);
        }

        private void CheckAccessPaths()
        {
            Level01Walkability walkability = GetComponent<Level01Walkability>();
            if (walkability == null)
            {
                Debug.LogError("[TribalOrigins] M2.4A: Level01Walkability missing.", this);
                return;
            }
            GridAStar grid = walkability.BuildGrid(out int count, out string error);
            if (grid == null)
            {
                Debug.LogError("[TribalOrigins] M2.4A navigation grid failed: " + error, this);
                return;
            }
            var start = new Vector2Int(4, 36); // Mission-0 villager #1 seed.
            var route = new List<Vector2Int>();
            bool hut = grid.FindPath(start, HutApproach, route);
            route.Clear();
            bool kitchen = grid.FindPath(start, KitchenApproach, route);
            if (hut && kitchen && !grid.IsWalkable(new Vector2Int(HutFootprint.x, HutFootprint.y)) &&
                !grid.IsWalkable(new Vector2Int(KitchenFootprint.x, KitchenFootprint.y)))
                Debug.Log("[TribalOrigins] M2.4A PATH CHECK PASS: start -> Hut SLEEP and Kitchen EAT; " +
                    "building footprints blocked; blocked cells=" + count, this);
            else
                Debug.LogError("[TribalOrigins] M2.4A PATH CHECK FAILED: Hut=" + hut +
                    ", Kitchen=" + kitchen + ". Check grid or extra test walls.", this);
        }

        private void CreateFacility(string objectName, string displayName, RectInt bounds,
            Color color, Vector2Int approach, string actionText)
        {
            Sprite tileSprite = MakeSprite(color, "M2_4A_" + displayName);
            var building = new GameObject(objectName);
            building.transform.SetParent(generatedRoot, false);
            building.transform.localPosition = new Vector3(
                bounds.x + bounds.width * 0.5f,
                bounds.y + bounds.height * 0.5f, -0.12f);
            building.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
            var image = building.AddComponent<SpriteRenderer>();
            image.sprite = tileSprite;
            image.sortingOrder = 65;

            CreateLabel(displayName + "_Label", displayName,
                new Vector3(bounds.x + bounds.width * 0.5f,
                            bounds.y + bounds.height * 0.5f, -0.2f),
                Color.black, 80, 0.075f);

            // These access cells are OUTSIDE the blocked footprint. Future AI walks
            // to the cell centre, NOT inside a solid building footprint.
            Sprite doorSprite = MakeSprite(new Color(0.22f, 0.9f, 0.44f, 1f),
                "M2_4A_" + actionText + "_Access");
            var entry = new GameObject(displayName + "_Access_" + approach.x + "_" + approach.y);
            entry.transform.SetParent(generatedRoot, false);
            entry.transform.localPosition = new Vector3(approach.x + 0.5f, approach.y + 0.5f, -0.22f);
            entry.transform.localScale = new Vector3(0.46f, 0.46f, 1f);
            SpriteRenderer door = entry.AddComponent<SpriteRenderer>();
            door.sprite = doorSprite;
            door.sortingOrder = 90;

            CreateLabel(displayName + "_" + actionText + "_Label", actionText,
                new Vector3(approach.x + 0.5f, approach.y + 1.02f, -0.3f),
                Color.white, 95, 0.055f);
        }

        private void CreateLabel(string objectName, string text, Vector3 localPosition,
            Color color, int order, float charSize)
        {
            var labelObject = new GameObject(objectName);
            labelObject.transform.SetParent(generatedRoot, false);
            labelObject.transform.localPosition = localPosition;
            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 64;
            textMesh.characterSize = charSize;
            textMesh.color = color;
            var renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = order;
        }

        private Sprite MakeSprite(Color center, string name)
        {
            const int pixels = 16;
            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            texture.name = name + "_Texture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < pixels; y++)
                for (int x = 0; x < pixels; x++)
                {
                    bool border = x < 2 || y < 2 || x >= pixels - 2 || y >= pixels - 2;
                    texture.SetPixel(x, y, border ? new Color(0.15f, 0.12f, 0.08f, 1f) : center);
                }
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, pixels, pixels),
                new Vector2(0.5f, 0.5f), pixels);
            sprite.name = name;
            ownedSprites.Add(sprite);
            ownedTextures.Add(texture);
            return sprite;
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in ownedSprites)
                if (sprite != null) Destroy(sprite);
            foreach (Texture2D texture in ownedTextures)
                if (texture != null) Destroy(texture);
        }
    }
}
