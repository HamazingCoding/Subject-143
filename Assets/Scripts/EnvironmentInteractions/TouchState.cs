using UnityEngine;

public class TouchState : EnvironmentInteractionState
{
    float t;

    public TouchState(EnvironmentInteractionContext c, EnvironmentInteractionStateMachine.EEnvironmentInteractionState k) : base(c, k) {}

    public override void EnterState() { t = 0; }

    public override void UpdateState()
    {
        t += Time.deltaTime;

        // Keep the forearm/elbow settled and alive while the palm rests on the surface.
        UpdateElbowHint();
    }

    public override EnvironmentInteractionStateMachine.EEnvironmentInteractionState GetNextState()
    {
        if (t > 0.5f)
            return EnvironmentInteractionStateMachine.EEnvironmentInteractionState.Reset;

        return StateKey;
    }

    public override void OnTriggerEnter(Collider o) => StartTracking(o);
    public override void OnTriggerStay(Collider o) => UpdateTracking(o);
    public override void OnTriggerExit(Collider o) => StopTracking(o);

    public override void ExitState() {}
}