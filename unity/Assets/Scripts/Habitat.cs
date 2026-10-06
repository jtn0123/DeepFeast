using UnityEngine;

namespace DeepFeast
{
    /// <summary>Continuous habitat transitions by depth: the scenery's look and which species spawn there.</summary>
    public static class Habitat
    {
        public readonly struct Look
        {
            public readonly float kelp, abyss;
            public readonly Color accent, light;
            public readonly string name;
            public Look(float kelp, float abyss, Color accent, Color light, string name)
            { this.kelp = kelp; this.abyss = abyss; this.accent = accent; this.light = light; this.name = name; }
        }

        // Every fish asks for its habitat each frame, so the palettes are parsed once.
        static readonly Color ReefAccent = U.Hex("#80ead9"), KelpAccent = U.Hex("#b4dba3"), AbyssAccent = U.Hex("#b6b6ff");
        static readonly Color ReefLight = new Color(1, 1, 0.94f), KelpLight = new Color(0.88f, 0.98f, 0.9f), AbyssLight = new Color(0.86f, 0.91f, 1);

        public static Look At(float depth)
        {
            float forest = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(850, 1600, depth));
            float abyss = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2700, 3550, depth));
            var accent = Color.Lerp(Color.Lerp(ReefAccent, KelpAccent, forest), AbyssAccent, abyss);
            var light = Color.Lerp(Color.Lerp(ReefLight, KelpLight, forest), AbyssLight, abyss);
            return new Look(forest * (1 - abyss), abyss, accent, light, abyss > 0.5f ? "THE ABYSS" : forest > 0.5f ? "KELP FOREST" : "CORAL REEF");
        }
    }
}
