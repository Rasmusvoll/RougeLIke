using UnityEngine;

namespace RougeLike.Battle
{
    /// <summary>Throwaway hit effects: a puff that pops out and fades.</summary>
    public class BattleEffects : MonoBehaviour
    {
        const float Life = 0.18f;

        float age, size;
        Renderer rend;
        MaterialPropertyBlock block;
        Color color;

        public static void Spark(Vector3 point, float size, Color color)
        {
            var go = BattleVisuals.Primitive(PrimitiveType.Sphere, "Hit Spark", null, BattleVisuals.Unlit(Color.white));
            go.transform.position = point;
            go.transform.localScale = Vector3.one * size * 0.3f;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fx = go.AddComponent<BattleEffects>();
            fx.size = size;
            fx.rend = r;
            fx.color = color;
            fx.block = new MaterialPropertyBlock();
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * size * Mathf.Lerp(0.3f, 1f, Mathf.Sqrt(t));
            // White flash cooling to the hit colour.
            block.SetColor("_Color", Color.Lerp(Color.white, color, t));
            rend.SetPropertyBlock(block);
        }
    }
}
