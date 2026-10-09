using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// The built battlefield for one ArenaDefinition: a rounded board whose ground follows the
    /// terrain (hills, brook beds) with a mesh collider, water, obstacle pieces and the woodland
    /// around it. Answers questions about the ground (height, water) for units and projectiles.
    /// </summary>
    public class Arena : MonoBehaviour
    {
        const float CellSize = 0.28f;
        const float RimDepth = 0.35f;
        const float SuperellipsePower = 4f;
        // Heights where the ground changes shade and an inked contour line is drawn, like a map.
        static readonly float[] ContourLevels = { 0.12f, 0.3f, 0.5f, 0.75f };
        // Pits drop this far: well past the depth where a falling unit is counted as dead.
        const float PitDepth = 6f;
        const float PitShade = -0.4f; // ground below this is drawn as the dark inside of a pit

        public static Arena Current { get; private set; }

        public ArenaDefinition Definition { get; private set; }
        /// <summary>Paths round pits and fixed obstacles for walking units.</summary>
        public ArenaNav Nav { get; private set; }

        public static Arena Build(ArenaDefinition def, Transform parent, Vector2 boardHalf)
        {
            var go = new GameObject($"Arena: {def.displayName}");
            go.transform.SetParent(parent, false);
            var arena = go.AddComponent<Arena>();
            arena.Definition = def;
            Current = arena;
            arena.BuildFloor();
            arena.BuildBoard(boardHalf);
            foreach (var s in def.streams) arena.BuildWater(s, boardHalf);
            foreach (var h in def.holes) arena.BuildPitMouth(h);
            foreach (var p in def.pieces) arena.Place(p);
            arena.Nav = new ArenaNav(arena, new Vector2(8.25f, 5.75f));

            var woods = new GameObject("Woodland").transform;
            woods.SetParent(go.transform, false);
            ArenaDressing.Scatter(woods, boardHalf, -0.1f, def.trees, def.smallProps, def.dressingSeed,
                                  p => arena.WaterAt(p.x, p.z, 0.9f) != null);
            return arena;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // Ground queries

        /// <summary>Height of the bare ground (no props) at a point.</summary>
        public float HeightAt(float x, float z)
        {
            float h = 0f;
            foreach (var m in Definition.mounds)
            {
                float dx = (x - m.center.x) / Mathf.Max(0.01f, m.radius.x);
                float dz = (z - m.center.y) / Mathf.Max(0.01f, m.radius.y);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                h += m.height * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(m.flatTop, 1f, d)));
            }
            foreach (var s in Definition.streams)
                h -= s.depth * StreamFactor(s, x, z, 0f);
            // Pits: a sheer drop just inside the edge.
            foreach (var hole in Definition.holes)
            {
                float d = hole.Distance(x, z);
                if (d < 1f) h = Mathf.Lerp(h, -PitDepth, Mathf.InverseLerp(1f, 0.8f, d));
            }
            return h;
        }

        /// <summary>Inside a pit (scaled by `scale` radii).</summary>
        public bool InHole(float x, float z, float scale = 1f)
        {
            foreach (var hole in Definition.holes)
                if (hole.Distance(x, z) < scale) return true;
            return false;
        }

        /// <summary>
        /// Bends a walking direction round any pit ahead, so units only end up in one when shoved.
        /// Returns the direction unchanged when no pit is near.
        /// </summary>
        public Vector3 SteerAroundHoles(Vector3 pos, Vector3 dir, float radius)
        {
            if (Definition.holes.Count == 0 || dir == Vector3.zero) return dir;
            var steered = dir;
            foreach (var hole in Definition.holes)
            {
                var c = new Vector3(hole.center.x, pos.y, hole.center.y);
                var away = new Vector3(pos.x - c.x, 0f, pos.z - c.z);
                float holeR = Mathf.Max(hole.radius.x, hole.radius.y);
                float clearance = away.magnitude - holeR - radius;
                if (clearance > 1.2f) continue;
                var outward = away.sqrMagnitude > 1e-4f ? away.normalized : -dir;
                float heading = Vector3.Dot(dir, -outward); // > 0: walking toward the pit
                if (heading <= 0f && clearance > 0.2f) continue;
                // Slide along the rim (on whichever side is closer to the way we were going),
                // pushing outward harder the closer we get.
                var tangent = Vector3.Cross(Vector3.up, outward);
                if (Vector3.Dot(tangent, dir) < 0f) tangent = -tangent;
                float urgency = 1f - Mathf.Clamp01(clearance / 1.2f);
                steered = Vector3.Lerp(steered, tangent, urgency * Mathf.Max(heading, 0.3f)) + outward * urgency * 0.8f;
            }
            return steered.normalized * dir.magnitude;
        }

        /// <summary>1 in the brook bed, falling to 0 at the top of the banks and under bridges.</summary>
        static float StreamFactor(ArenaStream s, float x, float z, float widen)
        {
            float across = Mathf.Abs(z - s.CenterZ(x));
            float f = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.halfWidth + widen, s.halfWidth + s.bank + widen, across));
            foreach (var bx in s.bridges)
                f *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.bridgeHalfWidth, s.bridgeHalfWidth + 0.12f, Mathf.Abs(x - bx)));
            return f;
        }

        public Vector3 NormalAt(float x, float z)
        {
            const float e = 0.1f;
            float hx = HeightAt(x + e, z) - HeightAt(x - e, z);
            float hz = HeightAt(x, z + e) - HeightAt(x, z - e);
            return new Vector3(-hx, 2f * e, -hz).normalized;
        }

        /// <summary>The brook at this point (within `margin` of its water), or null.</summary>
        public ArenaStream WaterAt(float x, float z, float margin = 0f)
        {
            foreach (var s in Definition.streams)
                if (HeightAt(x, z) < s.waterLevel + margin && StreamFactor(s, x, z, margin) > 0.01f) return s;
            return null;
        }

        /// <summary>How deep something standing with its feet at `pos` is in water (0 when dry).</summary>
        public float WadeDepth(Vector3 pos)
        {
            var s = WaterAt(pos.x, pos.z);
            return s == null ? 0f : Mathf.Max(0f, s.waterLevel - pos.y);
        }

        /// <summary>Walking speed multiplier at a point: slow in the brook.</summary>
        public float SpeedFactor(Vector3 pos)
        {
            var s = WaterAt(pos.x, pos.z);
            return s != null && pos.y < s.waterLevel ? s.wadeSpeed : 1f;
        }

        // Building

        void BuildFloor()
        {
            var floor = BattleVisuals.Primitive(PrimitiveType.Cube, "Forest Floor", transform, BattleVisuals.Toon(Definition.floorColor, 0f, 0.1f));
            floor.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            floor.transform.localScale = new Vector3(90f, 1f, 70f);
        }

        /// <summary>
        /// Maps a point of the unit square [-1, 1]² onto the rounded board: each square ring maps to a
        /// scaled copy of the board's superellipse outline (with a little hand-drawn wobble).
        /// </summary>
        static Vector2 BoardPoint(float u, float v, Vector2 half)
        {
            float r = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
            if (r < 1e-5f) return Vector2.zero;
            var d = new Vector2(u, v).normalized;
            float t = Mathf.Atan2(d.y, d.x);
            float p = SuperellipsePower;
            float edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(d.x), p) + Mathf.Pow(Mathf.Abs(d.y), p), -1f / p);
            float wobble = 1f + 0.012f * Mathf.Sin(t * 7f) + 0.008f * Mathf.Sin(t * 13f + 1f);
            return new Vector2(d.x * half.x, d.y * half.y) * (r * edge * wobble);
        }

        /// <summary>The board: a faceted ground mesh following the terrain, with a darker rim round its edge.</summary>
        void BuildBoard(Vector2 half)
        {
            int nx = Mathf.CeilToInt(half.x * 2f / CellSize), nz = Mathf.CeilToInt(half.y * 2f / CellSize);
            var grid = new Vector3[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int j = 0; j <= nz; j++)
            {
                var p = BoardPoint(i * 2f / nx - 1f, j * 2f / nz - 1f, half);
                grid[i, j] = new Vector3(p.x, HeightAt(p.x, p.y), p.y);
            }

            var verts = new List<Vector3>();
            // One list per height band (below the first contour, then above each), then the rim.
            var bands = new List<int>[ContourLevels.Length + 2];
            for (int k = 0; k < bands.Length; k++) bands[k] = new List<int>();
            var rim = new List<int>();
            List<int> Band(Vector3 a, Vector3 b, Vector3 c)
            {
                float h = (a.y + b.y + c.y) / 3f;
                if (h < PitShade) return bands[ContourLevels.Length + 1];
                int k = 0;
                while (k < ContourLevels.Length && h > ContourLevels[k]) k++;
                return bands[k];
            }
            // Unshared vertices per triangle give flat facets, the low-poly look.
            void Tri(List<int> into, Vector3 a, Vector3 b, Vector3 c)
            {
                into.Add(verts.Count); verts.Add(a);
                into.Add(verts.Count); verts.Add(b);
                into.Add(verts.Count); verts.Add(c);
            }
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 a = grid[i, j], b = grid[i + 1, j], c = grid[i + 1, j + 1], d = grid[i, j + 1];
                Tri(Band(a, d, c), a, d, c);
                Tri(Band(a, c, b), a, c, b);
            }

            // The rim: walk the grid's outer ring (counter-clockwise from above) and drop a wall.
            var ring = new List<Vector3>();
            for (int i = 0; i < nx; i++) ring.Add(grid[i, 0]);
            for (int j = 0; j < nz; j++) ring.Add(grid[nx, j]);
            for (int i = nx; i > 0; i--) ring.Add(grid[i, nz]);
            for (int j = nz; j > 0; j--) ring.Add(grid[0, j]);
            for (int k = 0; k < ring.Count; k++)
            {
                var a = ring[k];
                var b = ring[(k + 1) % ring.Count];
                var a2 = new Vector3(a.x, -RimDepth, a.z);
                var b2 = new Vector3(b.x, -RimDepth, b.z);
                Tri(rim, a, b, b2);
                Tri(rim, a, b2, a2);
            }

            var mesh = new Mesh { name = "Board", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.subMeshCount = bands.Length + 1;
            for (int k = 0; k < bands.Length; k++) mesh.SetTriangles(bands[k], k);
            mesh.SetTriangles(rim, bands.Length);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var board = new GameObject("Board");
            board.transform.SetParent(transform, false);
            board.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = board.AddComponent<MeshRenderer>();
            // Higher ground is paler, as if sun-bleached; the rim is the board's darker edge.
            var mats = new Material[bands.Length + 1];
            for (int k = 0; k <= ContourLevels.Length; k++)
                mats[k] = BattleVisuals.Toon(Color.Lerp(Definition.boardColor, new Color(0.98f, 0.95f, 0.86f), k * 0.22f), 0f, 0.08f);
            mats[ContourLevels.Length + 1] = BattleVisuals.Toon(Color.Lerp(BattleVisuals.Palette.Ink, Definition.rimColor, 0.2f), 0f);
            mats[bands.Length] = BattleVisuals.Toon(Definition.rimColor, 0f);
            mr.sharedMaterials = mats;
            mr.receiveShadows = true;
            board.AddComponent<MeshCollider>().sharedMesh = mesh;
            BuildContours(grid, nx, nz);
        }

        /// <summary>Inked contour lines over the hills (marching squares on the board grid).</summary>
        void BuildContours(Vector3[,] grid, int nx, int nz)
        {
            const float Width = 0.035f, Lift = 0.012f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var cuts = new List<Vector3>(4);
            var levels = new List<float>(ContourLevels) { PitShade * 0.5f };
            foreach (float level in levels)
            {
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    cuts.Clear();
                    var corners = new[] { grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1] };
                    for (int e = 0; e < 4; e++)
                    {
                        Vector3 a = corners[e], b = corners[(e + 1) % 4];
                        if ((a.y - level) * (b.y - level) >= 0f) continue;
                        cuts.Add(Vector3.Lerp(a, b, (level - a.y) / (b.y - a.y)));
                    }
                    for (int c = 0; c + 1 < cuts.Count; c += 2)
                    {
                        Vector3 p = cuts[c] + Vector3.up * Lift, q = cuts[c + 1] + Vector3.up * Lift;
                        var side = Vector3.Cross(Vector3.up, q - p).normalized * Width;
                        // Overlap a little along the line so neighbouring pieces join up.
                        var along = (q - p).normalized * Width;
                        int v = verts.Count;
                        verts.Add(p - side - along); verts.Add(p + side - along);
                        verts.Add(q + side + along); verts.Add(q - side + along);
                        tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3, v, v + 2, v + 1, v, v + 3, v + 2 });
                    }
                }
            }
            if (verts.Count == 0) return;
            var mesh = new Mesh { name = "Contours", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Contours");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BattleVisuals.Unlit(Color.Lerp(BattleVisuals.Palette.Ink, Definition.boardColor, 0.35f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A flat water strip along the brook, running on past the board into the woods.</summary>
        void BuildWater(ArenaStream s, Vector2 half)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float w = s.halfWidth + s.bank * 0.55f;
            float x0 = -half.x - 8f, x1 = half.x + 8f;
            // A grid of quads, so the ones over a pit can be left out: the brook drains into it.
            const int Across = 8;
            int n = Mathf.CeilToInt((x1 - x0) / 0.25f);
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)n);
                float z = s.CenterZ(x);
                for (int k = 0; k <= Across; k++)
                    verts.Add(new Vector3(x, s.waterLevel, z - w + 2f * w * k / Across));
            }
            for (int i = 0; i < n; i++)
            for (int k = 0; k < Across; k++)
            {
                int a = i * (Across + 1) + k, b = a + Across + 1;
                var mid = (verts[a] + verts[b + 1]) * 0.5f;
                if (InHole(mid.x, mid.z, 0.9f)) continue; // the pit's mouth, drawn just above, hides the ragged edge
                tris.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            var mesh = new Mesh { name = "Water" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var water = new GameObject("Water");
            water.transform.SetParent(transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BattleVisuals.Toon(s.waterColor, 0f, 0.05f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Light ripple streaks on the surface, so it reads as running water.
            var ripple = BattleVisuals.Toon(Color.Lerp(s.waterColor, Color.white, 0.45f), 0f);
            var rng = new System.Random(s.GetHashCode() ^ 91);
            for (int i = 0; i < 34; i++)
            {
                float x = Mathf.Lerp(x0 + 6f, x1 - 6f, (float)rng.NextDouble());
                bool underBridge = s.bridges.Exists(bx => Mathf.Abs(x - bx) < s.bridgeHalfWidth + 0.6f);
                if (underBridge) continue;
                float z = s.CenterZ(x) + ((float)rng.NextDouble() - 0.5f) * w * 1.4f;
                if (InHole(x, z, 1.4f)) continue;
                var streak = BattleVisuals.Primitive(PrimitiveType.Cube, "Ripple", water.transform, ripple);
                streak.transform.localPosition = new Vector3(x, s.waterLevel + 0.005f, z);
                streak.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan(s.meander * s.meanderFrequency * Mathf.Cos(x * s.meanderFrequency)) * Mathf.Rad2Deg, 0f);
                streak.transform.localScale = new Vector3(0.3f + (float)rng.NextDouble() * 0.5f, 0.004f, 0.05f);
                streak.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// The dark mouth of a pit with an inked rim, laid over the hole so it reads as a clean shape
        /// (the ground mesh under it is only as round as its grid). No collider: units fall through.
        /// </summary>
        void BuildPitMouth(ArenaHole hole)
        {
            const int Segments = 48;
            const float RimWidth = 0.09f, Lift = 0.008f;
            var verts = new List<Vector3>();
            var mouth = new List<int>();
            var rim = new List<int>();
            Vector3 Edge(float t, float scale)
            {
                // A point on the hole's superellipse outline.
                float c = Mathf.Cos(t), s = Mathf.Sin(t), p = 2f / hole.squareness;
                float x = hole.center.x + hole.radius.x * scale * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), p);
                float z = hole.center.y + hole.radius.y * scale * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), p);
                return new Vector3(x, 0f, z);
            }
            float avg = 0f;
            for (int i = 0; i < Segments; i++)
            {
                float t = i * Mathf.PI * 2f / Segments;
                var outer = Edge(t, 1f);
                // Rim height from the ground just outside the drop.
                var probe = Edge(t, 1.15f);
                // In the brook the mouth sits on the water instead.
                var water = WaterAt(probe.x, probe.z);
                float y = Mathf.Max(HeightAt(probe.x, probe.z), water != null ? water.waterLevel : float.MinValue) + Lift;
                avg += y / Segments;
                outer.y = y;
                var rimOut = Edge(t, 1f) + (Edge(t, 1f) - new Vector3(hole.center.x, 0f, hole.center.y)).normalized * RimWidth;
                rimOut.y = y;
                verts.Add(outer);
                verts.Add(rimOut);
            }
            int centre = verts.Count;
            verts.Add(new Vector3(hole.center.x, avg, hole.center.y));
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2, b = ((i + 1) % Segments) * 2;
                mouth.AddRange(new[] { centre, b, a });
                rim.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }
            var mesh = new Mesh { name = "Pit Mouth" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(mouth, 0);
            mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Pit");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                BattleVisuals.Unlit(new Color(0.11f, 0.08f, 0.07f)),
                BattleVisuals.Unlit(BattleVisuals.Palette.Ink),
            };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Place(ArenaPiece piece)
        {
            if (piece.prefab == null) return;
            var p = piece.position;
            var go = Instantiate(piece.prefab, transform);
            go.name = piece.prefab.name;
            go.transform.SetLocalPositionAndRotation(new Vector3(p.x, HeightAt(p.x, p.z) + p.y, p.z), Quaternion.Euler(0f, piece.yaw, 0f));
            go.transform.localScale = Vector3.one * piece.scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.receiveShadows = true;

            switch (piece.collider)
            {
                case PieceCollider.Box:
                    FitBox(go);
                    break;
                case PieceCollider.Convex:
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        mf.gameObject.AddComponent<MeshCollider>().convex = true;
                    break;
            }
            if (piece.mass > 0f && piece.collider != PieceCollider.None)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = piece.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        static void FitBox(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var t = go.transform;
            // Bounds in the piece's own space, so a rotated piece keeps a snug box.
            var b = new Bounds();
            bool first = true;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    var local = t.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (first) { b = new Bounds(local, Vector3.zero); first = false; }
                    else b.Encapsulate(local);
                }
            }
            var c = go.AddComponent<BoxCollider>();
            c.center = b.center;
            c.size = b.size;
        }
    }
}
