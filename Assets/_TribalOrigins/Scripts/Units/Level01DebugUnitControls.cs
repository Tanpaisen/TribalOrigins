using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TribalOrigins.Units
{
    /// <summary>
    /// M1.8: click-to-select and right-click-to-move for M1.7 DEBUG markers.
    /// Does not change THQ assets, spawn coordinates, jobs, collision, or pathfinding.
    /// Temporary Unity-side proof of concept; replace with actual Unit/Orders later.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TribalOrigins.Core.Level01SpawnDebug))]
    public sealed class Level01DebugUnitControls : MonoBehaviour
    {
        private const int Width = TribalOrigins.Core.Level01DebugMap.MapWidth;
        private const int Height = TribalOrigins.Core.Level01DebugMap.MapHeight;
        private const string SpawnRoot = "Generated_Initial_Entities_DEBUG";

        [SerializeField, Min(0.1f)] private float movementSpeed = 2.5f;
        [SerializeField, Range(0.2f, 0.75f)] private float clickRadius = 0.53f;
        [SerializeField] private bool showDebugHelp = true;

        private readonly List<Transform> villagers = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> destinations = new Dictionary<Transform, Vector3>();
        private readonly List<Transform> finished = new List<Transform>();

        private Camera worldCamera;
        private Transform selected;
        private GameObject selectionRing;
        private GameObject destinationMarker;
        private Texture2D ringTexture;
        private Texture2D destinationTexture;
        private Sprite ringSprite;
        private Sprite destinationSprite;
        private bool ready;

        private IEnumerator Start()
        {
            // Level01SpawnDebug.Start() waits a frame before creating its children.
            Transform root = null;
            for (int frame = 0; frame < 30; frame++)
            {
                root = transform.Find(SpawnRoot);
                if (root != null) break;
                yield return null;
            }

            if (root == null)
            {
                Debug.LogError("[TribalOrigins] M1.8: Starting villagers not found. " +
                    "Confirm Level01SpawnDebug works first.", this);
                yield break;
            }

            foreach (Transform entity in root)
            {
                if (entity.name.StartsWith("Villager_", StringComparison.Ordinal))
                    villagers.Add(entity);
            }
            if (villagers.Count != 9)
            {
                Debug.LogError("[TribalOrigins] M1.8: Expected 9 debug villagers; found " +
                    villagers.Count + ".", this);
                yield break;
            }

            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Debug.LogError("[TribalOrigins] M1.8: Main Camera is missing.", this);
                yield break;
            }

            ringSprite = MakeIndicatorSprite(true, out ringTexture);
            destinationSprite = MakeIndicatorSprite(false, out destinationTexture);

            selectionRing = new GameObject("M1_8_SelectionRing_DEBUG");
            selectionRing.transform.SetParent(transform, false);
            var selectionRenderer = selectionRing.AddComponent<SpriteRenderer>();
            selectionRenderer.sprite = ringSprite;
            selectionRenderer.sortingOrder = 102;
            selectionRing.SetActive(false);

            destinationMarker = new GameObject("M1_8_Destination_DEBUG");
            destinationMarker.transform.SetParent(transform, false);
            var destinationRenderer = destinationMarker.AddComponent<SpriteRenderer>();
            destinationRenderer.sprite = destinationSprite;
            destinationRenderer.sortingOrder = 102;
            destinationMarker.SetActive(false);

            ready = true;
            Debug.Log("[TribalOrigins] M1.8 READY: 9 villagers. " +
                "Left-click to select; right-click on map to move. " +
                "Movement is straight-line DEBUG (no collision or A*).", this);
        }

        private void Update()
        {
            if (!ready) return;

            UpdateUnitMovement();
            UpdateSelectionIndicator();

            if (!TryReadMouse(out Vector2 screenPosition, out bool leftClick, out bool rightClick))
                return;
            if (!leftClick && !rightClick) return;
            if (!worldCamera.pixelRect.Contains(screenPosition)) return;

            // Camera is at z=-10, map and markers at z~0.
            float distanceToMapPlane = -worldCamera.transform.position.z;
            if (distanceToMapPlane <= 0f) return;
            Vector3 world = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, distanceToMapPlane));

            if (leftClick) SelectClosestVillager(world);
            else if (rightClick) SetMoveDestination(world);
        }

        private static bool TryReadMouse(out Vector2 position, out bool leftClick, out bool rightClick)
        {
            position = Vector2.zero;
            leftClick = false;
            rightClick = false;
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            position = mouse.position.ReadValue();
            leftClick = mouse.leftButton.wasPressedThisFrame;
            rightClick = mouse.rightButton.wasPressedThisFrame;
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            leftClick = Input.GetMouseButtonDown(0);
            rightClick = Input.GetMouseButtonDown(1);
            return true;
#else
            return false;
#endif
        }

        private void SelectClosestVillager(Vector3 world)
        {
            Transform nearest = null;
            float bestSquaredDistance = clickRadius * clickRadius;
            foreach (Transform villager in villagers)
            {
                if (villager == null) continue;
                float dx = villager.position.x - world.x;
                float dy = villager.position.y - world.y;
                float sqDistance = dx * dx + dy * dy;
                if (sqDistance <= bestSquaredDistance)
                {
                    bestSquaredDistance = sqDistance;
                    nearest = villager;
                }
            }

            selected = nearest;
            selectionRing.SetActive(selected != null);
            if (selected == null)
            {
                destinationMarker.SetActive(false);
                return;
            }

            Debug.Log("[TribalOrigins] SELECTED: " + selected.name, this);
            if (destinations.TryGetValue(selected, out Vector3 target))
            {
                destinationMarker.transform.position = target;
                destinationMarker.SetActive(true);
            }
            else destinationMarker.SetActive(false);
        }

        private void SetMoveDestination(Vector3 world)
        {
            if (selected == null) return;
            int x = Mathf.FloorToInt(world.x);
            int y = Mathf.FloorToInt(world.y);
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                Debug.LogWarning("[TribalOrigins] M1.8: Destination outside the map.", this);
                return;
            }

            Vector3 destination = new Vector3(x + 0.5f, y + 0.5f, selected.position.z);
            destinations[selected] = destination;
            destinationMarker.transform.position = destination;
            destinationMarker.SetActive(true);
            int sourceY = Height - 1 - y;
            Debug.Log($"[TribalOrigins] MOVE ORDER: {selected.name} -> Unity cell ({x},{y}) / " +
                $"source cell ({x},{sourceY}). Straight-line DEBUG only.", this);
        }

        private void UpdateUnitMovement()
        {
            finished.Clear();
            foreach (KeyValuePair<Transform, Vector3> move in destinations)
            {
                Transform villager = move.Key;
                if (villager == null)
                {
                    finished.Add(villager);
                    continue;
                }
                villager.position = Vector3.MoveTowards(
                    villager.position, move.Value, movementSpeed * Time.deltaTime);
                if ((villager.position - move.Value).sqrMagnitude < 0.00001f)
                    finished.Add(villager);
            }

            foreach (Transform villager in finished)
            {
                destinations.Remove(villager);
                if (villager == selected && destinationMarker != null)
                    destinationMarker.SetActive(false);
            }
        }

        private void UpdateSelectionIndicator()
        {
            if (selected == null)
            {
                if (selectionRing != null) selectionRing.SetActive(false);
                return;
            }
            if (selectionRing != null)
                selectionRing.transform.position = new Vector3(
                    selected.position.x, selected.position.y, selected.position.z - 0.03f);
        }

        private void OnGUI()
        {
            if (!showDebugHelp || !ready) return;
            string name = selected == null ? "None" : selected.name;
            GUI.Box(new Rect(8, 8, 410, 74),
                "TRIBAL ORIGINS | M1.8 DEBUG\n" +
                "Left click: select villager    Right click: move to cell\n" +
                "Selected: " + name);
        }

        private static Sprite MakeIndicatorSprite(bool isRing, out Texture2D texture)
        {
            const int size = 48;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size / 2f;
                float dy = y + 0.5f - size / 2f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                bool visible = isRing
                    ? (radius >= 17f && radius <= 21f)
                    : ((Mathf.Abs(dx) <= 2f || Mathf.Abs(dy) <= 2f) &&
                       Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) <= 11f);
                texture.SetPixel(x, y, visible
                    ? (isRing ? new Color(1f, 0.95f, 0.1f, 1f) : new Color(1f, 0.35f, 0.1f, 1f))
                    : Color.clear);
            }
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
        }

        private void OnDestroy()
        {
            if (ringSprite != null) Destroy(ringSprite);
            if (destinationSprite != null) Destroy(destinationSprite);
            if (ringTexture != null) Destroy(ringTexture);
            if (destinationTexture != null) Destroy(destinationTexture);
        }
    }
}
