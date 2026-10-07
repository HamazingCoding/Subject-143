using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>Procedural UI textures (so the HUD has no external art dependencies). The editor builder saves them as PNG assets.</summary>
    public static class UISprites
    {
        public static Texture2D WhiteTexture()
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "ui_white" };
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Antialiased disc (inner = 0) or ring (inner &gt; 0), radii as fractions of the half size.</summary>
        public static Texture2D CircleTexture(int size, float inner, string name)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name };
            float r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float outer = Mathf.Clamp01((1f - d) * r);
                float inn = inner > 0f ? Mathf.Clamp01((d - inner) * r) : 1f;
                px[y * size + x] = new Color(1f, 1f, 1f, outer * inn);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Transparent centre fading to opaque edges (damage vignette).</summary>
        public static Texture2D VignetteTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ui_vignette" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Horizontal gradient strip used for bar fills (bright left, darker right).</summary>
        public static Texture2D GradientTexture()
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { name = "ui_gradient" };
            for (int x = 0; x < 64; x++)
            for (int y = 0; y < 4; y++)
            {
                float v = Mathf.Lerp(1f, 0.7f, x / 63f) * (y == 3 ? 1.15f : 1f);
                t.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            t.Apply();
            return t;
        }

        public static Sprite ToSprite(Texture2D t, float border = 0f)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }
    }

    /// <summary>The sprites the HUD factory needs.</summary>
    [System.Serializable]
    public class UISpriteSet
    {
        public Sprite white, circle, ring, vignette, gradient;

        public static UISpriteSet CreateRuntime()
        {
            return new UISpriteSet
            {
                white = UISprites.ToSprite(UISprites.WhiteTexture()),
                circle = UISprites.ToSprite(UISprites.CircleTexture(128, 0f, "ui_circle")),
                ring = UISprites.ToSprite(UISprites.CircleTexture(128, 0.82f, "ui_ring")),
                vignette = UISprites.ToSprite(UISprites.VignetteTexture(256)),
                gradient = UISprites.ToSprite(UISprites.GradientTexture()),
            };
        }
    }
}
