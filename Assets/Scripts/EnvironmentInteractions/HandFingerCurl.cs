using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// OPTIONAL procedural finger curl for the environmental hand IK.
///
/// As the hand plants on a surface (IK weight rises) the fingers relax/curl toward
/// a contact pose; as the hand retracts they open back to rest. Runs in LateUpdate,
/// AFTER the animation + RigBuilder have written the frame, and drives each joint
/// relative to its captured rest pose so it never compounds or fights the solver.
///
/// SETUP (per hand):
///   1. Add this component next to the hand (or anywhere on the character).
///   2. Assign the matching TwoBoneIKConstraint (left or right) to drive the curl.
///   3. For each finger, add an entry and drag its joint bones in order
///      (proximal -> distal), e.g. Index1, Index2, Index3.
///   4. Set Curl Axis to the bones' local bend axis (try (0,0,1); flip sign if
///      fingers bend backward). Tune Curl Angle and Max Curl in play mode.
/// The thumb usually wants a smaller angle and sometimes a different axis.
/// </summary>
[DefaultExecutionOrder(30000)]
public class HandFingerCurl : MonoBehaviour
{
    [System.Serializable]
    public class Finger
    {
        public string name = "Finger";
        [Tooltip("Joint bones in order, proximal to distal.")]
        public Transform[] joints;
        [Tooltip("Optional per-joint curl angle (deg). Leave empty to use the shared Curl Angle.")]
        public float[] jointAngles;
    }

    [Header("Drive")]
    [Tooltip("Curl follows this constraint's weight (the reaching hand). Leave null and use External Curl to drive it yourself.")]
    [SerializeField] private TwoBoneIKConstraint _ikConstraint;

    [Tooltip("If true, curl = IK constraint weight. If false, curl = External Curl value.")]
    [SerializeField] private bool _useConstraintWeight = true;

    [Header("Fingers")]
    [SerializeField] private List<Finger> _fingers = new List<Finger>();

    [Header("Shape")]
    [Tooltip("Local axis each joint bends around. Flip sign if fingers curl the wrong way.")]
    [SerializeField] private Vector3 _curlAxis = new Vector3(0f, 0f, 1f);

    [Tooltip("Curl angle per joint (deg) when fully planted, unless overridden per joint.")]
    [Range(0f, 90f)] [SerializeField] private float _curlAngle = 32f;

    [Tooltip("Scales the whole effect. 1 = full curl at full IK weight.")]
    [Range(0f, 1f)] [SerializeField] private float _maxCurl = 1f;

    [Tooltip("How quickly the fingers open/close (exponential).")]
    [Range(1f, 30f)] [SerializeField] private float _curlSmoothSpeed = 10f;

    private readonly List<Quaternion[]> _restRotations = new List<Quaternion[]>();
    private float _curl;
    private float _externalCurl;

    /// <summary>Drive the curl manually (0..1) when Use Constraint Weight is off.</summary>
    public void SetExternalCurl(float value) => _externalCurl = Mathf.Clamp01(value);

    private void Start()
    {
        CaptureRestPose();
    }

    private void CaptureRestPose()
    {
        _restRotations.Clear();
        foreach (Finger f in _fingers)
        {
            if (f?.joints == null) { _restRotations.Add(null); continue; }

            Quaternion[] rest = new Quaternion[f.joints.Length];
            for (int i = 0; i < f.joints.Length; i++)
                rest[i] = f.joints[i] != null ? f.joints[i].localRotation : Quaternion.identity;
            _restRotations.Add(rest);
        }
    }

    private void LateUpdate()
    {
        if (_restRotations.Count != _fingers.Count)
            CaptureRestPose();

        float target = _useConstraintWeight
            ? (_ikConstraint != null ? Mathf.Clamp01(_ikConstraint.weight) : 0f)
            : _externalCurl;
        target *= _maxCurl;

        _curl = Mathf.Lerp(_curl, target, 1f - Mathf.Exp(-_curlSmoothSpeed * Time.deltaTime));

        for (int fi = 0; fi < _fingers.Count; fi++)
        {
            Finger f = _fingers[fi];
            Quaternion[] rest = _restRotations[fi];
            if (f?.joints == null || rest == null) continue;

            for (int i = 0; i < f.joints.Length; i++)
            {
                Transform j = f.joints[i];
                if (j == null) continue;

                float angle = (f.jointAngles != null && f.jointAngles.Length > i)
                    ? f.jointAngles[i]
                    : _curlAngle;

                // Always rebuild from the captured rest pose so curl never accumulates.
                j.localRotation = rest[i] * Quaternion.AngleAxis(angle * _curl, _curlAxis);
            }
        }
    }
}
