using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Subject 143's attack feel, driven by the character's state:
    /// - action lines: streak trails off the claw tips while a strike is live (and on the claw throw / ultimate);
    /// - screen blur + speed lines when an attack flagged for them opens its hit window;
    /// - space cracks on heavy hits, and crackling around the claw arm while the ultimate winds up.
    /// Runs after the IK layer so the trails follow the final claw positions.
    /// </summary>
    [DefaultExecutionOrder(85)]
    public class CombatFX : MonoBehaviour
    {
        public NightfarerCharacter character;
        public Color lineColor = new Color(1f, 0.96f, 1f, 0.95f);
        public Color lineTail = new Color(0.7f, 0.45f, 1f, 0f);
        public float lineTime = 0.2f;
        public float lineWidth = 0.09f;
        [Tooltip("Heavy hits at or above this damage crack the air even if the attack isn't flagged.")]
        public float crackDamage = 150f;

        class Trail
        {
            public TrailRenderer renderer;
            public int segment;      // hitbox segment (0 = main claw, 1 = off hand)
            public float along;      // 0 = wrist end, 1 = tip
            public float offset;     // sideways spread (metres)
        }

        readonly List<Trail> trails = new List<Trail>();
        readonly List<(TrailRenderer trail, Transform bone, Vector3 offset)> surgeLines = new List<(TrailRenderer, Transform, Vector3)>();
        public bool SurgeLinesEmitting { get; private set; }
        Material lineMat;
        object lastAttackState;
        bool blurFired;
        float nextCrackle;

        public bool TrailsEmitting { get; private set; }

        void Awake()
        {
            if (character == null) character = GetComponent<NightfarerCharacter>();
            lineMat = new Material(Shader.Find("Sprites/Default"));
            AddTrail(0, 1f, 0f); AddTrail(0, 0.75f, 0.05f); AddTrail(0, 0.9f, -0.05f);
            AddTrail(1, 1f, 0f); AddTrail(1, 0.7f, 0.03f);
        }

        void Start() => BuildSurgeLines();

        /// <summary>Speed streaks off the legs while surge sprinting.</summary>
        void BuildSurgeLines()
        {
            var anim = character != null && character.Animator != null ? character.Animator.animator : null;
            if (anim == null || !anim.isHuman) return;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.7f, 1f), 1f) },
                      new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.3f, 0.4f), new GradientAlphaKey(0f, 1f) });
            foreach (var hb in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                var bone = anim.GetBoneTransform(hb);
                if (bone == null) continue;
                var go = new GameObject("SurgeLine_" + hb);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = lineMat;
                tr.time = 0.22f;
                tr.minVertexDistance = 0.05f;
                float w = hb == HumanBodyBones.Head ? 0.035f : 0.022f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, w), new Keyframe(1f, 0f));
                tr.colorGradient = g;
                tr.emitting = false;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                surgeLines.Add((tr, bone, Vector3.up * (hb == HumanBodyBones.Head ? 0.06f : 0f)));
            }
        }

        void OnEnable()
        {
            if (character == null) character = GetComponent<NightfarerCharacter>();
            if (character != null) character.HitLanded += OnHit;
        }

        void OnDisable()
        {
            if (character != null) character.HitLanded -= OnHit;
        }

        void OnDestroy()
        {
            foreach (var t in trails) if (t.renderer != null) Destroy(t.renderer.gameObject);
            foreach (var s in surgeLines) if (s.trail != null) Destroy(s.trail.gameObject);
            if (lineMat != null) Destroy(lineMat);
        }

        void AddTrail(int segment, float along, float offset)
        {
            var go = new GameObject($"ClawActionLine_{segment}_{trails.Count}");
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = lineMat;
            tr.time = lineTime;
            tr.minVertexDistance = 0.02f;
            float w = lineWidth * (segment == 0 ? 1f : 0.7f) * (offset == 0f ? 1f : 0.55f);
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, w), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(lineColor, 0f), new GradientColorKey(new Color(lineTail.r, lineTail.g, lineTail.b), 1f) },
                      new[] { new GradientAlphaKey(lineColor.a, 0f), new GradientAlphaKey(lineColor.a * 0.5f, 0.35f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.numCapVertices = 0;
            trails.Add(new Trail { renderer = tr, segment = segment, along = along, offset = offset });
        }

        void LateUpdate()
        {
            if (character == null || character.Hitbox == null) return;
            int mask = LiveMask();
            TrailsEmitting = mask != 0;
            var cam = Camera.main;
            foreach (var t in trails)
            {
                if (!character.Hitbox.TryGetSegment(t.segment, out var a, out var b)) { t.renderer.emitting = false; continue; }
                Vector3 axis = b - a;
                Vector3 side = cam != null ? Vector3.Cross(axis, cam.transform.forward).normalized : Vector3.up;
                Vector3 p = Vector3.Lerp(a, b, t.along) + side * t.offset;
                bool on = ((1 << t.segment) & mask) != 0;
                if (on && !t.renderer.emitting) t.renderer.Clear();
                t.renderer.transform.position = p;
                t.renderer.emitting = on;
            }
            AttackPulses();
            UltimateCrackle();
            SurgeLines();
        }

        /// <summary>Which claws are drawing action lines right now (bit 0 main claw, bit 1 off hand).</summary>
        int LiveMask()
        {
            var s = character.State;
            if (s is AttackState atk)
            {
                var a = atk.Data;
                float t = atk.AttackTime;
                if (!a.InHitWindow(t, 0.07f, 0.05f)) return 0;
                return a.hand == ClawHand.Primary ? 1 : a.hand == ClawHand.Off ? 2 : 3;
            }
            if (s is AbilityState ab)
            {
                if (ab.Data == character.Profile?.ultimate) return ab.Instance.Elapsed > 0.25f ? 3 : 0;
                if (ab.Instance.Phase == "Throw" || ab.Instance.Phase == "Pull") return 1;
            }
            return 0;
        }

        float nextSpark;

        void SurgeLines()
        {
            bool on = character.IsSurging && character.Motor.PlanarSpeed > character.Config.sprintSpeed * 0.9f;
            // The dragging claw scrapes sparks off the ground.
            if (on && character.Motor.Grounded && Time.time >= nextSpark && character.Hitbox.TryGetSegment(0, out _, out var tip) &&
                tip.y - character.transform.position.y < 0.15f)
            {
                nextSpark = Time.time + 0.035f;
                ClawSparks.Emit(tip, -character.Motor.PlanarVelocity.normalized, 2);
            }
            SurgeLinesEmitting = on;
            foreach (var s in surgeLines)
            {
                if (s.trail == null || s.bone == null) continue;
                if (on && !s.trail.emitting) s.trail.Clear();
                s.trail.transform.position = s.bone.position + s.offset;
                s.trail.emitting = on;
            }
        }

        void AttackPulses()
        {
            if (!(character.State is AttackState atk))
            {
                lastAttackState = null;
                return;
            }
            if (!ReferenceEquals(atk, lastAttackState))
            {
                lastAttackState = atk;
                blurFired = false;
            }
            var a = atk.Data;
            if (blurFired || atk.AttackTime < a.hitStart) return;
            blurFired = true;
            float charge = a.chargeTimeMax > 0f ? atk.ChargeRatio : 0f;
            float blur = a.screenBlur * (1f + charge);
            Vector3 tip = character.Hitbox.TryGetSegment(a.hand == ClawHand.Off ? 1 : 0, out _, out var end) ? end : character.transform.position + Vector3.up;
            if (blur > 0f) ScreenFX.Blur(blur, 0.22f, tip);
            if (a.speedLines > 0f || charge > 0.5f) ScreenFX.SpeedLines(Mathf.Max(a.speedLines, charge * 0.6f), 0.3f);
        }

        void UltimateCrackle()
        {
            if (!(character.State is AbilityState ab) || ab.Data != character.Profile?.ultimate) return;
            if (ab.Instance.Elapsed > 0.6f || Time.time < nextCrackle) return;
            nextCrackle = Time.time + 0.06f;
            var anim = character.Animator != null ? character.Animator.animator : null;
            if (anim == null || !anim.isHuman || character.Weapon == null) return;
            bool left = character.Weapon.primaryHand == HumanBodyBones.LeftHand;
            var fore = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            if (fore == null) return;
            Vector3 tip = character.Hitbox.TryGetSegment(0, out _, out var end) ? end : fore.position;
            Vector3 p = Vector3.Lerp(fore.position, tip, Random.value) + Random.insideUnitSphere * 0.12f;
            SpaceCrack.Spawn(p, Random.Range(0.18f, 0.32f), null, 0.3f, Random.Range(4, 6));
        }

        void OnHit(IDamageable target, DamageInfo info)
        {
            var atk = character.State as AttackState;
            bool flagged = atk != null && (atk.Data.spaceCrack || atk.ChargeRatio > 0.5f);
            if (!flagged && info.amount < crackDamage) return;
            SpaceCrack.Spawn(info.point, Mathf.Lerp(1.1f, 1.9f, Mathf.InverseLerp(60f, 300f, info.amount)));
            ScreenFX.Blur(0.03f, 0.15f, info.point);
        }
    }
}
