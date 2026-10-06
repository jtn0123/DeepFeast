using UnityEngine;

namespace DeepFeast
{
    // The scene camera draws into its own target, sized by the render scale and multisampled by the
    // anti-aliasing setting; this camera then puts that picture on screen (or into the capture
    // target), adding glow, the habitat's colour grade and water ripples on the way. The HUD canvas
    // draws over the result, so neither render scale nor grading touches the text.
    public sealed class Presenter : MonoBehaviour
    {
        public const int UiLayer = 5;
        // Color and depth samples kept in the scene target, about 1.5 GB at most.
        const long SampleBudget = 128L * 1024 * 1024;
        const int MaxBloomLevels = 6, MaxRipples = 6;
        const float RippleLife = 0.8f;

        // Glow levels: off, subtle, medium, strong.
        // Only light brighter than the open water blooms, so the shallows never haze over.
        static readonly float[] GlowIntensity = { 0, 0.35f, 0.6f, 0.95f };
        static readonly float[] GlowThreshold = { 1, 1.05f, 0.97f, 0.9f };
        const float GlowKnee = 0.15f;

        Camera scene, present, ui;
        RenderTexture target;
        Material post;
        readonly RenderTexture[] down = new RenderTexture[MaxBloomLevels], up = new RenderTexture[MaxBloomLevels];
        readonly Vector4[] ripples = new Vector4[MaxRipples];
        readonly float[] rippleAge = new float[MaxRipples];
        readonly float[] rippleForce = new float[MaxRipples];
        int nextRipple;
        Vector4 gain = Vector4.one, lift = Vector4.zero, grade = new Vector4(1, 1, 0, 0);

        struct Grade
        {
            public Vector4 gain, lift;
            public float saturation, contrast;
            public Grade(Color gain, Color lift, float saturation, float contrast)
            { this.gain = gain; this.lift = lift; this.saturation = saturation; this.contrast = contrast; }
        }
        // Warm, saturated reef shallows; golden-green kelp; a cold, deep blue abyss with lifted blacks
        // that lets the glowing creatures stand out.
        static readonly Grade Reef = new Grade(new Color(1.025f, 1, 0.96f), new Color(0, 0.003f, 0.008f), 1.08f, 1.05f);
        static readonly Grade Kelp = new Grade(new Color(1.01f, 1.02f, 0.92f), new Color(0.005f, 0.008f, 0), 1.06f, 1.05f);
        static readonly Grade Abyss = new Grade(new Color(0.96f, 0.98f, 1.04f), new Color(0, 0.004f, 0.014f), 1.06f, 1.1f);

        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex"),
            BaseTexId = Shader.PropertyToID("_BaseTex"), BloomTexelId = Shader.PropertyToID("_BloomTex_TexelSize"),
            ThresholdId = Shader.PropertyToID("_Threshold"), IntensityId = Shader.PropertyToID("_Intensity"),
            AspectId = Shader.PropertyToID("_Aspect"), BloomTintId = Shader.PropertyToID("_BloomTint"),
            GainId = Shader.PropertyToID("_Gain"), LiftId = Shader.PropertyToID("_Lift"),
            GradeId = Shader.PropertyToID("_Grade"), RipplesId = Shader.PropertyToID("_Ripples");

        /// The size and multisampling the scene is drawn at right now.
        public static Vector2Int SceneSize { get; private set; }
        public static int ActiveMsaa { get; private set; } = 1;

        public static Presenter Create(Camera scene)
        {
            scene.cullingMask &= ~(1 << UiLayer);
            var go = new GameObject("Presenter");
            var cam = go.AddComponent<Camera>();
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.depth = scene.depth + 1;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var p = go.AddComponent<Presenter>();
            p.scene = scene;
            p.present = cam;
            var shader = Resources.Load<Shader>("Shaders/PostFx");
            if (shader != null && shader.isSupported) p.post = new Material(shader) { name = "PostFx" };
            else Debug.LogWarning("[DeepFeast] PostFx shader unavailable; glow, grading and ripples are off.");
            return p;
        }

        /// Blends the colour grade for where the camera is: reef, kelp forest or abyss.
        public void SetHabitat(Habitat.Look look)
        {
            float reef = Mathf.Clamp01(1 - look.kelp - look.abyss), strength = GameSettings.Data.grading * 0.5f;
            Vector4 Mix(Vector4 a, Vector4 b, Vector4 c) => a * reef + b * look.kelp + c * look.abyss;
            float Blend(float a, float b, float c) => a * reef + b * look.kelp + c * look.abyss;
            gain = Vector4.Lerp(Vector4.one, Mix(Reef.gain, Kelp.gain, Abyss.gain), strength);
            lift = Vector4.Lerp(Vector4.zero, Mix(Reef.lift, Kelp.lift, Abyss.lift), strength);
            grade = new Vector4(Mathf.Lerp(1, Blend(Reef.saturation, Kelp.saturation, Abyss.saturation), strength),
                Mathf.Lerp(1, Blend(Reef.contrast, Kelp.contrast, Abyss.contrast), strength), 0, 0);
        }

        /// Starts a ring of bent water at a point on screen (0..1, origin bottom left).
        public void AddRipple(Vector2 viewport, float force = 1)
        {
            if (!GameSettings.Data.ripples) return;
            int i = nextRipple;
            nextRipple = (nextRipple + 1) % MaxRipples;
            ripples[i] = new Vector4(viewport.x, viewport.y, 0, 0);
            rippleAge[i] = 0;
            rippleForce[i] = force;
        }

