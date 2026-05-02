using UnityEngine;

public class RiseState : EnvironmentInteractionState
{
    float t;

    public RiseState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) {}

    public override void EnterState() { t = 0; }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        ctx.InteractionYOffset = Mathf.Lerp(
            ctx.InteractionYOffset,
            ctx.ClosestPoint.y,
            t / 0.5f
        );

        ctx.CurrentIK.weight = Mathf.Lerp(ctx.CurrentIK.weight, 1f, t);

        Vector3 dir = (ctx.ClosestPoint - ctx.CurrentShoulder.position).normalized;

        if (Physics.Raycast(ctx.CurrentShoulder.position, dir, out RaycastHit hit, 1f,
            LayerMask.GetMask("Interactable")))
        {
            Vector3 forward = -hit.normal;
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

            ctx.CurrentTarget.rotation = Quaternion.RotateTowards(
                ctx.CurrentTarget.rotation, rot, 1000 * Time.deltaTime);
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

    public override void ExitState() {}
}