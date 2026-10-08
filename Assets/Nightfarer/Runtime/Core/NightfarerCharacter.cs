using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Coordinates input buffering, the state machine, the motor, vitals, weapon and animation. Per frame:
    /// read input → buffer actions → tick the current state → simulate the motor → drive locomotion animation.
    /// States (NightfarerStates.cs) contain the behaviour; this class only offers shared helpers.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(NightfarerMotor), typeof(NightfarerVitals))]
    public class NightfarerCharacter : MonoBehaviour, IDamageable
    {
        public enum BufferedAction { None, Light, Heavy, Dodge, Jump, Skill, Flask, Ultimate, SuperJump }

        [Header("Profiles and animation sets")]
        public CharacterProfile[] profiles;
        public int startProfile;
        [Tooltip("Sets you can cycle through at runtime (F2). The profile's own set is selected on profile change.")]
        public AnimationSet[] animationSets;

        [Header("References")]
        public NightfarerAnimator animatorDriver;
        public WeaponHitbox hitbox;
        public LockOnSystem lockOn;
        public NightfarerCamera cameraRig;
        [Tooltip("Component implementing INightfarerInputSource (DeviceInputSource by default).")]
        public MonoBehaviour inputSource;
        public AnimationEventRelay eventRelay;
        public WeaponIK weaponIK;
        public FootIK footIK;

        public NightfarerMotor Motor { get; private set; }
        public NightfarerVitals Vitals { get; private set; }
        public NightfarerAnimator Animator => animatorDriver;
        public WeaponHitbox Hitbox => hitbox;
        public LockOnSystem LockOn => lockOn;
        public FootIK FootIK => footIK;
        public Transform ViewTransform => cameraRig != null ? cameraRig.transform : (Camera.main != null ? Camera.main.transform : null);

        public CharacterProfile Profile { get; private set; }
        public NightfarerConfig Config => Profile != null ? Profile.config : null;
        public WeaponData Weapon { get; private set; }
        public GameObject WeaponModel => weaponModel;
        public int ProfileIndex { get; private set; }
        public int AnimationSetIndex { get; private set; }

        public NightfarerInputFrame CurrentInput { get; private set; }
        public NightfarerState State { get; private set; }
        public string StateName => State != null ? State.Name : "-";
        public readonly List<string> StateLog = new List<string>();
        public event Action<string> StateChanged;

        // Flags and readouts written by states.
        public bool IsSprinting { get; set; }
        public bool IsSurging { get; set; }
        public bool WalkToggled { get; set; }
        public bool IsInvulnerable { get; set; }
        public bool HasHyperArmor { get; set; }
        public string MovementMode { get; set; } = "Idle";
        public string CombatPhase { get; set; } = "-";
        public int ComboStep { get; set; }
        public AttackData CurrentAttack { get; set; }
        public bool SprintHeld { get; private set; }
        public float SurgeBurstRemaining { get; set; }
        public float SkillCooldownRemaining { get; private set; }
        public int FlaskCharges { get; private set; }
        /// <summary>0..1; fills by dealing damage. The ultimate art is usable at 1.</summary>
        public float UltimateGauge { get; private set; }
        public bool UltimateReady => UltimateGauge >= 0.999f && Profile != null && Profile.ultimate != null;
        /// <summary>Set by menus: gameplay ignores input while true.</summary>
        public bool InputBlocked { get; set; }
        /// <summary>Spirit spring the character is standing in (set by the spring).</summary>
        public SpiritSpring ActiveSpring { get; set; }
        /// <summary>True while the Jump button is held (super jump charge).</summary>
        public bool JumpHeld => jumpDown;
        /// <summary>&gt; 0 while momentum from the claw line is carried (he slides on with it; a jump boosts it).</summary>
        public float MomentumTimer { get; set; }
        /// <summary>Last time he attacked, used a skill or was hit.</summary>
        public float LastCombatTime { get; private set; } = -99f;
        /// <summary>Claws held ready (locked on, or just after fighting); otherwise the arms hang.</summary>
        public bool InCombatStance => IsLocked || Time.time - LastCombatTime < 2.5f;

        // Events for UI / feedback.
        public event Action<DamageInfo> Damaged;
        public event Action Dodged;
        public event Action<IDamageable, DamageInfo> HitLanded;
        public event Action<string> Notification;

        // Stats for the debug HUD / tests.
        public int DodgedHits { get; private set; }
        public int TakenHits { get; private set; }
        public float LastHitDealt { get; private set; }
        public int HitsDealt { get; private set; }

        public bool IsLocked => lockOn != null && lockOn.Current != null;
        public bool IsAlive => true;
        public bool FollowUpReady => followUpAttack != null && Time.time <= followUpUntil;

        INightfarerInputSource input;
        BufferedAction buffered;
        float bufferedAt;
        bool dodgeDown;
        float dodgeDownAt;
        bool jumpDown, superBuffered;
        float jumpDownAt;
        float surgeStopTimer;
        AttackData followUpAttack;
        float followUpUntil;
        GameObject weaponModel;
        int weaponIndex;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        void Awake()
        {
            Motor = GetComponent<NightfarerMotor>();
            Vitals = GetComponent<NightfarerVitals>();
            if (input == null) input = inputSource as INightfarerInputSource;
            if (input == null) input = GetComponent<INightfarerInputSource>();
            Motor.Landed += (height, speed) =>
            {
                if (footIK != null) footIK.Impact(speed);
                if (cameraRig != null) cameraRig.LandingDip(speed);
                State?.OnLanded(height, speed);
            };
            if (hitbox != null) hitbox.Hit += OnWeaponHit;
            if (eventRelay != null) eventRelay.EventRaised += e => State?.OnAnimationEvent(e);
            // Measure the rig while it is still in its bind pose (before the Animator's first update).
            var a = animatorDriver != null ? animatorDriver.animator : null;
            WeaponMount.PrepareGrip(a, HumanBodyBones.RightHand);
            WeaponMount.PrepareGrip(a, HumanBodyBones.LeftHand);
            if (weaponIK != null) weaponIK.MeasureRig();
        }

        void Start()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            var first = profiles != null && profiles.Length > 0 ? profiles[Mathf.Clamp(startProfile, 0, profiles.Length - 1)] : null;
            animatorDriver.Initialize(first != null ? first.animationSet : null);
            ApplyProfile(startProfile);
            ChangeState(new LocomotionState(this));
        }

        public void SetInputSource(INightfarerInputSource source) => input = source;

        /// <summary>For abilities that deal damage outside the weapon hitbox (ultimates, hooks).</summary>
        public void ReportExternalHit(IDamageable target, DamageInfo info) => OnWeaponHit(target, info);

        public void Notify(string message) => Notification?.Invoke(message);

        void Update()
        {
            if (Profile == null) return;
            float dt = Time.deltaTime;
            var frame = input != null ? input.Read() : default;
            CurrentInput = InputBlocked ? default : frame;
            ProcessInput();
            if (SkillCooldownRemaining > 0f) SkillCooldownRemaining -= dt;
            if (MomentumTimer > 0f) MomentumTimer -= dt;

            State?.Tick(dt);
            Motor.Simulate(dt);
            UpdateLocomotionAnimation(dt);

            Vitals.RegenBlocked = (IsSprinting && Config.sprintStaminaPerSecond > 0f) || (State != null && State.BlocksStaminaRegen);
        }

        // ---------------------------------------------------------------- input

        void ProcessInput()
        {
            var f = CurrentInput;
            if (f.dodgePressed)
            {
                dodgeDown = true;
                dodgeDownAt = Time.time;
            }
            if (f.dodgeReleased && dodgeDown)
            {
                dodgeDown = false;
                if (Time.time - dodgeDownAt < Config.sprintHoldThreshold) Buffer(BufferedAction.Dodge);
            }
            if (!f.dodgeHeld && !f.dodgeReleased) dodgeDown = false;
            SprintHeld = dodgeDown && Time.time - dodgeDownAt >= Config.sprintHoldThreshold;

            if (f.lightPressed) Buffer(BufferedAction.Light);
            if (f.heavyPressed) Buffer(BufferedAction.Heavy);
            // Jump: a quick press jumps (on release); holding past the threshold charges a super jump instead.
            if (f.jumpPressed)
            {
                if (f.jumpHeld)
                {
                    jumpDown = true;
                    superBuffered = false;
                    jumpDownAt = Time.time;
                }
                else Buffer(BufferedAction.Jump);
            }
            if (jumpDown)
            {
                if (!f.jumpHeld)
                {
                    jumpDown = false;
                    if (!superBuffered) Buffer(BufferedAction.Jump);
                }
                else if (!superBuffered && Time.time - jumpDownAt >= Config.superJumpHoldThreshold)
                {
                    superBuffered = true;
                    Buffer(BufferedAction.SuperJump);
                }
            }
            if (f.skillPressed) Buffer(BufferedAction.Skill);
            if (f.flaskPressed) Buffer(BufferedAction.Flask);
            if (f.ultimatePressed) Buffer(BufferedAction.Ultimate);
            if (f.lockOnPressed) ToggleLockOn();
            if (f.walkTogglePressed) WalkToggled = !WalkToggled;
            if (f.switchWeaponPressed) CycleWeapon();
        }

        void Buffer(BufferedAction a)
        {
            buffered = a;
            bufferedAt = Time.time;
        }

        public BufferedAction PeekBuffer()
        {
            if (buffered != BufferedAction.None && Time.time - bufferedAt > Config.inputBufferTime) buffered = BufferedAction.None;
            return buffered;
        }

        public void ConsumeBuffer() => buffered = BufferedAction.None;

        public void ToggleLockOn()
        {
            if (lockOn == null) return;
            bool locked = lockOn.Toggle(ViewTransform != null ? ViewTransform : transform);
            if (!locked && cameraRig != null && lockOn.Current == null) cameraRig.Recenter();
        }

        // ---------------------------------------------------------------- helpers for states

        /// <summary>Camera-relative move intent on the ground plane, magnitude 0..1.</summary>
        public Vector3 MoveInputWorld()
        {
            Vector2 m = CurrentInput.move;
            if (m.magnitude < 0.15f) return Vector3.zero;
            if (m.sqrMagnitude > 1f) m.Normalize();
            Transform view = ViewTransform;
            Vector3 fwd = view != null ? view.forward : transform.forward;
            Vector3 right = view != null ? view.right : transform.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();
            return fwd * m.y + right * m.x;
        }

        public Vector3 LockTargetDirection()
        {
            if (!IsLocked) return Vector3.zero;
            Vector3 d = lockOn.Current.transform.position - transform.position;
            d.y = 0f;
            return d;
        }

        public float DistanceToLockTarget() => IsLocked ? LockTargetDirection().magnitude : float.MaxValue;

        public Vector3 HandPosition
        {
            get
            {
                var a = animatorDriver != null ? animatorDriver.animator : null;
                var hand = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.RightHand) : null;
                return hand != null ? hand.position : transform.position + Vector3.up * 1.3f;
            }
        }

        /// <summary>Hand carrying the main claw (or weapon); used for the claw line origin.</summary>
        public Vector3 PrimaryHandPosition
        {
            get
            {
                var a = animatorDriver != null ? animatorDriver.animator : null;
                var bone = Weapon != null ? Weapon.primaryHand : HumanBodyBones.RightHand;
                if (Weapon != null && !Weapon.claws) bone = Weapon.attachBone;
                var hand = a != null && a.isHuman ? a.GetBoneTransform(bone) : null;
                return hand != null ? hand.position : HandPosition;
            }
        }

        /// <summary>Shared ground movement (Locomotion and Landing states).</summary>
        public void Locomote(float dt, float speedScale, bool allowSprint)
        {
            var cfg = Config;
            Vector3 move = MoveInputWorld();
            float mag = Mathf.Clamp01(move.magnitude);
            bool moving = mag > 0.01f;
            Vector3 dir = moving ? move / move.magnitude : Vector3.zero;

            if (cfg.surgeEnabled && allowSprint && CurrentInput.surgeTogglePressed)
            {
                if (IsSurging) IsSurging = false;
                else if (moving && Vitals.TryConsume(cfg.surgeEntryStaminaCost))
                {
                    IsSurging = true;
                    SurgeBurstRemaining = cfg.surgeBurstTime;
                }
            }
            if (!moving)
            {
                surgeStopTimer += dt;
                if (surgeStopTimer > 0.2f) IsSurging = false;
            }
            else surgeStopTimer = 0f;

            bool sprint = allowSprint && moving && SprintHeld && !IsSurging && Vitals.HasStamina;
            IsSprinting = sprint;
            if (sprint && cfg.sprintStaminaPerSecond > 0f) Vitals.Drain(cfg.sprintStaminaPerSecond * dt);

            // Claw-line momentum: he slides on with the pull's speed, braking gently, steerable.
            if (MomentumTimer > 0f && Motor.PlanarSpeed > (sprint ? cfg.sprintSpeed : cfg.runSpeed) * speedScale + 0.1f)
            {
                Vector3 cur = Motor.PlanarVelocity;
                Vector3 heading = moving ? Vector3.Slerp(cur.normalized, dir, 3f * dt) : cur.normalized;
                Motor.SetPlanarVelocity(heading * (cur.magnitude - cfg.momentumDeceleration * dt));
                Motor.FaceDirection(heading, cfg.sprintTurnSpeed, dt);
                MovementMode = "Momentum";
                return;
            }

            bool fast = sprint || IsSurging;
            bool locked = IsLocked && !fast;
            bool walk = WalkToggled || mag < cfg.walkInputThreshold;

            float speed = IsSurging ? cfg.surgeSpeed
                : sprint ? cfg.sprintSpeed
                : locked ? (walk ? cfg.lockedWalkSpeed : cfg.lockedRunSpeed)
                : (walk ? cfg.walkSpeed : cfg.runSpeed);
            speed *= speedScale;

            float accel = IsSurging && SurgeBurstRemaining > 0f ? cfg.surgeAcceleration : fast ? cfg.sprintAcceleration : cfg.acceleration;
            SurgeBurstRemaining -= dt;

            float now = Motor.PlanarSpeed;
            float braking = BrakingFor(now);
            Vector3 targetVelocity = Vector3.zero;
            if (locked)
            {
                Motor.FaceDirection(LockTargetDirection(), cfg.lockedTurnSpeed, dt);
                targetVelocity = dir * speed;
            }
            else if (moving)
            {
                // Commitment grows with speed: the faster he goes, the slower he can turn.
                float turn = TurnRateFor(now);
                Motor.FaceDirection(dir, turn, dt);
                if (now > cfg.walkSpeed * 0.5f && now <= cfg.runSpeed * 1.05f && Vector3.Angle(Motor.PlanarVelocity, dir) < cfg.pivotAngle)
                {
                    // Running: full grip. The velocity swings round with the body at the turn rate, keeping its speed.
                    Vector3 h = Vector3.RotateTowards(Motor.PlanarVelocity / now, fast ? transform.forward : dir, turn * Mathf.Deg2Rad * dt, 0f);
                    Motor.SetPlanarVelocity(h * Mathf.MoveTowards(now, speed, (speed > now ? accel : braking) * dt));
                    MovementMode = locked ? "Strafe" : walk ? "Walk" : "Run";
                    return;
                }
                if (now > cfg.runSpeed * 1.05f)
                {
                    // Momentum regime: the body turns first and the velocity follows it with less grip (drift);
                    // sliding sideways bleeds speed.
                    Vector3 heading = Motor.PlanarVelocity / now;
                    heading = Vector3.RotateTowards(heading, transform.forward, turn * cfg.driftGrip * Mathf.Deg2Rad * dt, 0f);
                    float slip = Vector3.Angle(heading, transform.forward) * Mathf.Deg2Rad;
                    float s = Mathf.MoveTowards(now, speed, (speed > now ? accel : braking) * dt);
                    s = Mathf.Max(0f, s - cfg.driftDrag * Mathf.Sin(slip) * dt);
                    Motor.SetPlanarVelocity(heading * s);
                    MovementMode = IsSurging ? "Surge Sprint" : sprint ? "Sprint" : "Run (momentum)";
                    if (slip > 12f * Mathf.Deg2Rad) MovementMode += " (drift)";
                    return;
                }
                targetVelocity = fast ? transform.forward * speed : dir * speed;
            }
            Motor.Accelerate(targetVelocity, accel, braking, dt);

            MovementMode = IsSurging ? "Surge Sprint"
                : sprint ? "Sprint"
                : !moving ? (Motor.PlanarSpeed > 0.2f ? "Decelerating" : "Idle")
                : locked ? (walk ? "Strafe Walk" : "Strafe Run")
                : (walk ? "Walk" : "Run");
        }

        /// <summary>Turn rate (deg/s) at a planar speed: turnSpeed at walk falling to minTurnSpeed at surge.</summary>
        public float TurnRateFor(float speed)
        {
            var c = Config;
            float s01 = Mathf.InverseLerp(c.walkSpeed, c.surgeSpeed, speed);
            return Mathf.Lerp(c.turnSpeed, Mathf.Min(c.turnSpeed, c.minTurnSpeed), Mathf.Pow(s01, c.turnFalloff));
        }

        /// <summary>Braking (m/s^2) at a planar speed: 'deceleration' up to run, easing to the sprint / surge values.</summary>
        public float BrakingFor(float speed)
        {
            var c = Config;
            if (speed <= c.runSpeed) return c.deceleration;
            if (speed <= c.sprintSpeed) return Mathf.Lerp(c.deceleration, c.sprintDeceleration, Mathf.InverseLerp(c.runSpeed, c.sprintSpeed, speed));
            return Mathf.Lerp(c.sprintDeceleration, c.surgeDeceleration, Mathf.InverseLerp(c.sprintSpeed, c.surgeSpeed, speed));
        }

        /// <summary>Start whatever the buffered input asks for, if allowed. Returns true if the state changed.</summary>
        public bool TryBufferedGroundAction()
        {
            switch (PeekBuffer())
            {
                case BufferedAction.Jump: return TryJump();
                case BufferedAction.SuperJump: return TrySuperJump();
                case BufferedAction.Dodge: return TryDodge();
                case BufferedAction.Skill: return TrySkill();
                case BufferedAction.Light:
                    if ((IsSprinting || IsSurging) && Motor.PlanarSpeed > Config.runSpeed * 0.9f && Weapon.sprintAttack != null && !FollowUpReady)
                        return TryAttack(Weapon.sprintAttack, AttackKind.Sprint, 0);
                    return TryLight(0);
                case BufferedAction.Heavy: return TryHeavy(0);
                case BufferedAction.Flask: return TryFlask();
                case BufferedAction.Ultimate: return TryUltimate();
            }
            return false;
        }

        public bool TryFlask()
        {
            ConsumeBuffer();
            if (FlaskCharges <= 0)
            {
                Notification?.Invoke("No flasks left");
                return false;
            }
            ChangeState(new DrinkState(this));
            return true;
        }

        /// <summary>Called by DrinkState at the heal point.</summary>
        public void ApplyFlask()
        {
            if (FlaskCharges <= 0) return;
            FlaskCharges--;
            Vitals.Heal(Vitals.MaxHealth * Config.flaskHealFraction);
        }

        public bool TryUltimate()
        {
            ConsumeBuffer();
            if (!UltimateReady)
            {
                if (Profile.ultimate != null) Notification?.Invoke("Ultimate Art not ready");
                return false;
            }
            UltimateGauge = 0f;
            ChangeState(new AbilityState(this, Profile.ultimate, Profile.ultimate.CreateInstance(this)));
            return true;
        }

        public void AddUltimateGauge(float damage)
        {
            if (Profile == null || Profile.ultimate == null || Config.ultimateGaugeDamage <= 0f) return;
            bool was = UltimateReady;
            UltimateGauge = Mathf.Clamp01(UltimateGauge + damage / Config.ultimateGaugeDamage);
            if (!was && UltimateReady) Notification?.Invoke("Ultimate Art ready");
        }

        /// <summary>
        /// Nightreign-style ledge mantle: looks for a wall in the move/facing direction with a walkable top
        /// within reach. Returns true and enters MantleState if one is found.
        /// </summary>
        public bool TryMantle(bool fromGround)
        {
            var cfg = Config;
            Vector3 dir = MoveInputWorld();
            if (dir.sqrMagnitude < 0.02f) dir = transform.forward;
            dir.y = 0f;
            dir.Normalize();
            var cc = Motor.Controller;
            Vector3 feet = transform.position;
            Vector3 chest = feet + Vector3.up * Mathf.Min(1.0f, cc.height * 0.55f);
            if (!Physics.SphereCast(chest, cc.radius * 0.8f, dir, out var wall, cfg.mantleReach, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (wall.collider.transform.IsChildOf(transform) || wall.collider.GetComponentInParent<IDamageable>() != null) return false;
            if (Mathf.Abs(wall.normal.y) > 0.5f) return false;
            float maxH = fromGround ? cfg.mantleGroundMaxHeight : cfg.mantleMaxHeight;
            Vector3 probe = wall.point - wall.normal * (cc.radius + 0.15f);
            probe.y = feet.y + maxH + 0.3f;
            if (!Physics.SphereCast(probe, cc.radius * 0.6f, Vector3.down, out var top, maxH + 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (top.normal.y < 0.7f) return false;
            float height = top.point.y - feet.y;
            if (height < cfg.mantleMinHeight || height > maxH) return false;
            Vector3 dest = new Vector3(probe.x, top.point.y + 0.02f, probe.z);
            Vector3 p0 = dest + Vector3.up * (cc.radius + 0.05f), p1 = dest + Vector3.up * (cc.height - cc.radius);
            if (Physics.CheckCapsule(p0, p1, cc.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore)) return false;
            ConsumeBuffer();
            ChangeState(new MantleState(this, dest, height, wall.point, wall.normal, top.point.y));
            return true;
        }

        /// <summary>Spiritstream-style launch: rocket upward, steer in the air, land without damage.</summary>
        public void LaunchSpiritSpring(float height)
        {
            Motor.Jump(height);
            ChangeState(new AirborneState(this, true, true));
            Notification?.Invoke("Spiritstream");
        }

        public bool TryDodge()
        {
            if (Config.evadeStyle == EvadeStyle.FlashStep) return TryFlashStep();
            Vector3 dir = MoveInputWorld();
            bool roll = dir.sqrMagnitude > 0.02f;
            if (!Vitals.TryConsume(roll ? Config.rollStaminaCost : Config.backstepStaminaCost)) return false;
            ConsumeBuffer();
            ChangeState(new DodgeState(this, roll, roll ? dir.normalized : -transform.forward));
            return true;
        }

        /// <summary>
        /// Bloodhound's-Step-style evade: a near-instant displacement in the input direction (camera-relative).
        /// With no input it steps back: away from the lock-on target, or opposite the character's facing.
        /// </summary>
        public bool TryFlashStep()
        {
            Vector3 dir = MoveInputWorld();
            if (dir.sqrMagnitude < 0.02f)
            {
                Vector3 away = IsLocked ? -LockTargetDirection() : -transform.forward;
                away.y = 0f;
                dir = away.sqrMagnitude > 1e-4f ? away : -transform.forward;
            }
            if (!Vitals.TryConsume(Config.stepStaminaCost)) return false;
            ConsumeBuffer();
            ChangeState(new FlashStepState(this, dir.normalized));
            return true;
        }

        public bool TryJump()
        {
            if (!(Motor.Grounded || Motor.TimeSinceGrounded <= Config.coyoteTime)) return false;
            if (ActiveSpring != null && Motor.Grounded)
            {
                ConsumeBuffer();
                LaunchSpiritSpring(ActiveSpring.launchHeight);
                return true;
            }
            Vector3 move = MoveInputWorld();
            if (move.sqrMagnitude > 0.02f && TryMantle(true)) return true;
            if (WantsSideJump(move)) return TrySideJump(move);
            float cost = Config.jumpStaminaCost + (IsSurging ? Config.surgeJumpExtraStamina : 0f);
            if (!Vitals.TryConsume(cost)) return false;
            ConsumeBuffer();
            bool momentum = MomentumTimer > 0f;
            if (Config.jumpSquatTime > 0f && Motor.Grounded)
            {
                ChangeState(new JumpSquatState(this, momentum));   // brief crouch, then the jump
                return true;
            }
            LaunchJump(momentum);
            return true;
        }

        /// <summary>Leave the ground now (normal jump, or a momentum jump while carrying claw-line speed).</summary>
        public void LaunchJump(bool momentum)
        {
            if (momentum) LaunchMomentumJump();
            else Motor.Jump(Config.jumpHeight);
            ChangeState(new AirborneState(this, true));
        }

        /// <summary>Jump that keeps (and boosts) the claw line's momentum: Wylder's hook-and-leap.</summary>
        void LaunchMomentumJump()
        {
            Motor.SetPlanarVelocity(Motor.PlanarVelocity * Config.momentumJumpBoost);
            Motor.Jump(Config.momentumJumpHeight);
            MomentumTimer = 0f;
        }

        /// <summary>Jump cancel out of the claw line pull (momentum is already applied by the ability).</summary>
        public void MomentumJump()
        {
            ConsumeBuffer();
            Vitals.TryConsume(Config.jumpStaminaCost);
            LaunchMomentumJump();
            ChangeState(new AirborneState(this, true));
        }

        /// <summary>Carry a velocity out of an ability (claw line pull) for a while.</summary>
        public void CarryMomentum(Vector3 velocity, float time)
        {
            Motor.SetPlanarVelocity(new Vector3(velocity.x, 0f, velocity.z));
            if (velocity.y > 0f) Motor.SetVerticalVelocity(velocity.y);
            MomentumTimer = time;
        }

        /// <summary>Side jumps happen in combat: locked on, or out of an attack / flash step, with sideways input.</summary>
        bool WantsSideJump(Vector3 move)
        {
            if (move.sqrMagnitude < 0.2f) return false;
            if (!(IsLocked || State is AttackState || State is FlashStepState)) return false;
            Vector3 local = transform.InverseTransformDirection(move);
            return Mathf.Abs(local.x) > Mathf.Abs(local.z) * 1.2f;
        }

        public bool TrySideJump(Vector3 move)
        {
            if (!Vitals.TryConsume(Config.sideJumpStaminaCost)) return false;
            ConsumeBuffer();
            Vector3 local = transform.InverseTransformDirection(move);
            Vector3 dir = transform.right * Mathf.Sign(local.x);
            ChangeState(new SideJumpState(this, dir, local.x < 0f));
            return true;
        }

        /// <summary>Spider-Man style: hold Jump to crouch and charge, release to spring.</summary>
        public bool TrySuperJump()
        {
            ConsumeBuffer();
            if (!Motor.Grounded || !jumpDown) return false;
            if (ActiveSpring != null) return TryJump();
            // Surge sprinting: no crouch and no slowdown, he keeps running while it charges.
            ChangeState(new SuperJumpChargeState(this, (IsSurging || IsSprinting) && Motor.PlanarSpeed > Config.runSpeed * 1.05f));
            return true;
        }

        public void FaceDirectionInstant(Vector3 dir) => Motor.FaceDirection(dir, 0f, 0f);

        public bool TryAttack(AttackData attack, AttackKind kind, int chainIndex)
        {
            if (attack == null || !Vitals.TryConsume(attack.staminaCost)) return false;
            ConsumeBuffer();
            ChangeState(new AttackState(this, attack, kind, chainIndex));
            return true;
        }

        public bool TryLight(int index)
        {
            if (FollowUpReady)
            {
                var f = followUpAttack;
                followUpAttack = null;
                return TryAttack(f, AttackKind.FollowUp, 0);
            }
            var chain = Weapon != null ? Weapon.lightChain : null;
            if (chain == null || chain.Count == 0) return false;
            int i = index % chain.Count;
            return TryAttack(chain[i], AttackKind.Light, i);
        }

        public bool TryHeavy(int index)
        {
            var chain = Weapon != null ? Weapon.heavyChain : null;
            if (chain == null || chain.Count == 0) return false;
            int i = index % chain.Count;
            return TryAttack(chain[i], AttackKind.Heavy, i);
        }

        public bool TrySkill()
        {
            var skill = Profile.skill;
            if (skill == null || SkillCooldownRemaining > 0f) { ConsumeBuffer(); return false; }
            if (!Vitals.TryConsume(skill.staminaCost)) return false;
            ConsumeBuffer();
            SkillCooldownRemaining = skill.cooldown;
            ChangeState(new AbilityState(this, skill, skill.CreateInstance(this)));
            return true;
        }

        public void OpenFollowUpWindow(AttackData attack, float window)
        {
            followUpAttack = attack;
            followUpUntil = Time.time + window;
        }

        public void ChangeState(NightfarerState next)
        {
            State?.Exit();
            State = next;
            if (next is AttackState || next is AbilityState || next is HitReactState) LastCombatTime = Time.time;
            StateLog.Add(next.Name);
            if (StateLog.Count > 256) StateLog.RemoveAt(0);
            next.Enter();
            StateChanged?.Invoke(next.Name);
        }

        // ---------------------------------------------------------------- animation

        public float SpeedTier(float s)
        {
            var c = Config;
            if (s <= c.walkSpeed) return s / c.walkSpeed;
            if (s <= c.runSpeed) return 1f + (s - c.walkSpeed) / (c.runSpeed - c.walkSpeed);
            if (s <= c.sprintSpeed) return 2f + (s - c.runSpeed) / (c.sprintSpeed - c.runSpeed);
            return 3f + Mathf.Clamp01((s - c.sprintSpeed) / Mathf.Max(0.01f, c.surgeSpeed - c.sprintSpeed));
        }

        void UpdateLocomotionAnimation(float dt)
        {
            Vector3 v = Motor.PlanarVelocity;
            float speed = v.magnitude;
            Vector2 blend = Vector2.zero;
            if (speed > 0.05f)
            {
                Vector3 local = transform.InverseTransformDirection(v) / speed;
                blend = new Vector2(local.x, local.z) * SpeedTier(speed);
            }
            animatorDriver.SetLocomotion(blend, Motor.Grounded, dt);
        }

        // ---------------------------------------------------------------- profiles, sets, weapons

        public void ApplyProfile(int index)
        {
            if (profiles == null || profiles.Length == 0) return;
            ProfileIndex = (index % profiles.Length + profiles.Length) % profiles.Length;
            Profile = profiles[ProfileIndex];
            Motor.Config = Profile.config;
            Vitals.Configure(Profile.config);
            FlaskCharges = Profile.config.flaskCharges;
            weaponIndex = 0;
            EquipWeapon(Profile.weapons != null && Profile.weapons.Length > 0 ? Profile.weapons[0] : null);
            if (Profile.animationSet != null)
            {
                int i = animationSets != null ? Array.IndexOf(animationSets, Profile.animationSet) : -1;
                if (i >= 0) SetAnimationSet(i);
                else animatorDriver.ApplySet(Profile.animationSet);
            }
        }

        public void CycleProfile() => ApplyProfile(ProfileIndex + 1);

        public void SetAnimationSet(int index)
        {
            if (animationSets == null || animationSets.Length == 0) return;
            AnimationSetIndex = (index % animationSets.Length + animationSets.Length) % animationSets.Length;
            animatorDriver.ApplySet(animationSets[AnimationSetIndex]);
        }

        public void CycleAnimationSet() => SetAnimationSet(AnimationSetIndex + 1);

        public void CycleWeapon()
        {
            if (!(State is LocomotionState) || Profile.weapons == null || Profile.weapons.Length < 2) return;
            weaponIndex = (weaponIndex + 1) % Profile.weapons.Length;
            EquipWeapon(Profile.weapons[weaponIndex]);
        }

        public void EquipWeapon(WeaponData weapon)
        {
            if (weaponModel != null) Destroy(weaponModel);
            Weapon = weapon;
            if (weapon == null) return;
            if (weapon.claws)
            {
                ConfigureClaws(weapon);
                return;
            }
            weaponModel = WeaponMount.Attach(animatorDriver.animator, weapon, out Transform bone);
            Transform blade = weaponModel != null ? weaponModel.transform : bone != null ? bone : transform;
            if (weaponModel != null)
                foreach (var col in weaponModel.GetComponentsInChildren<Collider>()) Destroy(col);
            if (hitbox != null) hitbox.Configure(blade, weapon);
        }

        /// <summary>Natural weapons: hit segments run along each hand's fingers (measured from the rig).</summary>
        void ConfigureClaws(WeaponData weapon)
        {
            var anim = animatorDriver.animator;
            if (hitbox == null || anim == null || !anim.isHuman) return;
            var primary = weapon.primaryHand;
            var off = primary == HumanBodyBones.LeftHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            Vector3 Finger(HumanBodyBones hand) =>
                WeaponMount.TryGetGrip(anim, hand, out _, out Quaternion rot) ? rot * Vector3.forward : Vector3.up;
            hitbox.ConfigureClaws(anim.GetBoneTransform(primary), Finger(primary), weapon.primaryClawLength, weapon.primaryClawRadius,
                anim.GetBoneTransform(off), Finger(off), weapon.offClawLength, weapon.offClawRadius);
        }

        public void ResetToSpawn()
        {
            Motor.Warp(spawnPosition, spawnRotation);
            Vitals.RefillAll();
            FlaskCharges = Config.flaskCharges;
            lockOn?.Release();
            ChangeState(new LocomotionState(this));
            if (cameraRig != null) cameraRig.SnapBehind();
        }

        // ---------------------------------------------------------------- damage

        public bool ReceiveDamage(DamageInfo info)
        {
            if (IsInvulnerable)
            {
                DodgedHits++;
                Dodged?.Invoke();
                return false;
            }
            TakenHits++;
            Vitals.TakeDamage(info.amount);
            Damaged?.Invoke(info);
            if (Vitals.Health <= 0f) Vitals.RefillAll();
            if (!HasHyperArmor) ChangeState(new HitReactState(this, info.direction));
            return true;
        }

        void OnWeaponHit(IDamageable target, DamageInfo info)
        {
            HitsDealt++;
            LastHitDealt = info.amount;
            animatorDriver.HitStop(CurrentAttack != null ? CurrentAttack.hitStopTime : 0.05f);
            if (cameraRig != null) cameraRig.Shake(Mathf.Clamp(info.amount / 900f, 0.03f, 0.18f), 0.15f);
            AddUltimateGauge(info.amount);
            HitLanded?.Invoke(target, info);
        }
    }
}
