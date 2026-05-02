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
    private CapsuleCollider _collider;
    private Transform _root;

    public Collider CurrentIntersectingCollider { get; set; }
    public Vector3 ClosestPoint { get; set; } = Vector3.positiveInfinity;
    public float LowestDistance = Mathf.Infinity;

    public float ShoulderHeight;
    public float InteractionYOffset;
    public float ColliderCenterY;

    public Transform CurrentShoulder;
    public Transform CurrentTarget;

    public TwoBoneIKConstraint CurrentIK;
    public MultiRotationConstraint CurrentRot;

    public Vector3 OriginalLeftPos;
    public Vector3 OriginalRightPos;
    public Vector3 CurrentOriginalPos;
    public Quaternion OriginalRot;

    public Rigidbody Rb => _rb;
    public Transform Root => _root;

    public EnvironmentInteractionContext(
        TwoBoneIKConstraint left, TwoBoneIKConstraint right,
        MultiRotationConstraint lRot, MultiRotationConstraint rRot,
        Rigidbody rb, CapsuleCollider col, Transform root)
    {
        _leftIk = left;
        _rightIk = right;
        _leftRot = lRot;
        _rightRot = rRot;
        _rb = rb;
        _collider = col;
        _root = root;

        ShoulderHeight = left.data.root.position.y;

        OriginalLeftPos = left.data.target.localPosition;
        OriginalRightPos = right.data.target.localPosition;
        OriginalRot = left.data.target.rotation;

        SetCurrentSide(Vector3.positiveInfinity);
    }

    public void SetCurrentSide(Vector3 point)
    {
        float leftDist = Vector3.Distance(point, _leftIk.data.root.position);
        float rightDist = Vector3.Distance(point, _rightIk.data.root.position);

        if (leftDist < rightDist)
        {
            CurrentIK = _leftIk;
            CurrentRot = _leftRot;
            CurrentOriginalPos = OriginalLeftPos;
        }
        else
        {
            CurrentIK = _rightIk;
            CurrentRot = _rightRot;
            CurrentOriginalPos = OriginalRightPos;
        }

        CurrentShoulder = CurrentIK.data.root;
        CurrentTarget = CurrentIK.data.target;
    }
}