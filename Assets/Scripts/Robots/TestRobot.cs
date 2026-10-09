using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Builds a stand-in robot out of primitives for the drive test: a box chassis, a wedge on the
    /// nose and four wheels. Phase 2 replaces this with robots assembled from builder blueprints.
    /// </summary>
    public static class TestRobot
    {
        public const float WheelRadius = 0.2f;

        public static RobotDrive Create(string name, Color color, Vector3 position, float yaw, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var body = root.AddComponent<Rigidbody>();
            body.mass = 10f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var ink = BattleVisuals.Lit(BattleVisuals.Palette.Ink);
            var paint = BattleVisuals.Lit(color);
            var metal = BattleVisuals.Lit(BattleVisuals.Palette.Stone);

            // Chassis: one collider for the hull, the look made of a few boxes.
            var hull = root.AddComponent<BoxCollider>();
            hull.center = Vector3.zero;
            hull.size = new Vector3(0.8f, 0.3f, 1f);
            Box("Hull", root.transform, paint, Vector3.zero, new Vector3(0.8f, 0.3f, 1f));
            Box("Lid", root.transform, metal, new Vector3(0f, 0.17f, -0.08f), new Vector3(0.56f, 0.06f, 0.6f));
            Box("Eye", root.transform, ink, new Vector3(0f, 0.17f, 0.32f), new Vector3(0.3f, 0.07f, 0.1f));

            // Wedge: a ramp on the nose that scoops under whatever it hits.
            var wedge = new GameObject("Wedge");
            wedge.transform.SetParent(root.transform, false);
            wedge.transform.localPosition = new Vector3(0f, -0.22f, 0.6f);
            wedge.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            var wedgeCol = wedge.AddComponent<BoxCollider>();
            wedgeCol.size = new Vector3(0.86f, 0.05f, 0.4f);
            Box("Plate", wedge.transform, metal, Vector3.zero, new Vector3(0.86f, 0.05f, 0.4f));

            foreach (var (x, z) in new[] { (-0.48f, 0.32f), (0.48f, 0.32f), (-0.48f, -0.32f), (0.48f, -0.32f) })
                Wheel(root.transform, new Vector3(x, -0.05f, z), ink);

            // Low centre of mass: hard to roll, but a good wedge hit still flips it.
            body.centerOfMass = new Vector3(0f, -0.1f, 0f);
            return root.AddComponent<RobotDrive>();
        }

        static void Wheel(Transform root, Vector3 mount, Material mat)
        {
            var go = new GameObject(mount.x < 0f ? "Wheel L" : "Wheel R");
            go.transform.SetParent(root, false);
            go.transform.localPosition = mount;
            var wheel = go.AddComponent<RobotWheel>();
            wheel.radius = WheelRadius;

            // The visual pivots on the axle (local X); the cylinder inside lies along it.
            var spinner = new GameObject("Visual").transform;
            spinner.SetParent(go.transform, false);
            var tyre = BattleVisuals.Primitive(PrimitiveType.Cylinder, "Tyre", spinner, mat);
            tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tyre.transform.localScale = new Vector3(WheelRadius * 2f, 0.07f, WheelRadius * 2f);
            var hub = BattleVisuals.Primitive(PrimitiveType.Cube, "Hub", spinner, BattleVisuals.Lit(BattleVisuals.Palette.Parchment));
            hub.transform.localPosition = new Vector3(Mathf.Sign(mount.x) * 0.075f, 0f, 0f);
            hub.transform.localScale = new Vector3(0.02f, WheelRadius * 1.1f, 0.06f);
            wheel.visual = spinner;
        }

        static void Box(string name, Transform parent, Material mat, Vector3 pos, Vector3 size)
        {
            var go = BattleVisuals.Primitive(PrimitiveType.Cube, name, parent, mat);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
        }
    }
}
