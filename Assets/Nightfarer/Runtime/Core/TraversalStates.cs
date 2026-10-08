using UnityEngine;

namespace Subject143.Nightfarer
{
    // ------------------------------------------------------------------ Climb (mantle)

    /// <summary>
    /// Climb onto a ledge with the body touching the wall. Tall ledges: reach (hands go up to the lip) → climb
    /// (hands stay planted on the lip while the feet step up the wall, pushing the body up in pulses) → vault
    /// (body rolls over the lip as the hands push down, then let go). Low ledges: a one-handed claw vault.
    /// Hands are placed by WeaponIK (this state is an <see cref="IClawPoseProvider"/>), feet by FootIK overrides.
    /// </summary>
    public class MantleState : NightfarerState, IClawPoseProvider
    {
        readonly Vector3 destination, facing, normal, lip;
        readonly float height, duration;
        readonly bool vault;
        Vector3 start, last, wallStand;
        float hangY;
        int steps;

        public MantleState(NightfarerCharacter c, Vector3 destination, float height, Vector3 wallPoint, Vector3 wallNormal, float topY) : base(c)
        {
            this.destination = destination;
            this.height = height;
            normal = new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;
            facing = -normal;
            lip = new Vector3(wallPoint.x, topY, wallPoint.z);
            vault = height < c.Motor.Controller.height * 0.85f;
            duration = c.Config.mantleBaseDuration + c.Config.mantleDurationPerMetre * Mathf.Max(0f, height);
        }

        public override string Name => "Mantle";
        public bool IsVault => vault;
        public float Progress => Mathf.Clamp01(Elapsed / duration);
        public Vector3 Lip => lip;
        public Vector3 WallNormal => normal;

        float ReachEnd => vault ? 0.22f : 0.16f;
        float ClimbEnd => vault ? 0.22f : 0.64f;

        public override void Enter()
        {
            start = C.transform.position;
            last = start;
            var cc = C.Motor.Controller;
            Vector3 flatStart = new Vector3(start.x, 0f, start.z), flatLip = new Vector3(lip.x, 0f, lip.z);
            float along = Vector3.Dot(flatStart - flatLip, normal);
            wallStand = start - normal * Mathf.Max(0f, along - (cc.radius + 0.04f));
            hangY = Mathf.Max(start.y, lip.y - cc.height * 0.62f);
            steps = Mathf.Clamp(Mathf.RoundToInt((hangY - start.y) / 0.35f), 1, 4);
            C.IsSprinting = false;
            C.Motor.GravityEnabled = false;
            C.Motor.SetPlanarVelocity(Vector3.zero);
            C.Motor.SetVerticalVelocity(0f);
            C.Motor.FaceDirection(facing, 0f, 0f);
            C.Animator.PlayAction(vault ? "Vault" : "Mantle", duration, 0.05f);
            C.CombatPhase = "-";
        }

