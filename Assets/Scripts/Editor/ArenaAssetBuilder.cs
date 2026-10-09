using System.Collections.Generic;
using RougeLike.Battle;
using UnityEditor;
using UnityEngine;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Writes the battle arenas (Assets/ScriptableObjects/Battle/Arenas) from the layouts below, puts
    /// them in rotation on the Battle prefab and gives the first waves their home arena. Layouts live
    /// here as code so they're easy to tweak and rebuild: RougeLike > Build Arenas.
    /// Field: x in [-7.5, 7.5], z in [-5, 5]; the player's half is z &lt; 0. Keep terrain and solid
    /// pieces inside the no man's land strip (|z| &lt; noMansLand) or off the field.
    /// </summary>
    public static class ArenaAssetBuilder
    {
        const string Folder = "Assets/ScriptableObjects/Battle/Arenas";
        const string Models = "Assets/Art/Models/Arena/";
        const string BattlePrefab = "Assets/Prefabs/Battle/Battle.prefab";
        const string Waves = "Assets/ScriptableObjects/Battle/Waves/";

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        static GameObject M(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");

        static ArenaPiece P(string model, float x, float z, float yaw = 0f, float scale = 1f,
                            PieceCollider col = PieceCollider.Box, float y = 0f, float mass = 0f) =>
            new() { prefab = M(model), position = new Vector3(x, y, z), yaw = yaw, scale = scale, collider = col, mass = mass };

        [MenuItem("RougeLike/Build Arenas")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ScriptableObjects/Battle", "Arenas");
            var parchment = Hex("EAD9B0");
            var parchmentDark = Hex("CDB582");

            var clearing = Arena("01_ForestClearing", a =>
            {
                a.displayName = "Forest Clearing";
                a.description = "Open ground with nowhere to hide, and two old pits to shove the enemy into.";
                a.noMansLand = 1.2f;
                a.centreLine = true;
                a.boardColor = parchment;
                a.rimColor = parchmentDark;
                a.floorColor = Hex("4F6B3A");
                a.mounds = new List<TerrainMound>();
                a.streams = new List<ArenaStream>();
                a.holes = new List<ArenaHole>
                {
                    new() { center = new Vector2(-2.6f, 0f), radius = new Vector2(1.15f, 0.95f) },
                    new() { center = new Vector2(2.9f, 0.05f), radius = new Vector2(1.0f, 0.9f) },
                };
                a.pieces = new List<ArenaPiece>
                {
                    P("Rock", -4.6f, 0.3f, 20f, 0.9f, PieceCollider.Convex),
                    P("Stump", 4.4f, -0.2f, 0f, 1f, PieceCollider.Box),
                    P("Mushrooms", 4.95f, 0.45f, 0f, 1f, PieceCollider.None),
                    P("GrassTuft", -0.6f, 0.6f, 0f, 1f, PieceCollider.None),
                    P("GrassTuft", 1.9f, -0.5f, 0f, 1f, PieceCollider.None),
                };
                a.trees = new List<GameObject> { M("PineTree"), M("PineTree"), M("OakTree"), M("OakTree"), M("BirchTree"), M("Bush") };
                a.smallProps = new List<GameObject> { M("Rock"), M("Mushrooms"), M("GrassTuft"), M("GrassTuft"), M("Stump"), M("Bush") };
                a.dressingSeed = 7;
            });

            var brook = Arena("02_BrookCrossing", a =>
            {
                a.displayName = "Brook Crossing";
                a.description = "A shallow brook cuts the field. Wading is slow, the log bridge is not, and the sinkhole downstream swallows anyone pushed in.";
                a.noMansLand = 1.9f;
                a.centreLine = false;
                a.boardColor = Color.Lerp(parchment, Hex("8A9A4B"), 0.18f);
                a.rimColor = parchmentDark;
                a.floorColor = Hex("4F6B3A");
                var s = new ArenaStream
                {
                    z = 0f, halfWidth = 0.75f, bank = 0.65f, depth = 0.35f, meander = 0.3f, meanderFrequency = 0.45f,
                    waterLevel = -0.08f, wadeSpeed = 0.4f, waterColor = Hex("6F9C9E"),
                    bridges = new List<float> { 0f }, bridgeHalfWidth = 0.6f,
                };
                a.streams = new List<ArenaStream> { s };
                a.mounds = new List<TerrainMound>();
                a.holes = new List<ArenaHole> { new() { center = new Vector2(4.6f, s.CenterZ(4.6f)), radius = new Vector2(1.1f, 0.95f) } };
                // Pieces sit on the ground under them, the brook bed in the water; lily pads float.
                float pad = s.depth + s.waterLevel + 0.01f;
                a.pieces = new List<ArenaPiece>
                {
                    P("LogBridge", 0f, 0f, 0f, 1f, PieceCollider.None, y: -0.06f),
                    P("Rock", -4.6f, s.CenterZ(-4.6f), 30f, 1.15f, PieceCollider.Convex, y: -0.05f),
                    P("LilyPads", -2.4f, s.CenterZ(-2.4f) + 0.2f, 0f, 1f, PieceCollider.None, y: pad),
                    P("LilyPads", 6.6f, s.CenterZ(6.6f) - 0.2f, 70f, 0.9f, PieceCollider.None, y: pad),
                    P("LilyPads", -6.6f, s.CenterZ(-6.6f), 140f, 0.8f, PieceCollider.None, y: pad),
                    P("Reeds", -6.2f, s.CenterZ(-6.2f) + 1.3f, 0f, 1f, PieceCollider.None),
                    P("Reeds", -3.1f, s.CenterZ(-3.1f) - 1.35f, 40f, 0.9f, PieceCollider.None),
                    P("Reeds", -1.3f, s.CenterZ(-1.3f) + 1.3f, 80f, 1f, PieceCollider.None),
                    P("Reeds", 1.5f, s.CenterZ(1.5f) - 1.3f, 10f, 1.1f, PieceCollider.None),
                    P("Reeds", 3.2f, s.CenterZ(3.2f) + 1.35f, 0f, 0.9f, PieceCollider.None),
                    P("Reeds", 6.3f, s.CenterZ(6.3f) - 1.3f, 120f, 1f, PieceCollider.None),
                };
                a.trees = new List<GameObject> { M("OakTree"), M("OakTree"), M("PineTree"), M("Bush"), M("Bush") };
                a.smallProps = new List<GameObject> { M("Bush"), M("Reeds"), M("Reeds"), M("GrassTuft"), M("Rock") };
                a.dressingSeed = 21;
            });

            var ridge = Arena("03_BoulderRidge", a =>
            {
                a.displayName = "Boulder Ridge";
                a.description = "High ground, big rocks for cover and a crevasse in the saddle. Thrown boulders roll downhill.";
                a.noMansLand = 2.2f;
                a.centreLine = false;
                a.boardColor = Color.Lerp(parchment, Hex("8C8577"), 0.3f);
                a.rimColor = Hex("8C8577");
                a.floorColor = Color.Lerp(Hex("2F4A33"), Hex("4F6B3A"), 0.5f);
                a.mounds = new List<TerrainMound>
                {
                    // The ridge: two humps with a saddle between, flat passes at both flanks.
                    new() { center = new Vector2(-2.2f, 0.2f), radius = new Vector2(3.4f, 2.1f), height = 0.5f, flatTop = 0.25f },
                    new() { center = new Vector2(3.0f, -0.15f), radius = new Vector2(2.8f, 2.0f), height = 0.4f, flatTop = 0.2f },
                    // Knolls on the board's edge, outside the walls, for the look.
                    new() { center = new Vector2(-9.3f, 4.0f), radius = new Vector2(2.0f, 2.2f), height = 0.9f, flatTop = 0.1f },
                    new() { center = new Vector2(9.2f, 4.6f), radius = new Vector2(1.9f, 2.0f), height = 1.1f, flatTop = 0.1f },
                    new() { center = new Vector2(9.4f, -3.8f), radius = new Vector2(1.7f, 1.6f), height = 0.6f, flatTop = 0.1f },
                    new() { center = new Vector2(-9.4f, -4.2f), radius = new Vector2(1.6f, 1.5f), height = 0.5f, flatTop = 0.1f },
                };
                a.streams = new List<ArenaStream>();
                a.holes = new List<ArenaHole>
                {
                    new() { center = new Vector2(0.55f, -0.05f), radius = new Vector2(0.7f, 1.5f), squareness = 2.5f },
                    new() { center = new Vector2(-6.1f, 0.6f), radius = new Vector2(0.95f, 0.9f) },
                };
                a.pieces = new List<ArenaPiece>
                {
                    P("Boulder", -2.7f, 0.35f, 20f, 1.0f, PieceCollider.Convex, y: -0.05f),
                    P("Boulder", 2.5f, -0.45f, -40f, 0.85f, PieceCollider.Convex, y: -0.05f),
                    P("Rock", 5.9f, 0.8f, 10f, 1.1f, PieceCollider.Convex),
                    P("Boulder", -8.8f, 3.6f, 0f, 1.2f, PieceCollider.None),
                    P("Boulder", 8.9f, 4.3f, 140f, 1.4f, PieceCollider.None),
                    P("GrassTuft", -0.8f, -0.4f, 0f, 1f, PieceCollider.None),
                    P("GrassTuft", 4.2f, 0.6f, 0f, 1f, PieceCollider.None),
                };
                a.trees = new List<GameObject> { M("PineTree"), M("PineTree"), M("PineTree"), M("Boulder"), M("OakTree") };
                a.smallProps = new List<GameObject> { M("Rock"), M("Rock"), M("Rock"), M("GrassTuft"), M("Bush") };
                a.dressingSeed = 33;
            });

            var sawmill = Arena("04_OldSawmill", a =>
            {
                a.displayName = "Old Sawmill";
                a.description = "Log piles wall off both sides, and the open middle runs right past a saw pit. Mind the edge.";
                a.noMansLand = 1.8f;
                a.centreLine = false;
                a.boardColor = Color.Lerp(parchment, Hex("D19A3A"), 0.25f);
                a.rimColor = Hex("A87A4C");
                a.floorColor = new Color(0.45f, 0.38f, 0.2f);
                a.mounds = new List<TerrainMound>();
                a.streams = new List<ArenaStream>();
                // The saw pit sits at the end of the left log pile, on the edge of the open middle.
                a.holes = new List<ArenaHole> { new() { center = new Vector2(-1.75f, 0f), radius = new Vector2(0.75f, 1.25f), squareness = 6f } };
                a.pieces = new List<ArenaPiece>
                {
                    P("LogPile", -4.1f, 0.15f, 0f),
                    P("LogPile", 4.3f, -0.2f, 6f),
                    // Two stacked crates give a little cover in the open middle, beside the saw pit.
                    P("Crate", 1.1f, 0.2f, 18f),
                    P("Crate", 1.1f, 0.2f, -6f, y: 0.8f),
                    // Loose crates: units shove them about and boulders send them flying.
                    P("Crate", 2.0f, -1.05f, -12f, mass: 1.5f),
                    P("Crate", -6.6f, -0.5f, 40f, mass: 1.5f),
                    P("Crate", 6.7f, 0.6f, 0f, mass: 1.5f),
                    // Broken walls of the mill along the far side and the ends.
                    P("PlankWall", -3.5f, 6.3f, 4f, 1f, PieceCollider.None),
                    P("PlankWall", 2.6f, 6.4f, -6f, 1f, PieceCollider.None),
                    P("PlankWall", 8.55f, 2.4f, 90f),
                    P("PlankWall", -8.55f, -1.8f, -88f),
                    P("LogPile", -6.4f, 6.4f, 10f, 1f, PieceCollider.None),
                    P("Stump", 5.6f, 6.1f, 0f, 1.1f, PieceCollider.None),
                };
                a.trees = new List<GameObject> { M("BirchTree"), M("BirchTree"), M("BirchTree"), M("OakTree"), M("PineTree") };
                a.smallProps = new List<GameObject> { M("Stump"), M("Stump"), M("Crate"), M("GrassTuft"), M("Mushrooms"), M("Bush") };
                a.dressingSeed = 48;
            });

            // The Battle prefab: arenas in rotation.
            var root = PrefabUtility.LoadPrefabContents(BattlePrefab);
            try
            {
                var bm = root.GetComponentInChildren<BattleManager>(true);
                var so = new SerializedObject(bm);
                var list = so.FindProperty("arenas");
                var all = new[] { clearing, brook, ridge, sawmill };
                list.arraySize = all.Length;
                for (int i = 0; i < all.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BattlePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Each of the first waves gets a home that suits it; the Brute's boulders get a hill.
            SetWaveArena("Wave01_Grubs", clearing);
            SetWaveArena("Wave02_Clawlings", brook);
            SetWaveArena("Wave03_TheBrute", ridge);
            AssetDatabase.SaveAssets();
            Debug.Log("Built 4 arenas.");
        }

        static ArenaDefinition Arena(string file, System.Action<ArenaDefinition> fill)
        {
            string path = $"{Folder}/{file}.asset";
            var a = AssetDatabase.LoadAssetAtPath<ArenaDefinition>(path);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<ArenaDefinition>();
                AssetDatabase.CreateAsset(a, path);
            }
            fill(a);
            foreach (var p in a.pieces)
                if (p.prefab == null) Debug.LogWarning($"{file}: a piece has no model (not imported yet?)");
            EditorUtility.SetDirty(a);
            return a;
        }

        static void SetWaveArena(string wave, ArenaDefinition arena)
        {
            var w = AssetDatabase.LoadAssetAtPath<EnemyWave>(Waves + wave + ".asset");
            if (w == null) return;
            w.arena = arena;
            EditorUtility.SetDirty(w);
        }
    }
}
