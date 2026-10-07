using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Bone-chain cloth (the technique RE Engine-style coats use): the coat mesh stays high resolution and is
    /// smoothly skinned to a set of hidden bone chains; only the chain joints are simulated (Verlet), so the coat
    /// can never break into shards. Per frame, after animation/IK: reset chains to follow the body (the target
    /// shape) → integrate particles with gravity, wind, turbulence and air drag → shape stiffness toward the target
    /// → constraints (segment length, bend limit, body capsules, ground, neighbouring-chain spacing) → rotate the
    /// bones onto the particles. Chains are found by name (CoatChain_##_i, CoatSleeve_Side_i; built by
    /// Tools/Blender/rig_subject143.py).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class ChainCloth : MonoBehaviour
    {
        public enum Preset { Balanced, Flowing, Heavy, Hair, Custom }

        [Tooltip("Flowing = Journey-like (light, airy, wind-driven). Heavy = RE-like weighty coat. Hair = springy strands " +
                 "that keep their shape (RE Strand-style chain hair). Custom = use the values below.")]
        public Preset preset = Preset.Balanced;
        [Tooltip("Bone name prefixes this component simulates (chains are named <prefix>##_<index>).")]
        public string[] chainPrefixes = { "CoatChain_", "CoatSleeve_" };
        [Tooltip("Keep neighbouring chains spaced (a coat's panels). Off for independent strands.")]
        public bool linkNeighbours = true;
        [Tooltip("Collide with a sphere-capped capsule around the skull (hair).")]
        public bool collideHead;
        public float headRadius = 0.07f;

        [Header("Material")]
        public float gravityScale = 1f;
        [Range(0f, 1f)] public float damping = 0.12f;
        [Tooltip("Pull toward the body-following shape at the chain root / tip (per 1/60 s).")]
        [Range(0f, 1f)] public float stiffnessRoot = 0.25f;
        [Range(0f, 1f)] public float stiffnessTip = 0.03f;
        [Tooltip("Air resistance: acceleration per m/s of joint speed. Makes the coat trail when he moves.")]
        public float airDrag = 1.1f;
        [Tooltip("Fraction of the body's movement applied directly to the cloth (limits lag on fast moves).")]
        [Range(0f, 1f)] public float inertiaTransfer = 0.25f;
        public float maxBendAngle = 80f;
        [Range(0f, 1f)] public float lateralStiffness = 0.5f;

        [Header("Wind")]
        public Vector3 windDirection = new Vector3(1f, 0f, 0.35f);
        public float windStrength = 0.8f;
        public float gustStrength = 1.5f;
        public float gustFrequency = 0.35f;
        public float turbulence = 0.6f;

        [Header("Collision")]
        public float particleRadius = 0.035f;
        public float groundClearance = 0.015f;
        [Tooltip("Body movement per frame above which the cloth is carried along (flash steps, teleports).")]
        public float teleportDistance = 0.45f;
        [Tooltip("Body movement per frame above which the cloth simply snaps to the body (respawns, warps).")]
        public float snapDistance = 2.5f;

        [Header("Solver")]
        public float substepRate = 90f;
        [Range(1, 6)] public int iterations = 3;

        class Joint
        {
            public Transform bone;          // null for the chain's tip
            public Vector3 pos, prev, target;
            public float restLength;        // to the previous joint
            public Vector3 restLocalDir;    // bone-local vector to the next joint (bones only)
            public Quaternion restLocalRot;
            public float stiffness;
        }

        class Chain
        {
            public string name;
            public bool sleeve;
            public float angle;             // around the body (torso chains), for neighbour links
            public readonly List<Joint> joints = new List<Joint>();
        }

        struct Capsule { public Transform a, b; public float radius, extend; }

        bool HasPrefix(string n)
        {
            foreach (var p in chainPrefixes) if (!string.IsNullOrEmpty(p) && n.StartsWith(p)) return true;
            return false;
        }

        readonly List<Chain> chains = new List<Chain>();
        readonly List<(Chain a, Chain b)> neighbours = new List<(Chain, Chain)>();
        readonly List<Capsule> capsules = new List<Capsule>();
        Animator anim;
        NightfarerCharacter character;
        Vector3 lastRoot;
        float accumulator;
        bool started;

        public int ChainCount => chains.Count;
        public float MaxStretch { get; private set; }

        public void ApplyPreset()
        {
            switch (preset)
            {
                case Preset.Flowing:
                    gravityScale = 0.55f; damping = 0.06f; stiffnessRoot = 0.15f; stiffnessTip = 0.01f; airDrag = 2.2f;
                    inertiaTransfer = 0.1f; maxBendAngle = 100f; lateralStiffness = 0.35f;
                    windStrength = 1.6f; gustStrength = 3f; turbulence = 1.2f;
                    break;
                case Preset.Heavy:
                    gravityScale = 1.35f; damping = 0.2f; stiffnessRoot = 0.35f; stiffnessTip = 0.06f; airDrag = 0.7f;
                    inertiaTransfer = 0.45f; maxBendAngle = 60f; lateralStiffness = 0.65f;
                    windStrength = 0.4f; gustStrength = 0.6f; turbulence = 0.2f;
                    break;
                case Preset.Hair:
                    gravityScale = 0.25f; damping = 0.22f; stiffnessRoot = 0.85f; stiffnessTip = 0.45f; airDrag = 1.6f;
                    inertiaTransfer = 0.65f; maxBendAngle = 14f; lateralStiffness = 0f;
                    windStrength = 0.6f; gustStrength = 1.1f; turbulence = 0.9f;
                    particleRadius = 0.012f;
                    break;
                case Preset.Balanced:
                    gravityScale = 1f; damping = 0.12f; stiffnessRoot = 0.25f; stiffnessTip = 0.03f; airDrag = 1.1f;
                    inertiaTransfer = 0.25f; maxBendAngle = 80f; lateralStiffness = 0.5f;
                    windStrength = 0.8f; gustStrength = 1.5f; turbulence = 0.6f;
                    break;
            }
        }

        void OnValidate()
        {
            if (preset != Preset.Custom) ApplyPreset();
        }

        void Awake()
        {
            if (preset != Preset.Custom) ApplyPreset();
            anim = GetComponentInChildren<Animator>();
            if (anim == null) anim = GetComponentInParent<Animator>();
            character = GetComponentInParent<NightfarerCharacter>();
            BuildChains();
            BuildCapsules();
        }

        void BuildChains()
        {
            var byName = new Dictionary<string, SortedDictionary<int, Transform>>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!HasPrefix(t.name)) continue;
                int us = t.name.LastIndexOf('_');
                if (us < 0 || !int.TryParse(t.name.Substring(us + 1), out int idx)) continue;
                string key = t.name.Substring(0, us);
                if (!byName.TryGetValue(key, out var d)) byName[key] = d = new SortedDictionary<int, Transform>();
                d[idx] = t;
            }
            Vector3 axis = transform.position;
            foreach (var kv in byName)
            {
                var bones = new List<Transform>(kv.Value.Values);
                if (bones.Count < 2) continue;
                var c = new Chain { name = kv.Key, sleeve = kv.Key.StartsWith("CoatSleeve_") };
                for (int i = 0; i < bones.Count; i++)
                {
                    var b = bones[i];
                    Vector3 next = i + 1 < bones.Count ? bones[i + 1].position
                        : b.position + (b.position - bones[i - 1].position);   // tip: extend the last segment
                    c.joints.Add(new Joint
                    {
                        bone = b,
                        restLocalRot = b.localRotation,
                        restLocalDir = b.InverseTransformPoint(next),
                        restLength = i == 0 ? 0f : Vector3.Distance(b.position, bones[i - 1].position),
                    });
                }
                var last = bones[bones.Count - 1];
                Vector3 tip = last.position + (last.position - bones[bones.Count - 2].position);
                c.joints.Add(new Joint { bone = null, restLength = Vector3.Distance(tip, last.position) });
                int n = c.joints.Count;
                for (int i = 0; i < n; i++) c.joints[i].stiffness = Mathf.Lerp(stiffnessRoot, stiffnessTip, i / (float)(n - 1));
                Vector3 local = transform.InverseTransformPoint(bones[bones.Count - 1].position);
                c.angle = Mathf.Atan2(local.x, -local.z);
                chains.Add(c);
            }
            if (!linkNeighbours) return;
            // Neighbour links between adjacent torso chains (sorted around the body).
            var torso = chains.FindAll(c => !c.sleeve);
            torso.Sort((x, y) => x.angle.CompareTo(y.angle));
            float maxGap = Mathf.PI * 2f / Mathf.Max(1, torso.Count) * 2.2f;
            for (int i = 0; i < torso.Count; i++)
            {
                var a = torso[i];
                var b = torso[(i + 1) % torso.Count];
                if (a == b) continue;
                float gap = Mathf.Repeat(b.angle - a.angle, Mathf.PI * 2f);
                if (gap <= maxGap) neighbours.Add((a, b));
            }
        }

        void BuildCapsules()
        {
            if (anim == null || !anim.isHuman) return;
            void Add(HumanBodyBones a, HumanBodyBones b, float r)
            {
                var ta = anim.GetBoneTransform(a);
                var tb = anim.GetBoneTransform(b);
                if (ta != null && tb != null) capsules.Add(new Capsule { a = ta, b = tb, radius = r });
            }
            Add(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.12f);
            Add(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.11f);
            Add(HumanBodyBones.Chest, HumanBodyBones.Neck, 0.11f);
            Add(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.075f);
            Add(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.075f);
            Add(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.06f);
            Add(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.06f);
            Add(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.07f);
            Add(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.08f);   // the thorned claw arm is thick
            Add(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.05f);
            Add(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.045f);
            if (collideHead)
            {
                var neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (neck != null && head != null) capsules.Add(new Capsule { a = neck, b = head, radius = headRadius, extend = 0.09f });
            }
        }

        void LateUpdate()
        {
            if (chains.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 1. Body-following target shape.
            foreach (var c in chains)
            {
                for (int i = 0; i < c.joints.Count; i++)
                    if (c.joints[i].bone != null) c.joints[i].bone.localRotation = c.joints[i].restLocalRot;
                c.joints[0].target = c.joints[0].bone.position;
                for (int i = 0; i + 1 < c.joints.Count; i++)
                {
                    var j = c.joints[i];
                    c.joints[i + 1].target = j.bone.TransformPoint(j.restLocalDir);
                }
            }

            Vector3 root = transform.position;
            if (started && (root - lastRoot).magnitude > snapDistance) started = false;   // warped: start fresh
            if (!started)
            {
                foreach (var c in chains) foreach (var j in c.joints) j.pos = j.prev = j.target;
                lastRoot = root;
                started = true;
            }
            Vector3 rootDelta = root - lastRoot;
            lastRoot = root;
            bool teleport = rootDelta.magnitude > teleportDistance;
            Vector3 carry = rootDelta * (teleport ? 0.9f : inertiaTransfer);
            foreach (var c in chains)
                for (int i = 1; i < c.joints.Count; i++)
                {
                    c.joints[i].pos += carry;
                    c.joints[i].prev += carry;
                }

            // 2-3. Fixed substeps.
            float h = 1f / Mathf.Max(30f, substepRate);
            accumulator = Mathf.Min(accumulator + dt, h * 8f);
            float groundY = (character != null ? character.transform.position.y : root.y) + groundClearance + particleRadius;
            Vector3 wind = Wind();
            bool stepped = false;
            while (accumulator >= h)
            {
                accumulator -= h;
                Step(h, wind, groundY);
                stepped = true;
            }
            if (!stepped) Project();   // no substep due this frame: still pin roots and keep lengths

            // 4. Bones onto particles.
            MaxStretch = 0f;
            foreach (var c in chains)
            {
                for (int i = 0; i + 1 < c.joints.Count; i++)
                {
                    var j = c.joints[i];
                    Vector3 from = j.bone.TransformVector(j.restLocalDir);
                    Vector3 to = c.joints[i + 1].pos - j.bone.position;
                    if (to.sqrMagnitude > 1e-8f && from.sqrMagnitude > 1e-8f)
                        j.bone.rotation = Quaternion.FromToRotation(from, to) * j.bone.rotation;
                    float rest = c.joints[i + 1].restLength;
                    if (rest > 1e-4f) MaxStretch = Mathf.Max(MaxStretch, to.magnitude / rest);
                }
            }
        }

        void Project()
        {
            foreach (var c in chains)
            {
                c.joints[0].pos = c.joints[0].target;
                for (int i = 1; i < c.joints.Count; i++)
                {
                    var j = c.joints[i];
                    Vector3 seg = j.pos - c.joints[i - 1].pos;
                    float l = seg.magnitude;
                    if (l > 1e-6f) j.pos = c.joints[i - 1].pos + seg * (j.restLength / l);
                }
            }
        }

        Vector3 Wind()
        {
            float t = Time.time * gustFrequency;
            float gust = Mathf.PerlinNoise(t, 0.37f);
            Vector3 dir = windDirection.sqrMagnitude > 0f ? windDirection.normalized : Vector3.right;
            return dir * (windStrength + gust * gust * gustStrength);
        }

        void Step(float h, Vector3 wind, float groundY)
        {
            Vector3 gravity = Physics.gravity * gravityScale;
            float time = Time.time;
            foreach (var c in chains)
            {
                c.joints[0].pos = c.joints[0].target;   // root follows the body
                for (int i = 1; i < c.joints.Count; i++)
                {
                    var j = c.joints[i];
                    Vector3 vel = (j.pos - j.prev) / h;
                    j.prev = j.pos;
                    Vector3 turb = new Vector3(
                        Mathf.PerlinNoise(time * 1.7f + i, c.angle * 3f) - 0.5f, 0f,
                        Mathf.PerlinNoise(c.angle * 3f, time * 1.7f + i) - 0.5f) * (turbulence * 2f);
                    Vector3 acc = gravity + wind + turb - vel * airDrag;
                    j.pos += vel * (1f - damping) * h + acc * (h * h);
                    float k = 1f - Mathf.Pow(1f - j.stiffness, h * 60f);
                    j.pos = Vector3.Lerp(j.pos, j.target, k);
                }
            }

            for (int it = 0; it < iterations; it++)
            {
                // Neighbouring chains keep their spacing (no gaps opening, no crossing).
                foreach (var (a, b) in neighbours)
                {
                    int n = Mathf.Min(a.joints.Count, b.joints.Count);
                    for (int i = 1; i < n; i++)
                    {
                        var ja = a.joints[i];
                        var jb = b.joints[i];
                        float rest = Vector3.Distance(ja.target, jb.target);
                        Vector3 d = jb.pos - ja.pos;
                        float len = d.magnitude;
                        if (len < 1e-5f) continue;
                        float goal = Mathf.Clamp(len, rest * 0.65f, rest * 1.25f);
                        Vector3 corr = d * ((len - goal) / len) * 0.5f * lateralStiffness;
                        ja.pos += corr;
                        jb.pos -= corr;
                    }
                }

                foreach (var c in chains)
                {
                    for (int i = 1; i < c.joints.Count; i++)
                    {
                        var j = c.joints[i];
                        var parent = c.joints[i - 1];
                        // Bend limit relative to the body-following shape.
                        Vector3 cur = j.pos - parent.pos;
                        Vector3 rest = j.target - parent.target;
                        if (cur.sqrMagnitude > 1e-8f && rest.sqrMagnitude > 1e-8f)
                        {
                            float angle = Vector3.Angle(cur, rest);
                            if (angle > maxBendAngle)
                                cur = Vector3.RotateTowards(cur, rest, (angle - maxBendAngle) * Mathf.Deg2Rad, 0f);
                        }
                        j.pos = parent.pos + cur;

                        // Body capsules.
                        foreach (var cap in capsules)
                        {
                            Vector3 a = cap.a.position, b = cap.b.position;
                            if (cap.extend > 0f) b += (b - a).normalized * cap.extend;
                            Vector3 ab = b - a;
                            float t = Mathf.Clamp01(Vector3.Dot(j.pos - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                            Vector3 closest = a + ab * t;
                            Vector3 off = j.pos - closest;
                            float min = cap.radius + particleRadius;
                            float dist = off.magnitude;
                            if (dist < min)
                                j.pos = closest + (dist > 1e-5f ? off / dist : (j.target - closest).normalized) * min;
                        }
                        if (j.pos.y < groundY) j.pos.y = groundY;

                        // Inextensible segments (last, so lengths always hold).
                        Vector3 seg = j.pos - parent.pos;
                        float l = seg.magnitude;
                        if (l > 1e-6f) j.pos = parent.pos + seg * (j.restLength / l);
                    }
                }
            }
        }
    }
}
