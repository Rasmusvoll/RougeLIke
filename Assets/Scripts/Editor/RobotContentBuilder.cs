using System.Collections.Generic;
using RougeLike.Battle;
using RougeLike.Robots;
using RougeLike.Units;
using UnityEditor;
using UnityEngine;

namespace RougeLike.EditorTools
{
    /// <summary>
    /// Writes the robot content as code so it's easy to tweak and rebuild: RougeLike > Build Robot
    /// Content. Makes the chassis and parts (weapons from the robot fighting shows), puts them in the
    /// content database, gives new runs a robot starter set, swaps the reward table's bodies and parts
    /// for robot ones (buffs stay), writes robot enemy waves onto the Battle prefab and renders icons.
    /// Creature content stays in the database so old saves and scenes still load.
    /// </summary>
    public static class RobotContentBuilder
    {
        const string Root = "Assets/ScriptableObjects/Robots";
        const string Units = "Assets/ScriptableObjects/Units/";
        const string WaveFolder = "Assets/ScriptableObjects/Battle/Waves";
        const string BattlePrefab = "Assets/Prefabs/Battle/Battle.prefab";

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        static StatModifier Mod(StatType s, float v) => new() { stat = s, op = ModifierOp.Flat, value = v };

        [MenuItem("RougeLike/Build Robot Content")]
        public static void BuildAll()
        {
            Folder("Assets/ScriptableObjects", "Robots");
            Folder(Root, "Chassis");
            Folder(Root, "Parts");

            // Chassis: light, medium and heavy.
            var scrapper = Chassis("Scrapper", "robot_chassis_scrapper", new Vector3(0.7f, 0.26f, 0.9f), 8f, 7, 70f, 0f, Hex("C46A2E"), Rarity.Common);
            var brawler = Chassis("Brawler", "robot_chassis_brawler", new Vector3(0.8f, 0.3f, 1f), 12f, 10, 100f, 0f, Hex("6E8A4A"), Rarity.Common);
            var juggernaut = Chassis("Juggernaut", "robot_chassis_juggernaut", new Vector3(0.95f, 0.36f, 1.15f), 18f, 13, 150f, 2f, Hex("3F6E94"), Rarity.Rare);

            // Parts.
            var smallWheel = Part("GoKartWheel", "robot_wheel_small", "Go-Kart Wheel", RobotPartType.Wheel, SlotType.Leg, 1, 0.5f, Rarity.Common, p =>
            {
                p.wheelRadius = 0.18f; p.grip = 1.2f; p.motor = 55f; p.stride = 5f;
            });
            var bigWheel = Part("MonsterWheel", "robot_wheel_big", "Monster Wheel", RobotPartType.Wheel, SlotType.Leg, 2, 1.2f, Rarity.Uncommon, p =>
            {
                p.wheelRadius = 0.25f; p.grip = 1.6f; p.motor = 85f; p.stride = 4f;
            });
            var wedge = Part("Wedge", "robot_wedge", "Wedge", RobotPartType.Wedge, SlotType.Head, 1, 1.5f, Rarity.Common, p =>
            {
                p.modifiers.Add(Mod(StatType.Defense, 1f));
                p.tags.Add("wedge");
            });
            var flipper = Part("Flipper", "robot_flipper", "Flipper", RobotPartType.Flipper, SlotType.Head, 3, 2f, Rarity.Uncommon, p =>
            {
                p.damage = 5f; p.force = 7f; p.cooldown = 2.5f; p.color = Hex("E0B830");
                p.tags.Add("melee"); p.tags.Add("flipper");
            });
            var spinner = Part("BarSpinner", "robot_spinner", "Bar Spinner", RobotPartType.Spinner, SlotType.Head, 4, 3f, Rarity.Rare, p =>
            {
                p.damage = 30f; p.force = 6f; p.cooldown = 2f;
                p.tags.Add("melee"); p.tags.Add("spinner");
            });
            var drum = Part("DrumSpinner", "robot_drum", "Drum Spinner", RobotPartType.Drum, SlotType.Head, 4, 3f, Rarity.Rare, p =>
            {
                p.damage = 22f; p.force = 7f; p.cooldown = 1.5f; p.color = Hex("8C8577");
                p.tags.Add("melee"); p.tags.Add("spinner");
            });
            var hammer = Part("AxeHammer", "robot_hammer", "Axe Hammer", RobotPartType.Hammer, SlotType.Back, 3, 2.5f, Rarity.Uncommon, p =>
            {
                p.damage = 25f; p.force = 2f; p.cooldown = 1.6f; p.color = Hex("A33A2A");
                p.tags.Add("melee"); p.tags.Add("hammer");
            });
            var flame = Part("Flamethrower", "robot_flamethrower", "Flamethrower", RobotPartType.Flamethrower, SlotType.Head, 2, 1.5f, Rarity.Uncommon, p =>
            {
                p.damage = 12f; p.color = Hex("A33A2A");
                p.tags.Add("melee"); p.tags.Add("fire");
            });
            var armor = Part("ArmourPlate", "robot_armour", "Armour Plate", RobotPartType.Armor, SlotType.Arm, 1, 2f, Rarity.Common, p =>
            {
                p.modifiers.Add(Mod(StatType.MaxHealth, 25f));
                p.modifiers.Add(Mod(StatType.Defense, 2f));
                p.tags.Add("armored");
            });
            var srimech = Part("Srimech", "robot_srimech", "Self-Righter", RobotPartType.SelfRighter, SlotType.Back, 2, 1f, Rarity.Uncommon, p =>
            {
                p.force = 5f; p.cooldown = 3f;
            });

            var chassis = new List<ChassisDefinition> { scrapper, brawler, juggernaut };
            var parts = new List<RobotPartDefinition> { smallWheel, bigWheel, wedge, flipper, spinner, drum, hammer, flame, armor, srimech };

            // Content database: add (or keep) every robot definition.
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Units + "ContentDatabase.asset");
            foreach (var c in chassis) if (!db.bodies.Contains(c)) db.bodies.Add(c);
            foreach (var p in parts) if (!db.parts.Contains(p)) db.parts.Add(p);
            db.Rebuild();
            EditorUtility.SetDirty(db);

