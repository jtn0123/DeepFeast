using UnityEngine;

namespace DeepFeast
{
    // Authored silhouettes for lamnid and requiem sharks. The seven cached parts and
    // shader flex contract remain shared with FishVolume's other species.
    public sealed partial class FishVolume
    {
        static Model SharkBody(Species sp, FishArt.Art art)
        {
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            float height = art.hh * (tiger ? 1.11f : mako ? 0.87f : 1.03f);
            float depth = tiger ? 0.36f : mako ? 0.225f : 0.315f;
            // A white shark's short cone belongs to a thick torpedo, while the very
            // pointed nose belongs to the mako. The tiger keeps a fuller front half.
            return new Model
            {
                height = height, depth = depth,
                head = new Vector4(tiger ? 0.15f : mako ? -0.06f : 0.06f,
                    tiger ? 1.08f : mako ? 1.48f : 1.24f, height, depth),
                profile = new Vector4(tiger ? 1.55f : mako ? 1.55f : 1.32f,
                    tiger ? 0.40f : mako ? 0.90f : 0.63f,
                    tiger ? 0.20f : mako ? 0.16f : 0.18f, tiger ? 0.008f : 0.025f),
            };
        }

        static Vector3 SharkCurve(Vector3 a, Vector3 control, Vector3 b, float t)
        {
            float s = 1 - t;
            return s * s * a + 2 * s * t * control + t * t * b;
        }

        static void BuildSharkFins(Builder b, Species sp, Model model)
        {
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            SharkCaudal(b, sp, model);

            SharkSpine(b, sp, model, 1, tiger ? -0.03f : 0.10f,
                tiger ? -0.68f : -0.55f, tiger ? -0.34f : mako ? -0.30f : -0.19f,
                tiger ? 0.38f : mako ? 0.39f : 0.43f);
            // Second dorsal and anal fins are distinctly smaller than the first.
            SharkSpine(b, sp, model, 1, -0.73f, -0.99f, -0.87f, tiger ? 0.14f : 0.105f);
            SharkSpine(b, sp, model, -1, -0.76f, -1.01f, -0.90f, tiger ? 0.13f : 0.085f);
            foreach (int side in new[] { -1, 1 }) SharkPelvic(b, sp, model, side);
        }

        static void SharkCaudal(Builder b, Species sp, Model model)
        {
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            float stalk = model.height * model.profile.z, notchX = tiger ? -1.33f : -1.36f;
            var notch = new Vector3(notchX, 0, 0);
            // Close the small triangle between the peduncle and the tail's inner fork.
            SharkTailPatch(b, sp, 20, 20, (weight, along) =>
                Vector3.Lerp(new Vector3(-1.04f, (along * 2 - 1) * stalk, 0), notch, weight), true);
            foreach (int side in new[] { -1, 1 })
            {
                float height = side > 0 ? tiger ? 0.86f : 0.65f
                    : tiger ? 0.46f : mako ? 0.61f : 0.59f;
                float tipX = side > 0 ? tiger ? -2.06f : mako ? -1.89f : -1.86f
                    : tiger ? -1.86f : mako ? -1.87f : -1.83f;
                var leadingRoot = new Vector3(-1.04f, side * stalk, 0);
                var leadingControl = new Vector3(tiger && side > 0 ? -1.47f : -1.39f,
                    side * height * 0.88f, 0);
                var trailingControl = new Vector3(tiger && side > 0 ? -1.56f : -1.44f,
                    side * height * 0.37f, 0);
                SharkTailPatch(b, sp, 20, 20, (weight, across) =>
                {
                    // A short rounded rim joins independently curved leading and trailing
                    // boundaries. A linear root-to-tip fan produced the former straight V.
                    var tip = new Vector3(tipX - 0.025f * Mathf.Max(0, Mathf.Sin(across * Mathf.PI)),
                        side * (height - across * 0.026f), 0);
                    var leading = SharkCurve(leadingRoot, leadingControl, tip, weight);
                    var trailing = SharkCurve(notch, trailingControl, tip, weight);
                    var p = Vector3.Lerp(leading, trailing, across);
                    if (tiger && side > 0)
                        p.x += 0.075f * across * across * Mathf.Exp(-Mathf.Pow((weight - 0.78f) / 0.07f, 2));
                    p.z = 0.040f * Mathf.Sin(weight * Mathf.PI) * Mathf.Max(0, Mathf.Sin(across * Mathf.PI));
                    return p;
                });
            }
        }

