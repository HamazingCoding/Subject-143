using UnityEngine;

public abstract class EnvironmentInteractionState
    : BaseState<EnvironmentInteractionStateMachine.EEnvironmentInteractionState>
{
    protected EnvironmentInteractionState(
        EnvironmentInteractionContext c,
        EnvironmentInteractionStateMachine.EEnvironmentInteractionState key
    ) : base(key)
    {
        ctx = c;
    }

    protected EnvironmentInteractionContext ctx;
    private HandIKSettings S => ctx.Settings;

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Collider.ClosestPoint only supports primitives and CONVEX mesh colliders.
    /// Non-convex mesh colliders warn and return a degenerate result, so fall back
    /// to the AABB for those.
    /// </summary>
    protected static Vector3 SafeClosestPoint(Collider col, Vector3 point)
    {
        if (col == null)
            return point;

        if (col is MeshCollider mesh && !mesh.convex)
            return col.bounds.ClosestPoint(point);

        return col.ClosestPoint(point);
    }

    private static bool IsFinite(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
              || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    private static bool IsFinite(Quaternion q)
    {
        return !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w))
            && (q.x != 0f || q.y != 0f || q.z != 0f || q.w != 0f);
    }

    // ---------------------------------------------------------------- tracking

    protected void StartTracking(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Interactable"))
            return;

        if (ctx.CurrentIntersectingCollider != null)
            return;

        ctx.CurrentIntersectingCollider = other;

        ctx.LowestDistance = Mathf.Infinity;
        ctx.ColliderCenterY = other.bounds.center.y;

        Vector3 p = SafeClosestPoint(other, ctx.Root.position);
        ctx.SetCurrentSide(p);

        SetTarget(other);
    }

    protected void UpdateTracking(Collider other)
    {
        if (other != ctx.CurrentIntersectingCollider) return;
        SetTarget(other);
    }

    protected void StopTracking(Collider other)
    {
        if (other != ctx.CurrentIntersectingCollider) return;

        ctx.CurrentIntersectingCollider = null;
        ctx.ClosestPoint = Vector3.positiveInfinity;
        // Leave LowestDistance so ShouldReset() detects the lost collider and
        // blends the hand back out promptly.
    }

    // ---------------------------------------------------------- reach target

    /// <summary>
    /// Anticipatory reach: aim from where the shoulder WILL be (velocity look-ahead)
    /// so the hand starts reaching toward a surface before the body arrives, then
    /// spring-damps the target for natural ease/overshoot instead of a stiff snap.
    /// </summary>
    protected void SetTarget(Collider col)
    {
        if (ctx.CurrentTarget == null || col == null)
            return;

        Vector3 shoulder = ctx.CurrentShoulder.position;

        // --- Anticipation: predict the shoulder position a moment ahead. ---
        Vector3 vel = ctx.CharacterVelocity;
        if (!IsFinite(vel)) vel = Vector3.zero;
        Vector3 predicted = shoulder + vel * S.lookAheadTime;

        Vector3 probe = new Vector3(predicted.x, ctx.ShoulderHeight, predicted.z);

        Vector3 closest = SafeClosestPoint(col, probe);
        if (!IsFinite(closest))
            return;

        ctx.ClosestPoint = closest;

        // Direction from the surface back toward the (current) shoulder.
        Vector3 diff = shoulder - closest;
        Vector3 dir = diff.sqrMagnitude > 0.0001f ? diff.normalized : ctx.Root.forward;

        Vector3 goal = closest + dir * S.reachOffset;

        float y = ctx.InteractionYOffset;
        if (float.IsNaN(y) || float.IsInfinity(y))
            y = ctx.ShoulderHeight;
        goal.y = y;

        // --- Micro-motion: subtle life, scaled by how planted the hand is. ---
        if (S.idleNoiseAmount > 0f)
        {
            float w = Mathf.Clamp01(ctx.CurrentIK.weight);
            float tN = Time.time * S.idleNoiseSpeed;
            float nx = Mathf.PerlinNoise(tN, ctx.NoiseSeed) - 0.5f;
            float ny = Mathf.PerlinNoise(tN + 5.2f, ctx.NoiseSeed) - 0.5f;
            float nz = Mathf.PerlinNoise(tN + 9.8f, ctx.NoiseSeed) - 0.5f;
            goal += new Vector3(nx, ny * 0.5f, nz) * (2f * S.idleNoiseAmount * w);
        }

        if (!IsFinite(goal))
            return;

        // Recover cleanly if the target was somehow corrupted last frame.
        if (!IsFinite(ctx.CurrentTarget.position))
        {
            ctx.CurrentTarget.position = goal;
            ctx.TargetVelocity = Vector3.zero;
        }

        // --- Spring / secondary motion. ---
        float smoothTime = Mathf.Max(0.0001f, S.positionSmoothTime);
        Vector3 next = Vector3.SmoothDamp(
            ctx.CurrentTarget.position, goal, ref ctx.TargetVelocity, smoothTime);

        if (!IsFinite(next) || !IsFinite(ctx.TargetVelocity))
        {
            next = goal;
            ctx.TargetVelocity = Vector3.zero;
        }

        ctx.CurrentTarget.position = next;
    }

    // ---------------------------------------------------------- elbow & wrist

    /// <summary>Drives the existing IK hint pole so the elbow bends down-and-out
    /// naturally and follows the hand with slight lag.</summary>
    protected void UpdateElbowHint()
    {
        if (ctx.CurrentHint == null) return;

        Vector3 shoulder = ctx.CurrentShoulder.position;
        Vector3 hand = ctx.CurrentTarget.position;
        if (!IsFinite(shoulder) || !IsFinite(hand)) return;

        Vector3 mid = (shoulder + hand) * 0.5f;
        Vector3 outward = (ctx.IsLeftSide ? -ctx.Root.right : ctx.Root.right) * S.elbowOut;
        Vector3 hintGoal = mid + Vector3.down * S.elbowDrop + outward;

        if (!IsFinite(hintGoal)) return;

        if (!IsFinite(ctx.CurrentHint.position))
        {
            ctx.CurrentHint.position = hintGoal;
            ctx.HintVelocity = Vector3.zero;
        }

        float smoothTime = Mathf.Max(0.0001f, S.hintSmoothTime);
        Vector3 next = Vector3.SmoothDamp(
            ctx.CurrentHint.position, hintGoal, ref ctx.HintVelocity, smoothTime);

        if (!IsFinite(next) || !IsFinite(ctx.HintVelocity))
        {
            next = hintGoal;
            ctx.HintVelocity = Vector3.zero;
        }

        ctx.CurrentHint.position = next;
    }

    /// <summary>Wrist follow-through: eases the hand toward a target rotation with
    /// exponential smoothing, so it trails and settles rather than snapping.</summary>
    protected void SmoothTargetRotation(Quaternion goal)
    {
        if (!IsFinite(goal)) return;

        float k = 1f - Mathf.Exp(-S.rotationSmoothSpeed * Time.deltaTime);
        Quaternion next = Quaternion.Slerp(ctx.CurrentTarget.rotation, goal, k);
        if (IsFinite(next))
            ctx.CurrentTarget.rotation = next;
    }

    /// <summary>Exponential weight blend toward a target, scaled 0..1 externally.</summary>
    protected float SmoothWeight(float current, float target, float speed)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    // ---------------------------------------------------------------- reset test

    protected bool ShouldReset()
    {
        if (ctx.CurrentIntersectingCollider == null)
        {
            if (ctx.LowestDistance != Mathf.Infinity)
            {
                ctx.LowestDistance = Mathf.Infinity;
                return true;
            }
            return false;
        }

        if (!IsFinite(ctx.ClosestPoint))
            return false;

        float dist = Vector3.Distance(ctx.Root.position, ctx.ClosestPoint);

        if (ctx.LowestDistance == Mathf.Infinity)
        {
            ctx.LowestDistance = dist;
            return false;
        }

        if (dist < ctx.LowestDistance)
        {
            ctx.LowestDistance = dist;
            return false;
        }

        if (dist > ctx.LowestDistance + 1.5f)
        {
            ctx.LowestDistance = Mathf.Infinity;
            return true;
        }

        Vector3 toTarget = ctx.ClosestPoint - ctx.CurrentShoulder.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            float dot = Vector3.Dot(toTarget.normalized, ctx.Root.forward);
            if (dot < -0.3f)
            {
                ctx.LowestDistance = Mathf.Infinity;
                return true;
            }
        }

        return false;
    }
}
