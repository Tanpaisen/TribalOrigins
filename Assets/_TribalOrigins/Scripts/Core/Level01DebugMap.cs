using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TribalOrigins.Core
{
    /// <summary>
    /// Milestone 1.6: visualize source tile IDs from THQ JAR 0.0.20.
    /// Important: each tile's color is ONLY a debugging color, not the original THQ art.
    /// Keep extracted data in a private, gitignored folder.
    /// </summary>
    public sealed class Level01DebugMap : MonoBehaviour
    {
        public const int MapWidth = 64;
        public const int MapHeight = 48;
        private const string ResourcePath = "PrivateMaps/Level01RawTiles";

        [SerializeField] private bool centerMainCamera = true;
        [SerializeField] private bool useProceduralFallbackIfMissing = true;

        private readonly Dictionary<int, Tile> tilesById = new Dictionary<int, Tile>();
        private Texture2D whiteTexture;
        private Sprite whiteSprite;
        private GameObject generatedMap;

        private void Start()
        {
            TextAsset mapFile = Resources.Load<TextAsset>(ResourcePath);
            int[,] codes;
            bool fromOriginal;

            if (mapFile != null)
            {
                if (!TryParseMap(mapFile.text, out codes))
                {
                    Debug.LogError("[TribalOrigins] Cannot load original map CSV. Fix the file before proceeding.", this);
                    return;
                }
                fromOriginal = true;
            }
            else
            {
                if (!useProceduralFallbackIfMissing)
                {
                    Debug.LogError("[TribalOrigins] Missing Resources/PrivateMaps/Level01RawTiles.csv", this);
                    return;
                }
                codes = MakeProceduralDebugMap();
                fromOriginal = false;
            }

            CreateTiles(codes);
            if (centerMainCamera) ConfigureMainCamera();

            Debug.Log(fromOriginal
                ? "[TribalOrigins] Loaded original THQ 0.0.20 raw map: 64 x 48 cells. Colors are DEBUG ONLY."
                : "[TribalOrigins] Original CSV missing: showing PROCEDURAL TEST MAP instead (not THQ terrain).", this);
        }

        // Rows in the extracted CSV are stored top-to-bottom. Unity Tilemap Y is bottom-to-top.
        public static Vector3Int SourceCellToUnityCell(int x, int sourceY)
        {
            return new Vector3Int(x, MapHeight - 1 - sourceY, 0);
        }

        private static bool TryParseMap(string csv, out int[,] codes)
        {
            codes = new int[MapHeight, MapWidth];
            string[] lines = csv.Replace("\r", "").TrimEnd('\n', ' ', '\t').Split('\n');
            if (lines.Length != MapHeight)
            {
                Debug.LogError($"[TribalOrigins] Expected {MapHeight} rows, found {lines.Length}.");
                return false;
            }

            for (int row = 0; row < MapHeight; row++)
            {
                string[] parts = lines[row].Split(',');
                if (parts.Length != MapWidth)
                {
                    Debug.LogError($"[TribalOrigins] CSV row {row + 1}: expected {MapWidth} columns, got {parts.Length}.");
                    return false;
                }
                for (int x = 0; x < MapWidth; x++)
                {
                    if (!int.TryParse(parts[x].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int value) || value < 0 || value > 255)
                    {
                        Debug.LogError($"[TribalOrigins] Invalid tile at CSV row {row + 1}, column {x + 1}: '{parts[x]}'.");
                        return false;
                    }
                    codes[row, x] = value;
                }
            }
            return true;
        }

        private void CreateTiles(int[,] sourceRows)
        {
            generatedMap = new GameObject("Generated_Terrain_64x48");
            generatedMap.transform.SetParent(transform, false);

            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(generatedMap.transform, false);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            var tilemapObject = new GameObject("Raw_Tile_IDs_DEBUG");
            tilemapObject.transform.SetParent(gridObject.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();

            whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTexture.name = "RuntimeWhiteTile";
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
            whiteTexture.filterMode = FilterMode.Point;
            whiteTexture.wrapMode = TextureWrapMode.Clamp;
            whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "RuntimeWhiteTileSprite";

            // CSV row 0 is the TOP of the original map.
            for (int row = 0; row < MapHeight; row++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    int tileId = sourceRows[row, x];
                    tilemap.SetTile(SourceCellToUnityCell(x, row), GetTile(tileId));
                }
            }
            tilemap.RefreshAllTiles();
        }

        private Tile GetTile(int tileId)
        {
            if (tilesById.TryGetValue(tileId, out Tile tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "DebugTile_ID_" + tileId;
            tile.sprite = whiteSprite;
            tile.color = DebugColor(tileId);
            tile.flags = TileFlags.LockColor;
            tile.colliderType = Tile.ColliderType.None;
            tilesById.Add(tileId, tile);
            return tile;
        }

        private static Color DebugColor(int tileId)
        {
            // Stable ID-based color, NOT inferred grass/water/stone semantics.
            if (tileId == 0) return new Color(0.17f, 0.20f, 0.24f, 1f);
            if (tileId == 6) return new Color(0.25f, 0.38f, 0.26f, 1f);
            float hue = Mathf.Repeat((tileId * 0.6180339887f), 1f);
            float saturation = 0.30f + (tileId % 3) * 0.09f;
            float value = 0.58f + (tileId % 5) * 0.065f;
            return Color.HSVToRGB(hue, saturation, value);
        }

        private static int[,] MakeProceduralDebugMap()
        {
            var demo = new int[MapHeight, MapWidth];
            for (int y = 0; y < MapHeight; y++)
                for (int x = 0; x < MapWidth; x++)
                    demo[y, x] = (x / 8 + y / 8) % 8 + 10;
            return demo;
        }

        private static void ConfigureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[TribalOrigins] No MainCamera found. Terrain generated but not auto-framed.");
                return;
            }
            camera.orthographic = true;
            camera.orthographicSize = 28f;
            camera.transform.position = new Vector3(MapWidth * 0.5f, MapHeight * 0.5f, -10f);
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.14f, 1f);
        }

        private void OnDestroy()
        {
            foreach (Tile tile in tilesById.Values)
                if (tile != null) Destroy(tile);
            tilesById.Clear();
            if (whiteSprite != null) Destroy(whiteSprite);
            if (whiteTexture != null) Destroy(whiteTexture);
        }
    }
}
