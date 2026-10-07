using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>One weapon pose: where the grip is and where the blade points, in character space (x right, y up, z forward).</summary>
    [Serializable]
    public class SwingKey
    {
        [Range(0f, 1f)] public float t;
        public Vector3 grip = new Vector3(0.15f, 1.0f, 0.35f);
        public Vector3 blade = Vector3.up;

        public SwingKey() { }
        public SwingKey(float t, Vector3 grip, Vector3 blade)
        {
            this.t = t;
            this.grip = grip;
            this.blade = blade;
        }
    }

    public struct WeaponPose
    {
        public Vector3 grip;
        public Vector3 blade;
        public Vector3 edge;
    }

    /// <summary>
    /// The path the weapon takes through an attack, keyed on normalized attack time. <see cref="WeaponIK"/>
    /// drives the arms along it, so the blade follows a designed arc whatever body animation plays underneath.
    /// Leave empty to let the clip's own arm animation drive the weapon (e.g. with real mocap/keyframed clips).
    /// </summary>
    [Serializable]
    public class SwingPath
    {
        public List<SwingKey> keys = new List<SwingKey>();

        public bool IsValid => keys != null && keys.Count >= 2;

        public WeaponPose Sample(float u)
        {
            u = Mathf.Clamp01(u);
            int i = 0;
            while (i < keys.Count - 2 && u > keys[i + 1].t) i++;
            var k0 = keys[Mathf.Max(0, i - 1)];
            var k1 = keys[i];
            var k2 = keys[i + 1];
            var k3 = keys[Mathf.Min(keys.Count - 1, i + 2)];
            float span = Mathf.Max(1e-4f, k2.t - k1.t);
            float s = Mathf.Clamp01((u - k1.t) / span);
            float e = s * s * (3f - 2f * s);   // ease within each segment: snappy strikes, soft holds

            Vector3 grip = CatmullRom(k0.grip, k1.grip, k2.grip, k3.grip, e);
            Vector3 tangent = CatmullRomTangent(k0.grip, k1.grip, k2.grip, k3.grip, e);
            Vector3 blade = Vector3.Slerp(k1.blade.normalized, k2.blade.normalized, e).normalized;
            Vector3 edge = Vector3.ProjectOnPlane(tangent, blade);
            if (edge.sqrMagnitude < 1e-5f) edge = Vector3.ProjectOnPlane(Vector3.forward, blade);
            if (edge.sqrMagnitude < 1e-5f) edge = Vector3.right;
            return new WeaponPose { grip = grip, blade = blade, edge = edge.normalized };
        }

        public static WeaponPose FromKey(SwingKey k)
        {
            Vector3 blade = k.blade.normalized;
            Vector3 edge = Vector3.ProjectOnPlane(Vector3.forward, blade);
            if (edge.sqrMagnitude < 1e-5f) edge = Vector3.right;
            return new WeaponPose { grip = k.grip, blade = blade, edge = edge.normalized };
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        public static WeaponPose Lerp(WeaponPose a, WeaponPose b, float t)
        {
            return new WeaponPose
            {
                grip = Vector3.Lerp(a.grip, b.grip, t),
                blade = Vector3.Slerp(a.blade, b.blade, t).normalized,
                edge = Vector3.Slerp(a.edge, b.edge, t).normalized,
            };
        }
    }
}
