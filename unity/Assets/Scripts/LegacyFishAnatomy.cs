using UnityEngine;

namespace DeepFeast
{
    public sealed partial class FishVolume
    {
        // Bodies follow the painted silhouettes measured in sprite units, so the projected skin and
        // the painted fins meet the sculpted edge instead of floating inside a larger, blunter body.
        static Model LegacyBody(Species sp, FishArt.Art art)
        {
            Model Form(float height, float depth, float shoulder, float nose, float rear, float front, float peduncle, float lift = 0)
                => new Model { height = art.hh * height, depth = depth, head = new Vector4(shoulder, nose, art.hh * height, depth), profile = new Vector4(rear, front, peduncle, lift) };
            return sp.key switch
            {
                "minnow" => Form(0.80f, 0.13f, 0.10f, 1.18f, 1.50f, 0.65f, 0.16f, 0.04f),
                "clown" => Form(0.89f, 0.28f, 0.10f, 1.18f, 0.95f, 0.50f, 0.25f, 0.05f),
                "tang" => Form(0.74f, 0.19f, 0.12f, 1.16f, 0.90f, 0.55f, 0.17f, 0.08f),
                // The pointed snout and lower disc remove the coin-like profile of the old body.
                "angel" => Form(0.84f, 0.18f, 0.15f, 1.10f, 0.90f, 0.75f, 0.22f, 0.06f),
                "puffer" => Form(0.83f, 0.64f, 0.15f, 1.10f, 0.90f, 0.50f, 0.20f, -0.02f),
                "parrot" => Form(0.82f, 0.38f, 0.10f, 1.18f, 1.10f, 0.45f, 0.22f, 0.01f),
                "snapper" => Form(0.84f, 0.25f, 0.05f, 1.23f, 1.45f, 0.60f, 0.18f, 0.01f),
                "barracuda" => Form(0.80f, 0.12f, 0.00f, 1.28f, 0.75f, 0.85f, 0.30f),
                "grouper" => Form(0.79f, 0.46f, 0.15f, 1.09f, 0.70f, 0.45f, 0.30f, 0.06f),
                "tuna" => Form(0.97f, 0.30f, 0.00f, 1.28f, 1.65f, 0.68f, 0.10f, 0.01f),
                "player" => Form(0.92f, 0.33f, 0.08f, 1.20f, 1.20f, 0.50f, 0.20f, 0.03f),
                _ => null
            };
        }

        // The sculpted look keeps its procedural fins; painted envelopes need the projected skin.
        static bool HasPaintedFins(Species sp, FishArt.Art art) => Style == Look.Painted && art.painted && !sp.IsShark && LegacyBody(sp, art) != null;

