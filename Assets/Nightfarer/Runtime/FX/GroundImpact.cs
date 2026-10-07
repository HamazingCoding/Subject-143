using System.Collections.Generic;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Landing / launch impact on the ground: a ring of dust rolling outward, radial cracks in the floor and a
    /// few debris chips thrown up. Strength 0..1 scales everything. Puff() is a small dust kick (super jump charge).
    /// </summary>
    public class GroundImpact : MonoBehaviour
    {
        class Bit
        {
            public Transform t;
            public Vector3 velocity;
            public float spin;
            public bool debris;
            public float scale;
        }

        static Texture2D dustTex;
        readonly List<Bit> bits = new List<Bit>();
        readonly List<LineRenderer> cracks = new List<LineRenderer>();
        Material dustMat, crackMat, debrisMat;
        float life, lifetime, strength;

        public static int Spawned { get; private set; }

        public static void Spawn(Vector3 position, float strength, bool launch)
        {
            Spawned++;
            var go = new GameObject(launch ? "LaunchImpact" : "LandingImpact");
            go.transform.position = GroundPoint(position);
            go.AddComponent<GroundImpact>().Build(Mathf.Clamp01(strength), launch, cracksToo: strength > 0.35f);
        }

        public static void Puff(Vector3 position, float strength)
        {
            var go = new GameObject("DustPuff");
            go.transform.position = GroundPoint(position);
            go.AddComponent<GroundImpact>().Build(Mathf.Clamp01(strength) * 0.35f, false, cracksToo: false, puff: true);
        }

        static Vector3 GroundPoint(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.4f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.GetComponent<CharacterController>() == null && h.normal.y > 0.5f) return h.point + Vector3.up * 0.015f;
            return p + Vector3.up * 0.015f;
        }

        void Build(float s, bool launch, bool cracksToo, bool puff = false)
        {
            strength = s;
            lifetime = puff ? 0.6f : cracksToo ? 2.2f : 1.0f;
            var sprite = Shader.Find("Sprites/Default");
            if (dustTex == null) dustTex = SoftTexture(64);
            dustMat = new Material(sprite) { mainTexture = dustTex, color = new Color(0.55f, 0.5f, 0.45f, 0.4f) };

            int dust = puff ? 6 : Mathf.RoundToInt(Mathf.Lerp(10, 26, s));
            for (int i = 0; i < dust; i++)
            {
                float a = (i + Random.value * 0.5f) / dust * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.GetComponent<Renderer>().sharedMaterial = dustMat;
                q.transform.SetParent(transform, false);
                q.transform.localPosition = dir * 0.15f + Vector3.up * 0.1f;
                float sc = (puff ? 0.2f : 0.25f) + s * 0.3f;
                q.transform.localScale = Vector3.one * sc;
                bits.Add(new Bit
                {
                    t = q.transform, scale = sc,
                    velocity = dir * Mathf.Lerp(1.5f, 6f, s) * (puff ? 0.5f : 1f) + Vector3.up * (launch ? 1.2f : 0.35f) * Random.Range(0.5f, 1.2f),
                });
            }

            if (!puff)
            {
                debrisMat = new Material(sprite) { color = new Color(0.22f, 0.2f, 0.19f, 1f) };
                int chips = Mathf.RoundToInt(Mathf.Lerp(3, 12, s));
                for (int i = 0; i < chips; i++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(c.GetComponent<Collider>());
                    c.GetComponent<Renderer>().sharedMaterial = debrisMat;
                    c.transform.SetParent(transform, false);
                    float sc = Random.Range(0.015f, 0.035f) * (0.6f + s);
                    c.transform.localScale = Vector3.one * sc;
                    c.transform.localPosition = Random.insideUnitSphere * 0.2f;
                    Vector3 v = Random.insideUnitSphere;
                    v.y = Mathf.Abs(v.y) + 0.8f;
                    bits.Add(new Bit { t = c.transform, velocity = v.normalized * Random.Range(2f, 5f) * (0.5f + s), spin = Random.Range(-720f, 720f), debris = true, scale = sc });
                }
            }

            if (cracksToo)
            {
                crackMat = new Material(sprite) { color = new Color(0.04f, 0.035f, 0.035f, 0.9f) };
                int n = Mathf.RoundToInt(Mathf.Lerp(5, 11, s));
                for (int i = 0; i < n; i++)
                {
                    float a = (i + Random.value * 0.6f) / n * 360f;
                    var go = new GameObject("crack");
                    go.transform.SetParent(transform, false);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // line faces up: lies on the floor
                    var lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.alignment = LineAlignment.TransformZ;
                    lr.sharedMaterial = crackMat;
                    float w = 0.012f + 0.02f * s;
                    lr.widthCurve = new AnimationCurve(new Keyframe(0f, w), new Keyframe(1f, w * 0.1f));
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var pts = new List<Vector3>();
                    Vector3 p = Vector3.zero;
                    float len = Mathf.Lerp(0.35f, 1.1f, s) * Random.Range(0.5f, 1f);
                    int segs = 6;
                    float ang = a;
                    pts.Add(p);
                    for (int k = 0; k < segs; k++)
                    {
                        ang += Random.Range(-30f, 30f);
                        float r = ang * Mathf.Deg2Rad;
                        p += new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f) * (len / segs);   // local XY = floor plane
                        pts.Add(p);
                    }
                    lr.positionCount = pts.Count;
                    lr.SetPositions(pts.ToArray());
                    cracks.Add(lr);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            float u = Mathf.Clamp01(life / lifetime);
            var cam = Camera.main;
            foreach (var b in bits)
            {
                if (b.debris)
                {
                    b.velocity += Physics.gravity * dt;
                    b.t.position += b.velocity * dt;
                    if (b.t.position.y < transform.position.y) { b.t.position = new Vector3(b.t.position.x, transform.position.y, b.t.position.z); b.velocity *= 0.3f; }
                    b.t.Rotate(Vector3.one * (b.spin * dt));
                    b.t.localScale = Vector3.one * b.scale * (1f - Mathf.Clamp01((life - 0.5f) / 0.4f));
                }
                else
                {
                    b.velocity *= Mathf.Exp(-3.5f * dt);
                    b.t.position += b.velocity * dt;
                    b.t.localScale += Vector3.one * (dt * (0.6f + strength));
                    if (cam != null) b.t.rotation = Quaternion.LookRotation(b.t.position - cam.transform.position);
                }
            }
            if (dustMat != null) dustMat.color = new Color(0.55f, 0.5f, 0.45f, 0.4f * (1f - Mathf.Clamp01(life / Mathf.Min(1.0f, lifetime))));
            if (crackMat != null) crackMat.color = new Color(0.04f, 0.035f, 0.035f, 0.9f * (1f - Mathf.Clamp01((u - 0.6f) / 0.4f)));
            if (life >= lifetime) Destroy(gameObject);
        }

        /// <summary>Soft, slightly lumpy dust blob (alpha falls off smoothly to the edge).</summary>
        static Texture2D SoftTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "dust" };
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 d = new Vector2(x + 0.5f - r, y + 0.5f - r) / r;
                float n = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.35f;
                float a = Mathf.Clamp01(1f - d.magnitude - n * 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        void OnDestroy()
        {
            if (dustMat != null) Destroy(dustMat);
            if (crackMat != null) Destroy(crackMat);
            if (debrisMat != null) Destroy(debrisMat);
        }
    }
}
