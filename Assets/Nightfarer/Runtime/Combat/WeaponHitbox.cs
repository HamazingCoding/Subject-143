using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Melee hit detection over one or more segments (a weapon blade, or claws that follow the hands). During an
    /// active window, sample points on each active segment are swept from last frame's position to this frame's
    /// (fast swipes can't tunnel), plus an overlap capsule per segment and an optional arc volume in front of the
    /// owner. Each target is hit once per swing. Runs in LateUpdate after animation and the weapon IK layer.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public class WeaponHitbox : MonoBehaviour
    {
        public LayerMask hitMask = ~0;
        [Range(2, 12)] public int samples = 5;
        public bool drawDebug = true;
        [Tooltip("Height of the arc volume's centre above the owner's feet (scale with the character).")]
        public float arcCentreHeight = 1.0f;

        public bool Active { get; private set; }
        public event Action<IDamageable, DamageInfo> Hit;

        public const int Primary = 1, Off = 2, All = ~0;

        class Segment
        {
            public Transform bone;
            public Vector3 localStart, localEnd;
            public float radius;
            public Vector3[] previous;
        }

        readonly List<Segment> segments = new List<Segment>();
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        readonly Collider[] overlap = new Collider[32];
        DamageInfo template;
        float arcReach, arcAngle;
        int activeMask = All;

        /// <summary>Single blade (weapon model).</summary>
        public void Configure(Transform bladeTransform, WeaponData data)
        {
            segments.Clear();
            Add(bladeTransform, data.bladeStart, data.bladeEnd, data.bladeRadius);
        }

        /// <summary>Claws: segments run from each wrist along the finger direction (hand-local).</summary>
        public void ConfigureClaws(Transform primaryHand, Vector3 primaryFingerLocal, float primaryLength, float primaryRadius,
            Transform offHand, Vector3 offFingerLocal, float offLength, float offRadius)
        {
            segments.Clear();
            if (primaryHand != null) Add(primaryHand, LocalAlong(primaryHand, primaryFingerLocal, 0.25f * primaryLength), LocalAlong(primaryHand, primaryFingerLocal, primaryLength), primaryRadius);
            if (offHand != null) Add(offHand, LocalAlong(offHand, offFingerLocal, 0.2f * offLength), LocalAlong(offHand, offFingerLocal, offLength), offRadius);
        }

        /// <summary>A hand-local point at the given world distance along a hand-local direction.</summary>
        static Vector3 LocalAlong(Transform hand, Vector3 dirLocal, float metres)
        {
            float scale = Mathf.Max(1e-4f, hand.lossyScale.x);
            return dirLocal.normalized * (metres / scale);
        }

        void Add(Transform bone, Vector3 a, Vector3 b, float radius)
        {
            if (bone == null) return;
            segments.Add(new Segment { bone = bone, localStart = a, localEnd = b, radius = radius, previous = new Vector3[samples] });
        }

        /// <param name="arcReach">Optional arc volume in front of the owner (0 = segments only).</param>
        /// <param name="segmentMask">Bit 0 = primary segment, bit 1 = off-hand segment.</param>
        public void BeginSwing(DamageInfo info, float arcReach = 0f, float arcAngle = 0f, int segmentMask = All)
        {
            if (segments.Count == 0) return;
            template = info;
            this.arcReach = arcReach;
            this.arcAngle = arcAngle;
            activeMask = segmentMask;
            hitThisSwing.Clear();
            Active = true;
            foreach (var s in segments)
            {
                if (s.previous.Length != samples) s.previous = new Vector3[samples];
                for (int i = 0; i < samples; i++) s.previous[i] = Sample(s, i);
            }
            Detect(true);
        }

        public void EndSwing() => Active = false;

        Vector3 Sample(Segment s, int i)
        {
            float t = samples <= 1 ? 0f : i / (float)(samples - 1);
            return s.bone.TransformPoint(Vector3.Lerp(s.localStart, s.localEnd, t));
        }

        void LateUpdate()
        {
            if (Active) Detect(false);
        }

        void Detect(bool firstFrame)
        {
            for (int si = 0; si < segments.Count; si++)
            {
                var s = segments[si];
                if (s.bone == null) continue;
                bool active = ((1 << si) & activeMask) != 0;
                if (active)
                {
                    Vector3 a = Sample(s, 0), b = Sample(s, samples - 1);
                    int n = Physics.OverlapCapsuleNonAlloc(a, b, s.radius, overlap, hitMask, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < n; i++) Consider(overlap[i], overlap[i].ClosestPoint((a + b) * 0.5f));
                }
                for (int i = 0; i < samples; i++)
                {
                    Vector3 cur = Sample(s, i);
                    if (active && !firstFrame)
                    {
                        Vector3 delta = cur - s.previous[i];
                        float dist = delta.magnitude;
                        if (dist > 1e-4f)
                            foreach (var h in Physics.SphereCastAll(s.previous[i], s.radius, delta / dist, dist, hitMask, QueryTriggerInteraction.Collide))
                                Consider(h.collider, h.point == Vector3.zero ? cur : h.point);
                        if (drawDebug) Debug.DrawLine(s.previous[i], cur, Color.red, 0.5f);
                    }
                    s.previous[i] = cur;
                }
            }

            if (arcReach > 0f)
            {
                Vector3 centre = transform.position + Vector3.up * arcCentreHeight;
                int m = Physics.OverlapSphereNonAlloc(centre, arcReach, overlap, hitMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < m; i++)
                {
                    Vector3 p = overlap[i].ClosestPoint(centre);
                    Vector3 flat = p - transform.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 1e-4f && Vector3.Angle(transform.forward, flat) > arcAngle * 0.5f) continue;
                    if (p.y < transform.position.y - 0.5f || p.y > transform.position.y + arcCentreHeight * 2.6f) continue;
                    Consider(overlap[i], p);
                }
            }
        }

        void Consider(Collider col, Vector3 point)
        {
            if (col == null || col.transform.IsChildOf(transform)) return;
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || hitThisSwing.Contains(target)) return;
            hitThisSwing.Add(target);
            var info = template;
            info.point = point;
            Vector3 dir = target.transform.position - transform.position;
            dir.y = 0f;
            info.direction = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
            if (target.ReceiveDamage(info)) Hit?.Invoke(target, info);
        }

        /// <summary>World endpoints of a segment (for tests and debugging).</summary>
        public bool TryGetSegment(int index, out Vector3 start, out Vector3 end)
        {
            start = end = Vector3.zero;
            if (index < 0 || index >= segments.Count || segments[index].bone == null) return false;
            start = Sample(segments[index], 0);
            end = Sample(segments[index], samples - 1);
            return true;
        }

        void OnDrawGizmosSelected()
        {
            foreach (var s in segments)
            {
                if (s.bone == null) continue;
                Gizmos.color = Active ? Color.red : Color.yellow;
                Gizmos.DrawWireSphere(s.bone.TransformPoint(s.localStart), s.radius);
                Gizmos.DrawWireSphere(s.bone.TransformPoint(s.localEnd), s.radius);
                Gizmos.DrawLine(s.bone.TransformPoint(s.localStart), s.bone.TransformPoint(s.localEnd));
            }
        }
    }
}