        // Each envelope spans a little past the illustrated fin; the shader samples the painting at
        // the fin's rest position and its alpha cuts the exact painted outline, rays and ink.
        static bool BuildPaintedFins(Builder b, Species sp, Model model, FishArt.Art art)
        {
            if (!HasPaintedFins(sp, art)) return false;
            switch (sp.key)
            {
                case "minnow":
                    Envelope(b, sp, model, 1, 0.45f, -0.70f, 0.34f, 0.15f);
                    Envelope(b, sp, model, -1, -0.12f, -0.85f, 0.28f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.76f, 0.58f, -0.56f);
                    break;
                case "clown":
                    Envelope(b, sp, model, 1, 0.48f, -0.70f, 0.40f, 0.15f);
                    Envelope(b, sp, model, -1, -0.15f, -0.92f, 0.32f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.68f, 0.60f, -0.50f);
                    break;
                case "tang":
                    Envelope(b, sp, model, 1, 0.45f, -0.98f, 0.42f, 0.18f);
                    Envelope(b, sp, model, -1, 0.10f, -0.95f, 0.30f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.60f, 0.62f, -0.45f);
                    break;
                case "angel":
                    // Tall sickle fins sweep back past the tail root without crossing the caudal fan.
                    Envelope(b, sp, model, 1, 0.35f, -0.85f, 1.00f, 1.08f, 14, 48);
                    Envelope(b, sp, model, -1, 0.20f, -0.85f, 1.00f, 1.08f, 14, 48);
                    PaintedCaudal(b, sp, model, -1.62f, 0.64f, -0.64f);
                    break;
                case "puffer":
                    Envelope(b, sp, model, 1, -0.15f, -0.80f, 0.40f, 0.15f);
                    Envelope(b, sp, model, -1, -0.30f, -0.90f, 0.34f, 0.14f);
                    PaintedCaudal(b, sp, model, -1.55f, 0.60f, -0.46f);
                    break;
                case "parrot":
                    Envelope(b, sp, model, 1, 0.40f, -0.80f, 0.48f, 0.20f);
                    Envelope(b, sp, model, -1, -0.08f, -1.00f, 0.30f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.72f, 0.60f, -0.56f);
                    break;
                case "snapper":
                    Envelope(b, sp, model, 1, 0.40f, -0.80f, 0.40f, 0.20f);
                    Envelope(b, sp, model, -1, -0.10f, -0.92f, 0.30f, 0.15f);
                    PaintedCaudal(b, sp, model, -1.72f, 0.55f, -0.62f);
                    break;
                case "barracuda":
                    Envelope(b, sp, model, 1, 0.10f, -0.82f, 0.36f, 0.15f);
                    Envelope(b, sp, model, -1, -0.25f, -0.80f, 0.36f, 0.12f);
                    Envelope(b, sp, model, -1, 0.38f, 0.00f, 0.36f, 0.10f);
                    PaintedCaudal(b, sp, model, -1.66f, 0.45f, -0.58f);
                    break;
                case "grouper":
                    Envelope(b, sp, model, 1, 0.45f, -0.90f, 0.52f, 0.20f);
                    Envelope(b, sp, model, -1, -0.25f, -0.95f, 0.22f, 0.10f);
                    Envelope(b, sp, model, -1, 0.55f, 0.05f, 0.28f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.64f, 0.70f, -0.50f);
                    break;
                case "tuna":
                    Envelope(b, sp, model, 1, 0.35f, -0.62f, 0.45f, 0.28f);
                    // Yellow finlets run along both edges of the tapering tail stock.
                    Envelope(b, sp, model, 1, -0.56f, -1.04f, 0.16f, 0.05f);
                    Envelope(b, sp, model, -1, -0.52f, -1.04f, 0.16f, 0.05f);
                    Envelope(b, sp, model, -1, -0.18f, -0.70f, 0.34f, 0.15f);
                    Envelope(b, sp, model, -1, 0.48f, 0.02f, 0.32f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.72f, 0.66f, -0.65f);
                    break;
                case "player":
                    Envelope(b, sp, model, 1, 0.40f, -0.78f, 0.45f, 0.20f);
                    Envelope(b, sp, model, -1, -0.15f, -0.95f, 0.30f, 0.12f);
                    PaintedCaudal(b, sp, model, -1.70f, 0.62f, -0.52f);
                    break;
                default:
                    return false;
            }
            return true;
        }

        static void Envelope(Builder b, Species sp, Model model, int side, float start, float end, float height, float sweep,
            int radialSteps = 10, int alongSteps = 40)
        {
            b.Membrane(sp.fin, radialSteps, alongSteps, side > 0 ? 2 : 3, (weight, along) =>
            {
                // A broad, boxy crest keeps every painted fin inside; the root stays on the body surface.
                float x = Mathf.Lerp(start, end, along), crest = Mathf.Pow(Mathf.Max(0, Mathf.Sin(along * Mathf.PI)), 0.4f);
                float rootY = CenterY(x, model) + side * model.height * Profile(x, model) * 0.98f;
                return new Vector3(x - sweep * crest * weight, rootY + side * height * crest * weight,
                    Mathf.Sin(weight * Mathf.PI) * crest * 0.03f);
            });
        }

