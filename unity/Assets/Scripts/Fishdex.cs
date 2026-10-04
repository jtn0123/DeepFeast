using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeepFeast
{
    // The Fishdex: every species the player has eaten, kept between swims, with a portrait of each
    // one photographed from its 3D model.
    public static class Fishdex
    {
        const string SaveKey = "deepfeast.dex";
        public const int PortraitWidth = 356, PortraitHeight = 168;
        // Discovered fish are shown through a lit aquarium window; translucent fins need a backdrop
        // to read against, which a clear render would lose.
        public static readonly Color Window = U.Hex("#2f6f7c");
        // Reef first and abyss last, small to large within each; the shark encounters and the finale
        // giant close the book.
        public static readonly IReadOnlyList<Species> Entries = Order();
        static readonly HashSet<string> found = new HashSet<string>();
        static readonly Dictionary<string, RenderTexture> portraits = new Dictionary<string, RenderTexture>(), silhouettes = new Dictionary<string, RenderTexture>();
        // Test runs keep their discoveries in memory so they never touch the player's save.
        static bool persist;

        static List<Species> Order()
        {
            var book = Data.SpeciesMap.Values.OrderBy(Home).ThenBy(s => s.min + s.max).ToList();
            book.AddRange(Data.SharkVariants);
            book.Add(Data.WhaleShark);
            return book;
        }

        // 0 reef, 1 kelp forest, 2 abyss: where the species is most at home.
        static int Home(Species s) => s.reef >= Mathf.Max(s.kelp, s.abyss) ? 0 : s.kelp >= s.abyss ? 1 : 2;

        public static void Load(bool save)
        {
            persist = save;
            found.Clear();
            if (!save) return;
            foreach (var key in PlayerPrefs.GetString(SaveKey, "").Split(','))
                if (key.Length > 0) found.Add(key);
        }

        public static bool Has(Species s) => found.Contains(s.key);
        public static int FoundCount => Entries.Count(Has);

        /// Returns true the first time a species is eaten.
        public static bool Record(Species s)
        {
            if (s == Data.Player || !found.Add(s.key)) return false;
            if (persist) { PlayerPrefs.SetString(SaveKey, string.Join(",", found)); PlayerPrefs.Save(); }
            return true;
        }

        public static string Where(Species s)
        {
            if (s == Data.WhaleShark) return "The finale giant, met only by Legends";
            if (s.IsShark) return "Shark encounter, anywhere in the ocean";
            var homes = new List<string>();
            if (s.reef >= 0.5f) homes.Add("Coral reef");
            if (s.kelp >= 0.5f) homes.Add("Kelp forest");
            if (s.abyss >= 0.5f) homes.Add("The abyss");
            return string.Join("  ·  ", homes);
        }

        public static Texture Portrait(Species s) => Target(portraits, s, " portrait");
        /// The same pose on a clear background, tinted dark for species not yet eaten.
        public static Texture Silhouette(Species s) => Target(silhouettes, s, " silhouette");

        static RenderTexture Target(Dictionary<string, RenderTexture> set, Species s, string label)
        {
            if (!set.TryGetValue(s.key, out var texture))
                set[s.key] = texture = new RenderTexture(PortraitWidth, PortraitHeight, 16) { antiAliasing = 4, name = s.key + label };
            return texture;
        }

        /// Photographs any portrait that is missing (never taken, or lost with the graphics device).
        public static void EnsurePortraits()
        {
            if (Entries.Any(s => !((RenderTexture)Portrait(s)).IsCreated() || !((RenderTexture)Silhouette(s)).IsCreated())) RenderPortraits();
        }

        // Each model is posed off stage, well apart from the others, and framed to its own outline.
        static void RenderPortraits()
        {
            var stage = new GameObject("FishdexStage").transform;
            var camera = new GameObject("FishdexCamera").AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 5000;
            Shader.SetGlobalColor("_SceneLight", new Color(0.92f, 0.98f, 0.94f));
            var views = new List<FishView>();
            try
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    var sp = Entries[i];
                    var view = new FishView(stage, stage);
                    views.Add(view);
                    view.SetSpecies(sp);
                    // A slight turn toward the viewer shows the body's volume; shallow water keeps it clear of fog.
                    var pose = new Fish { sp = sp, x = -60000 - i * 2000, y = 300, r = 100, face = 1, faceS = 0.92f, wag = 1.4f };
                    for (int k = 0; k < 4; k++) view.Pose(pose, 0, FishView.EyeMode.Normal, 1f / 30);
                    var bounds = view.Bounds;
                    camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -2000);
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * PortraitHeight / PortraitWidth) * 1.1f;
                    camera.backgroundColor = Window;
                    camera.targetTexture = (RenderTexture)Portrait(sp);
                    camera.Render();
                    camera.backgroundColor = Color.clear;
                    camera.targetTexture = (RenderTexture)Silhouette(sp);
                    camera.Render();
                }
            }
            finally
            {
                camera.targetTexture = null;
                foreach (var view in views) view.Destroy();
                Object.Destroy(camera.gameObject);
                Object.Destroy(stage.gameObject);
            }
            Debug.Log($"[DeepFeast] fishdex: {Entries.Count} portraits rendered");
        }
    }
}
