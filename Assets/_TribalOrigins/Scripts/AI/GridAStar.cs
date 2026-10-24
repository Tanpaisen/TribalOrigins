using System;
using System.Collections.Generic;
using UnityEngine;

namespace TribalOrigins.AI
{
    /// <summary>
    /// M1.9: deterministic 4-direction A* over a configurable walkability grid.
    /// The grid stores UNITY cell coordinates, with (0,0) at the bottom-left.
    /// This is original project code; NOT an assertion about THQ's pathfinding.
    /// </summary>
    public sealed class GridAStar
    {
        private readonly int width;
        private readonly int height;
        private readonly bool[] blocked;

        private struct QueueEntry
        {
            public int Index;
            public int G;
            public int H;
            public long Sequence;
            public int F => G + H;
        }

        private sealed class MinHeap
        {
            private readonly List<QueueEntry> data = new List<QueueEntry>();
            public int Count => data.Count;

            private static bool ComesBefore(QueueEntry a, QueueEntry b)
            {
                if (a.F != b.F) return a.F < b.F;
                if (a.H != b.H) return a.H < b.H;
                return a.Sequence < b.Sequence;
            }

            public void Push(QueueEntry entry)
            {
                data.Add(entry);
                int index = data.Count - 1;
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!ComesBefore(data[index], data[parent])) break;
                    QueueEntry tmp = data[parent];
                    data[parent] = data[index];
                    data[index] = tmp;
                    index = parent;
                }
            }

            public QueueEntry Pop()
            {
                QueueEntry root = data[0];
                QueueEntry last = data[data.Count - 1];
                data.RemoveAt(data.Count - 1);
                if (data.Count == 0) return root;
                data[0] = last;
                int index = 0;
                while (true)
                {
                    int left = 2 * index + 1;
                    if (left >= data.Count) break;
                    int right = left + 1;
                    int child = left;
                    if (right < data.Count && ComesBefore(data[right], data[left]))
                        child = right;
                    if (!ComesBefore(data[child], data[index])) break;
                    QueueEntry tmp = data[index];
                    data[index] = data[child];
                    data[child] = tmp;
                    index = child;
                }
                return root;
            }
        }

        public int Width => width;
        public int Height => height;

        public GridAStar(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width;
            this.height = height;
            blocked = new bool[width * height];
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return InBounds(cell) && !blocked[Index(cell)];
        }

        public void SetBlocked(Vector2Int cell, bool value = true)
        {
            if (!InBounds(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            blocked[Index(cell)] = value;
        }

        public void BlockRectangle(int left, int bottom, int cellsWide, int cellsHigh)
        {
            if (cellsWide <= 0 || cellsHigh <= 0) throw new ArgumentOutOfRangeException(nameof(cellsWide));
            for (int y = bottom; y < bottom + cellsHigh; y++)
                for (int x = left; x < left + cellsWide; x++)
                    SetBlocked(new Vector2Int(x, y));
        }

        public bool FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (!IsWalkable(start) || !IsWalkable(goal)) return false;
            if (start == goal)
            {
                result.Add(start);
                return true;
            }

            int startIndex = Index(start);
            int goalIndex = Index(goal);
            int total = width * height;
            int[] g = new int[total];
            int[] previous = new int[total];
            bool[] closed = new bool[total];
            for (int i = 0; i < total; i++)
            {
                g[i] = int.MaxValue;
                previous[i] = -1;
            }

            long sequence = 0;
            var open = new MinHeap();
            g[startIndex] = 0;
            open.Push(new QueueEntry
            {
                Index = startIndex, G = 0, H = Manhattan(start, goal), Sequence = sequence++
            });

            // Stable order: East, North, West, South. Only four-direction movement.
            int[] dx = { 1, 0, -1, 0 };
            int[] dy = { 0, 1, 0, -1 };

            while (open.Count > 0)
            {
                QueueEntry current = open.Pop();
                int index = current.Index;
                if (closed[index] || current.G != g[index]) continue;
                if (index == goalIndex)
                {
                    for (int cursor = goalIndex; cursor >= 0; cursor = previous[cursor])
                    {
                        result.Add(Cell(cursor));
                        if (cursor == startIndex) break;
                    }
                    result.Reverse();
                    return result.Count > 0 && result[0] == start;
                }

                closed[index] = true;
                Vector2Int cell = Cell(index);
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int next = new Vector2Int(cell.x + dx[direction], cell.y + dy[direction]);
                    if (!IsWalkable(next)) continue;
                    int nextIndex = Index(next);
                    if (closed[nextIndex]) continue;
                    int nextG = g[index] + 1;
                    if (nextG >= g[nextIndex]) continue;
                    previous[nextIndex] = index;
                    g[nextIndex] = nextG;
                    open.Push(new QueueEntry
                    {
                        Index = nextIndex, G = nextG,
                        H = Manhattan(next, goal), Sequence = sequence++
                    });
                }
            }
            return false;
        }

        private int Index(Vector2Int cell) => cell.y * width + cell.x;
        private Vector2Int Cell(int index) => new Vector2Int(index % width, index / width);
        private static int Manhattan(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        /// <summary>Small in-game checks that run without Unity Test Framework/NUnit.</summary>
        public static bool RunDiagnostics(out string explanation)
        {
            var grid = new GridAStar(8, 8);
            var path = new List<Vector2Int>();
            for (int y = 1; y <= 6; y++) grid.SetBlocked(new Vector2Int(3, y));

            if (!grid.FindPath(new Vector2Int(1, 3), new Vector2Int(5, 3), path) ||
                path.Count <= 5 || !ValidSteps(grid, path))
            {
                explanation = "detour around obstacle failed";
                return false;
            }
            if (grid.FindPath(new Vector2Int(1, 3), new Vector2Int(3, 3), path) || path.Count != 0)
            {
                explanation = "blocked destination was accepted";
                return false;
            }
            grid.SetBlocked(new Vector2Int(3, 0));
            grid.SetBlocked(new Vector2Int(3, 7));
            if (grid.FindPath(new Vector2Int(1, 3), new Vector2Int(5, 3), path) || path.Count != 0)
            {
                explanation = "unreachable destination was accepted";
                return false;
            }
            if (!grid.FindPath(new Vector2Int(1, 3), new Vector2Int(1, 3), path) || path.Count != 1)
            {
                explanation = "same-cell path failed";
                return false;
            }
            explanation = "detour, blocked destination, unreachable destination, same cell: PASS";
            return true;
        }

        private static bool ValidSteps(GridAStar grid, List<Vector2Int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (!grid.IsWalkable(path[i])) return false;
                if (i == 0) continue;
                if (Manhattan(path[i - 1], path[i]) != 1) return false;
            }
            return true;
        }
    }
}
