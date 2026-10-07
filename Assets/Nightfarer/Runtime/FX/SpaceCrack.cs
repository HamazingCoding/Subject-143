using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// "The air itself cracks": jagged black fractures with a glowing rim burst out from a point, facing the
    /// camera like a crack in glass, with a void flash at the centre; then they shatter away. Spawned by heavy
    /// claw hits, the ultimate, and around the claw arm while the ultimate charges. Generated at runtime.
    /// </summary>
    public class SpaceCrack : MonoBehaviour
    {
        public static Color DefaultGlow = new Color(0.85f, 0.35f, 1f, 1f);

        class Branch
        {
            public LineRenderer core, glow;
            public Vector3[] points;
        }

        readonly List<Branch> branches = new List<Branch>();
        Transform voidDisc;
        Material coreMat, glowMat, voidMat;
        float life, lifetime, size;
        Color glowColor;
        static Texture2D discTex;

        public static int Spawned { get; private set; }

        public static SpaceCrack Spawn(Vector3 position, float size, Color? glow = null, float lifetime = 0.65f, int branches = 0)
        {
            Spawned++;
            var go = new GameObject("SpaceCrack");
            go.transform.position = position;
            var cam = Camera.main;
            if (cam != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position, cam.transform.up);
            var fx = go.AddComponent<SpaceCrack>();
            fx.Build(size, glow ?? DefaultGlow, lifetime, branches > 0 ? branches : Random.Range(7, 11));
            return fx;
        }

        void Build(float s, Color glow, float lt, int count)
        {
            size = s;
            lifetime = lt;
            glowColor = glow;
            var sprite = Shader.Find("Sprites/Default");
            coreMat = new Material(sprite) { color = new Color(0.02f, 0f, 0.04f, 1f) };
            glowMat = new Material(sprite) { color = glow };
            float baseAngle = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + 360f * i / count + Random.Range(-14f, 14f);
                float len = s * Random.Range(0.45f, 1f);
                var pts = Jagged(Vector3.zero, a, len, s);
                AddBranch(pts, s * Random.Range(0.03f, 0.05f));
                // Sub-cracks off the main fracture.
                for (int k = 2; k < pts.Count - 1; k++)
                    if (Random.value < 0.35f) AddBranch(Jagged(pts[k], a + Random.Range(-60f, 60f), len * Random.Range(0.2f, 0.45f), s), s * 0.018f);
            }
            if (discTex == null) discTex = UISprites.CircleTexture(64, 0.15f, "voiddisc");
            voidMat = new Material(sprite) { mainTexture = discTex, color = new Color(0f, 0f, 0f, 0.95f) };
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(disc.GetComponent<Collider>());
            disc.GetComponent<Renderer>().sharedMaterial = voidMat;
            disc.transform.SetParent(transform, false);
            disc.transform.localScale = Vector3.one * s * 0.35f;
            voidDisc = disc.transform;
        }

        static List<Vector3> Jagged(Vector3 from, float angleDeg, float length, float s)
        {
            var pts = new List<Vector3> { from };
            int n = Mathf.Max(3, Mathf.RoundToInt(length / (s * 0.13f)));
            float a = angleDeg;
            Vector3 p = from;
            for (int i = 0; i < n; i++)
            {
                a += Random.Range(-28f, 28f);
                float r = a * Mathf.Deg2Rad;
                p += new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * (length / n);
                pts.Add(p);
            }
            return pts;
        }

        void AddBranch(List<Vector3> pts, float width)
        {
            var b = new Branch { points = pts.ToArray() };
            b.glow = Line("glow", glowMat, width * 3.2f, 0);
            b.core = Line("core", coreMat, width, 1);
            branches.Add(b);
        }

        LineRenderer Line(string n, Material m, float width, int order)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = m;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, width * 0.15f));
            lr.numCapVertices = 0;
            lr.sortingOrder = order;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        void Update()
        {
            life += Time.deltaTime;
            float u = Mathf.Clamp01(life / lifetime);
            float grow = Mathf.Clamp01(life / 0.07f);              // cracks race outward
            float fade = 1f - Mathf.Clamp01((u - 0.55f) / 0.45f);   // then shatter away
            foreach (var b in branches)
            {
                int n = Mathf.Max(2, Mathf.CeilToInt(b.points.Length * grow));
                if (b.core.positionCount != n)
                {
                    b.core.positionCount = n;
                    b.glow.positionCount = n;
                    for (int i = 0; i < n; i++)
                    {
                        b.core.SetPosition(i, b.points[i]);
                        b.glow.SetPosition(i, b.points[i]);
                    }
                }
            }
            // Shatter: pieces drift outward and the glow flickers out.
            float spread = 1f + (1f - fade) * 0.35f;
            transform.localScale = Vector3.one * spread;
            coreMat.color = new Color(0.02f, 0f, 0.04f, fade);
            float flicker = 0.75f + 0.25f * Mathf.Sin(life * 90f);
            glowMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * fade * flicker);
            if (voidDisc != null)
            {
                float v = life < 0.05f ? life / 0.05f : Mathf.Clamp01(1f - (life - 0.05f) / 0.25f);
                voidDisc.localScale = Vector3.one * size * 0.35f * v;
            }
            if (life >= lifetime) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (coreMat != null) Destroy(coreMat);
            if (glowMat != null) Destroy(glowMat);
            if (voidMat != null) Destroy(voidMat);
        }
    }
}
