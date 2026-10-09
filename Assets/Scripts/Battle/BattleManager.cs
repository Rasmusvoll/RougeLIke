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
        [Tooltip("What a win offers. The player picks one offer or skips.")]
        [SerializeField] RewardTable rewardTable;

        [Header("Projectiles")]
        [SerializeField] Mesh boulderMesh;
        [SerializeField] Material boulderMaterial;

        [Header("Field")]
        [SerializeField] Vector2 fieldSize = new(15f, 10f);
        [Tooltip("Half-width of the strip in the middle where nobody can be placed.")]
        [SerializeField] float noMansLand = 1.2f;
        [SerializeField] Color groundColor = new(0.918f, 0.851f, 0.69f);
        [SerializeField] Color playerColor = new(0.247f, 0.431f, 0.58f);
        [SerializeField] Color enemyColor = new(0.851f, 0.475f, 0.169f);

        [Header("Arenas")]
        [Tooltip("Battlefields in rotation: battle N is fought on arena N (wrapping), unless its wave names one.")]
        [SerializeField] List<ArenaDefinition> arenas = new();
        [Tooltip("Half size of the board the field sits on.")]
        [SerializeField] Vector2 boardHalfSize = new(9.2f, 6.6f);

        public event Action Changed;

        public ContentDatabase Database => database;
        public Mesh BoulderMesh => boulderMesh;
        public Material BoulderMaterial => boulderMaterial;
        public BattlePhase Phase { get; private set; } = BattlePhase.Placement;
        public EnemyWave Wave { get; private set; }
        public Arena Arena { get; private set; }
        public ArenaDefinition ArenaDefinition => Arena != null ? Arena.Definition : null;
        /// <summary>The fight was called after nobody landed a hit for a while.</summary>
        public bool Stalemate { get; private set; }
        public int BattleNumber => RunState.BattlesWon + 1;
        public BattleContext Context { get; private set; }
        public Transform UnitRoot { get; private set; }
        public IReadOnlyList<UnitBlueprint> Roster => RunState.Collection.blueprints;
        public readonly List<BattleUnit> PlayerUnits = new();
        public readonly List<BattleUnit> EnemyUnits = new();
        /// <summary>Rolled once when the battle is won. Empty if there's no reward table.</summary>
        public readonly List<RewardEntry> RewardOffers = new();

        List<BuffDefinition> playerBuffs;
        GameObject playerZone, enemyZone;
        const float StalemateTime = 20f;  // seconds without a hit before the fight is called

        float endTimer = -1f, shake, lastHitTime;
        Camera cam;
        Vector3 camHome;
        BattlePhase pendingResult;

        void Awake()
        {
            if (!RunState.HasRun) RunState.StartNewRun(starterSet);
            Context = new BattleContext(Environment.TickCount);
            UnitRoot = new GameObject("Units").transform;
            UnitRoot.SetParent(transform, false);
            playerBuffs = UnitAssembler.ResolveBuffs(RunState.Collection.runBuffIds, database);
            cam = Camera.main;
            if (cam != null) camHome = cam.transform.position;
            PickWave();
            BuildField();
            SpawnWave();
        }

        // Field

        float HalfW => fieldSize.x * 0.5f;
        float HalfD => fieldSize.y * 0.5f;
        float NoMansLand => ArenaDefinition != null ? ArenaDefinition.noMansLand : noMansLand;

        /// <summary>
        /// The wave's own arena on its first showing, otherwise the next arena in rotation. Fights
        /// past the last wave rotate, so a repeated wave still gets a new field each time.
        /// </summary>
        ArenaDefinition PickArena()
        {
            int i = RunState.BattlesWon;
            if (Wave != null && Wave.arena != null && i < waves.Count) return Wave.arena;
            var list = arenas.FindAll(a => a != null);
            return list.Count > 0 ? list[i % list.Count] : null;
        }

        public float GroundHeight(Vector3 p) => Arena != null ? Arena.HeightAt(p.x, p.z) : 0f;

        void BuildField()
        {
            var field = new GameObject("Field").transform;
            field.SetParent(transform, false);

            var def = PickArena();
            if (def != null) Arena = Arena.Build(def, field, boardHalfSize);
            else
            {
                var ground = BattleVisuals.Primitive(PrimitiveType.Cube, "Ground", field, BattleVisuals.Lit(groundColor), keepCollider: true);
                ground.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                ground.transform.localScale = new Vector3(fieldSize.x + 2f, 0.5f, fieldSize.y + 2f);
            }

            // No walls: the edge of the board is a drop, and shoving enemies off it is half the fight.

            float zoneDepth = HalfD - NoMansLand;
            float zoneCenter = NoMansLand + zoneDepth * 0.5f;
            playerZone = Zone(field, "Player Zone", playerColor, -zoneCenter, zoneDepth);
            enemyZone = Zone(field, "Enemy Zone", enemyColor, zoneCenter, zoneDepth);

            // An inked dashed line down the middle, like a path drawn on the map.
            if (def != null && !def.centreLine) return;
            var ink = BattleVisuals.Unlit(BattleVisuals.Palette.Ink);
            for (float x = -HalfW + 0.3f; x < HalfW; x += 0.7f)
            {
                if (Arena != null && Arena.InHole(x, 0f, 1.1f)) continue;
                var dash = BattleVisuals.Primitive(PrimitiveType.Cube, "Centre Dash", field, ink);
                dash.transform.localPosition = new Vector3(x, 0.006f, 0f);
                dash.transform.localScale = new Vector3(0.38f, 0.01f, 0.07f);
                dash.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        GameObject Zone(Transform parent, string name, Color color, float z, float depth)
        {
            var zone = BattleVisuals.Primitive(PrimitiveType.Cube, name, parent, BattleVisuals.Toon(Color.Lerp(Color.white, color, 0.4f), 0f));
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
            new(Mathf.Clamp(p.x, -HalfW + radius, HalfW - radius), 0f, Mathf.Clamp(p.z, -HalfD + radius, -NoMansLand - radius));

        // Setup

        void PickWave()
        {
            if (waves.Count == 0) { Debug.LogError("BattleManager has no enemy waves."); return; }
            Wave = waves[Mathf.Min(RunState.BattlesWon, waves.Count - 1)];
        }

        void SpawnWave()
        {
            if (Wave == null) return;
            foreach (var e in Wave.units)
            {
                var pos = new Vector3(e.position.x, 0f, Mathf.Max(e.position.y, NoMansLand + 0.5f));
                pos.y = GroundHeight(pos);
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
            var p = ClampToPlayerZone(position);
            p.y = GroundHeight(p);
            var u = BattleUnit.Create(Roster[index], Team.Player, playerBuffs, this, p, playerColor);
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
                    p = new Vector3((col - (inRow - 1) * 0.5f) * 2.2f, 0f, -NoMansLand - 1.2f - row * 1.8f);
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
            foreach (var u in PlayerUnits) u.SetSimulated(true);
            foreach (var u in EnemyUnits) u.SetSimulated(true);
            lastHitTime = Time.time;
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

        /// <summary>Feedback for a landed hit: a spark, and a camera shake that grows with the knock.</summary>
        public void Shake(float amount) => shake = Mathf.Min(0.4f, shake + amount);

        public void OnHit(Vector3 point, float knock, Team victim)
        {
            lastHitTime = Time.time;
            BattleEffects.Spark(point, 0.25f + knock * 0.06f, victim == Team.Player ? playerColor : enemyColor);
            shake = Mathf.Min(0.35f, shake + knock * 0.02f);
        }

        public void OnUnitDied(BattleUnit u)
        {
            shake = Mathf.Min(0.4f, shake + 0.15f);
            Changed?.Invoke();
            if (Phase != BattlePhase.Fighting || endTimer >= 0f) return;
            if (!EnemyUnits.Exists(e => e.IsAlive)) { pendingResult = BattlePhase.Victory; endTimer = 1.5f; }
            else if (!PlayerUnits.Exists(p => p.IsAlive)) { pendingResult = BattlePhase.Defeat; endTimer = 1.5f; }
        }

        /// <summary>Health left on a side, as a share of its full health.</summary>
        static float HealthShare(List<BattleUnit> units)
        {
            float now = 0f, max = 0f;
            foreach (var u in units)
            {
                max += Mathf.Max(1f, u.Unit.Stats.Get(StatType.MaxHealth));
                if (u.IsAlive) now += u.Unit.CurrentHealth;
            }
            return max > 0f ? now / max : 0f;
        }

        void Update()
        {
            if (Phase != BattlePhase.Fighting) return;
            // Stalemate (say legless turrets out of each other's reach): whoever has more health left wins.
            if (endTimer < 0f && Time.time - lastHitTime > StalemateTime)
            {
                pendingResult = HealthShare(PlayerUnits) > HealthShare(EnemyUnits) ? BattlePhase.Victory : BattlePhase.Defeat;
                Stalemate = true;
                endTimer = 0.5f;
            }
            if (endTimer < 0f) return;
            endTimer -= Time.deltaTime;
            if (endTimer < 0f)
            {
                Phase = pendingResult;
                if (Phase == BattlePhase.Victory && rewardTable != null)
                    RewardOffers.AddRange(rewardTable.Roll(new System.Random(Environment.TickCount), RunState.Collection));
                Changed?.Invoke();
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.5f);
            cam.transform.position = camHome + UnityEngine.Random.insideUnitSphere * shake;
        }

        // Leaving

        /// <summary>Takes one of the reward offers (null skips) and goes back to the builder.</summary>
        public void ClaimReward(RewardEntry offer)
        {
            if (Phase != BattlePhase.Victory) return;
            if (offer != null && RewardOffers.Contains(offer))
            {
                RunState.Collection.Grant(offer);
                RunState.NewContentId = offer.content.id;
            }
            Continue();
        }

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
