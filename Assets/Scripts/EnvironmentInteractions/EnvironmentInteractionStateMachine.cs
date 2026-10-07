using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Assertions;

public class EnvironmentInteractionStateMachine
    : StateManager<EnvironmentInteractionStateMachine.EEnvironmentInteractionState>
{
    public enum EEnvironmentInteractionState
    {
        Reset,
        Search,
        Approach,
        Rise,
        Touch
    }

    // Ensures a given IK constraint set is only driven by a single state machine,
    // even if the scene accidentally contains duplicate components pointing at the
    // same constraints (two machines fighting over one hand causes sticking/jitter).
    private static readonly HashSet<TwoBoneIKConstraint> s_claimedConstraints = new HashSet<TwoBoneIKConstraint>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetClaims() => s_claimedConstraints.Clear();

    private EnvironmentInteractionContext _context;

    [SerializeField] private TwoBoneIKConstraint _leftIkConstraint;
    [SerializeField] private TwoBoneIKConstraint _rightIkConstraint;
    [SerializeField] private MultiRotationConstraint _leftMultiRotationConstraint;
    [SerializeField] private MultiRotationConstraint _rightMultiRotationConstraint;
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private CapsuleCollider _rootCollider;

    [Tooltip("Authoritative velocity source. Auto-found from a parent if left empty.")]
    [SerializeField] private CharacterController _characterController;

    [Header("Feel")]
    [SerializeField] private HandIKSettings _handIKSettings = new HandIKSettings();

    void Awake()
    {
        ValidateConstraints();

        // If another instance already owns these constraints, stand down so we
        // don't double-drive the same hand.
        if (_leftIkConstraint != null && !s_claimedConstraints.Add(_leftIkConstraint))
        {
            Debug.LogWarning(
                $"[EnvironmentInteraction] Duplicate state machine on '{name}' shares IK constraints " +
                "with another instance. Disabling this one to prevent conflicting IK. " +
                "Remove the extra component from the scene.", this);
            enabled = false;
            return;
        }

        // Prefer the CharacterController (the authoritative movement source) for
        // velocity. Auto-find it on a parent if it wasn't wired in the inspector.
        if (_characterController == null)
            _characterController = GetComponentInParent<CharacterController>();

        _context = new EnvironmentInteractionContext(
            _leftIkConstraint,
            _rightIkConstraint,
            _leftMultiRotationConstraint,
            _rightMultiRotationConstraint,
            _rigidbody,
            _rootCollider,
            transform.root,
            _handIKSettings,
            _characterController
        );

        ConstructEnvironmentDetectionCollider();
        InitializeStates();
    }

    private void ValidateConstraints()
    {
        Assert.IsNotNull(_leftIkConstraint, "Left IK constraint is not assigned.");
        Assert.IsNotNull(_rightIkConstraint, "Right IK constraint is not assigned.");
        Assert.IsNotNull(_leftMultiRotationConstraint, "Left multi-rotation constraint is not assigned.");
        Assert.IsNotNull(_rightMultiRotationConstraint, "Right multi-rotation constraint is not assigned.");
        Assert.IsNotNull(_rigidbody, "Rigidbody is not assigned.");
        Assert.IsNotNull(_rootCollider, "Root collider is not assigned.");
    }

    private void InitializeStates()
    {
        States.Add(EEnvironmentInteractionState.Reset, new ResetState(_context, EEnvironmentInteractionState.Reset));
        States.Add(EEnvironmentInteractionState.Search, new SearchState(_context, EEnvironmentInteractionState.Search));
        States.Add(EEnvironmentInteractionState.Approach, new ApproachState(_context, EEnvironmentInteractionState.Approach));
        States.Add(EEnvironmentInteractionState.Rise, new RiseState(_context, EEnvironmentInteractionState.Rise));
        States.Add(EEnvironmentInteractionState.Touch, new TouchState(_context, EEnvironmentInteractionState.Touch));

        CurrentState = States[EEnvironmentInteractionState.Reset];
    }

    private void ConstructEnvironmentDetectionCollider()
    {
        float wingspan = _rootCollider.height;

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(wingspan, wingspan, wingspan);

        box.center = new Vector3(
            _rootCollider.center.x,
            _rootCollider.center.y + (0.25f * wingspan),
            _rootCollider.center.z + (0.5f * wingspan)
        );

        box.isTrigger = true;
    }
}