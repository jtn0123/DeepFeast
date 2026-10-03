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
                Color orig = v.color;
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
        readonly Font font;
        readonly RectTransform rootRT, worldLayer;
        readonly CanvasGroup hudGroup, bannerGroup;
        readonly Text score, combo, tierName, tierNext, depth, bannerTitle, bannerSub;
        readonly Image growth, dash, bannerGlow;
        readonly RectTransform growthRT, dashRT, bannerRT, livesRT;
        readonly List<Image> lifeIcons = new List<Image>();
        readonly GameObject menu, pause, over, dashBtn;
        readonly Text bestMenu, bestOver, overTitle, newBest, sScore, sTier, sEaten, sTime;
        readonly RectTransform titleRT;
        readonly Image muteIcon;
        readonly Sprite noteSprite, mutedSprite;
        readonly HoldButton dashHold;
        float bannerT = 99;
        float hudAlpha, hudTarget;
        const float BAR_W = 440;

        sealed class FloatText { public float x, y, life, max, size; public Text t; public Color col; }
        readonly List<FloatText> texts = new List<FloatText>();
        readonly Stack<Text> textPool = new Stack<Text>();
        readonly List<Text> alertPool = new List<Text>();
        readonly List<Image> arrowPool = new List<Image>();

        public Hud()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            score = Label(hud, "0", 36, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(22, -12));
            AddShadow(score, new Color(0, 30 / 255f, 60 / 255f, 0.7f), new Vector2(0, -2));
            combo = Label(hud, "", 16, U.Hex("#ffd447"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(23, -56));
            AddOutline(combo, new Color(0.4f, 0.2f, 0, 0.6f), 1);

            tierName = Label(hud, "FRY", 13, U.Hex("#c4f7ff"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -12));
            AddShadow(tierName, new Color(0, 0.1f, 0.2f, 0.6f), new Vector2(0, -1));
            var bar = Panel(hud, new Color(0, 18 / 255f, 36 / 255f, 0.5f), 6);
            Place(bar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(BAR_W, 12));
            var barBorder = bar.gameObject.AddComponent<Outline>();
            barBorder.effectColor = new Color(180 / 255f, 240 / 255f, 1, 0.28f); barBorder.effectDistance = new Vector2(1, -1);
            growth = Panel(bar.rectTransform, Color.white, 6);
            growthRT = growth.rectTransform;
            Place(growthRT, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 12), new Vector2(0, 0.5f));
            var gg = growth.gameObject.AddComponent<VertexGradient>();
            gg.horizontal = true; gg.a = U.Hex("#2ef2c8"); gg.b = U.Hex("#4fd1ff"); gg.c = U.Hex("#ffd447"); gg.mid = 0.6f;
            tierNext = Label(hud, "", 11, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -52));

            livesRT = Node("Lives", hud);
            Place(livesRT, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -18), new Vector2(200, 20), new Vector2(1, 1));
            var lifeSprite = FishArt.LifeIcon();
            for (int i = 0; i < 5; i++)
            {
                var img = Img(livesRT, lifeSprite, Color.white);
                Place(img.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-i * 38, 0), new Vector2(32, 20), new Vector2(1, 1));
                AddShadow(img, new Color(0, 0, 0, 0.35f), new Vector2(0, -2));
                lifeIcons.Add(img);
            }

            depth = Label(hud, "DEPTH 0 m", 12, new Color(1, 1, 1, 0.8f), TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(22, 46));
            Label(hud, "DASH", 10, new Color(1, 1, 1, 0.75f), TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(22, 30));
            var dbar = Panel(hud, new Color(0, 18 / 255f, 36 / 255f, 0.5f), 4);
            Place(dbar.rectTransform, Vector2.zero, Vector2.zero, new Vector2(22, 18), new Vector2(170, 8), Vector2.zero);
            dash = Panel(dbar.rectTransform, Color.white, 4);
            dashRT = dash.rectTransform;
            Place(dashRT, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(170, 8), new Vector2(0, 0.5f));
            var dg = dash.gameObject.AddComponent<VertexGradient>();
            dg.horizontal = true; dg.a = U.Hex("#ff9f43"); dg.b = U.Hex("#ffc145"); dg.c = U.Hex("#ffd447");

            // ---------------------------------------------------------- banner
            bannerRT = Node("Banner", rootRT);
            Place(bannerRT, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900, 120));
            bannerGroup = bannerRT.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0; bannerGroup.blocksRaycasts = false;
            bannerGlow = Img(bannerRT, Gfx.Glow, Color.white);
            Place(bannerGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(760, 150));
            bannerTitle = Label(bannerRT, "", 60, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 16));
            AddOutline(bannerTitle, new Color(0, 30 / 255f, 50 / 255f, 0.5f), 1.5f);
            AddShadow(bannerTitle, new Color(0, 30 / 255f, 50 / 255f, 0.6f), new Vector2(0, -4));
            bannerSub = Label(bannerRT, "", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -34));
            AddOutline(bannerSub, new Color(0, 20 / 255f, 40 / 255f, 0.8f), 1.2f);

            // ---------------------------------------------------------- menu
            menu = Overlay("Menu", out var mp, new Vector2(560, 470));
            titleRT = Node("Title", mp);
            Place(titleRT, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(500, 200), new Vector2(0.5f, 1));
            var t1 = Label(titleRT, "DEEP", 104, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, 0));
            Gradient(t1, Color.white, U.Hex("#a6f4ff"), U.Hex("#31d4c8"));
            AddShadow(t1, new Color(0, 0.45f, 0.5f, 0.45f), new Vector2(0, -5));
            var t2 = Label(titleRT, "FEAST", 104, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -88));
            Gradient(t2, U.Hex("#fff1a8"), U.Hex("#ffc04d"), U.Hex("#ff7a3d"));
            AddShadow(t2, new Color(0.6f, 0.25f, 0, 0.45f), new Vector2(0, -5));
            Label(mp, "Eat smaller fish. Dodge bigger ones. Grow into a legend.", 17, new Color(1, 1, 1, 0.9f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -232));
            Button(mp, "PLAY", new Vector2(0, -278), () => OnPlay?.Invoke());
            Label(mp, "Mouse / Touch / WASD steer  ·  hold Click / Space to dash\nP pause  ·  M mute", 13, new Color(1, 1, 1, 0.8f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -372)).lineSpacing = 1.3f;
            bestMenu = Label(mp, "BEST  0", 13, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -422));

            // ---------------------------------------------------------- pause
            pause = Overlay("Pause", out var pp, new Vector2(480, 230));
            Label(pp, "Paused", 54, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -28));
            Label(pp, "Take a breath. The sharks will wait.", 17, new Color(1, 1, 1, 0.9f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -98));
            Button(pp, "RESUME", new Vector2(0, -136), () => OnResume?.Invoke());
            pause.SetActive(false);

            // ---------------------------------------------------------- game over
            over = Overlay("Over", out var op, new Vector2(500, 450));
            overTitle = Label(op, "Gobbled!", 56, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -26));
            AddShadow(overTitle, new Color(0, 0.1f, 0.2f, 0.6f), new Vector2(0, -3));
            newBest = Label(op, "", 16, U.Hex("#ffd447"), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -92));
            sScore = Stat(op, "SCORE", new Vector2(-104, -120));
            sTier = Stat(op, "TOP TIER", new Vector2(104, -120));
            sEaten = Stat(op, "FISH EATEN", new Vector2(-104, -194));
            sTime = Stat(op, "SURVIVED", new Vector2(104, -194));
            Button(op, "SWIM AGAIN", new Vector2(0, -290), () => OnPlay?.Invoke());
            bestOver = Label(op, "BEST  0", 13, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -400));
            over.SetActive(false);

            // ---------------------------------------------------------- corner buttons
            noteSprite = Icon(false); mutedSprite = Icon(true);
            var mb = Panel(rootRT, new Color(6 / 255f, 40 / 255f, 66 / 255f, 0.55f), 22);
            Place(mb.rectTransform, Vector2.zero + Vector2.right, Vector2.right, new Vector2(-16, 16), new Vector2(44, 44), new Vector2(1, 0));
            var mbo = mb.gameObject.AddComponent<Outline>();
            mbo.effectColor = new Color(160 / 255f, 240 / 255f, 1, 0.3f);
            var btn = mb.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => OnMute?.Invoke());
            muteIcon = Img(mb.rectTransform, noteSprite, new Color(0.87f, 1, 1));
            muteIcon.raycastTarget = false;
            Place(muteIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));

            var db = Panel(rootRT, new Color(1, 0.62f, 0.25f, 0.45f), 46);
            Place(db.rectTransform, Vector2.right, Vector2.right, new Vector2(-26, 80), new Vector2(92, 92), new Vector2(1, 0));
            var dbo = db.gameObject.AddComponent<Outline>();
            dbo.effectColor = new Color(1, 0.86f, 0.55f, 0.6f); dbo.effectDistance = new Vector2(2, -2);
            Label(db.rectTransform, "DASH", 14, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero).raycastTarget = false;
            dashHold = db.gameObject.AddComponent<HoldButton>();
            dashBtn = db.gameObject;
            dashBtn.SetActive(false);
        }

        // ------------------------------------------------------------------ public API
        public void Resize(float pxPerRef) => scaler.scaleFactor = pxPerRef;
        public Vector2 RefSize => rootRT.rect.size;

        public void ShowTouch(bool on) { if (dashBtn.activeSelf != on) dashBtn.SetActive(on); }

        public void SetMuted(bool m) => muteIcon.sprite = m ? mutedSprite : noteSprite;

        public void ShowMenu(int best)
        {
            bestMenu.text = "BEST  " + best.ToString("N0");
            menu.SetActive(true); over.SetActive(false); pause.SetActive(false);
            hudTarget = 0;
        }

        public void StartPlay()
        {
            menu.SetActive(false); over.SetActive(false); pause.SetActive(false);
            hudTarget = 1;
        }

        public void ShowPause(bool on) => pause.SetActive(on);

        public void ShowOver(int scoreV, string tier, int eaten, float time, bool isBest, int best)
        {
            overTitle.text = U.Pick(new[] { "Gobbled!", "Chomped!", "Fish Food!", "Swallowed!" });
            newBest.text = isBest && scoreV > 0 ? "NEW BEST!" : "";
            sScore.text = scoreV.ToString("N0");
            sTier.text = tier;
            sEaten.text = eaten.ToString();
            int s = Mathf.FloorToInt(time);
            sTime.text = $"{s / 60}:{s % 60:00}";
            bestOver.text = bestMenu.text = "BEST  " + best.ToString("N0");
            over.SetActive(true);
            hudTarget = 0;
        }

        public void UpdateHud(int scoreV, int comboV, bool comboOn, string tier, float prog, string next, int lives, float stamina, bool tired, int depthM)
        {
            SetText(score, scoreV.ToString("N0"));
            SetText(combo, comboOn && comboV > 1 ? $"COMBO ×{comboV}" : "");
            SetText(tierName, Spaced(tier.ToUpperInvariant()));
            growthRT.sizeDelta = new Vector2(Mathf.Max(0, prog) * BAR_W, 12);
            growth.enabled = prog > 0.005f;
            SetText(tierNext, next);
            for (int i = 0; i < lifeIcons.Count; i++) lifeIcons[i].enabled = i < lives;
            dashRT.sizeDelta = new Vector2(Mathf.Max(0, stamina) * 170, 8);
            dash.enabled = stamina > 0.01f;
            dash.color = tired ? new Color(0.42f, 0.49f, 0.53f) : Color.white;
            dash.GetComponent<VertexGradient>().enabled = !tired;
            SetText(depth, $"DEPTH {depthM} m");
        }

        public void Banner(string title, string sub, Color glow)
        {
            bannerTitle.text = title;
            bannerSub.text = sub ?? "";
            bannerGlow.color = U.WithA(glow, 0.32f);
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

            // title bob
            if (menu.activeSelf)
            {
                float b = Mathf.Sin(time * U.TAU / 3.2f);
                titleRT.anchoredPosition = new Vector2(0, -24 + Mathf.Max(0, b) * 7);
                titleRT.localRotation = Quaternion.Euler(0, 0, Mathf.Max(0, b) * 1);
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
        static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(s[i]); }
            return sb.ToString();
        }

        static void SetText(Text t, string v) { if (t.text != v) t.text = v; }

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

        Text Label(Transform parent, string text, int size, Color col, TextAnchor align, Vector2 anchor, Vector2 pos)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.color = col; t.text = text;
            t.alignment = align; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Vector2 pivot = align switch
            {
                TextAnchor.UpperLeft => new Vector2(0, 1),
                TextAnchor.LowerLeft => new Vector2(0, 0),
                TextAnchor.UpperCenter => new Vector2(0.5f, 1),
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
        static void Gradient(Graphic g, Color a, Color b, Color c) { var v = g.gameObject.AddComponent<VertexGradient>(); v.a = a; v.b = b; v.c = c; }

        GameObject Overlay(string name, out RectTransform panel, Vector2 size)
        {
            var root = Node(name, rootRT); Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.sprite = Gfx.Vignette; dim.color = new Color(0, 0.05f, 0.12f, 1);
            dim.raycastTarget = true;
            var border = Panel(root, new Color(160 / 255f, 240 / 255f, 1, 0.26f), 30);
            Place(border.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size + new Vector2(3, 3));
            AddShadow(border, new Color(0, 0, 0, 0.25f), new Vector2(0, -10));
            var inner = Panel(border.rectTransform, new Color(6 / 255f, 40 / 255f, 66 / 255f, 0.82f), 29);
            Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var g = inner.gameObject.AddComponent<VertexGradient>();
            g.a = new Color(0.16f, 0.36f, 0.5f, 1); g.b = new Color(0.06f, 0.2f, 0.32f, 1); g.c = new Color(0.03f, 0.13f, 0.23f, 1); g.mid = 0.4f;
            panel = inner.rectTransform;
            return root.gameObject;
        }

        void Button(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var img = Panel(parent, Color.white, 30);
            img.raycastTarget = true;
            float w = Mathf.Max(220, label.Length * 22 + 100);
            Place(img.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(w, 58), new Vector2(0.5f, 1));
            Gradient(img, U.Hex("#8dfde6"), U.Hex("#52e0cf"), U.Hex("#21c6b6"));
            AddShadow(img, U.Hex("#0f7f7a"), new Vector2(0, -6));
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1, 1, 1, 1); cb.normalColor = new Color(0.94f, 0.94f, 0.94f, 1); cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1); cb.selectedColor = cb.normalColor;
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            var t = Label(img.rectTransform, label, 24, U.Hex("#053040"), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 1));
            t.raycastTarget = false;
        }

        Text Stat(Transform parent, string label, Vector2 pos)
        {
            var box = Panel(parent, new Color(0, 20 / 255f, 40 / 255f, 0.4f), 14);
            Place(box.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(196, 64), new Vector2(0.5f, 1));
            var v = Label(box.rectTransform, "0", 26, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -8));
            Label(box.rectTransform, label, 11, new Color(1, 1, 1, 0.7f), TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -42));
            return v;
        }

        Text MakeWorldText()
        {
            var t = Label(worldLayer, "", 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 1), Vector2.zero);
            AddOutline(t, new Color(0, 30 / 255f, 50 / 255f, 0.7f), 2f);
            return t;
        }

        Text MakeAlert()
        {
            var t = Label(worldLayer, "!", 32, U.Hex("#ff2d4a"), TextAnchor.MiddleCenter, new Vector2(0, 1), Vector2.zero);
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

        static Sprite Icon(bool muted)
        {
            var r = new Raster(0, 0, 24, 24, 4);
            var m = r.Mask();
            if (!muted)
            {
                r.Ellipse(8, 18, 4, 3.2f, -0.35f, m);
                r.Fill(new Path().Move(10.5f, 17.5f).Line(12.2f, 17.5f).Line(12.2f, 4).Line(10.5f, 4), m);
                r.Stroke(Raster.QuadPts(new Vector2(11.5f, 4.5f), new Vector2(17, 6), new Vector2(18, 11)), 2f, m);
            }
            else
            {
                r.Stroke(new[] { new Vector2(5, 5), new Vector2(19, 19) }, 2.6f, m);
                r.Stroke(new[] { new Vector2(19, 5), new Vector2(5, 19) }, 2.6f, m);
            }
            r.Paint(m, Color.white);
            return r.ToSprite(new Vector2(12, 12), 4);
        }
    }
}