        static void PaintedCaudal(Builder b, Species sp, Model model, float tip, float top, float bottom)
        {
            float root = model.height * Profile(-1.04f, model), center = CenterY(-1.04f, model);
            b.Membrane(sp.tailCol, 12, 36, 4, (weight, across) =>
            {
                // The edges open quickly from the narrow stalk so rounded fans and forked lobes both fit.
                float y = Mathf.Lerp(Mathf.Lerp(center - root, center + root, across), Mathf.Lerp(bottom, top, across), Mathf.Pow(weight, 0.4f));
                return new Vector3(Mathf.Lerp(-1.04f, tip, weight), y, Mathf.Sin(weight * Mathf.PI) * 0.03f);
            });
        }

        // Pectorals traced from the illustrations: a short root on the flank and the rim, ordered
        // from the upper edge round the free end to the lower edge, all in sprite units. The grouper's
        // painting shows none, so its broad, rounded fan is drawn to suit in the painted fins' manner.
        static bool PectoralOutline(Species sp, FishArt.Art art, out Vector2 basePoint, out float root, out Vector2[] rim, out bool painted)
        {
            static Vector2 P(float x, float y) => new Vector2(x, y);
            (basePoint, root, rim) = sp.key switch
            {
                "minnow" => (P(0.385f, -0.085f), 0.045f, new[] { P(0.330f, -0.010f), P(0.250f, 0.040f), P(0.170f, 0.085f), P(0.156f, 0.069f), P(0.237f, -0.019f), P(0.300f, -0.075f), P(0.350f, -0.120f) }),
                "clown" => (P(0.430f, -0.070f), 0.090f, new[] { P(0.396f, -0.006f), P(0.314f, 0.067f), P(0.177f, 0.116f), P(0.047f, 0.116f), P(0.006f, 0.027f), P(0.014f, -0.103f), P(0.039f, -0.160f), P(0.095f, -0.225f), P(0.168f, -0.273f), P(0.241f, -0.281f), P(0.323f, -0.265f), P(0.387f, -0.208f), P(0.420f, -0.176f) }),
                "tang" => (P(0.500f, -0.120f), 0.060f, new[] { P(0.450f, -0.040f), P(0.350f, 0.020f), P(0.250f, 0.037f), P(0.100f, 0.025f), P(0.113f, -0.013f), P(0.163f, -0.075f), P(0.250f, -0.163f), P(0.330f, -0.205f), P(0.410f, -0.210f) }),
                "angel" => (P(0.410f, -0.220f), 0.070f, new[] { P(0.330f, -0.110f), P(0.200f, -0.040f), P(0.040f, 0.000f), P(-0.080f, -0.006f), P(-0.020f, -0.080f), P(0.020f, -0.190f), P(0.090f, -0.300f), P(0.160f, -0.370f), P(0.240f, -0.395f), P(0.330f, -0.380f) }),
                "puffer" => (P(0.300f, -0.020f), 0.100f, new[] { P(0.293f, 0.067f), P(0.267f, 0.084f), P(0.225f, 0.118f), P(0.166f, 0.135f), P(0.048f, 0.169f), P(-0.036f, 0.160f), P(-0.053f, 0.110f), P(-0.053f, 0.034f), P(-0.045f, -0.034f), P(0.006f, -0.135f), P(0.023f, -0.152f), P(0.073f, -0.185f), P(0.132f, -0.211f), P(0.200f, -0.202f), P(0.250f, -0.177f) }),
                "parrot" => (P(0.430f, -0.130f), 0.060f, new[] { P(0.418f, -0.078f), P(0.389f, -0.057f), P(0.332f, -0.021f), P(0.262f, -0.007f), P(0.127f, 0.007f), P(0.056f, 0.007f), P(0.084f, -0.064f), P(0.141f, -0.134f), P(0.184f, -0.184f), P(0.233f, -0.227f), P(0.290f, -0.234f), P(0.361f, -0.212f), P(0.389f, -0.205f), P(0.410f, -0.198f), P(0.425f, -0.184f) }),
                "snapper" => (P(0.440f, -0.230f), 0.060f, new[] { P(0.400f, -0.144f), P(0.300f, -0.088f), P(0.175f, -0.031f), P(0.050f, 0.006f), P(-0.094f, 0.019f), P(-0.037f, -0.062f), P(0.050f, -0.169f), P(0.138f, -0.275f), P(0.225f, -0.338f), P(0.300f, -0.344f), P(0.400f, -0.331f) }),
                "tuna" => (P(0.370f, -0.125f), 0.055f, new[] { P(0.320f, -0.050f), P(0.190f, 0.006f), P(0.050f, 0.045f), P(-0.100f, 0.072f), P(-0.250f, 0.085f), P(-0.120f, 0.020f), P(0.030f, -0.054f), P(0.160f, -0.121f), P(0.260f, -0.167f), P(0.340f, -0.185f) }),
                "player" => (P(0.380f, -0.110f), 0.080f, new[] { P(0.336f, -0.031f), P(0.263f, 0.025f), P(0.142f, 0.065f), P(-0.027f, 0.073f), P(-0.003f, -0.031f), P(0.029f, -0.112f), P(0.094f, -0.177f), P(0.142f, -0.217f), P(0.207f, -0.241f), P(0.255f, -0.249f), P(0.312f, -0.233f), P(0.344f, -0.225f), P(0.376f, -0.201f) }),
                "grouper" => (P(0.470f, -0.140f), 0.030f, new[] { P(0.430f, -0.100f), P(0.370f, -0.070f), P(0.290f, -0.055f), P(0.210f, -0.065f), P(0.140f, -0.100f), P(0.100f, -0.150f), P(0.095f, -0.205f), P(0.120f, -0.255f), P(0.180f, -0.290f), P(0.260f, -0.300f), P(0.340f, -0.280f), P(0.410f, -0.225f), P(0.440f, -0.180f) }),
                "barracuda" => (P(0.290f, -0.176f), 0.016f, new[] { P(0.270f, -0.150f), P(0.230f, -0.128f), P(0.180f, -0.105f), P(0.140f, -0.090f), P(0.105f, -0.090f), P(0.115f, -0.118f), P(0.150f, -0.160f), P(0.190f, -0.182f), P(0.240f, -0.192f), P(0.280f, -0.192f) }),
                _ => (Vector2.zero, 0f, null)
            };
            painted = sp.key != "grouper";
            return rim != null && HasPaintedFins(sp, art);
        }

