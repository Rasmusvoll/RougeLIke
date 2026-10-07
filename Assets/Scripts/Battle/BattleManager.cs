using System;
using System.Collections.Generic;
using RougeLike.Units;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RougeLike.Battle
{
    public enum BattlePhase { Placement, Fighting, Victory, Defeat }

    /// <summary>
    /// Runs one auto-battle: lays out the field, spawns the enemy wave, lets the player place units on
    /// their half, then simulates until one side is wiped out. Units decide for themselves (BattleUnit);
    /// this keeps them apart and decides the winner.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [SerializeField] ContentDatabase database;
        [Tooltip("Used to start a run when this scene is played directly.")]
        [SerializeField] StarterSet starterSet;
        [Tooltip("One wave per battle in the run. After the last, the last wave repeats.")]
        [SerializeField] List<EnemyWave> waves = new();
        [SerializeField] string builderScene = "Builder";

        [Header("Field")]
        [SerializeField] Vector2 fieldSize = new(15f, 10f);
        [Tooltip("Half-width of the strip in the middle where nobody can be placed.")]
        [SerializeField] float noMansLand = 1.2f;
        [SerializeField] Color groundColor = new(0.32f, 0.36f, 0.3f);
        [SerializeField] Color playerColor = new(0.3f, 0.65f, 1f);
        [SerializeField] Color enemyColor = new(1f, 0.35f, 0.3f);

        public event Action Changed;

        public ContentDatabase Database => database;
        public BattlePhase Phase { get; private set; } = BattlePhase.Placement;
        public EnemyWave Wave { get; private set; }
        public int BattleNumber => RunState.BattlesWon + 1;
        public BattleContext Context { get; private set; }
        public Transform UnitRoot { get; private set; }
        public IReadOnlyList<UnitBlueprint> Roster => RunState.Collection.blueprints;
        public readonly List<BattleUnit> PlayerUnits = new();
        public readonly List<BattleUnit> EnemyUnits = new();

        List<BuffDefinition> playerBuffs;
        GameObject playerZone, enemyZone;
        float endTimer = -1f;
        BattlePhase pendingResult;

        void Awake()
        {
            if (!RunState.HasRun) RunState.StartNewRun(starterSet);
            Context = new BattleContext(Environment.TickCount);
            UnitRoot = new GameObject("Units").transform;
            UnitRoot.SetParent(transform, false);
            playerBuffs = UnitAssembler.ResolveBuffs(RunState.Collection.runBuffIds, database);
            BuildField();
            SpawnWave();
        }

        // Field

        float HalfW => fieldSize.x * 0.5f;
        float HalfD => fieldSize.y * 0.5f;

        void BuildField()
        {
            var field = new GameObject("Field").transform;
            field.SetParent(transform, false);

            var ground = BattleVisuals.Primitive(PrimitiveType.Cube, "Ground", field, BattleVisuals.Lit(groundColor));
            ground.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(fieldSize.x + 2f, 0.5f, fieldSize.y + 2f);

            float zoneDepth = HalfD - noMansLand;
            float zoneCenter = noMansLand + zoneDepth * 0.5f;
            playerZone = Zone(field, "Player Zone", playerColor, -zoneCenter, zoneDepth);
            enemyZone = Zone(field, "Enemy Zone", enemyColor, zoneCenter, zoneDepth);

            var line = BattleVisuals.Primitive(PrimitiveType.Cube, "Centre Line", field, BattleVisuals.Unlit(new Color(1f, 1f, 1f, 1f) * 0.75f));
            line.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            line.transform.localScale = new Vector3(fieldSize.x, 0.01f, 0.06f);
        }

        GameObject Zone(Transform parent, string name, Color color, float z, float depth)
        {
            var zone = BattleVisuals.Primitive(PrimitiveType.Cube, name, parent, BattleVisuals.Lit(Color.Lerp(groundColor, color, 0.22f)));
            zone.transform.localPosition = new Vector3(0f, 0.002f, z);
            zone.transform.localScale = new Vector3(fieldSize.x, 0.004f, depth);
            return zone;
        }

        public bool TryGroundPoint(Ray ray, out Vector3 point)
        {
            point = default;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) return false;
            point = ray.GetPoint(d);
            return Mathf.Abs(point.x) <= HalfW + 1f && Mathf.Abs(point.z) <= HalfD + 1f;
        }

        public Vector3 ClampToPlayerZone(Vector3 p, float radius = 0.5f) =>
            new(Mathf.Clamp(p.x, -HalfW + radius, HalfW - radius), 0f, Mathf.Clamp(p.z, -HalfD + radius, -noMansLand - radius));

        // Setup

        void SpawnWave()
        {
            if (waves.Count == 0) { Debug.LogError("BattleManager has no enemy waves."); return; }
            Wave = waves[Mathf.Min(RunState.BattlesWon, waves.Count - 1)];
            foreach (var e in Wave.units)
            {
                var pos = new Vector3(e.position.x, 0f, Mathf.Max(e.position.y, noMansLand + 0.5f));
                var u = BattleUnit.Create(e.blueprint, Team.Enemy, Wave.buffs, this, pos, enemyColor);
                if (u == null) continue;
                EnemyUnits.Add(u);
                Context.Units.Add(u.Unit);
            }
        }

        public bool CanField(int index) =>
            index >= 0 && index < Roster.Count && UnitAssembler.Validate(Roster[index], database, out _);

        public BattleUnit PlacedUnit(int index) => PlayerUnits.Find(u => u.BlueprintIndex == index);

        public BattleUnit PlacePlayerUnit(int index, Vector3 position)
        {
            if (Phase != BattlePhase.Placement || !CanField(index) || PlacedUnit(index) != null) return null;
            var u = BattleUnit.Create(Roster[index], Team.Player, playerBuffs, this, ClampToPlayerZone(position), playerColor);
            if (u == null) return null;
            u.BlueprintIndex = index;
            PlayerUnits.Add(u);
            Context.Units.Add(u.Unit);
            Changed?.Invoke();
            return u;
        }

        public void RemovePlayerUnit(BattleUnit u)
        {
            if (Phase != BattlePhase.Placement || u == null) return;
            PlayerUnits.Remove(u);
            Context.Units.Remove(u.Unit);
            Destroy(u.gameObject);
            Changed?.Invoke();
        }

        /// <summary>Places every unplaced unit in rows at the front of the player's half.</summary>
        public void AutoPlace()
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((fieldSize.x - 2f) / 2.2f));
            int slot = 0;
            for (int i = 0; i < Roster.Count; i++)
            {
                if (!CanField(i) || PlacedUnit(i) != null) continue;
                Vector3 p;
                do
                {
                    int row = slot / perRow, col = slot % perRow;
                    int inRow = Mathf.Min(perRow, Roster.Count - row * perRow);
                    p = new Vector3((col - (inRow - 1) * 0.5f) * 2.2f, 0f, -noMansLand - 1.5f - row * 2.2f);
                    slot++;
                } while (PlayerUnits.Exists(u => (u.transform.position - p).sqrMagnitude < 1.5f));
                PlacePlayerUnit(i, p);
            }
        }

        public void ClearPlacement()
        {
            foreach (var u in PlayerUnits.ToArray()) RemovePlayerUnit(u);
        }

        public void NotifyChanged() => Changed?.Invoke();

        // Fight

        public void StartBattle()
        {
            if (Phase != BattlePhase.Placement || PlayerUnits.Count == 0) return;
            Phase = BattlePhase.Fighting;
            playerZone.SetActive(false);
            enemyZone.SetActive(false);
            Changed?.Invoke();
        }

        public BattleUnit NearestEnemy(BattleUnit from)
        {
            var list = from.Team == Team.Player ? EnemyUnits : PlayerUnits;
            BattleUnit best = null;
            float bestD = float.MaxValue;
            foreach (var u in list)
            {
                if (!u.IsAlive) continue;
                float d = (u.transform.position - from.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = u; }
            }
            return best;
        }

        public void OnUnitDied(BattleUnit u)
        {
            Changed?.Invoke();
            if (Phase != BattlePhase.Fighting || endTimer >= 0f) return;
            if (!EnemyUnits.Exists(e => e.IsAlive)) { pendingResult = BattlePhase.Victory; endTimer = 1.5f; }
            else if (!PlayerUnits.Exists(p => p.IsAlive)) { pendingResult = BattlePhase.Defeat; endTimer = 1.5f; }
        }

        void Update()
        {
            if (Phase != BattlePhase.Fighting) return;
            Separate();
            if (endTimer >= 0f)
            {
                endTimer -= Time.deltaTime;
                if (endTimer < 0f)
                {
                    Phase = pendingResult;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Pushes overlapping living units apart and keeps them on the field.</summary>
        void Separate()
        {
            var all = new List<BattleUnit>(PlayerUnits.Count + EnemyUnits.Count);
            foreach (var u in PlayerUnits) if (u.IsAlive) all.Add(u);
            foreach (var u in EnemyUnits) if (u.IsAlive) all.Add(u);
            for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
            {
                var a = all[i].transform;
                var b = all[j].transform;
                var d = b.position - a.position;
                d.y = 0f;
                float min = all[i].Radius + all[j].Radius;
                float dist = d.magnitude;
                if (dist >= min) continue;
                var push = (dist > 0.001f ? d / dist : Vector3.right) * (min - dist) * 0.5f;
                a.position -= push;
                b.position += push;
            }
            foreach (var u in all)
            {
                var p = u.transform.position;
                u.transform.position = new Vector3(Mathf.Clamp(p.x, -HalfW, HalfW), 0f, Mathf.Clamp(p.z, -HalfD, HalfD));
            }
        }

        // Leaving

        /// <summary>After the result: a win advances the run, a loss ends it. Both go back to the builder.</summary>
        public void Continue()
        {
            if (Phase == BattlePhase.Victory) RunState.BattlesWon++;
            else if (Phase == BattlePhase.Defeat) RunState.EndRun();
            else return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(builderScene);
        }
    }
}
