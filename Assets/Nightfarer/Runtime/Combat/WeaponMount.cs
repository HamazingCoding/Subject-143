using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Attaches a weapon model to a humanoid hand. With autoGrip the grip is derived from the rig itself
    /// (hand → middle finger = finger axis, thumb side = blade axis), so any humanoid model works without
    /// hand-tuned offsets; WeaponData offsets are applied on top as fine adjustments.
    /// </summary>
    public static class WeaponMount
    {
        public static GameObject Attach(Animator animator, WeaponData weapon, out Transform bone)
        {
            bone = null;
            if (weapon == null || weapon.modelPrefab == null) return null;
            bone = animator != null && animator.isHuman ? animator.GetBoneTransform(weapon.attachBone) : null;
            if (bone == null) bone = animator != null ? animator.transform : null;
            if (bone == null) return null;

            var model = Object.Instantiate(weapon.modelPrefab, bone);
            model.name = weapon.displayName;
            Vector3 s = bone.lossyScale;
            model.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));

            Quaternion gripRot = Quaternion.identity;
            Vector3 gripPos = Vector3.zero;
            if (weapon.autoGrip && TryComputeGrip(animator, weapon.attachBone, bone, out gripPos, out gripRot)) { }
            model.transform.localRotation = gripRot * Quaternion.Euler(weapon.gripRotationOffset);
            model.transform.localPosition = gripPos + gripRot * Vector3.Scale(weapon.gripPositionOffset, model.transform.localScale);
            return model;
        }

        static readonly System.Collections.Generic.Dictionary<(int, HumanBodyBones), (Vector3, Quaternion)> Cache =
            new System.Collections.Generic.Dictionary<(int, HumanBodyBones), (Vector3, Quaternion)>();

        /// <summary>
        /// Call before the Animator first poses the rig (e.g. in Awake/Start) so the finger-less fallback can
        /// read the T-pose. Results are cached per Animator, so later weapon swaps reuse them.
        /// </summary>
        /// <summary>Grip of a weapon held in the given hand (hand-local position and rotation), if computable.</summary>
        public static bool TryGetGrip(Animator animator, HumanBodyBones handBone, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (animator == null || !animator.isHuman) return false;
            var hand = animator.GetBoneTransform(handBone);
            return hand != null && TryComputeGrip(animator, handBone, hand, out position, out rotation);
        }

        public static void PrepareGrip(Animator animator, HumanBodyBones handBone)
        {
            if (animator == null || !animator.isHuman) return;
            var hand = animator.GetBoneTransform(handBone);
            if (hand != null) TryComputeGrip(animator, handBone, hand, out _, out _);
        }

        static bool TryComputeGrip(Animator animator, HumanBodyBones handBone, Transform hand, out Vector3 position, out Quaternion rotation)
        {
            var key = (animator.GetInstanceID(), handBone);
            if (Cache.TryGetValue(key, out var cached))
            {
                (position, rotation) = cached;
                return true;
            }
            bool ok = ComputeFromFingers(animator, handBone, hand, out position, out rotation) ||
                      ComputeFromTPose(animator, handBone, hand, out position, out rotation);
            if (ok) Cache[key] = (position, rotation);
            return ok;
        }

        /// <summary>Rigs without finger bones: assume the rig is in its (T-pose) bind pose, palms down.</summary>
        static bool ComputeFromTPose(Animator animator, HumanBodyBones handBone, Transform hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            bool right = handBone == HumanBodyBones.RightHand;
            var forearm = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            if (forearm == null) return false;
            Vector3 fingerWorld = (hand.position - forearm.position).normalized;
            // Palms down in a T-pose: the thumb (and so the blade) points along the character's forward.
            Vector3 bladeWorld = Vector3.ProjectOnPlane(animator.transform.forward, fingerWorld).normalized;
            Vector3 fingerDir = hand.InverseTransformDirection(fingerWorld).normalized;
            Vector3 bladeDir = hand.InverseTransformDirection(bladeWorld).normalized;
            rotation = Quaternion.LookRotation(fingerDir, bladeDir);
            float handLength = Vector3.Distance(hand.position, forearm.position) * 0.32f;
            Vector3 palmWorld = Vector3.down * handLength * 0.25f;
            position = hand.InverseTransformPoint(hand.position + fingerWorld * handLength * 0.55f + palmWorld);
            return true;
        }

        static bool ComputeFromFingers(Animator animator, HumanBodyBones handBone, Transform hand, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            bool right = handBone == HumanBodyBones.RightHand;
            if (!right && handBone != HumanBodyBones.LeftHand) return false;
            var mid = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            var thumb = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (mid == null || thumb == null) return false;

            Vector3 finger = hand.InverseTransformPoint(mid.position);
            Vector3 thumbLocal = hand.InverseTransformPoint(thumb.position);
            if (finger.sqrMagnitude < 1e-6f) return false;
            Vector3 fingerDir = finger.normalized;
            Vector3 bladeDir = Vector3.ProjectOnPlane(thumbLocal, fingerDir);
            if (bladeDir.sqrMagnitude < 1e-6f) return false;
            bladeDir.Normalize();
            // Weapon space: +Y along the blade, +Z toward the fingers (cutting edge).
            rotation = Quaternion.LookRotation(fingerDir, bladeDir);
            // Fist centre: a little over halfway to the knuckles, nudged toward the palm side.
            Vector3 palm = Vector3.Cross(fingerDir, bladeDir) * (right ? 1f : -1f);
            position = finger * 0.6f + palm * (finger.magnitude * 0.12f);
            return true;
        }
    }
}
