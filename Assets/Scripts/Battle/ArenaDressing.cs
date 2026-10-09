using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>
    /// Scatters woodland props around the battle clearing: tall trees in a band along the far and
    /// side edges, small things (rocks, mushrooms, grass) all round, nothing tall in front of the
    /// camera. Purely visual; props carry no colliders. Seeded so the layout is stable.
    /// </summary>
    public static class ArenaDressing
    {
        public static void Scatter(Transform parent, Vector2 clearingHalf, float groundY,
                                   IReadOnlyList<GameObject> trees, IReadOnlyList<GameObject> smallProps, int seed,
                                   System.Func<Vector3, bool> blocked = null)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var placed = new List<Vector3>();

            bool Free(Vector3 p, float gap)
            {
                foreach (var q in placed)
                    if ((q - p).sqrMagnitude < gap * gap) return false;
                return blocked == null || !blocked(p);
            }

            void Place(GameObject prefab, Vector3 p, float scale)
            {
                var go = Object.Instantiate(prefab, parent);
                go.transform.SetLocalPositionAndRotation(p, Quaternion.Euler(0f, R(0f, 360f), 0f));
                go.transform.localScale = Vector3.one * scale;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.receiveShadows = true;
                placed.Add(p);
            }

            // Point on a rounded-rectangle ring around the clearing, pushed out by `margin`.
            Vector3 Ring(float t, float margin)
            {
                float c = Mathf.Cos(t), s = Mathf.Sin(t);
                float x = (clearingHalf.x + margin) * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.5f);
                float z = (clearingHalf.y + margin) * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.5f);
                return new Vector3(x, groundY, z);
            }

            if (trees != null && trees.Count > 0)
            {
                for (int i = 0; i < 160 && placed.Count < 60; i++)
                {
                    float t = R(0f, Mathf.PI * 2f);
                    var p = Ring(t, R(0.6f, 7f));
                    // Keep the near edge open so trees never hide the fight.
                    if (p.z < -clearingHalf.y + 1.5f) continue;
                    if (!Free(p, 1.5f)) continue;
                    Place(trees[rng.Next(trees.Count)], p, R(0.85f, 1.3f));
                }
            }

            if (smallProps != null && smallProps.Count > 0)
            {
                for (int i = 0; i < 120; i++)
                {
                    float t = R(0f, Mathf.PI * 2f);
                    var p = Ring(t, R(0.15f, 2.2f));
                    if (!Free(p, 0.8f)) continue;
                    Place(smallProps[rng.Next(smallProps.Count)], p, R(0.8f, 1.3f));
                }
            }
        }
    }
}
