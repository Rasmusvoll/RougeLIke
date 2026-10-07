using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>A small billboard bar above a unit, built from two unlit quads.</summary>
    public class HealthBar : MonoBehaviour
    {
        const float Width = 0.9f, Height = 0.1f;

        Transform fill;
        Camera cam;

        public static HealthBar Create(Transform parent, float height, Color color)
        {
            var go = new GameObject("Health Bar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var bar = go.AddComponent<HealthBar>();
            Quad(go.transform, "Back", new Color(0.08f, 0.08f, 0.1f), Vector3.zero, new Vector3(Width + 0.06f, Height + 0.06f, 1f));
            bar.fill = Quad(go.transform, "Fill", color, new Vector3(0f, 0f, -0.01f), new Vector3(Width, Height, 1f));
            return bar;
        }

        public void Set(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            fill.localScale = new Vector3(Width * fraction, Height, 1f);
            fill.localPosition = new Vector3(-Width * (1f - fraction) * 0.5f, 0f, -0.01f);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        static Transform Quad(Transform parent, string name, Color color, Vector3 pos, Vector3 scale)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = BattleVisuals.Unlit(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q.transform;
        }
    }
}
