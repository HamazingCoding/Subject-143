using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// The only component that talks to the Animator. It applies <see cref="AnimationSet"/>s through an
    /// AnimatorOverrideController, drives the locomotion blend tree, and plays action states time-stretched
    /// to the gameplay duration (ActionSpeed = clipLength / duration).
    /// </summary>
    public class NightfarerAnimator : MonoBehaviour
    {
        public Animator animator;
        public RuntimeAnimatorController baseController;
        [Tooltip("Used for any slot the active set leaves empty.")]
        public AnimationSet fallbackSet;
        [Tooltip("Transform rotated for directional rolls while locked on (usually the model root).")]
        public Transform visualRoot;

        static readonly int MoveXId = Animator.StringToHash("MoveX");
        static readonly int MoveZId = Animator.StringToHash("MoveZ");
        static readonly int ActionSpeedId = Animator.StringToHash("ActionSpeed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");

        AnimatorOverrideController overrides;
        readonly Dictionary<string, AnimationClip> slotKeys = new Dictionary<string, AnimationClip>();
        float actionBaseSpeed = 1f, actionMultiplier = 1f;
        float hitStopTimer;
        float visualYaw, visualYawTarget;

        public AnimationSet ActiveSet { get; private set; }
        public string CurrentState { get; private set; } = AnimationSlots.LocomotionState;
        public bool IsHitStopped => hitStopTimer > 0f;

        public void Initialize(AnimationSet set)
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            overrides = new AnimatorOverrideController(baseController) { name = "Nightfarer Overrides" };
            slotKeys.Clear();
            foreach (var clip in baseController.animationClips)
            {
                if (clip == null || !clip.name.StartsWith(AnimationSlots.SlotPrefix)) continue;
                slotKeys[clip.name.Substring(AnimationSlots.SlotPrefix.Length)] = clip;
            }
            animator.runtimeAnimatorController = overrides;
            ApplySet(set);
        }

        public void ApplySet(AnimationSet set)
        {
            if (overrides == null) return;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.overridesCount);
            overrides.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                var key = pairs[i].Key;
                string slot = key.name.StartsWith(AnimationSlots.SlotPrefix) ? key.name.Substring(AnimationSlots.SlotPrefix.Length) : key.name;
                AnimationClip clip = set != null ? set.Get(slot) : null;
                if (clip == null && fallbackSet != null) clip = fallbackSet.Get(slot);
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, clip);
            }
            overrides.ApplyOverrides(pairs);
            ActiveSet = set;
        }

        public AnimationClip GetClip(string slot)
        {
            if (overrides == null || !slotKeys.TryGetValue(slot, out var key)) return null;
            var c = overrides[key];
            return c != null ? c : key;
        }

        public float GetClipLength(string slot)
        {
            var c = GetClip(slot);
            return c != null ? c.length : 0f;
        }

        /// <summary>Play an action slot so that it lasts exactly <paramref name="duration"/> seconds.</summary>
        public void PlayAction(string slot, float duration, float fade = 0.08f)
        {
            float len = GetClipLength(slot);
            actionBaseSpeed = duration > 0f && len > 0.01f ? len / duration : 1f;
            actionMultiplier = 1f;
            ApplyActionSpeed();
            int hash = Animator.StringToHash(slot);
            if (CurrentState == slot) animator.Play(hash, 0, 0f);
            else animator.CrossFadeInFixedTime(hash, fade, 0);
            CurrentState = slot;
        }

        public void PlayLoop(string slot, float fade = 0.15f)
        {
            if (CurrentState == slot) return;
            animator.CrossFadeInFixedTime(Animator.StringToHash(slot), fade, 0);
            CurrentState = slot;
        }

        public void ReturnToLocomotion(float fade = 0.18f)
        {
            if (CurrentState == AnimationSlots.LocomotionState) return;
            animator.CrossFadeInFixedTime(Animator.StringToHash(AnimationSlots.LocomotionState), fade, 0);
            CurrentState = AnimationSlots.LocomotionState;
        }

        /// <summary>0 freezes the current action (charging), 1 is normal.</summary>
        public void SetActionSpeedMultiplier(float m)
        {
            actionMultiplier = m;
            ApplyActionSpeed();
        }

        void ApplyActionSpeed() => animator.SetFloat(ActionSpeedId, actionBaseSpeed * actionMultiplier);

        /// <param name="blend">Local-space velocity in speed-tier units (1 walk, 2 run, 3 sprint, 4 surge).</param>
        public void SetLocomotion(Vector2 blend, bool grounded, float dt)
        {
            animator.SetFloat(MoveXId, blend.x, 0.08f, dt);
            animator.SetFloat(MoveZId, blend.y, 0.08f, dt);
            animator.SetBool(GroundedId, grounded);
        }

        public void HitStop(float seconds) => hitStopTimer = Mathf.Max(hitStopTimer, seconds);

        public void SetVisualYaw(float degrees, bool instant = false)
        {
            visualYawTarget = degrees;
            if (instant) visualYaw = degrees;
        }

        void Update()
        {
            if (hitStopTimer > 0f)
            {
                hitStopTimer -= Time.deltaTime;
                animator.speed = 0f;
            }
            else animator.speed = 1f;

            if (visualRoot != null)
            {
                visualYaw = Mathf.MoveTowardsAngle(visualYaw, visualYawTarget, 1440f * Time.deltaTime);
                visualRoot.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
            }
        }
    }
}
