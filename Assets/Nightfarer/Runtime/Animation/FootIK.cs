using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Foot IK, applied after the Animator and before the arm layer: each foot is raycast onto the ground under
    /// it, the pelvis drops so the lower foot can reach (slopes, steps, ledges), both legs are solved with
    /// two-bone IK keeping the clip's foot lift, and planted feet tilt to the ground's slope. States can also
    /// place the feet directly for a frame (climbing walls) with <see cref="SetOverride"/>.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class FootIK : MonoBehaviour
    {
        public NightfarerCharacter character;
        public LayerMask groundMask = ~0;
        [Tooltip("Furthest the pelvis may drop to let a foot reach lower ground.")]
        public float maxPelvisDrop = 0.3f;
        public float rayStart = 0.5f;
        public float rayDepth = 0.55f;
        public float blendSpeed = 7f;
        [Range(0f, 1f)] public float slopeAlign = 0.85f;

        Animator anim;
        Transform hips, lU, lL, lF, rU, rL, rF;
        float ankleHeight = 0.07f;
        float weight, pelvis;
        Vector3 overrideL, overrideR, hintL, hintR;
        bool customHints;
        float overrideWeight;
        int overrideFrame = -1;
        readonly RaycastHit[] hits = new RaycastHit[8];

        public float Weight => weight;
        public float AnkleHeight => ankleHeight;
        public float PelvisOffset => pelvis;
        /// <summary>Gap between each sole and the ground after the last solve (tests/debug).</summary>
        public float LeftGap { get; private set; }
        public float RightGap { get; private set; }

        void Awake() => Init();

        void Init()
        {
            if (character == null) character = GetComponent<NightfarerCharacter>();
            anim = character != null && character.animatorDriver != null ? character.animatorDriver.animator : GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) return;
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            lU = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            lL = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lF = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rU = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rL = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rF = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            // Bind pose (before the first animation update): ankle height above the soles (lowest point of the body mesh).
            if (lF != null && rF != null)
            {
                float sole = transform.position.y;
                foreach (var smr in anim.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.sharedMesh == null || smr.name.StartsWith("Coat")) continue;
                    var b = smr.sharedMesh.bounds;
                    float lowest = float.MaxValue;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        lowest = Mathf.Min(lowest, smr.transform.TransformPoint(corner).y);
                    }
                    sole = lowest;
                    break;
                }
                ankleHeight = Mathf.Clamp(Mathf.Min(lF.position.y, rF.position.y) - sole, 0.03f, 0.15f);
            }
        }

        /// <summary>Place both feet at world targets this frame (ankle positions), blended by weight.</summary>
        public void SetOverride(Vector3 leftAnkle, Vector3 rightAnkle, float w)
        {
            overrideL = leftAnkle;
            overrideR = rightAnkle;
            overrideWeight = Mathf.Clamp01(w);
            overrideFrame = Time.frameCount;
            customHints = false;
        }

        /// <summary>
        /// Plant both feet on the ground at these points this frame (crouches, kneeling): the ground height is found
        /// under each point; the knee directions say where each knee points. Extra heights lift a foot (a kneeling
        /// leg's foot rests on its instep).
        /// </summary>
        public void SetPlant(Vector3 leftPoint, Vector3 rightPoint, float w, Vector3 leftKnee, Vector3 rightKnee, float leftExtra = 0f, float rightExtra = 0f)
        {
            float rootY = transform.position.y;
            Vector3 l = Ground(leftPoint, rootY, out var lh, out _) ? lh : new Vector3(leftPoint.x, rootY, leftPoint.z);
            Vector3 r = Ground(rightPoint, rootY, out var rh, out _) ? rh : new Vector3(rightPoint.x, rootY, rightPoint.z);
            l.y += ankleHeight + leftExtra;
            r.y += ankleHeight + rightExtra;
            SetOverride(l, r, w);
            hintL = l + leftKnee.normalized * 0.5f;
            hintR = r + rightKnee.normalized * 0.5f;
            customHints = true;
        }

        bool WantsGround()
        {
            if (character == null || !character.Motor.Grounded) return false;
            var s = character.State;
            return s is LocomotionState || s is LandingState || s is HeroLandingState || s is SuperJumpChargeState ||
                   s is DrinkState || s is AttackState || s is HitReactState || s is AbilityState;
        }

        void LateUpdate()
        {
            if (anim == null || hips == null) Init();
            if (hips == null || lF == null || rF == null) return;
            float dt = Time.deltaTime;
            Vector3 fwd = character != null ? character.transform.forward : transform.forward;

            if (overrideFrame == Time.frameCount && overrideWeight > 0f)
            {
                weight = 0f;
                pelvis = 0f;
                Vector3 up = Vector3.up * 0.35f;
                Leg(lU, lL, lF, overrideL, customHints ? hintL : overrideL + fwd * 0.35f + up, overrideWeight, Vector3.zero, 0f);
                Leg(rU, rL, rF, overrideR, customHints ? hintR : overrideR + fwd * 0.35f + up, overrideWeight, Vector3.zero, 0f);
                return;
            }

            weight = Mathf.MoveTowards(weight, WantsGround() ? 1f : 0f, blendSpeed * dt);
            if (weight <= 0.001f)
            {
                pelvis = 0f;
                return;
            }

            float rootY = transform.position.y;
            Vector3 lAnim = lF.position, rAnim = rF.position;
            bool lOk = Ground(lAnim, rootY, out Vector3 lHit, out Vector3 lN);
            bool rOk = Ground(rAnim, rootY, out Vector3 rHit, out Vector3 rN);
            float lOff = lOk ? lHit.y - rootY : 0f, rOff = rOk ? rHit.y - rootY : 0f;
            float want = Mathf.Clamp(Mathf.Min(0f, Mathf.Min(lOff, rOff)), -maxPelvisDrop, 0f);
            pelvis = Mathf.Lerp(pelvis, want, 1f - Mathf.Exp(-14f * dt));
            hips.position += Vector3.up * (pelvis * weight);

            // Keep the clip's foot lift (steps), but never sink below the ground. Crouching landings keep both feet down.
            bool planted = character != null && (character.State is LandingState || character.State is LandRollState);
            float lLift = planted ? 0f : Mathf.Max(0f, lAnim.y - rootY - ankleHeight);
            float rLift = planted ? 0f : Mathf.Max(0f, rAnim.y - rootY - ankleHeight);
            if (lOk) Leg(lU, lL, lF, new Vector3(lAnim.x, lHit.y + ankleHeight + lLift, lAnim.z), lL.position + fwd * 0.4f, weight, lN, PlantFactor(lLift));
            if (rOk) Leg(rU, rL, rF, new Vector3(rAnim.x, rHit.y + ankleHeight + rLift, rAnim.z), rL.position + fwd * 0.4f, weight, rN, PlantFactor(rLift));
            LeftGap = lOk ? lF.position.y - ankleHeight - lHit.y : 0f;
            RightGap = rOk ? rF.position.y - ankleHeight - rHit.y : 0f;
        }

        static float PlantFactor(float lift) => 1f - Mathf.Clamp01(lift / 0.08f);

        void Leg(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 hint, float w, Vector3 normal, float plant)
        {
            Quaternion footRot = foot.rotation;
            TwoBoneIK.Solve(upper, lower, foot, target, hint, w);
            foot.rotation = footRot;
            if (plant > 0f && normal.sqrMagnitude > 0.5f)
                foot.rotation = Quaternion.Slerp(footRot, Quaternion.FromToRotation(Vector3.up, normal) * footRot, plant * slopeAlign * w);
        }

        bool Ground(Vector3 footPos, float rootY, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            Vector3 origin = new Vector3(footPos.x, rootY + rayStart, footPos.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, rayStart + rayDepth, groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || hits[i].distance <= 0f) continue;
                if (c.GetComponentInParent<IDamageable>() != null) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    point = hits[i].point;
                    normal = hits[i].normal;
                }
            }
            return best < float.MaxValue && normal.y > 0.55f;
        }
    }
}
