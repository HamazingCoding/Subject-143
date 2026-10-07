using UnityEngine;

namespace Subject143.Nightfarer
{
    public abstract class NightfarerState
    {
        protected readonly NightfarerCharacter C;
        public float Elapsed { get; protected set; }

        protected NightfarerState(NightfarerCharacter character) { C = character; }

        protected NightfarerConfig Cfg => C.Config;
        public abstract string Name { get; }
        public virtual bool BlocksStaminaRegen => false;

        public virtual void Enter() { }
        public virtual void Tick(float dt) { Elapsed += dt; }
        public virtual void Exit() { }
        public virtual void OnLanded(float fallHeight, float impactSpeed) { }
        public virtual void OnAnimationEvent(string eventName) { }
    }

    // ------------------------------------------------------------------ Locomotion

    public class LocomotionState : NightfarerState
    {
        public LocomotionState(NightfarerCharacter c) : base(c) { }
        public override string Name => "Locomotion";

        public override void Enter()
        {
            C.Animator.ReturnToLocomotion();
            C.CombatPhase = "-";
            C.ComboStep = 0;
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            C.Locomote(dt, 1f, true);
            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > Cfg.coyoteTime)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }
            C.TryBufferedGroundAction();
        }

        public override void Exit() => C.IsSprinting = false;
    }

    // ------------------------------------------------------------------ Air

    public class AirborneState : NightfarerState
    {
        readonly bool jumped;
        readonly bool spirit;
        readonly bool super;
        float entrySpeed;
        bool attacked;

        public AirborneState(NightfarerCharacter c, bool jumped, bool spiritSpring = false, bool superJump = false) : base(c)
        {
            this.jumped = jumped;
            spirit = spiritSpring;
            super = superJump;
        }

        public bool IsSuperJump => super;

        public bool IsSpiritLaunch => spirit;
        public override string Name => spirit ? "Spiritstream" : jumped ? "Jump" : "Fall";

        public override void Enter()
        {
            entrySpeed = C.Motor.PlanarSpeed;
            C.IsSprinting = false;
            C.CombatPhase = "-";
            float rise = Mathf.Max(0.45f, C.Motor.VerticalVelocity / Mathf.Max(1f, -Cfg.gravity));
            if (jumped) C.Animator.PlayAction("JumpStart", super ? rise : 0.45f, 0.06f);
            else C.Animator.PlayLoop("Fall", 0.2f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (jumped && C.Motor.VerticalVelocity < 1f && Elapsed > 0.4f && C.Animator.CurrentState == "JumpStart") C.Animator.PlayLoop("Fall", 0.25f);

            // Claw line in the air (Wylder's hook works mid-jump too).
            if (C.PeekBuffer() == NightfarerCharacter.BufferedAction.Skill && C.TrySkill()) return;

            Vector3 move = C.MoveInputWorld();
            Vector3 v = C.Motor.PlanarVelocity + move * (Cfg.airAcceleration * (spirit ? 3f : 1f) * dt);
            float cap = Mathf.Max(entrySpeed, Cfg.runSpeed * (spirit ? 1.2f : Cfg.airMinSpeedFraction));
            if (v.magnitude > cap) v = v.normalized * cap;
            C.Motor.SetPlanarVelocity(v);
            if (move.sqrMagnitude > 0.02f) C.Motor.FaceDirection(move, Cfg.airTurnSpeed, dt);

            // Grab ledges in front while rising slowly, at the apex or falling (Nightreign climbing).
            if (Elapsed > 0.08f && C.Motor.VerticalVelocity < 3.5f && move.sqrMagnitude > 0.02f && C.TryMantle(false)) return;

            if (!attacked && C.PeekBuffer() == NightfarerCharacter.BufferedAction.Light && C.Weapon != null && C.Weapon.jumpAttack != null)
            {
                attacked = true;
                C.TryAttack(C.Weapon.jumpAttack, AttackKind.Jump, 0);
            }
        }

        public override void OnLanded(float fallHeight, float impactSpeed) => Land(C, fallHeight);

        /// <summary>Shared landing choice: hero landing for big drops, roll if disabled, else a soft landing.</summary>
        public static void Land(NightfarerCharacter c, float fallHeight)
        {
            var cfg = c.Config;
            if (cfg.heroLanding && fallHeight >= cfg.heroLandMinHeight) c.ChangeState(new HeroLandingState(c, fallHeight));
            else if (!cfg.heroLanding && fallHeight >= cfg.hardLandFallHeight) c.ChangeState(new LandRollState(c));
            else c.ChangeState(new LandingState(c, fallHeight));
        }
    }

    public class LandingState : NightfarerState
    {
        readonly float fallHeight;

        public LandingState(NightfarerCharacter c, float fallHeight = 0f) : base(c) { this.fallHeight = fallHeight; }
        public override string Name => "Landing";

        public override void Enter()
        {
            C.Animator.PlayAction("Land", Cfg.softLandRecovery + 0.25f, 0.05f);
            if (fallHeight >= Cfg.dustLandMinHeight)
            {
                float k = Mathf.InverseLerp(Cfg.dustLandMinHeight, Cfg.heroLandMinHeight, fallHeight);
                GroundImpact.Spawn(C.transform.position, 0.12f + 0.15f * k, false);
                if (C.cameraRig != null) C.cameraRig.Shake(0.025f + 0.03f * k, 0.18f);
            }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            C.Locomote(dt, 0.6f, true);
            if (Elapsed >= 0.04f && C.TryBufferedGroundAction()) return;
            bool moving = C.MoveInputWorld().sqrMagnitude > 0.01f;
            if (Elapsed >= (moving ? Cfg.softLandRecovery : Cfg.softLandRecovery + 0.2f))
                C.ChangeState(new LocomotionState(C));
        }
    }

    /// <summary>Nightreign: high falls end in a roll instead of fall damage.</summary>
    public class LandRollState : NightfarerState
    {
        Vector3 dir;
        float last;

        public LandRollState(NightfarerCharacter c) : base(c) { }
        public override string Name => "Land Roll";
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            Vector3 v = C.Motor.PlanarVelocity;
            Vector3 move = C.MoveInputWorld();
            dir = move.sqrMagnitude > 0.02f ? move.normalized : v.sqrMagnitude > 0.25f ? v.normalized : C.transform.forward;
            C.Motor.FaceDirection(dir, 0f, 0f);
            C.Motor.SetPlanarVelocity(Vector3.zero);
            C.Animator.PlayAction("LandRoll", Cfg.landRollDuration, 0.04f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float u = Mathf.Clamp01(Elapsed / Cfg.landRollDuration);
            float p = Cfg.rollCurve.Evaluate(u) * Cfg.landRollDistance;
            C.Motor.AddDisplacement(dir * (p - last));
            last = p;
            C.IsInvulnerable = u < 0.6f;
            C.CombatPhase = C.IsInvulnerable ? "I-frames" : "Recovery";
            if (Elapsed >= Cfg.landRollDuration)
            {
                if (C.MoveInputWorld().sqrMagnitude > 0.01f) C.Motor.SetPlanarVelocity(dir * Cfg.runSpeed * 0.7f);
                C.ChangeState(new LocomotionState(C));
            }
        }

        public override void Exit() => C.IsInvulnerable = false;
    }

    // ------------------------------------------------------------------ Flask

    /// <summary>Drink a flask while still able to walk slowly; heals at the heal point.</summary>
    public class DrinkState : NightfarerState
    {
        bool healed;

        public DrinkState(NightfarerCharacter c) : base(c) { }
        public override string Name => "Flask";

        public override void Enter()
        {
            C.IsSurging = false;
            C.CombatPhase = "Drinking";
            C.Animator.PlayAction("Drink", Cfg.flaskDuration, 0.08f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            C.Locomote(dt, Cfg.flaskMoveSpeedScale, false);
            if (!healed && Elapsed >= Cfg.flaskHealTime)
            {
                healed = true;
                C.ApplyFlask();
            }
            if (healed && C.PeekBuffer() == NightfarerCharacter.BufferedAction.Dodge && C.TryDodge()) return;
            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > Cfg.coyoteTime)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }
            if (Elapsed >= Cfg.flaskDuration) C.ChangeState(new LocomotionState(C));
        }
    }

    // ------------------------------------------------------------------ Dodge

    public class DodgeState : NightfarerState
    {
        readonly bool roll;
        readonly Vector3 dir;
        readonly float duration, distance, iStart, iEnd, cancelTime, moveCancelTime;
        readonly Vector2 attackWindow;
        float last;
        bool locked;

        public DodgeState(NightfarerCharacter c, bool roll, Vector3 dir) : base(c)
        {
            this.roll = roll;
            this.dir = dir;
            var cfg = c.Config;
            duration = roll ? cfg.rollDuration : cfg.backstepDuration;
            distance = roll ? cfg.rollDistance : cfg.backstepDistance;
            iStart = roll ? cfg.rollIFrameStart : cfg.backstepIFrameStart;
            iEnd = roll ? cfg.rollIFrameEnd : cfg.backstepIFrameEnd;
            cancelTime = roll ? cfg.rollCancelTime : cfg.backstepCancelTime;
            moveCancelTime = roll ? cfg.rollMoveCancelTime : cfg.backstepDuration * 0.9f;
            attackWindow = roll ? cfg.rollAttackWindow : cfg.backstepAttackWindow;
        }

        public bool IsRoll => roll;
        public Vector3 Direction => dir;
        public override string Name => roll ? "Roll" : "Backstep";
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            locked = C.IsLocked;
            C.Motor.SetPlanarVelocity(Vector3.zero);
            if (roll)
            {
                if (!locked)
                {
                    C.Motor.FaceDirection(dir, 0f, 0f);
                    C.Animator.SetVisualYaw(0f, true);
                }
                else C.Animator.SetVisualYaw(Vector3.SignedAngle(C.transform.forward, dir, Vector3.up), true);
            }
            C.Animator.PlayAction(roll ? "Roll" : "Backstep", duration, 0.05f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float t = Elapsed;
            float p = Cfg.rollCurve.Evaluate(Mathf.Clamp01(t / duration)) * distance;
            C.Motor.AddDisplacement(dir * (p - last));
            last = p;

            if (locked && C.IsLocked)
            {
                C.Motor.FaceDirection(C.LockTargetDirection(), Cfg.lockedTurnSpeed, dt);
                if (roll) C.Animator.SetVisualYaw(Vector3.SignedAngle(C.transform.forward, dir, Vector3.up));
            }

            C.IsInvulnerable = t >= iStart && t <= iEnd;
            C.CombatPhase = C.IsInvulnerable ? "I-frames" : t < iStart ? "Startup" : "Recovery";

            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > 0.15f)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }

            if (t >= cancelTime)
            {
                var b = C.PeekBuffer();
                if (b == NightfarerCharacter.BufferedAction.Light)
                {
                    var follow = roll ? C.Weapon?.rollAttack : C.Weapon?.backstepAttack;
                    bool inWindow = t >= attackWindow.x && t <= attackWindow.y;
                    if (inWindow && follow != null ? C.TryAttack(follow, roll ? AttackKind.Roll : AttackKind.Backstep, 0) : C.TryLight(0)) return;
                }
                else if (b != NightfarerCharacter.BufferedAction.None && C.TryBufferedGroundAction()) return;
            }

            if (t >= moveCancelTime && C.MoveInputWorld().sqrMagnitude > 0.04f)
            {
                C.Motor.SetPlanarVelocity(C.MoveInputWorld().normalized * Cfg.runSpeed * 0.5f);
                C.ChangeState(new LocomotionState(C));
                return;
            }
            if (t >= duration) C.ChangeState(new LocomotionState(C));
        }

        public override void Exit()
        {
            C.IsInvulnerable = false;
            C.Animator.SetVisualYaw(0f);
        }
    }

    // ------------------------------------------------------------------ Flash step

    /// <summary>
    /// Subject 143's evade (Bloodhound's Step feel): the body flashes to a point a few metres away in a fraction
    /// of a second (hidden while travelling; afterimages and mist show the path), invulnerable through the
    /// travel, then a short recovery. Steps can be chained, and a light attack out of a step becomes the step attack.
    /// Locked on, the character keeps facing the target, so steps become true sidesteps/backsteps; unlocked, only
    /// forward-ish steps turn the body.
    /// </summary>
    public class FlashStepState : NightfarerState
    {
        readonly Vector3 dir;
        string slot = "StepB";
        float last;
        bool locked, arrived;
        FlashStepVFX vfx;

        public FlashStepState(NightfarerCharacter c, Vector3 direction) : base(c)
        {
            direction.y = 0f;
            dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : -c.transform.forward;
        }

        public Vector3 Direction => dir;
        /// <summary>F, B, L or R relative to the character when the step started.</summary>
        public string DirectionLabel => slot.Substring(4);
        public override string Name => "Flash Step";
        public override bool BlocksStaminaRegen => true;

        float Travel => Cfg.stepTravelTime;
        float Total => Cfg.stepTravelTime + Cfg.stepRecovery;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            locked = C.IsLocked;
            C.Motor.SetPlanarVelocity(Vector3.zero);
            Vector3 local = C.transform.InverseTransformDirection(dir);
            if (!locked && local.z > 0.5f)
            {
                C.Motor.FaceDirection(dir, 0f, 0f);   // unlocked forward dash: turn into it
                local = Vector3.forward;
            }
            slot = Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? (local.z >= 0f ? "StepF" : "StepB") : (local.x >= 0f ? "StepR" : "StepL");
            C.Animator.PlayAction(slot, Total, 0.03f);
            vfx = C.GetComponent<FlashStepVFX>();
            vfx?.Begin(C.transform.position);
            C.CombatPhase = "Flash";
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float t = Elapsed;
            float u = Mathf.Clamp01(t / Travel);
            float p = Cfg.stepCurve.Evaluate(u) * Cfg.stepDistance;
            C.Motor.AddDisplacement(dir * (p - last));
            last = p;

            if (locked && C.IsLocked) C.Motor.FaceDirection(C.LockTargetDirection(), 1440f, dt);
            C.IsInvulnerable = t <= Cfg.stepIFrameEnd;
            if (vfx != null)
            {
                vfx.SetHidden(Cfg.stepHideBody && t > 0.012f && t < Travel - 0.01f);
                if (t < Travel) vfx.Travel();
            }
            if (!arrived && t >= Travel)
            {
                arrived = true;
                vfx?.Arrive(C.transform.position);
            }
            C.CombatPhase = C.IsInvulnerable ? "Flash (evading)" : "Recovery";

            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > 0.2f && t > Travel)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }

            if (t >= Cfg.stepChainTime)
            {
                var b = C.PeekBuffer();
                if (b == NightfarerCharacter.BufferedAction.Light)
                {
                    bool inWindow = t >= Cfg.stepAttackWindow.x && t <= Cfg.stepAttackWindow.y;
                    var follow = C.Weapon != null ? C.Weapon.rollAttack : null;
                    if (inWindow && follow != null ? C.TryAttack(follow, AttackKind.Roll, 0) : C.TryLight(0)) return;
                }
                else if (b != NightfarerCharacter.BufferedAction.None && C.TryBufferedGroundAction()) return;
            }

            if (t >= Travel + Cfg.stepRecovery * 0.5f && C.MoveInputWorld().sqrMagnitude > 0.04f)
            {
                C.Motor.SetPlanarVelocity(C.MoveInputWorld().normalized * Cfg.runSpeed * 0.7f);
                C.ChangeState(new LocomotionState(C));
                return;
            }
            if (t >= Total) C.ChangeState(new LocomotionState(C));
        }

        public override void Exit()
        {
            C.IsInvulnerable = false;
            vfx?.SetHidden(false);
        }
    }

    // ------------------------------------------------------------------ Attack

    public class AttackState : NightfarerState
    {
        enum Queued { None, Light, Heavy }

        readonly AttackData a;
        readonly AttackKind kind;
        readonly int index;
        float t;
        bool hitOpen, hitDone;
        int window;
        float lastLunge;
        float chargeTimer, chargeRatio;
        bool charging, chargeDone, holdingAir;
        Queued queued;

        public AttackState(NightfarerCharacter c, AttackData attack, AttackKind kind, int chainIndex) : base(c)
        {
            a = attack;
            this.kind = kind;
            index = chainIndex;
        }

        public AttackData Data => a;
        public AttackKind Kind => kind;
        public int ChainIndex => index;
        public float AttackTime => t;
        public float ChargeRatio => chargeRatio;
        public override string Name => a.name;
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            C.CurrentAttack = a;
            C.ComboStep = index + 1;
            C.HasHyperArmor = a.hyperArmor;
            C.Motor.SetPlanarVelocity(C.Motor.PlanarVelocity * a.carryMomentum);
            C.Animator.PlayAction(a.animationSlot, a.duration, 0.06f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float adt = C.Animator.IsHitStopped ? 0f : dt;

            // Charge: while the heavy button is held, the wind-up keeps going, very slowly, from the hold point toward
            // the end of the wind-up (Elden Ring style), building power; releasing continues at full speed.
            if (a.chargeTimeMax > 0f && !chargeDone && t >= a.chargeHoldPoint)
            {
                if (C.CurrentInput.heavyHeld && chargeTimer < a.chargeTimeMax)
                {
                    float holdEnd = a.ChargeHoldEnd;
                    if (!charging)
                    {
                        charging = true;
                        C.Animator.SetActionSpeedMultiplier(Mathf.Max(0.02f, (holdEnd - a.chargeHoldPoint) / a.chargeTimeMax));
                    }
                    chargeTimer += dt;
                    chargeRatio = Mathf.Clamp01(chargeTimer / a.chargeTimeMax);
                    adt = Mathf.Max(0f, Mathf.Lerp(a.chargeHoldPoint, holdEnd, chargeRatio) - t);
                }
                else
                {
                    chargeDone = true;
                    if (charging) C.Animator.SetActionSpeedMultiplier(1f);
                    charging = false;
                }
            }

            // Jump attacks hold the strike pose until landing.
            if (a.holdUntilGrounded && !C.Motor.Grounded && t >= a.hitEnd - 0.02f)
            {
                if (!holdingAir)
                {
                    holdingAir = true;
                    C.Animator.SetActionSpeedMultiplier(0f);
                }
                adt = 0f;
            }
            else if (holdingAir)
            {
                holdingAir = false;
                C.Animator.SetActionSpeedMultiplier(1f);
            }

            t += adt;

            if (t <= a.trackingEndTime || charging)
            {
                Vector3 aim = C.IsLocked ? C.LockTargetDirection() : C.MoveInputWorld();
                if (aim.sqrMagnitude > 0.02f) C.Motor.FaceDirection(aim, a.trackingSpeed, dt);
            }

            if (a.lungeDistance > 0f && t > a.lungeStart)
            {
                float u = Mathf.Clamp01((t - a.lungeStart) / Mathf.Max(0.01f, a.lungeEnd - a.lungeStart));
                float p = Mathf.SmoothStep(0f, 1f, u) * a.lungeDistance;
                float d = p - lastLunge;
                lastLunge = p;
                if (C.IsLocked && C.DistanceToLockTarget() < 1.3f) d = 0f;
                if (d > 0f) C.Motor.AddDisplacement(C.transform.forward * d);
            }

            if (C.Motor.Grounded) C.Motor.Accelerate(Vector3.zero, 0f, Cfg.attackFriction, dt);

            if (!a.useAnimationEvents)
            {
                if (!hitOpen && window < a.HitWindowCount && t >= a.HitWindow(window).x) OpenHit();
                if (hitOpen && t >= a.HitWindow(window).y)
                {
                    CloseHit();
                    window++;
                }
            }

            C.CombatPhase = charging ? $"Charging {chargeRatio:P0}" : t < a.hitStart ? "Windup" : hitOpen || a.InHitWindow(t) ? "Active" : "Recovery";

            // Queue the next attack from the combo window; release it at chainTime.
            if (t >= a.comboWindowStart && queued == Queued.None)
            {
                var b = C.PeekBuffer();
                if (b == NightfarerCharacter.BufferedAction.Light) { queued = Queued.Light; C.ConsumeBuffer(); }
                else if (b == NightfarerCharacter.BufferedAction.Heavy) { queued = Queued.Heavy; C.ConsumeBuffer(); }
            }
            if (queued != Queued.None && t >= a.chainTime && C.Motor.Grounded)
            {
                bool started = queued == Queued.Light
                    ? C.TryLight(kind == AttackKind.Light ? index + 1 : kind == AttackKind.Heavy ? 0 : 1)
                    : C.TryHeavy(kind == AttackKind.Heavy ? index + 1 : 0);
                if (started) return;
                queued = Queued.None;
            }

            if (t >= a.dodgeCancelTime)
            {
                var b = C.PeekBuffer();
                if (b == NightfarerCharacter.BufferedAction.Dodge && C.TryDodge()) return;
                if (b == NightfarerCharacter.BufferedAction.Skill && C.TrySkill()) return;
                if (b == NightfarerCharacter.BufferedAction.Jump && C.Motor.Grounded && C.TryJump()) return;
            }

            if (t >= a.moveCancelTime && C.Motor.Grounded && C.MoveInputWorld().sqrMagnitude > 0.04f)
            {
                C.ChangeState(new LocomotionState(C));
                return;
            }

            if (t >= a.duration)
            {
                if (C.Motor.Grounded) C.ChangeState(new LocomotionState(C));
                else C.ChangeState(new AirborneState(C, false));
            }
        }

        void OpenHit()
        {
            hitOpen = true;
            float mul = a.chargeTimeMax > 0f ? Mathf.Lerp(1f, a.chargedDamageMultiplier, chargeRatio) : 1f;
            C.Hitbox?.BeginSwing(new DamageInfo
            {
                amount = a.damage * mul, poiseDamage = a.poiseDamage * mul, source = C.gameObject, attackName = a.name
            }, a.arcReach, a.arcAngle, SegmentMask());
        }

        int SegmentMask()
        {
            if (C.Weapon == null || !C.Weapon.claws) return WeaponHitbox.All;
            return a.hand == ClawHand.Primary ? WeaponHitbox.Primary : a.hand == ClawHand.Off ? WeaponHitbox.Off : WeaponHitbox.Primary | WeaponHitbox.Off;
        }

        void CloseHit()
        {
            hitOpen = false;
            hitDone = true;
            C.Hitbox?.EndSwing();
        }

        public override void OnAnimationEvent(string eventName)
        {
            if (!a.useAnimationEvents) return;
            if (eventName == "HitStart" && !hitOpen && !hitDone) OpenHit();
            else if (eventName == "HitEnd" && hitOpen) CloseHit();
        }

        public override void Exit()
        {
            if (hitOpen) CloseHit();
            C.Animator.SetActionSpeedMultiplier(1f);
            C.HasHyperArmor = false;
            C.CurrentAttack = null;
        }
    }

    // ------------------------------------------------------------------ Ability

    public class AbilityState : NightfarerState
    {
        readonly AbilityData data;
        readonly AbilityInstance instance;

        public AbilityState(NightfarerCharacter c, AbilityData data, AbilityInstance instance) : base(c)
        {
            this.data = data;
            this.instance = instance;
        }

        public AbilityInstance Instance => instance;
        public AbilityData Data => data;
        public override string Name => "Skill: " + data.displayName;
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            instance.Begin();
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            instance.Tick(dt);
            C.CombatPhase = instance.Phase;
            if (instance.IsFinished && instance.LaunchJump)
            {
                C.MomentumJump();
                return;
            }
            if (instance.IsFinished)
                C.ChangeState(C.Motor.Grounded && C.Motor.VerticalVelocity < 0.5f ? (NightfarerState)new LocomotionState(C) : new AirborneState(C, false));
        }

        public override void Exit() => instance.End();
    }

    // ------------------------------------------------------------------ Hit react

    public class HitReactState : NightfarerState
    {
        readonly Vector3 dir;
        float last;

        public HitReactState(NightfarerCharacter c, Vector3 hitDirection) : base(c)
        {
            hitDirection.y = 0f;
            dir = hitDirection.sqrMagnitude > 1e-4f ? hitDirection.normalized : -c.transform.forward;
        }

        public override string Name => "Hit React";

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            C.CombatPhase = "Staggered";
            C.Motor.SetPlanarVelocity(Vector3.zero);
            C.Animator.PlayAction("HitReact", Cfg.hitReactDuration, 0.04f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float u = Mathf.Clamp01(Elapsed / (Cfg.hitReactDuration * 0.4f));
            float p = Mathf.SmoothStep(0f, 1f, u) * Cfg.hitReactKnockback;
            C.Motor.AddDisplacement(dir * (p - last));
            last = p;
            if (Elapsed >= Cfg.hitReactDuration) C.ChangeState(new LocomotionState(C));
        }
    }
}
