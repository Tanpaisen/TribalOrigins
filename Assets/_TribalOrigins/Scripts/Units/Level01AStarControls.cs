using System;
using System.Collections;
using System.Collections.Generic;
using TribalOrigins.AI;
using TribalOrigins.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TribalOrigins.Units
{
    /// <summary>
    /// M1.9: selection and click-to-move for the SAME M1.7 debug villagers,
    /// upgraded to deterministic four-direction A* routes around a TEST obstruction.
    /// Does not assert that raw terrain tile IDs are walkable/non-walkable in THQ.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Level01SpawnDebug))]
    public sealed class Level01AStarControls : MonoBehaviour
    {
        private const int Width = Level01DebugMap.MapWidth;
        private const int Height = Level01DebugMap.MapHeight;
        private const string SpawnRootName = "Generated_Initial_Entities_DEBUG";
        private const int ProxyHutSourceX = 6;
        private const int ProxyHutSourceY = 11;
        private const int ProxyHutWidth = 4;
        private const int ProxyHutHeight = 4;

        [SerializeField, Min(0.1f)] private float movementSpeed = 2.5f;
        [SerializeField, Range(0.2f, 0.75f)] private float clickRadius = 0.53f;
        [SerializeField] private bool showDebugHelp = true;
        [SerializeField] private bool showProxyHutBlock = true;
        [SerializeField] private bool showSelectedPath = true;

        private sealed class RoutePlan
        {
            public readonly List<Vector2Int> Cells;
            public int NextIndex;
            public readonly Vector3 FinalPosition;
            public RoutePlan(List<Vector2Int> cells, int nextIndex, Vector3 finalPosition)
            {
                Cells = cells;
                NextIndex = nextIndex;
                FinalPosition = finalPosition;
            }
        }

        private readonly List<Transform> villagers = new List<Transform>();
        private readonly Dictionary<Transform, RoutePlan> activeRoutes = new Dictionary<Transform, RoutePlan>();
        private readonly List<Transform> finishedRoutes = new List<Transform>();
        private readonly List<GameObject> pathMarkers = new List<GameObject>();
        private readonly List<GameObject> obstacleMarkers = new List<GameObject>();
        private readonly List<Vector2Int> calculatedPath = new List<Vector2Int>();

        private GridAStar grid;
        private Camera worldCamera;
        private Transform selected;
        private GameObject selectionRing;
        private GameObject destinationMarker;
        private Sprite ringSprite;
        private Sprite targetSprite;
        private Sprite smallDotSprite;
        private Sprite tileSprite;
        private Texture2D ringTexture;
        private Texture2D targetTexture;
        private Texture2D dotTexture;
        private Texture2D tileTexture;
        private bool ready;

        private void Awake()
        {
            // Prevent M1.8's straight-line Update from competing with our A* movement.
            var oldControls = GetComponent<Level01DebugUnitControls>();
            if (oldControls != null && oldControls.enabled)
            {
                oldControls.enabled = false;
                Debug.Log("[TribalOrigins] M1.9 disabled old M1.8 straight-line controller for this Play session.", this);
            }
        }

        private IEnumerator Start()
        {
            Transform spawnRoot = null;
            for (int frame = 0; frame < 60; frame++)
            {
                spawnRoot = transform.Find(SpawnRootName);
                if (spawnRoot != null) break;
                yield return null;
            }
            if (spawnRoot == null)
            {
                Debug.LogError("[TribalOrigins] M1.9: No M1.7 villagers. Check Level01SpawnDebug.", this);
                yield break;
            }
            foreach (Transform entity in spawnRoot)
                if (entity.name.StartsWith("Villager_", StringComparison.Ordinal))
                    villagers.Add(entity);
            if (villagers.Count != 9)
            {
                Debug.LogError("[TribalOrigins] M1.9: Expected 9 villagers, found " + villagers.Count, this);
                yield break;
            }
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogError("[TribalOrigins] M1.9: Main Camera not found.", this);
                yield break;
            }

            if (!GridAStar.RunDiagnostics(out string checks))
            {
                Debug.LogError("[TribalOrigins] M1.9 A* SELF-TEST FAILED: " + checks, this);
                yield break;
            }
            Debug.Log("[TribalOrigins] M1.9 A* SELF-TEST PASS: " + checks, this);

            grid = new GridAStar(Width, Height);
            // TEST ONLY. The real footprint origin and walkability map remain unverified.
            // Choose a 4x4 proxy to the east/down (SOURCE coords) of the Hut marker.
            // SourceY 11..14 becomes UnityY 36..33.
            int hutBottomUnityY = Height - (ProxyHutSourceY + ProxyHutHeight);
            grid.BlockRectangle(ProxyHutSourceX, hutBottomUnityY, ProxyHutWidth, ProxyHutHeight);

            ringSprite = MakeCircleSprite(true, out ringTexture);
            targetSprite = MakeCrossSprite(out targetTexture);
            smallDotSprite = MakeCircleSprite(false, out dotTexture);
            tileSprite = MakeSquareSprite(out tileTexture);

            selectionRing = SpawnIndicator("M1_9_SelectionRing", ringSprite, Color.yellow, 110);
            selectionRing.SetActive(false);
            destinationMarker = SpawnIndicator("M1_9_Destination", targetSprite,
                new Color(1f, 0.4f, 0.07f, 1f), 110);
            destinationMarker.SetActive(false);

            if (showProxyHutBlock)
            {
                for (int y = hutBottomUnityY; y < hutBottomUnityY + ProxyHutHeight; y++)
                    for (int x = ProxyHutSourceX; x < ProxyHutSourceX + ProxyHutWidth; x++)
                    {
                        GameObject marker = SpawnIndicator("TEST_Blocked_" + x + "_" + y,
                            tileSprite, new Color(0.95f, 0.14f, 0.12f, 0.40f), 50);
                        marker.transform.position = CellCentre(new Vector2Int(x, y), -0.10f);
                        obstacleMarkers.Add(marker);
                    }
            }
            ready = true;
            Debug.Log("[TribalOrigins] M1.9 READY: A* 4-direction, 64x48, 9 villagers, " +
                "16 TEMP blocked Hut cells. Left click select, right click move. " +
                "No terrain walkability, unit collision or THQ pathfinding claims.", this);
        }

        private void Update()
        {
            if (!ready) return;
            AdvanceRoutes();
            UpdateSelectionRing();

            if (!TryReadMouse(out Vector2 pixel, out bool left, out bool right)) return;
            if ((!left && !right) || !worldCamera.pixelRect.Contains(pixel)) return;
            float distanceToPlane = -worldCamera.transform.position.z;
            if (distanceToPlane <= 0f) return;
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(pixel.x, pixel.y, distanceToPlane));
            if (left) SelectAt(world);
            else if (right) MoveSelectedTo(world);
        }

        private static bool TryReadMouse(out Vector2 position, out bool left, out bool right)
        {
            position = Vector2.zero;
            left = false;
            right = false;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            position = mouse.position.ReadValue();
            left = mouse.leftButton.wasPressedThisFrame;
            right = mouse.rightButton.wasPressedThisFrame;
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            left = Input.GetMouseButtonDown(0);
            right = Input.GetMouseButtonDown(1);
            return true;
#else
            return false;
#endif
        }

        private void SelectAt(Vector3 point)
        {
            Transform nearest = null;
            float best = clickRadius * clickRadius;
            foreach (Transform villager in villagers)
            {
                if (villager == null) continue;
                float dx = point.x - villager.position.x;
                float dy = point.y - villager.position.y;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared <= best)
                {
                    best = distanceSquared;
                    nearest = villager;
                }
            }
            selected = nearest;
            selectionRing.SetActive(selected != null);
            RefreshPathAndTarget();
            if (selected != null) Debug.Log("[TribalOrigins] M1.9 SELECTED: " + selected.name, this);
        }

        private void MoveSelectedTo(Vector3 point)
        {
            if (selected == null) return;
            Vector2Int goal = WorldToCell(point);
            Vector2Int start = WorldToCell(selected.position);
            if (!grid.InBounds(goal))
            {
                Debug.LogWarning("[TribalOrigins] M1.9: Destination outside 64x48 map.", this);
                return;
            }
            if (!grid.IsWalkable(goal))
            {
                Debug.LogWarning("[TribalOrigins] M1.9 BLOCKED destination " + goal +
                    " (temporary Hut footprint). Existing movement unchanged.", this);
                return;
            }
            if (!grid.FindPath(start, goal, calculatedPath))
            {
                Debug.LogWarning("[TribalOrigins] M1.9 NO PATH from " + start + " to " + goal, this);
                return;
            }

            var cells = new List<Vector2Int>(calculatedPath);
            int nextIndex = cells.Count > 1 ? 1 : 0;
            var final = CellCentre(goal, selected.position.z);
            activeRoutes[selected] = new RoutePlan(cells, nextIndex, final);
            RefreshPathAndTarget();
            Debug.Log($"[TribalOrigins] M1.9 A* PATH: {selected.name} ({start.x},{start.y}) -> " +
                $"({goal.x},{goal.y}); steps={cells.Count - 1}; " +
                $"source target=({goal.x},{Height - 1 - goal.y}).", this);
        }

        private void AdvanceRoutes()
        {
            finishedRoutes.Clear();
            foreach (KeyValuePair<Transform, RoutePlan> pair in activeRoutes)
            {
                Transform unit = pair.Key;
                RoutePlan route = pair.Value;
                if (unit == null)
                {
                    finishedRoutes.Add(unit);
                    continue;
                }

                if (route.NextIndex >= route.Cells.Count)
                {
                    finishedRoutes.Add(unit);
                    continue;
                }
                Vector3 target = CellCentre(route.Cells[route.NextIndex], unit.position.z);
                unit.position = Vector3.MoveTowards(unit.position, target, movementSpeed * Time.deltaTime);
                if ((unit.position - target).sqrMagnitude <= 0.00001f)
                {
                    unit.position = target;
                    route.NextIndex++;
                    if (route.NextIndex >= route.Cells.Count)
                        finishedRoutes.Add(unit);
                }
            }
            foreach (Transform unit in finishedRoutes)
            {
                activeRoutes.Remove(unit);
                if (unit == selected)
                    RefreshPathAndTarget();
            }
        }

        private void UpdateSelectionRing()
        {
            if (selected == null) return;
            selectionRing.transform.position = new Vector3(
                selected.position.x, selected.position.y, selected.position.z - 0.04f);
        }

        private void RefreshPathAndTarget()
        {
            ClearPathMarkers();
            if (selected == null || !activeRoutes.TryGetValue(selected, out RoutePlan route))
            {
                destinationMarker.SetActive(false);
                return;
            }
            destinationMarker.transform.position = route.FinalPosition;
            destinationMarker.SetActive(true);
            if (!showSelectedPath) return;
            for (int i = route.NextIndex; i < route.Cells.Count; i++)
            {
                GameObject dot = SpawnIndicator("M1_9_PathWaypoint_" + i,
                    smallDotSprite, new Color(0.32f, 1f, 0.33f, 0.95f), 106);
                dot.transform.position = CellCentre(route.Cells[i], -0.24f);
                pathMarkers.Add(dot);
            }
        }

        private void ClearPathMarkers()
        {
            foreach (GameObject dot in pathMarkers)
                if (dot != null) Destroy(dot);
            pathMarkers.Clear();
        }

        private GameObject SpawnIndicator(string objectName, Sprite sprite, Color tint, int order)
        {
            var indicator = new GameObject(objectName);
            indicator.transform.SetParent(transform, false);
            SpriteRenderer renderer = indicator.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = order;
            return indicator;
        }

        private static Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        private static Vector3 CellCentre(Vector2Int cell, float z)
        {
            return new Vector3(cell.x + 0.5f, cell.y + 0.5f, z);
        }

        private void OnGUI()
        {
            if (!ready || !showDebugHelp) return;
            GUI.Box(new Rect(8, 8, 440, 88),
                "TRIBAL ORIGINS | M1.9 A* DEBUG\n" +
                "Left: select  |  Right: A* move  |  Red cells: TEST Hut block\n" +
                "Selected: " + (selected == null ? "None" : selected.name));
        }

        private static Sprite MakeCircleSprite(bool ring, out Texture2D texture)
        {
            const int size = 48;
            texture = NewTexture(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    bool inside = ring ? radius >= 17f && radius <= 21f : radius <= 6f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite MakeCrossSprite(out Texture2D texture)
        {
            const int size = 48;
            texture = NewTexture(size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    bool inside = (Mathf.Abs(dx) <= 2f || Mathf.Abs(dy) <= 2f) &&
                                  Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) <= 11f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
        }

        private static Sprite MakeSquareSprite(out Texture2D texture)
        {
            texture = NewTexture(16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    texture.SetPixel(x, y, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 16, 16),
                new Vector2(0.5f, 0.5f), 16f);
        }

        private static Texture2D NewTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private void OnDestroy()
        {
            // Spawned GameObjects are children and are destroyed by Unity with this scene.
            if (ringSprite != null) Destroy(ringSprite);
            if (targetSprite != null) Destroy(targetSprite);
            if (smallDotSprite != null) Destroy(smallDotSprite);
            if (tileSprite != null) Destroy(tileSprite);
            if (ringTexture != null) Destroy(ringTexture);
            if (targetTexture != null) Destroy(targetTexture);
            if (dotTexture != null) Destroy(dotTexture);
            if (tileTexture != null) Destroy(tileTexture);
        }
    }
}
