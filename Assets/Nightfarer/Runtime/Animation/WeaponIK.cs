using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Procedural weapon layer, applied after the Animator each frame: solves both arms with two-bone IK so the
    /// weapon follows the current attack's <see cref="SwingPath"/> (or the weapon's guard/carry pose), with the
    /// off hand on the handle for two-handed weapons. Works on any humanoid; poses are authored for a ~1.4 m
    /// shoulder height and scaled to the rig. Runs before WeaponHitbox so hit detection sees the final blade.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class WeaponIK : MonoBehaviour
    {
        public NightfarerCharacter character;
        [Range(0f, 1f)] public float globalWeight = 1f;
        public float blendSpeed = 9f;
        public float poseBlendSpeed = 14f;

        Animator anim;
        Transform rUpper, rLower, rHand, lUpper, lLower, lHand;
        float rightWeight, leftWeight;
        float rigScale = 1f;
        WeaponPose current;
        bool hasCurrent;
        Quaternion leftGripRot = Quaternion.identity, rightGripRot = Quaternion.identity;
        Vector3 leftGripPos, rightGripPos;
        bool leftGripReady, rightGripReady;
        WeaponPose clawPrimary, clawOff;
        bool hasClaw;
        float primaryWeight, offWeight;

        // Stability for the long claw arm: blend into new pose sources instead of snapping, keep the wrist roll and
        // the elbow direction continuous (no 180-degree flips), and soften the reach near full extension.
        object poseSource;
        float sourceTime;
        [Tooltip("Seconds to blend into a new pose source (attack start, climb, landing...).")]
        public float sourceBlendTime = 0.12f;
        [Tooltip("How fast the wrist roll and elbow direction may turn (per second, exponential).")]
        public float rollSharpness = 22f, poleSharpness = 16f;
        class ArmState { public Vector3 edge, pole; public bool init; }
        readonly ArmState leftArm = new ArmState(), rightArm = new ArmState();

        public float RightWeight => rightWeight;
        public float LeftWeight => leftWeight;

        void Init()
        {
            anim = character.Animator != null ? character.Animator.animator : null;
            if (anim == null || !anim.isHuman) return;
            rUpper = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rLower = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            lUpper = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lLower = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            float shoulder = character.transform.InverseTransformPoint(rUpper.position).y;
            rigScale = Mathf.Clamp(shoulder / 1.4f, 0.6f, 1.6f);
            leftGripReady = WeaponMount.TryGetGrip(anim, HumanBodyBones.LeftHand, out leftGripPos, out leftGripRot);
            rightGripReady = WeaponMount.TryGetGrip(anim, HumanBodyBones.RightHand, out rightGripPos, out rightGripRot);
        }

        public float PrimaryClawWeight => primaryWeight;
        public float RigScale => rigScale;

        /// <summary>Called by the character before the first animation update (T-pose) to measure the rig.</summary>
        public void MeasureRig()
        {
            Init();
        }

        void LateUpdate()
        {
            if (character == null || character.Weapon == null) return;
            if (anim == null) Init();
            if (anim == null || rUpper == null) return;
            if (character.Weapon.claws)
            {
                ClawUpdate();
                return;
            }
            if (character.WeaponModel == null) return;

            var weapon = character.Weapon;
            float dt = Time.deltaTime;
            float targetRight = 0f, targetLeft = 0f;
            WeaponPose target = current;

            if (character.State is AttackState atk && atk.Data.swing != null && atk.Data.swing.IsValid)
            {
                float u = atk.AttackTime / Mathf.Max(0.01f, atk.Data.duration);
                target = atk.Data.swing.Sample(u);
                float env = Mathf.Clamp01(Mathf.Min(u / 0.06f, (1f - u) / 0.12f));
                targetRight = env;
                targetLeft = weapon.twoHanded ? env : 0f;
            }
            else if (character.State is AbilityState ab && ab.Instance is IWeaponPoseProvider provider && provider.TryGetPose(out var abilityPose, out float abilityWeight))
            {
                target = abilityPose;
                targetRight = abilityWeight;
                targetLeft = weapon.twoHanded ? abilityWeight : 0f;
            }
            else if (character.State is DrinkState && weapon.carryPose != null)
            {
                target = SwingPath.FromKey(weapon.carryPose);
                targetRight = 0.85f;
            }
            else if (character.State is LocomotionState || character.State is LandingState)
            {
                float speed = character.Motor.PlanarSpeed;
                bool guard = speed < character.Config.walkSpeed * 1.1f || character.IsLocked;
                var key = guard ? weapon.guardPose : weapon.carryPose;
                if (key != null)
                {
                    target = SwingPath.FromKey(key);
                    targetRight = guard ? 1f : 0.85f;
                    targetLeft = weapon.twoHanded && guard ? 1f : 0f;
                }
            }

            rightWeight = Mathf.MoveTowards(rightWeight, targetRight * globalWeight, blendSpeed * dt);
            leftWeight = Mathf.MoveTowards(leftWeight, targetLeft * globalWeight, blendSpeed * dt);
            if (!hasCurrent) { current = target; hasCurrent = true; }
            // Attack paths are followed exactly; switching between guard/carry/attacks is smoothed.
            float k = character.State is AttackState ? 1f : 1f - Mathf.Exp(-poseBlendSpeed * dt);
            current = SwingPath.Lerp(current, target, k);
            if (rightWeight <= 0.001f) return;

            Transform space = character.Animator.visualRoot != null ? character.Animator.visualRoot : character.transform;
            Quaternion weaponWorld = space.rotation * Quaternion.LookRotation(current.edge, current.blade);
            Vector3 gripWorld = space.TransformPoint(current.grip * rigScale);

            // Right hand: place the weapon's grip at the target, blade along the target direction.
            var model = character.WeaponModel.transform;
            Quaternion handRot = weaponWorld * Quaternion.Inverse(model.localRotation);
            Vector3 handPos = gripWorld - handRot * Vector3.Scale(model.localPosition, rHand.lossyScale);
            Vector3 rHint = space.TransformPoint(new Vector3(0.55f, 0.75f, -0.25f) * rigScale);
            Quaternion animHandRot = rHand.rotation;
            TwoBoneIK.Solve(rUpper, rLower, rHand, handPos, rHint, rightWeight);
            rHand.rotation = Quaternion.Slerp(animHandRot, handRot, rightWeight);

            // Off hand on the handle.
            if (leftWeight > 0.001f && lUpper != null)
            {
                Vector3 secondGrip = model.TransformPoint(weapon.secondGrip);
                Quaternion lHandRot = model.rotation * Quaternion.Inverse(leftGripReady ? leftGripRot : Quaternion.identity);
                Vector3 lHandPos = secondGrip - lHandRot * leftGripPos;
                Vector3 lHint = space.TransformPoint(new Vector3(-0.55f, 0.75f, -0.25f) * rigScale);
                Quaternion animL = lHand.rotation;
                TwoBoneIK.Solve(lUpper, lLower, lHand, lHandPos, lHint, leftWeight);
                lHand.rotation = Quaternion.Slerp(animL, lHandRot, leftWeight);
            }
        }

        // ------------------------------------------------------------------ claws

        /// <summary>
        /// Natural weapons: each hand follows its own path (attack.swing for the main claw, attack.swingOff for
        /// the off hand), with a ready pose when standing/locked on and a low carry while running. Pose "grip" is
        /// the palm position and "blade" the finger direction, in character space with real sides (x &lt; 0 = left).
        /// </summary>
        void ClawUpdate()
        {
            var w = character.Weapon;
            float dt = Time.deltaTime;
            bool primaryLeft = w.primaryHand == HumanBodyBones.LeftHand;
            bool directPose = false;
            float tp = 0f, to = 0f;
            WeaponPose pTarget = clawPrimary, oTarget = clawOff;

            if (character.State is AttackState atk)
            {
                float u = atk.AttackTime / Mathf.Max(0.01f, atk.Data.duration);
                float env = Mathf.Clamp01(Mathf.Min(u / 0.05f, (1f - u) / 0.14f));
                if (atk.Data.swing != null && atk.Data.swing.IsValid) { pTarget = atk.Data.swing.Sample(u); tp = env; }
                else if (w.guardPose != null) { pTarget = SwingPath.FromKey(w.guardPose); tp = env * 0.5f; }
                if (atk.Data.swingOff != null && atk.Data.swingOff.IsValid) { oTarget = atk.Data.swingOff.Sample(u); to = env; }
                else if (w.offGuardPose != null) { oTarget = SwingPath.FromKey(w.offGuardPose); to = env * 0.6f; }
            }
            else if (character.State is IClawPoseProvider stateProvider &&
                     stateProvider.TryGetClawPoses(out var sp, out float spw, out var so, out float sow))
            {
                pTarget = sp; tp = spw;   // climbing, hero landing: hands placed on the world
                oTarget = so; to = sow;
                directPose = true;
            }
            else if (character.State is AbilityState cab && cab.Instance is IClawPoseProvider clawProvider &&
                     clawProvider.TryGetClawPoses(out var cp, out float cpw, out var co, out float cow))
            {
                pTarget = cp; tp = cpw;
                oTarget = co; to = cow;
                directPose = true;
            }
            else if (character.State is AbilityState ab && ab.Instance is IWeaponPoseProvider provider && provider.TryGetPose(out var pose, out float weight))
            {
                pTarget = primaryLeft ? Mirror(pose) : pose;   // ability poses are authored right-handed
                tp = weight;
                oTarget = primaryLeft ? pose : Mirror(pose);
                to = weight * 0.8f;
            }
            else if (character.State is LocomotionState || character.State is LandingState)
            {
                float speed = character.Motor.PlanarSpeed;
                // Claws up only when fighting (locked on, or just after an attack/hit); otherwise the arms hang naturally.
                bool ready = character.IsLocked || (character.InCombatStance && speed < character.Config.walkSpeed * 1.1f);
                if (ready && w.guardPose != null)
                {
                    pTarget = SwingPath.FromKey(w.guardPose); tp = 1f;
                    if (w.offGuardPose != null) { oTarget = SwingPath.FromKey(w.offGuardPose); to = 0.8f; }
                }
                else
                {
                    pTarget = ClawArmGait(primaryLeft, speed);
                    tp = 0.9f;
                }
            }

            float weightRate = blendSpeed * (directPose ? 6f : 1.6f);   // the claw throw snaps in
            primaryWeight = Mathf.MoveTowards(primaryWeight, tp * globalWeight, weightRate * dt);
            offWeight = Mathf.MoveTowards(offWeight, to * globalWeight, weightRate * dt);
            if (!hasClaw) { clawPrimary = pTarget; clawOff = oTarget; hasClaw = true; }
            // Exact path following for attacks/abilities, but only after a short blend in from wherever the arm was.
            if (!ReferenceEquals(poseSource, character.State))
            {
                poseSource = character.State;
                sourceTime = 0f;
            }
            sourceTime += dt;
            bool exact = (character.State is AttackState || directPose) && sourceTime >= sourceBlendTime;
            float k = exact ? 1f : 1f - Mathf.Exp(-(character.State is AttackState || directPose ? 40f : poseBlendSpeed) * dt);
            clawPrimary = SwingPath.Lerp(clawPrimary, pTarget, k);
            clawOff = SwingPath.Lerp(clawOff, oTarget, k);
            rightWeight = primaryLeft ? offWeight : primaryWeight;
            leftWeight = primaryLeft ? primaryWeight : offWeight;

            Transform space = character.Animator.visualRoot != null ? character.Animator.visualRoot : character.transform;
            if (primaryWeight > 0.001f)
            {
                if (primaryLeft) PlaceHand(space, lUpper, lLower, lHand, clawPrimary, leftGripRot, leftGripPos, primaryWeight, -1f, leftArm, dt);
                else PlaceHand(space, rUpper, rLower, rHand, clawPrimary, rightGripRot, rightGripPos, primaryWeight, 1f, rightArm, dt);
            }
            else (primaryLeft ? leftArm : rightArm).init = false;
            if (offWeight > 0.001f)
            {
                if (primaryLeft) PlaceHand(space, rUpper, rLower, rHand, clawOff, rightGripRot, rightGripPos, offWeight, 1f, rightArm, dt);
                else PlaceHand(space, lUpper, lLower, lHand, clawOff, leftGripRot, leftGripPos, offWeight, -1f, leftArm, dt);
            }
            else (primaryLeft ? rightArm : leftArm).init = false;
        }

        /// <summary>
        /// Running/walking pose for the main claw arm, synced to the gait: it swings opposite the off arm, which the
        /// locomotion clip animates, so it stays in step with any clip. The long arm hangs low and swings heavier;
        /// the claws trail the swing, and it drags further back at sprint speed.
        /// </summary>
        WeaponPose ClawArmGait(bool primaryLeft, float speed)
        {
            Transform space = character.Animator.visualRoot != null ? character.Animator.visualRoot : character.transform;
            Transform offShoulder = primaryLeft ? rUpper : lUpper, offHand = primaryLeft ? rHand : lHand;
            Transform shoulder = primaryLeft ? lUpper : rUpper, elbow = primaryLeft ? lLower : rLower, hand = primaryLeft ? lHand : rHand;
            Vector3 offS = space.InverseTransformPoint(offShoulder.position), offH = space.InverseTransformPoint(offHand.position);
            Vector3 s = space.InverseTransformPoint(shoulder.position);
            float armLength = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
            var cfg = character.Config;
            float sprint = Mathf.InverseLerp(cfg.runSpeed, cfg.sprintSpeed, speed);
            float swing = Mathf.Clamp(offH.z - offS.z, -0.45f, 0.45f);             // off arm's swing (from the clip)
            float side = primaryLeft ? -1f : 1f;
            float z = s.z - swing * Mathf.Lerp(1.25f, 0.8f, sprint) - sprint * 0.18f;   // opposite phase, drags back when sprinting
            float y = s.y - armLength * Mathf.Lerp(0.86f, 0.8f, Mathf.Abs(swing) * 2f) + Mathf.Abs(swing) * 0.12f;
            Vector3 grip = new Vector3(s.x + side * 0.07f, y, z);
            Vector3 blade = new Vector3(side * 0.12f, -1f, -swing * 1.6f - sprint * 0.5f).normalized;   // claws trail the motion
            // Surge: the mutated arm drags behind him with the claws scraping the ground (less human at speed).
            float drag = character.IsSurging ? Mathf.InverseLerp(cfg.sprintSpeed, cfg.surgeSpeed, speed) : 0f;
            if (drag > 0f)
            {
                grip = Vector3.Lerp(grip, new Vector3(s.x + side * 0.14f, 0.16f, s.z - 0.28f), drag);
                blade = Vector3.Slerp(blade, new Vector3(side * 0.1f, -0.85f, -0.5f).normalized, drag);
            }
            Vector3 edge = Vector3.ProjectOnPlane(new Vector3(0f, 0f, -Mathf.Sign(swing == 0f ? 1f : swing)), blade);
            if (edge.sqrMagnitude < 1e-4f) edge = Vector3.right;
            return new WeaponPose { grip = grip / Mathf.Max(0.01f, rigScale), blade = blade, edge = edge.normalized };
        }

        void PlaceHand(Transform space, Transform upper, Transform lower, Transform hand, WeaponPose pose, Quaternion gripRot, Vector3 gripPos, float weight, float side, ArmState arm, float dt)
        {
            if (upper == null || hand == null) return;
            Vector3 blade = space.TransformDirection(pose.blade).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(space.TransformDirection(pose.edge), blade).normalized;
            // Wrist roll: turn continuously toward the target roll instead of flipping when a swing reverses.
            if (arm.init)
            {
                Vector3 prev = Vector3.ProjectOnPlane(arm.edge, blade).normalized;
                if (prev.sqrMagnitude > 0.5f) edge = Vector3.Slerp(prev, edge, 1f - Mathf.Exp(-rollSharpness * dt)).normalized;
            }
            if (edge.sqrMagnitude < 0.5f) edge = Vector3.ProjectOnPlane(space.up, blade).normalized;
            arm.edge = edge;
            Quaternion frame = Quaternion.LookRotation(blade, edge);   // fingers, thumb side
            Quaternion handRot = frame * Quaternion.Inverse(gripRot);
            Vector3 palm = space.TransformPoint(pose.grip * rigScale);
            Vector3 handPos = palm - handRot * Vector3.Scale(gripPos, hand.lossyScale);
            // Elbow direction: out, down and back from the shoulder, smoothed so it can't pop to the other side.
            Vector3 hintDir = (space.TransformPoint(new Vector3(0.55f * side, 0.7f, -0.3f) * rigScale) - upper.position).normalized;
            if (arm.init) hintDir = Vector3.Slerp(arm.pole, hintDir, 1f - Mathf.Exp(-poleSharpness * dt)).normalized;
            arm.pole = hintDir;
            arm.init = true;
            Quaternion animRot = hand.rotation;
            TwoBoneIK.Solve(upper, lower, hand, handPos, upper.position + hintDir, weight, true);
            hand.rotation = Quaternion.Slerp(animRot, handRot, weight);
        }

        /// <summary>A claw pose from world-space palm position, finger direction and thumb side.</summary>
        public static WeaponPose WorldPose(NightfarerCharacter c, Vector3 palm, Vector3 fingers, Vector3 thumb)
        {
            Transform space = c.Animator != null && c.Animator.visualRoot != null ? c.Animator.visualRoot : c.transform;
            float rs = c.weaponIK != null ? c.weaponIK.RigScale : 1f;
            Vector3 blade = space.InverseTransformDirection(fingers).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(space.InverseTransformDirection(thumb), blade);
            if (edge.sqrMagnitude < 1e-6f) edge = Vector3.ProjectOnPlane(Vector3.up, blade);
            return new WeaponPose { grip = space.InverseTransformPoint(palm) / Mathf.Max(0.01f, rs), blade = blade, edge = edge.normalized };
        }

        static WeaponPose Mirror(WeaponPose p)
        {
            return new WeaponPose
            {
                grip = new Vector3(-p.grip.x, p.grip.y, p.grip.z),
                blade = new Vector3(-p.blade.x, p.blade.y, p.blade.z),
                edge = new Vector3(-p.edge.x, p.edge.y, p.edge.z),
            };
        }
    }

    /// <summary>Abilities can implement this to pose the weapon during their animation.</summary>
    public interface IWeaponPoseProvider
    {
        bool TryGetPose(out WeaponPose pose, out float weight);
    }

    public static class TwoBoneIK
    {
        /// <summary>Analytic two-bone IK in world space; blends the result with the animated pose by weight.</summary>
        /// <param name="soft">Ease the reach near full extension so the limb never snaps straight (soft IK).</param>
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 hint, float weight, bool soft = false)
        {
            Quaternion upperLocal0 = upper.localRotation, lowerLocal0 = lower.localRotation;
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            Vector3 toTarget = target - a;
            float reach = toTarget.magnitude, max = l1 + l2;
            if (soft)
            {
                float band = 0.08f * max, start = max - band;
                if (reach > start) reach = start + band * (1f - Mathf.Exp(-(reach - start) / band));
            }
            float d = Mathf.Clamp(reach, Mathf.Abs(l1 - l2) + 1e-3f, max * 0.999f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : (c - a).normalized;
            Vector3 pole = Vector3.ProjectOnPlane(hint - a, dir);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(b - a, dir);
            pole.Normalize();

            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float angA = Mathf.Acos(cosA);
            Vector3 elbow = a + (dir * Mathf.Cos(angA) + pole * Mathf.Sin(angA)) * l1;

            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            b = lower.position;
            c = end.position;
            lower.rotation = Quaternion.FromToRotation(c - b, (a + dir * d) - b) * lower.rotation;

            if (weight < 0.999f)
            {
                upper.localRotation = Quaternion.Slerp(upperLocal0, upper.localRotation, weight);
                lower.localRotation = Quaternion.Slerp(lowerLocal0, lower.localRotation, weight);
            }
        }
    }
}
