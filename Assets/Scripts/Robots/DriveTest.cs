using System.Collections.Generic;
using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Phase 1 test bed for how wheeled robots feel: drive one robot with the keyboard on a real arena
    /// with its walls removed, shove a few dummy robots about, push them off the edge or into pits.
    /// Sliders on screen tune the drive live. Build the scene with RougeLike > Open Drive Test.
    /// </summary>
    public class DriveTest : MonoBehaviour
    {
        [SerializeField] List<ArenaDefinition> arenas = new();
        [SerializeField] Vector2 boardHalfSize = new(9.2f, 6.6f);
        [SerializeField] int dummyCount = 3;
        [Tooltip("Below this height a robot has fallen off the arena.")]
        [SerializeField] float fallHeight = -4f;

        static readonly Color PlayerColor = BattleVisuals.Palette.MarquiseOrange;
        static readonly Color DummyColor = BattleVisuals.Palette.EyrieBlue;

        Transform field, robotRoot;
        Arena arena;
        int arenaIndex;
        RobotDrive player;
        readonly List<RobotDrive> dummies = new();
        readonly Dictionary<RobotDrive, float> fallen = new();
        bool dummiesChase, chaseCam, showTuning = true;
        Camera cam;
        Vector3 camHome;
        Quaternion camHomeRot;
        string lastEvent = "";
        float lastEventTime = -10f;

        // Live tuning, applied to every robot each frame.
        float power = 14f, maxSpeed = 6f, turnRate = 200f, grip = 1.2f, springHz = 3f, damping = 0.45f, mass = 10f;

        void Start()
        {
            cam = Camera.main;
            if (cam != null)
            {
                camHome = cam.transform.position;
                camHomeRot = cam.transform.rotation;
            }
            robotRoot = new GameObject("Robots").transform;
            robotRoot.SetParent(transform, false);
            LoadArena(0);
        }

        void LoadArena(int index)
        {
            if (field != null) Destroy(field.gameObject);
            foreach (Transform t in robotRoot) Destroy(t.gameObject);
            dummies.Clear();
            fallen.Clear();

            field = new GameObject("Field").transform;
            field.SetParent(transform, false);
            arenaIndex = arenas.Count > 0 ? (index % arenas.Count + arenas.Count) % arenas.Count : 0;
            if (arenas.Count > 0 && arenas[arenaIndex] != null)
                arena = Arena.Build(arenas[arenaIndex], field, boardHalfSize);
            else
            {
                arena = null;
                var ground = BattleVisuals.Primitive(PrimitiveType.Cube, "Ground", field, BattleVisuals.Lit(BattleVisuals.Palette.Parchment), keepCollider: true);
                ground.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                ground.transform.localScale = new Vector3(boardHalfSize.x * 2f, 0.5f, boardHalfSize.y * 2f);
            }
            // No walls on purpose: the edge of the board is a drop.

            player = TestRobot.Create("Player Robot", PlayerColor, SpawnPoint(0f, -3.5f), 0f, robotRoot);
            for (int i = 0; i < dummyCount; i++)
            {
                float x = dummyCount == 1 ? 0f : Mathf.Lerp(-4.5f, 4.5f, i / (float)(dummyCount - 1));
                dummies.Add(TestRobot.Create($"Dummy {i + 1}", DummyColor, SpawnPoint(x, 2.5f), 180f, robotRoot));
            }
            ApplyTuning();
        }

        /// <summary>A spot on the ground near (x, z), nudged off any pit.</summary>
        Vector3 SpawnPoint(float x, float z)
        {
            if (arena != null)
                for (int tries = 0; tries < 12 && arena.InHole(x, z, 1.4f); tries++) x += 1f;
            float y = arena != null ? arena.HeightAt(x, z) : 0f;
            return new Vector3(x, y + 0.6f, z);
        }

        void Respawn(RobotDrive r, float x, float z, float yaw)
        {
            r.Body.linearVelocity = Vector3.zero;
            r.Body.angularVelocity = Vector3.zero;
            r.Body.position = SpawnPoint(x, z);
            r.Body.rotation = Quaternion.Euler(0f, yaw, 0f);
            r.transform.SetPositionAndRotation(r.Body.position, r.Body.rotation);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) LoadArena(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) LoadArena(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) LoadArena(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) LoadArena(3);
            if (Input.GetKeyDown(KeyCode.N)) LoadArena(arenaIndex + 1);
            if (Input.GetKeyDown(KeyCode.R)) Respawn(player, 0f, -3.5f, 0f);
            if (Input.GetKeyDown(KeyCode.G)) dummiesChase = !dummiesChase;
            if (Input.GetKeyDown(KeyCode.C)) chaseCam = !chaseCam;
            if (Input.GetKeyDown(KeyCode.Tab)) showTuning = !showTuning;
            if (Input.GetKeyDown(KeyCode.F)) Flip(player);

            if (player != null)
            {
                player.Throttle = Input.GetAxisRaw("Vertical");
                player.Steer = Input.GetAxisRaw("Horizontal");
            }
            foreach (var d in dummies) DriveDummy(d);
            ApplyTuning();
            CheckFalls();
        }

        /// <summary>A taste of the battle AI: dummies line up on the player and ram it.</summary>
        void DriveDummy(RobotDrive d)
        {
            if (!dummiesChase || player == null)
            {
                d.Throttle = d.Steer = 0f;
                return;
            }
            var to = player.transform.position - d.transform.position;
            to.y = 0f;
            var fwd = Vector3.ProjectOnPlane(d.transform.forward, Vector3.up);
            float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
            d.Steer = Mathf.Clamp(angle / 40f, -1f, 1f);
            d.Throttle = Mathf.Abs(angle) < 70f ? 1f : 0.2f;
        }

        /// <summary>Test helper: hop and roll the robot, to try flipping and landing.</summary>
        static void Flip(RobotDrive r)
        {
            if (r == null) return;
            r.Body.AddForce(Vector3.up * 6f, ForceMode.VelocityChange);
            r.Body.AddTorque(r.transform.forward * 12f, ForceMode.VelocityChange);
        }

        void ApplyTuning()
        {
            void Apply(RobotDrive r)
            {
                if (r == null) return;
                r.power = power;
                r.maxSpeed = maxSpeed;
                r.turnRate = turnRate;
                r.springFrequency = springHz;
                r.damping = damping;
                r.Body.mass = mass;
                foreach (var w in r.Wheels)
                {
                    w.grip = grip;
                    w.traction = grip * 0.9f;
                }
            }
            Apply(player);
            foreach (var d in dummies) Apply(d);
        }

        void CheckFalls()
        {
            void Check(RobotDrive r, float x, float z, float yaw, string label)
            {
                if (r == null) return;
                if (fallen.TryGetValue(r, out float since))
                {
                    if (Time.time - since > 1.5f)
                    {
                        fallen.Remove(r);
                        Respawn(r, x, z, yaw);
                    }
                    return;
                }
                if (r.transform.position.y < fallHeight)
                {
                    fallen[r] = Time.time;
                    ShowEvent(label + " fell off!");
                }
            }
            Check(player, 0f, -3.5f, 0f, "You");
            for (int i = 0; i < dummies.Count; i++)
                Check(dummies[i], Mathf.Lerp(-4.5f, 4.5f, dummies.Count == 1 ? 0.5f : i / (float)(dummies.Count - 1)), 2.5f, 180f, dummies[i].name);
        }

        void ShowEvent(string text)
        {
            lastEvent = text;
            lastEventTime = Time.time;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (chaseCam && player != null)
            {
                var fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
                if (fwd == Vector3.zero) fwd = Vector3.forward;
                var want = player.transform.position - fwd * 4.5f + Vector3.up * 3f;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-5f * Time.deltaTime));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                    Quaternion.LookRotation(player.transform.position + Vector3.up * 0.3f - cam.transform.position),
                    1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            else
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, camHome, 1f - Mathf.Exp(-4f * Time.deltaTime));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, camHomeRot, 1f - Mathf.Exp(-4f * Time.deltaTime));
            }
        }

        void OnGUI()
        {
            var box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
            string arenaName = arena != null ? arena.Definition.displayName : "Flat test board";
            string status = player == null ? "" :
                fallen.ContainsKey(player) ? "Fell off" :
                player.Stuck ? "Stuck on its back (F to flip, R to reset)" :
                $"{Mathf.Abs(player.Speed):0.0} m/s, {player.GroundedWheels}/{player.Wheels.Count} wheels down";
            GUI.Box(new Rect(10, 10, 330, 150),
                $"DRIVE TEST: {arenaName}\n{status}\n\n" +
                "WASD / arrows  drive (skid steer)\n" +
                "R reset   F flip   G dummies ram you: " + (dummiesChase ? "on" : "off") + "\n" +
                "1-4 / N arena   C chase camera   Tab tuning", box);

            if (Time.time - lastEventTime < 2f)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(0, 60, Screen.width, 50), lastEvent, big);
            }

            if (!showTuning) return;
            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 330), GUI.skin.box);
            GUILayout.Label("Tuning (all robots)");
            power = Slider("Power (N/kg)", power, 4f, 40f);
            maxSpeed = Slider("Top speed (m/s)", maxSpeed, 2f, 14f);
            turnRate = Slider("Turn rate (deg/s)", turnRate, 60f, 500f);
            grip = Slider("Tyre grip", grip, 0.2f, 2.5f);
            springHz = Slider("Suspension (Hz)", springHz, 1f, 8f);
            damping = Slider("Damping", damping, 0.05f, 1.2f);
            mass = Slider("Mass (kg)", mass, 2f, 40f);
            if (GUILayout.Button("Reset tuning"))
            {
                power = 14f; maxSpeed = 6f; turnRate = 200f; grip = 1.2f; springHz = 3f; damping = 0.45f; mass = 10f;
            }
            GUILayout.EndArea();
        }

        static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {value:0.##}", GUILayout.Width(150));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
