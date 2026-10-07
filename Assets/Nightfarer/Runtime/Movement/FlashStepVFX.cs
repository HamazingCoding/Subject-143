using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// The look of Subject 143's flash step: the body vanishes for the duration of the dash, smoky afterimages
    /// (baked snapshots of the skinned mesh) mark the path, and a mist burst marks departure and arrival.
    /// All placeholder art is generated at runtime, so there are no external asset dependencies.
    /// </summary>
    public class FlashStepVFX : MonoBehaviour
    {
        public Color ghostColor = new Color(0.32f, 0.22f, 0.45f, 0.5f);
        public Color mistColor = new Color(0.12f, 0.08f, 0.16f, 0.7f);
        public float ghostLifetime = 0.35f;
        public float ghostInterval = 0.04f;
        public int mistParticles = 22;

        SkinnedMeshRenderer[] bodies;
        Material ghostMaterial, mistMaterial;
        float nextGhost;
        bool hidden;

        public bool IsHidden => hidden;

        void Awake()
        {
            bodies = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        Material GhostMaterial()
        {
            if (ghostMaterial == null) ghostMaterial = new Material(Shader.Find("Sprites/Default")) { color = ghostColor };
            return ghostMaterial;
        }

        Material MistMaterial()
        {
            if (mistMaterial != null) return mistMaterial;
            var tex = UISprites.CircleTexture(64, 0f, "mist");
            mistMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = Color.white };
            return mistMaterial;
        }

        public void Begin(Vector3 position)
        {
            Ghost();
            Mist(position + Vector3.up * 0.5f);
            nextGhost = Time.time + ghostInterval;
        }

        /// <summary>Called every frame while travelling.</summary>
        public void Travel()
        {
            if (Time.time < nextGhost) return;
            nextGhost = Time.time + ghostInterval;
            Ghost(0.6f);
        }

        public void Arrive(Vector3 position) => Mist(position + Vector3.up * 0.5f, 0.6f);

        public void SetHidden(bool hide)
        {
            if (hide == hidden) return;
            hidden = hide;
            if (bodies == null) return;
            foreach (var b in bodies)
                if (b != null) b.enabled = !hide;
        }

        void Ghost(float alphaScale = 1f)
        {
            if (bodies == null) return;
            foreach (var smr in bodies)
            {
                if (smr == null || smr.sharedMesh == null) continue;
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true);
                var go = new GameObject("FlashStepGhost");
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                go.transform.localScale = Vector3.one;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = GhostMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fade = go.AddComponent<FadeAndDie>();
                fade.life = ghostLifetime;
                fade.mesh = mesh;
                fade.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, ghostColor.a * alphaScale);
            }
        }

        void Mist(Vector3 position, float scale = 1f)
        {
            var go = new GameObject("FlashStepMist");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 2.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * scale, 0.55f * scale);
            main.startColor = mistColor;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(mistParticles * scale)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MistMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Destroy(go, 1f);
        }

        void OnDisable() => SetHidden(false);

        void OnDestroy()
        {
            if (ghostMaterial != null) Destroy(ghostMaterial);
            if (mistMaterial != null) Destroy(mistMaterial);
        }
    }

    /// <summary>Fades a ghost's material alpha to zero, then destroys it (and its baked mesh).</summary>
    public class FadeAndDie : MonoBehaviour
    {
        public float life = 0.35f;
        public Mesh mesh;
        public Color color;
        float t;
        MaterialPropertyBlock mpb;
        Renderer rend;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Start()
        {
            rend = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
        }

        void Update()
        {
            t += Time.deltaTime;
            if (rend != null)
            {
                var c = color;
                c.a *= 1f - Mathf.Clamp01(t / life);
                mpb.SetColor(ColorId, c);
                rend.SetPropertyBlock(mpb);
            }
            if (t >= life)
            {
                if (mesh != null) Destroy(mesh);
                Destroy(gameObject);
            }
        }
    }
}