        static float FlankZ(float x, float y, Model model)
        {
            float profile = Profile(x, model), relative = (y - CenterY(x, model)) / Mathf.Max(0.001f, model.height * profile);
            return model.depth * profile * Mathf.Sqrt(Mathf.Max(0.01f, 1 - relative * relative));
        }

        // Point at fraction t of the rim's length, so uneven tracing spaces the fan evenly.
        static Vector2 RimPoint(Vector2[] rim, float t)
        {
            float total = 0;
            for (int i = 1; i < rim.Length; i++) total += Vector2.Distance(rim[i - 1], rim[i]);
            float target = t * total;
            for (int i = 1; i < rim.Length; i++)
            {
                float segment = Vector2.Distance(rim[i - 1], rim[i]);
                if (target <= segment || i == rim.Length - 1) return Vector2.Lerp(rim[i - 1], rim[i], Mathf.Clamp01(target / Mathf.Max(segment, 1e-5f)));
                target -= segment;
            }
            return rim[0];
        }

        static Mesh TracedPectoral(Species sp, Model model, int side, Vector2 basePoint, float root, Vector2[] rim, bool opaque = false)
        {
            var b = new Builder();
            const int radial = 10, across = 24, count = (radial + 1) * (across + 1);
            var points = new Vector3[count * 2];
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        float weight = u / (float)radial, along = t / (float)across;
                        var start = basePoint + new Vector2(0, root * (1 - 2 * along));
                        var xy = Vector2.Lerp(start, RimPoint(rim, along), weight);
                        // The fan lies against the flank and lifts slightly towards its rim.
                        float z = FlankZ(xy.x, xy.y, model) + 0.012f + 0.05f * weight + (layer == 0 ? 0.004f : -0.004f);
                        var p = new Vector3(xy.x, xy.y, side * z);
                        points[layer * count + u * (across + 1) + t] = p;
                        var color = FinColor(sp.fin, weight);
                        // A fin drawn for a painting stays as solid as the painted fins around it.
                        if (opaque) color.a = Mathf.Lerp(0.98f, 0.88f, weight * weight);
                        b.Vertex(p, color, new Vector2(weight, side), new Vector2(weight, along));
                    }
            // The outer layer faces away from the body and the inner layer faces it.
            void Face(int a, int c, int d, int layer)
            {
                float z = Vector3.Cross(points[c] - points[a], points[d] - points[a]).z;
                if ((z * side > 0) == (layer == 0)) b.Tri(a, c, d); else b.Tri(a, d, c);
            }
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u < radial; u++)
                    for (int t = 0; t < across; t++)
                    {
                        int a = layer * count + u * (across + 1) + t, c = a + across + 1;
                        Face(a, c, a + 1, layer); Face(a + 1, c, c + 1, layer);
                    }
            for (int u = 0; u < radial; u++) { b.Edge(u * (across + 1), (u + 1) * (across + 1), count); b.Edge((u + 1) * (across + 1) + across, u * (across + 1) + across, count); }
            for (int t = 0; t < across; t++) b.Edge(radial * (across + 1) + t, radial * (across + 1) + t + 1, count);
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " traced pectoral fin");
        }

        // Ellipse around the painted pectoral, in sprite units: the body darkens it into the fan's shadow.
        static Vector4 PectoralMask(Vector2 basePoint, float root, Vector2[] rim)
        {
            var min = basePoint - new Vector2(0, root); var max = basePoint + new Vector2(0, root);
            foreach (var p in rim) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            var center = (min + max) * 0.5f; var extent = (max - min) * 0.5f;
            return new Vector4(center.x, center.y, extent.x * 1.08f + 0.015f, extent.y * 1.08f + 0.015f);
        }

        // Short conical spines spread evenly over the puffer, each along its local surface normal.
        static void AddPufferSpines(Builder b, Model model)
        {
            var tint = new Color(0.80f, 0.66f, 0.46f);
            const int count = 46;
            float golden = Mathf.PI * (3 - Mathf.Sqrt(5));
            Vector3 Surface(float x, float a) { float p = Profile(x, model); return new Vector3(x, CenterY(x, model) + Mathf.Cos(a) * model.height * p, Mathf.Sin(a) * model.depth * p); }
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(-0.72f, 0.80f, (i + 0.5f) / count), a = i * golden;
                // Keep the face, eyes and painted pectoral clear, as in the illustration.
                float flank = Mathf.Abs(Mathf.Sin(a)), up = Mathf.Cos(a);
                if (x > 0.45f && up > -0.2f && flank > 0.5f) continue;
                if (x > -0.15f && x < 0.45f && flank > 0.75f && up > -0.45f && up < 0.45f) continue;
                var p = Surface(x, a);
                var normal = Vector3.Cross(Surface(x, a + 0.01f) - p, Surface(x + 0.01f, a) - p).normalized;
                if (Vector3.Dot(normal, new Vector3(0, p.y - CenterY(x, model), p.z)) < 0) normal = -normal;
                var tangent = Vector3.Cross(normal, Vector3.right).normalized;
                var bitangent = Vector3.Cross(normal, tangent);
                float radius = 0.034f, height = 0.10f;
                int apex = b.Vertex(p + normal * height, tint, Vector2.zero, new Vector2(x, a / U.TAU));
                int first = -1;
                const int sides = 6;
                for (int s = 0; s < sides; s++)
                {
                    float theta = s * U.TAU / sides;
                    int v = b.Vertex(p - normal * 0.01f + (tangent * Mathf.Cos(theta) + bitangent * Mathf.Sin(theta)) * radius, tint, Vector2.zero, new Vector2(x, a / U.TAU));
                    if (first < 0) first = v;
                }
                for (int s = 0; s < sides; s++) b.Tri(apex, first + s, first + (s + 1) % sides);
            }
        }
    }
}
