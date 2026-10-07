using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Default movesets. Timings are hand-tuned approximations of Elden Ring / Nightreign weapon classes
    /// (Nightreign plays noticeably faster than Elden Ring); they are starting points to tune in the
    /// WeaponData assets, not extracted data.
    /// </summary>
    public static class Movesets
    {
        static AttackData A(string name, string slot, float duration, float hitStart, float hitEnd, float chain,
            float dodgeCancel, float moveCancel, float lunge, float stamina, float damage, float poise)
        {
            return new AttackData
            {
                name = name, animationSlot = slot, duration = duration, hitStart = hitStart, hitEnd = hitEnd,
                comboWindowStart = Mathf.Max(0.12f, hitStart * 0.6f), chainTime = chain, dodgeCancelTime = dodgeCancel,
                moveCancelTime = moveCancel, trackingEndTime = Mathf.Min(0.24f, hitStart * 0.7f), lungeDistance = lunge,
                lungeStart = hitStart * 0.35f, lungeEnd = hitStart * 1.05f, staminaCost = stamina, damage = damage,
                poiseDamage = poise, hitStopTime = Mathf.Lerp(0.05f, 0.1f, Mathf.InverseLerp(60f, 160f, damage)),
            };
        }

        static readonly System.Collections.Generic.Dictionary<string, float> ArcAngles = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Light1", 150f }, { "Light2", 150f }, { "Light3", 70f }, { "Light4", 50f },
            { "Heavy1", 80f }, { "Heavy2", 200f }, { "SprintAttack", 90f }, { "JumpAttack", 100f },
            { "RollAttack", 100f }, { "BackstepAttack", 50f }, { "SkillFollowUp", 120f },
        };

        /// <summary>Placeholder clips only approximate blade paths, so default movesets add an arc volume.</summary>
        static void AddArcs(WeaponData w, float reach)
        {
            foreach (var a in w.lightChain.Concat(w.heavyChain).Concat(new[] { w.sprintAttack, w.jumpAttack, w.rollAttack, w.backstepAttack }))
            {
                if (a == null) continue;
                a.arcReach = reach;
                a.arcAngle = ArcAngles.TryGetValue(a.animationSlot, out var ang) ? ang : 120f;
            }
        }


        // ---------------------------------------------------------------- weapon arcs (character space: x right, y up, z forward)

        static readonly (Vector3 g, Vector3 b) Guard = (new Vector3(0.12f, 1.0f, 0.32f), new Vector3(0.15f, 0.75f, 0.65f));

        static SwingPath Path(params (float t, Vector3 grip, Vector3 blade)[] keys)
        {
            var path = new SwingPath();
            foreach (var k in keys) path.keys.Add(new SwingKey(k.t, k.grip, k.blade.normalized));
            return path;
        }

        static (float, Vector3, Vector3) K(float t, float gx, float gy, float gz, float bx, float by, float bz) =>
            (t, new Vector3(gx, gy, gz), new Vector3(bx, by, bz));

        static (float, Vector3, Vector3) G(float t) => (t, Guard.g, Guard.b);

        /// <summary>Greatsword-class arcs: wide, committed two-handed swings with a strong wind-up and follow-through.</summary>
        static void GreatswordArcs(WeaponData w)
        {
            w.lightChain[0].swing = Path(G(0f),                                  // diagonal: right-high to left-low
                K(0.22f, 0.25f, 1.55f, -0.05f, 0.35f, 0.55f, -0.75f),
                K(0.36f, 0.18f, 1.30f, 0.45f, 0.2f, 0.55f, 0.8f),
                K(0.43f, 0.0f, 1.05f, 0.55f, -0.35f, -0.15f, 0.92f),
                K(0.50f, -0.22f, 0.85f, 0.38f, -0.85f, -0.45f, 0.25f),
                K(0.72f, -0.15f, 0.90f, 0.32f, -0.6f, -0.6f, 0.5f),
                G(1f));
            w.lightChain[1].swing = Path(K(0f, -0.15f, 0.90f, 0.32f, -0.6f, -0.6f, 0.5f),   // backhand: left to right
                K(0.22f, -0.25f, 1.2f, 0.15f, -0.8f, 0.3f, -0.5f),
                K(0.34f, -0.05f, 1.15f, 0.5f, -0.4f, 0.1f, 0.9f),
                K(0.41f, 0.2f, 1.15f, 0.48f, 0.6f, 0.05f, 0.8f),
                K(0.48f, 0.35f, 1.1f, 0.2f, 0.95f, 0.0f, -0.2f),
                K(0.70f, 0.25f, 1.05f, 0.25f, 0.5f, 0.6f, 0.6f),
                G(1f));
            w.lightChain[2].swing = Path(G(0f),                                  // overhead chop
                K(0.30f, 0.05f, 1.75f, -0.05f, 0.05f, 0.35f, -0.93f),
                K(0.42f, 0.03f, 1.6f, 0.35f, 0.0f, 0.9f, 0.4f),
                K(0.50f, 0.0f, 1.1f, 0.58f, 0.0f, -0.3f, 0.95f),
                K(0.58f, 0.0f, 0.72f, 0.5f, 0.0f, -0.85f, 0.5f),
                K(0.80f, 0.05f, 0.85f, 0.45f, 0.05f, -0.6f, 0.8f),
                G(1f));
            if (w.lightChain.Count > 3) w.lightChain[3].swing = Path(G(0f),      // thrust
                K(0.25f, 0.15f, 1.15f, 0.0f, 0.0f, 0.1f, 1f),
                K(0.36f, 0.05f, 1.2f, 0.62f, 0.0f, 0.05f, 1f),
                K(0.55f, 0.05f, 1.2f, 0.6f, 0.0f, 0.05f, 1f),
                G(1f));
            w.heavyChain[0].swing = Path(G(0f),                                  // charged slam (holds at the wind-up)
                K(0.19f, 0.3f, 1.6f, -0.2f, 0.45f, 0.4f, -0.8f),
                K(0.36f, 0.25f, 1.7f, -0.15f, 0.35f, 0.45f, -0.8f),
                K(0.45f, 0.1f, 1.55f, 0.35f, 0.05f, 0.9f, 0.4f),
                K(0.50f, 0.0f, 1.0f, 0.62f, 0.0f, -0.2f, 0.98f),
                K(0.55f, 0.0f, 0.6f, 0.55f, 0.0f, -0.9f, 0.42f),
                K(0.75f, 0.0f, 0.62f, 0.55f, 0.0f, -0.9f, 0.42f),
                G(1f));
            w.heavyChain[1].swing = Path(G(0f),                                  // charged horizontal sweep
                K(0.19f, 0.4f, 1.2f, -0.25f, 0.75f, 0.15f, -0.65f),
                K(0.41f, 0.25f, 1.15f, 0.45f, 0.55f, 0.0f, 0.84f),
                K(0.46f, 0.0f, 1.1f, 0.58f, 0.0f, -0.05f, 1f),
                K(0.52f, -0.3f, 1.1f, 0.42f, -0.85f, 0.0f, 0.5f),
                K(0.60f, -0.35f, 1.1f, 0.05f, -0.9f, 0.0f, -0.4f),
                K(0.80f, -0.2f, 1.0f, 0.25f, -0.6f, 0.4f, 0.6f),
                G(1f));
            w.sprintAttack.swing = Path(K(0f, 0.3f, 0.95f, -0.05f, 0.2f, -0.3f, -0.9f),   // running leap slam
                K(0.25f, 0.15f, 1.7f, 0.0f, 0.1f, 0.4f, -0.9f),
                K(0.38f, 0.05f, 1.5f, 0.4f, 0.0f, 0.9f, 0.4f),
                K(0.44f, 0.0f, 1.0f, 0.62f, 0.0f, -0.2f, 1f),
                K(0.50f, 0.0f, 0.6f, 0.55f, 0.0f, -0.9f, 0.4f),
                K(0.75f, 0.0f, 0.65f, 0.55f, 0.0f, -0.85f, 0.45f),
                G(1f));
            w.jumpAttack.swing = Path(K(0f, 0.1f, 1.6f, 0.1f, 0.1f, 0.6f, -0.8f),          // plunging overhead
                K(0.25f, 0.05f, 1.8f, 0.0f, 0.0f, 0.35f, -0.94f),
                K(0.38f, 0.03f, 1.4f, 0.5f, 0.0f, 0.5f, 0.86f),
                K(0.52f, 0.0f, 0.8f, 0.58f, 0.0f, -0.8f, 0.6f),
                K(0.80f, 0.0f, 0.8f, 0.55f, 0.0f, -0.8f, 0.6f),
                G(1f));
            w.rollAttack.swing = Path(K(0f, 0.2f, 0.7f, 0.3f, 0.3f, -0.8f, 0.5f),          // rising slash out of the roll
                K(0.18f, 0.3f, 0.75f, 0.1f, 0.5f, -0.7f, -0.5f),
                K(0.30f, 0.15f, 1.1f, 0.5f, 0.2f, 0.3f, 0.93f),
                K(0.42f, 0.05f, 1.6f, 0.35f, 0.0f, 0.95f, 0.3f),
                K(0.60f, 0.1f, 1.5f, 0.2f, 0.1f, 0.9f, -0.4f),
                G(1f));
            w.backstepAttack.swing = Path(G(0f),                                 // lunging thrust
                K(0.20f, 0.15f, 1.15f, 0.0f, 0.0f, 0.1f, 1f),
                K(0.33f, 0.05f, 1.2f, 0.65f, 0.0f, 0.05f, 1f),
                K(0.50f, 0.05f, 1.2f, 0.62f, 0.0f, 0.05f, 1f),
                G(1f));
        }

        /// <summary>Straight sword: same shapes, tighter and one-handed.</summary>
        static void StraightSwordArcs(WeaponData w)
        {
            GreatswordArcs(w);
            foreach (var a in w.lightChain.Concat(w.heavyChain).Concat(new[] { w.sprintAttack, w.jumpAttack, w.rollAttack, w.backstepAttack }))
                if (a?.swing != null)
                    foreach (var k in a.swing.keys) k.grip = new Vector3(k.grip.x * 0.9f + 0.06f, k.grip.y, k.grip.z * 0.9f);
        }

        public static void Greatsword(WeaponData w)
        {
            w.displayName = "Greatsword (Wylder-style placeholder)";
            w.autoGrip = true;
            w.bladeStart = new Vector3(0f, 0.42f, 0f);
            w.bladeEnd = new Vector3(0f, 1.55f, 0f);
            w.bladeRadius = 0.14f;
            w.lightChain = new List<AttackData>
            {
                A("GS Light 1", "Light1", 1.0f, 0.36f, 0.5f, 0.62f, 0.56f, 0.85f, 0.9f, 18f, 95f, 40f),
                A("GS Light 2", "Light2", 1.0f, 0.34f, 0.48f, 0.62f, 0.56f, 0.85f, 0.8f, 18f, 100f, 40f),
                A("GS Light 3", "Light3", 1.15f, 0.42f, 0.58f, 0.74f, 0.66f, 0.95f, 1.1f, 20f, 120f, 55f),
            };
            var h1 = A("GS Heavy 1 (hold to charge)", "Heavy1", 1.45f, 0.62f, 0.78f, 0.95f, 0.9f, 1.2f, 1.0f, 28f, 150f, 75f);
            h1.chargeTimeMax = 1.0f; h1.chargeHoldPoint = 0.28f; h1.hyperArmor = true;
            var h2 = A("GS Heavy 2 (hold to charge)", "Heavy2", 1.4f, 0.58f, 0.76f, 0.95f, 0.88f, 1.15f, 1.0f, 28f, 160f, 80f);
            h2.chargeTimeMax = 1.0f; h2.chargeHoldPoint = 0.26f; h2.hyperArmor = true;
            w.heavyChain = new List<AttackData> { h1, h2 };
            w.sprintAttack = A("GS Sprint Attack", "SprintAttack", 1.1f, 0.38f, 0.55f, 0.75f, 0.7f, 0.9f, 1.8f, 22f, 115f, 50f);
            w.sprintAttack.carryMomentum = 0.5f;
            w.jumpAttack = A("GS Jump Attack", "JumpAttack", 0.95f, 0.3f, 0.52f, 0.72f, 0.68f, 0.8f, 0f, 20f, 130f, 60f);
            w.jumpAttack.holdUntilGrounded = true;
            w.jumpAttack.carryMomentum = 1f;
            w.rollAttack = A("GS Roll Attack", "RollAttack", 0.95f, 0.28f, 0.42f, 0.6f, 0.55f, 0.8f, 0.9f, 16f, 90f, 35f);
            w.backstepAttack = A("GS Backstep Thrust", "BackstepAttack", 0.9f, 0.3f, 0.42f, 0.6f, 0.55f, 0.78f, 1.2f, 16f, 85f, 30f);
            w.twoHanded = true;
            w.secondGrip = new Vector3(0f, 0.16f, 0f);
            w.guardPose = new SwingKey(0f, Guard.g, Guard.b.normalized);
            w.carryPose = new SwingKey(0f, new Vector3(0.32f, 0.92f, 0.05f), new Vector3(0.15f, -0.35f, -0.92f).normalized);
            GreatswordArcs(w);
            // The procedural arcs put the blade through the target; a short arc volume covers point-blank hits.
            AddArcs(w, 1.5f);
        }

        public static void StraightSword(WeaponData w)
        {
            w.displayName = "Straight Sword (placeholder)";
            w.autoGrip = true;
            w.bladeStart = new Vector3(0f, 0.25f, 0f);
            w.bladeEnd = new Vector3(0f, 1.0f, 0f);
            w.bladeRadius = 0.1f;
            w.lightChain = new List<AttackData>
            {
                A("SS Light 1", "Light1", 0.72f, 0.22f, 0.32f, 0.4f, 0.36f, 0.56f, 0.6f, 12f, 55f, 18f),
                A("SS Light 2", "Light2", 0.72f, 0.21f, 0.31f, 0.4f, 0.36f, 0.56f, 0.55f, 12f, 55f, 18f),
                A("SS Light 3", "Light3", 0.8f, 0.24f, 0.35f, 0.46f, 0.4f, 0.62f, 0.7f, 13f, 60f, 20f),
                A("SS Light 4", "Light4", 0.9f, 0.3f, 0.4f, 0.56f, 0.48f, 0.7f, 0.9f, 14f, 70f, 25f),
            };
            var h1 = A("SS Heavy 1", "Heavy1", 1.1f, 0.42f, 0.55f, 0.72f, 0.66f, 0.9f, 0.8f, 20f, 95f, 40f);
            h1.chargeTimeMax = 0.8f; h1.chargeHoldPoint = 0.2f;
            var h2 = A("SS Heavy 2", "Heavy2", 1.05f, 0.4f, 0.52f, 0.7f, 0.64f, 0.88f, 0.8f, 20f, 100f, 40f);
            h2.chargeTimeMax = 0.8f; h2.chargeHoldPoint = 0.2f;
            w.heavyChain = new List<AttackData> { h1, h2 };
            w.sprintAttack = A("SS Sprint Attack", "SprintAttack", 0.85f, 0.26f, 0.4f, 0.55f, 0.5f, 0.7f, 1.6f, 14f, 70f, 25f);
            w.sprintAttack.carryMomentum = 0.5f;
            w.jumpAttack = A("SS Jump Attack", "JumpAttack", 0.8f, 0.24f, 0.44f, 0.6f, 0.55f, 0.7f, 0f, 13f, 80f, 30f);
            w.jumpAttack.holdUntilGrounded = true;
            w.jumpAttack.carryMomentum = 1f;
            w.rollAttack = A("SS Roll Attack", "RollAttack", 0.75f, 0.2f, 0.32f, 0.45f, 0.4f, 0.6f, 0.7f, 11f, 55f, 18f);
            w.backstepAttack = A("SS Backstep Thrust", "BackstepAttack", 0.7f, 0.22f, 0.32f, 0.45f, 0.4f, 0.58f, 1.0f, 11f, 50f, 16f);
            w.twoHanded = false;
            w.guardPose = new SwingKey(0f, new Vector3(0.22f, 1.0f, 0.3f), new Vector3(0.1f, 0.8f, 0.6f).normalized);
            w.carryPose = new SwingKey(0f, new Vector3(0.3f, 0.9f, 0.08f), new Vector3(0.1f, -0.5f, 0.86f).normalized);
            StraightSwordArcs(w);
            AddArcs(w, 1.2f);
        }

        public static AttackData ClawFollowUp()
        {
            var a = A("Claw Follow-up", "SkillFollowUp", 1.0f, 0.3f, 0.48f, 0.7f, 0.6f, 0.85f, 1.2f, 0f, 110f, 50f);
            a.hyperArmor = true;
            a.arcReach = 1.6f;
            a.arcAngle = 120f;
            a.swing = Path(K(0f, 0.1f, 1.1f, 0.4f, 0.1f, 0.7f, 0.7f),
                K(0.20f, 0.05f, 1.8f, 0.0f, 0.0f, 0.35f, -0.94f),
                K(0.32f, 0.03f, 1.5f, 0.45f, 0.0f, 0.85f, 0.5f),
                K(0.42f, 0.0f, 0.85f, 0.6f, 0.0f, -0.7f, 0.7f),
                K(0.70f, 0.0f, 0.8f, 0.55f, 0.0f, -0.75f, 0.65f),
                G(1f));
            return a;
        }
    }
}
