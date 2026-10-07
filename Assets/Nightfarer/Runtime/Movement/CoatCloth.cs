using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Cloth setup for Subject 143's coat (Unity Cloth on the coat's SkinnedMeshRenderer). The coat is skinned to
    /// the torso; here each cloth vertex gets a max distance from its skinned position: zero across the collar,
    /// shoulders and upper back (pinned to the body), ramping to fully free below, so the coat body and the empty
    /// sleeves hang, swing with inertia and blow in the wind. Capsule colliders on the body keep it outside him.
    /// </summary>
    [RequireComponent(typeof(Cloth))]
    public class CoatCloth : MonoBehaviour
    {
        [Header("Pinning (metres, measured from the character's feet / spine)")]
        [Tooltip("Vertices higher than (shoulder height - pinDepth) and within pinHalfWidth of the spine are fixed.")]
        public float pinDepth = 0.14f;
        public float pinHalfWidth = 0.17f;
        [Tooltip("Height of the band below the pinned area over which vertices loosen.")]
        public float blendBand = 0.22f;
        public float freeMaxDistance = 0.9f;
        public float bandMaxDistance = 0.06f;
        [Tooltip("Sleeve tops: vertices within this radius of the upper half of each upper arm are pinned to it.")]
        public float sleeveRadius = 0.14f;
        [Tooltip("How far down the upper arm (0 shoulder, 1 elbow) the sleeve stays attached; it loosens to the elbow.")]
        [Range(0f, 1f)] public float sleeveAttach = 0.55f;

        [Header("Physics")]
        public float stretchingStiffness = 0.85f;
        public float bendingStiffness = 0.15f;
        public float damping = 0.12f;
        public float friction = 0.4f;
        public float worldVelocityScale = 0.55f;
        public float worldAccelerationScale = 0.9f;
        public float solverFrequency = 120f;

        [Header("Wind")]
        public Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        public float windStrength = 2.2f;
        public float gustStrength = 3.5f;
        public float gustFrequency = 0.35f;
        public float turbulence = 1.2f;
        [Tooltip("Airflow from moving (Unity Cloth has no air drag): acceleration per m/s of character speed, opposite the motion.")]
        public float movementDrag = 1.5f;
        public float maxDragAcceleration = 14f;

        public Cloth Cloth { get; private set; }
        public int PinnedCount { get; private set; }
        public int FreeCount { get; private set; }

        Animator anim;
        NightfarerCharacter character;
        bool configured;
        float teleportDamp;

        void Awake()
        {
            Cloth = GetComponent<Cloth>();
            anim = GetComponentInParent<Animator>();
            character = GetComponentInParent<NightfarerCharacter>();
            Cloth.stretchingStiffness = stretchingStiffness;
            Cloth.bendingStiffness = bendingStiffness;
            Cloth.damping = damping;
            Cloth.friction = friction;
            Cloth.useGravity = true;
            Cloth.worldVelocityScale = worldVelocityScale;
            Cloth.worldAccelerationScale = worldAccelerationScale;
            Cloth.clothSolverFrequency = solverFrequency;
            Cloth.enableContinuousCollision = true;
            BuildColliders();
            if (character != null) character.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (character != null) character.StateChanged -= OnStateChanged;
        }

        // Flash steps move the body several metres in a fraction of a second; let the cloth follow instead of whipping.
        void OnStateChanged(string state)
        {
            if (state == "Flash Step") teleportDamp = 0.25f;
        }

        void Update()
        {
            if (!configured) TryConfigure();
            float t = Time.time * gustFrequency;
            float gust = Mathf.PerlinNoise(t, 0.37f);
            Vector3 dir = windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector3.right;
            Vector3 airflow = Vector3.zero;
            if (character != null)
            {
                Vector3 v = character.Motor.ActualVelocity;
                v.y *= 0.3f;
                airflow = Vector3.ClampMagnitude(-v * movementDrag, maxDragAcceleration);
            }
            Cloth.externalAcceleration = dir * (windStrength + gust * gust * gustStrength) + airflow;
            Cloth.randomAcceleration = new Vector3(turbulence, turbulence * 0.3f, turbulence);

            if (teleportDamp > 0f)
            {
                teleportDamp -= Time.deltaTime;
                Cloth.worldVelocityScale = worldVelocityScale * 0.2f;
                Cloth.worldAccelerationScale = worldAccelerationScale * 0.2f;
            }
            else
            {
                Cloth.worldVelocityScale = worldVelocityScale;
                Cloth.worldAccelerationScale = worldAccelerationScale;
            }
        }

        void TryConfigure()
        {
            var verts = Cloth.vertices;
            if (verts == null || verts.Length == 0 || anim == null || !anim.isHuman) return;
            Transform root = anim.transform;
            var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            var lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (neck == null || lArm == null || rArm == null) return;
            float shoulderY = (root.InverseTransformPoint(lArm.position).y + root.InverseTransformPoint(rArm.position).y) * 0.5f;
            float spineX = root.InverseTransformPoint(neck.position).x;
            float pinY = shoulderY - pinDepth;

            var lElbow = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var rElbow = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);

            // 0 = attached to the upper arm, 1 = free, -1 = not near a sleeve top.
            float SleeveFreedom(Vector3 world)
            {
                float best = -1f;
                foreach (var (sh, el) in new[] { (lArm, lElbow), (rArm, rElbow) })
                {
                    if (el == null) continue;
                    Vector3 a = sh.position, b = el.position, ab = b - a;
                    float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    if (Vector3.Distance(world, a + ab * t) > sleeveRadius) continue;
                    float f = t <= sleeveAttach ? 0f : Mathf.InverseLerp(sleeveAttach, 1f, t);
                    best = best < 0f ? f : Mathf.Min(best, f);
                }
                return best;
            }

            var coeffs = new ClothSkinningCoefficient[verts.Length];
            PinnedCount = FreeCount = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = transform.TransformPoint(verts[i]);
                Vector3 p = root.InverseTransformPoint(world);
                bool overTorso = Mathf.Abs(p.x - spineX) <= pinHalfWidth;
                float sleeve = SleeveFreedom(world);
                float maxDist;
                if (sleeve == 0f || (overTorso && p.y >= pinY)) { maxDist = 0f; PinnedCount++; }
                else if (sleeve > 0f) maxDist = Mathf.Lerp(bandMaxDistance, freeMaxDistance * 0.5f, sleeve);
                else if (overTorso && p.y >= pinY - blendBand)
                    maxDist = Mathf.Lerp(bandMaxDistance, freeMaxDistance * 0.4f, (pinY - p.y) / blendBand);
                else { maxDist = freeMaxDistance; FreeCount++; }
                coeffs[i] = new ClothSkinningCoefficient { maxDistance = maxDist, collisionSphereDistance = 0.01f };
            }
            Cloth.coefficients = coeffs;
            configured = true;
        }

        void BuildColliders()
        {
            if (anim == null || !anim.isHuman) return;
            var caps = new List<CapsuleCollider>();
            void Add(HumanBodyBones from, HumanBodyBones to, float radius)
            {
                var a = anim.GetBoneTransform(from);
                var b = anim.GetBoneTransform(to);
                if (a == null || b == null) return;
                var go = new GameObject("CoatCollider_" + from);
                go.transform.SetParent(a, false);
                go.layer = 2;   // Ignore Raycast: cloth-only helpers stay out of gameplay queries
                var c = go.AddComponent<CapsuleCollider>();
                c.isTrigger = true;
                Vector3 local = a.InverseTransformPoint(b.position);
                float scale = Mathf.Max(1e-4f, a.lossyScale.x);
                c.direction = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
                c.center = local * 0.5f;
                c.radius = radius / scale;
                c.height = local.magnitude + c.radius * 2f;
                caps.Add(c);
            }
            Add(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.12f);
            Add(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.11f);
            Add(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.115f);
            Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.075f);
            Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.075f);
            Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.055f);
            Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.055f);
            Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.075f);   // the thorned claw arm is thick
            Add(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.085f);
            Add(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.05f);
            Add(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.045f);
            Cloth.capsuleColliders = caps.ToArray();
        }
    }
}
