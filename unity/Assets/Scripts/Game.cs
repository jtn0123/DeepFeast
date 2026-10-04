using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DeepFeast
{
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-species-review") >= 0)
            {
                new GameObject("SpeciesVisualReview").AddComponent<SpeciesVisualReview>(); return;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-turn-animation") >= 0)
            {
                new GameObject("FishTurnReview").AddComponent<FishTurnReview>(); return;
            }
            FishVolume.Style = FishVolume.StyleFromArgs(Environment.GetCommandLineArgs());
            if (UnityEngine.Object.FindAnyObjectByType<Game>() == null) new GameObject("DeepFeast").AddComponent<Game>();
        }
    }

    /// <summary>The whole game loop: state machine, player, AI, spawning, camera and rendering glue.</summary>
    public sealed partial class Game : MonoBehaviour
    {
        enum GState { Menu, Play, Paused, Over, Victory }

        // ------------------------------------------------------------------ systems
        Camera cam3;
        Presenter presenter;
        World world;
        SceneFx fx;
        Hud hud;
        Sfx sfx;
        Particles parts;
        Transform worldRoot, fishRoot, haloRoot, fxRoot;
        Mesh jellyMesh;
        readonly MeshBuilder jmb = new MeshBuilder();

        // ------------------------------------------------------------------ state
        GState state = GState.Menu;
        int score, lives = 3, combo, eaten, tier, best, newSpecies;
        float comboT, playTime, sharkT = 30, pearlT = 18, slowT, shake, dieT, time;
        // The finale: once the hero is a Legend the whale shark arrives, too big to swallow; each
        // bite takes a chunk out of it until it is small enough to eat, which wins the game.
        float finaleT = -1, victoryT = -1, biteCool;
        int finaleBites;
        bool endless;
        Fish finale;
        readonly HashSet<string> hints = new HashSet<string>();

        Vector2 cam = new Vector2(7000, 1500);
        float zoom = 1, menuCamX = 7000, menuDir = 1;
        float pxPerRef = 1, refW = 1400, refH = 820;

        readonly Fish player = new Fish { sp = null };
        bool pActive, pAlive, pTired, pDashing;
        float pInvuln, pStamina = 1, pBlink, pBlinkT = 3, pStun, pStungCool, wakeT, rippleT;
        // Seconds left on each pearl power, indexed by PearlKind.
        readonly float[] power = new float[Powers.Count];
        float pShield => power[(int)PearlKind.Shield];
        bool Powered(PearlKind k) => power[(int)k] > 0;
        float MagnetReach => player.r * 6 + 60;
        FishView playerView;
        RenderTexture captureTarget;
        string captureName;
        SpriteRenderer playerGlow, shieldRing, shieldFill, shieldShine;
        readonly SpriteRenderer[] stunStars = new SpriteRenderer[3];

        readonly List<Fish> fish = new List<Fish>();
        readonly List<School> schools = new List<School>();
        readonly List<Jelly> jellies = new List<Jelly>();
        readonly List<Pearl> pearls = new List<Pearl>();
        // The few fish the magnet is reeling in right now, nearest first.
        readonly List<(float d, Fish f)> magnetPull = new List<(float, Fish)>();
        const int MagnetHold = 4;
        readonly Stack<FishView> viewPool = new Stack<FishView>();
        readonly List<Hole> holes = new List<Hole>();
        readonly List<Alert> alerts = new List<Alert>();
        readonly Dictionary<float, Sprite> bellSprites = new Dictionary<float, Sprite>();
        Sprite pearlSprite, pearlStar, pearlRing;
        int nextId = 1, nextSharkVariant, pearlSeq;

        // ------------------------------------------------------------------ input
        bool kbMode, pointerMode, touchMode;
        Vector2 pointer;
        Vector3 lastMouse;

        // ------------------------------------------------------------------ test harness (command line)
        bool autoplay, noPause, gallery, animateGallery, padTest;
        string shotDir, scenery, interfaceReview;
        float worstFrame, shotEvery = 15, nextShot, quitAfter, startSize, startX = -1, firstShark = -1, finaleDelay = 12, pearlEvery, realTime, statT, botWanderDir = 1, restartT = -1;
        int shotN, uiFlowStage;
        bool menuShotDone, turnReviewLogged, portraitsTaken, rippleShown;

        float FocusR => pActive ? player.r : 22;
        float ViewW => refW / zoom;
        float ViewH => refH / zoom;
        bool Playing => state == GState.Play && pActive && pAlive;
        bool Legend => tier >= Data.Tiers.Length - 1;
        // Test and review runs never write the player's best score or Fishdex.
        bool Persist => !autoplay && scenery == null && !gallery && !padTest;

        // ================================================================== setup
        void Awake()
        {
            ParseArgs();
            if (scenery != null) UnityEngine.Random.InitState(20261003);
            Application.runInBackground = true;
            GameSettings.Load(Persist, Arg("-set"));
            GameSettings.ApplyAtBoot();
            // The scene is multisampled in its own target; the screen itself needs no samples.
            QualitySettings.antiAliasing = 0;

            var camGo = new GameObject("Camera");
            cam3 = camGo.AddComponent<Camera>();
            cam3.orthographic = true;
            cam3.clearFlags = CameraClearFlags.SolidColor;
            cam3.backgroundColor = U.Hex("#03121f");
            cam3.nearClipPlane = 0.1f; cam3.farClipPlane = 5000;
            camGo.AddComponent<AudioListener>();
            presenter = Presenter.Create(cam3);

            worldRoot = new GameObject("WorldRoot").transform;
            world = new World(worldRoot);
            fx = new SceneFx(cam3, worldRoot);
            fishRoot = new GameObject("Fish").transform; fishRoot.SetParent(worldRoot, false);
            haloRoot = new GameObject("Halos").transform; haloRoot.SetParent(worldRoot, false);
            fxRoot = new GameObject("Fx").transform; fxRoot.SetParent(worldRoot, false);
            jellyMesh = Gfx.MeshObject("JellyTentacles", fxRoot, Layer.Jelly - 1).mesh;
            parts = new Particles(fxRoot);
            FishArt.Prewarm();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int volumes = FishVolume.Prewarm();
            Debug.Log($"[DeepFeast] prewarmed {volumes} fish volumes in {watch.ElapsedMilliseconds} ms");
            BuildPlayerView();
            BuildPearlArt();

            sfx = new Sfx(gameObject, autoplay || Has("-mute"));
            hud = new Hud();
            if (Application.isBatchMode && shotDir != null)
            {
                captureTarget = new RenderTexture((int)ArgF("-screen-width", 1280), (int)ArgF("-screen-height", 720), 24);
                captureTarget.Create();
                hud.UseCaptureCamera(presenter.CaptureTo(captureTarget));
            }
            hud.OnPlay = StartGame;
            hud.OnResume = () => TogglePause(false);
            hud.OnMute = ToggleMute;
            hud.OnContinue = KeepSwimming;
            hud.SetMuted(sfx.Muted);
            GameSettings.Changed += ApplySettings;
            ApplySettings();

            best = PlayerPrefs.GetInt("deepfeast.best", 0);
            Fishdex.Load(Persist);
            hud.ShowMenu(best);

            Resize();
            menuCamX = world.FindStart().x;
            cam = new Vector2(menuCamX, world.FloorY(menuCamX) - 480);
            zoom = Data.ZoomFor(22);
            for (int i = 0; i < 60; i++) SpawnOne(true);
            lastMouse = Input.mousePosition;
            if (gallery) SetupGallery();
            if (scenery != null) SetupScenery();
            if (interfaceReview != null) SetupInterface();
        }

        void BuildPlayerView()
        {
            player.sp = Data.Player;
            playerView = new FishView(fishRoot, haloRoot);
            playerView.SetSpecies(Data.Player);
            playerView.SetActive(false);
            playerGlow = Gfx.SpriteObject("PlayerGlow", fxRoot, Gfx.Glow, Layer.Glow, true);
            shieldFill = Gfx.SpriteObject("ShieldFill", fxRoot, Gfx.Disc, Layer.Shield);
            shieldRing = Gfx.SpriteObject("ShieldRing", fxRoot, Gfx.Ring, Layer.Shield + 1);
            shieldShine = Gfx.SpriteObject("ShieldShine", fxRoot, Gfx.Disc, Layer.Shield + 2);
            for (int i = 0; i < 3; i++) stunStars[i] = Gfx.SpriteObject("Stun", fxRoot, Gfx.Disc, Layer.Shield + 3);
        }

        void BuildPearlArt()
        {
            // iridescent pearl: warm core, lavender rim, cyan and pink sheen, bright catch-light
            static float Blob(float dx, float dy, float rad) => Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy) / rad);
            var r = new Raster(-1.05f, -1.05f, 1.05f, 1.05f, 64);
            Color core = U.Hex("#fff4ea"), mid = U.Hex("#eedcff"), rim = U.Hex("#a993ea"), cyan = U.Hex("#9fe8ff"), pink = U.Hex("#ffc4e6");
            var disc = r.Circle(0, 0, 1);
            r.Paint(disc, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(-0.12f, -0.1f));
                var c = d < 0.55f ? Color.Lerp(core, mid, d / 0.55f) : Color.Lerp(mid, rim, (d - 0.55f) / 0.55f);
                c = Color.Lerp(c, cyan, Blob(x - 0.42f, y - 0.45f, 0.45f) * 0.6f);
                return Color.Lerp(c, pink, Blob(x - 0.5f, y + 0.25f, 0.38f) * 0.45f);
            });
            r.Paint(r.InnerRing(disc, 2), new Color(1, 1, 1, 0.5f));
            r.Paint(r.Ellipse(-0.38f, -0.42f, 0.27f, 0.19f, -0.6f), new Color(1, 1, 1, 0.92f));
            pearlSprite = r.ToSprite(Vector2.zero, 64);

            // thin halo ring with a soft glow, white so each kind of pearl can tint it
            var h = new Raster(-1.75f, -1.75f, 1.75f, 1.75f, 48);
            var ring = h.Circle(0, 0, 1.5f);
            var inner = h.Circle(0, 0, 1.42f);
            for (int i = 0; i < ring.Length; i++) ring[i] = Mathf.Clamp01(ring[i] - inner[i]);
            h.Paint(h.Blur(ring, 4), new Color(0.7f, 0.95f, 1, 0.9f));
            h.Paint(ring, new Color(0.9f, 0.99f, 1, 1));
            pearlRing = h.ToSprite(Vector2.zero, 48);

            // long, thin four-point sparkle
            var s = new Raster(-3.2f, -3.2f, 3.2f, 3.2f, 32);
            var m = s.Mask();
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI / 2, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                Vector2 R(float x, float y) => new Vector2(x * cs - y * sn, x * sn + y * cs);
                Vector2 p0 = R(0, -0.09f), p1 = R(i % 2 == 0 ? 2.6f : 3.1f, 0), p2 = R(0, 0.09f);
                s.Fill(new Path().Move(p0.x, p0.y).Line(p1.x, p1.y).Line(p2.x, p2.y), m);
            }
            s.Circle(0, 0, 0.22f, m);
            s.Paint(m, new Color(1, 1, 1, 0.95f));
            pearlStar = s.ToSprite(Vector2.zero, 32);
        }

        Sprite BellSprite(float hue)
        {
            if (bellSprites.TryGetValue(hue, out var spr)) return spr;
            spr = ConceptArt.JellyBell(hue) ?? ProceduralBell(hue);
            bellSprites[hue] = spr;
            return spr;
        }

        static Sprite ProceduralBell(float hue)
        {
            float bw = 1, bh = 0.75f;
            var r = new Raster(-1.08f, -0.83f, 1.08f, 0.25f, 64);
            var p = new Path().Move(-bw, 0);
            for (int i = 1; i <= 24; i++) { float a = Mathf.PI + Mathf.PI * i / 24f; p.Line(Mathf.Cos(a) * bw, Mathf.Sin(a) * bh); }
            const int sc = 6;
            for (int i = 0; i < sc; i++)
            {
                float xa = bw - (i + 0.5f) * (2 * bw / sc), xb = bw - (i + 1) * (2 * bw / sc);
                p.Quad(xa, bh * 0.22f, xb, 0, 6);
            }
            var m = r.Fill(p);
            Color c0 = U.Hsl(hue, 1, 0.93f, 0.92f), c1 = U.Hsl(hue, 0.9f, 0.7f, 0.6f), c2 = U.Hsl(hue, 0.85f, 0.55f, 0.3f);
            r.Paint(m, (x, y) =>
            {
                float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(0, -bh * 0.4f)) / bw);
                return t < 0.55f ? Color.Lerp(c0, c1, t / 0.55f) : Color.Lerp(c1, c2, (t - 0.55f) / 0.45f);
            });
            r.Paint(r.InnerRing(m, 2), U.Hsl(hue, 1, 0.92f, 0.6f));
            return r.ToSprite(Vector2.zero, 64);
        }

        // ================================================================== args / harness
        static string[] args;
        static bool Has(string k) => Array.IndexOf(args, k) >= 0;
        static string Arg(string k) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static float ArgF(string k, float d) => float.TryParse(Arg(k), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : d;

        void ParseArgs()
        {
            args = Environment.GetCommandLineArgs();
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
            float ts = ArgF("-timescale", 1);
            if (ts != 1) Time.timeScale = ts;
            if (shotDir != null) System.IO.Directory.CreateDirectory(shotDir);
            Raster.DumpDir = Arg("-dumpart");
            if (Raster.DumpDir != null) System.IO.Directory.CreateDirectory(Raster.DumpDir);
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
            finally { RenderTexture.active = old; Destroy(image); captureName = null; }
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
            if (captureTarget == null) return;
            captureTarget.Release();
            Destroy(captureTarget);
        }

        void Harness(float rdt)
        {
            realTime += rdt;
            if (quitAfter > 0 && realTime >= quitAfter)
            {
                Debug.Log("[DeepFeast] quit after " + quitAfter);
                Application.Quit();
            }
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
                worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                if (statT <= 0)
                {
                    statT = 10;
                    Debug.Log($"[DeepFeast] stat t={(int)playTime} r={player.r:0.0} tier={Data.Tiers[tier].name} lives={lives} score={score} eaten={eaten} fish={fish.Count} fps={1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):0} worst={worstFrame * 1000:0}ms");
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

        // -gallery: every species side by side at one size, plus jellies and a pearl, for art review
        void SetupGallery()
        {
            state = GState.Play;
            hud.StartPlay();
            foreach (var f in fish) ReleaseView(f);
            fish.Clear(); schools.Clear();
            var all = Data.AllSpecies;
            zoom = 1.5f;
            cam = new Vector2(10200, 1300);
            int columns = all.Count > 30 ? 8 : 6;
            int slots = all.Count + 3, rows = Mathf.CeilToInt(slots / (float)columns);
            float cw = refW / columns / zoom, top = cam.y - refH / 2 / zoom;
            for (int i = 0; i < slots; i++)
            {
                float x = cam.x + (i % columns - (columns - 1) * 0.5f) * cw;
                float y = top + (105 + (i / columns + 0.5f) * (refH - 125) / rows) / zoom;
                if (i < all.Count)
                {
                    var f = MakeFish(all[i], 26, x, y, 1);
                    fish.Add(f);
                }
                else if (i < all.Count + 2) AddJelly(x, y - 20, 20, i == all.Count ? 270 : 190);
                else
                    for (int k = 0; k < Powers.Count; k++) AddPearl(x + (k - 1.5f) * cw * 0.22f, y, 9, (PearlKind)k);
            }
            Debug.Log($"[DeepFeast] native gallery: {all.Count} catalog identities, {columns} columns, {rows} rows; " +
                string.Join(", ", System.Linq.Enumerable.Select(all, sp => sp.key + "=" + sp.displayName)));
        }

        void Gallery(float rdt)
        {
            realTime += rdt; time += rdt;
            foreach (var f in fish) f.wag += rdt * 5;
            foreach (var j in jellies) j.phase += rdt * j.pf;
            if (animateGallery)
            {
                float poseTime = time % 2.1f;
                float mouth = poseTime < 0.55f ? 0 : poseTime < 0.75f ? Mathf.InverseLerp(0.55f, 0.75f, poseTime) :
                    poseTime < 0.98f ? 1 : poseTime < 1.17f ? 1 - Mathf.InverseLerp(0.98f, 1.17f, poseTime) : 0;
                foreach (var f in fish)
                {
                    if (Has("-animate-turns")) { f.faceS = Mathf.Cos(time * Mathf.PI / 1.6f); f.face = f.faceS < 0 ? -1 : 1; }
                    f.mouth = mouth;
                    f.chomp = poseTime >= 0.98f && poseTime < 1.2f ? 1.2f - poseTime : 0;
                    f.state = mouth > 0.4f && f.sp != Data.Player ? FState.Chase : FState.Wander;
                }
                if (realTime >= nextShot) { Shot($"pose_{shotN++:000}"); nextShot += Mathf.Max(1 / 30f, shotEvery); }
                if (quitAfter > 0 && realTime >= quitAfter) Application.Quit();
                return;
            }
            if (shotN == 0 && realTime > 2.5f) { Shot("gallery"); shotN = 1; }
            if (shotN == 1 && realTime > 3.5f) { foreach (var f in fish) { f.state = f.sp == Data.Player ? FState.Wander : FState.Chase; f.mouth = 1; } shotN = 2; }
            if (shotN == 2 && realTime > 4.5f) { Shot("gallery_bite"); shotN = 3; }
            if (quitAfter > 0 && realTime >= quitAfter) Application.Quit();
        }

        // ================================================================== flow
        // Fixed native camera/creatures/time for comparable environment reviews; normal gameplay never enters this path.
        void SetupScenery()
        {
            StartGame();
            foreach (var f in fish) ReleaseView(f);
            fish.Clear(); schools.Clear();
            foreach (var j in jellies) DestroyJelly(j);
            jellies.Clear();
            hud.ClearTexts();
            float x = scenery == "abyss" ? 11000 : scenery == "kelp" ? 7200 : scenery == "surface" ? 8600 : 5500;
            zoom = 1.05f;
            cam = new Vector2(x, scenery == "surface" ? 340 : world.FloorY(x) - 260);
            player.r = 22; tier = 1; pInvuln = 0;
            player.x = x; player.y = cam.y; player.wag = 2;
            var species = new[] { Data.SpeciesMap["clown"], Data.SpeciesMap["tang"], Data.SpeciesMap["snapper"], Data.SpeciesMap["angel"] };
            for (int i = 0; i < species.Length; i++)
                fish.Add(MakeFish(species[i], 17 + i * 2, x + (i - 1.5f) * 250, cam.y - 100 + (i % 2) * 90, i % 2 == 0 ? 1 : -1));
            Debug.Log($"[DeepFeast] scenery {scenery}: camera={cam}, zoom={zoom}, fixed seed=20261003.");
        }

        void Scenery(float rdt)
        {
            realTime += rdt;
            if (interfaceReview == "flow")
            {
                time = 2;
                InterfaceFlow();
                if (quitAfter > 0 && realTime >= quitAfter) Application.Quit();
                return;
            }
            bool animate = Has("-animate-scenery");
            time = 2 + (animate ? shotN * shotEvery : 0);
            // -ripple: a ring from the hero just before the shot, as a hit would send.
            if (Has("-ripple") && !rippleShown && realTime > 2.8f) { rippleShown = true; Ripple(player.x, player.y, 1.6f); }
            foreach (var f in fish) f.wag = time * 5;
            player.wag = time * 5;
            if (Has("-turn-review"))
            {
                player.face = -1; player.faceS = ArgF("-turn", 0.01f);
                foreach (var f in fish) { f.face = -1; f.faceS = player.faceS; }
            }
            if (realTime > 3.1f && (shotN == 0 || animate && realTime >= nextShot))
            {
                Shot(animate ? $"scenery_{shotN:000}" : interfaceReview != null ? "interface" : Has("-turn-review") ? "turn" : "scenery");
                shotN++; nextShot = realTime + shotEvery;
            }
            if (quitAfter > 0 && realTime >= quitAfter) Application.Quit();
        }

        void SetupInterface()
        {
            if (interfaceReview == "menu" || interfaceReview == "flow") { state = GState.Menu; pActive = false; hud.ShowMenu(4240); }
            else if (interfaceReview == "pause") { state = GState.Paused; hud.ShowPause(true); }
            else if (interfaceReview == "victory") { state = GState.Victory; hud.ShowVictory(48210, 512, 1312, 48210); }
            else if (interfaceReview == "settings")
            {
                state = GState.Menu; pActive = false; hud.ShowMenu(4240); hud.OpenSettings();
                // -settingspage N shows another page; its first row takes focus.
                for (int i = 0; i < (int)ArgF("-settingspage", 0); i++) hud.SettingsTab(1);
            }
            else if (interfaceReview == "dex")
            {
                // A sample collection: most of the reef and kelp, a little of the deep, no sharks yet.
                for (int i = 0; i < Fishdex.Entries.Count; i++) if (i % 5 < 3 && !Fishdex.Entries[i].IsShark) Fishdex.Record(Fishdex.Entries[i]);
                state = GState.Menu; pActive = false; hud.ShowMenu(4240); hud.OpenDex();
            }
            else { state = GState.Over; pActive = false; hud.ShowOver(2340, "Predator", 42, 164, false, 4240, 3); }
        }

        void InterfaceFlow()
        {
            void Check(GState expected, string overlay, string action)
            {
                if (state != expected || hud.ActiveOverlay != overlay)
                    throw new InvalidOperationException($"UI flow {action} failed: state={state}, overlay={hud.ActiveOverlay}.");
                Debug.Log($"[DeepFeast] UI flow {action} passed: state={state}, overlay={overlay}.");
            }
            void CheckSetting(bool ok, string action)
            {
                if (!ok) throw new InvalidOperationException($"UI flow {action} failed: selected={hud.Selected}, settings={JsonUtility.ToJson(GameSettings.Data)}.");
                Debug.Log($"[DeepFeast] UI flow {action} passed: selected={hud.Selected}.");
            }
            switch (uiFlowStage)
            {
                case 0 when realTime > 3.2f: Shot("menu"); uiFlowStage++; break;
                case 1 when realTime > 3.8f: hud.SubmitPrimary(); pInvuln = 0; Check(GState.Play, "none", "play submit"); uiFlowStage++; break;
                case 2 when realTime > 4.4f: TogglePause(true); Check(GState.Paused, "pause", "pause"); uiFlowStage++; break;
                case 3 when realTime > 4.8f: Shot("pause"); uiFlowStage++; break;
                case 4 when realTime > 5.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "resume submit"); uiFlowStage++; break;
                case 5 when realTime > 5.8f: Shot("resumed"); uiFlowStage++; break;
                case 6 when realTime > 6.4f: GameOver(); Check(GState.Over, "over", "game over"); uiFlowStage++; break;
                case 7 when realTime > 7: Shot("over"); uiFlowStage++; break;
                case 8 when realTime > 7.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "retry submit"); uiFlowStage++; break;
                case 9 when realTime > 8: Victory(); Check(GState.Victory, "victory", "victory"); uiFlowStage++; break;
                case 10 when realTime > 8.6f: Shot("victory"); uiFlowStage++; break;
                case 11 when realTime > 9: hud.SubmitPrimary(); Check(GState.Play, "none", "keep swimming submit"); uiFlowStage++; break;
                case 12 when realTime > 9.6f: GameOver(); Check(GState.Over, "over", "second game over"); uiFlowStage++; break;
                case 13 when realTime > 10: hud.SubmitButton("FISHDEX"); Check(GState.Over, "fishdex", "fishdex submit"); uiFlowStage++; break;
                case 14 when realTime > 10.6f: Shot("fishdex"); uiFlowStage++; break;
                case 15 when realTime > 11: hud.SubmitButton("BACK"); Check(GState.Over, "over", "fishdex back"); uiFlowStage++; break;
                case 16 when realTime > 11.4f: hud.SubmitPrimary(); Check(GState.Play, "none", "retry after fishdex"); uiFlowStage++; break;
                case 17 when realTime > 11.8f: TogglePause(true); Check(GState.Paused, "pause", "pause again"); uiFlowStage++; break;
                case 18 when realTime > 12.2f: hud.SubmitButton("SETTINGS"); Check(GState.Paused, "settings", "settings submit"); uiFlowStage++; break;
                case 19 when realTime > 12.8f: Shot("settings"); uiFlowStage++; break;
                case 20 when realTime > 13.2f:
                    hud.FocusSetting("master"); hud.MoveFocused(UnityEngine.EventSystems.MoveDirection.Left);
                    CheckSetting(GameSettings.Data.master == 90, "left lowers the master volume");
                    uiFlowStage++; break;
                case 21 when realTime > 13.8f:
                    Shot("settings_sound"); hud.MoveFocused(UnityEngine.EventSystems.MoveDirection.Right);
                    CheckSetting(GameSettings.Data.master == 100, "right raises it again");
                    uiFlowStage++; break;
                case 22 when realTime > 14.2f:
                    hud.SubmitButton("BACK"); Check(GState.Paused, "pause", "settings back");
                    CheckSetting(hud.Selected == "Button_SETTINGS", "focus returns to the settings button");
                    uiFlowStage++; break;
                case 23 when realTime > 14.6f: hud.SubmitPrimary(); Check(GState.Play, "none", "resume after settings"); uiFlowStage++; break;
                case 24 when realTime > 15: Debug.Log("[DeepFeast] complete native UI flow passed."); uiFlowStage++; break;
            }
        }

        void StartGame()
        {
            foreach (var f in fish) ReleaseView(f);
            fish.Clear(); schools.Clear();
            foreach (var j in jellies) DestroyJelly(j);
            jellies.Clear();
            foreach (var p in pearls) DestroyPearl(p);
            pearls.Clear();
            parts.Clear(); hud.ClearTexts();
            state = GState.Play; score = 0; lives = 3; combo = 0; comboT = 0; eaten = 0; newSpecies = 0; playTime = 0; tier = 0;
            sharkT = firstShark > 0 ? firstShark : U.Rand(32, 40); pearlT = pearlEvery > 0 ? 2 : U.Rand(14, 20); pearlSeq = 0; slowT = 0; shake = 0; dieT = 0;
            finaleT = -1; victoryT = -1; biteCool = 0; finaleBites = 0; endless = false; finale = null;
            hints.Clear();
            var s = world.FindStart();
            if (startX >= 0) s = new Vector2(startX, world.FloorY(startX) - 260);
            player.x = s.x; player.y = s.y; player.vx = player.vy = 0; player.r = 12; player.face = player.faceS = 1;
            player.tilt = 0; player.mouth = 0; player.chomp = 0; player.state = FState.Wander;
            if (startSize > 0)
            {
                player.r = startSize;
                for (int i = 0; i < Data.Tiers.Length; i++) if (player.r >= Data.Tiers[i].r) tier = i;
                if (Legend) finaleT = finaleDelay;
            }
            pInvuln = 2; pStamina = 1; pTired = false; pDashing = false; pActive = true; pAlive = true; System.Array.Clear(power, 0, power.Length); pStun = 0; pStungCool = 0;
            zoom = Data.ZoomFor(player.r);
            cam = new Vector2(player.x, player.y);
            for (int i = 0; i < 70; i++) SpawnOne(true);
            hud.StartPlay();
            Banner("EAT OR BE EATEN", "Smaller fish are food · bigger fish are danger", new Color(80 / 255f, 240 / 255f, 220 / 255f));
        }

        void TogglePause(bool? force = null)
        {
            if (state == GState.Play && force != false) { state = GState.Paused; hud.ShowPause(true); }
            else if (state == GState.Paused && force != true) { state = GState.Play; hud.ShowPause(false); }
        }

        void ApplySettings()
        {
            var d = GameSettings.Data;
            sfx.SetLevels(d.master / 100f, d.effects / 100f, d.ambience / 100f);
        }

        void ToggleMute()
        {
            sfx.SetMuted(!sfx.Muted);
            hud.SetMuted(sfx.Muted);
        }

        void GameOver()
        {
            state = GState.Over;
            pActive = false;
            bool isBest = score > best;
            if (isBest) { best = score; SaveBest(); }
            hud.ShowOver(score, Data.Tiers[tier].name, eaten, playTime, isBest, best, newSpecies);
            menuCamX = cam.x;
            Debug.Log($"[DeepFeast] game over t={(int)playTime} score={score} tier={Data.Tiers[tier].name} eaten={eaten}");
        }

        void Victory()
        {
            state = GState.Victory;
            pDashing = false;
            if (score > best) { best = score; SaveBest(); }
            hud.ShowVictory(score, eaten, playTime, best);
            Debug.Log($"[DeepFeast] victory t={(int)playTime} score={score} r={player.r:0.0} eaten={eaten}");
        }

        // After the finale the swim goes on: same ocean, no more whale shark.
        void KeepSwimming()
        {
            if (state != GState.Victory) return;
            state = GState.Play;
            endless = true;
            pInvuln = 2;
            hud.HideVictory();
            Banner("ENDLESS DEEP", "The ocean is yours. Feast for as long as you last", new Color(80 / 255f, 240 / 255f, 220 / 255f));
            Debug.Log("[DeepFeast] endless swim continues");
        }

        void SaveBest()
        {
            if (!Persist) return;
            PlayerPrefs.SetInt("deepfeast.best", best);
            PlayerPrefs.Save();
        }

        void Banner(string title, string sub, Color glow) => hud.Banner(title, sub, glow);

        void Hint(string key, string title, string sub, Color glow)
        {
            if (hints.Contains(key)) return;
            hints.Add(key);
            Banner(title, sub, glow);
        }

        void CheckTier()
        {
            int t = 0;
            for (int i = 0; i < Data.Tiers.Length; i++) if (player.r >= Data.Tiers[i].r) t = i;
            if (t <= tier) return;
            tier = t;
            if (Legend && !endless) finaleT = finaleDelay;
            bool extra = lives < 5;
            if (extra) lives++;
            Banner(Data.Tiers[t].name.ToUpperInvariant() + "!", Data.Tiers[t].blurb + (extra ? "  +1 life" : ""), new Color(1, 212 / 255f, 71 / 255f));
            sfx.TierUp();
            hud.PulseGrowth(true);
            parts.Add(new Particle { type = PType.Ring, x = player.x, y = player.y, life = 0.75f, max = 0.75f, size = player.r * 3.3f, col = new Color(1, 220 / 255f, 120 / 255f) });
            Sparkle(player.x, player.y, player.r, 16, U.Hex("#ffe38a"));
        }

        void EatFish(int i)
        {
            var f = fish[i];
            fish.RemoveAt(i);
            ReleaseView(f);
            float pr = player.r;
            player.chomp = 0.22f;
            // Young fish grow fast so the first tiers come quickly; the boost fades out by Hunter.
            float growth = Data.GROW * Mathf.Lerp(1.8f, 1, Mathf.InverseLerp(Data.Tiers[0].r, Data.Tiers[2].r, pr));
            // Past Legend growth tapers off, so the hero never outgrows the screen.
            float legendR = Data.Tiers[Data.Tiers.Length - 1].r;
            growth *= Mathf.Lerp(1, 0.15f, Mathf.InverseLerp(legendR, legendR * 1.5f, pr));
            player.r = Mathf.Sqrt(pr * pr + f.r * f.r * growth);
            combo = comboT > 0 ? Mathf.Min(combo + 1, 12) : 1;
            comboT = 1.8f;
            int pts = Mathf.RoundToInt((f.r * 1.5f + 5) * (f.shark ? 5 : 1)) * combo;
            score += pts;
            eaten++;
            hud.PulseGrowth(false);
            Burst(f.x, f.y, f.r, f.sp.c0);
            // float the score above the player's head rather than over its mouth
            hud.AddText(f.x, Mathf.Min(f.y, player.y) - player.r * 1.5f - 10 / zoom, "+" + pts, combo > 1 ? U.Hex("#ffd447") : Color.white, combo > 2 ? 26 : 22);
            if (combo == 5 || combo == 10)
                Banner(combo == 10 ? "MEGA FRENZY!" : "FEEDING FRENZY!", $"×{combo} combo", new Color(1, 170 / 255f, 40 / 255f));
            if (Fishdex.Record(f.sp))
            {
                newSpecies++;
                hud.AddText(f.x, Mathf.Min(f.y, player.y) - player.r * 1.5f - 38 / zoom, "NEW: " + f.sp.displayName, U.Hex("#7ff5dc"), 18);
                Hint("dex", "NEW SPECIES!", "Every fish you eat joins your Fishdex", new Color(80 / 255f, 240 / 255f, 220 / 255f));
            }
            sfx.Chomp(f.r / pr, combo);
            shake = Mathf.Max(shake, 2 + (f.r / pr) * 3);
            CheckTier();
            if (f == finale) WinFinale(f);
        }

        void WinFinale(Fish f)
        {
            finale = null;
            const int bonus = 10000;
            score += bonus;
            hud.AddText(player.x, player.y - player.r * 2.2f - 40 / zoom, "LEGEND BONUS +" + bonus.ToString("N0"), U.Hex("#ffd447"), 30);
            Banner("LEGEND OF THE DEEP!", "You caught the whale shark", new Color(1, 212 / 255f, 71 / 255f));
            for (int k = 0; k < 3; k++)
                parts.Add(new Particle { type = PType.Ring, x = player.x, y = player.y, life = 0.7f + k * 0.25f, max = 0.7f + k * 0.25f, size = player.r * (3 + k * 1.4f), col = new Color(1, 220 / 255f, 120 / 255f) });
            Sparkle(player.x, player.y, player.r * 1.6f, 40, U.Hex("#ffe38a"));
            sfx.TierUp();
            slowT = 0.9f; shake = Mathf.Max(shake, 10);
            victoryT = 1.6f;
        }

        // A bite out of the whale shark: it shrinks, bolts, and after a few bites fits in your mouth.
        void BiteFinale(Fish f)
        {
            biteCool = 0.5f;
            finaleBites++;
            f.r *= 0.8f; f.cool = 1.4f;
            player.chomp = 0.22f;
            int pts = 1500 * finaleBites;
            score += pts;
            float mx = player.x + player.face * player.r * 0.9f;
            Burst(mx, player.y, f.r * 0.45f, f.sp.c0);
            hud.AddText(mx, player.y - player.r * 1.5f - 10 / zoom, "BITE! +" + pts.ToString("N0"), U.Hex("#ffd447"), 26);
            sfx.Chomp(0.9f, finaleBites);
            shake = Mathf.Max(shake, 6);
            hud.PulseGrowth(false);
            Debug.Log($"[DeepFeast] finale: bite {finaleBites}, whale shark radius={f.r:0.0} player={player.r:0.0}");
            if (f.r <= player.r * Data.EAT) Banner("SWALLOW IT!", "The whale shark is small enough now", new Color(1, 212 / 255f, 71 / 255f));
        }

        void KillPlayer(Fish killer)
        {
            pAlive = false;
            dieT = 1.5f;
            lives--;
            if (killer != null) { killer.chomp = 0.5f; killer.state = FState.Wander; killer.cool = 6; }
            Burst(player.x, player.y, player.r * 1.4f, Data.Player.c0);
            Sparkle(player.x, player.y, player.r, 16, U.Hex("#ffd447"));
            shake = 16; slowT = 0.7f; combo = 0; comboT = 0;
            Ripple(player.x, player.y, 1.6f);
            sfx.Hurt();
        }

        void Respawn()
        {
            pAlive = true; pInvuln = 3; pStamina = 1; pTired = false; player.vx = player.vy = 0; pStun = 0;
            float sight = ViewW;
            foreach (var f in fish)
            {
                if (f != finale && f.r >= player.r * Data.DANGER && Dist(f.x - player.x, f.y - player.y) < sight)
                {
                    f.state = f.shark ? FState.Leave : FState.Wander;
                    f.cool = 6;
                    f.dir = f.x < player.x ? -1 : 1;
                    if (f.shark) { f.life = 0; f.leaveDir = f.dir; }
                }
            }
            Banner($"{lives} {(lives == 1 ? "LIFE" : "LIVES")} LEFT", "Shake it off and keep feeding", new Color(80 / 255f, 240 / 255f, 220 / 255f));
        }

        static float Dist(float x, float y) => Mathf.Sqrt(x * x + y * y);

        // ================================================================== spawning
        Fish MakeFish(Species sp, float r, float x, float y, int dir)
        {
            var f = new Fish
            {
                id = nextId++, sp = sp, shark = sp.IsShark, r = r, x = x, y = y, vx = dir * 20, face = dir, faceS = dir, wag = U.Rand(0, U.TAU),
                dir = dir, phase = U.Rand(0, U.TAU), cruise = U.Rand(0.3f, 0.5f), homeY = y, homeT = U.Rand(2, 7),
                state = FState.Wander, cool = U.Rand(1.5f, 4), eatCool = 2, aggro = U.Rand(0.5f, 1.2f),
            };
            return f;
        }

        // The view spans a wide band of depth; halfway to the camera keeps the cast true to the
        // zone the player is in while still shading toward the depth where each fish appears.
        Habitat.Look SpawnZone(float y) => Habitat.At((y + cam.y) * 0.5f);
        // A fish's light in world coordinates: its body, or the anglerfish's lure ahead of the snout.
        static Vector2 GlowPoint(Fish f) => new Vector2(f.x + f.sp.glowOffset.x * f.r * f.faceS, f.y - f.sp.glowOffset.y * f.r);

        void SpawnSchool(float x, float y, int dir, float r, int n)
        {
            var key = Data.SpeciesFor(r, true, SpawnZone(y));
            var s = new School { dir = dir, turnT = U.Rand(6, 14), cx = x, cy = y };
            schools.Add(s);
            for (int i = 0; i < n; i++)
            {
                float fr = r * U.Rand(0.85f, 1.12f);
                float fx = x + U.Rand(-1, 1) * r * 5, fy = Mathf.Clamp(y + U.Rand(-1, 1) * r * 3, 80, world.FloorY(fx) - fr - 30);
                var f = MakeFish(Data.SpeciesMap[key], fr, fx, fy, dir);
                f.school = s; f.slotX = U.Rand(-1, 1) * r * 5; f.slotY = U.Rand(-1, 1) * r * 3; f.cruise = 0.42f;
                fish.Add(f);
            }
        }

        void SpawnOne(bool initial)
        {
            float pr = FocusR, vw = ViewW, vh = ViewH;
            // Legends see fewer giants, so the last tier is a sea of prey rather than a wall of titans.
            float threatP = Legend ? 0.04f : tier >= 5 ? 0.08f : 0.2f + Mathf.Min(tier, 3) * 0.03f;
            float r; bool threat = false, school = false;
            if (U.Rand() < threatP) { r = pr * U.Rand(1.25f, 2.3f); threat = true; }
            else
            {
                float v = U.Rand();
                if (v < 0.22f) { r = pr * U.Rand(0.28f, 0.5f); school = true; }
                else if (v < 0.36f) r = pr * U.Rand(0.18f, 0.34f);
                else r = pr * U.Rand(0.4f, 0.86f);
            }
            if (threat)
            {
                int threats = 0;
                foreach (var f in fish) if (f.r >= pr * Data.DANGER) threats++;
                if (threats >= (Legend ? 3 : 7)) { r = pr * U.Rand(0.4f, 0.86f); threat = false; }
            }
            r = Mathf.Max(3, r);
            float x, y; int dir;
            if (initial)
            {
                x = cam.x + U.Rand(-0.95f, 0.95f) * vw;
                y = cam.y + U.Rand(-0.85f, 0.85f) * vh;
                dir = U.Chance(0.5f) ? 1 : -1;
                if (pActive && Dist(x - player.x, y - player.y) < vw * (threat ? 0.55f : 0.12f)) return;
            }
            else
            {
                int side = U.Chance(0.5f) ? -1 : 1;
                x = cam.x + side * (vw * 0.5f + U.Rand(r * 2.5f + 30, vw * 0.32f));
                y = cam.y + U.Rand(-0.6f, 0.6f) * vh;
                dir = U.Chance(0.78f) ? -side : side;
            }
            y = Mathf.Min(y, world.FloorY(x) - r * 2 - 40);
            if (!world.InWater(x, y, r)) return;
            if (school) SpawnSchool(x, y, dir, r, 5 + U.RandInt(0, 5));
            else fish.Add(MakeFish(Data.SpeciesMap[Data.SpeciesFor(r, false, SpawnZone(y))], r, x, y, dir));
        }

        void SpawnJelly()
        {
            float pr = FocusR, vw = ViewW, vh = ViewH;
            int side = U.Chance(0.5f) ? -1 : 1;
            float x = cam.x + side * (vw * 0.5f + U.Rand(60, vw * 0.4f));
            float r = Mathf.Max(9, pr * U.Rand(0.75f, 1.35f));
            float y = Mathf.Clamp(cam.y + U.Rand(-0.5f, 0.5f) * vh, 650, world.FloorY(x) - r * 4 - 60);
            if (!world.InWater(x, y, r * 2)) return;
            AddJelly(x, y, r, U.Pick(new float[] { 300, 320, 190, 270, 200 })).vx = U.Rand(-0.04f, 0.04f) * Data.SpeedFor(pr);
        }

        Jelly AddJelly(float x, float y, float r, float hue)
        {
            var j = new Jelly { x = x, y = y, r = r, phase = U.Rand(0, U.TAU), pf = U.Rand(1.6f, 2.4f), hue = hue };
            j.bell = Gfx.SpriteObject("Jelly", fxRoot, BellSprite(j.hue), Layer.Jelly + jellies.Count);
            j.glow = Gfx.SpriteObject("JellyGlow", fxRoot, Gfx.Glow, Layer.Glow, true);
            j.glow.color = U.Hsl(j.hue, 1, 0.7f, 0.24f);
            jellies.Add(j);
            return j;
        }

        void DestroyJelly(Jelly j) { Destroy(j.bell.gameObject); Destroy(j.glow.gameObject); }
        void DestroyPearl(Pearl p) { Destroy(p.body.gameObject); Destroy(p.star.gameObject); Destroy(p.glow.gameObject); Destroy(p.ring.gameObject); }

        void SpawnShark()
        {
            float pr = player.r, vw = ViewW;
            bool edible = tier >= 5;
            int side = -(int)Mathf.Sign(player.face);
            if (side == 0) side = 1;
            float r = edible ? pr * 0.75f : pr * 3.2f;
            float x = Mathf.Clamp(player.x + side * (vw * 0.62f + r * 2), 200, World.W - 200);
            float y = Mathf.Clamp(player.y + U.Rand(-150, 150), 120 + r, world.FloorY(x) - r - 60);
            var identity = Data.SharkForEncounter(nextSharkVariant);
            nextSharkVariant = (nextSharkVariant + 1) % Data.SharkVariants.Length;
            var s = MakeFish(identity, r, x, y, -side);
            s.shark = true; s.life = 14; s.state = FState.Chase; s.leaveDir = side; s.alertT = 1.4f;
            fish.Add(s);
            Debug.Log($"[DeepFeast] shark encounter: key={identity.key}, name={identity.displayName}, aggressive={identity.aggressive}, chaseMultiplier={identity.chaseSpeedMultiplier:0.00}, edible={edible}, radius={r:0.0}, life={s.life:0.0}.");
            if (edible) Banner("SHARK!", "You are the Leviathan now — hunt it down!", new Color(1, 200 / 255f, 60 / 255f));
            else Banner("SHARK!", "Hold click / Space to dash away", new Color(1, 60 / 255f, 80 / 255f));
            sfx.Shark();
        }

        // The ocean's largest fish cruises in at twice the Legend's size. It never hunts.
        void SpawnWhaleShark()
        {
            float pr = player.r, vw = ViewW;
            int side = -(int)Mathf.Sign(player.face);
            if (side == 0) side = 1;
            float r = pr * 2;
            float x = Mathf.Clamp(player.x + side * (vw * 0.62f + r * 2), 200, World.W - 200);
            float y = Mathf.Clamp(player.y + U.Rand(-120, 120), 120 + r, world.FloorY(x) - r - 60);
            finale = MakeFish(Data.WhaleShark, r, x, y, -side);
            finale.life = 50; finale.leaveDir = side;
            finaleBites = 0;
            fish.Add(finale);
            Debug.Log($"[DeepFeast] finale: whale shark radius={r:0.0} player={pr:0.0}");
            Banner("THE WHALE SHARK", "Too big to swallow. Bite it down to size", new Color(1, 212 / 255f, 71 / 255f));
            sfx.Shark();
        }

        void SpawnPearl()
        {
            float pr = player.r, vw = ViewW, vh = ViewH;
            for (int t = 0; t < 12; t++)
            {
                float x = cam.x + U.Rand(-0.38f, 0.38f) * vw, y = cam.y + U.Rand(-0.36f, 0.36f) * vh;
                if (!world.InWater(x, y, pr) || Dist(x - player.x, y - player.y) < pr * 5) continue;
                AddPearl(x, y, Mathf.Max(6, pr * 0.32f), NextPearlKind());
                return;
            }
        }

        bool Reeling(Fish f)
        {
            foreach (var m in magnetPull) if (m.f == f) return true;
            return false;
        }

        // Shields, magnets and bursts anywhere; the lantern turns up only where the water is dark.
        PearlKind NextPearlKind()
        {
            if (pearlEvery > 0) return (PearlKind)(pearlSeq++ % Powers.Count);
            if (World.DarkAt(player.y) > 0.2f && U.Chance(0.4f)) return PearlKind.Lantern;
            float roll = U.Rand();
            return roll < 0.4f ? PearlKind.Shield : roll < 0.7f ? PearlKind.Magnet : PearlKind.Burst;
        }

        Pearl AddPearl(float x, float y, float r, PearlKind kind = PearlKind.Shield)
        {
            var col = Powers.Colors[(int)kind];
            var p = new Pearl { x = x, y = y, r = r, life = 16, ph = U.Rand(0, U.TAU), kind = kind };
            p.glow = Gfx.SpriteObject("PearlGlow", fxRoot, Gfx.Glow, Layer.Glow, true);
            p.glow.color = U.WithA(col, 0.26f);
            p.ring = Gfx.SpriteObject("PearlRing", fxRoot, pearlRing, Layer.Pearl - 1, true);
            p.body = Gfx.SpriteObject("Pearl", fxRoot, pearlSprite, Layer.Pearl);
            p.body.color = Color.Lerp(Color.white, col, 0.4f);
            p.star = Gfx.SpriteObject("PearlStar", fxRoot, pearlStar, Layer.Pearl + 1);
            pearls.Add(p);
            return p;
        }

        // ================================================================== particles
        void Bubble(float x, float y, float s, float? vy = null)
            => parts.Add(new Particle { type = PType.Bubble, x = x, y = y, vx = U.Rand(-0.3f, 0.3f) * s, vy = vy ?? -U.Rand(2.5f, 5) * s, life = U.Rand(1.2f, 2.6f), max = 2.6f, size = s * U.Rand(0.5f, 1.2f), ph = U.Rand(0, U.TAU) });

        void Burst(float x, float y, float r, Color col)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = U.Rand(0, U.TAU), sp = U.Rand(0.6f, 2.4f) * r;
                parts.Add(new Particle { type = PType.Bit, x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp, life = U.Rand(0.5f, 1), max = 1, size = r * U.Rand(0.1f, 0.22f), col = col });
            }
            for (int i = 0; i < 4; i++) Bubble(x + U.Rand(-r, r) * 0.6f, y + U.Rand(-r, r) * 0.6f, r * 0.14f);
            parts.Add(new Particle { type = PType.Ring, x = x, y = y, life = 0.34f, max = 0.34f, size = r * 0.9f, col = Color.Lerp(col, Color.white, 0.55f) });
        }

        void Sparkle(float x, float y, float r, int n, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float a = U.Rand(0, U.TAU), sp = U.Rand(0.5f, 2) * r;
                parts.Add(new Particle { type = PType.Spark, x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp, life = U.Rand(0.5f, 1.1f), max = 1.1f, size = r * U.Rand(0.08f, 0.18f), col = col });
            }
        }

        // ================================================================== main loop
        void Update()
        {
            PaintedArt.PrepareMeshes();
            // The Fishdex portraits are photographed once, on the first frame after loading.
            if (!portraitsTaken) { portraitsTaken = true; Fishdex.EnsurePortraits(); }
            float rdt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            GameSettings.Tick(rdt);
            Resize();
            if (scenery != null)
            {
                Scenery(rdt); Render(0, rdt);
                if (Has("-turn-review") && !turnReviewLogged && realTime > 3.2f)
                {
                    turnReviewLogged = true;
                    Debug.Log($"[DeepFeast] turn review: player width ratio={Mathf.Abs(playerView.root.transform.localScale.x):0.000}.");
                }
                return;
            }
            if (gallery) { Gallery(rdt); Render(rdt, rdt); return; }
            ReadInput();
            Harness(rdt);
            if (state != GState.Paused)
            {
                if (slowT > 0) { slowT -= dt; dt *= 0.35f; }
                Step(dt);
            }
            Render(state == GState.Paused ? 0 : dt, rdt);
        }

        void OnApplicationFocus(bool focus) { if (!focus && !noPause) { TogglePause(true); } }

        void Resize()
        {
            float w = captureTarget != null ? captureTarget.width : Mathf.Max(1, Screen.width);
            float h = captureTarget != null ? captureTarget.height : Mathf.Max(1, Screen.height);
            pxPerRef = Mathf.Min(w, h) / 820f;
            refW = w / pxPerRef; refH = h / pxPerRef;
            hud?.Resize(pxPerRef, refW);
            presenter.Fit((int)w, (int)h);
        }

        void Step(float dt)
        {
            time += dt;
            if (state == GState.Play)
            {
                if (pAlive) UpdatePlayer(dt);
                UpdateEvents(dt);
            }
            else if (state == GState.Victory)
            {
                // The hero coasts to a stop behind the victory card.
                float k = Mathf.Min(1, dt * 2);
                player.vx -= player.vx * k; player.vy -= player.vy * k;
                AnimateFish(player, dt, false);
            }
            UpdateFish(dt);
            UpdateJellies(dt);
            UpdateSpawner(dt);
            UpdateVents(dt);
            parts.Step(dt);
            UpdateCamera(dt);
        }

        /// Seabed vents near the camera trickle columns of bubbles.
        void UpdateVents(float dt)
        {
            float hw = ViewW / 2 + 200;
            foreach (var v in world.vents)
            {
                if (Mathf.Abs(v.x - cam.x) > hw || !U.Chance(dt * 7)) continue;
                float s = U.Rand(2.5f, 6);
                parts.Add(new Particle { type = PType.Bubble, x = v.x + U.Rand(-5, 5), y = v.y - 6, vx = U.Rand(-4, 4), vy = -U.Rand(70, 120), life = U.Rand(3, 5.5f), max = 5.5f, size = s, ph = U.Rand(0, U.TAU) });
            }
        }

        // ------------------------------------------------------------------ input
        bool moveUp, moveDown, moveLeft, moveRight, dashKey, mouseDown;

        void ReadInput()
        {
            moveUp = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            moveDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            dashKey = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                kbMode = true; padMode = false;
            }

            if (Input.touchCount > 0)
            {
                touchMode = true;
                pointerMode = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (hud.PointerOverUI(t.position)) continue;
                    pointer = new Vector2(t.position.x / pxPerRef, (Screen.height - t.position.y) / pxPerRef);
                    pointerMode = true; kbMode = false; padMode = false;
                    break;
                }
                mouseDown = false;
            }
            else
            {
                if (touchMode) pointerMode = false;
                var mp = Input.mousePosition;
                bool moved = (mp - lastMouse).sqrMagnitude > 0.5f;
                lastMouse = mp;
                if (!touchMode)
                {
                    pointer = new Vector2(mp.x / pxPerRef, (Screen.height - mp.y) / pxPerRef);
                    if (moved || Input.GetMouseButtonDown(0)) { pointerMode = true; kbMode = false; padMode = false; }
                }
                mouseDown = !touchMode && Input.GetMouseButton(0) && !hud.PointerOverUI(mp);
            }
            hud.ShowTouch(touchMode && state == GState.Play);

            if (hud.SettingsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) hud.CloseSettings();
                if (Input.GetKeyDown(KeyCode.Tab)) hud.SettingsTab(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            }
            else if (hud.DexOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) hud.CloseDex();
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) TogglePause();
                // With a card button focused, Enter presses that button through the UI instead.
                if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && (state == GState.Menu || state == GState.Over) && !hud.HasFocus) StartGame();
            }
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            ReadPad();
        }

        // ------------------------------------------------------------------ player
        void AnimateFish(Fish f, float dt, bool animateMouth = true)
        {
            float sp = Dist(f.vx, f.vy);
            if (f.vx > 6) f.face = 1; else if (f.vx < -6) f.face = -1;
            // Advance the actual heading at a bounded angular rate; cosine is consumed by the 3D rig.
            float yaw = Mathf.Acos(Mathf.Clamp(f.faceS, -1, 1)) * Mathf.Rad2Deg;
            float rate = f.sp.IsShark ? 390 : f.sp.shape == "round" ? 460 : 540;
            yaw = Mathf.MoveTowards(yaw, f.face > 0 ? 0 : 180, dt * rate);
            f.faceS = Mathf.Cos(yaw * Mathf.Deg2Rad);
            float tilt = Mathf.Clamp(Mathf.Atan2(f.vy, Mathf.Abs(f.vx) + 1e-3f), -1.05f, 1.05f);
            f.tilt += (tilt - f.tilt) * Mathf.Min(1, dt * 6);
            f.wag += dt * Mathf.Clamp(3.5f + (sp / f.r) * 0.55f, 3.5f, 20);
            f.chomp = Mathf.Max(0, f.chomp - dt);
            if (animateMouth) f.mouth += ((f.chomp > 0 ? 1 : 0) - f.mouth) * Mathf.Min(1, dt * 16);
        }

        void UpdatePlayer(float dt)
        {
            float bas = Data.SpeedFor(player.r);
            pInvuln = Mathf.Max(0, pInvuln - dt);
            for (int pk = 0; pk < power.Length; pk++) power[pk] = Mathf.Max(0, power[pk] - dt);
            pStun = Mathf.Max(0, pStun - dt);
            pStungCool = Mathf.Max(0, pStungCool - dt);
            biteCool = Mathf.Max(0, biteCool - dt);

            float dx = 0, dy = 0, mag = 0;
            bool wantDash;
            if (autoplay) { var v = BotSteer(out wantDash); dx = v.x; dy = v.y; mag = v.sqrMagnitude > 0 ? 1 : 0; }
            else
            {
                if (padMode) PadSteer(out dx, out dy, out mag);
                else if (kbMode)
                {
                    dx = (moveRight ? 1 : 0) - (moveLeft ? 1 : 0);
                    dy = (moveDown ? 1 : 0) - (moveUp ? 1 : 0);
                    float n = Dist(dx, dy);
                    if (n > 0) { dx /= n; dy /= n; mag = 1; }
                }
                else if (pointerMode)
                {
                    var s = ToScreen(player.x, player.y);
                    float ddx = pointer.x - s.x, ddy = pointer.y - s.y, d = Dist(ddx, ddy);
                    if (d > 6) { dx = ddx / d; dy = ddy / d; mag = Mathf.Clamp01((d - 6) / 110); }
                }
                wantDash = dashKey || mouseDown || hud.DashHeld || padDash;
            }

            bool want = wantDash && mag > 0.2f;
            if (pTired && pStamina > 0.35f) pTired = false;
            bool was = pDashing;
            pDashing = want && !pTired && pStun <= 0;
            if (pDashing)
            {
                if (!Powered(PearlKind.Burst)) pStamina -= dt * 0.85f;
                if (pStamina <= 0) { pStamina = 0; pTired = true; pDashing = false; }
                if (U.Rand() < dt * 30) Bubble(player.x - player.face * player.r, player.y + U.Rand(-0.4f, 0.4f) * player.r, player.r * 0.18f, -U.Rand(0.5f, 1.5f) * player.r);
            }
            else pStamina = Mathf.Min(1, pStamina + dt * 0.3f);
            if (pDashing && !was) sfx.Dash();
            wakeT = Mathf.Max(0, wakeT - dt);
            bool burst = Powered(PearlKind.Burst);
            if ((pDashing || (burst && mag > 0.2f)) && wakeT <= 0)
            {
                wakeT = 0.055f;
                parts.Add(new Particle { type = PType.Wake, x = player.x - player.face * player.r * 1.55f, y = player.y,
                    vx = player.vx, vy = player.vy, life = 0.25f, max = 0.25f, size = player.r * 0.55f, col = burst ? Powers.Colors[(int)PearlKind.Burst] : U.Hex("#97ece2") });
            }
            // A dash leaves rings in the water behind the tail.
            rippleT = Mathf.Max(0, rippleT - dt);
            if (pDashing && rippleT <= 0)
            {
                rippleT = 0.16f;
                Ripple(player.x - player.face * player.r * 0.8f, player.y, 0.7f);
            }
            if (Powered(PearlKind.Magnet) && U.Rand() < dt * 28)
            {
                // Pink motes stream in from the edge of the magnet's reach.
                float a = U.Rand(0, U.TAU), reach = MagnetReach;
                parts.Add(new Particle { type = PType.Spark, x = player.x + Mathf.Cos(a) * reach, y = player.y + Mathf.Sin(a) * reach,
                    vx = -Mathf.Cos(a) * reach * 2.2f, vy = -Mathf.Sin(a) * reach * 2.2f, life = 0.55f, max = 0.55f, size = player.r * 0.24f, col = Powers.Colors[(int)PearlKind.Magnet] });
            }

            float spd = bas * mag * (pDashing ? 1.85f : 1) * (burst ? 1.3f : 1) * (pStun > 0 ? 0.3f : 1);
            float k = Mathf.Min(1, dt * (pDashing ? 9 : 6));
            player.vx += (dx * spd - player.vx) * k;
            player.vy += (dy * spd - player.vy) * k;
            player.x = Mathf.Clamp(player.x + player.vx * dt, player.r, World.W - player.r);
            float fy = world.FloorY(player.x);
            player.y = Mathf.Clamp(player.y + player.vy * dt, player.r * 0.4f, fy - player.r * 0.6f);

            bool open = false;
            foreach (var f in fish)
            {
                if (f.r > player.r * Data.EAT && f != finale) continue;
                float ox = f.x - player.x, oy = f.y - player.y;
                if (ox * player.face < -player.r * 0.3f) continue;
                if (Dist(ox, oy) < (player.r + f.r) * 2.3f) { open = true; break; }
            }
            pBlinkT -= dt;
            if (pBlinkT < 0) { pBlink = 0.12f; pBlinkT = U.Rand(2.5f, 5); }
            pBlink = Mathf.Max(0, pBlink - dt);

            AnimateFish(player, dt, false);
            float target = player.chomp > 0 ? 0.15f : open ? 1 : 0;
            player.mouth += (target - player.mouth) * Mathf.Min(1, dt * 14);

            float pr = player.r;
            for (int i = fish.Count - 1; i >= 0; i--)
            {
                var f = fish[i];
                float ox = f.x - player.x, oy = f.y - player.y, d = Dist(ox, oy);
                if (d > pr + f.r) continue;
                float ratio = f.r / pr;
                if (f == finale && ratio > Data.EAT)
                {
                    // Too big to swallow and harmless: bump against it and take a bite.
                    if (d < (pr + f.r) * 0.8f)
                    {
                        float nx = ox / (d > 0 ? d : 1), ny = oy / (d > 0 ? d : 1), push = ((pr + f.r) * 0.8f - d) * 0.5f;
                        player.x -= nx * push; player.y -= ny * push; f.x += nx * push; f.y += ny * push;
                        if (biteCool <= 0) BiteFinale(f);
                    }
                }
                else if (ratio <= Data.EAT)
                {
                    if (d < (pr + f.r * 0.5f) * 0.9f) EatFish(i);
                }
                else if (ratio >= Data.DANGER)
                {
                    if (d < pr * 0.55f + f.r * 0.72f)
                    {
                        if (pShield > 0 || pInvuln > 0 || victoryT > 0)
                        {
                            float nx = ox / (d > 0 ? d : 1), ny = oy / (d > 0 ? d : 1);
                            player.vx -= nx * bas * 1.2f; player.vy -= ny * bas * 1.2f;
                            f.vx += nx * bas * 0.6f; f.vy += ny * bas * 0.6f;
                            int bt = Mathf.FloorToInt(time * 4);
                            if (pShield > 0 && f.bumpT != bt) { f.bumpT = bt; sfx.Bump(); }
                        }
                        else { KillPlayer(f); return; }
                    }
                }
                else if (d < (pr + f.r) * 0.8f)
                {
                    float nx = ox / (d > 0 ? d : 1), ny = oy / (d > 0 ? d : 1), push = ((pr + f.r) * 0.8f - d) * 0.5f;
                    player.x -= nx * push; player.y -= ny * push; f.x += nx * push; f.y += ny * push;
                }
            }

            foreach (var j in jellies)
            {
                if (pStungCool > 0 || pShield > 0 || pInvuln > 0 || victoryT > 0) break;
                float d1 = Dist(j.x - player.x, j.y - player.y);
                float d2 = Dist(j.x - player.x, j.y + j.r * 1.4f - player.y);
                if (d1 < pr * 0.7f + j.r * 0.85f || d2 < pr * 0.7f + j.r * 0.55f)
                {
                    pStun = 1.1f; pStungCool = 2.2f; pStamina = 0; pTired = true;
                    float nx = (player.x - j.x) / (d1 > 0 ? d1 : 1), ny = (player.y - j.y) / (d1 > 0 ? d1 : 1);
                    player.vx = nx * bas * 1.4f; player.vy = ny * bas * 1.4f;
                    Sparkle(player.x, player.y, pr, 14, U.Hsl(j.hue, 1, 0.75f));
                    hud.AddText(player.x, player.y - pr * 1.4f, "ZAP!", U.Hex("#ff9cf0"), 26);
                    shake = Mathf.Max(shake, 7);
                    sfx.Zap();
                    Hint("jelly", "OUCH!", "Jellyfish sting at any size — swim around them", new Color(1, 120 / 255f, 230 / 255f));
                }
            }

            for (int i = pearls.Count - 1; i >= 0; i--)
            {
                var p = pearls[i];
                if (Dist(p.x - player.x, p.y - player.y) < pr + p.r)
                {
                    pearls.RemoveAt(i);
                    DestroyPearl(p);
                    int kind = (int)p.kind;
                    power[kind] = Powers.Durations[kind]; pStamina = 1; pTired = false;
                    int pts = 100 * (tier + 1);
                    score += pts;
                    var col = Powers.Colors[kind];
                    hud.AddText(p.x, p.y - p.r * 2, Powers.Names[kind] + " +" + pts, Color.Lerp(col, Color.white, 0.5f), 24);
                    Sparkle(p.x, p.y, pr, 22, Color.white);
                    parts.Add(new Particle { type = PType.Ring, x = p.x, y = p.y, life = 0.6f, max = 0.6f, size = pr * 3, col = Color.Lerp(col, Color.white, 0.4f) });
                    sfx.Pearl();
                    Debug.Log($"[DeepFeast] pearl: {p.kind} at t={playTime:0}");
                    switch (p.kind)
                    {
                        case PearlKind.Shield: Hint("pearl", "BUBBLE SHIELD", "Predators bounce off you for a few seconds", col); break;
                        case PearlKind.Magnet: Hint("magnet", "PEARL MAGNET", "Fish small enough to eat are reeled in", col); break;
                        case PearlKind.Burst: Hint("burst", "SPEED BURST", "Swim faster and dash without tiring", col); break;
                        default: Hint("lantern", "ABYSS LANTERN", "Light up the deep — hunters lose your trail", col); break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ AI
        void UpdateFish(float dt)
        {
            float pr = FocusR, bas = Data.SpeedFor(pr);
            bool active = Playing;
            float sight = Mathf.Min(ViewW, ViewH) * 0.62f;

            foreach (var s in schools) { s.cx = 0; s.cy = 0; s.n = 0; s.vx = 0; }
            foreach (var f in fish) if (f.school != null) { f.school.cx += f.x; f.school.cy += f.y; f.school.vx += f.vx; f.school.n++; }
            schools.RemoveAll(s => s.n == 0);
            foreach (var s in schools)
            {
                s.cx /= s.n; s.cy /= s.n;
                s.fleeT = Mathf.Max(0, s.fleeT - dt);
                s.turnT -= dt;
                if (s.fleeT <= 0 && s.wasFleeing) s.dir = s.vx >= 0 ? 1 : -1;
                s.wasFleeing = s.fleeT > 0;
                if (s.turnT <= 0) { s.turnT = U.Rand(7, 15); if (U.Chance(0.4f)) s.dir *= -1; }
                if (s.cx < 300) s.dir = 1; else if (s.cx > World.W - 300) s.dir = -1;
            }

            // The magnet holds only its nearest few catches, so it reels in a steady stream
            // rather than a whole school at once.
            magnetPull.Clear();
            if (active && Powered(PearlKind.Magnet))
            {
                float reach = MagnetReach;
                foreach (var f in fish)
                {
                    float d = Dist(player.x - f.x, player.y - f.y);
                    if (f != finale && f.r / pr <= Data.EAT && d < reach) magnetPull.Add((d, f));
                }
                magnetPull.Sort((a, b) => a.d.CompareTo(b.d));
                if (magnetPull.Count > MagnetHold) magnetPull.RemoveRange(MagnetHold, magnetPull.Count - MagnetHold);
            }

            foreach (var f in fish)
            {
                f.cool = Mathf.Max(0, f.cool - dt);
                f.alertT = Mathf.Max(0, f.alertT - dt);
                f.eatCool = Mathf.Max(0, f.eatCool - dt);
                float dx = player.x - f.x, dy = player.y - f.y, d = Dist(dx, dy);
                if (d <= 0) d = 1;
                float ratio = f.r / pr;
                float tx, ty, spd, agility;

                if (f.shark)
                {
                    f.life -= dt;
                    bool prey = ratio <= Data.EAT;
                    if (f == finale && f.life > 0 && active)
                    {
                        // A filter feeder: it cruises past without hunting, swims off ahead of you once you
                        // close in and bolts for a moment after every bite. It keeps to open water rather
                        // than pinning itself to the seabed.
                        bool shy = d < pr * 4 + f.r || f.cool > 0;
                        if (shy) f.dir = dx > 0 ? -1 : 1;
                        float mid = Mathf.Clamp(f.homeY, 120 + f.r * 1.5f, world.FloorY(f.x) - f.r * 2);
                        tx = f.dir; ty = Mathf.Clamp((mid - f.y) / (f.r * 6), -0.5f, 0.5f);
                        spd = bas * (f.cool > 0 ? 0.85f : shy ? 0.62f : 0.45f); agility = shy ? 1.8f : 1.2f;
                        f.state = shy ? FState.Flee : FState.Wander;
                    }
                    else if (f.life > 0 && active && !prey)
                    {
                        float lead = Mathf.Min(d / bas, 0.5f);
                        float ax = player.x + player.vx * lead - f.x, ay = player.y + player.vy * lead - f.y, ad = Dist(ax, ay);
                        if (ad <= 0) ad = 1;
                        tx = ax / ad; ty = ay / ad; spd = bas * 1.1f * f.sp.chaseSpeedMultiplier; agility = 1.7f; f.state = FState.Chase;
                    }
                    else if (f.life > 0 && active && prey)
                    {
                        tx = -dx / d; ty = -dy / d * 0.7f; spd = bas * 0.82f; agility = 2.4f; f.state = FState.Flee;
                    }
                    else
                    {
                        f.state = FState.Leave;
                        tx = f.leaveDir; ty = -0.15f; spd = bas * 1.1f; agility = 1.5f;
                    }
                }
                else
                {
                    if (active && ratio >= Data.DANGER && f.state != FState.Chase && f.cool <= 0 && d < sight &&
                        pInvuln <= 0 && pShield <= 0 && !Powered(PearlKind.Lantern) && U.Rand() < dt * 0.7f * f.aggro)
                    {
                        f.state = FState.Chase; f.chaseT = U.Rand(3, 5.5f); f.alertT = 1.1f;
                        sfx.Alert();
                        Hint("pred", "WATCH OUT!", "Bigger fish will hunt you — outswim them or dash", new Color(1, 80 / 255f, 90 / 255f));
                    }
                    if (f.state == FState.Chase)
                    {
                        f.chaseT -= dt;
                        if (!active || ratio < Data.DANGER || pInvuln > 0 || pShield > 0 || Powered(PearlKind.Lantern) || f.chaseT <= 0 || d > sight * 1.7f)
                        {
                            f.state = FState.Wander; f.cool = U.Rand(4, 8); f.dir = f.vx >= 0 ? 1 : -1;
                        }
                    }
                    bool fleeing = active && ratio <= Data.EAT && d < sight * 0.5f;
                    if (f.state == FState.Chase)
                    {
                        float lead = Mathf.Min(d / bas, 0.6f);
                        float ax = player.x + player.vx * lead - f.x, ay = player.y + player.vy * lead - f.y, ad = Dist(ax, ay);
                        if (ad <= 0) ad = 1;
                        tx = ax / ad; ty = ay / ad; spd = bas * 0.9f; agility = 2.1f;
                    }
                    else if (fleeing || (f.school != null && f.school.fleeT > 0))
                    {
                        if (fleeing && f.school != null) f.school.fleeT = 0.8f;
                        tx = -dx / d + Mathf.Sin(time * 3 + f.phase) * 0.35f;
                        ty = (-dy / d) * 0.8f + Mathf.Cos(time * 2.3f + f.phase) * 0.25f;
                        spd = bas * (f.school != null ? 0.68f : 0.6f); agility = 3.2f;
                        f.dir = tx >= 0 ? 1 : -1;
                    }
                    else if (f.school != null)
                    {
                        var s = f.school;
                        tx = s.dir + (s.cx + f.slotX - f.x) / (f.r * 10);
                        ty = (s.cy + f.slotY - f.y) / (f.r * 10) + Mathf.Sin(time * 1.1f + s.cx * 0.001f) * 0.2f;
                        spd = bas * f.cruise; agility = 2.2f;
                    }
                    else
                    {
                        f.homeT -= dt;
                        if (f.homeT <= 0)
                        {
                            f.homeT = U.Rand(3, 8);
                            f.homeY = Mathf.Clamp(f.y + U.Rand(-1, 1) * f.r * 14, 90 + f.r, world.FloorY(f.x) - f.r - 40);
                            if (U.Chance(0.12f)) f.dir *= -1;
                        }
                        tx = f.dir;
                        ty = Mathf.Clamp((f.homeY - f.y) / (f.r * 20), -0.4f, 0.4f) + Mathf.Sin(time * 0.9f + f.phase) * 0.12f;
                        spd = bas * f.cruise; agility = 1.5f;
                    }
                }

                // The magnet pearl reels in its catches, fastest from close in.
                if (Reeling(f))
                {
                    tx = dx / d; ty = dy / d; spd = bas * Mathf.Lerp(0.8f, 0.35f, d / MagnetReach); agility = 4;
                }

                if (!f.shark || f.state != FState.Leave)
                {
                    if (f.x < 160) { f.dir = 1; tx = Mathf.Abs(tx) + 0.6f; }
                    else if (f.x > World.W - 160) { f.dir = -1; tx = -Mathf.Abs(tx) - 0.6f; }
                }
                float fy = world.FloorY(f.x);
                if (f.y < 70 + f.r) ty = Mathf.Abs(ty) + 0.4f;
                else if (f.y > fy - f.r - 30) ty = -Mathf.Abs(ty) - 0.4f;
                float tn = Dist(tx, ty);
                if (tn <= 0) tn = 1;
                float k = Mathf.Min(1, dt * agility);
                f.vx += (tx / tn * spd - f.vx) * k;
                f.vy += (ty / tn * spd - f.vy) * k;
                f.x += f.vx * dt; f.y += f.vy * dt;
                f.y = Mathf.Clamp(f.y, 30 + f.r * 0.5f, world.FloorY(f.x) - f.r * 0.6f);
                AnimateFish(f, dt);
            }

            // the food chain runs without you, too (on screen only so you can see it)
            float vx0 = cam.x - ViewW / 2, vx1 = cam.x + ViewW / 2;
            for (int i = 0; i < fish.Count; i++)
            {
                var a = fish[i];
                if (a.school != null || a.eatCool > 0 || a.x < vx0 || a.x > vx1) continue;
                for (int j = fish.Count - 1; j >= 0; j--)
                {
                    var b = fish[j];
                    if (b == a || b.shark || a.r < b.r * 1.45f) continue;
                    if (Dist(a.x - b.x, a.y - b.y) < (a.r + b.r) * 0.55f)
                    {
                        fish.RemoveAt(j);
                        ReleaseView(b);
                        if (j < i) i--;
                        a.chomp = 0.3f; a.eatCool = U.Rand(3, 6);
                        Burst(b.x, b.y, b.r * 0.8f, b.sp.c0);
                        break;
                    }
                }
            }
        }

        void UpdateJellies(float dt)
        {
            foreach (var j in jellies)
            {
                j.phase += dt * j.pf;
                float pulse = Mathf.Sin(j.phase);
                float target = pulse > 0.55f ? -j.r * 1.7f : j.r * 0.22f;
                j.vy += (target - j.vy) * Mathf.Min(1, dt * 2.2f);
                j.x += j.vx * dt; j.y += j.vy * dt;
                j.y = Mathf.Clamp(j.y, 500, world.FloorY(j.x) - j.r * 3.5f);
            }
        }

        void UpdateSpawner(float dt)
        {
            float vw = ViewW, vh = ViewH;
            for (int i = fish.Count - 1; i >= 0; i--)
            {
                var f = fish[i];
                bool far = Mathf.Abs(f.x - cam.x) > vw * 1.1f + f.r * 3 || Mathf.Abs(f.y - cam.y) > vh * 1.15f + f.r * 3;
                if (far && !(f.shark && f.life > 0)) { fish.RemoveAt(i); ReleaseView(f); }
            }
            for (int i = jellies.Count - 1; i >= 0; i--)
            {
                var j = jellies[i];
                if (Mathf.Abs(j.x - cam.x) > vw * 1.4f || Mathf.Abs(j.y - cam.y) > vh * 1.6f) { DestroyJelly(j); jellies.RemoveAt(i); }
            }
            for (int i = pearls.Count - 1; i >= 0; i--)
            {
                pearls[i].life -= dt;
                if (pearls[i].life <= 0) { DestroyPearl(pearls[i]); pearls.RemoveAt(i); }
            }
            for (int n = 0; n < 3 && fish.Count < 78; n++) SpawnOne(false);
            int jTarget = cam.y > 900 ? 4 : 2;
            if (jellies.Count < jTarget && U.Rand() < dt * 0.5f) SpawnJelly();

            if (U.Rand() < dt * 6)
            {
                float x = cam.x + U.Rand(-0.5f, 0.5f) * vw, fy = world.FloorY(x);
                if (fy < cam.y + vh / 2) Bubble(x, fy - 4, U.Rand(2, 5) / zoom, -U.Rand(25, 60) / zoom);
            }
        }

        void UpdateEvents(float dt)
        {
            playTime += dt;
            comboT = Mathf.Max(0, comboT - dt);
            if (comboT <= 0) combo = 0;
            if (!pAlive)
            {
                dieT -= dt;
                if (dieT <= 0) { if (lives > 0) Respawn(); else GameOver(); }
                return;
            }
            if (finale != null && !fish.Contains(finale))
            {
                finale = null; finaleT = U.Rand(20, 30);
                Debug.Log("[DeepFeast] finale: the whale shark got away");
                Banner("IT GOT AWAY", "The whale shark will be back", new Color(1, 212 / 255f, 71 / 255f));
            }
            if (finaleT > 0 && (finaleT -= dt) <= 0)
            {
                // Wait for any shark encounter to clear before the finale.
                if (fish.Exists(f => f.shark)) finaleT = 3; else SpawnWhaleShark();
            }
            if (victoryT > 0 && (victoryT -= dt) <= 0) { Victory(); return; }
            sharkT -= dt;
            if (sharkT <= 0)
            {
                if (!fish.Exists(f => f.shark)) SpawnShark();
                sharkT = U.Rand(35, 55);
            }
            pearlT -= dt;
            if (pearlT <= 0) { if (pearls.Count == 0) SpawnPearl(); pearlT = pearlEvery > 0 ? pearlEvery : U.Rand(20, 32); }
        }

        void UpdateCamera(float dt)
        {
            float tz = Data.ZoomFor(pActive ? player.r : 22);
            zoom += (tz - zoom) * Mathf.Min(1, dt * 1.6f);
            float fx, fy;
            if (pActive) { fx = player.x + player.vx * 0.3f; fy = player.y + player.vy * 0.2f; }
            else
            {
                menuCamX += menuDir * 70 * dt;
                if (menuCamX > World.W - 1500) menuDir = -1; else if (menuCamX < 1500) menuDir = 1;
                fx = menuCamX; fy = world.FloorY(menuCamX) - 480;
            }
            float k = Mathf.Min(1, dt * (pActive ? 4 : 1.2f));
            cam.x += (fx - cam.x) * k; cam.y += (fy - cam.y) * k;
            float hw = refW / 2 / zoom, hh = refH / 2 / zoom;
            cam.x = Mathf.Clamp(cam.x, hw, World.W - hw);
            cam.y = Mathf.Clamp(cam.y, hh - Mathf.Min(170, hh * 0.28f), World.H - hh);
            shake *= Mathf.Exp(-dt * 7);
        }

        // ------------------------------------------------------------------ bot (for -autoplay testing)
        Vector2 BotSteer(out bool dash)
        {
            dash = false;
            float pr = player.r;
            Vector2 avoid = Vector2.zero, seek = Vector2.zero;
            float bestV = 0;
            foreach (var f in fish)
            {
                float dx = f.x - player.x, dy = f.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                if (f == finale) continue;
                if (f.r >= pr * Data.DANGER)
                {
                    bool hunting = f.state == FState.Chase;
                    float R = f.r * (hunting ? 4.5f : 2.6f) + pr * 5;
                    if (d < R)
                    {
                        float w = (1 - d / R); w *= w * (hunting ? 9 : 4);
                        avoid -= new Vector2(dx, dy) / d * w;
                        if (hunting && d < f.r * 2.4f + pr * 4) dash = true;
                    }
                }
                else if (f.r <= pr * Data.EAT)
                {
                    float v = (f.r / pr) / (d + pr * 3);
                    if (v > bestV) { bestV = v; seek = new Vector2(dx, dy) / d; }
                }
            }
            foreach (var j in jellies)
            {
                float dx = j.x - player.x, dy = j.y + j.r * 0.7f - player.y, d = Mathf.Max(1, Dist(dx, dy));
                float R = j.r * 3 + pr * 3;
                if (d < R) { float w = 1 - d / R; avoid -= new Vector2(dx, dy) / d * w * w * 6; }
            }
            foreach (var p in pearls)
            {
                float dx = p.x - player.x, dy = p.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                seek = new Vector2(dx, dy) / d * 1.5f;
            }
            // The finale is worth more than any snack: follow the gold arrow and dash in close.
            if (finale != null)
            {
                float dx = finale.x - player.x, dy = finale.y - player.y, d = Mathf.Max(1, Dist(dx, dy));
                seek = new Vector2(dx, dy) / d * 4; bestV = 1;
                if (d < pr * 6 && pStamina > 0.4f) dash = true;
            }
            if (bestV <= 0 && pearls.Count == 0)
            {
                if (player.x < 600) botWanderDir = 1; else if (player.x > World.W - 600) botWanderDir = -1;
                seek = new Vector2(botWanderDir, 0);
            }
            var v2 = seek + avoid;
            float fy = world.FloorY(player.x);
            if (fy - player.y < pr * (finale != null ? 1.5f : 4)) v2.y -= 1.5f;
            if (player.y < pr * 3 + 60) v2.y += 1;
            if (v2.sqrMagnitude < 1e-4f) v2 = new Vector2(botWanderDir, 0);
            return v2.normalized;
        }

        // ================================================================== rendering glue
        Vector2 ToScreen(float x, float y) => new Vector2((x - cam.x) * zoom + refW / 2, (y - cam.y) * zoom + refH / 2);

        void Ripple(float x, float y, float force)
        {
            var s = ToScreen(x, y);
            presenter.AddRipple(new Vector2(s.x / refW, 1 - s.y / refH), force);
        }

        FishView GetView()
        {
            var v = viewPool.Count > 0 ? viewPool.Pop() : new FishView(fishRoot, haloRoot);
            v.SetActive(true);
            return v;
        }

        void ReleaseView(Fish f)
        {
            if (f.view == null) return;
            f.view.SetActive(false);
            viewPool.Push(f.view);
            f.view = null;
        }

        readonly List<Fish> sorted = new List<Fish>();

        void Render(float dt, float rdt)
        {
            var habitat = Habitat.At(cam.y);
            Shader.SetGlobalColor("_SceneLight", habitat.light);
            presenter.SetHabitat(habitat);
            presenter.Step(state == GState.Paused ? 0 : rdt);
            hud.SetHabitat(habitat.name, habitat.accent);
            float shk = state == GState.Paused ? 0 : shake * GameSettings.Data.shake / 100f;
            float shx = (U.Rand() - 0.5f) * shk * 2 / zoom, shy = (U.Rand() - 0.5f) * shk * 2 / zoom;
            cam3.orthographicSize = refH / 2 / zoom;
            var camPos = new Vector3(cam.x + shx, -(cam.y + shy), -2000);
            cam3.transform.position = camPos;

            float hw = refW / 2 / zoom, hh = refH / 2 / zoom;
            float x0 = cam.x - hw, x1 = cam.x + hw, yTop = cam.y - hh, yBot = cam.y + hh;
            float pr = FocusR;

            // fish, biggest at the back, sharks above everything else
            sorted.Clear();
            foreach (var f in fish)
            {
                bool vis = f.x > x0 - f.r * 3 && f.x < x1 + f.r * 3 && f.y > yTop - f.r * 3 && f.y < yBot + f.r * 3;
                if (!vis) { ReleaseView(f); continue; }
                sorted.Add(f);
            }
            sorted.Sort((a, b) => a.shark != b.shark ? (a.shark ? 1 : -1) : b.r != a.r ? b.r.CompareTo(a.r) : a.id.CompareTo(b.id));
            for (int i = 0; i < sorted.Count; i++)
            {
                var f = sorted[i];
                if (f.view == null) f.view = GetView();
                f.view.SetSpecies(f.sp);
                f.view.Pose(f, f.shark ? Layer.Shark + i : Layer.FishBase + i, f.state == FState.Chase ? FishView.EyeMode.Angry : FishView.EyeMode.Normal, dt);
                var halo = f.view.halo;
                bool quarry = f == finale, threat = pActive && !quarry && f.r >= pr * Data.DANGER, glows = !threat && !quarry && f.sp.glow > 0;
                halo.enabled = threat || glows || quarry;
                if (threat || quarry)
                {
                    float hot = f.state == FState.Chase ? 1 : 0;
                    float a = quarry ? 0.1f + 0.04f * Mathf.Sin(time * 4) : 0.045f + hot * (0.075f + 0.025f * Mathf.Sin(time * 7));
                    halo.transform.localPosition = U.V3(f.x, f.y);
                    float d = f.r * 1.9f * 2;
                    halo.transform.localScale = new Vector3(d, d, 1);
                    halo.color = quarry ? new Color(1, 212 / 255f, 71 / 255f, a) : new Color(1, 70 / 255f, 85 / 255f, a);
                }
                else if (glows)
                {
                    // Photophores are too small to read at play size; a soft light in their color carries them.
                    float a = f.sp.glow * (0.26f + 0.06f * Mathf.Sin(time * 3 + f.phase)) * Mathf.Clamp01(World.DarkAt(f.y) / 0.3f + 0.25f);
                    var light = GlowPoint(f);
                    halo.transform.localPosition = U.V3(light.x, light.y);
                    float d = f.r * (f.sp.glowOffset == Vector2.zero ? 2.6f : 1.6f) * 2;
                    halo.transform.localScale = new Vector3(d, d, 1);
                    halo.color = U.WithA(f.sp.glowColor, a);
                }
            }

            // player
            bool pv = pActive && pAlive;
            playerView.SetActive(pv);
            playerGlow.enabled = pv;
            if (pv)
            {
                bool blinkOut = pInvuln > 0 && Mathf.FloorToInt(time * 12) % 2 == 0;
                playerView.SetVisible(!blinkOut);
                var mode = pBlink > 0 ? FishView.EyeMode.Blink : player.chomp > 0 ? FishView.EyeMode.Happy : FishView.EyeMode.Normal;
                playerView.Pose(player, Layer.Player, mode, dt);
                float deep = Mathf.Clamp01((player.y - 1200) / 2500);
                playerGlow.transform.localPosition = U.V3(player.x, player.y);
                // The aura takes the color of the strongest active power; the lantern burns wide and warm.
                var aura = new Color(80 / 255f, 1, 220 / 255f, 0.1f + deep * 0.16f + Mathf.Sin(time * 2.4f) * 0.025f);
                float gd = player.r * 8;
                for (int k = 1; k < Powers.Count; k++)
                {
                    float on = Mathf.Clamp01(power[k] / 1.5f);
                    if (on <= 0) continue;
                    var c = Powers.Colors[k];
                    aura = Color.Lerp(aura, new Color(c.r, c.g, c.b, k == (int)PearlKind.Lantern ? 0.42f : 0.26f), on);
                    if (k == (int)PearlKind.Lantern) gd = Mathf.Lerp(gd, player.r * 15, on);
                }
                playerGlow.transform.localScale = new Vector3(gd, gd, 1);
                playerGlow.color = aura;
            }
            bool sh = pv && pShield > 0;
            shieldRing.enabled = shieldFill.enabled = shieldShine.enabled = sh;
            if (sh)
            {
                float a = pShield < 2 ? 0.3f + 0.3f * Mathf.Sin(time * 20) : 0.6f;
                float R = player.r * 1.9f + Mathf.Sin(time * 4) * player.r * 0.08f;
                var pos = U.V3(player.x, player.y);
                shieldRing.transform.localPosition = pos; shieldRing.transform.localScale = Vector3.one * (2 * R / 0.9f);
                shieldRing.color = new Color(170 / 255f, 245 / 255f, 1, a);
                shieldFill.transform.localPosition = pos; shieldFill.transform.localScale = Vector3.one * (2 * R);
                shieldFill.color = new Color(170 / 255f, 245 / 255f, 1, a * 0.15f);
                shieldShine.transform.localPosition = U.V3(player.x - R * 0.45f, player.y - R * 0.5f);
                shieldShine.transform.localScale = new Vector3(R * 0.44f, R * 0.2f, 1);
                shieldShine.transform.localRotation = Quaternion.Euler(0, 0, 0.7f * Mathf.Rad2Deg);
                shieldShine.color = new Color(1, 1, 1, a);
            }
            for (int i = 0; i < 3; i++)
            {
                bool on = pv && pStun > 0;
                stunStars[i].enabled = on;
                if (!on) continue;
                float a = time * 6 + i * U.TAU / 3;
                stunStars[i].transform.localPosition = U.V3(player.x + Mathf.Cos(a) * player.r * 0.9f, player.y - player.r * 1.3f + Mathf.Sin(a) * player.r * 0.25f);
                stunStars[i].transform.localScale = Vector3.one * player.r * 0.24f;
                stunStars[i].color = U.Hex("#fff6a8");
            }

            // jellies
            jmb.Clear();
            for (int ji = 0; ji < jellies.Count; ji++)
            {
                var j = jellies[ji];
                float pulse = 0.5f + 0.5f * Mathf.Sin(j.phase);
                float bw = j.r * (1 + pulse * 0.14f), bh = j.r * (0.82f - pulse * 0.14f);
                j.bell.transform.localPosition = U.V3(j.x, j.y);
                j.bell.transform.localScale = new Vector3(bw, bh / 0.75f, 1);
                j.bell.sortingOrder = Layer.Jelly + ji;
                j.glow.transform.localPosition = U.V3(j.x, j.y);
                j.glow.transform.localScale = Vector3.one * j.r * 6;
                // fine tentacles ending in glowing beads
                var tc = U.Hsl(j.hue, 0.65f, 0.8f, 0.55f);
                var bead = U.Hsl(j.hue, 0.7f, 0.93f, 0.78f);
                float tw = Mathf.Max(0.75f / zoom, j.r * 0.04f);
                var pts = new List<Vector2>(12);
                for (int i = 0; i < 8; i++)
                {
                    float tx0 = j.x + (i / 7f - 0.5f) * bw * 1.5f;
                    pts.Clear();
                    pts.Add(new Vector2(tx0, j.y));
                    for (int s = 1; s <= 10; s++)
                        pts.Add(new Vector2(tx0 + Mathf.Sin(time * 2.4f + s * 0.6f + i * 1.3f) * j.r * 0.14f * (s / 4f), j.y + s * j.r * (0.26f + (i % 3) * 0.03f)));
                    Draw.Stroke(jmb, pts, tw, tw * 0.18f, tc, U.WithA(tc, 0.22f));
                    var e = pts[pts.Count - 1];
                    Draw.RadialDisc(jmb, e.x, e.y, tw * 1.8f, U.WithA(bead, 0.26f), U.WithA(bead, 0), 12);
                    Draw.Circle(jmb, e.x, e.y, tw * 0.7f, bead, 10);
                }
                // frilly oral arms: wide translucent ribbons with a brighter spine
                Color armA = U.Hsl(j.hue, 0.65f, 0.84f, 0.65f), armB = U.Hsl(j.hue, 0.7f, 0.75f, 0.2f), spine = U.Hsl(j.hue, 0.6f, 0.93f, 0.36f);
                float ow = Mathf.Max(1.5f / zoom, j.r * 0.32f);
                for (int i = 0; i < 4; i++)
                {
                    float tx0 = j.x + (i - 1.5f) * bw * 0.16f;
                    var c = Draw.CubicPts(tx0, j.y, tx0 + Mathf.Sin(time * 1.6f + i) * j.r * 0.3f, j.y + j.r * 0.8f,
                        tx0 - Mathf.Sin(time * 1.3f + i) * j.r * 0.3f, j.y + j.r * 1.4f, tx0 + (i - 1.5f) * j.r * 0.12f, j.y + j.r * (1.9f + (i & 1) * 0.35f), 16);
                    Draw.Ribbon(jmb, c, ow, ow * 0.15f, 0.18f, time * 2 + i * 1.7f, armA, armB);
                    Draw.Stroke(jmb, c, ow * 0.18f, ow * 0.04f, spine, U.WithA(spine, 0.1f));
                }
            }
            jmb.Apply(jellyMesh);

            // pearls
            foreach (var p in pearls)
            {
                float bob = Mathf.Sin(time * 2 + p.ph) * p.r * 0.4f;
                bool hidden = p.life < 3 && Mathf.FloorToInt(time * 8) % 2 == 1;
                p.body.enabled = p.star.enabled = p.ring.enabled = p.glow.enabled = !hidden;
                var pos = U.V3(p.x, p.y + bob);
                p.body.transform.localPosition = pos; p.body.transform.localScale = Vector3.one * p.r;
                p.ring.transform.localPosition = pos; p.ring.transform.localScale = Vector3.one * p.r * (1 + 0.04f * Mathf.Sin(time * 3 + p.ph));
                p.ring.color = U.WithA(Color.Lerp(Powers.Colors[(int)p.kind], Color.white, 0.25f), 0.6f + 0.14f * Mathf.Sin(time * 2 + p.ph));
                float tw = 0.85f + 0.15f * Mathf.Sin(time * 5 + p.ph);
                p.star.transform.localPosition = pos; p.star.transform.localScale = Vector3.one * p.r * tw * 0.62f;
                p.star.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 0.8f + p.ph) * 12);
                p.glow.transform.localPosition = pos; p.glow.transform.localScale = Vector3.one * p.r * 6;
            }

            parts.Render();

            // light holes in the abyss
            holes.Clear();
            if (pv)
            {
                // The lantern opens a wide pool of light, shrinking back as it runs out.
                float lamp = Mathf.Clamp01(power[(int)PearlKind.Lantern] / 1.5f);
                holes.Add(new Hole(player.x, player.y, Mathf.Lerp(player.r * 8, Mathf.Max(player.r * 22, ViewH * 0.55f), lamp), Mathf.Lerp(0.9f, 1, lamp)));
            }
            foreach (var j in jellies) holes.Add(new Hole(j.x, j.y + j.r * 0.5f, j.r * 4, 0.75f));
            foreach (var p in pearls) holes.Add(new Hole(p.x, p.y, p.r * 8, 0.8f));
            foreach (var f in fish)
                if (f.sp.glow > 0 && f.x > x0 - 200 && f.x < x1 + 200)
                {
                    var light = GlowPoint(f);
                    holes.Add(new Hole(light.x, light.y, f.r * 3.5f, 0.45f * f.sp.glow));
                }
            foreach (var a in world.glowAnemones)
                if (a.x > x0 - 300 && a.x < x1 + 300) holes.Add(new Hole(a.x, a.y - a.s * 0.4f, a.s * 2.4f, 0.7f));
            foreach (var light in world.reefLights)
                if (light.x > x0 - light.r && light.x < x1 + light.r) holes.Add(light);

            fx.Update(time, cam, camPos, zoom, refW, refH, holes);
            world.UpdateView(time, x0, x1, yBot);

            // HUD
            alerts.Clear();
            if (Playing)
            {
                foreach (var f in fish)
                {
                    bool threat = f.r >= player.r * Data.DANGER && f.state == FState.Chase, quarry = f == finale;
                    if (!threat && !quarry && f.alertT <= 0) continue;
                    var s = ToScreen(f.x, f.y);
                    bool on = s.x > 0 && s.x < refW && s.y > 0 && s.y < refH;
                    if (on && f.alertT > 0)
                    {
                        float b = Mathf.Abs(Mathf.Sin((1.1f - f.alertT) * 8)) * 8;
                        alerts.Add(new Alert { onScreen = true, screen = new Vector2(s.x, s.y - f.r * 1.2f * zoom - 22), bounce = b });
                    }
                    else if (!on && (threat || quarry))
                    {
                        float dx = s.x - refW / 2, dy = s.y - refH / 2;
                        float m = Mathf.Min((refW / 2 - 34) / Mathf.Max(1e-3f, Mathf.Abs(dx)), (refH / 2 - 34) / Mathf.Max(1e-3f, Mathf.Abs(dy)));
                        alerts.Add(new Alert { onScreen = false, screen = new Vector2(refW / 2 + dx * m, refH / 2 + dy * m), angle = Mathf.Atan2(dy, dx), pulse = 0.6f + 0.4f * Mathf.Sin(time * 10), quarry = quarry });
                    }
                }
            }
            if (state == GState.Play || state == GState.Paused)
            {
                var cur = Data.Tiers[tier];
                bool hasNext = tier + 1 < Data.Tiers.Length;
                float prog = hasNext ? Mathf.Clamp01((player.r - cur.r) / (Data.Tiers[tier + 1].r - cur.r)) : 1;
                hud.UpdateHud(score, combo, comboT > 0, cur.name, prog, hasNext ? "next: " + Data.Tiers[tier + 1].name.ToUpperInvariant() : endless ? "ENDLESS DEEP" : "finale: WHALE SHARK",
                    lives, pStamina, pTired, Mathf.Max(0, Mathf.RoundToInt(player.y / 8)));
                hud.UpdatePowers(power);
            }
            hud.Tick(dt, rdt, (x, y) => ToScreen(x, y), alerts, interfaceReview != null ? time : Time.unscaledTime);
        }
    }
}
