using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    // Authored silhouettes for lamnid, requiem, hammerhead and whale sharks. The seven cached
    // parts and shader flex contract remain shared with FishVolume's other species.
    public sealed partial class FishVolume
    {
        // The hammerhead's cephalofoil: its center along the body and its half span across. The
        // hammerhead banks toward the viewer (degrees) so the side view shows the blade's top.
        const float HammerX = 1.04f, HammerSpan = 0.50f, HammerBank = 35;

        // The blade's centre line along the body at a point across it (-1..1): its ends sweep back.
        static float HammerMid(float across) => HammerX - 0.050f * across * across;

        static Model SharkBody(Species sp, FishArt.Art art)
        {
            if (sp.key == "whale_shark")
            {
                // A broad, flattened giant: the back humps behind a low, wide head that ends
                // bluntly in the mouth (the centre line dips toward it).
                float h = art.hh * 0.95f;
                return new Model { height = h, depth = 0.44f, snout = -0.14f, head = new Vector4(0.18f, 1.10f, h, 0.44f), profile = new Vector4(1.25f, 0.42f, 0.16f, 0.02f) };
            }
            if (sp.key == "great_hammerhead")
            {
                // A short snout that ends inside the cephalofoil, which is added with the ridges.
                float h = art.hh * 0.84f;
                return new Model { height = h, depth = 0.26f, snout = 0.12f, head = new Vector4(0.24f, 0.82f, h, 0.26f), profile = new Vector4(1.30f, 0.75f, 0.14f, 0.01f) };
            }
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            float height = art.hh * (tiger ? 0.92f : mako ? 0.80f : 0.88f);
            float depth = tiger ? 0.32f : mako ? 0.22f : 0.29f;
            // Sharks are deepest well forward, over the pectorals, and narrow in a long cone to a
            // snout that rides high above the underslung mouth. The mako's cone is the sharpest;
            // the tiger's snout is short, broad and nearly square.
            return new Model
            {
                height = height, depth = depth, snout = tiger ? 0.16f : mako ? 0.38f : 0.40f,
                head = new Vector4(tiger ? 0.30f : mako ? 0.22f : 0.28f,
                    tiger ? 0.88f : mako ? 1.10f : 1.02f, height, depth),
                profile = new Vector4(tiger ? 1.30f : mako ? 1.25f : 1.10f,
                    tiger ? 0.46f : mako ? 1.05f : 0.95f,
                    tiger ? 0.17f : mako ? 0.13f : 0.15f, tiger ? 0.01f : 0.015f),
            };
        }

        static Vector3 SharkCurve(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float s = 1 - t;
            return s * s * a + 2 * s * t * control + t * t * b;
        }

        static Vector3 SharkCubic(Vector3 a, Vector3 c, Vector3 d, Vector3 b, float t)
        {
            float s = 1 - t;
            return s * s * s * a + 3 * s * s * t * c + 3 * s * t * t * d + t * t * t * b;
        }

        // The median fins: which side, the base's front and rear ends, the apex's position along the
        // body and its height above the back or below the belly.
        static (int side, float start, float end, float apex, float height)[] SharkMedians(Species sp)
        {
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            bool whale = sp.key == "whale_shark", hammer = sp.key == "great_hammerhead";
            // The whale shark's first dorsal sits far back; the great hammerhead's is tall and sickle-shaped.
            var first = whale ? (1, -0.10f, -0.62f, -0.38f, 0.36f)
                : hammer ? (1, 0.16f, -0.30f, -0.34f, 0.66f)
                : (1, tiger ? -0.03f : 0.10f, tiger ? -0.68f : -0.55f, tiger ? -0.34f : mako ? -0.30f : -0.19f,
                    tiger ? 0.36f : mako ? 0.39f : 0.44f);
            // Second dorsal and anal fins are distinctly smaller than the first.
            return new[] { first, (1, -0.73f, -0.99f, -0.87f, tiger || hammer ? 0.14f : 0.105f),
                (-1, -0.76f, -1.01f, -0.90f, tiger || hammer ? 0.13f : 0.085f) };
        }

        static void BuildSharkFins(Builder b, Species sp, Model model)
        {
            SharkCaudal(b, sp, model);
            foreach (var fin in SharkMedians(sp)) SharkSpine(b, sp, model, fin.side, fin.start, fin.end, fin.apex, fin.height);
            foreach (int side in new[] { -1, 1 }) SharkPelvic(b, sp, model, side);
        }

        // The body's contour ink, carried in its vertex alpha above 0.91: none along the median fin
        // bases, so each fin rises out of the back or belly instead of sitting behind a drawn line.
        // up is the vertex's position around the body, 1 on top and -1 underneath.
        static float SharkContour((int side, float start, float end, float apex, float height)[] medians, float x, float up)
        {
            float hidden = 0;
            foreach (var fin in medians)
                hidden = Mathf.Max(hidden, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fin.end, fin.end + 0.05f, x))
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fin.start, fin.start - 0.05f, x))
                    * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 0.90f, up * fin.side)));
            return 1 - hidden;
        }

        // Lateral keels: lamnids and the whale shark carry a strong ridge along each side of the
        // peduncle, the tiger shark a low one and the hammerhead none. The body's cross section swells
        // at its midline, so the keel is shaded skin rather than a separate, inked part.
        static float SharkKeel(Species sp, float x)
        {
            float height = sp.key switch { "great_hammerhead" => 0, "tiger_shark" => 0.016f, "whale_shark" => 0.055f, "mako_shark" => 0.050f, _ => 0.042f };
            return height * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.InverseLerp(-1.12f, -0.64f, x) * Mathf.PI)), 1.5f);
        }

        // Shark fins are opaque cartilage in the fin color. They darken toward dusky tips and along
        // the trailing margin and carry a faint pale leading edge; each mask runs from 0 to 1.
        static Color SharkFinColor(Species sp, Color color, float leading, float trailing, float tip)
        {
            var dusky = Color.Lerp(color, sp.c1, 0.6f) * 0.72f;
            var result = Color.Lerp(color, dusky, Mathf.Clamp01(tip * SharkTipShade(sp) + trailing * 0.6f));
            result = Color.Lerp(result, Color.Lerp(color, sp.c2, 0.35f), leading * 0.5f);
            result.a = 1;
            return result;
        }

        // White sharks have black-tipped pectorals; the others are only dusky.
        static float SharkTipShade(Species sp) => sp.key switch { "shark" => 0.85f, "tiger_shark" => 0.5f, "great_hammerhead" => 0.45f, _ => 0.35f };

        // Margins of a fin laid out from its root (weight 0) to its rim (1), where the rim runs from
        // the leading root up the leading edge to the apex, then back down the trailing edge.
        static Color SharkRimColor(Species sp, Color color, float weight, float along, float apex, float tipScale)
        {
            float rim = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.78f, 1, weight));
            float front = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(apex + 0.05f, apex - 0.05f, along));
            float tip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.35f, 1, weight)) * Mathf.Exp(-Mathf.Pow((along - apex) / 0.20f, 2));
            return SharkFinColor(sp, color, rim * front, rim * (1 - front), tip * tipScale);
        }

        static void SharkCaudal(Builder b, Species sp, Model model)
        {
            bool tiger = sp.key == "tiger_shark";
            float stalk = model.height * model.profile.z, notchX = tiger ? -1.33f : -1.36f;
            var notch = new Vector3(notchX, 0, 0);
            // Close the small triangle between the peduncle and the tail's inner fork.
            SharkTailPatch(b, sp, 20, 20, (weight, along) =>
                Vector3.Lerp(new Vector3(-1.04f, (along * 2 - 1) * stalk, 0), notch, weight),
                (weight, along) => SharkFinColor(sp, sp.tailCol, 0, 0, 0), true);
            foreach (int side in new[] { -1, 1 })
            {
                // The upper lobe carries the spine and outreaches the lower one. Only the fast
                // mako's tail is close to symmetric.
                var (height, tipX) = (side > 0, sp.key) switch
                {
                    (true, "tiger_shark") => (0.80f, -2.04f),
                    (true, "mako_shark") => (0.66f, -1.92f),
                    (true, "whale_shark") => (0.78f, -2.02f),
                    (true, "great_hammerhead") => (0.82f, -2.06f),
                    (true, _) => (0.72f, -1.96f),
                    (false, "tiger_shark") => (0.40f, -1.74f),
                    (false, "mako_shark") => (0.58f, -1.86f),
                    (false, "whale_shark") => (0.46f, -1.82f),
                    (false, "great_hammerhead") => (0.38f, -1.74f),
                    (false, _) => (0.52f, -1.76f),
                };
                // The leading edge leaves the peduncle along its contour and only then sweeps out
                // to the tip, so the back flows into the upper lobe without a pinched corner.
                var leadingRoot = new Vector3(-1.04f, side * stalk, 0);
                var leadingFillet = new Vector3(-1.17f, side * (stalk + 0.035f), 0);
                var leadingControl = new Vector3(-1.42f, side * height * 0.86f, 0);
                var trailingControl = new Vector3(-1.44f, side * height * 0.37f, 0);
                SharkTailPatch(b, sp, 20, 20, (weight, across) =>
                {
                    // A short rounded rim joins independently curved leading and trailing
                    // boundaries. A linear root-to-tip fan produced the former straight V.
                    var tip = new Vector3(tipX - 0.025f * Mathf.Max(0, Mathf.Sin(across * Mathf.PI)),
                        side * (height - across * 0.026f), 0);
                    var leading = SharkCubic(leadingRoot, leadingFillet, leadingControl, tip, weight);
                    var trailing = SharkCurve(notch, trailingControl, tip, weight);
                    var p = Vector3.Lerp(leading, trailing, across);
                    // The camber leaves the root flat, so the lobes meet the bridge without a crease.
                    float camber = Mathf.Sin(weight * Mathf.PI);
                    p.z = 0.040f * camber * camber * Mathf.Max(0, Mathf.Sin(across * Mathf.PI));
                    return p;
                }, (weight, across) =>
                {
                    // across runs from the leading edge (0) to the forked trailing edge (1).
                    float along = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.15f, 0.45f, weight));
                    return SharkFinColor(sp, sp.tailCol, (1 - Mathf.SmoothStep(0, 1, across / 0.14f)) * along,
                        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.74f, 1, across)) * along * 1.3f,
                        Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.50f, 1, weight)) * 0.65f);
                });
            }
        }

        static void SharkTailPatch(Builder b, Species sp, int radial, int across,
            System.Func<float, float, Vector3> point, System.Func<float, float, Color> shade, bool bridge = false)
        {
            int count = (radial + 1) * (across + 1), first = -1;
            var points = new Vector3[count * 2];
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        var p = point(u / (float)radial, t / (float)across);
                        // All caudal patches derive flex, phase and thickness from position.
                        // Thus both sides of every shared edge keep the same animated seam.
                        float flex = Mathf.Clamp01((-1.04f - p.x) / 1.02f), phase = (p.y + 0.65f) / 1.51f;
                        // Thinning eases in from the root: a steep start tilts the skin there toward the
                        // light and draws a pale collar around the peduncle.
                        p.z += (layer == 0 ? -1 : 1) * Mathf.Lerp(0.035f, 0.004f, Mathf.SmoothStep(0, 1, Mathf.Min(1, flex * 1.6f)));
                        int local = layer * count + u * (across + 1) + t;
                        points[local] = p;
                        int index = b.Vertex(p, shade(u / (float)radial, t / (float)across), new Vector2(flex, 4), new Vector2(flex, phase));
                        if (first < 0) first = index;
                    }
            void Face(int a, int c, int d, int layer)
            {
                float z = Vector3.Cross(points[c] - points[a], points[d] - points[a]).z;
                if ((z < 0) == (layer == 0)) b.Tri(first + a, first + c, first + d);
                else b.Tri(first + a, first + d, first + c);
            }
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u < radial; u++)
                    for (int t = 0; t < across; t++)
                    {
                        int a = layer * count + u * (across + 1) + t, c = a + across + 1;
                        Face(a, c, a + 1, layer); Face(a + 1, c, c + 1, layer);
                    }
            // Bridge sides and lobe roots meet inside the same closed tail. Closing
            // those internal borders creates coincident walls and a dark normal seam.
            // Matching sample counts weld each shared surface vertex instead.
            if (!bridge)
                for (int u = 0; u < radial; u++)
                {
                    b.Edge(first + u * (across + 1), first + (u + 1) * (across + 1), count);
                    b.Edge(first + (u + 1) * (across + 1) + across, first + u * (across + 1) + across, count);
                }
            for (int t = 0; t < across; t++)
            {
                if (bridge) b.Edge(first + t + 1, first + t, count);
                b.Edge(first + radial * (across + 1) + t, first + radial * (across + 1) + t + 1, count);
            }
        }

        static void SharkSpine(Builder b, Species sp, Model model, int side,
            float start, float end, float apexX, float height)
        {
            float Surface(float x) => CenterY(x, model) + side * model.height * Profile(x, model) * 0.98f;
            var a = new Vector3(start, Surface(start), 0);
            var c = new Vector3(end, Surface(end), 0);
            var apex = new Vector3(apexX, Surface(apexX) + side * height, 0);
            var lead = new Vector3(Mathf.Lerp(start, apexX, 0.30f), a.y + side * height * 0.70f, 0);
            // Concave trailing edge and a modest free rear tip avoid a ruler-straight triangle.
            var trail = new Vector3(Mathf.Lerp(apexX, end, 0.32f), c.y + side * height * 0.16f, 0);
            b.Membrane(sp.fin, 12, 32, side > 0 ? 2 : 3, (weight, along) =>
            {
                float x = Mathf.Lerp(start, end, along);
                var root = new Vector3(x, Surface(x), 0);
                var outline = along <= 0.46f ? SharkCurve(a, lead, apex, along / 0.46f)
                    : SharkCurve(apex, trail, c, (along - 0.46f) / 0.54f);
                var p = Vector3.Lerp(root, outline, weight);
                // Flat where it leaves the body: a camber sloping at the root shades a dark crease there.
                float camber = Mathf.Sin(weight * Mathf.PI);
                p.z = 0.045f * camber * camber * Mathf.Max(0, Mathf.Sin(along * Mathf.PI));
                return p;
            }, true, (weight, along) => SharkRimColor(sp, sp.fin, weight, along, 0.46f, side > 0 ? 0.45f : 0.35f));
        }

        // Each pectoral's root chord along the body, its tip (behind, and below the centre line in
        // body heights) and how far it reaches out from the flank.
        static (float front, float rear, Vector2 tip, float span) SharkPectoralShape(Species sp) => sp.key switch
        {
            "tiger_shark" => (0.42f, 0.08f, new Vector2(-0.10f, 1.62f), 0.28f),
            "mako_shark" => (0.38f, 0.12f, new Vector2(0.00f, 1.40f), 0.24f),
            "great_hammerhead" => (0.34f, 0.10f, new Vector2(0.02f, 1.36f), 0.24f),
            "whale_shark" => (0.52f, 0.14f, new Vector2(-0.06f, 1.52f), 0.34f),
            _ => (0.42f, 0.10f, new Vector2(-0.04f, 1.62f), 0.26f),
        };

        static Vector3 SharkPectoralRoot(Species sp, Model model, int side, float along)
        {
            var shape = SharkPectoralShape(sp);
            float x = Mathf.Lerp(shape.front, shape.rear, along);
            float profile = Profile(x, model), relativeY = -model.height * profile * Mathf.Lerp(0.38f, 0.54f, along);
            float z = model.depth * profile * Mathf.Sqrt(Mathf.Max(0.01f,
                1 - Mathf.Pow(relativeY / (model.height * profile), 2)));
            return new Vector3(x, CenterY(x, model) + relativeY, side * (z - 0.004f));
        }

        static Mesh SharkPectoral(Species sp, Model model, int side)
        {
            var b = new Builder();
            var shape = SharkPectoralShape(sp);
            var front = SharkPectoralRoot(sp, model, side, 0);
            var rear = SharkPectoralRoot(sp, model, side, 1);
            // The wings angle down and out, so the side view shows their broad sickle: a convex
            // leading edge sweeping back to a narrow tip, a concave trailing edge, and a short inner
            // margin from the rounded free rear tip back to the body.
            var tip = new Vector3(shape.tip.x, CenterY(shape.tip.x, model) - model.height * shape.tip.y, front.z + side * shape.span);
            var free = Vector3.Lerp(rear, tip, 0.10f) + new Vector3(-0.06f, 0, 0);
            var lead = new Vector3(front.x - 0.02f, Mathf.Lerp(front.y, tip.y, 0.55f), Mathf.Lerp(front.z, tip.z, 0.60f));
            var middle = (tip + free) * 0.5f;
            var trail = middle + (front - middle) * 0.22f;
            var inner = (free + rear) * 0.5f + new Vector3(-0.025f, 0, 0);
            // The wing's normal, across its chord and its drop: up and outward.
            var drop = new Vector3(0, tip.y - (front.y + rear.y) * 0.5f, tip.z - (front.z + rear.z) * 0.5f).normalized;
            var normal = new Vector3(0, side * drop.z, -side * drop.y);
            const int radial = 16, across = 40;
            const float apex = 0.40f, freeTip = 0.86f;
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        float weight = u / (float)radial, along = t / (float)across;
                        // The tip's column starts mid-chord, so the leading columns fan from the front of
                        // the root and the trailing ones from its rear without crossing.
                        float rootAlong = along <= apex ? 0.55f * along / apex : 0.55f + 0.45f * (along - apex) / (1 - apex);
                        var root = SharkPectoralRoot(sp, model, side, rootAlong);
                        var edge = along <= apex ? SharkCurve(front, lead, tip, along / apex)
                            : along <= freeTip ? SharkCurve(tip, trail, free, (along - apex) / (freeTip - apex))
                            : SharkCurve(free, inner, rear, (along - freeTip) / (1 - freeTip));
                        var p = Vector3.Lerp(root, edge, weight);
                        float chord = Mathf.Max(0, Mathf.Sin(rootAlong * Mathf.PI));
                        // A cambered foil, thick at the root and thin at the rim.
                        float thickness = Mathf.Lerp(0.016f, 0.002f, weight) * (0.30f + 0.70f * chord);
                        float camber = Mathf.Sin(weight * Mathf.PI);
                        p += normal * (0.03f * camber * camber * chord + (layer == 0 ? 1 : -1) * thickness);
                        b.Vertex(p, SharkRimColor(sp, sp.fin, weight, along, apex, 1), new Vector2(weight, side), new Vector2(weight, along));
                    }
            int count = (radial + 1) * (across + 1);
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u < radial; u++)
                    for (int t = 0; t < across; t++)
                    {
                        int a = layer * count + u * (across + 1) + t, c = a + across + 1;
                        bool forward = (side < 0) == (layer == 0);
                        if (forward) { b.Tri(a, c, a + 1); b.Tri(a + 1, c, c + 1); }
                        else { b.Tri(a, a + 1, c); b.Tri(a + 1, c + 1, c); }
                    }
            for (int u = 0; u < radial; u++)
            {
                b.Edge(u * (across + 1), (u + 1) * (across + 1), count);
                b.Edge((u + 1) * (across + 1) + across, u * (across + 1) + across, count);
            }
            for (int t = 0; t < across; t++)
            {
                b.Edge(t + 1, t, count);
                b.Edge(radial * (across + 1) + t, radial * (across + 1) + t + 1, count);
            }
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " cambered falcate pectoral");
        }

        static void SharkPelvic(Builder b, Species sp, Model model, int side)
        {
            const int radial = 8, across = 20;
            int count = (radial + 1) * (across + 1);
            // A solid foil like the pectorals', inside the shared fins mesh. Builder.Membrane
            // thickens along Z and is therefore reserved for vertical fins.
            int first = -1;
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        float weight = u / (float)radial, along = t / (float)across;
                        float x = Mathf.Lerp(-0.46f, -0.70f, along), profile = Profile(x, model);
                        float y = -model.height * profile * 0.83f;
                        float z = side * model.depth * profile * Mathf.Sqrt(1 - 0.83f * 0.83f);
                        var root = new Vector3(x, CenterY(x, model) + y, z);
                        float outline = Mathf.Pow(Mathf.Max(0, Mathf.Sin(along * Mathf.PI)), 0.78f);
                        // Like the pectorals, the pelvics angle down and out rather than lying flat.
                        var p = root + new Vector3(-0.15f, -0.12f, side * 0.12f) * weight * outline;
                        p.y += (layer == 0 ? 1 : -1) * Mathf.Lerp(0.012f, 0.002f, weight);
                        int vertex = b.Vertex(p, SharkFinColor(sp, sp.fin, 0, 0, weight * weight * 0.4f), new Vector2(weight, 3), new Vector2(weight, along));
                        if (first < 0) first = vertex;
                    }
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u < radial; u++)
                    for (int t = 0; t < across; t++)
                    {
                        int a = first + layer * count + u * (across + 1) + t, c = a + across + 1;
                        if ((side < 0) == (layer == 0)) { b.Tri(a, c, a + 1); b.Tri(a + 1, c, c + 1); }
                        else { b.Tri(a, a + 1, c); b.Tri(a + 1, c + 1, c); }
                    }
            for (int u = 0; u < radial; u++)
            {
                b.Edge(first + u * (across + 1), first + (u + 1) * (across + 1), count);
                b.Edge(first + (u + 1) * (across + 1) + across, first + u * (across + 1) + across, count);
            }
            for (int t = 0; t < across; t++)
            {
                b.Edge(first + t + 1, first + t, count);
                b.Edge(first + radial * (across + 1) + t, first + radial * (across + 1) + t + 1, count);
            }
        }

        // The cephalofoil: a flat blade across the head whose front margin runs nearly straight, with a
        // shallow notch at the middle. Its hind margin curves back into the head, so the blade is
        // deepest where it joins the body and narrows to the eyes at its swept ends. Laid out like
        // Builder.Ellipsoid with its axis across the body.
        static void AddCephalofoil(Builder b, Model model)
        {
            const int rings = 36, sides = 20;
            float y = CenterY(HammerX, model) + 0.01f;
            int start = -1;
            for (int ring = 0; ring <= rings; ring++)
            {
                float theta = ring * Mathf.PI / rings, across = Mathf.Cos(theta), round = Mathf.Pow(Mathf.Max(0, Mathf.Sin(theta)), 0.22f);
                float join = (1 - Mathf.Abs(across)) * (1 - Mathf.Abs(across));
                float ahead = 0.060f * round, behind = (0.050f + 0.100f * join) * round;
                float thickness = (0.018f + 0.014f * join) * round, notch = 0.016f * Mathf.Exp(-across * across / 0.012f);
                for (int side = 0; side <= sides; side++)
                {
                    float phi = side * U.TAU / sides, c = Mathf.Cos(phi);
                    float x = HammerMid(across) + (c > 0 ? c * (ahead - notch) : c * behind);
                    int v = b.Vertex(new Vector3(x, y + Mathf.Sin(phi) * thickness, across * HammerSpan), Color.white, Skull);
                    if (start < 0) start = v;
                    if (ring < rings && side < sides)
                    {
                        int a = start + ring * (sides + 1) + side, d = a + sides + 1;
                        b.Tri(a, d, a + 1); b.Tri(a + 1, d, d + 1);
                    }
                }
            }
        }

        // Relief added to the shark body: the whale shark's flank ridges and the hammerhead's
        // cephalofoil. Peduncle keels are part of the body's own cross section (SharkKeel).
        static void AddSharkRidges(Builder b, Species sp, Model model)
        {
            if (sp.key == "whale_shark")
                // Three ridges run along each upper flank from behind the head to the tail.
                foreach (int side in new[] { -1, 1 })
                    foreach (float level in new[] { 0.78f, 0.52f, 0.24f })
                    {
                        var ridge = new List<Vector3>();
                        for (int i = 0; i <= 24; i++)
                        {
                            float x = Mathf.Lerp(0.55f, -0.98f, i / 24f), profile = Profile(x, model);
                            ridge.Add(new Vector3(x, CenterY(x, model) + model.height * profile * level,
                                side * (model.depth * profile * Mathf.Sqrt(1 - level * level) * 0.99f + SharkKeel(sp, x) * Mathf.Exp(-level * level / 0.05f))));
                        }
                        b.Tube(ridge, t => 0.015f * Mathf.Sqrt(Mathf.Max(0, Mathf.Sin(t * Mathf.PI))), Vector2.one, Color.white, Vector2.zero, 6);
                    }
            if (sp.key == "great_hammerhead") AddCephalofoil(b, model);
        }
    }
}
