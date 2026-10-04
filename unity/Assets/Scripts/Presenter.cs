using UnityEngine;

namespace DeepFeast
{
    // The scene camera draws into its own target, sized by the render scale and multisampled by the
    // anti-aliasing setting; this camera then puts that picture on screen (or into the capture
    // target). The HUD canvas draws over the result, so render scale never blurs the text.
    public sealed class Presenter : MonoBehaviour
    {
        public const int UiLayer = 5;
        // Color and depth samples kept in the scene target, about 1.5 GB at most.
        const long SampleBudget = 128L * 1024 * 1024;

        Camera scene, present, ui;
        RenderTexture target;

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
            return p;
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
            Graphics.Blit(target != null ? (Texture)target : src, dst);
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

        void OnDestroy() => Release();
    }
}
