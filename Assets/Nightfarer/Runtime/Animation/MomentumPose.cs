using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Makes the forces on the body visible: the whole body banks into turns in proportion to the sideways
    /// (centripetal) acceleration, pitches forward when accelerating and back when braking. Tilts the hips around
    /// the feet after the Animator; FootIK (which runs next) keeps the feet planted.
    /// </summary>
    [DefaultExecutionOrder(38)]
    public class MomentumPose : MonoBehaviour
    {
        public NightfarerCharacter character;
        [Tooltip("Bank angle as a fraction of the physical lean angle (atan(lateral accel / g)).")]
        public float rollGain = 0.9f;
        public float maxRoll = 28f;
        [Tooltip("Degrees of pitch per m/s^2 of forward (+) or braking (-) acceleration.")]
        public float pitchPerAccel = 1.3f;
        public float maxPitchForward = 12f, maxPitchBack = 16f;
        public float sharpness = 8f;

        Transform hips;
        Vector3 lastVel, accel;
        float roll, pitch;
        bool init;

        public float Roll => roll;
        public float Pitch => pitch;

        void LateUpdate()
        {
            if (character == null) character = GetComponent<NightfarerCharacter>();
            if (hips == null)
            {
                var a = character != null && character.Animator != null ? character.Animator.animator : null;
                if (a == null || !a.isHuman) return;
                hips = a.GetBoneTransform(HumanBodyBones.Hips);
                if (hips == null) return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 v = character.Motor.PlanarVelocity;
            if (!init) { lastVel = v; init = true; }
            accel = Vector3.Lerp(accel, (v - lastVel) / dt, 1f - Mathf.Exp(-12f * dt));
            lastVel = v;

            float speed = v.magnitude, tr = 0f, tp = 0f;
            var s = character.State;
            bool active = character.Motor.Grounded &&
                          (s is LocomotionState || s is SkidState || s is LandingState || (s is SuperJumpChargeState sj && sj.Running));
            if (active && speed > 0.5f)
            {
                Vector3 fwd = v / speed;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                float lat = Vector3.Dot(accel, right), lon = Vector3.Dot(accel, fwd);
                // Banking grows with speed: a run turns cleanly, a surge throws the whole body into the turn.
                var cfg = character.Config;
                float speedFactor = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(cfg.runSpeed, cfg.surgeSpeed, speed)) * Mathf.InverseLerp(0.5f, cfg.runSpeed, speed);
                tr = Mathf.Clamp(Mathf.Atan2(lat, 9.81f) * Mathf.Rad2Deg * rollGain * speedFactor, -maxRoll, maxRoll);
                tp = Mathf.Clamp(lon * pitchPerAccel, -maxPitchBack, maxPitchForward);
            }
            float k = 1f - Mathf.Exp(-sharpness * dt);
            roll = Mathf.Lerp(roll, tr, k);
            pitch = Mathf.Lerp(pitch, tp, k);
            if (Mathf.Abs(roll) < 0.01f && Mathf.Abs(pitch) < 0.01f) return;
            Vector3 pivot = character.transform.position;
            hips.RotateAround(pivot, character.transform.right, pitch);
            hips.RotateAround(pivot, character.transform.forward, -roll);
        }
    }
}
