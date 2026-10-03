using UnityEngine;

namespace DeepFeast
{
    public sealed partial class FishVolume
    {
        // Proportions follow the species reference silhouettes. The camera-facing body of a
        // halibut is its eyed flank: it is broad in XY and very thin through Z.
        static Model ExpandedBody(Species sp, FishArt.Art art)
        {
            Model Form(float height, float depth, float shoulder, float nose, float rear, float front, float stalk, float lift = 0)
                => new Model
                {
                    height = art.hh * height,
                    depth = depth,
                    head = new Vector4(shoulder, nose, art.hh * height, depth),
                    profile = new Vector4(rear, front, stalk, lift)
                };

            return sp.key switch
            {
                "almaco_jack" => Form(0.84f, 0.24f, -0.10f, 1.47f, 1.18f, 0.63f, 0.14f, 0.025f),
                "goliath_grouper" => Form(0.78f, 0.45f, 0.06f, 1.50f, 1.00f, 0.39f, 0.23f, 0.025f),
                "atlantic_halibut" => Form(0.72f, 0.085f, -0.06f, 1.43f, 0.95f, 0.64f, 0.11f),
                "atlantic_mackerel" => Form(0.64f, 0.18f, -0.08f, 1.52f, 1.65f, 0.70f, 0.11f),
                // Low front exponent gives the male mahi its steep, broad forehead rather than
                // the pointed muzzle used by the tunas; the dorsal banner completes the outline.
                "mahi_mahi" => Form(1.05f, 0.21f, 0.35f, 1.03f, 1.10f, 0.23f, 0.12f, 0.095f),
                "skipjack_tuna" => Form(0.85f, 0.29f, -0.10f, 1.55f, 1.55f, 0.69f, 0.095f),
                "striped_bass" => Form(0.66f, 0.23f, 0.01f, 1.58f, 1.28f, 0.62f, 0.15f),
                "yellowfin_tuna" => Form(0.87f, 0.26f, -0.12f, 1.68f, 1.70f, 0.74f, 0.085f),
                "bluefish" => Form(0.80f, 0.25f, -0.09f, 1.52f, 1.32f, 0.64f, 0.13f),
                "red_drum" => Form(0.68f, 0.26f, -0.02f, 1.56f, 1.20f, 0.59f, 0.17f, 0.035f),
                _ => null
            };
        }

        static bool BuildExpandedSpines(Builder b, Species sp, Model model)
        {
            switch (sp.key)
            {
                case "almaco_jack":
                    // Small spinous first dorsal, then the characteristic high second dorsal
                    // and long anal base. These are discrete fins, with no tuna-like finlets.
                    SweptSpine(b, sp, model, 1, 0.53f, 0.13f, 0.16f, 0.38f);
                    ExpandedSpine(b, sp, model, 1, 0.10f, -0.89f, 0.43f,
                        t => t < 0.20f ? t / 0.20f : Mathf.Pow((1 - t) / 0.80f, 2.30f), 0.11f);
                    ExpandedSpine(b, sp, model, -1, -0.04f, -0.85f, 0.28f,
                        t => t < 0.23f ? t / 0.23f : Mathf.Pow((1 - t) / 0.77f, 2.05f), 0.08f);
                    return true;

                case "goliath_grouper":
                    ExpandedSpine(b, sp, model, 1, 0.58f, -0.92f, 0.30f, t =>
                    {
                        float envelope = Mathf.Pow(FinSine(t), 0.55f);
                        float spines = t < 0.57f ? 0.66f + 0.23f * Mathf.Abs(Mathf.Sin(t / 0.57f * Mathf.PI * 10)) : 1;
                        return envelope * spines;
                    }, 0.045f, 64);
                    ExpandedSpine(b, sp, model, -1, -0.24f, -0.91f, 0.26f,
                        t => Mathf.Pow(FinSine(t), 0.60f), 0.055f);
                    return true;

                case "atlantic_halibut":
                    // Continuous dorsal/anal margins begin close to the head and taper into the
                    // caudal stalk. The same thin body profile keeps their roots attached in turns.
                    foreach (int side in new[] { -1, 1 })
                        ExpandedSpine(b, sp, model, side, side > 0 ? 0.99f : 0.69f, -0.96f,
                            side > 0 ? 0.17f : 0.15f, t => Mathf.Pow(FinSine(t), 0.57f)
                                * (0.94f + 0.06f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 38))), 0.025f, 76);
                    return true;

