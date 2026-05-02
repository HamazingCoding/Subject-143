using UnityEngine;

public abstract class EnvironmentInteractionState 
    : BaseState<EnvironmentInteractionStateMachine.EEnvironmentInteractionState>
{
    protected EnvironmentInteractionContext ctx;

    protected EnvironmentInteractionState(
        EnvironmentInteractionContext c,
        EnvironmentInteractionStateMachine.EEnvironmentInteractionState key
    ) : base(key)
    {
        ctx = c;
    }

    protected void StartTracking(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("Interactable"))
            return;

        if (ctx.CurrentIntersectingCollider != null)
            return;

        ctx.CurrentIntersectingCollider = other;

        Vector3 p = other.ClosestPoint(ctx.Root.position);
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
        ctx.LowestDistance = Mathf.Infinity;
    }

    protected void SetTarget(Collider col)
    {
        if (ctx.CurrentTarget == null || col == null)
            return;

        Vector3 shoulder = ctx.CurrentShoulder.position;

        Vector3 adjusted = new Vector3(
            shoulder.x,
            ctx.ShoulderHeight,
            shoulder.z
        );

        Vector3 closest = col.ClosestPoint(adjusted);

        // Failsafe: invalid closest point
        if (float.IsNaN(closest.x) || float.IsNaN(closest.y) || float.IsNaN(closest.z))
            return;

        ctx.ClosestPoint = closest;

        // Safe direction
        Vector3 diff = shoulder - closest;
        Vector3 dir = diff.sqrMagnitude > 0.0001f
            ? diff.normalized
            : ctx.Root.forward;

        Vector3 pos = closest + dir * 0.5f;

        // Safe Y
        float y = ctx.InteractionYOffset;
        if (float.IsNaN(y) || float.IsInfinity(y))
            y = ctx.ShoulderHeight;

        pos.y = y;

        // Final NaN guard
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
            return;

        // Smooth instead of snap
        ctx.CurrentTarget.position = Vector3.Lerp(
            ctx.CurrentTarget.position,
            pos,
            Time.deltaTime * 5f
        );
    }

    protected bool ShouldReset()
    {
        if (ctx.CurrentIntersectingCollider == null)
            return false;

        float dist = Vector3.Distance(ctx.Root.position, ctx.ClosestPoint);

        // Initialize
        if (ctx.LowestDistance == Mathf.Infinity)
        {
            ctx.LowestDistance = dist;
            return false;
        }

        // Getting closer
        if (dist < ctx.LowestDistance)
        {
            ctx.LowestDistance = dist;
            return false;
        }

        // Only reset if clearly moving away
        if (dist > ctx.LowestDistance + 1.5f)
        {
            ctx.LowestDistance = Mathf.Infinity;
            return true;
        }

        // Angle check (less aggressive)
        Vector3 toTarget = (ctx.ClosestPoint - ctx.CurrentShoulder.position).normalized;
        Vector3 forward = ctx.Root.forward;

        float dot = Vector3.Dot(toTarget, forward);

        if (dot < -0.3f) // instead of < 0
            return true;

        return false;
    }
}