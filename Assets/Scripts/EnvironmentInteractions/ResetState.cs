using UnityEngine;

public class ResetState : EnvironmentInteractionState
{
    float t;

    public ResetState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) {}

    public override void EnterState()
    {
        t = 0;
        ctx.CurrentIntersectingCollider = null;
        ctx.ClosestPoint = Vector3.positiveInfinity;
    }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        ctx.CurrentIK.weight = Mathf.Lerp(ctx.CurrentIK.weight, 0, t);
        ctx.CurrentRot.weight = Mathf.Lerp(ctx.CurrentRot.weight, 0, t);

        ctx.CurrentTarget.localPosition =
            Vector3.Lerp(ctx.CurrentTarget.localPosition, ctx.CurrentOriginalPos, t);

        ctx.CurrentTarget.rotation =
            Quaternion.RotateTowards(ctx.CurrentTarget.rotation, ctx.OriginalRot, 500 * Time.deltaTime);
    }

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        if (t > 2f && ctx.Rb.linearVelocity != Vector3.zero)
            return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Search;

        return StateKey;
    }

    public override void ExitState() {}
    public override void OnTriggerEnter(Collider o) {}
    public override void OnTriggerStay(Collider o) {}
    public override void OnTriggerExit(Collider o) {}
}