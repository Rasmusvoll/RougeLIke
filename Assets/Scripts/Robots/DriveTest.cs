using System.Collections.Generic;
using RougeLike.Battle;
using UnityEngine;

namespace RougeLike.Robots
{
    /// <summary>
    /// Phase 1 test bed for how wheeled robots feel: two teams of robots drive and fight on their own
    /// on a real arena with its walls removed, shoving each other off the edge and into pits. Sliders
    /// on screen tune the drive live. Build the scene with RougeLike > Open Drive Test.
    /// </summary>
    public class DriveTest : MonoBehaviour
    {
        [SerializeField] List<ArenaDefinition> arenas = new();
        [SerializeField] Vector2 boardHalfSize = new(9.2f, 6.6f);
        [SerializeField] int teamSize = 4;
        [Tooltip("Below this height a robot has fallen off the arena.")]
        [SerializeField] float fallHeight = -4f;
        [Tooltip("A robot stuck on its back this long is out.")]
        [SerializeField] float stuckOutTime = 4f;

        static readonly Color[] TeamColors = { BattleVisuals.Palette.MarquiseOrange, BattleVisuals.Palette.EyrieBlue };
        static readonly string[] TeamNames = { "Orange", "Blue" };

        class Bot
        {
            public RobotDrive drive;
            public RobotBrain brain;
            public int team;
            public bool isOut;
            public float stuckTime;
        }

        Transform field, robotRoot;
        Arena arena;
        int arenaIndex;
        readonly List<Bot> bots = new();
        readonly List<RobotBrain> brains = new();
        bool followCam, showTuning = true, slowMo;
        Camera cam;
        Vector3 camHome;
        Quaternion camHomeRot;
        string banner = "";
        float bannerTime = -10f, roundOverAt = -1f;
        int[] wins = new int[2];

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
            if (field != null)
            {
                field.gameObject.SetActive(false); // gone now, not at the end of the frame
                Destroy(field.gameObject);
            }
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
            StartRound();
        }

        void StartRound()
        {
            foreach (Transform t in robotRoot)
            {
                // Off at once, so last round's robots can't collide with the new ones on the same spots.
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
            bots.Clear();
            brains.Clear();
            roundOverAt = -1f;
            for (int team = 0; team < 2; team++)
            for (int i = 0; i < teamSize; i++)
            {
                float x = teamSize == 1 ? 0f : Mathf.Lerp(-5f, 5f, i / (float)(teamSize - 1));
                float z = team == 0 ? -3.6f : 3.6f;
                var drive = TestRobot.Create($"{TeamNames[team]} {i + 1}", TeamColors[team], SpawnPoint(x, z), team == 0 ? 0f : 180f, robotRoot);
                var brain = drive.gameObject.AddComponent<RobotBrain>();
                brain.Team = team;
                brain.Others = brains;
                // A little variety so they don't move as one block.
                brain.lineUpDistance = Random.Range(1.2f, 2.2f);
                brain.caution = Random.Range(0.25f, 0.45f);
                brains.Add(brain);
                bots.Add(new Bot { drive = drive, brain = brain, team = team });
            }
            ApplyTuning();
            ShowBanner("Fight!");
        }

        /// <summary>A spot on the ground near (x, z), nudged off any pit.</summary>
        Vector3 SpawnPoint(float x, float z)
        {
            if (arena != null)
                for (int tries = 0; tries < 12 && arena.InHole(x, z, 1.4f); tries++) x += x > 0f ? -1f : 1f;
            float y = arena != null ? arena.HeightAt(x, z) : 0f;
            return new Vector3(x, y + 0.6f, z);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) LoadArena(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) LoadArena(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) LoadArena(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) LoadArena(3);
            if (Input.GetKeyDown(KeyCode.N)) LoadArena(arenaIndex + 1);
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space)) StartRound();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) { teamSize = Mathf.Min(10, teamSize + 1); StartRound(); }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) { teamSize = Mathf.Max(1, teamSize - 1); StartRound(); }
            if (Input.GetKeyDown(KeyCode.C)) followCam = !followCam;
            if (Input.GetKeyDown(KeyCode.T)) slowMo = !slowMo;
            if (Input.GetKeyDown(KeyCode.Tab)) showTuning = !showTuning;
            Time.timeScale = slowMo ? 0.35f : 1f;

            ApplyTuning();
            CheckKnockouts();
            if (roundOverAt > 0f && Time.time - roundOverAt > 5f) StartRound();
        }

        void CheckKnockouts()
        {
            foreach (var b in bots)
            {
                if (b.drive == null) continue;
                if (b.isOut)
                {
                    // Wreckage that's later shoved off the edge is hidden too, not left falling forever.
                    if (b.drive.transform.position.y < fallHeight) b.drive.gameObject.SetActive(false);
                    continue;
                }
                b.stuckTime = b.drive.Stuck ? b.stuckTime + Time.deltaTime : 0f;
                string why = b.drive.transform.position.y < fallHeight ? "fell off"
                           : b.stuckTime > stuckOutTime ? "is stuck on its back"
                           : null;
                if (why == null) continue;
                b.isOut = true;
                // Taken out of the fight; a fallen one is hidden, a flipped one stays as wreckage.
                b.brain.enabled = false;
                b.drive.Throttle = b.drive.Steer = 0f;
                if (b.drive.transform.position.y < fallHeight) b.drive.gameObject.SetActive(false);
                ShowBanner($"{b.drive.name} {why}!");
            }

            if (roundOverAt > 0f) return;
            int orange = Alive(0), blue = Alive(1);
            if (orange > 0 && blue > 0) return;
            roundOverAt = Time.time;
            if (orange == 0 && blue == 0) ShowBanner("Draw!");
            else
            {
                int winner = orange > 0 ? 0 : 1;
                wins[winner]++;
                ShowBanner($"{TeamNames[winner]} wins!");
            }
        }

        int Alive(int team)
        {
            int n = 0;
            foreach (var b in bots) if (b.team == team && !b.isOut) n++;
            return n;
        }

        void ApplyTuning()
        {
            foreach (var b in bots)
            {
                var r = b.drive;
                if (r == null) continue;
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
        }

        void ShowBanner(string text)
        {
            banner = text;
            bannerTime = Time.unscaledTime;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Transform follow = null;
            if (followCam)
                foreach (var b in bots)
                    if (!b.isOut && b.drive != null) { follow = b.drive.transform; break; }

            float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            if (follow != null)
            {
                var want = follow.position + new Vector3(0f, 5f, -4.5f);
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                    Quaternion.LookRotation(follow.position - want), k);
            }
            else
            {
                cam.transform.position = Vector3.Lerp(cam.transform.position, camHome, k);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, camHomeRot, k);
            }
        }

        void OnGUI()
        {
            var box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
            string arenaName = arena != null ? arena.Definition.displayName : "Flat test board";
            GUI.Box(new Rect(10, 10, 340, 150),
                $"ROBOT TEST: {arenaName}\n" +
                $"Orange {Alive(0)} left vs Blue {Alive(1)} left   (wins {wins[0]} - {wins[1]})\n\n" +
                "R / Space  new round     + / -  robots per team (" + teamSize + ")\n" +
                "1-4 / N  arena     C  follow camera     T  slow motion\n" +
                "Tab  tuning panel", box);

            if (Time.unscaledTime - bannerTime < 2.5f)
            {
                var big = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(0, 60, Screen.width, 50), banner, big);
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
