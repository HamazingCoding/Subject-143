using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Telegraphed area attack on a fixed rhythm, for practising dodge timing: the orb swells red during the
    /// windup, then a shockwave hits everything in range that is not invulnerable.
    /// </summary>
    public class DummyAttacker : MonoBehaviour
    {
        public float interval = 3.2f;
        public float windup = 0.9f;
        public float radius = 3.2f;
        public float damage = 120f;
        public Transform telegraph;

        public int Hits { get; private set; }
        public int Dodged { get; private set; }

        float timer;
        readonly Collider[] buffer = new Collider[16];

        void Update()
        {
            timer += Time.deltaTime;
            float t = timer - (interval - windup);
            if (telegraph != null)
            {
                float s = t > 0f ? Mathf.Lerp(0.3f, radius * 2f, t / windup) : 0.3f;
                telegraph.localScale = Vector3.one * s;
                telegraph.gameObject.SetActive(t > 0f);
            }
            if (timer < interval) return;
            timer = 0f;
            int n = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var target = buffer[i].GetComponentInParent<NightfarerCharacter>();
                if (target == null) continue;
                bool hit = target.ReceiveDamage(new DamageInfo
                {
                    amount = damage, poiseDamage = 30f, point = target.transform.position + Vector3.up,
                    direction = (target.transform.position - transform.position).normalized, source = gameObject,
                    attackName = "Shockwave"
                });
                if (hit) Hits++; else Dodged++;
                break;
            }
        }
    }
}
