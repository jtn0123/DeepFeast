using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepFeast
{
    /// <summary>Vertical (or horizontal) three-stop colour gradient for any uGUI graphic.</summary>
    public sealed class VertexGradient : BaseMeshEffect
    {
        public Color a = Color.white, b = Color.white, c = Color.white;
        public Color tint = Color.white;
        public bool multiplyGraphicColor;
        public float mid = 0.5f;
        public bool horizontal;
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts) { float k = horizontal ? v.position.x : v.position.y; min = Mathf.Min(min, k); max = Mathf.Max(max, k); }
            float span = Mathf.Max(1e-3f, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float t = ((horizontal ? v.position.x : v.position.y) - min) / span;
                if (!horizontal) t = 1 - t; // a = top
                Color col = t < mid ? Color.Lerp(a, b, t / mid) : Color.Lerp(b, c, (t - mid) / (1 - mid));
                col *= tint;
                Color orig = v.color;
                if (multiplyGraphicColor) { col.r *= orig.r; col.g *= orig.g; col.b *= orig.b; }
                col.a *= orig.a;
                v.color = col;
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }

    /// <summary>Holds a pointer-down flag for the on-screen dash button.</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool held;
        public void OnPointerDown(PointerEventData e) => held = true;
        public void OnPointerUp(PointerEventData e) => held = false;
        public void OnPointerExit(PointerEventData e) => held = false;
    }

    public struct Alert { public Vector2 screen; public bool onScreen; public float bounce, angle, pulse; }

    /// <summary>All UI: HUD, banners, menus and screen-space world labels — built from code.</summary>
    public sealed class Hud
    {
        public Action OnPlay, OnResume, OnMute;
        public bool DashHeld => dashHold != null && dashHold.held;

        readonly Canvas canvas;
        readonly CanvasScaler scaler;
        readonly Font font, display;
        readonly RectTransform rootRT, worldLayer;
        readonly CanvasGroup hudGroup, bannerGroup;
        readonly Text score, combo, tierName, tierNext, depth, bannerTitle, bannerSub, dashLabel, habitatName;
        readonly Image growth, dash, bannerGlow, comboPill, livesBg;
        readonly RectTransform growthRT, dashRT, bannerRT, livesRT, comboRT;
        readonly VertexGradient bannerGrad, growthGrad;
        readonly List<Image> lifeIcons = new List<Image>();
        readonly GameObject menu, pause, over, dashBtn;
        readonly Text bestMenu, bestOver, overTitle, newBest, sScore, sTier, sEaten, sTime;
        readonly RectTransform titleRT;
        readonly RectTransform menuCard, pauseCard, overCard;
        readonly List<RectTransform> menuFish = new List<RectTransform>();
        readonly Vector2 titleOrigin = new Vector2(52, -67);
        readonly Image muteIcon;
        readonly Sprite noteSprite, mutedSprite;
        readonly HoldButton dashHold;
        float bannerT = 99;
        float hudAlpha, hudTarget;
        float targetGrowth, shownGrowth, growthPulse, scorePulse;
        bool growthInitialized;
        const float BAR_W = 440, DASH_W = 178, LIFE_STEP = 38;

        static readonly Color PanelCol = new Color(0.02f, 0.11f, 0.2f, 0.6f);
        static readonly Color EdgeCol = new Color(0.63f, 0.94f, 1, 0.22f);
        static readonly Color TrackCol = new Color(0, 0.05f, 0.12f, 0.75f);

        sealed class FloatText { public float x, y, life, max, size; public Text t; public Color col; }
        readonly List<FloatText> texts = new List<FloatText>();
        readonly Stack<Text> textPool = new Stack<Text>();
        readonly List<Text> alertPool = new List<Text>();
        readonly List<Image> arrowPool = new List<Image>();

        public Hud()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var lilita = Resources.Load<Font>("Fonts/LilitaOne-Regular");
            display = lilita != null ? lilita : font;
            var cgo = new GameObject("UI");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cgo.AddComponent<GraphicRaycaster>();
            rootRT = cgo.GetComponent<RectTransform>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            worldLayer = Node("WorldLabels", rootRT);
            Stretch(worldLayer);

            // ---------------------------------------------------------- HUD
            var hud = Node("Hud", rootRT); Stretch(hud);
            hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
            hudGroup.alpha = 0; hudGroup.blocksRaycasts = false;

            // score + combo pill (top left)
            Label(hud, "SCORE", 11, U.Hex("#9bbdcb"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -12));
            score = Label(hud, "0", 36, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(23, -24), display);
            Gradient(score, Color.white, Color.white, U.Hex("#bff6ff"));
            AddOutline(score, new Color(0, 0.14f, 0.26f, 0.6f), 2f);
            AddShadow(score, new Color(0, 0.08f, 0.18f, 0.45f), new Vector2(0, -4));
            comboPill = Panel(hud, Color.white, 15);
            comboRT = comboPill.rectTransform;
            Place(comboRT, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -66), new Vector2(120, 30), new Vector2(0, 1));
            Gradient(comboPill, U.Hex("#ffe066"), U.Hex("#ffb52e"), U.Hex("#ff8a1c"));
            AddShadow(comboPill, new Color(0.45f, 0.18f, 0, 0.5f), new Vector2(0, -3));
            combo = Label(comboRT, "", 19, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 1), display);
            AddOutline(combo, new Color(0.55f, 0.22f, 0, 0.75f), 1.5f);
            comboPill.gameObject.SetActive(false);

            // tier + growth (top centre)
            var tp = Panel(hud, PanelCol, 18);
            Place(tp.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(BAR_W + 40, 66), new Vector2(0.5f, 1));
            AddOutline(tp, EdgeCol, 1.5f);
            tierName = Label(tp.rectTransform, "FRY", 25, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(20, -6), display);
            Gradient(tierName, Color.white, U.Hex("#c4f7ff"), U.Hex("#7fe8ff"));
            AddShadow(tierName, new Color(0, 0.1f, 0.2f, 0.6f), new Vector2(0, -2));
            tierNext = Label(tp.rectTransform, "", 15, new Color(1, 1, 1, 0.72f), TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-20, -13), display);
            var bar = Panel(tp.rectTransform, TrackCol, 8);
            Place(bar.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 11), new Vector2(BAR_W, 16), new Vector2(0.5f, 0));
            growth = Panel(bar.rectTransform, Color.white, 8);
            growthRT = growth.rectTransform;
            Place(growthRT, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 16), new Vector2(0, 0.5f));
            var gg = growthGrad = growth.gameObject.AddComponent<VertexGradient>();
            gg.horizontal = true; gg.a = U.Hex("#2ef2c8"); gg.b = U.Hex("#4fd1ff"); gg.c = U.Hex("#ffd447"); gg.mid = 0.6f;
            Shine(growthRT, 8);

            // lives (top right)
            livesRT = Node("Lives", hud);
            Place(livesRT, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(220, 42), new Vector2(1, 1));
            livesBg = Panel(livesRT, PanelCol, 18);
            Place(livesBg.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(60, 42), new Vector2(1, 1));
            AddOutline(livesBg, EdgeCol, 1.5f);
            var lifeSprite = FishArt.LifeIcon();
            for (int i = 0; i < 5; i++)
            {
                var img = Img(livesRT, lifeSprite, Color.white);
                Place(img.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12 - i * LIFE_STEP, -11), new Vector2(32, 20), new Vector2(1, 1));
                AddShadow(img, new Color(0, 0, 0, 0.35f), new Vector2(0, -2));
                lifeIcons.Add(img);
            }

            // depth + dash (bottom left)
            var dp = Panel(hud, PanelCol, 16);
            Place(dp.rectTransform, Vector2.zero, Vector2.zero, new Vector2(14, 14), new Vector2(DASH_W + 40, 88), Vector2.zero);
            AddOutline(dp, EdgeCol, 1.5f);
            habitatName = Label(dp.rectTransform, "CORAL REEF", 16, U.Hex("#80ead9"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(14, -9), display);
            depth = Label(dp.rectTransform, "DEPTH 0 m", 14, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(14, -37));
            dashLabel = Label(dp.rectTransform, "DASH", 12, new Color(1, 0.86f, 0.5f, 0.85f), TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-14, -39), display);
            var dbar = Panel(dp.rectTransform, TrackCol, 6);
            Place(dbar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(14, 12), new Vector2(DASH_W, 12), Vector2.zero);
            dash = Panel(dbar.rectTransform, Color.white, 6);
            dashRT = dash.rectTransform;
            Place(dashRT, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(DASH_W, 12), new Vector2(0, 0.5f));
            var dg = dash.gameObject.AddComponent<VertexGradient>();
            dg.horizontal = true; dg.a = U.Hex("#ff9f43"); dg.b = U.Hex("#ffc145"); dg.c = U.Hex("#ffe066");
            Shine(dashRT, 6);

            // ---------------------------------------------------------- banner
            bannerRT = Node("Banner", rootRT);
            Place(bannerRT, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900, 120));
            bannerGroup = bannerRT.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0; bannerGroup.blocksRaycasts = false;
            bannerGlow = Img(bannerRT, Gfx.Glow, Color.white);
            Place(bannerGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(820, 170));
            bannerTitle = Label(bannerRT, "", 68, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 18), display);
            bannerGrad = Gradient(bannerTitle, Color.white, Color.white, Color.white);
            AddOutline(bannerTitle, new Color(0, 0.1f, 0.2f, 0.7f), 2.5f);
            AddShadow(bannerTitle, new Color(0, 0.08f, 0.16f, 0.55f), new Vector2(0, -5));
            bannerSub = Label(bannerRT, "", 19, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -36));
            AddOutline(bannerSub, new Color(0, 20 / 255f, 40 / 255f, 0.8f), 1.2f);

            // ---------------------------------------------------------- menu
            menu = Overlay("Menu", out var mp, new Vector2(940, 600));
            menuCard = mp;
            Label(mp, "A LITTLE FISH. A VERY BIG OCEAN.", 12, U.Hex("#95d9cd"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(52, -30));
            titleRT = Node("Title", mp);
            Place(titleRT, new Vector2(0, 1), new Vector2(0, 1), titleOrigin, new Vector2(390, 196), new Vector2(0, 1));
            var t1 = Label(titleRT, "DEEP", 92, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), Vector2.zero, display);
            Gradient(t1, U.Hex("#eefcf8"), U.Hex("#c0eddf"), U.Hex("#7dd9c7"));
            AddShadow(t1, new Color(0, 0.09f, 0.14f, 0.7f), new Vector2(0, -4));
            var t2 = Label(titleRT, "FEAST", 92, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, -80), display);
            Gradient(t2, U.Hex("#fff1bf"), U.Hex("#f9cf7e"), U.Hex("#efa858"));
            AddShadow(t2, new Color(0, 0.09f, 0.14f, 0.7f), new Vector2(0, -4));
            Label(mp, "Small fish. Big appetite.", 23, U.Hex("#edf1df"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(54, -265), display);
            Label(mp, "Eat your way from fry to legend.", 17, U.Hex("#a9c5cc"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(54, -302));
            Button(mp, "LET'S SWIM", new Vector2(-230, -352), () => OnPlay?.Invoke(), 330);
            bestMenu = Label(mp, "PERSONAL BEST  0", 15, U.Hex("#f4d397"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(55, -438), display);
            var glow = Img(mp, Gfx.Glow, new Color(0.25f, 0.76f, 0.63f, 0.16f));
            Place(glow.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-239, -264), new Vector2(420, 330));
            MenuFish(mp, Data.Player, new Vector2(-230, -257), new Vector2(342, 208), 0);
            MenuFish(mp, Data.SpeciesMap["clown"], new Vector2(-355, -150), new Vector2(102, 72), -9);
            MenuFish(mp, Data.SpeciesMap["tang"], new Vector2(-103, -363), new Vector2(96, 72), 7);
            Label(mp, "FROM FRY TO LEGEND", 14, U.Hex("#9dd6c6"), TextAnchor.UpperCenter, new Vector2(1, 1), new Vector2(-238, -418), display);
            Guide(mp, "STEER", "Mouse / touch / WASD", -290);
            Guide(mp, "DASH", "Hold click / Space", 0);
            Guide(mp, "TAKE A BREAK", "P pauses  ·  M mutes", 290);

            // ---------------------------------------------------------- pause
            pause = Overlay("Pause", out var pp, new Vector2(600, 340));
            pauseCard = pp;
            Label(pp, "YOUR SWIM IS ON HOLD", 12, U.Hex("#a7dacc"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -28));
            Label(pp, "PAUSED", 62, U.Hex("#eff7e8"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -66), display);
            Label(pp, "Take a breath. The sharks will wait.", 18, U.Hex("#b8ced2"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -156));
            Button(pp, "KEEP SWIMMING", new Vector2(0, -221), () => OnResume?.Invoke(), 350);
            Label(pp, "P / ESC to resume", 13, U.Hex("#8baebc"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -305));
            pause.SetActive(false);

            // ---------------------------------------------------------- game over
            over = Overlay("Over", out var op, new Vector2(740, 560));
            overCard = op;
            Label(op, "END OF THIS SWIM", 12, U.Hex("#a6d2cb"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -24));
            overTitle = Label(op, "GOBBLED!", 56, U.Hex("#f2ecd9"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -53), display);
            AddShadow(overTitle, new Color(0, 0.09f, 0.14f, 0.7f), new Vector2(0, -3));
            newBest = Label(op, "", 15, U.Hex("#f4d397"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -127), display);
            sScore = Stat(op, "SCORE", new Vector2(-164, -174));
            sTier = Stat(op, "TOP TIER", new Vector2(164, -174));
            sEaten = Stat(op, "FISH EATEN", new Vector2(-164, -268));
            sTime = Stat(op, "SURVIVED", new Vector2(164, -268));
            Button(op, "ONE MORE SWIM", new Vector2(0, -382), () => OnPlay?.Invoke(), 360);
            bestOver = Label(op, "PERSONAL BEST  0", 15, U.Hex("#f4d397"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -473), display);
            Label(op, "Every legend starts as a fry.", 15, U.Hex("#91b6bf"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -517));
            over.SetActive(false);

            // ---------------------------------------------------------- corner buttons
            noteSprite = Icon(false); mutedSprite = Icon(true);
            var mb = Panel(rootRT, new Color(0.02f, 0.13f, 0.22f, 0.65f), 22);
            Place(mb.rectTransform, Vector2.right, Vector2.right, new Vector2(-16, 16), new Vector2(46, 46), new Vector2(1, 0));
            AddOutline(mb, new Color(0.63f, 0.94f, 1, 0.3f), 1.5f);
            mb.raycastTarget = true;
            var btn = mb.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.AddListener(() => OnMute?.Invoke());
            muteIcon = Img(mb.rectTransform, noteSprite, new Color(0.87f, 1, 1));
            Place(muteIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));

            var db = Panel(rootRT, new Color(1, 0.62f, 0.25f, 0.45f), 46);
            Place(db.rectTransform, Vector2.right, Vector2.right, new Vector2(-26, 80), new Vector2(92, 92), new Vector2(1, 0));
            var dbo = db.gameObject.AddComponent<Outline>();
            dbo.effectColor = new Color(1, 0.86f, 0.55f, 0.6f); dbo.effectDistance = new Vector2(2, -2);
            db.raycastTarget = true;
            Label(db.rectTransform, "DASH", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, display);
            dashHold = db.gameObject.AddComponent<HoldButton>();
            dashBtn = db.gameObject;
            dashBtn.SetActive(false);
        }

        // ------------------------------------------------------------------ public API
        public void Resize(float pxPerRef, float refW)
        {
            scaler.scaleFactor = pxPerRef;
            // narrow screens: drop the lives below the tier panel so they don't collide
            livesRT.anchoredPosition = refW < 960 ? new Vector2(-16, -86) : new Vector2(-16, -12);
            menuCard.parent.localScale = Vector3.one * Mathf.Min(1, (refW - 40) / 940);
            pauseCard.parent.localScale = Vector3.one * Mathf.Min(1, (refW - 40) / 600);
            overCard.parent.localScale = Vector3.one * Mathf.Min(1, (refW - 40) / 740);
        }
        public void UseCaptureCamera(Camera camera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 30000;
        }
        public Vector2 RefSize => rootRT.rect.size;

        public void ShowTouch(bool on) { if (dashBtn.activeSelf != on) dashBtn.SetActive(on); }

        public void SetMuted(bool m) => muteIcon.sprite = m ? mutedSprite : noteSprite;
        public void SetHabitat(string name, Color accent)
        {
            SetText(habitatName, name);
            habitatName.color = accent;
        }

        public void ShowMenu(int best)
        {
            bestMenu.text = "PERSONAL BEST  " + best.ToString("N0");
            menu.SetActive(true); over.SetActive(false); pause.SetActive(false);
            hudTarget = 0;
            FocusPrimary();
        }

        public void StartPlay()
        {
            menu.SetActive(false); over.SetActive(false); pause.SetActive(false);
            hudTarget = 1;
            growthInitialized = false;
            growthPulse = scorePulse = 0;
        }

        public void ShowPause(bool on) { pause.SetActive(on); if (on) FocusPrimary(); }

        public void ShowOver(int scoreV, string tier, int eaten, float time, bool isBest, int best)
        {
            overTitle.text = isBest && scoreV > 0 ? "A NEW LEGEND!" : "GOBBLED!";
            newBest.text = isBest && scoreV > 0 ? "NEW BEST!" : "";
            sScore.text = scoreV.ToString("N0");
            sTier.text = tier;
            sEaten.text = eaten.ToString();
            int s = Mathf.FloorToInt(time);
            sTime.text = $"{s / 60}:{s % 60:00}";
            bestOver.text = bestMenu.text = "PERSONAL BEST  " + best.ToString("N0");
            over.SetActive(true);
            hudTarget = 0;
            FocusPrimary();
        }

        public void UpdateHud(int scoreV, int comboV, bool comboOn, string tier, float prog, string next, int lives, float stamina, bool tired, int depthM)
        {
            SetText(score, scoreV.ToString("N0"));
            bool showCombo = comboOn && comboV > 1;
            if (comboPill.gameObject.activeSelf != showCombo) comboPill.gameObject.SetActive(showCombo);
            if (showCombo && SetText(combo, $"COMBO x{comboV}")) comboRT.sizeDelta = new Vector2(combo.preferredWidth + 30, 30);
            SetText(tierName, tier.ToUpperInvariant());
            SetActive(growth.gameObject, prog > 0.005f);
            targetGrowth = prog;
            if (!growthInitialized) { growthInitialized = true; shownGrowth = prog; }
            growthRT.sizeDelta = new Vector2(Mathf.Max(16, shownGrowth * BAR_W), 16);
            SetText(tierNext, next);
            int shown = Mathf.Min(lives, lifeIcons.Count);
            for (int i = 0; i < lifeIcons.Count; i++) lifeIcons[i].enabled = i < shown;
            livesBg.enabled = shown > 0;
            livesBg.rectTransform.sizeDelta = new Vector2(shown * LIFE_STEP + 18, 42);
            SetActive(dash.gameObject, stamina > 0.01f);
            dashRT.sizeDelta = new Vector2(Mathf.Max(12, stamina * DASH_W), 12);
            dash.color = tired ? new Color(0.42f, 0.49f, 0.53f) : Color.white;
            dash.GetComponent<VertexGradient>().enabled = !tired;
            SetText(dashLabel, tired ? "RECOVER" : "DASH");
            SetText(depth, $"DEPTH {depthM} m");
        }

        public void PulseGrowth(bool tierUp)
        {
            scorePulse = 0.18f;
            growthPulse = tierUp ? 0.75f : 0.22f;
        }

        public void Banner(string title, string sub, Color glow)
        {
            bannerTitle.text = title;
            bannerSub.text = sub ?? "";
            bannerGlow.color = U.WithA(glow, 0.32f);
            bannerGrad.b = Color.Lerp(Color.white, glow, 0.2f);
            bannerGrad.c = Color.Lerp(Color.white, glow, 0.65f);
            bannerTitle.SetVerticesDirty();
            bannerT = 0;
        }

        public void AddText(float x, float y, string text, Color col, float size)
        {
            var t = textPool.Count > 0 ? textPool.Pop() : MakeWorldText();
            t.gameObject.SetActive(true);
            t.text = text;
            texts.Add(new FloatText { x = x, y = y, life = 1.1f, max = 1.1f, size = size, t = t, col = col });
        }

        public void ClearTexts()
        {
            foreach (var f in texts) { f.t.gameObject.SetActive(false); textPool.Push(f.t); }
            texts.Clear();
        }

        public bool PointerOverUI(int pointerId = -1)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(pointerId);
        }

        /// toScreen maps world (y-down) to ref px (y-down from top-left).
        public void Tick(float dt, float rdt, Func<float, float, Vector2> toScreen, List<Alert> alerts, float time)
        {
            hudAlpha = Mathf.MoveTowards(hudAlpha, hudTarget, rdt * 2);
            hudGroup.alpha = hudAlpha;
            shownGrowth = Mathf.Lerp(shownGrowth, targetGrowth, 1 - Mathf.Exp(-rdt * 10));
            growthRT.sizeDelta = new Vector2(Mathf.Max(16, shownGrowth * BAR_W), 16);
            growthPulse = Mathf.Max(0, growthPulse - rdt);
            scorePulse = Mathf.Max(0, scorePulse - rdt);
            var tint = Color.Lerp(Color.white, U.Hex("#fff3be"), Mathf.Clamp01(growthPulse * 1.4f));
            if (growthGrad.tint != tint) { growthGrad.tint = tint; growth.SetVerticesDirty(); }
            score.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(scorePulse / 0.18f * Mathf.PI) * 0.07f);

            // title bob
            if (menu.activeSelf)
            {
                float b = Mathf.Sin(time * U.TAU / 3.2f);
                titleRT.anchoredPosition = titleOrigin + new Vector2(0, b * 3);
                titleRT.localRotation = Quaternion.Euler(0, 0, b * 0.3f);
                for (int i = 0; i < menuFish.Count; i++)
                {
                    var fish = menuFish[i];
                    fish.localRotation = Quaternion.Euler(0, 0, (i == 1 ? -9 : i == 2 ? 7 : 0) + Mathf.Sin(time * 1.2f + i) * 2);
                }
            }

            // banner animation (2.6 s)
            bannerT += rdt;
            float T = bannerT / 2.6f, alpha, sc, dy;
            if (T >= 1) { alpha = 0; sc = 1; dy = 0; }
            else if (T < 0.1f) { float k = U.Smooth(T / 0.1f); alpha = k; sc = Mathf.Lerp(0.7f, 1.06f, k); dy = Mathf.Lerp(-18, 0, k); }
            else if (T < 0.18f) { float k = U.Smooth((T - 0.1f) / 0.08f); alpha = 1; sc = Mathf.Lerp(1.06f, 1, k); dy = 0; }
            else if (T < 0.8f) { alpha = 1; sc = 1; dy = 0; }
            else { float k = U.Smooth((T - 0.8f) / 0.2f); alpha = 1 - k; sc = Mathf.Lerp(1, 0.98f, k); dy = 14 * k; }
            bannerGroup.alpha = alpha;
            bannerRT.localScale = new Vector3(sc, sc, 1);
            bannerRT.anchoredPosition = new Vector2(0, dy);

            // floating score texts
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                var f = texts[i];
                f.life -= dt;
                if (f.life <= 0) { f.t.gameObject.SetActive(false); textPool.Push(f.t); texts.RemoveAt(i); continue; }
                float a = Mathf.Clamp01(f.life / f.max);
                var s = toScreen(f.x, f.y);
                float rise = (1 - a) * 46;
                float k = a > 0.85f ? 1 + (a - 0.85f) * 3 : 1;
                f.t.fontSize = Mathf.RoundToInt(f.size * k);
                f.t.color = U.WithA(f.col, Mathf.Min(1, a * 1.6f));
                f.t.rectTransform.anchoredPosition = new Vector2(s.x, -(s.y - rise));
            }

            // "!" alerts + off-screen arrows
            int ai = 0, wi = 0;
            foreach (var al in alerts)
            {
                if (al.onScreen)
                {
                    if (ai >= alertPool.Count) alertPool.Add(MakeAlert());
                    var t = alertPool[ai++];
                    t.enabled = true;
                    t.rectTransform.anchoredPosition = new Vector2(al.screen.x, -(al.screen.y - al.bounce));
                }
                else
                {
                    if (wi >= arrowPool.Count) arrowPool.Add(MakeArrow());
                    var img = arrowPool[wi++];
                    img.enabled = true;
                    img.rectTransform.anchoredPosition = new Vector2(al.screen.x, -al.screen.y);
                    img.rectTransform.localRotation = Quaternion.Euler(0, 0, -al.angle * Mathf.Rad2Deg);
                    img.color = new Color(1, 45 / 255f, 74 / 255f, al.pulse);
                }
            }
            for (; ai < alertPool.Count; ai++) alertPool[ai].enabled = false;
            for (; wi < arrowPool.Count; wi++) arrowPool[wi].enabled = false;
        }

        // ------------------------------------------------------------------ builders
        static bool SetText(Text t, string v) { if (t.text == v) return false; t.text = v; return true; }
        static void SetActive(GameObject go, bool on) { if (go.activeSelf != on) go.SetActive(on); }

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        /// Text label; pass the display font for headings (it has no bold face, so it's used as-is).
        Text Label(Transform parent, string text, int size, Color col, TextAnchor align, Vector2 anchor, Vector2 pos, Font f = null)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            f ??= font;
            t.font = f; t.fontSize = size; t.fontStyle = f == font ? FontStyle.Bold : FontStyle.Normal; t.color = col; t.text = text;
            t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Vector2 pivot = align switch
            {
                TextAnchor.UpperLeft => new Vector2(0, 1),
                TextAnchor.LowerLeft => new Vector2(0, 0),
                TextAnchor.UpperCenter => new Vector2(0.5f, 1),
                TextAnchor.UpperRight => new Vector2(1, 1),
                _ => new Vector2(0.5f, 0.5f),
            };
            Place(rt, anchor, anchor, pos, new Vector2(10, size * 1.2f), pivot);
            return t;
        }

        static Image Img(Transform parent, Sprite s, Color c)
        {
            var rt = Node("Image", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s; img.color = c; img.raycastTarget = false;
            return img;
        }

        /// Rounded-rect panel; radius in ref px.
        static Image Panel(Transform parent, Color c, float radius)
        {
            var img = Img(parent, Gfx.Rounded, c);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 24f / Mathf.Max(1, radius);
            return img;
        }

        static void AddShadow(Graphic g, Color c, Vector2 d) { var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = c; s.effectDistance = d; }
        static void AddOutline(Graphic g, Color c, float d) { var s = g.gameObject.AddComponent<Outline>(); s.effectColor = c; s.effectDistance = new Vector2(d, -d); }
        static VertexGradient Gradient(Graphic g, Color a, Color b, Color c) { var v = g.gameObject.AddComponent<VertexGradient>(); v.a = a; v.b = b; v.c = c; return v; }

        /// Glossy highlight across the top half of a bar or button.
        static void Shine(RectTransform parent, float radius, float alpha = 0.32f)
        {
            var rt = Panel(parent, new Color(1, 1, 1, alpha), radius * 0.6f).rectTransform;
            rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Mathf.Min(4, radius * 0.5f), 0); rt.offsetMax = new Vector2(-Mathf.Min(4, radius * 0.5f), -2);
        }

        GameObject Overlay(string name, out RectTransform panel, Vector2 size)
        {
            var root = Node(name, rootRT); Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.sprite = Gfx.Vignette; dim.color = new Color(0, 0.05f, 0.12f, 1);
            dim.raycastTarget = true;
            var border = Panel(root, new Color(0.51f, 0.78f, 0.76f, 0.26f), 24);
            Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(3, 3));
            AddShadow(border, new Color(0, 0, 0, 0.25f), new Vector2(0, -10));
            var inner = Panel(border.rectTransform, new Color(0.03f, 0.12f, 0.19f, 0.97f), 23);
            Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var g = inner.gameObject.AddComponent<VertexGradient>();
            g.a = U.Hex("#153c43"); g.b = U.Hex("#102d37"); g.c = U.Hex("#0b2331"); g.mid = 0.4f;
            var accent = Panel(inner.rectTransform, U.Hex("#84d8c4", 0.55f), 2);
            Place(accent.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -5), new Vector2(-54, 2));
            panel = inner.rectTransform;
            return root.gameObject;
        }

        void Button(Transform parent, string label, Vector2 pos, Action onClick, float width = 0)
        {
            var img = Panel(parent, Color.white, 30);
            img.raycastTarget = true;
            img.gameObject.name = "Button_" + label;
            float w = width > 0 ? width : Mathf.Max(220, label.Length * 22 + 100);
            Place(img.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(w, 64), new Vector2(0.5f, 1));
            Gradient(img, U.Hex("#d0f3df"), U.Hex("#91ddc5"), U.Hex("#68c6b2")).multiplyGraphicColor = true;
            AddShadow(img, U.Hex("#163d41"), new Vector2(0, -4));
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cb = b.colors; cb.highlightedColor = Color.white; cb.normalColor = new Color(0.84f, 0.94f, 0.95f, 1); cb.pressedColor = new Color(0.65f, 0.82f, 0.85f, 1); cb.selectedColor = new Color(1, 0.94f, 0.75f, 1);
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            Shine(img.rectTransform, 30, 0.1f);
            Label(img.rectTransform, label, 28, U.Hex("#053040"), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), display);
        }

        Text Stat(Transform parent, string label, Vector2 pos)
        {
            var box = Panel(parent, new Color(0, 20 / 255f, 40 / 255f, 0.4f), 14);
            Place(box.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(300, 78), new Vector2(0.5f, 1));
            var v = Label(box.rectTransform, "0", 30, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -6), display);
            Label(box.rectTransform, label, 12, new Color(1, 1, 1, 0.65f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -51));
            return v;
        }

        void MenuFish(Transform parent, Species species, Vector2 pos, Vector2 size, float angle)
        {
            var fish = Img(parent, FishArt.Get(species).body, new Color(0.96f, 1, 0.95f));
            fish.preserveAspect = true;
            Place(fish.rectTransform, Vector2.one, Vector2.one, pos, size);
            fish.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
            menuFish.Add(fish.rectTransform);
        }

        void Guide(Transform parent, string heading, string description, float x)
        {
            var card = Panel(parent, new Color(0.03f, 0.1f, 0.15f, 0.6f), 12);
            Place(card.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -495), new Vector2(268, 68), new Vector2(0.5f, 1));
            Label(card.rectTransform, heading, 11, U.Hex("#94c7bd"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(16, -11));
            Label(card.rectTransform, description, 15, U.Hex("#d4e4df"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(16, -32));
        }

        // Native harness uses Unity's Submit event to verify the same button callbacks as the player.
        public void SubmitPrimary()
        {
            var screen = menu.activeSelf ? menu : pause.activeSelf ? pause : over.activeSelf ? over : null;
            var button = screen?.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button == null) throw new InvalidOperationException("No active primary menu button.");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        public string ActiveOverlay => menu.activeSelf ? "menu" : pause.activeSelf ? "pause" : over.activeSelf ? "over" : "none";

        void FocusPrimary()
        {
            var screen = menu.activeSelf ? menu : pause.activeSelf ? pause : over.activeSelf ? over : null;
            var button = screen?.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        Text MakeWorldText()
        {
            var t = Label(worldLayer, "", 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 1), Vector2.zero, display);
            AddOutline(t, new Color(0, 30 / 255f, 50 / 255f, 0.75f), 2f);
            AddShadow(t, new Color(0, 0.08f, 0.16f, 0.4f), new Vector2(0, -2));
            return t;
        }

        Text MakeAlert()
        {
            var t = Label(worldLayer, "!", 40, U.Hex("#ff2d4a"), TextAnchor.MiddleCenter, new Vector2(0, 1), Vector2.zero, display);
            AddOutline(t, Color.white, 2.5f);
            return t;
        }

        Image MakeArrow()
        {
            var img = Img(worldLayer, Gfx.Arrow, Color.red);
            Place(img.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(34, 28));
            AddOutline(img, new Color(1, 1, 1, 0.8f), 1.5f);
            return img;
        }

        /// Speaker icon, with sound waves or (muted) a small cross.
        static Sprite Icon(bool muted)
        {
            var r = new Raster(0, 0, 24, 24, 4);
            var m = r.Mask();
            r.Fill(new Path().Move(2.5f, 9).Line(6.5f, 9).Line(12, 4.5f).Line(12, 19.5f).Line(6.5f, 15).Line(2.5f, 15), m);
            if (!muted)
            {
                r.Stroke(ArcPts(12, 12, 4.4f, 0.85f), 1.9f, m);
                r.Stroke(ArcPts(12, 12, 8.2f, 0.9f), 1.9f, m);
            }
            else
            {
                r.Stroke(new[] { new Vector2(15.2f, 8.8f), new Vector2(21.2f, 15.2f) }, 2.3f, m);
                r.Stroke(new[] { new Vector2(21.2f, 8.8f), new Vector2(15.2f, 15.2f) }, 2.3f, m);
            }
            r.Paint(m, Color.white);
            return r.ToSprite(new Vector2(12, 12), 4);
        }

        static List<Vector2> ArcPts(float cx, float cy, float rad, float half, int n = 10)
        {
            var l = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = -half + 2 * half * i / n;
                l.Add(new Vector2(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad));
            }
            return l;
        }
    }
}