        /// <summary>Root height during the climb phase (p 0..1): rises in pulses, one per foot push.</summary>
        float ClimbY(float p)
        {
            float n = steps * 2f;
            float stair = p - Mathf.Sin(2f * Mathf.PI * n * p) / (2f * Mathf.PI * n) * 0.6f;
            return Mathf.Lerp(start.y, hangY, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stair)));
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            float u = Progress;
            Vector3 target;
            if (u < ClimbEnd)
            {
                float p = Mathf.Clamp01(u / ClimbEnd);
                Vector3 flat = Vector3.Lerp(start, wallStand, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / Mathf.Max(0.01f, ReachEnd))));
                target = new Vector3(flat.x, vault ? start.y : ClimbY(p), flat.z);
            }
            else
            {
                float v = (u - ClimbEnd) / (1f - ClimbEnd);
                float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.6f));
                float fwd = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.3f) / 0.7f));
                float y0 = vault ? start.y : hangY;
                float arc = Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI) * (vault ? 0.12f : 0.06f);
                Vector3 from = vault ? Vector3.Lerp(start, wallStand, 1f) : wallStand;
                target = new Vector3(
                    Mathf.Lerp(from.x, destination.x, fwd),
                    Mathf.Lerp(y0, destination.y + 0.05f, up) + arc,
                    Mathf.Lerp(from.z, destination.z, fwd));
            }
            C.Motor.AddDisplacement(target - last);
            last = target;
            if (!vault) PlaceFeet(u);

            if (u >= 1f)
            {
                if (C.MoveInputWorld().sqrMagnitude > 0.02f) C.Motor.SetPlanarVelocity(facing * Cfg.runSpeed * 0.6f);
                C.ChangeState(new LocomotionState(C));
            }
        }

        // ---------------------------------------------------------------- feet on the wall

        void PlaceFeet(float u)
        {
            var feet = C.FootIK;
            if (feet == null) return;
            float w = Mathf.Clamp01((u - ReachEnd * 0.6f) / 0.08f) * Mathf.Clamp01((ClimbEnd + 0.1f - u) / 0.1f);
            if (w <= 0f) return;
            float p = Mathf.Clamp01((u - ReachEnd * 0.6f) / (ClimbEnd - ReachEnd * 0.6f));
            feet.SetOverride(FootOnWall(p, 0), FootOnWall(p, 1), w);
        }

        Vector3 WallPoint(float y, int side)
        {
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            return new Vector3(lip.x, y, lip.z) + normal * 0.075f + right * (side == 0 ? -0.09f : 0.09f);
        }

        /// <summary>Feet alternate: one stays planted on the wall while the other steps up past it.</summary>
        Vector3 FootOnWall(float p, int side)
        {
            float stages = steps * 2f;
            float phase = p * stages - side;
            int k = Mathf.FloorToInt(phase / 2f) * 2;
            float since = phase - k;
            float lift = 0.22f;
            float Plant(int kk) => ClimbY(Mathf.Clamp01((kk + side) / stages)) + lift;
            Vector3 a = WallPoint(Plant(k), side);
            if (since < 1.35f) return a;
            float s = Mathf.SmoothStep(0f, 1f, (since - 1.35f) / 0.65f);
            Vector3 b = WallPoint(Plant(k + 2), side);
            return Vector3.Lerp(a, b, s) + normal * (0.12f * Mathf.Sin(s * Mathf.PI));
        }

        // ---------------------------------------------------------------- hands on the lip

        public bool TryGetClawPoses(out WeaponPose primary, out float primaryWeight, out WeaponPose off, out float offWeight)
        {
            primary = off = default;
            primaryWeight = offWeight = 0f;
            var w = C.Weapon;
            if (w == null) return false;
            bool primaryLeft = w.primaryHand == HumanBodyBones.LeftHand;
            float u = Progress;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Vector3 fingers = (facing - Vector3.up * 0.35f).normalized;
            Vector3 Palm(float side) => lip + right * (side * 0.16f) + facing * 0.06f + Vector3.up * 0.035f;
            // Hands reach up during the first phase, stay planted while he climbs and vaults, then let go.
            float release = vault ? 0.75f : 0.9f;
            float weight = Mathf.Clamp01(u / Mathf.Max(0.01f, ReachEnd)) * Mathf.Clamp01((release + 0.08f - u) / 0.08f);
            float leftSide = -1f, rightSide = 1f;
            var leftPose = WeaponIK.WorldPose(C, Palm(leftSide), fingers, right);
            var rightPose = WeaponIK.WorldPose(C, Palm(rightSide), fingers, -right);
            primary = primaryLeft ? leftPose : rightPose;
            off = primaryLeft ? rightPose : leftPose;
            primaryWeight = weight;
            offWeight = vault ? 0f : weight;   // low vault: only the claw hand plants
            return true;
        }

        public override void Exit() => C.Motor.GravityEnabled = true;
    }

    // ------------------------------------------------------------------ Super jump

    /// <summary>
    /// Spider-Man's charged jump: holding Jump crouches and builds power (he can keep moving slowly), releasing
    /// springs him up; height and forward speed scale with the charge.
    /// </summary>
    public class SuperJumpChargeState : NightfarerState
    {
        readonly bool running;
        float nextPuff;
        bool full;

        public SuperJumpChargeState(NightfarerCharacter c, bool running = false) : base(c) { this.running = running; }
        public override string Name => "Super Jump Charge";
        /// <summary>Charging on the run (sprint / surge): full speed, no crouch.</summary>
        public bool Running => running;
        /// <summary>Planar speed given by the last launch (tests / debug).</summary>
        public static float LaunchSpeed { get; private set; }
        public float Charge => Mathf.Clamp01(Elapsed / Mathf.Max(0.05f, Cfg.superJumpChargeTime));

        public override void Enter()
        {
            C.CombatPhase = "Charging";
            if (running) return;
            C.IsSprinting = false;
            C.IsSurging = false;
            C.Animator.PlayAction("SuperJumpCharge", Cfg.superJumpChargeTime, 0.08f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (running) C.Locomote(dt, 1f, true);
            else
            {
                C.Locomote(dt, Cfg.superJumpChargeMoveScale * (1f - 0.5f * Charge), false);
                PlantFeet();
            }
            C.CombatPhase = $"Charging {Charge:P0}";
            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > Cfg.coyoteTime)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }
            if (C.PeekBuffer() == NightfarerCharacter.BufferedAction.Dodge && C.TryDodge()) return;

            if (!running && Elapsed >= nextPuff)
            {
                nextPuff = Elapsed + 0.12f;
                GroundImpact.Puff(C.transform.position, 0.25f + 0.35f * Charge);
            }
            if (!full && Charge >= 1f)
            {
                full = true;
                GroundImpact.Spawn(C.transform.position, 0.15f, false);
                if (C.cameraRig != null) C.cameraRig.Shake(0.03f, 0.15f);
            }
            if (!C.JumpHeld) Launch();
        }

        /// <summary>Feet planted under the hips, knees pushed forward and slightly out (a coiled spring).</summary>
        void PlantFeet()
        {
            if (C.FootIK == null) return;
            Transform t = C.transform;
            float w = Mathf.Clamp01(Elapsed / 0.12f);
            C.FootIK.SetPlant(t.position - t.right * 0.11f + t.forward * 0.04f, t.position + t.right * 0.11f + t.forward * 0.04f, w,
                t.forward - t.right * 0.25f + Vector3.up * 0.3f, t.forward + t.right * 0.25f + Vector3.up * 0.3f);
        }

        void Launch()
        {
            float charge = Charge;
            if (!C.Vitals.TryConsume(Cfg.superJumpStaminaCost)) charge = 0f;
            float h = Mathf.Lerp(Cfg.jumpHeight * 1.5f, Cfg.superJumpHeight, charge);
            Vector3 move = C.MoveInputWorld();
            float now = C.Motor.PlanarSpeed;
            if (move.sqrMagnitude > 0.02f || now > Cfg.runSpeed)
            {
                // Sprint launch: the speed he has built is multiplied into the launch (kinetic energy into distance),
                // plus the charge's own push; faster launches fly flatter and further.
                Vector3 dir = running && now > 0.5f ? C.Motor.PlanarVelocity / now : move.normalized;
                float speed = now * Mathf.Lerp(1f, Cfg.launchSpeedGain, charge) + Mathf.Lerp(Cfg.runSpeed * 0.5f, Cfg.superJumpForwardSpeed, charge);
                speed = Mathf.Min(speed, Cfg.launchMaxSpeed);
                h *= Mathf.Lerp(1f, Cfg.launchFlatten, Mathf.InverseLerp(Cfg.runSpeed, Cfg.surgeSpeed, now));
                C.Motor.SetPlanarVelocity(dir * speed);
                C.Motor.FaceDirection(dir, 0f, 0f);
                LaunchSpeed = speed;
            }
            else C.Motor.SetPlanarVelocity(C.Motor.PlanarVelocity * 0.3f);
            C.Motor.Jump(h);
            GroundImpact.Spawn(C.transform.position, 0.2f + 0.45f * charge, true);
            if (C.cameraRig != null) C.cameraRig.Shake(0.05f + 0.12f * charge, 0.3f);
            ScreenFX.SpeedLines(0.25f + 0.6f * charge, 0.45f);
            ScreenFX.Blur(0.03f + 0.05f * charge, 0.3f, C.transform.position + Vector3.up * 0.6f);
            C.ChangeState(new AirborneState(C, true, false, true));
        }
    }

    // ------------------------------------------------------------------ Hero landing

    /// <summary>
    /// Three-point superhero landing after a big drop: knee down, claw hand planted on the ground, off arm out for
    /// balance. The ground cracks, dust rings out and the camera shakes, all scaled by the fall height.
    /// </summary>
    public class HeroLandingState : NightfarerState, IClawPoseProvider
    {
        readonly float severity;
        float duration;
        Vector3 handPoint, handFingers;
        bool slide;
        float nextSpark;

        public HeroLandingState(NightfarerCharacter c, float fallHeight) : base(c)
        {
            severity = Mathf.InverseLerp(c.Config.heroLandMinHeight, c.Config.heroLandMaxHeight, fallHeight);
        }

        public override string Name => "Hero Landing";
        public float Severity => severity;
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            duration = Mathf.Lerp(Cfg.heroLandRecovery.x, Cfg.heroLandRecovery.y, severity);
            // Coming down fast across the ground he slides on in the crouch (claw dragging); straight drops stop dead.
            slide = C.Motor.PlanarSpeed > Cfg.runSpeed * Cfg.landKeepMomentumFraction;
            C.Motor.SetPlanarVelocity(C.Motor.PlanarVelocity * (slide ? Cfg.heroLandSlideCarry : 0.1f));
            C.Animator.PlayAction("HeroLand", duration, 0.02f);
            C.CombatPhase = "Landing";

            Vector3 right = C.transform.right, fwd = C.transform.forward;
            bool primaryLeft = C.Weapon == null || C.Weapon.primaryHand == HumanBodyBones.LeftHand;
            float side = primaryLeft ? -1f : 1f;
            Vector3 probe = C.transform.position + fwd * 0.3f + right * (0.2f * side) + Vector3.up * 0.5f;
            handPoint = Physics.Raycast(probe, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(C.transform)
                ? hit.point : new Vector3(probe.x, C.transform.position.y, probe.z);
            handFingers = (fwd + right * (0.45f * side)).normalized;

            GroundImpact.Spawn(C.transform.position, 0.45f + 0.55f * severity, false);
            if (C.cameraRig != null) C.cameraRig.Shake(Mathf.Lerp(0.09f, 0.32f, severity), Mathf.Lerp(0.3f, 0.6f, severity));
            ScreenFX.Blur(0.02f + 0.05f * severity, 0.22f, C.transform.position);
        }

        /// <summary>Left foot planted forward, right knee down with that foot resting behind on its instep.</summary>
        void PlantFeet()
        {
            if (C.FootIK == null) return;
            float u = Mathf.Clamp01(Elapsed / Mathf.Max(0.01f, duration));
            float w = Mathf.Clamp01((0.85f - u) / 0.25f);
            if (w <= 0f) return;
            Transform t = C.transform;
            C.FootIK.SetPlant(t.position + t.forward * 0.24f - t.right * 0.12f, t.position - t.forward * 0.3f + t.right * 0.1f, w,
                t.forward + Vector3.up * 0.6f - t.right * 0.2f, t.forward - Vector3.up * 0.55f, 0f, 0.01f);
        }

        public bool Sliding => slide;

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (slide)
            {
                Vector3 v = C.Motor.PlanarVelocity;
                float s = Mathf.Max(0f, v.magnitude - Cfg.skidDeceleration * dt);
                C.Motor.SetPlanarVelocity(v.sqrMagnitude > 1e-4f ? v.normalized * s : Vector3.zero);
                // The planted claw drags along the ground with him.
                bool primaryLeft = C.Weapon == null || C.Weapon.primaryHand == HumanBodyBones.LeftHand;
                Vector3 probe = C.transform.position + C.transform.forward * 0.3f + C.transform.right * (primaryLeft ? -0.2f : 0.2f) + Vector3.up * 0.5f;
                handPoint = Physics.Raycast(probe, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(C.transform)
                    ? hit.point : new Vector3(probe.x, C.transform.position.y, probe.z);
                if (s > 1f && Elapsed >= nextSpark)
                {
                    nextSpark = Elapsed + 0.04f;
                    ClawSparks.Emit(handPoint + Vector3.up * 0.02f, -v.normalized, 3);
                    GroundImpact.Puff(C.transform.position, 0.35f);
                }
                if (Elapsed >= duration * 0.35f && C.MoveInputWorld().sqrMagnitude > 0.04f && s > Cfg.runSpeed * 0.5f)
                {
                    C.ChangeState(new LocomotionState(C));   // spring out of the slide, keeping the speed
                    return;
                }
            }
            PlantFeet();
            if (Elapsed >= duration * 0.35f && C.PeekBuffer() == NightfarerCharacter.BufferedAction.Dodge && C.TryDodge()) return;
            if (Elapsed >= duration * 0.55f)
            {
                if (C.TryBufferedGroundAction()) return;
                if (C.MoveInputWorld().sqrMagnitude > 0.04f)
                {
                    C.ChangeState(new LocomotionState(C));
                    return;
                }
            }
            if (Elapsed >= duration) C.ChangeState(new LocomotionState(C));
        }

        public bool TryGetClawPoses(out WeaponPose primary, out float primaryWeight, out WeaponPose off, out float offWeight)
        {
            primary = off = default;
            primaryWeight = offWeight = 0f;
            var anim = C.Animator != null ? C.Animator.animator : null;
            if (anim == null || !anim.isHuman || C.Weapon == null) return false;
            bool primaryLeft = C.Weapon.primaryHand == HumanBodyBones.LeftHand;
            float u = Mathf.Clamp01(Elapsed / Mathf.Max(0.01f, duration));
            float w = Mathf.Clamp01(u / 0.05f) * Mathf.Clamp01((0.8f - u) / 0.2f);
            Vector3 thumb = C.transform.right * (primaryLeft ? 1f : -1f);
            primary = WeaponIK.WorldPose(C, handPoint + Vector3.up * 0.03f, (handFingers - Vector3.up * 0.25f).normalized, thumb);
            primaryWeight = w;
            // Off arm swept back and out for balance.
            var offShoulder = anim.GetBoneTransform(primaryLeft ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            if (offShoulder != null)
            {
                Vector3 outward = C.transform.right * (primaryLeft ? 1f : -1f);
                Vector3 palm = offShoulder.position - C.transform.forward * 0.3f + outward * 0.14f - Vector3.up * 0.3f;
                off = WeaponIK.WorldPose(C, palm, (-C.transform.forward + outward * 0.5f - Vector3.up * 0.4f).normalized, Vector3.up);
                offWeight = w * 0.8f;
            }
            return true;
        }
    }

    // ------------------------------------------------------------------ Side jump

    /// <summary>
    /// A quick sideways hop out of a projectile's path (and for mobility): keeps facing the target when locked
    /// on, brief i-frames on take-off, and the claw line or a jump attack can be used from it.
    /// </summary>
    public class SideJumpState : NightfarerState
    {
        readonly Vector3 dir;
        readonly bool left;
        float speed, airtime;
        bool attacked;

        public SideJumpState(NightfarerCharacter c, Vector3 direction, bool left) : base(c)
        {
            direction.y = 0f;
            dir = direction.normalized;
            this.left = left;
        }

        public override string Name => "Side Jump";
        public Vector3 Direction => dir;
        public override bool BlocksStaminaRegen => true;

        public override void Enter()
        {
            C.IsSprinting = false;
            C.IsSurging = false;
            float g = Mathf.Max(1f, -Cfg.gravity);
            airtime = 2f * Mathf.Sqrt(2f * Cfg.sideJumpHeight / g);
            speed = Cfg.sideJumpDistance / airtime;
            C.Motor.Jump(Cfg.sideJumpHeight);
            C.Motor.SetPlanarVelocity(dir * speed);
            C.Animator.PlayAction(left ? "SideJumpL" : "SideJumpR", airtime + 0.08f, 0.03f);
            C.CombatPhase = "Side jump";
            ScreenFX.Blur(0.025f, 0.18f, C.transform.position + Vector3.up * 0.6f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            C.Motor.SetPlanarVelocity(dir * speed);
            if (C.IsLocked) C.Motor.FaceDirection(C.LockTargetDirection(), Cfg.lockedTurnSpeed, dt);
            C.IsInvulnerable = Elapsed >= Cfg.sideJumpIFrames.x && Elapsed <= Cfg.sideJumpIFrames.y;
            C.CombatPhase = C.IsInvulnerable ? "Side jump (evading)" : "Side jump";
            var b = C.PeekBuffer();
            if (b == NightfarerCharacter.BufferedAction.Skill && C.TrySkill()) return;
            if (!attacked && b == NightfarerCharacter.BufferedAction.Light && C.Weapon != null && C.Weapon.jumpAttack != null)
            {
                attacked = true;
                C.TryAttack(C.Weapon.jumpAttack, AttackKind.Jump, 0);
                return;
            }
            if (Elapsed > airtime * 2.5f) C.ChangeState(new AirborneState(C, false));
        }

        public override void OnLanded(float fallHeight, float impactSpeed) => C.ChangeState(new LandingState(C, fallHeight));

        public override void Exit() => C.IsInvulnerable = false;
    }

    // ------------------------------------------------------------------ Skid / pivot-skid

    /// <summary>
    /// Letting go at sprint speed (or reversing hard) doesn't stop him on a dime: he plants his feet, leans back and
    /// skids, kicking up dust, until the momentum bleeds off. A pivot-skid then relaunches him the new way. Flash
    /// step, attacks, jumps and skills cancel it, so combat stays responsive.
    /// </summary>
    public class SkidState : NightfarerState
    {
        readonly Vector3? pivot;
        Vector3 dir;
        bool wasSurging;
        float nextPuff;

        public SkidState(NightfarerCharacter c, Vector3? pivotDirection) : base(c) { pivot = pivotDirection; }

        public override string Name => pivot.HasValue ? "Pivot Skid" : "Skid";
        public bool IsPivot => pivot.HasValue;

        public override void Enter()
        {
            Vector3 v = C.Motor.PlanarVelocity;
            dir = v.sqrMagnitude > 0.01f ? v.normalized : C.transform.forward;
            wasSurging = C.IsSurging;
            C.IsSprinting = false;
            C.IsSurging = false;
            C.Motor.FaceDirection(dir, 0f, 0f);
            C.Animator.PlayAction("Skid", 0.9f, 0.06f);
            C.CombatPhase = "-";
            if (C.cameraRig != null) C.cameraRig.Shake(0.02f, 0.2f);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            Vector3 v = C.Motor.PlanarVelocity;
            float s = Mathf.Max(0f, v.magnitude - Cfg.skidDeceleration * dt);
            C.Motor.SetPlanarVelocity(dir * s);
            C.MovementMode = Name;

            // Feet planted ahead of the hips, knees bent: the body braces against its own momentum.
            if (C.FootIK != null)
            {
                Transform t = C.transform;
                float w = Mathf.Clamp01(Elapsed / 0.08f);
                C.FootIK.SetPlant(t.position + dir * 0.22f - t.right * 0.11f, t.position + dir * 0.08f + t.right * 0.11f, w,
                    dir + Vector3.up * 0.5f, dir + Vector3.up * 0.5f);
            }
            if (Elapsed >= nextPuff && s > 1f)
            {
                nextPuff = Elapsed + 0.06f;
                GroundImpact.Puff(C.transform.position + dir * 0.2f, 0.3f + 0.04f * s);
            }

            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > Cfg.coyoteTime)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }
            if (C.PeekBuffer() != NightfarerCharacter.BufferedAction.None && C.TryBufferedGroundAction()) return;

            Vector3 move = C.MoveInputWorld();
            if (pivot.HasValue)
            {
                if (s <= Cfg.runSpeed * 0.4f)
                {
                    // Plant and relaunch the new way.
                    Vector3 to = move.sqrMagnitude > 0.01f ? move.normalized : pivot.Value;
                    C.Motor.FaceDirection(to, 0f, 0f);
                    C.Motor.SetPlanarVelocity(to * Cfg.runSpeed * 0.9f);
                    if (wasSurging) C.IsSurging = true;
                    GroundImpact.Puff(C.transform.position, 0.6f);
                    C.ChangeState(new LocomotionState(C));
                }
                return;
            }
            // Pushing on in roughly the same direction picks the run back up with the speed that's left.
            if (Elapsed > 0.1f && move.sqrMagnitude > 0.01f && Vector3.Angle(move, dir) < 60f)
            {
                C.ChangeState(new LocomotionState(C));
                return;
            }
            if (s <= Cfg.runSpeed * 0.5f) C.ChangeState(new LocomotionState(C));
        }
    }

    // ------------------------------------------------------------------ Jump squat

    /// <summary>A brief crouch before a normal jump leaves the ground: the body loads, then springs.</summary>
    public class JumpSquatState : NightfarerState
    {
        readonly bool momentum;

        public JumpSquatState(NightfarerCharacter c, bool momentum) : base(c) { this.momentum = momentum; }
        public override string Name => "Jump Squat";

        public override void Enter()
        {
            C.Animator.PlayAction("JumpStart", Cfg.jumpSquatTime + 0.45f, 0.03f);
            if (C.FootIK != null) C.FootIK.Impact(4f);
            C.CombatPhase = "-";
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!C.Motor.Grounded && C.Motor.TimeSinceGrounded > Cfg.coyoteTime)
            {
                C.ChangeState(new AirborneState(C, false));
                return;
            }
            if (Elapsed >= Cfg.jumpSquatTime) C.LaunchJump(momentum);
        }
    }
}
