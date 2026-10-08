using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer.EditorTools
{
    /// <summary>
    /// Subject 143's claw moveset (version 2: beast-style). The long thorned LEFT arm is the main claw; the right hand adds quick
    /// off-hand rakes. Attacks are short, commit little, chain fast and cancel into the flash step early: the
    /// FromSoft structure (combo windows, cancel points, charge, hyper armour) at a feral tempo.
    /// Hand paths are in character space with real sides (x &lt; 0 = left), authored for a 1.4 m shoulder height;
    /// WeaponIK scales them to the rig. "grip" = palm position, "blade" = finger direction.
    /// </summary>
    public static class ClawMoveset
    {
        static AttackData A(string name, string slot, float duration, float hitStart, float hitEnd, float chain,
            float dodgeCancel, float moveCancel, float lunge, float stamina, float damage, float poise, ClawHand hand, float arcAngle)
        {
            return new AttackData
            {
                name = name, animationSlot = slot, duration = duration, hitStart = hitStart, hitEnd = hitEnd,
                comboWindowStart = Mathf.Max(0.06f, hitStart * 0.5f), chainTime = chain, dodgeCancelTime = dodgeCancel,
                moveCancelTime = moveCancel, trackingEndTime = Mathf.Min(0.16f, hitStart * 0.9f), trackingSpeed = 900f,
                lungeDistance = lunge, lungeStart = 0.02f, lungeEnd = hitStart * 1.1f, staminaCost = stamina,
                damage = damage, poiseDamage = poise, hitStopTime = Mathf.Lerp(0.035f, 0.08f, Mathf.InverseLerp(40f, 130f, damage)),
                hand = hand, arcReach = 1.25f, arcAngle = arcAngle,
            };
        }

        static (float, Vector3, Vector3) K(float t, float gx, float gy, float gz, float bx, float by, float bz) =>
            (t, new Vector3(gx, gy, gz), new Vector3(bx, by, bz));

        static SwingPath P(params (float t, Vector3 grip, Vector3 blade)[] keys)
        {
            var p = new SwingPath();
            foreach (var k in keys) p.keys.Add(new SwingKey(k.t, k.grip, k.blade.normalized));
            return p;
        }

        // Ready poses.
        static readonly Vector3 GuardGrip = new Vector3(-0.32f, 0.95f, 0.38f), GuardBlade = new Vector3(-0.15f, -0.4f, 0.9f);
        static readonly Vector3 OffGrip = new Vector3(0.24f, 1.02f, 0.28f), OffBlade = new Vector3(0.15f, 0.2f, 0.96f);
        static (float, Vector3, Vector3) G(float t) => (t, GuardGrip, GuardBlade);
        static (float, Vector3, Vector3) O(float t) => (t, OffGrip, OffBlade);

        static SwingPath MirrorPath(SwingPath src)
        {
            var p = new SwingPath();
            foreach (var k in src.keys)
                p.keys.Add(new SwingKey(k.t, new Vector3(-k.grip.x, k.grip.y, k.grip.z), new Vector3(-k.blade.x, k.blade.y, k.blade.z)));
            return p;
        }

        /// <summary>Bump when the moveset changes: existing weapon assets are rebuilt on the next build.</summary>
        public const int Version = 4;

        /// <summary>
        /// Beast-style claw moveset (original, in the spirit of feral claw weapons): fast alternating swipes that
        /// build into a multi-hit frenzy and a pouncing maul; heavies are the charged reaver sweep and thorn whirl.
        /// </summary>
        public static void Build(WeaponData w)
        {
            w.displayName = "Thorn Claw";
            w.claws = true;
            w.modelPrefab = null;
            w.twoHanded = false;
            w.primaryHand = HumanBodyBones.LeftHand;
            w.primaryClawLength = 0.24f;
            w.primaryClawRadius = 0.1f;
            w.offClawLength = 0.1f;
            w.offClawRadius = 0.07f;
            w.guardPose = new SwingKey(0f, GuardGrip, GuardBlade.normalized);
            w.offGuardPose = new SwingKey(0f, OffGrip, OffBlade.normalized);
            w.carryPose = new SwingKey(0f, new Vector3(-0.36f, 0.72f, -0.12f), new Vector3(-0.1f, -0.6f, -0.8f).normalized);
            w.movesetVersion = Version;

            // 1: Beast Swipe: main claw rips up and across, low-left to high-right.
            var l1 = A("Beast Swipe", "Light1", 0.42f, 0.11f, 0.19f, 0.22f, 0.16f, 0.3f, 0.6f, 8f, 42f, 12f, ClawHand.Primary, 160f);
            l1.swing = P(G(0f),
                K(0.16f, -0.55f, 0.6f, 0.1f, -0.6f, -0.7f, 0.3f),
                K(0.24f, -0.3f, 0.95f, 0.6f, 0.1f, 0.3f, 0.95f),
                K(0.32f, 0.15f, 1.35f, 0.55f, 0.7f, 0.6f, 0.4f),
                K(0.42f, 0.35f, 1.5f, 0.25f, 0.8f, 0.55f, -0.2f),
                K(0.7f, 0.05f, 1.2f, 0.35f, 0.4f, 0.3f, 0.85f),
                G(1f));

            // 2: Counter Swipe: the off hand answers with the mirrored rip; the main claw cocks back.
            var l2 = A("Counter Swipe", "Light2", 0.4f, 0.1f, 0.18f, 0.21f, 0.15f, 0.29f, 0.6f, 7f, 36f, 10f, ClawHand.Off, 160f);
            l2.swingOff = P(O(0f),
                K(0.16f, 0.5f, 0.6f, 0.1f, 0.6f, -0.7f, 0.3f),
                K(0.24f, 0.25f, 0.95f, 0.55f, -0.1f, 0.3f, 0.95f),
                K(0.32f, -0.15f, 1.3f, 0.5f, -0.7f, 0.6f, 0.4f),
                K(0.6f, 0.0f, 1.1f, 0.35f, -0.3f, 0.3f, 0.9f),
                O(1f));
            l2.swing = P(G(0f), K(0.3f, -0.55f, 1.25f, -0.15f, -0.5f, 0.5f, -0.7f), K(0.85f, -0.5f, 1.2f, -0.1f, -0.5f, 0.5f, -0.7f), G(1f));

            // 3: Twin Rake: hop in and drag both claws straight down.
            var l3 = A("Twin Rake", "Light3", 0.52f, 0.16f, 0.26f, 0.3f, 0.22f, 0.38f, 1.2f, 10f, 55f, 18f, ClawHand.Both, 120f);
            l3.swing = P(G(0f),
                K(0.25f, -0.25f, 1.7f, 0.15f, 0f, 0.7f, -0.7f),
                K(0.35f, -0.2f, 1.3f, 0.6f, 0f, 0.3f, 0.95f),
                K(0.45f, -0.18f, 0.6f, 0.6f, 0f, -0.9f, 0.45f),
                K(0.7f, -0.2f, 0.65f, 0.5f, 0f, -0.85f, 0.5f),
                G(1f));
            l3.swingOff = MirrorPath(l3.swing);

            // 4: Frenzy: three alternating swipes (main, off, main), each a separate hit.
            var l4 = A("Frenzy", "Light4", 0.7f, 0.1f, 0.17f, 0.5f, 0.46f, 0.58f, 0.9f, 13f, 30f, 10f, ClawHand.Both, 150f);
            l4.extraHitWindows.Add(new Vector2(0.24f, 0.31f));
            l4.extraHitWindows.Add(new Vector2(0.38f, 0.46f));
            l4.swing = P(G(0f),
                K(0.08f, -0.6f, 1.25f, 0.0f, -0.85f, 0.2f, -0.4f),
                K(0.17f, 0.2f, 1.1f, 0.55f, 0.8f, -0.1f, 0.55f),
                K(0.3f, -0.5f, 1.3f, 0.05f, -0.8f, 0.3f, -0.5f),
                K(0.44f, 0.25f, 1.0f, 0.6f, 0.85f, -0.2f, 0.5f),
                K(0.65f, 0.0f, 1.0f, 0.4f, 0.4f, -0.2f, 0.9f),
                G(1f));
            l4.swingOff = P(O(0f),
                K(0.2f, 0.6f, 1.25f, 0.0f, 0.85f, 0.2f, -0.4f),
                K(0.31f, -0.2f, 1.05f, 0.55f, -0.8f, -0.1f, 0.55f),
                K(0.6f, 0.1f, 1.0f, 0.35f, 0.2f, 0.1f, 0.95f),
                O(1f));

            // 5: Mauling Pounce: leap forward and slam both claws down (finisher).
            var l5 = A("Mauling Pounce", "Light5", 0.85f, 0.36f, 0.48f, 0.56f, 0.46f, 0.68f, 2.0f, 15f, 90f, 40f, ClawHand.Both, 130f);
            l5.lungeStart = 0.1f; l5.lungeEnd = 0.42f;
            l5.swing = P(G(0f),
                K(0.2f, -0.3f, 1.8f, -0.05f, -0.1f, 0.6f, -0.8f),
                K(0.36f, -0.2f, 1.4f, 0.55f, 0f, 0.4f, 0.9f),
                K(0.46f, -0.15f, 0.45f, 0.6f, 0f, -0.95f, 0.3f),
                K(0.75f, -0.15f, 0.5f, 0.55f, 0f, -0.9f, 0.4f),
                G(1f));
            l5.swingOff = MirrorPath(l5.swing);
            w.lightChain = new List<AttackData> { l1, l2, l3, l4, l5 };

            // Heavy 1: Reaver Sweep: the mutated arm hauls far back and out to the side while charging (winding up slowly),
            // then whips through a wide horizontal arc in front of him and follows through past the other shoulder.
            var h1 = A("Reaver Sweep (hold to charge)", "Heavy1", 1.0f, 0.42f, 0.6f, 0.7f, 0.58f, 0.82f, 1.0f, 20f, 125f, 55f, ClawHand.Primary, 210f);
            h1.chargeTimeMax = 0.9f; h1.chargeHoldPoint = 0.14f; h1.chargeHoldEnd = 0.37f; h1.hyperArmor = true; h1.chargedDamageMultiplier = 1.8f;
            h1.arcReach = 1.5f;
            h1.lungeStart = 0.38f; h1.lungeEnd = 0.5f;
            h1.swing = P(G(0f),
                K(0.14f, -0.7f, 1.1f, -0.2f, -0.85f, 0.05f, -0.45f),
                K(0.37f, -0.75f, 1.2f, -0.5f, -0.55f, 0.15f, -0.8f),
                K(0.45f, -0.55f, 1.05f, 0.45f, -0.35f, -0.05f, 0.95f),
                K(0.51f, 0.0f, 1.0f, 0.8f, 0.35f, -0.08f, 0.93f),
                K(0.57f, 0.55f, 0.98f, 0.45f, 0.95f, -0.1f, 0.25f),
                K(0.64f, 0.7f, 0.98f, -0.1f, 0.75f, -0.15f, -0.6f),
                K(0.85f, 0.3f, 0.95f, 0.15f, 0.5f, -0.3f, 0.8f),
                G(1f));
            // Heavy 2: spinning sweep with both claws out (full circle).
            var h2 = A("Thorn Whirl (hold to charge)", "Heavy2", 0.9f, 0.32f, 0.5f, 0.6f, 0.48f, 0.74f, 0.8f, 20f, 110f, 50f, ClawHand.Both, 360f);
            h2.chargeTimeMax = 0.8f; h2.chargeHoldPoint = 0.14f; h2.chargeHoldEnd = 0.27f; h2.hyperArmor = true;
            h2.swing = P(G(0f),
                K(0.25f, -0.6f, 1.1f, -0.2f, -0.9f, 0f, -0.4f),
                K(0.38f, -0.55f, 1.05f, 0.4f, -0.6f, 0f, 0.8f),
                K(0.46f, 0f, 1.0f, 0.65f, 0.3f, 0f, 0.95f),
                K(0.55f, 0.4f, 1.0f, 0.4f, 0.9f, 0f, 0.4f),
                K(0.8f, -0.2f, 1.0f, 0.3f, -0.4f, -0.2f, 0.9f),
                G(1f));
            h2.swingOff = MirrorPath(h2.swing);
            w.heavyChain = new List<AttackData> { h1, h2 };

            // Sprint attack: Prowling Lunge: low leap that drives the main claw forward like a spear.
            w.sprintAttack = A("Prowling Lunge", "SprintAttack", 0.75f, 0.24f, 0.36f, 0.48f, 0.36f, 0.6f, 2.6f, 14f, 80f, 30f, ClawHand.Primary, 90f);
            w.sprintAttack.carryMomentum = 0.6f;
            w.sprintAttack.swing = P(K(0f, -0.36f, 0.72f, -0.12f, -0.1f, -0.6f, -0.8f),
                K(0.2f, -0.3f, 0.9f, -0.2f, -0.1f, -0.3f, -0.95f),
                K(0.3f, -0.15f, 1.05f, 0.75f, 0f, 0f, 1f),
                K(0.4f, -0.1f, 1.05f, 0.8f, 0f, 0f, 1f),
                K(0.7f, -0.2f, 1.0f, 0.45f, -0.1f, -0.2f, 0.95f),
                G(1f));
            // Jump attack: Falling Maul: both claws overhead, slammed down on landing.
            w.jumpAttack = A("Falling Maul", "JumpAttack", 0.7f, 0.2f, 0.4f, 0.5f, 0.4f, 0.58f, 0f, 12f, 95f, 40f, ClawHand.Both, 140f);
            w.jumpAttack.holdUntilGrounded = true;
            w.jumpAttack.carryMomentum = 1f;
            w.jumpAttack.swing = P(K(0f, -0.25f, 1.6f, 0.1f, -0.1f, 0.6f, -0.8f),
                K(0.22f, -0.2f, 1.75f, 0.0f, 0f, 0.35f, -0.94f),
                K(0.35f, -0.15f, 1.3f, 0.5f, 0f, 0.4f, 0.9f),
                K(0.55f, -0.12f, 0.6f, 0.55f, 0f, -0.85f, 0.5f),
                K(0.8f, -0.12f, 0.6f, 0.55f, 0f, -0.85f, 0.5f),
                G(1f));
            w.jumpAttack.swingOff = MirrorPath(w.jumpAttack.swing);
            // Step attack: Ambush X: both claws start crossed high and rip outward and down.
            w.rollAttack = A("Ambush X", "RollAttack", 0.6f, 0.12f, 0.24f, 0.34f, 0.24f, 0.44f, 1.4f, 10f, 60f, 22f, ClawHand.Both, 150f);
            w.rollAttack.swing = P(K(0f, 0.2f, 1.4f, 0.35f, 0.5f, 0.6f, 0.6f),
                K(0.12f, 0.1f, 1.3f, 0.5f, 0.3f, 0.5f, 0.8f),
                K(0.24f, -0.55f, 0.75f, 0.45f, -0.7f, -0.6f, 0.4f),
                K(0.5f, -0.45f, 0.8f, 0.35f, -0.5f, -0.5f, 0.7f),
                G(1f));
            w.rollAttack.swingOff = MirrorPath(w.rollAttack.swing);
            w.backstepAttack = w.rollAttack.Clone();
            w.backstepAttack.name = "Recoil X";
            w.backstepAttack.animationSlot = "BackstepAttack";
        }

        /// <summary>Follow-up after the Claw Shot skill connects: a diving overhead rake.</summary>
        public static AttackData ClawShotFollowUp()
        {
            var a = A("Claw Shot Rend", "SkillFollowUp", 0.75f, 0.2f, 0.34f, 0.45f, 0.36f, 0.58f, 1.0f, 0f, 95f, 45f, ClawHand.Both, 130f);
            a.hyperArmor = true;
            a.swing = P(K(0f, -0.3f, 1.2f, 0.3f, -0.2f, 0.6f, 0.7f),
                K(0.2f, -0.25f, 1.8f, 0.0f, 0f, 0.35f, -0.94f),
                K(0.32f, -0.15f, 1.45f, 0.45f, 0f, 0.85f, 0.5f),
                K(0.45f, -0.12f, 0.7f, 0.6f, 0f, -0.75f, 0.65f),
                K(0.7f, -0.12f, 0.7f, 0.55f, 0f, -0.8f, 0.6f),
                G(1f));
            a.swingOff = MirrorPath(a.swing);
            return a;
        }
    }
}
