using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Health and stamina. Like Elden Ring, any action is allowed while stamina is above zero.</summary>
    public class NightfarerVitals : MonoBehaviour
    {
        public float MaxStamina { get; private set; } = 100f;
        public float Stamina { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 1000f;
        public float Health { get; private set; } = 1000f;
        /// <summary>Set by the character while attacking, dodging or sprinting.</summary>
        public bool RegenBlocked { get; set; }

        NightfarerConfig config;
        float regenDelay;

        public void Configure(NightfarerConfig cfg)
        {
            config = cfg;
            MaxStamina = cfg.maxStamina;
            MaxHealth = cfg.maxHealth;
            Stamina = MaxStamina;
            Health = MaxHealth;
        }

        public bool HasStamina => Stamina > 0.01f;

        public bool TryConsume(float amount)
        {
            if (amount <= 0f) return true;
            if (!HasStamina) return false;
            Drain(amount);
            return true;
        }

        public void Drain(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            if (config != null)
                regenDelay = Stamina <= 0f ? config.staminaRegenDelayWhenEmpty : config.staminaRegenDelay;
        }

        public void TakeDamage(float amount)
        {
            Health = Mathf.Max(0f, Health - amount);
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(MaxHealth, Health + amount);
        }

        public void RefillAll()
        {
            Stamina = MaxStamina;
            Health = MaxHealth;
        }

        void Update()
        {
            if (config == null) return;
            if (regenDelay > 0f) { regenDelay -= Time.deltaTime; return; }
            if (!RegenBlocked) Stamina = Mathf.Min(MaxStamina, Stamina + config.staminaRegen * Time.deltaTime);
        }
    }
}
