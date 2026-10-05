using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DeepFeast
{
    // The command-line test harness: its flags, screenshots and recordings, exit codes, and the
    // autoplay run's restarts and stat lines. The reviews are in GameReviews.cs, the bot in
    // GameBot.cs and the virtual gamepad check in PadFlow.cs.
    public sealed partial class Game
    {
        bool autoplay, noPause, gallery, animateGallery, padTest;
        string shotDir, scenery, interfaceReview;
        float worstFrame, shotEvery = 15, nextShot, quitAfter, startSize, startX = -1, firstShark = -1, finaleDelay = 12, pearlEvery, realTime, statT, restartT = -1, notch;
        int shotN, uiFlowStage, captureFrame = -1, gcSeen;
        bool menuShotDone, turnReviewLogged, rippleShown, harnessRun, exiting, flowDone;
        RenderTexture captureTarget;
        string captureName;
        Recorder recorder;

        // Every flag that makes a run a test or review rather than play.
        static readonly string[] HarnessFlags =
        {
            "-autoplay", "-gallery", "-padtest", "-scenery", "-interface", "-shots", "-shotevery", "-quitafter", "-record",
            "-size", "-startx", "-shark", "-sharkvariant", "-finale", "-pearlevery", "-timescale", "-set", "-notch",
            "-dumpart", "-nopause",
        };
        static string[] args;
        static bool Has(string k) => Array.IndexOf(args, k) >= 0;
        static string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static float ArgF(string k, float d) => float.TryParse(Arg(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

        void ParseArgs()
        {
            args = Environment.GetCommandLineArgs();
            persist = !Application.isBatchMode && !Array.Exists(HarnessFlags, Has);
            if (!persist) Debug.Log("[DeepFeast] test run: the best score, Fishdex, settings and mute switch are not saved");
            autoplay = Has("-autoplay");
            gallery = Has("-gallery");
            padTest = Has("-padtest");
            animateGallery = Has("-animate-gallery") || Has("-animate-turns");
            scenery = Arg("-scenery");
            interfaceReview = Arg("-interface");
            if (interfaceReview != null && scenery == null) scenery = "reef";
            noPause = autoplay || padTest || scenery != null || Has("-nopause");
            shotDir = Arg("-shots");
            shotEvery = ArgF("-shotevery", 15);
            quitAfter = ArgF("-quitafter", 0);
            startSize = ArgF("-size", 0);
            startX = ArgF("-startx", -1);
            firstShark = ArgF("-shark", -1);
            nextSharkVariant = (int)ArgF("-sharkvariant", 0);
            finaleDelay = ArgF("-finale", 12);
            // Test runs can drop pearls on a fixed beat, cycling through every kind.
            pearlEvery = ArgF("-pearlevery", 0);
            notch = ArgF("-notch", 0);
            float ts = ArgF("-timescale", 1);
            if (ts != 1) Time.timeScale = ts;
            if (shotDir != null) System.IO.Directory.CreateDirectory(shotDir);
            if (shotDir != null && Application.isBatchMode && ArgF("-record", 0) > 0) recorder = new Recorder(shotDir, ArgF("-record", 0));
            harnessRun = autoplay || gallery || padTest || scenery != null || shotDir != null || quitAfter > 0;
            Raster.DumpDir = Arg("-dumpart");
            if (Raster.DumpDir != null) System.IO.Directory.CreateDirectory(Raster.DumpDir);
            // A test or review run fails on the first error or exception it logs.
            if (harnessRun) Application.logMessageReceived += FailOnError;
            if (scenery != null) UnityEngine.Random.InitState(20261003);
        }

        // Headless captures render the camera and HUD into their own target instead of the screen.
        void SetupCapture()
        {
            if (!Application.isBatchMode || shotDir == null) return;
            captureTarget = new RenderTexture((int)ArgF("-screen-width", 1280), (int)ArgF("-screen-height", 720), 24);
            captureTarget.Create();
            hud.UseCaptureCamera(presenter.CaptureTo(captureTarget));
        }

        void CloseHarness()
        {
            Application.logMessageReceived -= FailOnError;
            recorder?.Close();
            if (captureTarget == null) return;
            captureTarget.Release();
            Destroy(captureTarget);
        }

        void Shot(string name)
        {
            if (shotDir == null) return;
            if (captureTarget != null) { captureName = name; return; }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotDir, name + ".png"));
            Debug.Log($"[DeepFeast] shot {name}");
        }

        // A real native-player camera capture when no display/backbuffer is available, including the same uGUI HUD.
        void LateUpdate()
        {
            if (recorder != null && captureTarget != null)
            {
                Canvas.ForceUpdateCanvases();
                presenter.RenderNow();
                recorder.Frame(captureTarget);
            }
            if (captureName == null) return;
            Canvas.ForceUpdateCanvases();
            presenter.RenderNow();
            var old = RenderTexture.active;
            var image = new Texture2D(captureTarget.width, captureTarget.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = captureTarget;
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(shotDir, captureName + ".png"), image.EncodeToPNG());
                Debug.Log($"[DeepFeast] native camera shot {captureName} ({image.width}x{image.height})");
            }
            finally { RenderTexture.active = old; Destroy(image); captureName = null; captureFrame = Time.frameCount; }
        }


        // A harness run ends with an exit code a script can check: 0 when it ran cleanly, 1 when a
        // check failed, anything logged an error or exception, or a flow had not finished in time.
        void Exit(int code, string why)
        {
            if (exiting) return;
            exiting = true;
            Debug.Log($"[DeepFeast] exit {code}: {why}");
            Application.Quit(code);
        }

        void FailOnError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Exit(1, $"{type}: {message}");
        }

        void QuitWhenDue()
        {
            if (quitAfter <= 0 || realTime < quitAfter) return;
            bool flow = padTest || interfaceReview == "flow";
            if (flow && !flowDone) Exit(1, $"the {(padTest ? "gamepad" : "UI")} flow stalled at stage {(padTest ? padStage : uiFlowStage)} by {quitAfter} s");
            else Exit(0, "quit after " + quitAfter);
        }

        // A finished flow has nothing more to show, so a headless run ends there.
        void FlowPassed(string flow)
        {
            Debug.Log($"[DeepFeast] complete {flow} flow passed.");
            flowDone = true;
            if (Application.isBatchMode) Exit(0, flow + " flow passed");
        }

        void Harness(float rdt)
        {
            realTime += rdt;
            QuitWhenDue();
            if (padTest) { PadFlow(); return; }
            if (!autoplay && shotDir == null) return;
            if (state == GState.Menu && realTime > 2.5f)
            {
                if (!menuShotDone) { menuShotDone = true; Shot("menu"); return; }
                if (autoplay && realTime > 3) { StartGame(); nextShot = 4; }
            }
            if (state == GState.Play && shotDir != null && playTime >= nextShot)
            {
                Shot($"play_{shotN++:00}_t{(int)playTime}_r{(int)player.r}");
                nextShot = playTime + shotEvery;
            }
            if (state == GState.Play && autoplay)
            {
                statT -= rdt;
                WatchBot(rdt);
                // Every long frame is reported with what it overlapped: a screenshot being written
                // (test runs only) or a garbage collection. A recording's frames are all slow by design.
                int gc = System.GC.CollectionCount(0);
                if (recorder == null && Time.unscaledDeltaTime > 0.05f)
                    Debug.Log($"[DeepFeast] long frame {Time.unscaledDeltaTime * 1000:0} ms at t={playTime:0.0}: " +
                        (captureFrame == Time.frameCount - 1 ? "after a screenshot" : gc != gcSeen ? $"garbage collection ({System.GC.CollectionCount(1)} gen1, {System.GC.CollectionCount(2)} gen2 so far)" : "no capture or collection") +
                        $", fish={fish.Count}, particles={parts.list.Count}");
                gcSeen = gc;
                // Screenshots are the harness's own cost, not the game's.
                if (captureFrame != Time.frameCount - 1) worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                if (statT <= 0)
                {
                    statT = 10;
                    Debug.Log($"[DeepFeast] stat t={(int)playTime} r={player.r:0.0} tier={Data.Tiers[tier].name} lives={lives} score={score} eaten={eaten} fish={fish.Count} fps={1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):0} worst={worstFrame * 1000:0}ms still={TakeBotStill():0}%");
                    // The cast around the player shows that spawns follow the habitat.
                    var cast = new SortedDictionary<string, int>();
                    foreach (var f in fish) { cast.TryGetValue(f.sp.key, out int n); cast[f.sp.key] = n + 1; }
                    Debug.Log($"[DeepFeast] cast y={player.y:0} zone={Habitat.At(player.y).name}: " + string.Join(", ", System.Linq.Enumerable.Select(cast, c => c.Key + "=" + c.Value)));
                    worstFrame = 0;
                }
            }
            if (state == GState.Victory && autoplay)
            {
                if (restartT < 0) { restartT = 3; }
                restartT -= rdt;
                if (restartT <= 1.5f && restartT + rdt > 1.5f) Shot($"victory_{shotN++:00}");
                if (restartT <= 0) { restartT = -1; hud.SubmitPrimary(); nextShot = playTime + 2; }
            }
            if (state == GState.Over && autoplay)
            {
                if (restartT < 0) { restartT = 2.5f; }
                restartT -= rdt;
                if (restartT <= 1.2f && restartT + rdt > 1.2f) Shot($"over_{shotN++:00}");
                if (restartT <= 0) { restartT = -1; StartGame(); nextShot = playTime + 2; }
            }
        }
    }
}
