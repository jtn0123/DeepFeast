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
                "pacific_sardine" => Form(0.56f, 0.16f, -0.06f, 1.50f, 1.50f, 0.66f, 0.10f),
                // Deep and short like the damselfish it is, with a blunt, rounded head.
                "garibaldi" => Form(0.88f, 0.22f, 0.06f, 1.22f, 1.00f, 0.34f, 0.19f, 0.02f),
                "california_sheephead" => Form(0.72f, 0.24f, 0.02f, 1.52f, 1.20f, 0.48f, 0.17f, 0.04f),
                // A heavy head and long, even body taper; the big mouth is set in the mouth table.
                "lingcod" => Form(1.22f, 0.27f, 0.20f, 1.32f, 1.35f, 0.45f, 0.12f, 0.02f),
                "lanternfish" => Form(0.62f, 0.15f, 0.10f, 1.30f, 1.40f, 0.40f, 0.10f),
                // The hatchet's blade is its deep belly: the centerline drops at the front while the
                // narrow tail stays high, and the steep rear exponent keeps the stalk long and thin.
                "hatchetfish" => Form(0.76f, 0.11f, 0.18f, 0.98f, 2.60f, 0.55f, 0.07f, -0.35f),
                // A globe of a body behind an enormous head; the lure is added with the body.
                "humpback_anglerfish" => Form(0.62f, 0.40f, 0.22f, 1.08f, 1.10f, 0.32f, 0.22f, 0.04f),
                "viperfish" => Form(0.80f, 0.17f, 0.30f, 1.02f, 1.10f, 0.60f, 0.10f, 0.03f),
                "swordfish" => Form(0.66f, 0.27f, -0.05f, 1.55f, 1.55f, 0.80f, 0.09f, 0.02f),
                // A tall, compressed disc whose rear stays deep: the body ends abruptly at the clavus.
                "ocean_sunfish" => Form(1.08f, 0.20f, 0.10f, 0.95f, 0.80f, 0.40f, 0.60f),
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

                case "pacific_sardine":
                    // One small dorsal at mid-body and a low anal fin just ahead of the tail.
                    SweptSpine(b, sp, model, 1, 0.18f, -0.16f, 0.20f, 0.30f);
                    SweptSpine(b, sp, model, -1, -0.42f, -0.68f, 0.09f, 0.35f);
                    return true;

                case "garibaldi":
                    // A low spiny front rises into the rounded soft lobe; the anal fin mirrors the lobe.
                    ExpandedSpine(b, sp, model, 1, 0.50f, -0.86f, 0.34f, t => Mathf.Max(
                        t < 0.62f ? 0.55f * Mathf.Pow(FinSine(t / 0.62f), 0.4f) : 0,
                        t > 0.40f ? Mathf.Pow(FinSine((t - 0.40f) / 0.60f), 0.6f) : 0), 0.06f, 48);
                    ExpandedSpine(b, sp, model, -1, -0.25f, -0.86f, 0.30f,
                        t => Mathf.Pow(FinSine(Mathf.Pow(t, 0.8f)), 0.55f), 0.07f);
                    return true;

                case "california_sheephead":
                    ExpandedSpine(b, sp, model, 1, 0.42f, -0.86f, 0.18f,
                        t => Plateau(t, 0.08f, 0.22f) * Mathf.Lerp(0.78f, 1, t), 0.05f, 48);
                    ExpandedSpine(b, sp, model, -1, -0.28f, -0.84f, 0.17f, t => Plateau(t, 0.15f, 0.30f), 0.06f);
                    return true;

                case "lingcod":
                    // A long, notched dorsal: spiny front half, then a taller soft rear half.
                    ExpandedSpine(b, sp, model, 1, 0.56f, -0.02f, 0.20f,
                        t => Plateau(t, 0.10f, 0.25f) * (0.85f + 0.15f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 9))), 0.05f, 40);
                    ExpandedSpine(b, sp, model, 1, -0.05f, -0.90f, 0.22f, t => Plateau(t, 0.10f, 0.30f), 0.06f);
                    ExpandedSpine(b, sp, model, -1, -0.18f, -0.88f, 0.18f, t => Plateau(t, 0.12f, 0.30f), 0.06f);
                    return true;

                case "lanternfish":
                    SweptSpine(b, sp, model, 1, 0.16f, -0.22f, 0.24f, 0.32f);
                    SweptSpine(b, sp, model, -1, -0.22f, -0.66f, 0.14f, 0.25f);
                    // The small adipose fin behind the dorsal.
                    ExpandedSpine(b, sp, model, 1, -0.70f, -0.80f, 0.05f, t => Mathf.Sqrt(FinSine(t)), 0.02f, 10, 4);
                    return true;

                case "hatchetfish":
                    SweptSpine(b, sp, model, 1, -0.02f, -0.30f, 0.20f, 0.35f);
                    SweptSpine(b, sp, model, -1, -0.50f, -0.80f, 0.10f, 0.30f);
                    return true;

                case "humpback_anglerfish":
                    // Small soft dorsal and anal fins sit far back on the globular body.
                    ExpandedSpine(b, sp, model, 1, -0.38f, -0.78f, 0.16f, t => Mathf.Pow(FinSine(t), 0.6f), 0.05f);
                    ExpandedSpine(b, sp, model, -1, -0.50f, -0.82f, 0.14f, t => Mathf.Pow(FinSine(t), 0.6f), 0.05f);
                    return true;

                case "viperfish":
                    // A short dorsal behind the head (its first ray is the long filament), an adipose
                    // fin and an anal fin close to the tail.
                    SweptSpine(b, sp, model, 1, 0.62f, 0.42f, 0.10f, 0.25f);
                    ExpandedSpine(b, sp, model, 1, -0.66f, -0.78f, 0.06f, t => Mathf.Sqrt(FinSine(t)), 0.02f, 10, 4);
                    SweptSpine(b, sp, model, -1, -0.60f, -0.88f, 0.12f, 0.30f);
                    return true;

                case "ocean_sunfish":
                    // Tall, pointed dorsal and anal fins set far back, mirror images of each other.
                    foreach (int side in new[] { -1, 1 })
                        ExpandedSpine(b, sp, model, side, -0.46f, -0.88f, 0.95f,
                            t => t < 0.45f ? Mathf.Pow(t / 0.45f, 0.8f) : Mathf.Pow((1 - t) / 0.55f, 1.2f), 0.30f, 36, 12);
                    return true;

                case Manta:
                    // A small dorsal fin sits at the root of the tail.
                    SweptSpine(b, sp, model, 1, -0.80f, -0.98f, 0.09f, 0.30f);
                    return true;

                case "swordfish":
                    // A tall, rigid crescent dorsal close behind the head; the second dorsal and anal are tiny.
                    ExpandedSickle(b, sp, model, 1, 0.48f, 0.10f, 0.70f);
                    SweptSpine(b, sp, model, 1, -0.74f, -0.84f, 0.07f, 0.35f);
                    ExpandedSickle(b, sp, model, -1, -0.30f, -0.52f, 0.30f);
                    SweptSpine(b, sp, model, -1, -0.72f, -0.82f, 0.06f, 0.35f);
                    return true;

                default:
                    return false;
            }
        }

        // Rises over the first `rise` of the base, holds, then rounds off over the last `fall`.
        static float Plateau(float t, float rise, float fall)
            => Mathf.SmoothStep(0, 1, t / rise) * Mathf.Sqrt(Mathf.Clamp01((1 - t) / fall));

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
            if (sp.key == "ocean_sunfish") { Clavus(b, sp, model); return true; }
            // The manta has no caudal fin; its whip tail grows from the body.
            if (sp.key == Manta) return true;
            // Broad tails without the two long lobes of a jack or tuna. The halibut's trailing edge
            // is shallowly concave, the drum's and sheephead's nearly square, the lingcod's rounded.
            (float height, float notch)? spec = sp.key switch
            {
                "atlantic_halibut" => (0.36f, 0.15f),
                "red_drum" => (0.35f, 0.075f),
                "california_sheephead" => (0.33f, 0.06f),
                "lingcod" => (0.31f, -0.07f),
                _ => null
            };
            if (spec == null) return false;
            float height = spec.Value.height * FishArt.HL, notch = spec.Value.notch;
            b.Membrane(sp.tailCol, 12, 40, 4, (weight, along) =>
            {
                float s = along * 2 - 1, edge = Mathf.Abs(s);
                var root = new Vector3(-1.04f, s * model.height * model.profile.z, 0);
                var rim = new Vector3(-1.55f - notch * edge * edge,
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

        // Cartoon eyes sized like the painted cast, placed on each authored head (relative to the centerline).
        static bool ExpandedEye(Species sp, out Vector2 position, out float radius)
        {
            (position, radius) = sp.key switch
            {
                "almaco_jack" => (new Vector2(0.80f, 0.13f), 0.150f),
                "goliath_grouper" => (new Vector2(0.93f, 0.17f), 0.110f),
                "atlantic_halibut" => (new Vector2(0.64f, 0.12f), 0.095f),
                "atlantic_mackerel" => (new Vector2(0.80f, 0.08f), 0.130f),
                "mahi_mahi" => (new Vector2(0.80f, 0.10f), 0.120f),
                "skipjack_tuna" => (new Vector2(0.82f, 0.10f), 0.140f),
                "striped_bass" => (new Vector2(0.86f, 0.10f), 0.140f),
                "yellowfin_tuna" => (new Vector2(0.86f, 0.09f), 0.140f),
                "bluefish" => (new Vector2(0.82f, 0.09f), 0.140f),
                "red_drum" => (new Vector2(0.86f, 0.12f), 0.140f),
                "pacific_sardine" => (new Vector2(0.86f, 0.06f), 0.130f),
                "garibaldi" => (new Vector2(0.80f, 0.14f), 0.140f),
                "california_sheephead" => (new Vector2(0.84f, 0.17f), 0.120f),
                "lingcod" => (new Vector2(0.90f, 0.17f), 0.110f),
                // Deep-sea eyes are large for the little light there is.
                "lanternfish" => (new Vector2(0.92f, 0.06f), 0.180f),
                "hatchetfish" => (new Vector2(0.74f, 0.30f), 0.160f),
                "humpback_anglerfish" => (new Vector2(0.86f, 0.40f), 0.075f),
                "viperfish" => (new Vector2(1.02f, 0.06f), 0.085f),
                "swordfish" => (new Vector2(0.98f, 0.06f), 0.120f),
                "ocean_sunfish" => (new Vector2(0.62f, 0.16f), 0.085f),
                Manta => (new Vector2(0.36f, 0.03f), 0.070f),
                _ => (Vector2.zero, 0f)
            };
            return radius > 0;
        }

        enum FinForm { Paddle, Blade, Sickle }

        // Pectoral fans for species without an illustration: jacks and tunas carry long sickles,
        // mackerel, mahi and bluefish tapering blades, the grouper, bass, drum and halibut paddles.
        static bool ExpandedPectoral(Species sp, Model model, out Vector2 basePoint, out float root, out Vector2[] rim)
        {
            (Vector2 point, float length, float droop, float width, float curl, FinForm form)? spec = sp.key switch
            {
                "almaco_jack" => (new Vector2(0.40f, -0.10f), 0.62f, 10f, 0.09f, 0.10f, FinForm.Sickle),
                "goliath_grouper" => (new Vector2(0.48f, -0.14f), 0.42f, 24f, 0.16f, 0f, FinForm.Paddle),
                "atlantic_halibut" => (new Vector2(0.38f, -0.06f), 0.26f, 20f, 0.08f, 0f, FinForm.Paddle),
                "atlantic_mackerel" => (new Vector2(0.42f, -0.05f), 0.34f, 14f, 0.06f, 0.05f, FinForm.Blade),
                "mahi_mahi" => (new Vector2(0.42f, -0.14f), 0.42f, 16f, 0.08f, 0.05f, FinForm.Blade),
                "skipjack_tuna" => (new Vector2(0.44f, -0.04f), 0.40f, 12f, 0.07f, 0.06f, FinForm.Blade),
                "striped_bass" => (new Vector2(0.48f, -0.08f), 0.38f, 20f, 0.10f, 0f, FinForm.Paddle),
                "yellowfin_tuna" => (new Vector2(0.48f, -0.04f), 0.66f, 8f, 0.07f, 0.10f, FinForm.Sickle),
                "bluefish" => (new Vector2(0.44f, -0.06f), 0.38f, 14f, 0.07f, 0.04f, FinForm.Blade),
                "red_drum" => (new Vector2(0.50f, -0.10f), 0.36f, 22f, 0.10f, 0f, FinForm.Paddle),
                "pacific_sardine" => (new Vector2(0.50f, -0.12f), 0.26f, 18f, 0.05f, 0.03f, FinForm.Blade),
                "garibaldi" => (new Vector2(0.44f, -0.08f), 0.36f, 18f, 0.13f, 0f, FinForm.Paddle),
                "california_sheephead" => (new Vector2(0.50f, -0.06f), 0.34f, 18f, 0.11f, 0f, FinForm.Paddle),
                "lingcod" => (new Vector2(0.46f, -0.12f), 0.44f, 26f, 0.15f, 0f, FinForm.Paddle),
                "lanternfish" => (new Vector2(0.60f, -0.10f), 0.24f, 14f, 0.045f, 0.03f, FinForm.Blade),
                "hatchetfish" => (new Vector2(0.50f, -0.10f), 0.24f, 30f, 0.05f, 0.02f, FinForm.Blade),
                "humpback_anglerfish" => (new Vector2(0.12f, -0.18f), 0.24f, 8f, 0.10f, 0f, FinForm.Paddle),
                "viperfish" => (new Vector2(0.72f, -0.10f), 0.20f, 25f, 0.035f, 0.02f, FinForm.Blade),
                "swordfish" => (new Vector2(0.45f, -0.16f), 0.55f, 22f, 0.07f, 0.10f, FinForm.Sickle),
                "ocean_sunfish" => (new Vector2(0.30f, 0.0f), 0.20f, 10f, 0.09f, 0f, FinForm.Paddle),
                _ => null
            };
            basePoint = default; root = 0; rim = null;
            if (spec == null) return false;
            var (point, length, droop, width, curl, form) = spec.Value;
            basePoint = new Vector2(point.x, point.y + CenterY(point.x, model));
            rim = LeafRim(basePoint, length, droop, width, curl, form, out root);
            return true;
        }

        // A leaf from a short root, as rim points ordered upper edge, tip, lower edge.
        static Vector2[] LeafRim(Vector2 basePoint, float length, float droop, float width, float curl, FinForm form, out float root)
        {
            float angle = droop * Mathf.Deg2Rad;
            var direction = new Vector2(-Mathf.Cos(angle), -Mathf.Sin(angle));
            var normal = new Vector2(direction.y, -direction.x);
            Vector2 Center(float s) => basePoint + direction * length * s + normal * curl * length * s * s;
            // sin(pi) is a hair below zero in floats; clamp before the fractional powers.
            float Arch(float s) => Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Min(1, s)));
            float Width(float s) => form switch
            {
                FinForm.Paddle => width * Mathf.Sqrt(Arch(s * 0.5f + 0.5f * Mathf.Pow(s, 1.5f))) * (1 - 0.25f * s),
                FinForm.Sickle => width * Mathf.Pow(Arch(s), 0.8f) * (1 - 0.55f * s),
                _ => width * Mathf.Pow(Arch(s), 0.7f) * (1 - 0.35f * s)
            };
            const int steps = 10; const float start = 0.18f;
            var rim = new Vector2[steps * 2 + 3];
            for (int i = 0; i <= steps; i++)
            {
                float s = start + (1 - start) * i / steps;
                rim[i] = Center(s) + normal * Width(s);
                rim[rim.Length - 1 - i] = Center(s) - normal * Width(s);
            }
            rim[steps + 1] = Center(1);
            root = Width(start);
            return rim;
        }

        static float FinSine(float along) => Mathf.Max(0, Mathf.Sin(Mathf.Clamp01(along) * Mathf.PI));
    }
}
