using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Models exported from ArtSource import with the storybook toon shader. Blender stays the source
    /// of truth for colours: each embedded material keeps the base colour it was exported with.
    /// </summary>
    public class ToonModelPostprocessor : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/Art/Models/";

        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var shader = Shader.Find("RougeLike/Toon");
            if (shader == null) return;

            var color = Color.white;
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse)) color = diffuse;
            else if (description.TryGetProperty("BaseColor", out Vector4 baseColor)) color = baseColor;
            // FBX carries linear values; materials take sRGB colours in either colour space.
            color = color.gamma;
            color.a = 1f;

            material.shader = shader;
            material.SetColor("_Color", color);
            // Tags from toon_common.py: small trim parts (eyes, claw tips) would drown in their own
            // outline, and big flat ground gets painted-paper blotches.
            material.SetFloat("_OutlineWidth", description.materialName.Contains("_NoLine") ? 0f : 3f);
            if (description.materialName.Contains("_Paper"))
            {
                material.SetFloat("_GrainStrength", 0.08f);
                material.SetFloat("_GrainScale", 1.6f);
            }
        }
    }
}
