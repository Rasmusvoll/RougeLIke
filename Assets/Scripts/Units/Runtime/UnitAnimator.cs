using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Units
{
    public enum AttackKind { Melee, Spit, Throw }

    /// <summary>
    /// Procedural animation for an assembled unit, so any mix of body and parts moves without
    /// hand-made clips. Every part pivots on its attach point (the model origin), so each one is
    /// posed by rotating and nudging it from its rest pose, by part kind wherever it's mounted (in the
    /// body's frame, so a leg on the front still steps forward): legs step in diagonal pairs (or hop,
    /// or limp), arms swing and strike, heads look and bob, tails sway, legs that can't reach the
    /// ground paddle in the air, and the body breathes, bobs and squashes. UnitAssembler
    /// adds it to every unit; BattleUnit feeds it movement and attacks, and the builder preview shows
    /// off parts with it. Only the visual moves: physics stays on BattleUnit's rigidbody.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        const float TwoPi = Mathf.PI * 2f;
        const float SwingAmp = 30f * Mathf.Deg2Rad;  // leg swing either side of rest, radians
        const float MaxStepRate = 4.5f;               // steps cycles per second, at most

        class Limb
        {
            public Transform t;
            public string slotId;
            public SlotType type;       // the part's kind, not the slot's: a leg poses as a leg anywhere
            public bool walks;          // a leg that reaches the ground
            public Vector3 attach;      // attach point, relative to the ground under the body
            public float side;          // -1 on mirrored (left) slots
            public float offset;        // per-limb idle offset so nothing moves in lockstep
            public float stepPhase;     // 0 or pi: which half of the walk cycle this limb steps in
            public float hipHeight;
            public bool melee, ranged, thrower;
            public Quaternion restRot;
            public Vector3 restPos, restScale;
            public Transform heldRock;
            public Vector3 rockScale;
            public float rockRegrow = 1f;

            public float windup, windupTarget;
            public float strikeAge = 99f, strikePower;
            public AttackKind strikeKind;
            public int strikeStyle;
        }

        readonly List<Limb> limbs = new();
        Limb head;
        Quaternion restRot;
        Vector3 restPos, restScale;
        float size = 0.6f;
        bool measured;
        bool hasLegs, gaitReady, restCaptured;
        float legLength, lean;
        readonly List<Limb> walkers = new();
        Limb kickLeg;
        Vector3 bodyMid;
        Vector2 bodyHalf = Vector2.one * 0.5f;
        // The end the legs don't hold up hangs down, pivoting on the feet.
        Quaternion droop = Quaternion.identity;
        Vector3 droopPivot;
        const float MaxDroop = 10f;

        float clock, phase, move, airborne, flail;
        float speed, signedDir = 1f;
        bool grounded = true;
        Vector3? lookTarget;
        float lookYaw, lookPitch;
        float squash, flinch;
        float windupCalledAt = -99f;
        int meleeCount;

        // Root (body) attack state.
        float rootWindup, rootWindupTarget, rootStrikeAge = 99f, rootStrikePower;
        AttackKind rootWindupKind, rootStrikeKind;

        // Builder preview show-offs.
        Limb flourishLimb;
        AttackKind flourishKind;
        float flourishAge, marchUntil = -1f;
        bool flourishStruck;

        /// <summary>Animate on unscaled time, for screens that pause the game clock.</summary>
        public bool UnscaledTime { get; set; }
        /// <summary>Limbs kick and flop, for a unit that has fallen over.</summary>
        public bool Flailing { get; set; }

        void Awake() => CaptureRest();

        // Also called lazily, since Awake doesn't run when a unit is spawned in edit mode (e.g. icon renders).
        void CaptureRest()
        {
            restCaptured = true;
            restRot = transform.localRotation;
            restPos = transform.localPosition;
            restScale = transform.localScale;
        }

        void MeasureSize()
        {
            // Body size scales bobs and lunges so a big brute and a little newt move alike.
            measured = true;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            size = Mathf.Clamp(b.size.y / Mathf.Max(0.01f, transform.lossyScale.y), 0.4f, 2.5f);
            bodyMid = transform.InverseTransformPoint(b.center);
            var e = transform.InverseTransformVector(b.extents);
            bodyHalf = new Vector2(Mathf.Max(0.1f, Mathf.Abs(e.x)), Mathf.Max(0.1f, Mathf.Abs(e.z)));
        }

        /// <summary>
        /// Registers a part spawned on a slot. Called by UnitAssembler. The part is posed by its own
        /// kind wherever it's mounted; walks marks a leg that reaches the ground and steps.
        /// </summary>
        public void AddLimb(Transform part, string slotId, SlotType kind, bool mirror, IList<string> tags, Vector3 attachPoint, bool walks)
        {
            var l = new Limb
            {
                t = part,
                slotId = slotId,
                type = kind,
                walks = walks,
                attach = attachPoint,
                side = mirror ? -1f : 1f,
                offset = limbs.Count * 1.7f,
                hipHeight = Mathf.Max(0.15f, attachPoint.y),
                melee = tags != null && tags.Contains("melee"),
                ranged = tags != null && tags.Contains("ranged"),
                thrower = tags != null && tags.Contains("thrower"),
                restRot = part.localRotation,
                restPos = part.localPosition,
                restScale = part.localScale,
            };
            // Left and right swing in opposite halves of the cycle; arms swing against the legs.
            // Walking legs get their phases in SetUpGait, once all are known.
            bool left = attachPoint.x < 0f;
            l.stepPhase = left ? Mathf.PI : 0f;
            if (kind == SlotType.Arm) l.stepPhase += Mathf.PI;
            if (l.thrower)
            {
                l.heldRock = part.Find("Held Rock");
                if (l.heldRock != null) l.rockScale = l.heldRock.localScale;
            }
            limbs.Add(l);
            if (kind == SlotType.Head && head == null) head = l;
            gaitReady = false;
        }

        /// <summary>
        /// Works out the walk from wherever the legs ended up: legs step in diagonal pairs (left
        /// against right, front against back), a lone leg hops, and legs bunched on one side limp.
        /// </summary>
        void SetUpGait()
        {
            gaitReady = true;
            walkers.Clear();
            foreach (var l in limbs) if (l.walks) walkers.Add(l);
            hasLegs = walkers.Count > 0;
            kickLeg = null;
            droop = Quaternion.identity;
            if (!hasLegs) { legLength = size * 0.3f; lean = 0f; return; }

            float total = 0f, midZ = 0f, sideSum = 0f;
            var feetMid = Vector3.zero;
            foreach (var l in walkers) { total += l.hipHeight; midZ += l.attach.z; feetMid += l.attach; }
            legLength = total / walkers.Count;
            midZ /= walkers.Count;
            feetMid /= walkers.Count;

            // Legs bunched at one end: the other end droops (nose down for legs at the tail, one
            // side down for legs all on the other side).
            var off = new Vector2((feetMid.x - bodyMid.x) / bodyHalf.x, (feetMid.z - bodyMid.z) / bodyHalf.y);
            float sag = MaxDroop * (1f - Gait.Support(walkers.Count, off));
            if (off.magnitude > 0.1f)
            {
                var d = off.normalized;
                droop = Quaternion.Euler(-d.y * sag, 0f, d.x * sag);
                droopPivot = new Vector3(feetMid.x, 0f, feetMid.z);
            }
            foreach (var l in walkers)
            {
                float x = l.attach.x;
                bool left = x < -0.05f, right = x > 0.05f;
                bool rear = l.attach.z < midZ - 0.05f;
                l.stepPhase = (left ? Mathf.PI : 0f) + (rear ? Mathf.PI : 0f);
                sideSum += right ? 1f : left ? -1f : 0f;
                if (kickLeg == null || l.attach.z > kickLeg.attach.z) kickLeg = l; // the front-most leg kicks
            }
            lean = sideSum / walkers.Count;
        }

        // Inputs

        /// <summary>The unit's world velocity; drives the walk cycle.</summary>
        public void SetMotion(Vector3 worldVelocity, bool onGround)
        {
            var flat = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
            speed = flat.magnitude;
            var fwd = transform.forward;
            signedDir = Vector3.Dot(flat, new Vector3(fwd.x, 0f, fwd.z)) < -0.3f * speed ? -1f : 1f;
            grounded = onGround;
        }

        /// <summary>A world point the head turns toward, or null to glance around.</summary>
        public void SetLookTarget(Vector3? worldPoint) => lookTarget = worldPoint;

        /// <summary>
        /// How far into the anticipation of the next attack the unit is, 0 to 1. Call every frame while
        /// winding up; it relaxes on its own when the calls stop.
        /// </summary>
        public void SetWindup(AttackKind kind, float amount)
        {
            amount = Mathf.Clamp01(amount);
            windupCalledAt = clock;
            rootWindupKind = kind;
            rootWindupTarget = amount;
            foreach (var l in limbs) l.windupTarget = Uses(l, kind) ? amount : 0f;
        }

        /// <summary>Plays the attack itself. Melee strikes alternate between arms.</summary>
        public void Strike(AttackKind kind)
        {
            StrikeRoot(kind, 1f);
            var arms = new List<Limb>();
            foreach (var l in limbs)
            {
                if (!Uses(l, kind)) continue;
                if (kind == AttackKind.Melee && l.type == SlotType.Arm) arms.Add(l);
                else StrikeLimb(l, kind, 1f);
            }
            if (arms.Count > 0)
            {
                // One arm leads, the other follows through.
                int lead = meleeCount % arms.Count;
                for (int i = 0; i < arms.Count; i++) StrikeLimb(arms[i], kind, i == lead ? 1f : 0.35f);
            }
            if (kind == AttackKind.Melee) meleeCount++;
        }

        /// <summary>A hit: squash and recoil.</summary>
        public void Flinch()
        {
            squash = 1f;
            flinch = 1f;
        }

        /// <summary>Stops animating and puts the body back to its rest size, e.g. on death.</summary>
        public void Freeze()
        {
            transform.localPosition = restPos;
            transform.localScale = restScale;
            enabled = false;
        }

        /// <summary>
        /// Shows off the part on a slot (builder preview): arms and heads attack, tails whip, legs
        /// march on the spot and backs bounce. Returns false if the slot has no part.
        /// </summary>
        public bool Flourish(string slotId)
        {
            var l = limbs.Find(x => x.slotId == slotId && x.t != null);
            if (l == null) return false;
            switch (l.type)
            {
                case SlotType.Leg:
                    marchUntil = clock + 1.8f;
                    break;
                case SlotType.Back:
                    squash = 0.8f;
                    flinch = 0.4f;
                    break;
                default:
                    flourishLimb = l;
                    flourishKind = l.thrower ? AttackKind.Throw
                        : l.ranged && l.type == SlotType.Head ? AttackKind.Spit
                        : AttackKind.Melee;
                    flourishAge = 0f;
                    flourishStruck = false;
                    break;
            }
            return true;
        }

        /// <summary>Shows off a random part, or does a little hop if the unit has none.</summary>
        public void FlourishRandom()
        {
            if (limbs.Count == 0) { squash = 0.6f; return; }
            Flourish(limbs[Random.Range(0, limbs.Count)].slotId);
        }

        bool Uses(Limb l, AttackKind kind) => kind switch
        {
            AttackKind.Throw => l.thrower,
            AttackKind.Spit => l.ranged && !l.thrower && l.type == SlotType.Head,
            _ => HitsInMelee(l) || l == kickLeg && !limbs.Exists(HitsInMelee), // nothing to hit with: kick
        };

        static bool HitsInMelee(Limb l) =>
            l.type == SlotType.Arm && !l.thrower || l.melee && (l.type == SlotType.Head || l.type == SlotType.Tail);

        void StrikeRoot(AttackKind kind, float power)
        {
            rootStrikeKind = kind;
            rootStrikeAge = 0f;
            rootStrikePower = power;
        }

        void StrikeLimb(Limb l, AttackKind kind, float power)
        {
            l.strikeKind = kind;
            l.strikeAge = 0f;
            l.strikePower = power;
            l.strikeStyle = Random.Range(0, 2);
            if (kind == AttackKind.Throw && l.heldRock != null) l.rockRegrow = -0.5f;
        }

        // Per frame

        void LateUpdate() => Tick(UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>Advances the animation by dt seconds and poses the unit. Runs every frame on its own.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (!restCaptured) CaptureRest();
            if (!measured) MeasureSize();
            if (!gaitReady) SetUpGait();
            clock += dt;

            TickFlourish(dt);
            if (clock - windupCalledAt > 0.15f)
            {
                rootWindupTarget = 0f;
                foreach (var l in limbs) l.windupTarget = 0f;
            }

            // Walk cycle. The phase follows distance travelled, so feet roughly stay planted.
            bool marching = clock < marchUntil;
            float moveTarget = marching ? 1f : Mathf.Clamp01(speed / 0.5f);
            move = Mathf.MoveTowards(move, moveTarget, dt * 4f);
            float stride = 4f * SwingAmp * Mathf.Max(0.15f, legLength);
            float rate = marching ? 1.6f : grounded ? Mathf.Min(speed / stride, MaxStepRate) * signedDir : 0f;
            phase = Mathf.Repeat(phase + rate * TwoPi * dt, TwoPi);
            airborne = Mathf.MoveTowards(airborne, grounded || marching ? 0f : 1f, dt * 5f);
            flail = Mathf.MoveTowards(flail, Flailing ? 1f : 0f, dt * 4f);

            squash = Mathf.MoveTowards(squash, 0f, dt * 6f);
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);
            rootWindup = Approach(rootWindup, rootWindupTarget, dt);
            rootStrikeAge += dt;

            UpdateLook(dt);
            PoseRoot();
            foreach (var l in limbs)
            {
                if (l.t == null || l.t.parent != transform) continue; // broken off
                l.windup = Approach(l.windup, l.windupTarget, dt);
                l.strikeAge += dt;
                PoseLimb(l, dt);
            }
        }

        void TickFlourish(float dt)
        {
            if (flourishLimb == null) return;
            flourishAge += dt;
            float windupTime = WindupTime(flourishKind);
            if (!flourishStruck)
            {
                float w = Mathf.Clamp01(flourishAge / windupTime);
                windupCalledAt = clock;
                rootWindupKind = flourishKind;
                rootWindupTarget = w * 0.6f;
                flourishLimb.windupTarget = w;
                if (flourishAge >= windupTime)
                {
                    flourishStruck = true;
                    flourishLimb.windupTarget = 0f;
                    rootWindupTarget = 0f;
                    StrikeLimb(flourishLimb, flourishKind, 1f);
                    StrikeRoot(flourishKind, 0.6f);
                }
            }
            else if (flourishAge > windupTime + 0.8f)
            {
                flourishLimb = null;
            }
        }

        /// <summary>Anticipation time BattleUnit should wind up over before each kind of attack.</summary>
        public static float WindupTime(AttackKind kind) => kind switch
        {
            AttackKind.Throw => 0.6f,
            AttackKind.Spit => 0.35f,
            _ => 0.25f,
        };

        static float Approach(float v, float target, float dt) =>
            Mathf.MoveTowards(v, target, dt * (target > v ? 8f : 14f)); // let go faster than it builds

        /// <summary>0 to 1 to 0: a fast snap out, then an eased recovery.</summary>
        static float StrikeCurve(float age, AttackKind kind)
        {
            float rise = kind == AttackKind.Throw ? 0.12f : kind == AttackKind.Spit ? 0.07f : 0.09f;
            float total = kind == AttackKind.Throw ? 0.7f : 0.45f;
            if (age >= total) return 0f;
            if (age < rise) { float x = age / rise; return 1f - (1f - x) * (1f - x); }
            return 1f - Mathf.SmoothStep(0f, 1f, (age - rise) / (total - rise));
        }

        void UpdateLook(float dt)
        {
            float yaw, pitch;
            if (lookTarget.HasValue && head != null && head.t != null)
            {
                var parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
                var dir = Quaternion.Inverse(parentRot * restRot) * (lookTarget.Value - head.t.position);
                yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -40f, 40f);
                pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
            }
            else
            {
                // Glance around now and then.
                yaw = (Mathf.PerlinNoise(clock * 0.35f, 3.1f) - 0.5f) * 80f;
                pitch = (Mathf.PerlinNoise(7.7f, clock * 0.3f) - 0.5f) * 20f;
            }
            float k = 1f - Mathf.Exp(-dt * 6f);
            lookYaw = Mathf.Lerp(lookYaw, yaw, k);
            lookPitch = Mathf.Lerp(lookPitch, pitch, k);
        }

        void PoseRoot()
        {
            var e = Vector3.zero;
            var p = Vector3.zero;
            var s = Vector3.one;

            // Idle breathing, less of it on the move.
            float breath = Mathf.Sin(clock * 2.2f) * (1f - move * 0.6f);
            s = Vector3.Scale(s, new Vector3(1f - breath * 0.012f, 1f + breath * 0.025f, 1f - breath * 0.012f));

            // Walk: a bob each step, a lean into the walk and a sway.
            p.y += Mathf.Abs(Mathf.Sin(phase)) * 0.035f * size * move;
            if (walkers.Count == 1) p.y += Mathf.Abs(Mathf.Sin(phase)) * 0.09f * size * move; // hop
            e.z += lean * 7f * move * (0.6f + 0.4f * Mathf.Sin(phase));                        // limp toward the short side
            e.x += 5f * move * signedDir;
            e.z += Mathf.Sin(phase) * 3f * move;
            s.y *= 1f + airborne * 0.05f;

            float w = rootWindup;
            float st = StrikeCurve(rootStrikeAge, rootStrikeKind) * rootStrikePower;
            switch (rootWindupKind)
            {
                case AttackKind.Melee: e.x -= 5f * w; s.y *= 1f - 0.07f * w; break;  // crouch
                case AttackKind.Spit: e.x -= 9f * w; s.y *= 1f + 0.03f * w; break;   // rear up
                case AttackKind.Throw: e.x -= 10f * w; s.y *= 1f + 0.05f * w; break; // stretch tall
            }
            switch (rootStrikeKind)
            {
                case AttackKind.Melee: e.x += 12f * st; s.z *= 1f + 0.08f * st; s.y *= 1f - 0.04f * st; break;
                case AttackKind.Spit: e.x -= 8f * st; p.z -= 0.05f * size * st; s.z *= 1f - 0.06f * st; break; // recoil
                case AttackKind.Throw: e.x += 14f * st; s.y *= 1f - 0.07f * st; break;
            }

            // Hit: a quick squash and a knock back.
            float sq = 1f - squash * 0.15f;
            s = Vector3.Scale(s, new Vector3(1f / sq, sq, 1f / sq));
            e.x -= 8f * flinch;

            p += droopPivot - droop * droopPivot; // droop turns about the feet, not the middle
            transform.localPosition = restPos + restRot * p;
            transform.localRotation = restRot * droop * Quaternion.Euler(e);
            transform.localScale = Vector3.Scale(restScale, s);
        }

        void PoseLimb(Limb l, float dt)
        {
            var e = Vector3.zero;    // pitch (- swings forward and up), yaw (+ swings back), roll (+ lifts outward)
            var p = Vector3.zero;
            var s = Vector3.one;
            float w = l.windup;
            float st = StrikeCurve(l.strikeAge, l.strikeKind) * l.strikePower;
            float idle = clock + l.offset;
            float step = phase + l.stepPhase;
            float lift = Mathf.Max(0f, Mathf.Cos(step)); // foot travelling forward = off the ground

            switch (l.type)
            {
                case SlotType.Leg when !l.walks:
                    // Can't reach the ground (e.g. on top of the body): paddles in the air.
                    e.x += Mathf.Sin(idle * 2.6f) * 18f - Mathf.Sin(step) * 25f * move;
                    e.z += Mathf.Sin(idle * 1.9f) * 8f;
                    e.x += Mathf.Sin(clock * 16f + l.offset) * 40f * flail;
                    break;

                case SlotType.Leg:
                    e.x += -Mathf.Sin(step) * SwingAmp * Mathf.Rad2Deg * move;
                    p.y += lift * Mathf.Min(l.hipHeight, 0.5f) * 0.25f * move;
                    e.z += lift * 6f * move;
                    e.x += 15f * airborne;  // dangle
                    e.z += 10f * airborne;
                    e.x += Mathf.Sin(clock * 16f + l.offset) * 40f * flail;
                    // A kick, for a unit with nothing else to hit with: draw back, then stamp forward.
                    e.x += 20f * w;
                    p.y += 0.06f * w;
                    if (l.strikeKind == AttackKind.Melee) { e.x -= 55f * st; p.y += 0.08f * st; }
                    break;

                case SlotType.Arm when l.thrower:
                    e.x += Mathf.Sin(idle * 1.3f) * 3f - Mathf.Sin(step) * 8f * move;
                    e += new Vector3(-110f, 10f, 75f) * w;   // rock up and back over the shoulder
                    p.y += 0.3f * w;
                    if (l.strikeKind == AttackKind.Throw) e += new Vector3(40f, -10f, 10f) * st; // heave
                    e += new Vector3(Mathf.Sin(clock * 13f + l.offset) * 30f, 0f, 25f) * flail;
                    e.z += 20f * flinch;
                    if (l.heldRock != null)
                    {
                        l.rockRegrow = Mathf.Min(1f, l.rockRegrow + dt / 1.2f);
                        l.heldRock.localScale = l.rockScale * BackOut(Mathf.Clamp01(l.rockRegrow));
                    }
                    break;

                case SlotType.Arm:
                    // Swing against the legs; with no legs the arms do the walking.
                    float swing = hasLegs ? 18f : 28f;
                    e.x += -Mathf.Sin(step) * swing * move;
                    if (!hasLegs) p.y += lift * 0.05f * size * move;
                    e.x += Mathf.Sin(idle * 1.3f) * 3f;
                    e.z += Mathf.Sin(idle * 1.1f) * 2f;
                    e += new Vector3(-45f, 30f, 20f) * w;   // raise and draw back
                    if (l.strikeKind == AttackKind.Melee)
                    {
                        if (l.strikeStyle == 0) e += new Vector3(25f, -45f, -5f) * st; // swipe across
                        else { e += new Vector3(-20f, -18f, 0f) * st; p.z += 0.12f * st; } // jab
                    }
                    e += new Vector3(Mathf.Sin(clock * 13f + l.offset) * 35f, 0f, 20f + Mathf.Sin(clock * 9f + l.offset) * 20f) * flail;
                    e += new Vector3(-10f, 0f, 25f) * flinch;  // arms fly out
                    break;

                case SlotType.Head:
                    e.y += lookYaw * (1f - flail);
                    e.x += lookPitch * (1f - flail);
                    e.x += Mathf.Sin(phase * 2f) * 4f * move + Mathf.Sin(clock * 2.2f - 0.6f) * 2f;
                    if (rootWindupKind == AttackKind.Spit || l.strikeKind == AttackKind.Spit)
                    {
                        // Rear back with a swelling throat, then snap forward and spit.
                        e.x -= 28f * w;
                        p.z -= 0.05f * w;
                        s = Vector3.Scale(s, new Vector3(1f + 0.18f * w, 1f + 0.12f * w, 1f + 0.18f * w));
                    }
                    else
                    {
                        e.x -= 20f * w;     // rear up for a headbutt
                        p.z -= 0.04f * w;
                    }
                    if (l.strikeKind == AttackKind.Spit)
                    {
                        e.x += 18f * st;
                        p.z += 0.08f * st;
                        s = Vector3.Scale(s, new Vector3(1f - 0.1f * st, 1f - 0.1f * st, 1f + 0.25f * st));
                    }
                    else if (l.strikeKind == AttackKind.Melee)
                    {
                        e.x += 25f * st;
                        p.z += 0.12f * st;
                    }
                    e.x -= 15f * flinch;
                    e.y += Mathf.Sin(clock * 7f) * 20f * flail;
                    break;

                case SlotType.Tail:
                    e.y += Mathf.Sin(idle * 1.7f) * 10f + Mathf.Sin(idle * 3.1f) * 3f;
                    e.x += Mathf.Sin(idle * 1.3f) * 4f;
                    e.y += Mathf.Sin(phase + 0.8f) * 16f * move;  // lags the body sway
                    e += new Vector3(15f, 30f, 0f) * w;            // cock to one side
                    if (l.strikeKind == AttackKind.Melee) e += new Vector3(10f, -60f, 0f) * st; // whip
                    e.x += 20f * flinch;
                    e.y += Mathf.Sin(clock * 10f + l.offset) * 30f * flail;
                    break;

                case SlotType.Back:
                    // Rides a beat behind the body's bob.
                    p.y -= Mathf.Abs(Mathf.Sin(phase - 0.7f)) * 0.012f * size * move;
                    p.y += Mathf.Sin(clock * 2.2f - 0.6f) * 0.004f * size + 0.04f * flinch;
                    e.z += Mathf.Sin(phase - 0.7f) * 2f * move;
                    break;
            }

            // Mirrored (left) parts mirror yaw, roll and sideways moves.
            l.t.localRotation = Quaternion.Euler(e.x, e.y * l.side, e.z * l.side) * l.restRot;
            l.t.localPosition = l.restPos + new Vector3(p.x * l.side, p.y, p.z);
            l.t.localScale = Vector3.Scale(l.restScale, s);
        }

        static float BackOut(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
