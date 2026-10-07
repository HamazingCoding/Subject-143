using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Placeholder VFX: an expanding, fading shockwave dome plus a flash of light.</summary>
    public class BlastEffect : MonoBehaviour
    {
        float radius, life;
        Color color;
        Material mat;
        Light flash;
        const float Duration = 0.55f;

        public static void Spawn(Vector3 position, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BlastEffect";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            var fx = go.AddComponent<BlastEffect>();
            fx.radius = radius;
            fx.color = color;
            fx.mat = new Material(Shader.Find("Sprites/Default")) { color = color };
            go.GetComponent<Renderer>().sharedMaterial = fx.mat;
            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(go.transform, false);
            fx.flash = lightGo.AddComponent<Light>();
            fx.flash.type = LightType.Point;
            fx.flash.color = new Color(color.r, color.g, color.b);
            fx.flash.range = radius * 3f;
            fx.flash.intensity = 6f;
        }

        void Update()
        {
            life += Time.deltaTime;
            float u = Mathf.Clamp01(life / Duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, 1f - (1f - u) * (1f - u));
            var c = color;
            c.a = color.a * (1f - u);
            mat.color = c;
            flash.intensity = 6f * (1f - u);
            if (life >= Duration)
            {
                Destroy(mat);
                Destroy(gameObject);
            }
        }
    }
}