            // New runs start with two chassis and enough to build two simple robots.
            var starter = AssetDatabase.LoadAssetAtPath<StarterSet>(Units + "StarterSet.asset");
            starter.bodies = new List<BodyDefinition> { scrapper, brawler };
            starter.parts = new List<StarterPart>
            {
                new() { part = smallWheel, count = 8 },
                new() { part = wedge, count = 1 },
                new() { part = flipper, count = 1 },
                new() { part = hammer, count = 1 },
                new() { part = armor, count = 1 },
            };
            EditorUtility.SetDirty(starter);

            // Rewards: robot bodies and parts replace the creature ones; buffs stay.
            var rewards = AssetDatabase.LoadAssetAtPath<RewardTable>(Units + "RewardTable.asset");
            rewards.entries.RemoveAll(e => e == null || e.kind != RewardKind.Buff);
            rewards.entries.Add(new RewardEntry { kind = RewardKind.Body, content = juggernaut, weight = 1 });
            rewards.entries.Add(new RewardEntry { kind = RewardKind.Body, content = brawler, weight = 1 });
            foreach (var (p, w) in new[] { (smallWheel, 3), (bigWheel, 2), (wedge, 3), (flipper, 2), (spinner, 1), (drum, 1), (hammer, 2), (flame, 2), (armor, 3), (srimech, 2) })
                rewards.entries.Add(new RewardEntry { kind = RewardKind.Part, content = p, weight = w });
            EditorUtility.SetDirty(rewards);

