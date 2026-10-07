using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Movement / defensive tuning for one character. Values are approximations tuned by feel, not data
    /// extracted from Nightreign (its archives are encrypted; see Assets/Nightfarer/README.md).
    /// </summary>
    public enum EvadeStyle { FlashStep, Roll }

    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Movement Config", fileName = "Config_")]
    public class NightfarerConfig : ScriptableObject
    {
        [Header("Ground speeds (m/s)")]
        public float walkSpeed = 1.8f;
        public float runSpeed = 4.8f;
        public float sprintSpeed = 7.0f;
        [Tooltip("Nightreign-style surge sprint. Toggled while moving.")]
        public float surgeSpeed = 9.5f;
        public float lockedWalkSpeed = 1.6f;
        public float lockedRunSpeed = 4.0f;
        [Tooltip("Stick magnitude below which the character walks.")]
        [Range(0.1f, 0.9f)] public float walkInputThreshold = 0.55f;

        [Header("Acceleration (m/s^2)")]
        public float acceleration = 32f;
        public float deceleration = 45f;
        public float sprintAcceleration = 18f;
        public float surgeAcceleration = 60f;
        [Tooltip("Reversals sharper than this angle brake first (pivot).")]
        public float pivotAngle = 135f;
        public float pivotDecelerationMultiplier = 1.6f;

        [Header("Turning (deg/s)")]
        public float turnSpeed = 900f;
        public float sprintTurnSpeed = 360f;
        public float lockedTurnSpeed = 720f;
        public float airTurnSpeed = 180f;

        [Header("Sprint / surge")]
        [Tooltip("Hold the dodge button longer than this to sprint; a shorter tap dodges (on release, like Elden Ring).")]
        public float sprintHoldThreshold = 0.25f;
        public float sprintStaminaPerSecond = 6f;
        public bool surgeEnabled = true;
        public float surgeEntryStaminaCost = 10f;
        public float surgeBurstTime = 0.25f;
        public float surgeJumpExtraStamina = 8f;

        [Header("Jump / air")]
        public float jumpHeight = 1.25f;
        public float jumpStaminaCost = 4f;
        public float gravity = -26f;
        public float maxFallSpeed = -45f;
        public float airAcceleration = 6f;
        [Tooltip("Air speed cap as a fraction of run speed when the jump started slower than that.")]
        public float airMinSpeedFraction = 0.55f;
        public float coyoteTime = 0.12f;

        [Header("Super jump (hold Jump, release to spring)")]
        [Tooltip("Holding Jump longer than this starts charging a super jump; a shorter press is a normal jump.")]
        public float superJumpHoldThreshold = 0.16f;
        public float superJumpChargeTime = 0.75f;
        [Tooltip("Apex at full charge (metres). Minimum charge gives 1.5x the normal jump.")]
        public float superJumpHeight = 7.5f;
        [Tooltip("Forward speed added when released while moving, at full charge.")]
        public float superJumpForwardSpeed = 9f;
        [Tooltip("Speed scale while charging and moving (stationary = crouch in place).")]
        public float superJumpChargeMoveScale = 0.45f;
        public float superJumpStaminaCost = 14f;

        [Header("Side jump (locked on / out of combat actions)")]
        public float sideJumpHeight = 0.75f;
        public float sideJumpDistance = 3.4f;
        public float sideJumpStaminaCost = 10f;
        public Vector2 sideJumpIFrames = new Vector2(0.04f, 0.22f);

        [Header("Momentum (claw line)")]
        [Tooltip("Ground braking while carrying claw-line momentum (m/s^2); low = he slides on with it.")]
        public float momentumDeceleration = 9f;
        [Tooltip("Planar speed multiplier for a jump made while carrying momentum.")]
        public float momentumJumpBoost = 1.2f;
        public float momentumJumpHeight = 1.6f;

        [Header("Landing")]
        public float softLandRecovery = 0.12f;
        [Tooltip("Falls higher than this end in a landing roll (Nightreign has no fall damage).")]
        public float hardLandFallHeight = 4.5f;
        public float landRollDuration = 0.65f;
        public float landRollDistance = 2.6f;
        public bool fallDamage = false;
        [Tooltip("Falls at least this high end in a three-point superhero landing (knee down, claw on the ground, ground shake).")]
        public bool heroLanding = true;
        public float heroLandMinHeight = 1.9f;
        [Tooltip("Fall height giving the strongest hero landing (longest recovery, biggest shake).")]
        public float heroLandMaxHeight = 8f;
        public Vector2 heroLandRecovery = new Vector2(0.42f, 0.75f);
        [Tooltip("Normal landings from at least this high shake the camera a little and kick up dust.")]
        public float dustLandMinHeight = 0.8f;

        [Header("Evade style")]
        [Tooltip("FlashStep: Bloodhound's-Step-like displacement. Roll: classic dodge roll + backstep.")]
        public EvadeStyle evadeStyle = EvadeStyle.FlashStep;

        [Header("Flash step")]
        public float stepDistance = 4.0f;
        [Tooltip("Time to cover the distance (the 'flash').")]
        public float stepTravelTime = 0.15f;
        [Tooltip("Recovery after arriving before free movement resumes.")]
        public float stepRecovery = 0.14f;
        public float stepIFrameEnd = 0.2f;
        public float stepStaminaCost = 13f;
        [Tooltip("From this time another step (chained) is allowed.")]
        public float stepChainTime = 0.19f;
        [Tooltip("A light attack pressed in this window becomes the step attack.")]
        public Vector2 stepAttackWindow = new Vector2(0.1f, 0.5f);
        [Tooltip("Hide the body while travelling (afterimages and mist show the path).")]
        public bool stepHideBody = true;
        public AnimationCurve stepCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f), new Keyframe(0.6f, 0.94f, 0.4f, 0.4f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Roll")]
        public float rollDuration = 0.78f;
        public float rollDistance = 3.4f;
        public float rollIFrameStart = 0.03f;
        public float rollIFrameEnd = 0.46f;
        public float rollStaminaCost = 12f;
        public float rollCancelTime = 0.58f;
        public float rollMoveCancelTime = 0.68f;
        public Vector2 rollAttackWindow = new Vector2(0.4f, 0.95f);
        public AnimationCurve rollCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(0.55f, 0.88f, 0.9f, 0.9f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("Backstep")]
        public float backstepDuration = 0.52f;
        public float backstepDistance = 1.7f;
        public float backstepIFrameStart = 0.02f;
        public float backstepIFrameEnd = 0.26f;
        public float backstepStaminaCost = 8f;
        public float backstepCancelTime = 0.4f;
        public Vector2 backstepAttackWindow = new Vector2(0.3f, 0.8f);

        [Header("Stamina / health")]
        public float maxStamina = 100f;
        public float staminaRegen = 45f;
        public float staminaRegenDelay = 0.55f;
        public float staminaRegenDelayWhenEmpty = 1.2f;
        public float maxHealth = 1000f;

        [Header("Flask (Crimson Tears)")]
        public int flaskCharges = 3;
        [Range(0f, 1f)] public float flaskHealFraction = 0.45f;
        public float flaskDuration = 0.95f;
        public float flaskHealTime = 0.5f;
        [Tooltip("Nightreign lets you keep moving while drinking, slowly.")]
        public float flaskMoveSpeedScale = 0.45f;

        [Header("Ultimate Art")]
        [Tooltip("Damage dealt to fill the ultimate gauge.")]
        public float ultimateGaugeDamage = 700f;

        [Header("Mantle / climbing")]
        public float mantleReach = 0.75f;
        public float mantleMinHeight = 0.55f;
        [Tooltip("Highest ledge (above the feet) that can be grabbed while airborne.")]
        public float mantleMaxHeight = 2.4f;
        [Tooltip("Highest ledge vaulted straight from the ground when jumping into it.")]
        public float mantleGroundMaxHeight = 1.6f;
        public float mantleBaseDuration = 0.38f;
        public float mantleDurationPerMetre = 0.12f;

        [Header("Combat")]
        [Tooltip("How long a pressed action stays buffered.")]
        public float inputBufferTime = 0.35f;
        [Tooltip("Planar braking applied while attacking (m/s^2).")]
        public float attackFriction = 30f;
        public float hitReactDuration = 0.45f;
        public float hitReactKnockback = 0.8f;
    }
}
