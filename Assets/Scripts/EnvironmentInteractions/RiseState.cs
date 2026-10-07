using UnityEngine;

public class RiseState : EnvironmentInteractionState
{
    private const float HeightBlendSpeed = 8f;

    float t;

    public RiseState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) { }

    public override void EnterState() { t = 0; }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        float reach = ctx.ComputeReachStrength();

        // Blend the interaction height toward the actual contact height.
        if (!float.IsNaN(ctx.ClosestPoint.y) && !float.IsInfinity(ctx.ClosestPoint.y))
        {
            ctx.InteractionYOffset = Mathf.Lerp(
                ctx.InteractionYOffset,
                ctx.ClosestPoint.y,
                Time.deltaTime * HeightBlendSpeed);
        }

        ctx.CurrentIK.weight = SmoothWeight(
            ctx.CurrentIK.weight, ctx.Settings.maxIkWeight * reach, ctx.Settings.weightSmoothSpeed);
        ctx.CurrentRot.weight = SmoothWeight(
            ctx.CurrentRot.weight, ctx.Settings.maxRotationWeight * reach, ctx.Settings.weightSmoothSpeed);

        UpdateElbowHint();

        // Orient the palm to the surface it is actually touching.
        Vector3 dir = ctx.ClosestPoint - ctx.CurrentShoulder.position;
        if (dir.sqrMagnitude < 0.0001f)
            return;
        dir.Normalize();

        if (Physics.Raycast(ctx.CurrentShoulder.position, dir, out RaycastHit hit, 1f,
            LayerMask.GetMask("Interactable")))
        {
            Vector3 forward = -hit.normal;

            // Avoid a degenerate LookRotation on floor/ceiling normals.
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f
                ? ctx.Root.forward
                : Vector3.up;

            Quaternion rot = Quaternion.LookRotation(forward, up);
            SmoothTargetRotation(rot);
        }
    }

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        if (ShouldReset()) return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Reset;

        if (Vector3.Distance(ctx.CurrentTarget.position, ctx.ClosestPoint) < 0.05f && t > 0.2f)
            return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Touch;

        return StateKey;
    }

    public override void OnTriggerEnter(Collider o) => StartTracking(o);
    public override void OnTriggerStay(Collider o) => UpdateTracking(o);
    public override void OnTriggerExit(Collider o) => StopTracking(o);

    public override void ExitState() { }
}