            // Enemy waves, toughening up.
            string[] wheels4 = { "wheel_front_left", "wheel_front_right", "wheel_back_left", "wheel_back_right" };
            UnitBlueprint Bot(string name, ChassisDefinition c, RobotPartDefinition wheel, params (string slot, RobotPartDefinition part)[] extra)
            {
                var bp = new UnitBlueprint { name = name, bodyId = c.id };
                foreach (var s in wheels4) bp.SetPart(s, wheel.id);
                foreach (var (slot, part) in extra) bp.SetPart(slot, part.id);
                return bp;
            }
            var waves = new List<EnemyWave>
            {
                Wave("RobotWave01_ScrapHeap", "Scrap Heap",
                    (Bot("Rust Bucket", scrapper, smallWheel, ("front", wedge)), new Vector2(-2f, 3f)),
                    (Bot("Rust Bucket", scrapper, smallWheel, ("front", wedge)), new Vector2(2f, 3f))),
                Wave("RobotWave02_FlipSquad", "Flip Squad",
                    (Bot("Tosser", brawler, smallWheel, ("front", flipper)), new Vector2(-2.5f, 3f)),
                    (Bot("Rust Bucket", scrapper, smallWheel, ("front", wedge)), new Vector2(0f, 3.5f)),
                    (Bot("Tosser", brawler, smallWheel, ("front", flipper)), new Vector2(2.5f, 3f))),
                Wave("RobotWave03_Spinners", "Spinners",
                    (Bot("Whirlwind", brawler, smallWheel, ("front", spinner), ("side_left", armor), ("side_right", armor)), new Vector2(-2f, 3f)),
                    (Bot("Grinder", scrapper, smallWheel, ("front", drum)), new Vector2(2f, 3f)),
                    (Bot("Torch", scrapper, smallWheel, ("front", flame)), new Vector2(0f, 4f))),
                Wave("RobotWave04_Juggernaut", "The Juggernaut",
                    (Bot("Juggernaut", juggernaut, bigWheel, ("front", drum), ("top", hammer), ("side_left", armor), ("side_right", armor), ("rear", srimech)), new Vector2(0f, 3.5f)),
                    (Bot("Rust Bucket", scrapper, smallWheel, ("front", wedge)), new Vector2(-3f, 3f)),
                    (Bot("Rust Bucket", scrapper, smallWheel, ("front", wedge)), new Vector2(3f, 3f))),
            };
            var root = PrefabUtility.LoadPrefabContents(BattlePrefab);
            try
            {
                var bm = root.GetComponentInChildren<BattleManager>(true);
                var so = new SerializedObject(bm);
                var list = so.FindProperty("waves");
                list.arraySize = waves.Count;
                for (int i = 0; i < waves.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BattlePrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Icons: a chassis on wheels, and each part on its own.
            foreach (var c in chassis)
            {
                var holder = new GameObject("Icon");
                var bp = Bot(c.displayName, c, smallWheel);
                RobotAssembler.SpawnVisual(bp, c, db, holder.transform);
                IconRenderer.Assign(c, IconRenderer.Photograph(holder, 155f), "Bodies");
                Object.DestroyImmediate(holder);
            }
            foreach (var p in parts)
            {
                var holder = new GameObject("Icon");
                RobotVisuals.Part(p, holder.transform);
                IconRenderer.Assign(p, IconRenderer.Photograph(holder, 120f), "Parts");
                Object.DestroyImmediate(holder);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Built robot content: {chassis.Count} chassis, {parts.Count} parts, {waves.Count} waves. New runs start with robots.");
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        static T Load<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static ChassisDefinition Chassis(string file, string id, Vector3 size, float mass, int energy, float health, float defense, Color color, Rarity rarity)
        {
            var c = Load<ChassisDefinition>($"{Root}/Chassis/{file}.asset");
            c.id = id;
            c.displayName = file;
            c.rarity = rarity;
            c.prefab = null;
            c.size = size;
            c.mass = mass;
            c.color = color;
            c.energy = energy;
            c.moveScale = 1f;
            c.builtInLegs = 0;
            c.baseStats = new List<StatValue>
            {
                new() { stat = StatType.MaxHealth, value = health },
                new() { stat = StatType.Defense, value = defense },
            };
            // Mount points: front, rear, top, both sides, and four wheel wells outside the hull.
            float w = size.x * 0.5f, h = size.y * 0.5f, l = size.z * 0.5f;
            float wheelX = w + 0.1f, wheelY = -size.y * 0.15f, wheelZ = size.z * 0.3f;
            c.slots = new List<SlotDefinition>
            {
                new() { slotId = "front", type = SlotType.Head, localPosition = new Vector3(0f, -size.y * 0.3f, l) },
                new() { slotId = "top", type = SlotType.Back, localPosition = new Vector3(0f, h, 0f) },
                new() { slotId = "rear", type = SlotType.Tail, localPosition = new Vector3(0f, 0f, -l) },
                new() { slotId = "side_left", type = SlotType.Arm, localPosition = new Vector3(-w, 0f, 0f) },
                new() { slotId = "side_right", type = SlotType.Arm, localPosition = new Vector3(w, 0f, 0f) },
                new() { slotId = "wheel_front_left", type = SlotType.Leg, localPosition = new Vector3(-wheelX, wheelY, wheelZ) },
                new() { slotId = "wheel_front_right", type = SlotType.Leg, localPosition = new Vector3(wheelX, wheelY, wheelZ) },
                new() { slotId = "wheel_back_left", type = SlotType.Leg, localPosition = new Vector3(-wheelX, wheelY, -wheelZ) },
                new() { slotId = "wheel_back_right", type = SlotType.Leg, localPosition = new Vector3(wheelX, wheelY, -wheelZ) },
            };
            EditorUtility.SetDirty(c);
            return c;
        }

        static RobotPartDefinition Part(string file, string id, string name, RobotPartType type, SlotType kind, int energy, float mass,
                                        Rarity rarity, System.Action<RobotPartDefinition> fill)
        {
            var p = Load<RobotPartDefinition>($"{Root}/Parts/{file}.asset");
            p.id = id;
            p.displayName = name;
            p.type = type;
            p.kind = kind;
            p.energyCost = energy;
            p.mass = mass;
            p.rarity = rarity;
            p.prefab = null;
            p.stride = 0f;
            p.color = new Color(0f, 0f, 0f, 0f);
            p.modifiers = new List<StatModifier>();
            p.tags = new List<string>();
            p.abilities = new List<AbilityDefinition>();
            fill(p);
            EditorUtility.SetDirty(p);
            return p;
        }

        static EnemyWave Wave(string file, string name, params (UnitBlueprint bp, Vector2 pos)[] units)
        {
            var w = Load<EnemyWave>($"{WaveFolder}/{file}.asset");
            w.displayName = name;
            w.units = new List<EnemyWaveEntry>();
            foreach (var (bp, pos) in units) w.units.Add(new EnemyWaveEntry { blueprint = bp, position = pos });
            w.buffs = new List<BuffDefinition>();
            w.arena = null;
            EditorUtility.SetDirty(w);
            return w;
        }
    }
}
