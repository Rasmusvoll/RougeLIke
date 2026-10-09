using System.Linq;
using RougeLike.Battle;
using RougeLike.Robots;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Writes Assets/Scenes/DriveTest.unity: the battle camera and lights, plus a DriveTest with every
    /// arena in rotation. RougeLike > Open Drive Test builds it (or rebuilds it) and opens it; press Play
    /// to watch two teams of robots fight on their own.
    /// </summary>
    public static class DriveTestSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/DriveTest.unity";
        const string ArenaFolder = "Assets/ScriptableObjects/Battle/Arenas";

        [MenuItem("RougeLike/Open Drive Test")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Same view as the battle camera, so the robots read at the size they'll fight at.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 13f, -10.8f), Quaternion.Euler(55f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.184f, 0.29f, 0.2f);
            camGo.AddComponent<AudioListener>();

            Light("Key Light", new Quaternion(-0.4082179f, 0.2345698f, -0.10938169f, -0.8754261f),
                  new Color(1f, 0.94f, 0.86f), 1f, LightShadows.Soft);
            Light("Fill Light", new Quaternion(0.056018703f, 0.9430295f, -0.20906462f, 0.25268403f),
                  new Color(1f, 0.9f, 0.77f), 0.35f, LightShadows.None);

            var test = new GameObject("Drive Test").AddComponent<DriveTest>();
            var arenas = AssetDatabase.FindAssets("t:ArenaDefinition", new[] { ArenaFolder })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .OrderBy(path => path)
                .Select(path => AssetDatabase.LoadAssetAtPath<ArenaDefinition>(path))
                .Where(a => a != null)
                .ToList();
            var so = new SerializedObject(test);
            var list = so.FindProperty("arenas");
            list.arraySize = arenas.Count;
            for (int i = 0; i < arenas.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = arenas[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Drive test scene written to {ScenePath} with {arenas.Count} arenas. Press Play to watch the robots fight.");
        }

        static void Light(string name, Quaternion rotation, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
        }
    }
}
