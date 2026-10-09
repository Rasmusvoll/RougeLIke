using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Builds robot looks out of primitives: the chassis hull and each part, already wired to its
    /// behaviour (a wheel gets a RobotWheel, a weapon its RobotWeapon). Nothing here has colliders;
    /// weapons add theirs when armed for battle, so the builder's slot clicks aren't blocked.
    /// Parts are built facing +Z (out of their mount), with their mount point at the origin.
    /// </summary>
    public static class RobotVisuals
    {
        public const float WheelTravel = 0.16f;

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        static readonly Color Steel = Hex("9A9488");
        static readonly Color DarkSteel = Hex("5C574F");
        static readonly Color Rubber = Hex("2B1F1A");
        static readonly Color Brass = Hex("C9A04A");
        static readonly Color Hazard = Hex("E0B830");
        static readonly Color Flame = Hex("F08A24");

        public static Transform Chassis(ChassisDefinition def, Transform parent)
        {
            var root = new GameObject("Chassis").transform;
            root.SetParent(parent, false);
            var s = def.size;
            Box("Hull", root, def.color, Vector3.zero, s);
            Box("Lid", root, Color.Lerp(def.color, Color.white, 0.25f), new Vector3(0f, s.y * 0.5f + 0.02f, -s.z * 0.08f), new Vector3(s.x * 0.7f, 0.04f, s.z * 0.6f));
            Box("Visor", root, Rubber, new Vector3(0f, s.y * 0.5f + 0.03f, s.z * 0.32f), new Vector3(s.x * 0.4f, 0.06f, 0.08f));
            // Hazard stripe round the nose, and bolts on the corners.
            Box("Stripe", root, Hazard, new Vector3(0f, s.y * 0.15f, s.z * 0.5f + 0.005f), new Vector3(s.x * 0.9f, s.y * 0.18f, 0.02f));
            foreach (var x in new[] { -1f, 1f })
            foreach (var z in new[] { -1f, 1f })
                Box("Bolt", root, DarkSteel, new Vector3(x * s.x * 0.42f, s.y * 0.5f + 0.01f, z * s.z * 0.44f), Vector3.one * 0.05f);
            return root;
        }

        /// <summary>Builds a part, wired to its behaviour, under a mount transform.</summary>
        public static GameObject Part(RobotPartDefinition def, Transform mount)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetParent(mount, false);
            var t = go.transform;
            var paint = def.color.a > 0f ? def.color : Steel;
            switch (def.type)
            {
                case RobotPartType.Wheel: Wheel(def, t); break;
                case RobotPartType.Wedge: Wedge(def, t, paint); break;
                case RobotPartType.Flipper: Flipper(def, t, paint); break;
                case RobotPartType.Spinner: Spinner(def, t, paint); break;
                case RobotPartType.Drum: Drum(def, t, paint); break;
                case RobotPartType.Hammer: Hammer(def, t, paint); break;
                case RobotPartType.Flamethrower: Flamethrower(def, t, paint); break;
                case RobotPartType.Armor: Armor(t, paint); break;
                case RobotPartType.SelfRighter: SelfRighter(def, t, paint); break;
            }
            return go;
        }

        static void Wheel(RobotPartDefinition def, Transform t)
        {
            var wheel = t.gameObject.AddComponent<RobotWheel>();
            wheel.radius = def.wheelRadius;
            wheel.travel = WheelTravel;
            wheel.grip = def.grip;
            wheel.traction = def.grip * 0.9f;
            // The visual pivots on the axle (local X); the cylinder inside lies along it.
            var spinner = new GameObject("Visual").transform;
            spinner.SetParent(t, false);
            float r = def.wheelRadius, width = 0.06f + r * 0.25f;
            var tyre = Prim(PrimitiveType.Cylinder, "Tyre", spinner, Rubber);
            tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tyre.transform.localScale = new Vector3(r * 2f, width * 0.5f, r * 2f);
            var hub = Prim(PrimitiveType.Cylinder, "Hub", spinner, Brass);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hub.transform.localScale = new Vector3(r * 0.9f, width * 0.55f, r * 0.9f);
            // A spoke so the spin shows.
            Box("Spoke", spinner, DarkSteel, Vector3.zero, new Vector3(width * 1.15f, r * 1.6f, r * 0.25f));
            wheel.visual = spinner;
        }

        static void Wedge(RobotPartDefinition def, Transform t, Color paint)
        {
            var plate = new GameObject("Plate").transform;
            plate.SetParent(t, false);
            plate.localPosition = new Vector3(0f, -0.13f, 0.2f);
            plate.localRotation = Quaternion.Euler(24f, 0f, 0f);
            Box("Steel", plate, paint, Vector3.zero, new Vector3(0.84f, 0.04f, 0.46f));
            Box("Edge", plate, Hazard, new Vector3(0f, 0.01f, 0.22f), new Vector3(0.84f, 0.045f, 0.04f));
            t.gameObject.AddComponent<Wedge>().Setup(def, plate, new Vector3(0.84f, 0.04f, 0.46f));
        }

        static void Flipper(RobotPartDefinition def, Transform t, Color paint)
        {
            Box("Base", t, DarkSteel, new Vector3(0f, -0.08f, 0.06f), new Vector3(0.6f, 0.08f, 0.14f));
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(0f, -0.08f, 0.04f);
            var plate = Box("Plate", hinge, paint, new Vector3(0f, 0f, 0.24f), new Vector3(0.72f, 0.04f, 0.46f));
            plate.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            Box("Ram", hinge, Hazard, new Vector3(0f, -0.02f, 0.47f), new Vector3(0.72f, 0.03f, 0.04f));
            Box("Piston", t, Brass, new Vector3(0f, -0.07f, 0.18f), new Vector3(0.06f, 0.06f, 0.2f));
            t.gameObject.AddComponent<Flipper>().Setup(def, hinge);
        }

        static void Spinner(RobotPartDefinition def, Transform t, Color paint)
        {
            Box("Boom", t, DarkSteel, new Vector3(0f, 0f, 0.12f), new Vector3(0.14f, 0.08f, 0.26f));
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0f, 0f, 0.3f);
            var hub = Prim(PrimitiveType.Cylinder, "Hub", rotor, Brass);
            hub.transform.localScale = new Vector3(0.16f, 0.05f, 0.16f);
            Box("Bar", rotor, paint, Vector3.zero, new Vector3(1f, 0.05f, 0.12f));
            Box("Tooth", rotor, Hazard, new Vector3(0.48f, 0f, 0.07f), new Vector3(0.06f, 0.06f, 0.06f));
            Box("Tooth", rotor, Hazard, new Vector3(-0.48f, 0f, -0.07f), new Vector3(0.06f, 0.06f, 0.06f));
            t.gameObject.AddComponent<Spinner>().Setup(def, rotor, Vector3.up);
        }

        static void Drum(RobotPartDefinition def, Transform t, Color paint)
        {
            foreach (var x in new[] { -0.33f, 0.33f })
                Box("Fork", t, DarkSteel, new Vector3(x, -0.04f, 0.12f), new Vector3(0.05f, 0.1f, 0.3f));
            var rotor = new GameObject("Drum").transform;
            rotor.SetParent(t, false);
            rotor.localPosition = new Vector3(0f, -0.08f, 0.26f);
            var drum = Prim(PrimitiveType.Cylinder, "Barrel", rotor, paint);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            drum.transform.localScale = new Vector3(0.26f, 0.3f, 0.26f);
            for (int i = 0; i < 3; i++)
            {
                var a = i * 120f;
                var tooth = Box("Tooth", rotor, Hazard, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, 0.14f, 0f), new Vector3(0.5f, 0.05f, 0.05f));
                tooth.transform.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
            t.gameObject.AddComponent<Spinner>().Setup(def, rotor, Vector3.right);
        }

        static void Hammer(RobotPartDefinition def, Transform t, Color paint)
        {
            Box("Mount", t, DarkSteel, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.08f, 0.18f));
            var arm = new GameObject("Arm").transform;
            arm.SetParent(t, false);
            arm.localPosition = new Vector3(0f, 0.1f, 0f);
            Box("Shaft", arm, Steel, new Vector3(0f, 0f, 0.35f), new Vector3(0.06f, 0.06f, 0.7f));
            Box("Head", arm, paint, new Vector3(0f, -0.06f, 0.7f), new Vector3(0.26f, 0.2f, 0.14f));
            Box("Spike", arm, Hazard, new Vector3(0f, -0.18f, 0.7f), new Vector3(0.06f, 0.08f, 0.06f));
            t.gameObject.AddComponent<Hammer>().Setup(def, arm);
        }

        static void Flamethrower(RobotPartDefinition def, Transform t, Color paint)
        {
            Box("Tank", t, paint, new Vector3(0f, 0.02f, 0.05f), new Vector3(0.16f, 0.16f, 0.14f));
            var nozzle = Prim(PrimitiveType.Cylinder, "Nozzle", t, Brass);
            nozzle.transform.localPosition = new Vector3(0f, 0.04f, 0.2f);
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            nozzle.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
            var flames = new GameObject("Flames").transform;
            flames.SetParent(t, false);
            flames.localPosition = new Vector3(0f, 0.04f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                var f = Prim(PrimitiveType.Sphere, "Flame", flames, BattleVisuals.Unlit(Color.Lerp(Flame, Hazard, i / 3f)));
                f.transform.localPosition = new Vector3(0f, 0f, 0.12f + i * 0.17f);
                f.transform.localScale = Vector3.one * (0.12f + i * 0.06f);
            }
            flames.gameObject.SetActive(false);
            t.gameObject.AddComponent<Flamethrower>().Setup(def, flames);
        }

        static void Armor(Transform t, Color paint)
        {
            Box("Plate", t, paint, new Vector3(0f, 0f, 0.035f), new Vector3(0.72f, 0.26f, 0.06f));
            foreach (var x in new[] { -0.3f, 0f, 0.3f })
                Box("Rivet", t, DarkSteel, new Vector3(x, 0.08f, 0.07f), Vector3.one * 0.035f);
            t.gameObject.AddComponent<ArmorPlate>().Setup(new Vector3(0f, 0f, 0.035f), new Vector3(0.72f, 0.26f, 0.06f));
        }

        static void SelfRighter(RobotPartDefinition def, Transform t, Color paint)
        {
            Box("Base", t, DarkSteel, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.06f, 0.14f));
            var arm = new GameObject("Arm").transform;
            arm.SetParent(t, false);
            arm.localPosition = new Vector3(0f, 0.06f, 0f);
            Box("Bar", arm, paint, new Vector3(0f, 0f, -0.25f), new Vector3(0.1f, 0.04f, 0.5f));
            Box("Foot", arm, Hazard, new Vector3(0f, 0f, -0.5f), new Vector3(0.3f, 0.05f, 0.06f));
            t.gameObject.AddComponent<SelfRighter>().Setup(def, arm);
        }

        // Primitives (safe in edit mode too, for icon rendering)

        public static GameObject Box(string name, Transform parent, Color color, Vector3 pos, Vector3 size)
        {
            var go = Prim(PrimitiveType.Cube, name, parent, color);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Color color) =>
            Prim(type, name, parent, BattleVisuals.Lit(color));

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            col.enabled = false; // Destroy only happens at the end of the frame
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
