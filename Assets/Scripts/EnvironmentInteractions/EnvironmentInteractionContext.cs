using UnityEngine;
using UnityEngine.Animations.Rigging;

public class EnvironmentInteractionContext
{
    public enum EBodySide { RIGHT, LEFT }

    private TwoBoneIKConstraint _leftIk;
    private TwoBoneIKConstraint _rightIk;
    private MultiRotationConstraint _leftRot;
    private MultiRotationConstraint _rightRot;
    private Rigidbody _rb;
    private CharacterController _cc;
    private CapsuleCollider _collider;
    private Transform _root;

    public HandIKSettings Settings;

    public Collider CurrentIntersectingCollider { get; set; }
    public Vector3 ClosestPoint { get; set; } = Vector3.positiveInfinity;
    public float LowestDistance = Mathf.Infinity;

    public float ShoulderHeight;
    public float InteractionYOffset;
    public float ColliderCenterY;

    public Transform CurrentShoulder;
    public Transform CurrentTarget;
    public Transform CurrentHint;

    public TwoBoneIKConstraint CurrentIK;
    public MultiRotationConstraint CurrentRot;

    public bool IsLeftSide;

    // Rest poses, captured per-side at startup.
    public Vector3 OriginalLeftPos;
    public Vector3 OriginalRightPos;
    public Vector3 CurrentOriginalPos;

    public Quaternion OriginalLeftRot;
    public Quaternion OriginalRightRot;
    public Quaternion CurrentOriginalRot;

    public Vector3 OriginalLeftHintPos;
    public Vector3 OriginalRightHintPos;
    public Vector3 CurrentOriginalHintPos;

    // Spring (SmoothDamp) velocity state for secondary motion.
    public Vector3 TargetVelocity;
    public Vector3 HintVelocity;

    // Per-side seed so the micro-motion noise on the two hands doesn't sync up.
    public float NoiseSeed;

    public Rigidbody Rb => _rb;
    public Transform Root => _root;

    // Authoritative character velocity. Prefers the CharacterController (the movement
    // authority) and falls back to the Rigidbody only if no controller was supplied.
    public Vector3 CharacterVelocity =>
        _cc != null ? _cc.velocity : (_rb != null ? _rb.linearVelocity : Vector3.zero);

    public EnvironmentInteractionContext(
        TwoBoneIKConstraint left, TwoBoneIKConstraint right,
        MultiRotationConstraint lRot, MultiRotationConstraint rRot,
        Rigidbody rb, CapsuleCollider col, Transform root,
        HandIKSettings settings, CharacterController cc = null)
    {
        _leftIk = left;
        _rightIk = right;
        _leftRot = lRot;
        _rightRot = rRot;
        _rb = rb;
        _cc = cc;
        _collider = col;
        _root = root;

        Settings = settings ?? new HandIKSettings();

        ShoulderHeight = left.data.root.position.y;

        // Sensible defaults so the interaction target starts near the shoulder,
        // never at world Y = 0 (which would drag the hand toward the ground).
        InteractionYOffset = ShoulderHeight;
        ColliderCenterY = ShoulderHeight;

        OriginalLeftPos = left.data.target.localPosition;
        OriginalRightPos = right.data.target.localPosition;
        OriginalLeftRot = left.data.target.rotation;
        OriginalRightRot = right.data.target.rotation;

        OriginalLeftHintPos = left.data.hint != null ? left.data.hint.localPosition : Vector3.zero;
        OriginalRightHintPos = right.data.hint != null ? right.data.hint.localPosition : Vector3.zero;

        SetCurrentSide(Vector3.positiveInfinity);
    }

    public void SetCurrentSide(Vector3 point)
    {
        float leftDist = Vector3.Distance(point, _leftIk.data.root.position);
        float rightDist = Vector3.Distance(point, _rightIk.data.root.position);

        if (leftDist < rightDist)
        {
            IsLeftSide = true;
            CurrentIK = _leftIk;
            CurrentRot = _leftRot;
            CurrentOriginalPos = OriginalLeftPos;
            CurrentOriginalRot = OriginalLeftRot;
            CurrentOriginalHintPos = OriginalLeftHintPos;
        }
        else
        {
            IsLeftSide = false;
            CurrentIK = _rightIk;
            CurrentRot = _rightRot;
            CurrentOriginalPos = OriginalRightPos;
            CurrentOriginalRot = OriginalRightRot;
            CurrentOriginalHintPos = OriginalRightHintPos;
        }

        CurrentShoulder = CurrentIK.data.root;
        CurrentTarget = CurrentIK.data.target;
        CurrentHint = CurrentIK.data.hint;

        // Reset spring state so a hand switch doesn't inherit stale velocity.
        TargetVelocity = Vector3.zero;
        HintVelocity = Vector3.zero;
        NoiseSeed = IsLeftSide ? 13.7f : 71.3f;
    }

    /// <summary>
    /// 0..1 "how much should the hand be reaching right now", from movement speed
    /// and how directly the character is approaching the surface. Drives weight so
    /// a quick glancing pass gives a light brush and a slow close pass gives a full plant.
    /// </summary>
    public float ComputeReachStrength()
    {
        Vector3 vel = CharacterVelocity;
        vel.y = 0f;
        float speed = vel.magnitude;

        float speedT = Mathf.InverseLerp(Settings.minMoveSpeed, Settings.fullReachSpeed, speed);
        if (speedT <= 0f)
            return 0f;

        float angleT = 1f;
        if (Settings.approachAngleInfluence > 0f && !float.IsInfinity(ClosestPoint.x) && speed > 0.01f)
        {
            Vector3 toPoint = ClosestPoint - _root.position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude > 0.0001f)
            {
                float align = Vector3.Dot(vel.normalized, toPoint.normalized); // -1..1
                float aligned01 = Mathf.Clamp01(align * 0.5f + 0.5f);
                angleT = Mathf.Lerp(1f, aligned01, Settings.approachAngleInfluence);
            }
        }

        return Mathf.Clamp01(speedT * angleT);
    }
}
