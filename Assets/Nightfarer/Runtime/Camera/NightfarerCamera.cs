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

        [Header("Options")]
        public bool invertY;

        float yaw, pitch = 12f, currentDistance;
        float shakeAmplitude, shakeTime, shakeDuration;
        Vector3 pivot;
        float switchCooldown;
        bool recentering;

        public float Yaw => yaw;

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

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            float wanted = target != null ? lockedDistance : distance;
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
            transform.SetPositionAndRotation(pivot + back * currentDistance + shake, rot);

            if (cam != null)
            {
                float fov = baseFov + (character.IsSurging ? surgeFovBonus : character.IsSprinting ? sprintFovBonus : 0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * dt));
            }
        }
    }
}
