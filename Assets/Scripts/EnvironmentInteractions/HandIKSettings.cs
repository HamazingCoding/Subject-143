using UnityEngine;

/// <summary>
/// Tunable parameters for the environmental hand IK "feel".
/// Everything that shapes the Uncharted-style naturalism lives here so it can be
/// dialed in from the inspector without touching code. Defaults are chosen to be
/// a good starting point for a ~1.8m humanoid walking at 2-4 m/s.
/// </summary>
[System.Serializable]
public class HandIKSettings
{
    [Header("Anticipation (look-ahead)")]
    [Tooltip("Seconds of velocity prediction. The hand reaches toward where the shoulder WILL be, so it starts reaching before you pass a surface. 0 = no anticipation.")]
    [Range(0f, 1f)] public float lookAheadTime = 0.35f;

    [Tooltip("How far in front of the surface the palm plants, along the surface normal (metres).")]
    [Range(0.05f, 1f)] public float reachOffset = 0.45f;

    [Tooltip("Surfaces farther than this from the body are ignored (metres).")]
    public float maxReachDistance = 2.2f;

    [Tooltip("Reach begins ramping in once the predicted contact is within this distance (metres). Larger = the hand notices surfaces earlier.")]
    public float anticipationRange = 1.6f;

    [Header("Speed & angle response")]
    [Tooltip("Below this speed (m/s) the hand does not reach (standing still). ")]
    public float minMoveSpeed = 0.25f;

    [Tooltip("At/above this speed the reach is at full strength.")]
    public float fullReachSpeed = 3.0f;

    [Tooltip("How much the approach angle matters. 1 = only reach for surfaces roughly ahead; 0 = reach regardless of angle.")]
    [Range(0f, 1f)] public float approachAngleInfluence = 0.6f;

    [Header("Spring / secondary motion")]
    [Tooltip("SmoothDamp time for the hand target position. Lower = snappier, higher = floatier with more drag/overshoot.")]
    [Range(0.02f, 0.4f)] public float positionSmoothTime = 0.12f;

    [Tooltip("SmoothDamp time for the elbow hint. Slightly slower than the hand reads as natural forearm follow.")]
    [Range(0.02f, 0.5f)] public float hintSmoothTime = 0.16f;

    [Tooltip("Exponential smoothing rate for hand/wrist rotation. Lower = the wrist lags and trails (follow-through); higher = locks on fast.")]
    [Range(2f, 30f)] public float rotationSmoothSpeed = 11f;

    [Tooltip("Exponential smoothing rate for IK weight blending in and out.")]
    [Range(1f, 20f)] public float weightSmoothSpeed = 7f;

    [Header("Elbow pose")]
    [Tooltip("How far the elbow hint drops below the shoulder-to-hand line (metres). Gives a relaxed, lowered elbow instead of a chicken-wing.")]
    public float elbowDrop = 0.35f;

    [Tooltip("How far the elbow bows outward, away from the torso (metres).")]
    public float elbowOut = 0.15f;

    [Header("Life / micro-motion")]
    [Tooltip("Amplitude of subtle Perlin drift added to the hand while reaching (metres). Keeps the plant from looking frozen. 0 = off.")]
    [Range(0f, 0.05f)] public float idleNoiseAmount = 0.012f;

    [Tooltip("Speed of the micro-motion noise.")]
    public float idleNoiseSpeed = 1.1f;

    [Header("Peak weights")]
    [Tooltip("IK position weight when fully planted on a surface.")]
    [Range(0f, 1f)] public float maxIkWeight = 1f;

    [Tooltip("Hand orientation (MultiRotation) weight when fully planted.")]
    [Range(0f, 1f)] public float maxRotationWeight = 0.85f;
}
