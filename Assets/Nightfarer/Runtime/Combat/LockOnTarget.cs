using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Marks something the player can lock on to.</summary>
    public class LockOnTarget : MonoBehaviour
    {
        public static readonly List<LockOnTarget> All = new List<LockOnTarget>();
        public float pointHeight = 1.2f;

        public Vector3 Point => transform.position + Vector3.up * pointHeight;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
