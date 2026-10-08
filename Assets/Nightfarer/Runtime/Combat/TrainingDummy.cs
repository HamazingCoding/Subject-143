using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Damageable test target: flashes, wobbles, staggers on poise break, shows damage numbers, regenerates.</summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        public float maxHealth = 800f;
        public float regenDelay = 3f;
        public float maxPoise = 60f;
        public float poiseRegen = 25f;
        public Transform body;
        public Color hitColor = new Color(1f, 0.85f, 0.4f);

        public float Health { get; private set; }
        public float Poise { get; private set; }
        public int HitCount { get; private set; }
        public float LastDamage { get; private set; }
        public string LastAttack { get; private set; } = "-";
        public bool IsStaggered => stagger > 0.3f;
        public bool IsAlive => true;

        float sinceHit = 99f, flash, stagger, wobble;
        Vector3 push, pushVel, bodyHome;
        bool homeSet;
        public float LastKnockback { get; private set; }
        Vector3 hitDir;
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            Health = maxHealth;
            Poise = maxPoise;
            if (body == null && transform.childCount > 0) body = transform.GetChild(0);
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            mpb = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        public bool ReceiveDamage(DamageInfo info)
        {
            HitCount++;
            LastDamage = info.amount;
            LastAttack = info.attackName;
            sinceHit = 0f;
            Health -= info.amount;
            if (Health <= 0f) Health = maxHealth;
            Poise -= info.poiseDamage;
            if (Poise <= 0f)
            {
                stagger = 1.1f;
                Poise = maxPoise;
            }
            flash = 0.12f;
            wobble = Mathf.Max(wobble, Mathf.Clamp01(info.amount / 120f) * 0.6f + 0.25f);
            hitDir = info.direction;
            LastKnockback = info.knockback;
            if (info.knockback > 0f) pushVel += info.direction * (info.knockback * 9f);   // slide back, then spring home
            DamageNumber.Spawn(info.point + Vector3.up * 0.3f, info.amount, stagger > 1f);
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            sinceHit += dt;
            if (sinceHit > regenDelay) Health = Mathf.MoveTowards(Health, maxHealth, maxHealth * 0.5f * dt);
            Poise = Mathf.MoveTowards(Poise, maxPoise, poiseRegen * dt);
            stagger = Mathf.Max(0f, stagger - dt);
            wobble = Mathf.MoveTowards(wobble, 0f, dt * 1.6f);
            flash = Mathf.Max(0f, flash - dt);

            if (body != null)
            {
                if (!homeSet) { bodyHome = body.localPosition; homeSet = true; }
                pushVel += (-push * 18f - pushVel * 7f) * dt;
                push += pushVel * dt;
                body.localPosition = bodyHome + transform.InverseTransformVector(push);
                float tilt = (wobble * Mathf.Sin(sinceHit * 28f) * 12f) + (IsStaggered ? 20f : 0f);
                Vector3 axis = Vector3.Cross(Vector3.up, hitDir.sqrMagnitude > 0f ? hitDir : transform.forward);
                body.localRotation = Quaternion.AngleAxis(tilt, transform.InverseTransformDirection(axis));
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(mpb);
                Color c = flash > 0f ? hitColor : IsStaggered ? Color.Lerp(baseColors[i], Color.red, 0.5f) : baseColors[i];
                mpb.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(mpb);
            }
        }
    }
}
