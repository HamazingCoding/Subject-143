using UnityEngine;

public class ApproachState : EnvironmentInteractionState
{
    float t;

    public ApproachState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) { }

    public override void EnterState() { t = 0; }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        // Anticipation: reach strength scales the partial pre-plant weight, so a
        // fast glancing pass barely lifts the hand while a close/slow pass commits.
        float reach = ctx.ComputeReachStrength();

        float ikTarget = 0.5f * ctx.Settings.maxIkWeight * reach;
        float rotTarget = 0.75f * ctx.Settings.maxRotationWeight * reach;

        ctx.CurrentIK.weight = SmoothWeight(ctx.CurrentIK.weight, ikTarget, ctx.Settings.weightSmoothSpeed);
        ctx.CurrentRot.weight = SmoothWeight(ctx.CurrentRot.weight, rotTarget, ctx.Settings.weightSmoothSpeed);

        ctx.InteractionYOffset = ctx.ColliderCenterY;

        UpdateElbowHint();

        // Palm rotates to face the surface plane; wrist follows through via smoothing.
        Quaternion rot = Quaternion.LookRotation(-Vector3.up, ctx.Root.forward);
        SmoothTargetRotation(rot);
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

    public override void ExitState() { }
}
