using UnityEngine;

public class ResetState : EnvironmentInteractionState
{
    private const float RotateSpeed = 500f;
    private const float MinTimeInState = 0.15f;

    private float _t;

    public ResetState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) { }

    public override void EnterState()
    {
        _t = 0f;
        ctx.CurrentIntersectingCollider = null;
        ctx.ClosestPoint = Vector3.positiveInfinity;
        ctx.LowestDistance = Mathf.Infinity;
        ctx.TargetVelocity = Vector3.zero;
        ctx.HintVelocity = Vector3.zero;
    }

    public override void UpdateState()
    {
        _t += Time.deltaTime;

        float speed = ctx.Settings.weightSmoothSpeed;
        float k = 1f - Mathf.Exp(-speed * Time.deltaTime);

        ctx.CurrentIK.weight = SmoothWeight(ctx.CurrentIK.weight, 0f, speed);
        ctx.CurrentRot.weight = SmoothWeight(ctx.CurrentRot.weight, 0f, speed);

        ctx.CurrentTarget.localPosition =
            Vector3.Lerp(ctx.CurrentTarget.localPosition, ctx.CurrentOriginalPos, k);

        ctx.CurrentTarget.rotation =
            Quaternion.RotateTowards(ctx.CurrentTarget.rotation, ctx.CurrentOriginalRot, RotateSpeed * Time.deltaTime);

        if (ctx.CurrentHint != null)
        {
            ctx.CurrentHint.localPosition =
                Vector3.Lerp(ctx.CurrentHint.localPosition, ctx.CurrentOriginalHintPos, k);
        }
    }

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        bool retracted = ctx.CurrentIK.weight < 0.01f;
        bool moving = ctx.CharacterVelocity.sqrMagnitude > 0.01f;

        if (_t > MinTimeInState && retracted && moving)
            return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Search;

        return StateKey;
    }

    public override void ExitState()
    {
        ctx.CurrentIK.weight = 0f;
        ctx.CurrentRot.weight = 0f;
    }

    public override void OnTriggerEnter(Collider o) { }
    public override void OnTriggerStay(Collider o) { }
    public override void OnTriggerExit(Collider o) { }
}