                case "atlantic_mackerel":
                    SweptSpine(b, sp, model, 1, 0.43f, -0.03f, 0.28f, 0.25f);
                    SweptSpine(b, sp, model, 1, -0.39f, -0.64f, 0.16f, 0.34f);
                    SweptSpine(b, sp, model, -1, -0.37f, -0.65f, 0.13f, 0.34f);
                    return true;

                case "mahi_mahi":
                    ExpandedSpine(b, sp, model, 1, 1.12f, -0.99f, 0.31f, t =>
                    {
                        float ends = Mathf.SmoothStep(0, 1, t / 0.05f) * Mathf.SmoothStep(0, 1, (1 - t) / 0.12f);
                        return ends * Mathf.Lerp(1, 0.51f, t) * (0.97f + 0.03f * Mathf.Sin(t * Mathf.PI * 60));
                    }, 0.045f, 84);
                    ExpandedSpine(b, sp, model, -1, -0.11f, -0.93f, 0.19f,
                        t => Mathf.Pow(FinSine(t), 0.63f), 0.065f);
                    return true;

                case "skipjack_tuna":
                    SweptSpine(b, sp, model, 1, 0.47f, -0.14f, 0.27f, 0.24f);
                    SweptSpine(b, sp, model, 1, -0.29f, -0.54f, 0.25f, 0.35f);
                    SweptSpine(b, sp, model, -1, -0.26f, -0.53f, 0.21f, 0.32f);
                    return true;

