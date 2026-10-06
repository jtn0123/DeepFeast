using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>
    /// Sprites cut from the art-drafts concept sheets by tools/cut_concepts.py (Resources/Concept).
    /// Anything missing here falls back to the procedural art.
    /// </summary>
    public static class ConceptArt
    {
        [System.Serializable]
        public sealed class Fish
        {
            public string key;
            public float ppu, bodyPX, bodyPY, tailPX, tailPY, eyeX, eyeY, eyeRX, eyeRY, lidR, lidG, lidB, hh;
            public bool open, overlay;
        }

        [System.Serializable]
        sealed class FishSet { public Fish[] fish; }

        [System.Serializable]
        sealed class Bell { public float ppu, pivotX, pivotY; }

        static Dictionary<string, Fish> fish;
        static Bell bell, reef;
        static bool bellLoaded, reefLoaded;

        static T Json<T>(string name) where T : class
        {
            var json = Resources.Load<TextAsset>("Concept/" + name);
            return json == null ? null : JsonUtility.FromJson<T>(json.text);
        }

        public static Fish FishMeta(string key)
        {
            if (fish == null)
            {
                fish = new Dictionary<string, Fish>();
                var set = Json<FishSet>("fish");
                if (set?.fish != null) foreach (var f in set.fish) fish[f.key] = f;
            }
            return fish.TryGetValue(key, out var m) ? m : null;
        }

        public static Sprite Load(string name, float pivotX, float pivotY, float ppu)
        {
            var tex = Resources.Load<Texture2D>("Concept/" + name);
            return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(pivotX, pivotY), ppu, 0, SpriteMeshType.FullRect);
        }

        /// Jelly bell: half its width is one unit and the pivot sits on the rim, like the procedural bell.
        public static Sprite JellyBell(float hue)
        {
            if (!bellLoaded) { bellLoaded = true; bell = Json<Bell>("jelly"); }
            return bell == null ? null : Load($"jelly_bell_{Mathf.RoundToInt(hue)}", bell.pivotX, bell.pivotY, bell.ppu);
        }

        /// Coral group (branch, brain, tubes): 2.4 units wide with the pivot on the sand line.
        public static Sprite Reef()
        {
            if (!reefLoaded) { reefLoaded = true; reef = Json<Bell>("reef"); }
            return reef == null ? null : Load("reef_cluster", reef.pivotX, reef.pivotY, reef.ppu);
        }
    }
}
