using System;
using System.Collections.Generic;
using System.Globalization;
using TribalOrigins.AI;
using UnityEngine;

namespace TribalOrigins.Core
{
    /// <summary>
    /// M1.10: reads the SAME private map CSV used by M1.6 to build an A* collision grid.
    /// Tile codes alone do NOT prove passability. Block only explicitly verified IDs.
    /// Source (top-down) Y converts to Unity Y as (47 - sourceY).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level01Walkability : MonoBehaviour
    {
        private const string MapResource = "PrivateMaps/Level01RawTiles";
        private const int Width = Level01DebugMap.MapWidth;
        private const int Height = Level01DebugMap.MapHeight;

        [Header("Collision sources")]
        [SerializeField] private bool blockTemporaryChiefHutArea = true;
        [Tooltip("Comma-separated raw tile IDs that you have VERIFIED are unwalkable. Leave EMPTY until verified.")]
        [SerializeField] private string verifiedBlockedTileIds = "";
        [Tooltip("OPTIONAL test wall, not THQ map data. Switch off after test.")]
        [SerializeField] private bool extraTestWall = false;

        public bool BlockTemporaryChiefHutArea => blockTemporaryChiefHutArea;
        public bool ExtraTestWall => extraTestWall;
        public string VerifiedBlockedTileIds => verifiedBlockedTileIds;

        public GridAStar BuildGrid(out int blockedCount, out string failure)
        {
            blockedCount = 0;
            failure = null;
            TextAsset asset = Resources.Load<TextAsset>(MapResource);
            if (asset == null)
            {
                failure = "Missing private CSV in Assets/_TribalOrigins/Data/Resources/PrivateMaps/Level01RawTiles.csv";
                return null;
            }

            if (!TryReadTileCodes(asset.text, out int[,] rawCodes, out failure))
                return null;

            if (!TryParseIds(verifiedBlockedTileIds, out HashSet<int> forbidden, out failure))
                return null;

            var grid = new GridAStar(Width, Height);
            for (int sourceY = 0; sourceY < Height; sourceY++)
            {
                int unityY = Height - 1 - sourceY;
                for (int x = 0; x < Width; x++)
                {
                    if (forbidden.Contains(rawCodes[sourceY, x]))
                        grid.SetBlocked(new Vector2Int(x, unityY));
                }
            }

            // M1.9's 4x4 TEST collision remains unchanged. The actual footprint
            // of the source Chief's Hut is not established by the marker anchor.
            if (blockTemporaryChiefHutArea)
                BlockRect(grid, 6, 33, 4, 4);

            // M2.4A: place collision ONLY for enabled prototype facilities.
            // Footprints are shared with the visual test markers (not THQ coordinates).
            Level01PrototypeFacilities facilities = GetComponent<Level01PrototypeFacilities>();
            bool testFacilities = facilities != null && facilities.ActiveForNavigation;
            if (testFacilities)
            {
                RectInt hut = Level01PrototypeFacilities.HutFootprint;
                RectInt kitchen = Level01PrototypeFacilities.KitchenFootprint;
                BlockRect(grid, hut.x, hut.y, hut.width, hut.height);
                BlockRect(grid, kitchen.x, kitchen.y, kitchen.width, kitchen.height);
            }

            // An independent, optional obstacle for verifying A* reroutes;
            // NOT a feature of the THQ map. At Unity x=12 and y=30..35.
            if (extraTestWall)
                BlockRect(grid, 12, 30, 1, 6);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (!grid.IsWalkable(new Vector2Int(x, y))) blockedCount++;

            Debug.Log("[TribalOrigins] M1.10 COLLISION GRID: " + Width + "x" + Height +
                ", blocked=" + blockedCount + ", verified raw tile IDs=" + forbidden.Count +
                ", proxy Hut=" + blockTemporaryChiefHutArea +
                ", test wall=" + extraTestWall + ", M2.4A test facilities=" + testFacilities +
                ". Unlisted IDs remain WALKABLE (unknown), not confirmed terrain.", this);
            return grid;
        }

        private static void BlockRect(GridAStar grid, int x, int y, int w, int h)
        {
            for (int cy = y; cy < y + h; cy++)
                for (int cx = x; cx < x + w; cx++)
                    grid.SetBlocked(new Vector2Int(cx, cy));
        }

        private static bool TryParseIds(string text, out HashSet<int> ids, out string error)
        {
            ids = new HashSet<int>();
            error = null;
            if (string.IsNullOrWhiteSpace(text)) return true;
            string[] parts = text.Split(',');
            foreach (string part in parts)
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int id) || id < 0 || id > 255)
                {
                    error = "Invalid verified blocked tile ID: '" + part + "'. Use integers 0..255 separated by commas.";
                    return false;
                }
                ids.Add(id);
            }
            return true;
        }

        private static bool TryReadTileCodes(string csv, out int[,] codes, out string error)
        {
            codes = new int[Height, Width];
            error = null;
            string[] rows = csv.Replace("\r", "").TrimEnd('\n', ' ', '\t').Split('\n');
            if (rows.Length != Height)
            {
                error = "Expected " + Height + " raw-map rows, got " + rows.Length;
                return false;
            }
            for (int y = 0; y < Height; y++)
            {
                string[] parts = rows[y].Split(',');
                if (parts.Length != Width)
                {
                    error = "Raw-map row " + (y + 1) + ": expected " + Width + " columns, got " + parts.Length;
                    return false;
                }
                for (int x = 0; x < Width; x++)
                {
                    if (!int.TryParse(parts[x].Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int id) || id < 0 || id > 255)
                    {
                        error = "Invalid raw tile at source (" + x + "," + y + "): '" + parts[x] + "'";
                        return false;
                    }
                    codes[y, x] = id;
                }
            }
            return true;
        }
    }
}