                case "striped_bass":
                    ExpandedSpine(b, sp, model, 1, 0.49f, -0.06f, 0.28f,
                        t => (t < 0.22f ? t / 0.22f : Mathf.Pow((1 - t) / 0.78f, 0.72f))
                            * (0.87f + 0.13f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 8))), 0.055f, 40);
                    ExpandedSpine(b, sp, model, 1, -0.12f, -0.76f, 0.26f,
                        t => Mathf.Pow(FinSine(t), 0.61f), 0.075f);
                    ExpandedSpine(b, sp, model, -1, -0.22f, -0.78f, 0.20f,
                        t => Mathf.Pow(FinSine(t), 0.63f), 0.085f);
                    return true;

                case "yellowfin_tuna":
                    SweptSpine(b, sp, model, 1, 0.47f, -0.13f, 0.28f, 0.28f);
                    ExpandedSickle(b, sp, model, 1, -0.23f, -0.58f, 0.76f);
                    ExpandedSickle(b, sp, model, -1, -0.25f, -0.61f, 0.66f);
                    return true;

                case "bluefish":
                    SweptSpine(b, sp, model, 1, 0.43f, 0.05f, 0.14f, 0.35f);
                    ExpandedSpine(b, sp, model, 1, -0.03f, -0.91f, 0.24f,
                        t => t < 0.23f ? t / 0.23f : Mathf.Pow((1 - t) / 0.77f, 1.38f), 0.065f);
                    ExpandedSpine(b, sp, model, -1, -0.14f, -0.88f, 0.18f,
                        t => Mathf.Pow(FinSine(t), 0.62f), 0.075f);
                    return true;

                case "red_drum":
                    ExpandedSpine(b, sp, model, 1, 0.50f, -0.07f, 0.25f,
                        t => (t < 0.24f ? t / 0.24f : Mathf.Pow((1 - t) / 0.76f, 0.82f))
                            * (0.88f + 0.12f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 10))), 0.055f, 40);
                    ExpandedSpine(b, sp, model, 1, -0.08f, -0.91f, 0.21f,
                        t => Mathf.Pow(FinSine(t), 0.54f), 0.035f);
                    ExpandedSpine(b, sp, model, -1, -0.47f, -0.86f, 0.18f,
                        t => Mathf.Pow(FinSine(t), 0.68f), 0.055f);
                    return true;

                default:
                    return false;
            }
        }

        static void AddExpandedFinDetails(Builder b, Species sp, Model model)
        {
            bool mackerel = sp.key == "atlantic_mackerel";
            if (!mackerel && sp.key != "skipjack_tuna" && sp.key != "yellowfin_tuna") return;
            int count = mackerel ? 5 : 7;
            float start = mackerel ? -0.68f : -0.58f, spacing = mackerel ? 0.065f : 0.055f;
            for (int i = 0; i < count; i++)
                foreach (int side in new[] { -1, 1 })
                {
                    float x = start - i * spacing;
                    ExpandedSpine(b, sp, model, side, x, x - spacing * 0.82f,
                        (mackerel ? 0.065f : 0.083f) - i * 0.004f,
                        t => t < 0.32f ? t / 0.32f : Mathf.Pow((1 - t) / 0.68f, 1.25f), 0.032f, 10, 4);
                }
        }

        static bool BuildExpandedCaudal(Builder b, Species sp, Model model)
        {
            bool halibut = sp.key == "atlantic_halibut";
            if (!halibut && sp.key != "red_drum") return false;
            float height = (halibut ? 0.36f : 0.35f) * FishArt.HL;
            b.Membrane(sp.tailCol, 12, 40, 4, (weight, along) =>
            {
                float s = along * 2 - 1, edge = Mathf.Abs(s);
                // A shallow concave trailing edge is characteristic of both species. Halibut
                // has the deeper notch; neither has the two long lobes of a jack or tuna tail.
                var root = new Vector3(-1.04f, s * model.height * model.profile.z, 0);
                var rim = new Vector3(-1.55f - (halibut ? 0.15f : 0.075f) * edge * edge,
                    s * height * (1 - 0.07f * Mathf.Pow(edge, 6)), 0);
                var p = Vector3.Lerp(root, rim, weight);
                p.y += s * height * 0.035f * Mathf.Sin(weight * Mathf.PI);
                p.z = Mathf.Sin(weight * Mathf.PI) * 0.025f
                    + Mathf.Sin(along * Mathf.PI * 24) * 0.0015f * weight;
                return p;
            });
            return true;
        }

        // All membrane roots use exactly the body surface. Variation is authored at the rim,
        // so shared body-wave deformation cannot separate the fin from its attachment.
        static void ExpandedSpine(Builder b, Species sp, Model model, int side, float start, float end,
            float height, System.Func<float, float> outline, float sweep, int alongSteps = 36, int radialSteps = 8)
        {
            b.Membrane(sp.fin, radialSteps, alongSteps, side > 0 ? 2 : 3, (weight, along) =>
            {
                float x = Mathf.Lerp(start, end, along), crest = Mathf.Max(0, outline(along));
                float rootY = CenterY(x, model) + side * model.height * Profile(x, model) * 0.98f;
                return new Vector3(x - sweep * crest * weight,
                    rootY + side * height * crest * weight,
                    Mathf.Sin(weight * Mathf.PI) * crest * 0.027f);
            });
        }

        static void ExpandedSickle(Builder b, Species sp, Model model, int side, float start, float end, float height)
        {
            b.Membrane(sp.fin, 14, 40, side > 0 ? 2 : 3, (weight, along) =>
            {
                float x = Mathf.Lerp(start, end, along);
                float crest = along < 0.22f ? Mathf.Pow(along / 0.22f, 0.65f)
                    : Mathf.Pow(Mathf.Max(0, (1 - along) / 0.78f), 3.2f);
                float rootY = CenterY(x, model) + side * model.height * Profile(x, model) * 0.98f;
                // The leading ray rises high and curls backwards; the trailing membrane recedes
                // quickly into the body, yielding a narrow sickle instead of a tall triangular plate.
                return new Vector3(x - 0.56f * crest * weight * weight,
                    rootY + side * height * crest * weight,
                    Mathf.Sin(weight * Mathf.PI) * crest * 0.031f);
            });
        }

        static float FinSine(float along) => Mathf.Max(0, Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI));
    }
}
