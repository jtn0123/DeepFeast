using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DeepFeast
{
    // Opt-in native review of the actual gameplay models, not a separate illustration renderer.
    public sealed class SpeciesVisualReview : MonoBehaviour
    {
        const int FPS = 30, FRAMES = 120, WIDTH = 1280, HEIGHT = 960;
        Camera camera3;
        RenderTexture target;
        Texture2D readback;
        FishView[] views;
        Fish[] fish;
        string output;
        int frame, warmup;
        [Serializable] sealed class Identity
        {
            public string key, displayName, scientificName, referenceUrl;
            public float chaseSpeedMultiplier;
        }
        [Serializable] sealed class Sample { public int frame; public float facing, mouth; }
        [Serializable] sealed class Evidence
        {
            public int fps = FPS, frames = FRAMES, width = WIDTH, height = HEIGHT;
            public string group, renderer = "Native Unity FishView / FishVolume";
            public List<Identity> species = new List<Identity>();
            public List<Sample> samples = new List<Sample>();
        }
        readonly Evidence evidence = new Evidence();

        static string Argument(string[] args, string key, string fallback = null)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            evidence.group = Argument(args, "-review-group", "sharks");
            output = Argument(args, "-shots") ?? throw new ArgumentException("-shots is required.");
            Directory.CreateDirectory(output);
            FishVolume.Style = FishVolume.StyleFromArgs(args);
            Application.runInBackground = true;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            camera3 = new GameObject("SpeciesReviewCamera").AddComponent<Camera>();
            camera3.orthographic = true; camera3.orthographicSize = 480;
            camera3.transform.position = new Vector3(0, -1500, -2000);
            camera3.clearFlags = CameraClearFlags.SolidColor;
            camera3.backgroundColor = new Color(.035f, .12f, .18f, 1);
            camera3.nearClipPlane = .1f; camera3.farClipPlane = 5000;
            target = new RenderTexture(WIDTH, HEIGHT, 24); target.Create();
            camera3.targetTexture = target; camera3.enabled = false;
            readback = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, false);
            var root = new GameObject("SpeciesReviewFish").transform;
            var canvas = new GameObject("ReviewLabels").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera3;
            canvas.planeDistance = 1; canvas.sortingOrder = 1000;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(WIDTH, HEIGHT);
            string[] keys = evidence.group switch
            {
                "atlantic-a" => new[] { "almaco_jack", "goliath_grouper", "atlantic_halibut", "atlantic_mackerel" },
                "atlantic-b" => new[] { "mahi_mahi", "skipjack_tuna", "striped_bass", "yellowfin_tuna" },
                "extras" => new[] { "bluefish", "red_drum" },
                "kelp" => new[] { "pacific_sardine", "garibaldi", "california_sheephead", "lingcod" },
                "deep" => new[] { "lanternfish", "hatchetfish" },
                "legacy-a" => new[] { "minnow", "clown", "tang", "angel" },
                "legacy-b" => new[] { "puffer", "parrot", "snapper", "barracuda" },
                "legacy-c" => new[] { "grouper", "tuna", "player" },
                "sharks" => new[] { "shark", "tiger_shark", "mako_shark" },
                _ => throw new ArgumentException("Unknown review group: " + evidence.group)
            };
            views = new FishView[keys.Length]; fish = new Fish[keys.Length];
            FishArt.Prewarm();
            bool stacked = keys.Length == 3 || keys.Length == 2;
            int rows = stacked ? keys.Length : 2;
            float cellHeight = HEIGHT / (float)rows;
            for (int i = 0; i < keys.Length; i++)
            {
                Species sp = keys[i] == "shark" ? Data.Shark : keys[i] == "tiger_shark" ? Data.TigerShark :
                    keys[i] == "mako_shark" ? Data.MakoShark : keys[i] == "player" ? Data.Player : Data.SpeciesMap[keys[i]];
                float x = stacked ? 0 : i % 2 == 0 ? -320 : 320;
                float y = 1020 + (stacked ? i : i / 2) * cellHeight + cellHeight * (keys.Length == 3 ? .64f : .53f);
                views[i] = new FishView(root, root); views[i].SetSpecies(sp);
                // Tall legacy angelfish needs extra margin above/below its long fins.
                float radius = sp.key == "angel" ? 92 : keys.Length == 3 ? 133 : stacked ? 155 : 135;
                fish[i] = new Fish { sp = sp, x = x, y = y, r = radius, vx = 80, face = 1, faceS = 1 };
                evidence.species.Add(new Identity { key = sp.key, displayName = sp.displayName,
                    scientificName = sp.scientificName, referenceUrl = sp.referenceUrl, chaseSpeedMultiplier = sp.chaseSpeedMultiplier });
                Label(canvas.transform, sp.displayName, x, -((stacked ? i : i / 2) * cellHeight + 30), 26, FontStyle.Bold);
                Label(canvas.transform, sp.key, x, -((stacked ? i : i / 2) * cellHeight + 62), 16, FontStyle.Normal);
            }
            Shader.SetGlobalColor("_SceneLight", new Color(.92f, .98f, .94f));
        }

        static void Label(Transform parent, string value, float x, float y, int size, FontStyle style)
        {
            var go = new GameObject(value, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(600, 38);
            var text = go.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.fontStyle = style;
            text.color = style == FontStyle.Bold ? new Color(.91f,.97f,.94f) : new Color(.50f,.70f,.74f);
            text.alignment = TextAnchor.UpperCenter; text.raycastTarget = false;
        }

        void Update()
        {
            try
            {
                PaintedArt.PrepareMeshes();
                if (warmup++ < 2) return;
                if (!Resources.Load<Shader>("Shaders/FishVolume").isSupported) throw new InvalidOperationException("Volume shader unsupported.");
                float t = frame / (float)FPS, facing = Mathf.Cos(t * Mathf.PI / 2);
                // Open at the 45-degree view, then recover before the opposite flank appears.
                float bite = Mathf.SmoothStep(0, 1, Mathf.Clamp01(1 - Mathf.Abs(t - .5f) / .30f));
                for (int i = 0; i < fish.Length; i++)
                {
                    fish[i].faceS = facing; fish[i].face = facing < 0 ? -1 : 1;
                    fish[i].wag = 4 + t * 5; fish[i].mouth = bite;
                    views[i].Pose(fish[i], 100 + i, FishView.EyeMode.Normal, 1f / FPS);
                }
                evidence.samples.Add(new Sample { frame = frame, facing = facing, mouth = bite });
                Canvas.ForceUpdateCanvases(); camera3.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                readback.ReadPixels(new Rect(0,0,WIDTH,HEIGHT), 0, 0); readback.Apply(); RenderTexture.active = previous;
                File.WriteAllBytes(System.IO.Path.Combine(output, $"frame_{frame:000}.png"), readback.EncodeToPNG());
                if (++frame >= FRAMES)
                {
                    File.WriteAllText(System.IO.Path.Combine(output, "native-samples.json"), JsonUtility.ToJson(evidence, true));
                    Debug.Log($"[SpeciesVisualReview] complete: {frame} native frames, group={evidence.group}, {fish.Length} species.");
                    Application.Quit(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); enabled = false; Application.Quit(1); }
        }

        void OnDestroy()
        {
            if (target != null) { target.Release(); Destroy(target); }
            if (readback != null) Destroy(readback);
        }
    }
}
