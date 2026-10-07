using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Claw Shot: Subject 143 throws the claw arm out at the target (a web-shooter-style snap: arm locked straight,
    /// claws pointed at the target, off arm pulled back), the claw line shoots out and visibly travels, hooks, and
    /// reels the body in; a short window then allows a follow-up attack. Hooking terrain pulls you to that point,
    /// so it doubles as traversal. Targets: lock-on target, else aim-assisted target near the camera centre, else
    /// whatever the camera points at. Works in the air (he hangs briefly while the line flies), and the pull's
    /// speed is carried out of it (Wylder's hook): he slides on with it, and jumping during or right after the
    /// pull turns it into a faster, longer leap.
    /// </summary>
    [CreateAssetMenu(menuName = "Subject 143/Nightfarer/Abilities/Claw Shot", fileName = "Ability_ClawShot")]
    public class ClawShotAbility : AbilityData
    {
        public float range = 30f;
        [Tooltip("Without lock-on, targets within this angle of the camera's aim are hooked directly.")]
        public float aimAssistAngle = 18f;
        [Tooltip("Time for the arm to snap out straight.")]
        public float thrustTime = 0.09f;
        [Tooltip("Speed the claw line travels at (m/s).")]
        public float lineSpeed = 90f;
        public float pullSpeed = 30f;
        public float stopDistance = 1.6f;
        public float whiffRecovery = 0.3f;
        public float hookDamage = 25f;
        public float hookPoiseDamage = 15f;
        public float followUpWindow = 0.7f;
        public AttackData followUpAttack = new AttackData { name = "Claw Follow-up", animationSlot = "SkillFollowUp" };
        public Color lineColor = new Color(0.9f, 0.75f, 0.35f);
        public float lineWidth = 0.035f;

        [Header("Momentum")]
        [Tooltip("Fraction of the pull speed kept when the pull ends (terrain hooks).")]
        public float momentumCarry = 0.4f;
        [Tooltip("Fraction kept when hooked to an enemy (you stop near them).")]
        public float enemyMomentumCarry = 0.15f;
        [Tooltip("Upward speed added when the pull ends on terrain (an arc over the top).")]
        public float momentumPop = 3f;
        public float momentumTime = 0.7f;
        [Tooltip("A jump pressed after this much pulling cancels into a momentum leap.")]
        public float jumpCancelAfter = 0.05f;

        // Kept for older assets; the windup is now thrustTime + line travel time.
        [HideInInspector] public float windup = 0.22f;
        [HideInInspector] public float maxPullTime = 0.7f;

        public override AbilityInstance CreateInstance(NightfarerCharacter owner) => new Instance(owner, this);

        class Instance : AbilityInstance, IClawPoseProvider
        {
            readonly ClawShotAbility data;
            Vector3 hookPoint;
            IDamageable hookedTarget;
            Transform hookedTransform;
            bool hit, pulling, retracting, airborne;
            float pullTime, travelTime, maxPull, retractStart;
            Vector3 pullVelocity;
            LineRenderer line;

            public Instance(NightfarerCharacter owner, ClawShotAbility data) : base(owner) { this.data = data; }

            public override string Phase => IsFinished ? "Done" : pulling ? "Pull" : retracting ? "Retract" : Elapsed < data.thrustTime ? "Throw" : "Line out";

            float LineOut => data.thrustTime + travelTime;

            public override void Begin()
            {
                FindHookPoint();
                float dist = Vector3.Distance(Owner.PrimaryHandPosition, hookPoint);
                travelTime = dist / Mathf.Max(1f, data.lineSpeed);
                maxPull = dist / Mathf.Max(1f, data.pullSpeed) + 0.25f;
                Owner.Animator.PlayAction(data.animationSlot, LineOut + 0.35f, 0.04f);
                airborne = !Owner.Motor.Grounded;
                Owner.Motor.SetPlanarVelocity(airborne ? Owner.Motor.PlanarVelocity * 0.25f : Vector3.zero);
                Owner.MomentumTimer = 0f;
                Vector3 flat = hookPoint - Owner.transform.position; flat.y = 0f;
                Owner.Motor.FaceDirection(flat, 0f, 0f);   // snap to face the target: the throw reads instantly
                CreateLine();
            }

            void FindHookPoint()
            {
                Vector3 origin = Owner.transform.position + Vector3.up * 1.0f;
                var target = Owner.LockOn != null ? Owner.LockOn.Current : null;
                if (target != null && Vector3.Distance(origin, target.Point) <= data.range)
                {
                    Hook(target.Point, target.transform, target.GetComponentInParent<IDamageable>());
                    return;
                }

                Transform view = Owner.ViewTransform;
                Vector3 dir = view != null ? view.forward : Owner.transform.forward;
                LockOnTarget assisted = null;
                float bestAngle = data.aimAssistAngle;
                Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
                foreach (var t in LockOnTarget.All)
                {
                    if (t == null || t.transform.IsChildOf(Owner.transform)) continue;
                    Vector3 to = t.Point - origin;
                    if (to.magnitude > data.range) continue;
                    float a = Vector3.Angle(flatDir, new Vector3(to.x, 0f, to.z));
                    if (a < bestAngle)
                    {
                        bestAngle = a;
                        assisted = t;
                    }
                }
                if (assisted != null)
                {
                    Hook(assisted.Point, assisted.transform, assisted.GetComponentInParent<IDamageable>());
                    return;
                }

                float best = float.MaxValue;
                foreach (var h in Physics.RaycastAll(view != null ? view.position : origin, dir, data.range + 8f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.transform.IsChildOf(Owner.transform)) continue;
                    if (Vector3.Distance(origin, h.point) > data.range || h.distance >= best) continue;
                    best = h.distance;
                    var dmg = h.collider.GetComponentInParent<IDamageable>();
                    Hook(h.point, dmg != null ? h.collider.transform : null, dmg);
                }
                if (!hit) hookPoint = origin + dir * data.range;
            }

            void Hook(Vector3 point, Transform t, IDamageable d)
            {
                hookPoint = point;
                hookedTransform = t;
                hookedTarget = d;
                hit = true;
            }

            void CreateLine()
            {
                var go = new GameObject("ClawShotLine");
                line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.startWidth = data.lineWidth;
                line.endWidth = data.lineWidth * 0.6f;
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = data.lineColor;
                line.enabled = false;
            }

            void UpdateLine(float extend)
            {
                if (line == null) return;
                Vector3 hand = Owner.PrimaryHandPosition;
                line.enabled = extend > 0.001f;
                line.SetPosition(0, hand);
                line.SetPosition(1, Vector3.Lerp(hand, hookPoint, extend));
            }

            public override void Tick(float dt)
            {
                Elapsed += dt;
                if (hookedTransform != null && Owner.LockOn != null && Owner.LockOn.Current != null)
                    hookPoint = Owner.LockOn.Current.Point;
                Vector3 flat = hookPoint - Owner.transform.position; flat.y = 0f;
                Owner.Motor.FaceDirection(flat, 1440f, dt);

                // The line leaves the claw once the arm is out, then travels at lineSpeed.
                float lineT = travelTime > 0f ? Mathf.Clamp01((Elapsed - data.thrustTime * 0.6f) / travelTime) : 1f;
                if (Elapsed < LineOut)
                {
                    UpdateLine(lineT);
                    if (airborne && Owner.Motor.VerticalVelocity < -1.5f) Owner.Motor.SetVerticalVelocity(-1.5f);   // hang while it flies
                    return;
                }

                if (!hit)
                {
                    if (!retracting) { retracting = true; retractStart = Elapsed; }
                    float r = Mathf.Clamp01((Elapsed - retractStart) / data.whiffRecovery);
                    UpdateLine(1f - r);
                    if (r >= 1f) IsFinished = true;
                    return;
                }

                if (!pulling)
                {
                    pulling = true;
                    Owner.Motor.GravityEnabled = false;
                    Owner.Motor.SetVerticalVelocity(0f);
                    if (hookedTarget != null && hookedTarget.IsAlive)
                    {
                        hookedTarget.ReceiveDamage(new DamageInfo
                        {
                            amount = data.hookDamage, poiseDamage = data.hookPoiseDamage, point = hookPoint,
                            direction = (hookPoint - Owner.transform.position).normalized, source = Owner.gameObject,
                            attackName = data.displayName
                        });
                    }
                    if (Owner.cameraRig != null) Owner.cameraRig.Shake(0.05f, 0.12f);
                }

                pullTime += dt;
                UpdateLine(1f);
                Vector3 chest = Owner.transform.position + Vector3.up * 0.7f;
                Vector3 toHook = hookPoint - chest;
                float stop = hookedTarget != null ? data.stopDistance : 0.6f;
                float dist = toHook.magnitude;
                if (dist <= stop || pullTime >= maxPull)
                {
                    Finish(false);
                    return;
                }
                if (pullTime >= data.jumpCancelAfter && Owner.PeekBuffer() == NightfarerCharacter.BufferedAction.Jump)
                {
                    Finish(true);
                    return;
                }
                // Accelerate into the pull: a yank, not a glide.
                float speed = data.pullSpeed * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(pullTime / 0.12f));
                pullVelocity = toHook / dist * speed;
                Owner.Motor.AddDisplacement(toHook / dist * Mathf.Min(speed * dt, dist - stop));
            }

            /// <summary>End of the pull: keep part of its speed (and leap if a jump cancelled it).</summary>
            void Finish(bool jump)
            {
                IsFinished = true;
                LaunchJump = jump;
                bool enemy = hookedTarget != null;
                Vector3 v = pullVelocity * (enemy ? data.enemyMomentumCarry : data.momentumCarry);
                if (!enemy || jump) v.y = Mathf.Max(v.y, 0f) + data.momentumPop * (enemy ? 0.4f : 1f);
                if (enemy && !jump) v.y = 0f;
                Owner.CarryMomentum(v, data.momentumTime);
            }

            public Vector3 PullVelocity => pullVelocity;

            /// <summary>Claw arm thrown straight at the hook point, off arm pulled back to the chest.</summary>
            public bool TryGetClawPoses(out WeaponPose primary, out float primaryWeight, out WeaponPose off, out float offWeight)
            {
                primary = off = default;
                primaryWeight = offWeight = 0f;
                var anim = Owner.Animator != null ? Owner.Animator.animator : null;
                var w = Owner.Weapon;
                if (anim == null || w == null || !anim.isHuman) return false;
                bool left = w.primaryHand == HumanBodyBones.LeftHand;
                var shoulder = anim.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var elbow = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                if (shoulder == null || elbow == null || hand == null) return false;
                Transform space = Owner.Animator.visualRoot != null ? Owner.Animator.visualRoot : Owner.transform;
                float rs = Owner.weaponIK != null ? Owner.weaponIK.RigScale : 1f;

                float armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
                Vector3 aim = (hookPoint - shoulder.position).normalized;
                float snap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / data.thrustTime));
                float reach = Mathf.Lerp(0.45f, 0.97f, snap);   // from cocked to locked straight
                Vector3 cocked = shoulder.position + space.TransformDirection(new Vector3(left ? -0.05f : 0.05f, -0.15f, -0.1f));
                Vector3 gripW = Vector3.Lerp(cocked, shoulder.position + aim * armLength, reach);
                Vector3 bladeW = Vector3.Slerp(space.TransformDirection(new Vector3(0f, -0.3f, 0.95f)), aim, snap);
                primary = new WeaponPose
                {
                    grip = space.InverseTransformPoint(gripW) / rs,
                    blade = space.InverseTransformDirection(bladeW).normalized,
                    edge = space.InverseTransformDirection(Vector3.ProjectOnPlane(Vector3.up, bladeW)).normalized,
                };
                float total = IsFinished ? 0f : 1f;
                primaryWeight = retracting ? Mathf.Clamp01(1f - (Elapsed - retractStart) / data.whiffRecovery) : total;

                float side = left ? 1f : -1f;   // off arm is on the other side
                off = new WeaponPose
                {
                    grip = new Vector3(side * 0.2f, 1.05f, -0.05f),
                    blade = new Vector3(side * 0.3f, 0.2f, 0.93f).normalized,
                    edge = Vector3.up,
                };
                offWeight = primaryWeight * 0.85f;
                return true;
            }

            public override void End()
            {
                Owner.Motor.GravityEnabled = true;
                if (line != null)
                {
                    Object.Destroy(line.material);
                    Object.Destroy(line.gameObject);
                }
                if (hit && hookedTarget != null) Owner.OpenFollowUpWindow(data.followUpAttack, data.followUpWindow);
            }
        }
    }

    /// <summary>Abilities that pose both claw arms directly (character space, real sides, authoring units).</summary>
    public interface IClawPoseProvider
    {
        bool TryGetClawPoses(out WeaponPose primary, out float primaryWeight, out WeaponPose off, out float offWeight);
    }
}
