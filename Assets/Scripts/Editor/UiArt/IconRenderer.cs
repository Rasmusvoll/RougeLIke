using System.Collections.Generic;
using System.IO;
using RougeLike.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Gives every body, part and buff an icon. Bodies and parts are photographed: the model is
    /// placed in a private preview scene under a warm key light and rendered with the toon shader
    /// onto a transparent background. Buffs get a drawn emblem. Re-run it whenever content is added
    /// or a model changes; it overwrites the PNGs and assigns each definition's icon.
    /// </summary>
    public static class IconRenderer
    {
        public const string IconsFolder = "Assets/Art/Icons";
        const int Size = 256;
        const int RenderSize = 768;
        const float Padding = 0.08f;
        const float FieldOfView = 22f;
        const float Pitch = 22f;
        const float BodyYaw = 155f;
        const float PartYaw = 120f;
        const float OutlinePx = 12f;

        [MenuItem("RougeLike/UI/Render Content Icons")]
        public static void RenderAll()
        {
            int count = 0;
            foreach (var body in Load<BodyDefinition>())
                if (body.prefab != null && Assign(body, Photograph(body.prefab, BodyYaw), "Bodies")) count++;
            foreach (var part in Load<PartDefinition>())
                if (part.prefab != null && Assign(part, Photograph(part.prefab, PartYaw), "Parts")) count++;
            foreach (var buff in Load<BuffDefinition>())
                if (Assign(buff, UiArtGenerator.BuffEmblem(buff, Size), "Buffs")) count++;
            AssetDatabase.SaveAssets();
            Debug.Log($"Rendered {count} content icons into {IconsFolder}");
        }

        static List<T> Load<T>() where T : Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        static bool Assign(ContentDefinition def, Texture2D tex, string folder)
        {
            if (tex == null) return false;
            var path = $"{IconsFolder}/{folder}/{def.name}.png";
            UiArtGenerator.Save(tex, path);
            UiArtGenerator.ConfigureSprite(path);
            def.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(def);
            return def.icon != null;
        }

        /// <summary>Renders a model from a three-quarter view, framed to fill the icon.</summary>
        public static Texture2D Photograph(GameObject prefab, float yaw)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            try
            {
                var model = (GameObject)Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(model, scene);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
                ThickenOutlines(model);

                var lightGo = new GameObject("Key Light");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.86f);
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

                var camGo = new GameObject("Icon Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.fieldOfView = FieldOfView;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;
                cam.targetTexture = rt;

                var bounds = WorldBounds(model);
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * 0.98f;
                var rot = Quaternion.Euler(Pitch, 0f, 0f);
                cam.transform.SetPositionAndRotation(bounds.center - rot * Vector3.forward * distance, rot);
                cam.farClipPlane = distance + radius * 2f;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;
                UnpremultiplyEdges(shot);
                var icon = CropToContent(shot);
                Object.DestroyImmediate(shot);
                return icon;
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>Icons are shown small, so the ink lines are drawn a little heavier than in game.</summary>
        static void ThickenOutlines(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_OutlineWidth") || mats[i].GetFloat("_OutlineWidth") <= 0f) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    block.SetFloat("_OutlineWidth", OutlinePx);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// Frames the model tightly: crops the render to its opaque pixels plus a little padding and
        /// scales it down to the icon size, which also smooths the edges.
        /// </summary>
        static Texture2D CropToContent(Texture2D src)
        {
            var px = src.GetPixels32();
            int minX = src.width, minY = src.height, maxX = -1, maxY = -1;
            for (int y = 0; y < src.height; y++)
            for (int x = 0; x < src.width; x++)
            {
                if (px[y * src.width + x].a < 8) continue;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            if (maxX < 0) { minX = minY = 0; maxX = maxY = src.width - 1; }

            float side = Mathf.Max(maxX - minX, maxY - minY) * (1f + Padding * 2f);
            var center = new Vector2(minX + maxX, minY + maxY) * 0.5f;
            var origin = center - Vector2.one * side * 0.5f;
            var dst = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var outPx = new Color[Size * Size];
            const int taps = 3;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Average a few samples per icon pixel, weighting colour by alpha.
                Vector4 sum = Vector4.zero;
                float alpha = 0f;
                for (int sy = 0; sy < taps; sy++)
                for (int sx = 0; sx < taps; sx++)
                {
                    float u = (origin.x + (x + (sx + 0.5f) / taps) / Size * side) / src.width;
                    float v = (origin.y + (y + (sy + 0.5f) / taps) / Size * side) / src.height;
                    var c = u < 0f || v < 0f || u > 1f || v > 1f ? Color.clear : src.GetPixelBilinear(u, v);
                    sum += new Vector4(c.r, c.g, c.b, 1f) * c.a;
                    alpha += c.a;
                }
                outPx[y * Size + x] = alpha > 0f
                    ? new Color(sum.x / alpha, sum.y / alpha, sum.z / alpha, alpha / (taps * taps))
                    : Color.clear;
            }
            dst.SetPixels(outPx);
            dst.Apply();
            return dst;
        }

        /// <summary>
        /// Antialiased edges were blended against the transparent black clear colour, which darkens
        /// them. Dividing by alpha restores the true colour of those pixels.
        /// </summary>
        static void UnpremultiplyEdges(Texture2D tex)
        {
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a <= 0f || c.a >= 1f) continue;
                px[i] = new Color(Mathf.Clamp01(c.r / c.a), Mathf.Clamp01(c.g / c.a), Mathf.Clamp01(c.b / c.a), c.a);
            }
            tex.SetPixels(px);
            tex.Apply();
        }
    }
}
