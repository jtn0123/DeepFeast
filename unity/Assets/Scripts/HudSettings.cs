using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepFeast
{
    // One settings row. Left and right step its value, submit steps forward, and up and down move
    // between rows as usual.
    public sealed class OptionRow : Selectable, ISubmitHandler
    {
        public Action<int> onStep;
        public Action onFocus;
        /// Selected, hovered.
        public Action<bool, bool> onState;

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left || e.moveDir == MoveDirection.Right)
            {
                if (IsInteractable()) onStep?.Invoke(e.moveDir == MoveDirection.Right ? 1 : -1);
                e.Use();
                return;
            }
            base.OnMove(e);
        }

        public void OnSubmit(BaseEventData e) { if (IsInteractable()) onStep?.Invoke(1); }
        public override void OnSelect(BaseEventData e) { base.OnSelect(e); onFocus?.Invoke(); }
        public override void OnPointerEnter(PointerEventData e) { base.OnPointerEnter(e); onFocus?.Invoke(); }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            onState?.Invoke(state == SelectionState.Selected || state == SelectionState.Pressed, state == SelectionState.Highlighted);
        }
    }

    // The settings card: display, graphics and sound pages of option rows, reachable from the menu
    // and the pause card. Every row works the same with mouse, keyboard and gamepad.
    public sealed partial class Hud
    {
        sealed class Option
        {
            public string key, label;
            public Func<string> value, help;
            public Func<float> fill;
            public Func<bool> enabled;
            public Action<int> step;
            public bool display;
            public OptionRow row;
            public Image bg, accent, fillBar;
            public Text name, shown;
            public CanvasGroup group;
        }

        sealed class SettingsPage
        {
            public RectTransform root;
            public UnityEngine.UI.Button tab;
            public Image tabImage;
            public VertexGradient tabFill;
            public Text tabLabel;
            public readonly List<Option> options = new List<Option>();
        }

        const float RowW = 952, RowH = 56, RowStep = 64, RowTop = -176;
        static readonly bool Desktop = Application.platform != RuntimePlatform.WebGLPlayer;
        static readonly Color RowIdle = new Color(0.02f, 0.1f, 0.16f, 0.55f), RowHover = new Color(0.06f, 0.2f, 0.26f, 0.8f), RowFocus = new Color(0.09f, 0.27f, 0.3f, 0.95f);
        GameObject settings, settingsReturn, settingsOpener;
        RectTransform settingsCard;
        Text settingsHelp, settingsTabsHint, fpsText;
        Selectable settingsDefaults, settingsBack;
        readonly List<SettingsPage> pages = new List<SettingsPage>();
        int page;
        Option helpFor, lastStepped;
        float settingsRefresh, lastStepTime, fpsTime, fpsWorst;
        int stepStreak, fpsFrames;

        public bool SettingsOpen => settings.activeSelf;
        static SettingsData Opt => GameSettings.Data;
        static readonly string[] GlowNames = { "Off", "Subtle", "Medium", "Strong" }, GradeNames = { "Off", "Subtle", "Rich" },
            DetailNames = { "Standard", "Sharp", "Ultra" }, DensityNames = { "Low", "Normal", "High" };
        static float Hz => GameSettings.RefreshRate;

        void BuildSettings()
        {
            settings = Overlay("Settings", out var card, new Vector2(1040, 720));
            settingsCard = card;
            var title = Label(card, "SETTINGS", 46, U.Hex("#f2ecd9"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(44, -24), display);
            AddShadow(title, new Color(0, 0.09f, 0.14f, 0.7f), new Vector2(0, -3));
            Label(card, "YOUR OCEAN, YOUR WAY", 15, U.Hex("#a6d2cb"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(46, -80));
            settingsTabsHint = Label(card, "", 15, U.Hex("#8baebc"), TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-44, -124));

            AddPage(card, "DISPLAY", new[]
            {
                new Option
                {
                    key = "displayMode", label = "DISPLAY MODE", display = true, enabled = () => Desktop,
                    value = () => ModeName(GameSettings.Modes[Opt.displayMode]),
                    step = dir => Opt.displayMode = (Opt.displayMode + dir + GameSettings.Modes.Length) % GameSettings.Modes.Length,
                    help = () => "Fullscreen fills the display; windowed sits on the desktop. Changes apply a moment after you stop.",
                },
                new Option
                {
                    key = "resolution", label = "RESOLUTION", display = true, enabled = () => Desktop,
                    value = ResolutionName, step = StepResolution,
                    help = () => "The window's size, or the picture's size in fullscreen. Native is the sharpest.",
                },
                new Option
                {
                    key = "renderScale", label = "RENDER SCALE",
                    value = () => Opt.renderScale + "%", step = dir => Opt.renderScale = Step(GameSettings.RenderScales, Opt.renderScale, dir),
                    help = () => $"The ocean is drawn at {Presenter.SceneSize.x} × {Presenter.SceneSize.y}, then fitted to the screen. Above 100% is sharper; below runs faster.",
                },
                new Option
                {
                    key = "vSync", label = "VSYNC", enabled = () => Desktop,
                    value = () => Opt.vSync ? $"On  ·  {Hz:0} Hz" : "Off", step = _ => Opt.vSync = !Opt.vSync,
                    help = () => "Paces frames to the display's refresh for smooth motion without tearing. Turn it off to choose a frame-rate cap.",
                },
                new Option
                {
                    key = "maxFps", label = "MAX FRAME RATE", enabled = () => Desktop && !Opt.vSync,
                    value = () => Opt.vSync ? $"VSync  ·  {Hz:0}" : Opt.maxFps == 0 ? "Unlimited" : Opt.maxFps < 0 ? "Custom" : Opt.maxFps + " FPS",
                    step = dir => Opt.maxFps = Step(GameSettings.FpsPresets, Opt.maxFps, dir),
                    help = () => Opt.vSync ? "VSync sets the pace. Turn VSync off to choose a cap, or none at all." : "The most frames drawn each second. Unlimited draws as many as the Mac can.",
                },
                new Option
                {
                    key = "customFps", label = "CUSTOM FRAME RATE", enabled = () => Desktop && !Opt.vSync && Opt.maxFps < 0,
                    value = () => Opt.customFps + " FPS", step = dir => Opt.customFps += dir * (stepStreak < 5 ? 1 : stepStreak < 15 ? 5 : 10),
                    help = () => $"Any cap from {GameSettings.MinFps} to {GameSettings.MaxCustomFps} frames a second. Hold left or right to step faster.",
                },
                new Option
                {
                    key = "showFps", label = "SHOW FPS",
                    value = () => Opt.showFps ? "On" : "Off", step = _ => Opt.showFps = !Opt.showFps,
                    help = () => "Frames per second and frame time in the corner of the screen.",
                },
            });
            AddPage(card, "GRAPHICS", new[]
            {
                new Option
                {
                    key = "msaa", label = "ANTI-ALIASING",
                    value = () => Opt.msaa <= 1 ? "Off" : Opt.msaa + "× MSAA", step = dir => Opt.msaa = Step(GameSettings.MsaaLevels, Opt.msaa, dir),
                    help = () => Opt.msaa > Presenter.ActiveMsaa
                        ? $"Smooths the edges of fish, kelp and reef. At this render scale it runs at {Presenter.ActiveMsaa}× to save memory."
                        : "Smooths the edges of fish, kelp and reef. Higher levels cost more GPU time.",
                },
                new Option
                {
                    key = "glow", label = "GLOW",
                    value = () => GlowNames[Opt.glow], step = dir => Opt.glow = Mathf.Clamp(Opt.glow + dir, 0, GlowNames.Length - 1),
                    help = () => "Sunlight, pearls, jellies and lanterns bleed a soft light into the water around them.",
                },
                new Option
                {
                    key = "grading", label = "COLOR GRADING",
                    value = () => GradeNames[Opt.grading], step = dir => Opt.grading = Mathf.Clamp(Opt.grading + dir, 0, GradeNames.Length - 1),
                    help = () => "A colour mood for each habitat: warm reef shallows, golden kelp and the cold blue deep.",
                },
                new Option
                {
                    key = "ripples", label = "WATER RIPPLES",
                    value = () => Opt.ripples ? "On" : "Off", step = _ => Opt.ripples = !Opt.ripples,
                    help = () => "Dashing and taking a hit send rings through the water that bend the scene behind them.",
                },
                new Option
                {
                    key = "spriteDetail", label = "SPRITE DETAIL",
                    value = () => DetailNames[Opt.spriteDetail - 1] + (Opt.spriteDetail == GameSettings.ActiveSpriteDetail ? $"  ·  {Opt.spriteDetail}×" : "  ·  restart"),
                    step = dir => Opt.spriteDetail = Mathf.Clamp(Opt.spriteDetail + dir, 1, GameSettings.MaxSpriteDetail),
                    help = () => Opt.spriteDetail == GameSettings.ActiveSpriteDetail
                        ? "How finely the game paints its own effects: pearls, bubbles, sparks and glows. Sharper uses more memory."
                        : $"Takes effect the next time Deep Feast starts. This run is painted at {GameSettings.ActiveSpriteDetail}×.",
                },
                new Option
                {
                    key = "effectDensity", label = "EFFECTS DETAIL",
                    value = () => DensityNames[Opt.effectDensity],
                    step = dir => Opt.effectDensity = Mathf.Clamp(Opt.effectDensity + dir, 0, DensityNames.Length - 1),
                    help = () => "How much marine snow drifts by, and how many bubbles and sparks fly when a fish is eaten.",
                },
                new Option
                {
                    key = "shadows", label = "FISH SHADOWS",
                    value = () => Opt.shadows ? "On" : "Off", step = _ => Opt.shadows = !Opt.shadows,
                    help = () => "Fish near the seabed cast soft shadows on the sunlit sand.",
                },
            });
            AddPage(card, "SOUND", new[]
            {
                Percent("master", "MASTER VOLUME", () => Opt.master, v => Opt.master = v, 10, 100, () => $"Everything you hear. {MutePrompt} mutes at any time."),
                Percent("music", "MUSIC VOLUME", () => Opt.music, v => Opt.music = v, 10, 100, () => "The music that follows you from the reef down to the abyss."),
                Percent("effects", "EFFECTS VOLUME", () => Opt.effects, v => Opt.effects = v, 10, 100, () => "Chomps, dashes, pearls and warnings."),
                Percent("ambience", "AMBIENCE VOLUME", () => Opt.ambience, v => Opt.ambience = v, 10, 100, () => "The low hum of the ocean all around you."),
            });
            AddPage(card, "ACCESSIBILITY", new[]
            {
                new Option
                {
                    key = "textSize", label = "TEXT SIZE",
                    value = () => Opt.textSize + "%", step = dir => Opt.textSize = Step(GameSettings.TextSizes, Opt.textSize, dir),
                    help = () => AppliedTextSize < Opt.textSize
                        ? $"Makes the HUD, banners and menus larger. This screen has room for {AppliedTextSize}%; a wider window fits more."
                        : "Makes the HUD, banners and menus larger, as far as the screen has room.",
                },
                new Option
                {
                    key = "reduceFlashing", label = "REDUCE FLASHING",
                    value = () => Opt.reduceFlashing ? "On" : "Off", step = _ => Opt.reduceFlashing = !Opt.reduceFlashing,
                    help = () => "Slows every blink and flicker (a hero just back from a bite, a fading shield, pearl or power) to a gentle beat.",
                },
                Percent("shake", "SCREEN SHAKE", () => Opt.shake, v => Opt.shake = v, 25, 150, () => "How hard the screen shakes when you are hit or take a big bite."),
                new Option
                {
                    key = "dashToggle", label = "DASH",
                    value = () => Opt.dashToggle ? "Toggle" : "Hold", step = _ => Opt.dashToggle = !Opt.dashToggle,
                    help = () => Opt.dashToggle ? "Press dash once to start, again to stop. Running out of breath or holding still stops it too." : "Dash for as long as you hold the button.",
                },
            });

            for (int i = 0; i < pages.Count; i++)
            {
                int index = i;
                var b = Panel(card, Color.white, 25);
                b.raycastTarget = true;
                b.gameObject.name = "Button_" + pages[i].tabLabel.text;
                pages[i].tabLabel.rectTransform.SetParent(b.rectTransform, false);
                Place(b.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-386 + i * 190, -104), new Vector2(180, 50), new Vector2(0.5f, 1));
                pages[i].tabImage = b;
                pages[i].tabFill = Gradient(b, Color.white, Color.white, Color.white);
                pages[i].tabFill.multiplyGraphicColor = true;
                var tab = pages[i].tab = b.gameObject.AddComponent<UnityEngine.UI.Button>();
                var cb = tab.colors; cb.normalColor = new Color(0.84f, 0.94f, 0.95f, 1); cb.highlightedColor = Color.white; cb.selectedColor = new Color(1, 0.94f, 0.75f, 1); tab.colors = cb;
                tab.onClick.AddListener(() => ShowPage(index));
                // Arriving on a tab by keyboard or pad shows its page; hovering one does not.
                var relay = b.gameObject.AddComponent<FocusRelay>();
                relay.hover = false;
                relay.onFocus = () => ShowPage(index);
            }

            settingsHelp = Label(card, "", 15, U.Hex("#b8ced2"), TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(46, -648));
            settingsHelp.horizontalOverflow = HorizontalWrapMode.Wrap;
            settingsHelp.rectTransform.sizeDelta = new Vector2(500, 50);
            Button(card, "DEFAULTS", new Vector2(156, -636), ResetSettings, 210, true);
            Button(card, "BACK", new Vector2(376, -636), CloseSettings, 200);
            settingsDefaults = card.Find("Button_DEFAULTS").GetComponent<Selectable>();
            settingsBack = card.Find("Button_BACK").GetComponent<Selectable>();
            GameSettings.Changed += RefreshSettings;
            ShowPage(0);
            settings.SetActive(false);
        }

        Option Percent(string key, string label, Func<int> get, Action<int> set, int step, int max, Func<string> help) => new Option
        {
            key = key, label = label, help = help,
            value = () => get() == 0 ? "Off" : get() + "%",
            fill = () => get() / (float)max,
            step = dir => set(Mathf.Clamp(get() + dir * step, 0, max)),
        };

        void AddPage(RectTransform card, string name, Option[] options)
        {
            var p = new SettingsPage { root = Node("Page_" + name, card) };
            Stretch(p.root);
            p.tabLabel = Label(card, name, 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 1), display);
            for (int i = 0; i < options.Length; i++) AddRow(p.root, options[i], RowTop - i * RowStep);
            p.options.AddRange(options);
            pages.Add(p);
        }

        void AddRow(RectTransform parent, Option o, float y)
        {
            o.bg = Panel(parent, RowIdle, 14);
            o.bg.raycastTarget = true;
            o.bg.gameObject.name = "Row_" + o.key;
            var rt = o.bg.rectTransform;
            Place(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(RowW, RowH), new Vector2(0.5f, 1));
            o.group = o.bg.gameObject.AddComponent<CanvasGroup>();
            o.accent = Panel(rt, U.Hex("#ffd447"), 3);
            Place(o.accent.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(5, 34), new Vector2(0, 0.5f));
            o.name = Label(rt, o.label, 21, U.Hex("#e8f4ee"), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(28, 1), display);
            o.name.rectTransform.pivot = new Vector2(0, 0.5f);

            o.shown = Label(rt, "", 19, Color.white, TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(-180, o.fill != null ? 5 : 1), display);
            if (o.fill != null)
            {
                var track = Panel(rt, TrackCol, 2);
                Place(track.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-180, -15), new Vector2(180, 5));
                o.fillBar = Panel(track.rectTransform, U.Hex("#58dcc2"), 2);
                Place(o.fillBar.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(180, 5), new Vector2(0, 0.5f));
            }
            o.row = o.bg.gameObject.AddComponent<OptionRow>();
            o.row.transition = Selectable.Transition.None;
            o.row.onStep = dir => StepOption(o, dir);
            o.row.onFocus = () => ShowHelp(o);
            o.row.onState = (selected, hovered) =>
            {
                o.bg.color = selected ? RowFocus : hovered ? RowHover : RowIdle;
                o.accent.enabled = selected;
                o.shown.color = selected ? U.Hex("#ffd447") : Color.white;
            };
            o.row.onState(false, false);
            ArrowButton(rt, o, -1, new Vector2(-322, 0));
            ArrowButton(rt, o, 1, new Vector2(-38, 0));
        }

        void ArrowButton(RectTransform row, Option o, int dir, Vector2 pos)
        {
            var b = Panel(row, new Color(1, 1, 1, 0.18f), 12);
            b.raycastTarget = true;
            Place(b.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), pos, new Vector2(46, 40));
            var arrow = Img(b.rectTransform, Gfx.Arrow, U.Hex("#9fe3d2"));
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 17));
            if (dir < 0) arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            var button = b.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var cb = button.colors; cb.normalColor = new Color(1, 1, 1, 0.5f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1); button.colors = cb;
            button.onClick.AddListener(() =>
            {
                // Clicking an arrow keeps its row in focus, as stepping by keyboard would.
                if (o.row.IsInteractable()) EventSystem.current?.SetSelectedGameObject(o.row.gameObject);
                StepOption(o, dir);
            });
        }

        void StepOption(Option o, int dir)
        {
            if (!o.row.IsInteractable()) return;
            // A held arrow or stick repeats every tenth of a second; long holds take bigger steps.
            float now = Time.unscaledTime;
            stepStreak = o == lastStepped && now - lastStepTime < 0.3f ? stepStreak + 1 : 0;
            lastStepped = o; lastStepTime = now;
            o.step(dir);
            GameSettings.Commit(o.display);
            ShowHelp(o);
        }

        static int Step(int[] list, int current, int dir)
        {
            int i = Mathf.Max(0, Array.IndexOf(list, current));
            return list[Mathf.Clamp(i + dir, 0, list.Length - 1)];
        }

        static string ModeName(FullScreenMode mode) => mode switch
        {
            FullScreenMode.Windowed => "Windowed",
            FullScreenMode.ExclusiveFullScreen => "Exclusive",
            FullScreenMode.MaximizedWindow => "Maximized",
            _ => "Fullscreen",
        };

        static string ResolutionName()
        {
            var mode = GameSettings.Modes[Opt.displayMode];
            var size = GameSettings.TargetSize(mode);
            string res = $"{size.x} × {size.y}";
            if (Opt.width > 0) return res;
            return (mode == FullScreenMode.Windowed ? "Default  " : "Native  ") + res;
        }

        static void StepResolution(int dir)
        {
            var list = GameSettings.Resolutions();
            int i = Mathf.Max(0, list.IndexOf(new Vector2Int(Opt.width, Opt.height)));
            var next = list[Mathf.Clamp(i + dir, 0, list.Count - 1)];
            Opt.width = next.x; Opt.height = next.y;
        }

        void ShowHelp(Option o)
        {
            helpFor = o;
            SetText(settingsHelp, o.help());
        }

        void ShowPage(int index)
        {
            page = index;
            for (int i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                bool on = i == index;
                SetActive(p.root.gameObject, on);
                var f = p.tabFill;
                if (on) { f.a = U.Hex("#d0f3df"); f.b = U.Hex("#91ddc5"); f.c = U.Hex("#68c6b2"); }
                else { f.a = U.Hex("#3d808a"); f.b = U.Hex("#2a6670"); f.c = U.Hex("#1d515b"); }
                p.tabImage.SetVerticesDirty();
                p.tabLabel.color = on ? U.Hex("#053040") : U.Hex("#e6fbf4");
            }
            RefreshSettings();
            if (helpFor == null || !pages[page].options.Contains(helpFor)) ShowHelp(pages[page].options[0]);
        }

        /// Steps to the next or previous page (bumpers or Tab) and focuses its first row.
        public void SettingsTab(int dir)
        {
            if (!SettingsOpen) return;
            ShowPage((page + dir + pages.Count) % pages.Count);
            FocusFirstRow();
        }

        void FocusFirstRow()
        {
            foreach (var o in pages[page].options)
                if (o.row.IsInteractable()) { EventSystem.current?.SetSelectedGameObject(o.row.gameObject); return; }
            EventSystem.current?.SetSelectedGameObject(settingsBack.gameObject);
        }

        void RefreshSettings()
        {
            if (settings == null) return;
            settingsRefresh = 0.25f;
            var live = new List<Selectable>();
            foreach (var o in pages[page].options)
            {
                bool on = o.enabled == null || o.enabled();
                if (o.row.interactable != on) o.row.interactable = on;
                o.group.alpha = on ? 1 : 0.42f;
                SetText(o.shown, o.value());
                if (o.fillBar != null) o.fillBar.rectTransform.sizeDelta = new Vector2(Mathf.Max(5, o.fill() * 180), 5);
                if (on) live.Add(o.row);
            }
            if (helpFor != null) SetText(settingsHelp, helpFor.help());
            SetActive(fpsText.gameObject, Opt.showFps);

            // Up and down walk the rows that can change, between the tabs above and the buttons below.
            var tab = pages[page].tab;
            for (int i = 0; i < live.Count; i++)
                live[i].navigation = Nav(null, null, i > 0 ? live[i - 1] : tab, i < live.Count - 1 ? live[i + 1] : settingsDefaults);
            for (int i = 0; i < pages.Count; i++)
                pages[i].tab.navigation = Nav(i > 0 ? pages[i - 1].tab : null, i < pages.Count - 1 ? pages[i + 1].tab : null, null, live.Count > 0 ? live[0] : settingsDefaults);
            var last = live.Count > 0 ? live[live.Count - 1] : tab;
            settingsDefaults.navigation = Nav(null, settingsBack, last, null);
            settingsBack.navigation = Nav(settingsDefaults, null, last, null);
        }

        static Navigation Nav(Selectable left, Selectable right, Selectable up, Selectable down) => new Navigation
        {
            mode = Navigation.Mode.Explicit, selectOnLeft = left, selectOnRight = right, selectOnUp = up, selectOnDown = down,
        };

        // Defaults leave the window alone; only the player can choose to resize it.
        void ResetSettings()
        {
            int mode = Opt.displayMode, w = Opt.width, h = Opt.height;
            GameSettings.ResetToDefaults();
            Opt.displayMode = mode; Opt.width = w; Opt.height = h;
            GameSettings.Commit();
        }

        /// The settings card covers the menu or pause card and returns to it, focusing the button that opened it.
        public void OpenSettings()
        {
            if (SettingsOpen) return;
            settingsOpener = EventSystem.current?.currentSelectedGameObject;
            settingsReturn = menu.activeSelf ? menu : pause.activeSelf ? pause : over.activeSelf ? over : null;
            if (settingsReturn != null) settingsReturn.SetActive(false);
            GameSettings.SyncFromScreen();
            settings.SetActive(true);
            ShowPage(page);
            FocusFirstRow();
        }

        public void CloseSettings()
        {
            if (!SettingsOpen) return;
            settings.SetActive(false);
            if (settingsReturn != null) settingsReturn.SetActive(true);
            settingsReturn = null;
            if (settingsOpener != null && settingsOpener.activeInHierarchy) EventSystem.current?.SetSelectedGameObject(settingsOpener);
            else FocusPrimary();
            settingsOpener = null;
        }

        /// Native harness: focus a row by its setting name, showing its page.
        public void FocusSetting(string key)
        {
            for (int i = 0; i < pages.Count; i++)
                foreach (var o in pages[i].options)
                    if (o.key == key)
                    {
                        ShowPage(i);
                        EventSystem.current.SetSelectedGameObject(o.row.gameObject);
                        return;
                    }
            throw new InvalidOperationException("No setting " + key);
        }

        /// Native harness: send a move to the focused control, as an arrow key or d-pad press would.
        public void MoveFocused(MoveDirection dir)
        {
            var es = EventSystem.current;
            var e = new AxisEventData(es) { moveDir = dir };
            ExecuteEvents.Execute(es.currentSelectedGameObject, e, ExecuteEvents.moveHandler);
        }

        void TickSettings(float rdt)
        {
            if (SettingsOpen && (settingsRefresh -= rdt) <= 0) RefreshSettings();
            if (!fpsText.gameObject.activeSelf) return;
            float frame = Time.unscaledDeltaTime;
            fpsTime += frame; fpsFrames++;
            fpsWorst = Mathf.Max(fpsWorst, frame);
            if (fpsTime < 0.5f) return;
            fpsText.text = $"{fpsFrames / fpsTime:0} FPS  ·  {fpsTime / fpsFrames * 1000:0.0} ms  ·  worst {fpsWorst * 1000:0}";
            fpsTime = 0; fpsFrames = 0; fpsWorst = 0;
        }

        void BuildFpsCounter()
        {
            fpsText = Label(rootRT, "", 14, new Color(0.88f, 1, 0.96f, 0.85f), TextAnchor.UpperRight, new Vector2(1, 0), new Vector2(-74, 49), display);
            AddOutline(fpsText, new Color(0, 0.08f, 0.14f, 0.8f), 1.5f);
            fpsText.gameObject.SetActive(GameSettings.Data.showFps);
        }
    }
}