        /// Grows and fades the ripples (real seconds; the game passes 0 while paused).
        public void Step(float dt)
        {
            for (int i = 0; i < MaxRipples; i++)
            {
                if (rippleForce[i] <= 0) continue;
                rippleAge[i] += dt;
                float t = rippleAge[i] / RippleLife;
                if (t >= 1) { rippleForce[i] = 0; ripples[i].w = 0; continue; }
                ripples[i].z = 0.015f + t * 0.17f;
                ripples[i].w = rippleForce[i] * (1 - t) * (1 - t) * Mathf.Clamp01(t * 8);
            }
        }

        bool Rippling
        {
            get
            {
                if (!GameSettings.Data.ripples) return false;
                foreach (var r in ripples) if (r.w > 0) return true;
                return false;
            }
        }

        /// Headless shots: the picture goes into an image instead of the screen, and a UI camera
        /// draws the HUD over it. Returns that camera for the canvas.
        public Camera CaptureTo(RenderTexture output)
        {
            present.targetTexture = output;
            ui = new GameObject("UiCamera").AddComponent<Camera>();
            ui.transform.position = new Vector3(0, 0, -100000);
            ui.orthographic = true;
            ui.cullingMask = 1 << UiLayer;
            ui.clearFlags = CameraClearFlags.Depth;
            ui.depth = present.depth + 1;
            ui.allowHDR = false;
            ui.allowMSAA = false;
            ui.targetTexture = output;
            return ui;
        }

        /// Draws the whole frame again right now, for a capture that must match this frame's state.
        public void RenderNow()
        {
            scene.Render();
            present.Render();
            if (ui != null) ui.Render();
        }

        /// Matches the scene target to the output size and the current settings.
        public void Fit(int width, int height)
        {
            var d = GameSettings.Data;
            float s = d.renderScale / 100f;
            int max = SystemInfo.maxTextureSize;
            int w = Mathf.Clamp(Mathf.RoundToInt(width * s), 16, max), h = Mathf.Clamp(Mathf.RoundToInt(height * s), 16, max);
            int msaa = d.msaa;
            // Very large targets drop to fewer samples rather than exhausting memory.
            while (msaa > 1 && (long)w * h * msaa > SampleBudget) msaa /= 2;
            if (target != null && target.width == w && target.height == h && target.antiAliasing == msaa) return;

            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.DefaultHDR, 24) { msaaSamples = msaa };
            desc.msaaSamples = msaa = Mathf.Max(1, Mathf.Min(msaa, SystemInfo.GetRenderTextureSupportedMSAASampleCount(desc)));
            Release();
            target = new RenderTexture(desc) { name = "SceneTarget", filterMode = FilterMode.Bilinear };
            target.Create();
            scene.targetTexture = target;
            SceneSize = new Vector2Int(w, h);
            ActiveMsaa = msaa;
            Debug.Log($"[DeepFeast] scene target {w}x{h} ({d.renderScale}% of {width}x{height}), {msaa}x MSAA");
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            var source = target != null ? target : src;
            var d = GameSettings.Data;
            float intensity = GlowIntensity[d.glow];
            bool rippling = Rippling;
            if (post == null || intensity <= 0 && d.grading == 0 && !rippling)
            {
                Graphics.Blit(source, dst);
                return;
            }
            int outW = dst != null ? dst.width : Screen.width, outH = dst != null ? dst.height : Screen.height;
            int levels = intensity > 0 ? Bloom(source, outW, outH, GlowThreshold[d.glow]) : 0;
            var bloom = levels > 0 ? up[0] : null;
            post.SetTexture(BloomTexId, bloom != null ? bloom : (Texture)Texture2D.blackTexture);
            if (bloom != null) post.SetVector(BloomTexelId, new Vector4(1f / bloom.width, 1f / bloom.height, bloom.width, bloom.height));
            post.SetFloat(IntensityId, intensity);
            post.SetVector(BloomTintId, new Vector4(1, 1, 1, 0.6f));
            post.SetVector(GainId, gain);
            post.SetVector(LiftId, lift);
            post.SetVector(GradeId, grade);
            post.SetFloat(AspectId, outW / (float)Mathf.Max(1, outH));
            if (!rippling) for (int i = 0; i < MaxRipples; i++) ripples[i].w = 0;
            post.SetVectorArray(RipplesId, ripples);
            Graphics.Blit(source, dst, post, 3);
            for (int i = 0; i < levels; i++)
            {
                RenderTexture.ReleaseTemporary(down[i]);
                if (up[i] != down[i]) RenderTexture.ReleaseTemporary(up[i]);
                down[i] = up[i] = null;
            }
        }

        // Prefilters at half the output size, halves down to a small mip, then adds each level back
        // into the one above. Returns the number of levels; up[0] holds the result.
        int Bloom(RenderTexture source, int outW, int outH, float threshold)
        {
            post.SetVector(ThresholdId, new Vector4(threshold, threshold - GlowKnee, 2 * GlowKnee, 0.25f / GlowKnee));
            int w = Mathf.Max(1, outW / 2), h = Mathf.Max(1, outH / 2), n = 0;
            down[n] = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
            Graphics.Blit(source, down[n++], post, 0);
            while (n < MaxBloomLevels && h > 32)
            {
                w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
                down[n] = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.DefaultHDR);
                Graphics.Blit(down[n - 1], down[n], post, 1);
                n++;
            }
            up[n - 1] = down[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                up[i] = RenderTexture.GetTemporary(down[i].width, down[i].height, 0, RenderTextureFormat.DefaultHDR);
                post.SetTexture(BaseTexId, down[i]);
                Graphics.Blit(up[i + 1], up[i], post, 2);
            }
            return n;
        }

        void Release()
        {
            if (target == null) return;
            // At quit the scene camera may already be gone.
            if (scene != null) scene.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }

        void OnDestroy()
        {
            Release();
            if (post != null) Destroy(post);
        }
    }
}
