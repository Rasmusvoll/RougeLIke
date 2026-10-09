using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// A coarse walk grid over the field so units can find their way round pits and fixed obstacles
    /// (log piles, rocks, crate stacks) instead of pressing into them. Built once per battle; loose
    /// props and units don't block it, they just get shoved.
    /// </summary>
    public class ArenaNav
    {
        const float Cell = 0.5f;
        const float Clearance = 0.2f; // extra room kept round a blocker, on top of half a cell

        readonly Vector2 min;
        readonly int nx, nz;
        readonly bool[,] blocked;
        readonly Collider[] overlap = new Collider[16];

        // A* scratch, reused between searches.
        readonly float[,] cost;
        readonly Vector2Int[,] cameFrom;
        readonly int[,] visit;
        int search;

        public ArenaNav(Arena arena, Vector2 halfExtent)
        {
            min = -halfExtent;
            nx = Mathf.CeilToInt(halfExtent.x * 2f / Cell);
            nz = Mathf.CeilToInt(halfExtent.y * 2f / Cell);
            blocked = new bool[nx, nz];
            cost = new float[nx, nz];
            cameFrom = new Vector2Int[nx, nz];
            visit = new int[nx, nz];

            Physics.SyncTransforms();
            var half = new Vector3(Cell * 0.5f + Clearance, 0.3f, Cell * 0.5f + Clearance);
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                var p = Center(i, j);
                float ground = arena.HeightAt(p.x, p.z);
                bool hole = false;
                foreach (var h in arena.Definition.holes)
                    if (h.Distance(p.x, p.z) < 1f + (Clearance + Cell * 0.5f) / Mathf.Min(h.radius.x, h.radius.y)) hole = true;
                blocked[i, j] = hole || Solid(new Vector3(p.x, ground + 0.55f, p.z), half);
            }
        }

        /// <summary>A fixed obstacle overlaps this box. Ground, walls, units and loose props don't count.</summary>
        bool Solid(Vector3 center, Vector3 half)
        {
            int n = Physics.OverlapBoxNonAlloc(center, half, overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < n; k++)
            {
                var c = overlap[k];
                if (c is MeshCollider { convex: false }) continue; // the board itself
                if (c.attachedRigidbody != null) continue;
                if (c.name == "Wall") continue;
                return true;
            }
            return false;
        }

        Vector3 Center(int i, int j) => new(min.x + (i + 0.5f) * Cell, 0f, min.y + (j + 0.5f) * Cell);

        Vector2Int CellOf(Vector3 p) => new(
            Mathf.Clamp(Mathf.FloorToInt((p.x - min.x) / Cell), 0, nx - 1),
            Mathf.Clamp(Mathf.FloorToInt((p.z - min.y) / Cell), 0, nz - 1));

        bool Free(int i, int j) => i >= 0 && j >= 0 && i < nx && j < nz && !blocked[i, j];

        /// <summary>The nearest free cell, searching outward in rings.</summary>
        Vector2Int NearestFree(Vector2Int c)
        {
            if (Free(c.x, c.y)) return c;
            for (int r = 1; r < 6; r++)
            for (int di = -r; di <= r; di++)
            for (int dj = -r; dj <= r; dj++)
            {
                if (Mathf.Max(Mathf.Abs(di), Mathf.Abs(dj)) != r) continue;
                if (Free(c.x + di, c.y + dj)) return new Vector2Int(c.x + di, c.y + dj);
            }
            return c;
        }

        /// <summary>Walking straight from a to b crosses no blocked cell.</summary>
        public bool Clear(Vector3 a, Vector3 b)
        {
            var d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            int steps = Mathf.CeilToInt(d.magnitude / (Cell * 0.5f));
            for (int s = 1; s < steps; s++)
            {
                var c = CellOf(a + d * (s / (float)steps));
                if (blocked[c.x, c.y]) return false;
            }
            return true;
        }

        /// <summary>
        /// Where to head next on the way from `from` to `to`: `to` itself when the way is clear,
        /// otherwise the furthest point along the grid path that can be walked to in a straight line.
        /// </summary>
        public Vector3 NextWaypoint(Vector3 from, Vector3 to)
        {
            if (Clear(from, to)) return to;
            var start = NearestFree(CellOf(from));
            var goal = NearestFree(CellOf(to));
            var path = FindPath(start, goal);
            if (path == null) return to;
            Vector3 best = Center(path[0].x, path[0].y);
            for (int k = path.Count - 1; k >= 0; k--)
            {
                var p = Center(path[k].x, path[k].y);
                if (Clear(from, p)) { best = p; break; }
            }
            best.y = from.y;
            return best;
        }

        List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            search++;
            var open = new List<Vector2Int> { start };
            cost[start.x, start.y] = 0f;
            visit[start.x, start.y] = search;
            cameFrom[start.x, start.y] = start;
            while (open.Count > 0)
            {
                // The grid is small, so a linear scan for the best open node is fine.
                int bi = 0;
                float bf = float.MaxValue;
                for (int k = 0; k < open.Count; k++)
                {
                    var o = open[k];
                    float f = cost[o.x, o.y] + Vector2Int.Distance(o, goal);
                    if (f < bf) { bf = f; bi = k; }
                }
                var cur = open[bi];
                open.RemoveAt(bi);
                if (cur == goal) break;
                for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = cur.x + di, j = cur.y + dj;
                    if (!Free(i, j)) continue;
                    if (di != 0 && dj != 0 && (!Free(cur.x + di, cur.y) || !Free(cur.x, cur.y + dj))) continue; // no corner cutting
                    float c = cost[cur.x, cur.y] + (di != 0 && dj != 0 ? 1.414f : 1f);
                    if (visit[i, j] == search && c >= cost[i, j]) continue;
                    if (visit[i, j] != search) open.Add(new Vector2Int(i, j));
                    visit[i, j] = search;
                    cost[i, j] = c;
                    cameFrom[i, j] = cur;
                }
            }
            if (visit[goal.x, goal.y] != search) return null;
            var path = new List<Vector2Int>();
            for (var c = goal; c != start; c = cameFrom[c.x, c.y]) path.Add(c);
            path.Reverse();
            return path.Count > 0 ? path : null;
        }
    }
}
