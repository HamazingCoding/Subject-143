using System;
using UnityEngine;

namespace Subject143.Nightfarer
{
    public enum AttackKind { Light, Heavy, Sprint, Jump, Roll, Backstep, FollowUp }

    /// <summary>Which claw(s) deal damage in a claw attack.</summary>
    public enum ClawHand { Primary, Off, Both }

    /// <summary>
    /// One attack. All times are seconds from the start of the attack at 1x speed. The animation clip in
    /// <see cref="animationSlot"/> is time-stretched to <see cref="duration"/>, so gameplay timing never
    /// depends on which clip is plugged in.
    /// </summary>
    [Serializable]
    public class AttackData
    {
        public string name = "Attack";
        public string animationSlot = "Light1";

        [Header("Timeline (s)")]
        public float duration = 0.95f;
        public float hitStart = 0.32f;
        public float hitEnd = 0.46f;
        [Tooltip("More hit windows after the first (x = open, y = close): each is a fresh swing, so a flurry can hit a target several times.")]
        public System.Collections.Generic.List<Vector2> extraHitWindows = new System.Collections.Generic.List<Vector2>();
        [Tooltip("From here a light/heavy press is queued as the next attack.")]
        public float comboWindowStart = 0.25f;
        [Tooltip("Earliest moment the queued next attack starts.")]
        public float chainTime = 0.58f;
        public float dodgeCancelTime = 0.52f;
        [Tooltip("From here movement input ends the attack (late recovery).")]
        public float moveCancelTime = 0.8f;

        [Header("Aim / movement")]
        [Tooltip("Rotation toward target/input is allowed until this time.")]
        public float trackingEndTime = 0.22f;
        public float trackingSpeed = 540f;
        public float lungeDistance = 0.8f;
        public float lungeStart = 0.12f;
        public float lungeEnd = 0.38f;
        [Range(0f, 1f)] public float carryMomentum = 0f;

        [Header("Charge (heavy attacks)")]
        [Tooltip("> 0 makes the attack holdable: it pauses at chargeHoldPoint while the button is held.")]
        public float chargeTimeMax = 0f;
        public float chargeHoldPoint = 0.2f;
        [Tooltip("While held, the wind-up keeps creeping from chargeHoldPoint to this time over chargeTimeMax " +
                 "(Elden Ring style slow wind-up). < 0 = just before hitStart.")]
        public float chargeHoldEnd = -1f;
        public float chargedDamageMultiplier = 1.6f;

        [Header("Air")]
        [Tooltip("Jump attacks: the animation holds at hitEnd until the character lands.")]
        public bool holdUntilGrounded = false;

        [Header("Weapon path (procedural layer; empty = clip drives the arms)")]
        public SwingPath swing = new SwingPath();
        [Tooltip("Claws: path of the off hand (empty = off hand keeps its ready pose).")]
        public SwingPath swingOff = new SwingPath();
        [Tooltip("Claws: which hand's claws are active during the hit window.")]
        public ClawHand hand = ClawHand.Primary;

        [Header("Hit volume")]
        [Tooltip("Extra arc-shaped hit volume in front of the character, in metres (0 = blade sweep only). " +
                 "Placeholder clips only approximate the blade path, so the default movesets use it; set it to 0 " +
                 "once real animations drive the blade.")]
        public float arcReach = 0f;
        [Tooltip("Width of the arc volume in degrees, centred on the character's facing.")]
        public float arcAngle = 120f;

        [Header("Cost / effect")]
        public float staminaCost = 16f;
        public float damage = 60f;
        public float poiseDamage = 20f;
        public float hitStopTime = 0.06f;
        public bool hyperArmor = false;

        [Header("Feel (screen effects)")]
        [Tooltip("Radial screen blur pulse when the hit window opens (0 = none; ~0.05 is strong).")]
        public float screenBlur = 0f;
        [Tooltip("Anime speed lines on screen when the hit window opens (0..1).")]
        public float speedLines = 0f;
        [Tooltip("Hits crack the air (space-crack effect at the impact point).")]
        public bool spaceCrack = false;

        [Tooltip("If true, the hit window is driven by NFEvent(\"HitStart\"/\"HitEnd\") animation events in the clip instead of the times above.")]
        public bool useAnimationEvents = false;

        public float ChargeHoldEnd => Mathf.Clamp(chargeHoldEnd >= 0f ? chargeHoldEnd : hitStart - 0.06f, chargeHoldPoint, hitStart);

        public int HitWindowCount => 1 + (extraHitWindows != null ? extraHitWindows.Count : 0);
        public Vector2 HitWindow(int i) => i <= 0 ? new Vector2(hitStart, hitEnd) : extraHitWindows[i - 1];

        /// <summary>True if t is inside any hit window, widened by the margins.</summary>
        public bool InHitWindow(float t, float before = 0f, float after = 0f)
        {
            for (int i = 0; i < HitWindowCount; i++)
            {
                var w = HitWindow(i);
                if (t >= w.x - before && t <= w.y + after) return true;
            }
            return false;
        }

        public AttackData Clone()
        {
            var c = (AttackData)MemberwiseClone();
            c.extraHitWindows = extraHitWindows != null ? new System.Collections.Generic.List<Vector2>(extraHitWindows) : new System.Collections.Generic.List<Vector2>();
            return c;
        }
    }
}
