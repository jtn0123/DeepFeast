using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace DeepFeast
{
    // Every player option, saved as one JSON entry in PlayerPrefs.
    [Serializable]
    public sealed class SettingsData
    {
        public int displayMode;          // index into GameSettings.Modes
        public int width, height;        // 0: native in fullscreen, a comfortable window otherwise
        public bool vSync = true;
        public int maxFps = 120;         // 0 unlimited, -1 custom
        public int customFps = 90;
        public int renderScale = 100;    // percent of the output resolution
        public int msaa = 4;
        public bool showFps;
        public int shake = 100;          // percent
        public int master = 100, effects = 100, ambience = 100;
    }

    // Loads, saves and applies the player's options. Test runs start from the defaults, never
    // write the save and only change the frame rate when a test asks for it.
    public static class GameSettings
    {
        const string SaveKey = "deepfeast.settings";
        public static SettingsData Data { get; private set; } = new SettingsData();
        public static event Action Changed;
        static bool persist, saved, testFrameRate;
        static float displayPending = -1;

        public static readonly FullScreenMode[] Modes =
#if UNITY_STANDALONE_WIN
            { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen };
#else
            { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
#endif
        public static readonly int[] FpsPresets = { 0, 30, 60, 90, 120, 144, 165, 240, 360, -1 };
        public static readonly int[] RenderScales = { 50, 67, 75, 85, 100, 125, 150, 175, 200 };
        public static readonly int[] MsaaLevels = { 1, 2, 4, 8 };
        public const int MinFps = 20, MaxCustomFps = 1000;

        public static void Load(bool save, string overrides)
        {
            persist = save;
            Data = new SettingsData();
            if (save)
            {
                var json = PlayerPrefs.GetString(SaveKey, "");
                if (json.Length > 0)
                {
                    saved = true;
                    try { JsonUtility.FromJsonOverwrite(json, Data); }
                    catch (ArgumentException e) { Debug.LogWarning("[DeepFeast] settings reset: " + e.Message); Data = new SettingsData(); }
                }
            }
            if (overrides != null) Override(overrides);
            Sanitize();
        }

        // -set key=value,key=value: test runs pin any option by its field name.
        static void Override(string pairs)
        {
            foreach (var pair in pairs.Split(','))
            {
                var kv = pair.Split('=');
                var field = kv.Length == 2 ? typeof(SettingsData).GetField(kv[0].Trim(), BindingFlags.Public | BindingFlags.Instance) : null;
                if (field == null) throw new ArgumentException("Unknown setting override: " + pair);
                string v = kv[1].Trim();
                field.SetValue(Data, field.FieldType == typeof(bool) ? (object)bool.Parse(v) : int.Parse(v, CultureInfo.InvariantCulture));
                if (field.Name == "vSync" || field.Name == "maxFps" || field.Name == "customFps") testFrameRate = true;
            }
            Debug.Log("[DeepFeast] settings override: " + JsonUtility.ToJson(Data));
        }

        static void Sanitize()
        {
            var d = Data;
            d.displayMode = Mathf.Clamp(d.displayMode, 0, Modes.Length - 1);
            if (Array.IndexOf(FpsPresets, d.maxFps) < 0) d.maxFps = 120;
            d.customFps = Mathf.Clamp(d.customFps, MinFps, MaxCustomFps);
            if (Array.IndexOf(RenderScales, d.renderScale) < 0) d.renderScale = 100;
            if (Array.IndexOf(MsaaLevels, d.msaa) < 0) d.msaa = 4;
            d.shake = Mathf.Clamp(d.shake, 0, 150);
            d.master = Mathf.Clamp(d.master, 0, 100);
            d.effects = Mathf.Clamp(d.effects, 0, 100);
            d.ambience = Mathf.Clamp(d.ambience, 0, 100);
        }

        /// Call after changing Data: saves, applies what applies at once and tells listeners.
        public static void Commit(bool display = false)
        {
            Sanitize();
            if (persist) { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }
            ApplyFrameRate();
            // Window changes wait until the player stops stepping through them.
            if (display) displayPending = 0.8f;
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            Data = new SettingsData();
            Commit(true);
        }

        /// Startup: a saved choice puts the window back as the player left it; with nothing saved
        /// yet the rows simply describe the window the game opened in.
        public static void ApplyAtBoot()
        {
            ApplyFrameRate();
            if (saved) ApplyDisplay(true);
            else SyncFromScreen();
        }

        /// Takes the window's actual mode and size, which the player may have changed by dragging
        /// its edge or with the green button since the last apply.
        public static void SyncFromScreen()
        {
#if !UNITY_WEBGL
            if (Application.isBatchMode || displayPending > 0) return;
            var mode = Screen.fullScreenMode;
            int index = Array.IndexOf(Modes, mode);
            Data.displayMode = index >= 0 ? index : 0;
            var size = new Vector2Int(Screen.width, Screen.height);
            Data.width = Data.height = 0;
            if (size != TargetSize(Modes[Data.displayMode])) { Data.width = size.x; Data.height = size.y; }
#endif
        }

        public static void Tick(float rdt)
        {
            if (displayPending > 0 && (displayPending -= rdt) <= 0) ApplyDisplay();
        }

        /// The frame cap that applies, or -1 for none.
        public static int FrameCap => Data.maxFps == -1 ? Data.customFps : Data.maxFps <= 0 ? -1 : Data.maxFps;

        public static void ApplyFrameRate()
        {
            // Headless checks run at the steady 60 they always have, unless a test pins the frame rate.
            bool steady = Application.isBatchMode && !testFrameRate;
#if !UNITY_WEBGL
            QualitySettings.vSyncCount = steady || Data.vSync ? 1 : 0;
            Application.targetFrameRate = steady ? 60 : Data.vSync ? -1 : FrameCap;
#endif
            Debug.Log($"[DeepFeast] frame rate: vSync={QualitySettings.vSyncCount}, cap={Application.targetFrameRate}");
        }

        public static void ApplyDisplay(bool onlyIfChanged = false)
        {
            displayPending = -1;
#if !UNITY_WEBGL
            var mode = Modes[Data.displayMode];
            var size = TargetSize(mode);
            if (Application.isBatchMode)
            {
                Debug.Log($"[DeepFeast] display: {mode} {size.x}x{size.y} (not applied headless)");
                return;
            }
            if (onlyIfChanged && Screen.fullScreenMode == mode && Screen.width == size.x && Screen.height == size.y) return;
            Screen.SetResolution(size.x, size.y, mode);
#endif
        }

        public static Vector2Int Native
        {
            get
            {
                var main = Display.main;
                if (main != null && main.systemWidth > 0 && main.systemHeight > 0) return new Vector2Int(main.systemWidth, main.systemHeight);
                var info = Screen.mainWindowDisplayInfo;
                if (info.width > 0 && info.height > 0) return new Vector2Int(info.width, info.height);
                var cur = Screen.currentResolution;
                return new Vector2Int(Mathf.Max(1280, cur.width), Mathf.Max(720, cur.height));
            }
        }

        // Fullscreen defaults to the display's own resolution; a window defaults to three quarters of it.
        public static Vector2Int TargetSize(FullScreenMode mode)
        {
            if (Data.width > 0 && Data.height > 0) return new Vector2Int(Data.width, Data.height);
            var native = Native;
            return mode == FullScreenMode.Windowed ? new Vector2Int(native.x * 3 / 4, native.y * 3 / 4) : native;
        }

        /// Sizes the display offers, largest first, without duplicates; 0x0 stands for the default.
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int> { Vector2Int.zero };
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 640 && v.y >= 400 && !list.Contains(v)) list.Add(v);
            }
            var saved = new Vector2Int(Data.width, Data.height);
            if (saved != Vector2Int.zero && !list.Contains(saved)) list.Add(saved);
            list.Sort((a, b) => a == Vector2Int.zero ? -1 : b == Vector2Int.zero ? 1 : (b.x * b.y).CompareTo(a.x * a.y));
            return list;
        }

        public static float RefreshRate
        {
            get
            {
                double hz = Screen.currentResolution.refreshRateRatio.value;
                return hz > 1 ? (float)hz : 60;
            }
        }
    }
}
