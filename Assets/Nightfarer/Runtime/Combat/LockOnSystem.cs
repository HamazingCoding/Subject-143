using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Elden Ring-style lock-on: pick the target nearest the view centre, flick to switch, break at range.</summary>
    public class LockOnSystem : MonoBehaviour
    {
        public float maxDistance = 22f;
        public float breakDistance = 28f;
        public float maxViewAngle = 55f;
        public LayerMask obstructionMask = ~0;

        public LockOnTarget Current { get; private set; }
        /// <summary>Why each candidate was accepted or rejected in the last query (debugging).</summary>
        public string LastQueryReport { get; private set; } = "";

        public bool Toggle(Transform view)
        {
            if (Current != null)
            {
                Current = null;
                return false;
            }
            Current = FindBest(view, null, Vector2.zero);
            return Current != null;
        }

        public void Release() => Current = null;

        /// <summary>Switch to the target closest in the given screen direction (x: -1 left, +1 right).</summary>
        public bool Switch(Transform view, float direction)
        {
            if (Current == null) return false;
            var next = FindBest(view, Current, new Vector2(Mathf.Sign(direction), 0f));
            if (next == null) return false;
            Current = next;
            return true;
        }

        LockOnTarget FindBest(Transform view, LockOnTarget exclude, Vector2 screenDir)
        {
            LockOnTarget best = null;
            float bestScore = float.MaxValue;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Vector3 viewFwd = view.forward; viewFwd.y = 0f; viewFwd.Normalize();
            Vector3 viewRight = view.right; viewRight.y = 0f; viewRight.Normalize();
            Vector3 refPoint = exclude != null ? exclude.Point : origin;
            var report = new System.Text.StringBuilder();

            foreach (var t in LockOnTarget.All)
            {
                if (t == null || t == exclude || t.transform.IsChildOf(transform)) continue;
                Vector3 to = t.Point - origin;
                float dist = to.magnitude;
                if (dist > maxDistance) { report.Append($"{t.name}: far {dist:F1}; "); continue; }
                Vector3 flat = to; flat.y = 0f;
                float angle = Vector3.Angle(viewFwd, flat);
                if (screenDir == Vector2.zero && angle > maxViewAngle) { report.Append($"{t.name}: angle {angle:F0}; "); continue; }
                if (screenDir != Vector2.zero)
                {
                    float side = Vector3.Dot(t.Point - refPoint, viewRight);
                    if (Mathf.Sign(side) != Mathf.Sign(screenDir.x) || Mathf.Abs(side) < 0.2f) continue;
                    angle = Mathf.Abs(side) * 4f;
                }
                if (Physics.Linecast(origin, t.Point, out var hit, obstructionMask, QueryTriggerInteraction.Ignore) &&
                    !hit.collider.transform.IsChildOf(t.transform) && !hit.collider.transform.IsChildOf(transform))
                {
                    report.Append($"{t.name}: blocked by {hit.collider.name}; ");
                    continue;
                }
                report.Append($"{t.name}: ok a{angle:F0} d{dist:F1}; ");
                float score = angle + dist * 0.6f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            LastQueryReport = $"view {view.name} fwd {viewFwd} | {report}";
            return best;
        }

        void Update()
        {
            if (Current == null) return;
            if (!Current.isActiveAndEnabled || Vector3.Distance(transform.position, Current.transform.position) > breakDistance)
                Current = null;
        }
    }
}
