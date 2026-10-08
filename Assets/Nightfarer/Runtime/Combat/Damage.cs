using UnityEngine;

namespace Subject143.Nightfarer
{
    public struct DamageInfo
    {
        public float amount;
        public float poiseDamage;
        public Vector3 point;
        public Vector3 direction;
        public GameObject source;
        public string attackName;
        /// <summary>Push-back distance (m); grows with the attacker's approach speed.</summary>
        public float knockback;
    }

    /// <summary>Anything that can be hit. Found with GetComponentInParent from the collider that was hit.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform transform { get; }
        /// <summary>Returns false if the hit was ignored (e.g. invulnerable).</summary>
        bool ReceiveDamage(DamageInfo info);
    }
}
