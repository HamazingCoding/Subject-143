using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Sparks thrown off the claws when they scrape the ground (pooled streaks with gravity).</summary>
    public class ClawSparks : MonoBehaviour
    {
        class Spark
        {
            public TrailRenderer trail;
            public Vector3 velocity;
            public float life;
        }

        static ClawSparks instance;
        readonly List<Spark> pool = new List<Spark>();
        Material mat;

        public static int Emitted { get; private set; }

        public static void Emit(Vector3 position, Vector3 away, int count)
        {
            if (instance == null) instance = new GameObject("ClawSparks").AddComponent<ClawSparks>();
            for (int i = 0; i < count; i++) instance.One(position, away);
        }

        void One(Vector3 position, Vector3 away)
        {
            Emitted++;
            Spark s = pool.Find(p => p.life <= 0f);
            if (s == null)
            {
                if (pool.Count >= 64) return;
                if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
                var go = new GameObject("Spark");
                go.transform.SetParent(transform, false);
                var tr = go.AddComponent<TrailRenderer>();
                tr.sharedMaterial = mat;
                tr.time = 0.07f;
                tr.minVertexDistance = 0.01f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.012f), new Keyframe(1f, 0f));
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
                s = new Spark { trail = tr };
                pool.Add(s);
            }
            s.trail.emitting = false;
            s.trail.transform.position = position;
            s.trail.Clear();
            s.trail.emitting = true;
            Vector3 v = (away.normalized + Random.insideUnitSphere * 0.6f + Vector3.up * 0.7f).normalized;
            s.velocity = v * Random.Range(2.5f, 5.5f);
            s.life = Random.Range(0.15f, 0.3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var s in pool)
            {
                if (s.life <= 0f) continue;
                s.life -= dt;
                s.velocity += Physics.gravity * dt;
                s.trail.transform.position += s.velocity * dt;
                if (s.life <= 0f) s.trail.emitting = false;
            }
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (instance == this) instance = null;
        }
    }
}