        static void SharkTailPatch(Builder b, Species sp, int radial, int across,
            System.Func<float, float, Vector3> point, bool bridge = false)
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
                        p.z += (layer == 0 ? -1 : 1) * Mathf.Lerp(0.035f, 0.004f, Mathf.Sqrt(flex));
                        int local = layer * count + u * (across + 1) + t;
                        points[local] = p;
                        int index = b.Vertex(p, FinColor(sp.tailCol, flex, true), new Vector2(flex, 4), new Vector2(flex, phase));
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
                p.z = 0.045f * Mathf.Sin(weight * Mathf.PI) * Mathf.Max(0, Mathf.Sin(along * Mathf.PI));
                return p;
            }, true);
        }

        static Vector3 SharkPectoralRoot(Species sp, Model model, int side, float along)
        {
            bool tiger = sp.key == "tiger_shark";
            float x = Mathf.Lerp(tiger ? 0.16f : 0.24f, tiger ? -0.24f : -0.16f, along);
            float profile = Profile(x, model), relativeY = -model.height * profile * Mathf.Lerp(0.30f, 0.56f, along);
            float z = model.depth * profile * Mathf.Sqrt(Mathf.Max(0.01f,
                1 - Mathf.Pow(relativeY / (model.height * profile), 2)));
            return new Vector3(x, CenterY(x, model) + relativeY, side * (z - 0.004f));
        }

        static Mesh SharkPectoral(Species sp, Model model, int side)
        {
            var b = new Builder();
            bool tiger = sp.key == "tiger_shark", mako = sp.key == "mako_shark";
            float span = tiger ? 0.62f : mako ? 0.48f : 0.57f;
            var front = SharkPectoralRoot(sp, model, side, 0);
            var rear = SharkPectoralRoot(sp, model, side, 1);
            var tip = new Vector3(tiger ? -0.56f : mako ? -0.60f : -0.46f,
                -model.height * (tiger ? 0.72f : mako ? 0.70f : 0.78f), front.z + side * span);
            var lead = new Vector3(Mathf.Lerp(front.x, tip.x, 0.32f), front.y - 0.075f,
                front.z + side * span * 0.73f);
            var trail = new Vector3(Mathf.Lerp(tip.x, rear.x, 0.45f), rear.y - 0.035f,
                rear.z + side * span * 0.33f);
            const int radial = 14, across = 32;
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        float weight = u / (float)radial, along = t / (float)across;
                        var root = SharkPectoralRoot(sp, model, side, along);
                        var edge = along <= 0.43f ? SharkCurve(front, lead, tip, along / 0.43f)
                            : SharkCurve(tip, trail, rear, (along - 0.43f) / 0.57f);
                        var p = Vector3.Lerp(root, edge, weight);
                        float chord = Mathf.Max(0, Mathf.Sin(along * Mathf.PI));
                        p.y += 0.035f * Mathf.Sin(weight * Mathf.PI) * chord;
                        // Thickness normal to the horizontal wing, with a convex foil and thin rim.
                        float thickness = Mathf.Lerp(0.023f, 0.002f, weight) * (0.30f + 0.70f * chord);
                        p.y += (layer == 0 ? 1 : -1) * thickness;
                        b.Vertex(p, FinColor(sp.fin, weight, true), new Vector2(weight, side), new Vector2(weight, along));
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
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " cambered swept pectoral");
        }

        static void SharkPelvic(Builder b, Species sp, Model model, int side)
        {
            const int radial = 8, across = 20;
            int count = (radial + 1) * (across + 1);
            // Use the same solid horizontal foil as the pectorals, inside the shared fins mesh.
            // Builder.Membrane thickens along Z and is therefore reserved for vertical fins.
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
                        var p = root + new Vector3(-0.17f, -0.045f, side * 0.22f) * weight * outline;
                        p.y += (layer == 0 ? 1 : -1) * Mathf.Lerp(0.012f, 0.002f, weight);
                        int vertex = b.Vertex(p, FinColor(sp.fin, weight, true), new Vector2(weight, 3), new Vector2(weight, along));
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

        static void AddSharkKeels(Builder b, Species sp, Model model)
        {
            bool tiger = sp.key == "tiger_shark";
            // Low cartilage ridges blend into the peduncle, well before the tail fin.
            foreach (int side in new[] { -1, 1 })
                b.Ellipsoid(new Vector3(-0.94f, CenterY(-0.94f, model), side * model.depth * Profile(-0.94f, model) * 0.98f),
                    new Vector3(0.14f, tiger ? 0.012f : 0.018f, tiger ? 0.025f : 0.046f),
                    Quaternion.identity, Color.white, 8, 16);
        }
    }
}
