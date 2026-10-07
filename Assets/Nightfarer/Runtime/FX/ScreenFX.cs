using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Screen-space combat feel on the gameplay camera: radial zoom blur pulses toward the strike, anime speed
    /// lines, and impact frames for the ultimate (a few frames of inverted / black-and-white high contrast with
    /// a flash). The pass is enqueued per camera at render time, so no URP asset needs a renderer feature, and it
    /// costs nothing while idle. Call the static methods from gameplay.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenFX : MonoBehaviour
    {
        [Tooltip("Global multiplier for the blur pulses (0 turns them off).")]
        [Range(0f, 2f)] public float blurScale = 1f;
        [Range(0f, 2f)] public float linesScale = 1f;
        public bool impactFramesEnabled = true;
        public Color impactTint = new Color(0.85f, 0.15f, 0.25f);

        static ScreenFX instance;

        Camera cam;
        Material material;
        FXPass pass;
        float blurPeak, blurTime, blurDuration;
        float linesPeak, linesTime, linesDuration;
        float impactTime = 99f, impactDuration;
        Vector3 blurCentreWorld;
        bool hasCentre;

        /// <summary>Current values (tests / debug).</summary>
        public static float CurrentBlur { get; private set; }
        public static float CurrentLines { get; private set; }
        public static float CurrentImpact { get; private set; }
        public static int ImpactFramesPlayed { get; private set; }
        public static int BlurPulses { get; private set; }

        public static void Blur(float strength, float duration, Vector3? worldCentre = null)
        {
            BlurPulses++;
            if (instance == null) return;
            instance.blurPeak = Mathf.Max(strength * instance.blurScale, instance.blurPeak * instance.Remaining(instance.blurTime, instance.blurDuration));
            instance.blurTime = 0f;
            instance.blurDuration = Mathf.Max(0.05f, duration);
            instance.hasCentre = worldCentre.HasValue;
            if (worldCentre.HasValue) instance.blurCentreWorld = worldCentre.Value;
        }

        public static void SpeedLines(float amount, float duration)
        {
            if (instance == null) return;
            instance.linesPeak = Mathf.Max(amount * instance.linesScale, instance.linesPeak * instance.Remaining(instance.linesTime, instance.linesDuration));
            instance.linesTime = 0f;
            instance.linesDuration = Mathf.Max(0.05f, duration);
        }

        public static void ImpactFrames(float duration)
        {
            ImpactFramesPlayed++;
            if (instance == null || !instance.impactFramesEnabled) return;
            instance.impactTime = 0f;
            instance.impactDuration = Mathf.Max(0.1f, duration);
        }

        float Remaining(float t, float d) => d <= 0f ? 0f : Mathf.Clamp01(1f - t / d);

        void OnEnable()
        {
            instance = this;
            cam = GetComponent<Camera>();
            var shader = Shader.Find("Hidden/Nightfarer/ScreenFX");
            if (shader != null && material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            pass ??= new FXPass();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (instance == this) instance = null;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            blurTime += dt;
            linesTime += dt;
            impactTime += dt;
            // Blur: snaps in, eases out.
            float bu = Remaining(blurTime, blurDuration);
            CurrentBlur = blurPeak * bu * bu;
            float lu = Remaining(linesTime, linesDuration);
            CurrentLines = linesPeak * lu;

            // Impact frames: hard cuts (anime style), not fades.
            float mono = 0f, invert = 0f, flash = 0f;
            if (impactTime < impactDuration)
            {
                float s = impactTime / impactDuration;
                if (s < 0.18f) { mono = 1f; invert = 1f; }
                else if (s < 0.36f) { mono = 1f; invert = 0f; flash = 0.55f; }
                else if (s < 0.5f) { mono = 1f; invert = 1f; }
                else if (s < 0.68f) { mono = 1f; flash = 0.3f; }
                else { mono = 1f - (s - 0.68f) / 0.32f; }
                CurrentLines = Mathf.Max(CurrentLines, 0.8f * (1f - s));
            }
            CurrentImpact = mono;

            if (material == null || cam == null) return;
            Vector2 centre = new Vector2(0.5f, 0.5f);
            if (hasCentre)
            {
                Vector3 vp = cam.WorldToViewportPoint(blurCentreWorld);
                if (vp.z > 0f) centre = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
            }
            material.SetVector("_FXCenter", centre);
            material.SetFloat("_FXBlur", CurrentBlur);
            material.SetFloat("_FXLines", CurrentLines);
            material.SetFloat("_FXMono", mono);
            material.SetFloat("_FXInvert", invert);
            material.SetFloat("_FXFlash", flash);
            material.SetColor("_FXTint", impactTint);
            material.SetFloat("_FXSeed", Mathf.Floor(Time.unscaledTime * 12f));
        }

        bool Active => CurrentBlur > 0.0005f || CurrentLines > 0.01f || impactTime < impactDuration;

        void OnBeginCamera(ScriptableRenderContext ctx, Camera c)
        {
            if (c != cam || material == null || !Active) return;
            var data = c.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            pass.material = material;
            data.scriptableRenderer.EnqueuePass(pass);
        }

        class FXPass : ScriptableRenderPass
        {
            public Material material;

            public FXPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_NightfarerScreenFX";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Nightfarer Screen FX");
                resources.cameraColor = destination;
            }
        }
    }
}
