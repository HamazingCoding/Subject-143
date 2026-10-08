using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Free orbit camera that frames the lock-on target when locked, with sphere-cast collision and a small
    /// sprint FOV kick. Lives on its own rig object (not parented to the player) and runs in LateUpdate.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NightfarerCamera : MonoBehaviour
    {
        public NightfarerCharacter character;
        public Camera cam;

        [Header("Framing")]
        public float pivotHeight = 1.55f;
        public float distance = 4.2f;
        public float lockedDistance = 4.6f;
        public float minDistance = 0.6f;
        public float followSharpness = 18f;

        [Header("Look")]
        public float mouseSensitivity = 0.12f;  // degrees per pixel
        public float stickSpeed = 200f;          // degrees per second
        public float pitchMin = -45f, pitchMax = 70f;

        [Header("Lock-on")]
        public float lockSharpness = 9f;
        public float lockPitchBias = 8f;
        public float switchFlickPixels = 60f;
        public float switchFlickStick = 0.8f;

        [Header("Collision")]
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        [Header("FOV")]
        public float baseFov = 55f;
        public float sprintFovBonus = 5f;
        public float surgeFovBonus = 9f;
        [Tooltip("Drive the FOV from actual speed (calm -> fluid -> aggressive) instead of the sprint/surge flags.")]
        public bool fovBySpeed = true;
        [Tooltip("FOV at walk, run, sprint and surge speed (interpolated by speed).")]
        public Vector4 speedFov = new Vector4(55f, 58f, 63f, 70f);

        [Header("Speed feel")]
        [Tooltip("Extra camera distance at surge speed.")]
        public float speedPullBack = 0.6f;
        [Tooltip("Roll into turns at surge speed (degrees, at the fastest turns).")]
        public float turnRoll = 2f;
        [Tooltip("Camera drop per m/s of landing impact (metres).")]
        public float landingDipPerSpeed = 0.018f;
        public float maxLandingDip = 0.3f;

        [Header("Options")]
        public bool invertY;

        float yaw, pitch = 12f, currentDistance;
        float pull, roll, lastHeading, dip, dipVel;
        bool headingInit;
        float shakeAmplitude, shakeTime, shakeDuration;
        Vector3 pivot;
        float switchCooldown;
        bool recentering;

        public float Yaw => yaw;
        public float CurrentFov => cam != null ? cam.fieldOfView : baseFov;
        public float Roll => roll;
        public float Dip => dip;

        /// <summary>Drop the camera on landing in proportion to the impact, then spring back.</summary>
        public void LandingDip(float impactSpeed) => dipVel -= Mathf.Min(Mathf.Abs(impactSpeed) * landingDipPerSpeed * 6f, maxLandingDip * 6f);

        /// <summary>Brief positional shake (hits, ultimates, heavy landings).</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmplitude * (1f - shakeTime / Mathf.Max(0.01f, shakeDuration))) return;
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTime = 0f;
        }

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (character != null)
            {
                yaw = character.transform.eulerAngles.y;
                pivot = character.transform.position + Vector3.up * pivotHeight;
            }
            currentDistance = distance;
        }

        /// <summary>Swing the camera behind the character (Elden Ring does this when lock-on finds nothing).</summary>
        public void Recenter() => recentering = true;

        public void SnapBehind()
        {
            if (character == null) return;
            yaw = character.transform.eulerAngles.y;
            pivot = character.transform.position + Vector3.up * pivotHeight;
        }

        void LateUpdate()
        {
            if (character == null) return;
            float dt = Time.deltaTime;
            var input = character.CurrentInput;
            var lockOn = character.LockOn;
            var target = lockOn != null ? lockOn.Current : null;

            // Speed feel: pull back with speed, roll into hard turns at surge, dip on landings.
            var cfgSpeed = character.Config;
            float spd = character.Motor.PlanarSpeed;
            float fast01 = cfgSpeed != null ? Mathf.InverseLerp(cfgSpeed.runSpeed, cfgSpeed.surgeSpeed, spd) : 0f;
            pull = Mathf.Lerp(pull, speedPullBack * fast01, 1f - Mathf.Exp(-3f * dt));
            Vector3 pv = character.Motor.PlanarVelocity;
            float heading = pv.sqrMagnitude > 0.25f ? Mathf.Atan2(pv.x, pv.z) * Mathf.Rad2Deg : lastHeading;
            float rate = headingInit && dt > 0f ? Mathf.DeltaAngle(lastHeading, heading) / dt : 0f;
            lastHeading = heading; headingInit = true;
            float surge01 = cfgSpeed != null ? Mathf.InverseLerp(cfgSpeed.sprintSpeed, cfgSpeed.surgeSpeed, spd) : 0f;
            float rollTarget = Mathf.Clamp(-rate * 0.02f, -turnRoll, turnRoll) * surge01;
            roll = Mathf.Lerp(roll, rollTarget, 1f - Mathf.Exp(-4f * dt));
            dipVel += (-140f * dip - 2f * Mathf.Sqrt(140f) * 0.7f * dipVel) * dt;
            dip = Mathf.Clamp(dip + dipVel * dt, -maxLandingDip, maxLandingDip);

            Vector3 desiredPivot = character.transform.position + Vector3.up * pivotHeight;
            pivot = Vector3.Lerp(pivot, desiredPivot, 1f - Mathf.Exp(-followSharpness * dt));

            switchCooldown -= dt;
            if (target != null)
            {
                recentering = false;
                float flick = input.lookMouse.x / Mathf.Max(1f, switchFlickPixels);
                if (Mathf.Abs(input.lookStick.x) > switchFlickStick) flick = input.lookStick.x;
                if (switchCooldown <= 0f && Mathf.Abs(flick) >= 1f && lockOn.Switch(transform, flick))
                    switchCooldown = 0.35f;
                target = lockOn.Current;

                Vector3 mid = Vector3.Lerp(pivot, target.Point, 0.5f);
                Vector3 dir = target.Point - pivot;
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                float desiredYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float desiredPitch = Mathf.Clamp(-Mathf.Atan2(mid.y - pivot.y, flat.magnitude * 0.5f) * Mathf.Rad2Deg + lockPitchBias, pitchMin, pitchMax);
                float k = 1f - Mathf.Exp(-lockSharpness * dt);
                yaw = Mathf.LerpAngle(yaw, desiredYaw, k);
                pitch = Mathf.LerpAngle(pitch, desiredPitch, k);
            }
            else
            {
                Vector2 look = input.lookMouse * mouseSensitivity + input.lookStick * (stickSpeed * dt);
                if (look.sqrMagnitude > 0.0001f) recentering = false;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - (invertY ? -look.y : look.y), pitchMin, pitchMax);
                if (recentering)
                {
                    float k = 1f - Mathf.Exp(-12f * dt);
                    yaw = Mathf.LerpAngle(yaw, character.transform.eulerAngles.y, k);
                    pitch = Mathf.LerpAngle(pitch, 12f, k);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, character.transform.eulerAngles.y)) < 1f) recentering = false;
                }
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, roll);
            float wanted = (target != null ? lockedDistance : distance) + pull;
            float allowed = wanted;
            Vector3 back = rot * Vector3.back;
            foreach (var h in Physics.SphereCastAll(pivot, collisionRadius, back, wanted, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(character.transform) || h.distance <= 0f) continue;
                if (h.collider.GetComponentInParent<IDamageable>() != null) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(minDistance, h.distance));
            }
            // Pull in instantly on collision, ease back out.
            currentDistance = allowed < currentDistance ? allowed : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-6f * dt));

            Vector3 shake = Vector3.zero;
            if (shakeTime < shakeDuration)
            {
                shakeTime += Time.unscaledDeltaTime;
                float fall = 1f - Mathf.Clamp01(shakeTime / shakeDuration);
                float t = Time.unscaledTime * 35f;
                shake = rot * new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * shakeAmplitude * fall);
            }
            transform.SetPositionAndRotation(pivot + back * currentDistance + shake + Vector3.up * dip, rot);

            if (cam != null)
            {
                float fov = baseFov + (character.IsSurging ? surgeFovBonus : character.IsSprinting ? sprintFovBonus : 0f);
                if (fovBySpeed && cfgSpeed != null)
                {
                    float tier = character.SpeedTier(spd);   // 1 walk, 2 run, 3 sprint, 4 surge
                    fov = tier <= 1f ? speedFov.x
                        : tier <= 2f ? Mathf.Lerp(speedFov.x, speedFov.y, tier - 1f)
                        : tier <= 3f ? Mathf.Lerp(speedFov.y, speedFov.z, tier - 2f)
                        : Mathf.Lerp(speedFov.z, speedFov.w, tier - 3f);
                }
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * dt));
            }
        }
    }
}
