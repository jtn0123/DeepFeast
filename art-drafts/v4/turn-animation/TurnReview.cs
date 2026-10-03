using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeepFeast
{
    // Capture-only driver: identical scripted turning inputs through each revision's real FishView/shader.
    // Kept outside production Assets; installed only in an isolated project copy.
    public sealed class TurnReview : MonoBehaviour
    {
        const int FPS = 30, FRAMES = 120;
        Camera reviewCamera;
        RenderTexture target;
        Texture2D readback;
        FishView[] views;
        Fish[] fish;
        string output;
        int frame, warmup;
        [Serializable] sealed class Sample { public int frame; public float time, facing; public float[] width; }
        [Serializable] sealed class Evidence { public int fps = FPS, frames = FRAMES; public List<Sample> samples = new List<Sample>(); }
        readonly Evidence evidence = new Evidence();

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-shots");
            if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("-shots is required.");
            output = args[i + 1]; Directory.CreateDirectory(output);
            Application.runInBackground = true;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            reviewCamera = new GameObject("ReviewCamera").AddComponent<Camera>();
            reviewCamera.orthographic = true; reviewCamera.orthographicSize = 400;
            reviewCamera.transform.position = new Vector3(0, -1300, -10);
            reviewCamera.clearFlags = CameraClearFlags.SolidColor;
            reviewCamera.backgroundColor = new Color(0.035f, 0.12f, 0.18f, 1);
            reviewCamera.nearClipPlane = 0.1f; reviewCamera.farClipPlane = 100;
            target = new RenderTexture(960, 720, 24); target.Create();
            reviewCamera.targetTexture = target; reviewCamera.enabled = false;
            readback = new Texture2D(960, 720, TextureFormat.RGB24, false);
            var root = new GameObject("ReviewFish").transform;
            FishArt.Prewarm();
            var species = new[] { Data.Player, Data.SpeciesMap["clown"], Data.Shark };
            var sizes = new[] { 125f, 110f, 130f };
            var y = new[] { 1065f, 1300f, 1535f };
            views = new FishView[3]; fish = new Fish[3];
            for (int f = 0; f < 3; f++)
            {
                views[f] = new FishView(root, root); views[f].SetSpecies(species[f]);
                fish[f] = new Fish { sp = species[f], r = sizes[f], x = 0, y = y[f], vx = 80, face = 1, faceS = 1 };
            }
            Shader.SetGlobalColor("_SceneLight", new Color(0.92f, 0.98f, 0.94f));
        }

        void Update()
        {
            try
            {
                PaintedArt.PrepareMeshes();
                if (warmup++ < 2) return;
                if (!PaintedArt.SwimMaterial.shader.isSupported) throw new InvalidOperationException("Fish shader unsupported.");
                float t = frame / (float)FPS;
                float facing = Mathf.Cos(t * Mathf.PI / 2);
                var sample = new Sample { frame = frame, time = t, facing = facing, width = new float[3] };
                for (int f = 0; f < fish.Length; f++)
                {
                    fish[f].faceS = facing; fish[f].face = facing < 0 ? -1 : 1;
                    fish[f].wag = 4 + t * 5; fish[f].mouth = 0; fish[f].chomp = 0;
                    views[f].Pose(fish[f], 100 + f, FishView.EyeMode.Normal);
                    sample.width[f] = Mathf.Abs(views[f].root.transform.localScale.x);
                }
                evidence.samples.Add(sample);
                reviewCamera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); readback.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(System.IO.Path.Combine(output, $"frame_{frame:000}.png"), readback.EncodeToPNG());
                if (++frame >= FRAMES)
                {
                    File.WriteAllText(System.IO.Path.Combine(output, "native-samples.json"), JsonUtility.ToJson(evidence, true));
                    Debug.Log($"[TurnReview] complete: {frame} native frames at {FPS} FPS scripted timeline, closed jaw, 3 species.");
                    Application.Quit(0);
                }
            }
            catch (Exception e) { Debug.LogException(e); enabled = false; Application.Quit(1); }
        }
    }
}
