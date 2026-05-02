using UnityEngine;

public class ApproachState : EnvironmentInteractionState
{
    float t;

    public ApproachState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) {}

    public override void EnterState() { t = 0; }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        ctx.CurrentIK.weight = Mathf.Lerp(ctx.CurrentIK.weight, 0.5f, t);
        ctx.CurrentRot.weight = Mathf.Lerp(ctx.CurrentRot.weight, 0.75f, t);

        ctx.InteractionYOffset = ctx.ColliderCenterY;

        Quaternion rot = Quaternion.LookRotation(-Vector3.up, ctx.Root.forward);
        ctx.CurrentTarget.rotation = Quaternion.RotateTowards(
            ctx.CurrentTarget.rotation, rot, 500 * Time.deltaTime);
    }

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        if (ShouldReset()) return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Reset;

        float dist = Vector3.Distance(ctx.CurrentShoulder.position, ctx.ClosestPoint);

        if (dist < 0.5f) return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Rise;
        if (t > 2f) return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Reset;

        return StateKey;
    }

    public override void OnTriggerEnter(Collider o) => StartTracking(o);
    public override void OnTriggerStay(Collider o) => UpdateTracking(o);
    public override void OnTriggerExit(Collider o) => StopTracking(o);

    public override void ExitState() {}
}