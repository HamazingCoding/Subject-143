using UnityEngine;

public class SearchState : EnvironmentInteractionState
{
    public SearchState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) {}

    public override void OnTriggerEnter(Collider o) => StartTracking(o);
    public override void OnTriggerStay(Collider o) => UpdateTracking(o);
    public override void OnTriggerExit(Collider o) => StopTracking(o);

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        if (ShouldReset()) return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Reset;

        if (ctx.ClosestPoint != Vector3.positiveInfinity &&
            Vector3.Distance(ctx.Root.position, ctx.ClosestPoint) < 2f)
            return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Approach;

        return StateKey;
    }

    public override void EnterState() {}
    public override void ExitState() {}
    public override void UpdateState() {}
}