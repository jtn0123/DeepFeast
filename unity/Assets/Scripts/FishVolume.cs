using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Shared rounded bodies and solid fins. Both review styles use the same actual 3D rig.</summary>
    public sealed class FishVolume
    {
        public enum Look { Painted, Sculpted }
        public static Look Style { get; set; } = Look.Painted;
        sealed class Model
        {
            public Mesh body, fins, nearFin, farFin, nearEye, farEye, mouth;
            public float height, depth;
            public Vector4 head;
            public Vector3 nearNormal, farNormal;
            public Vector3 nearEyeCenter, farEyeCenter;
        }
        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
        static Material material;
        static Material Material => material ??= new Material(Resources.Load<Shader>("Shaders/FishVolume"));
        readonly MeshRenderer[] parts = new MeshRenderer[7];
        readonly MeshFilter[] filters = new MeshFilter[7];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Model model;
        Species species;
        FishArt.Art art;
        float previousYaw, angularVelocity;
        bool initialized;
        public float Yaw { get; private set; }

        public FishVolume(Transform parent)
        {
            var names = new[] { "VolumeBody", "SolidFins", "PectoralNear", "PectoralFar", "EyeNear", "EyeFar", "Mouth" };
            for (int i = 0; i < parts.Length; i++)
            {
                var go = new GameObject(names[i]); go.transform.SetParent(parent, false);
                filters[i] = go.AddComponent<MeshFilter>(); parts[i] = go.AddComponent<MeshRenderer>();
                parts[i].sharedMaterial = Material;
                parts[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts[i].receiveShadows = false;
            }
        }

        public void SetSpecies(Species sp, FishArt.Art artwork)
        {
            species = sp; art = artwork;
            ResetMotion();
            if (!cache.TryGetValue(sp.key, out model)) cache[sp.key] = model = Build(sp, artwork);
            var meshes = new[] { model.body, model.fins, model.nearFin, model.farFin, model.nearEye, model.farEye, model.mouth };
            for (int i = 0; i < parts.Length; i++) filters[i].sharedMesh = meshes[i];
        }

        public void ResetMotion() { initialized = false; angularVelocity = 0; }

        public Quaternion Rotation(float facing, float tilt, float phase, float dt)
        {
            Yaw = Mathf.Acos(Mathf.Clamp(facing, -1, 1)) * Mathf.Rad2Deg;
            if (initialized && dt > 0)
            {
                float velocity = Mathf.Clamp((Yaw - previousYaw) / dt, -240, 240);
                angularVelocity = Mathf.Lerp(angularVelocity, velocity, 1 - Mathf.Exp(-dt * 14));
            }
            initialized = true;
            previousYaw = Yaw;
            float bank = angularVelocity / 240 * (species.shape == "round" ? 7 : species.shape == "shark" ? 10 : 13);
            // Pitch in the fish's local frame before yaw, so descending fish point down on either heading.
            return Quaternion.Euler(0, Yaw, 0) * Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * Quaternion.Euler(bank + Mathf.Sin(phase) * 1.2f, 0, 0);
        }

        public void Pose(Fish f, Vector4 motion, FishView.EyeMode expression, Quaternion rotation)
        {
            // The mesh jaw supplies the open pose; keep skin coordinates stable throughout feeding.
            Sprite sprite = art.body;
            var rect = sprite.rect;
            var bounds = sprite.bounds;
            float sin = Mathf.Sin(Yaw * Mathf.Deg2Rad);
            for (int i = 0; i < parts.Length; i++)
            {
                block.Clear();
                block.SetTexture("_MainTex", sprite.texture);
                block.SetVector("_Frame", new Vector4(rect.x / sprite.texture.width, rect.y / sprite.texture.height, rect.width / sprite.texture.width, rect.height / sprite.texture.height));
                block.SetVector("_SpriteBounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
                block.SetVector("_Eye", new Vector4(art.eyePos.x, art.eyePos.y, art.eyeSize.x * 1.18f, art.eyeSize.y * 1.18f));
                block.SetColor("_Base", species.c0); block.SetColor("_Dark", species.c1); block.SetColor("_Belly", species.c2);
                block.SetColor("_Accent", species.fin);
                block.SetVector("_Head", model.head);
                block.SetFloat("_Pattern", Pattern(species.pat));
                block.SetFloat("_Style", Style == Look.Sculpted ? 1 : 0);
                block.SetFloat("_FrontView", Mathf.Pow(sin, 8));
                block.SetFloat("_Facing", f.faceS);
                block.SetFloat("_Part", i == 0 ? 0 : i < 4 ? 1 : i < 6 ? 2 : 3);
                block.SetFloat("_Expression", expression == FishView.EyeMode.Blink ? 1 : expression == FishView.EyeMode.Angry ? 2 : expression == FishView.EyeMode.Happy ? 3 : 0);
                block.SetFloat("_Phase", f.wag * motion.x);
                block.SetFloat("_TailFlex", motion.y * 1.5f);
                block.SetFloat("_Energy", Mathf.Clamp(0.6f + new Vector2(f.vx, f.vy).magnitude / Mathf.Max(1, f.r) * 0.02f, 0.6f, 1.25f));
                block.SetFloat("_TurnBend", angularVelocity / 240 * 0.22f);
                block.SetFloat("_Flutter", i == 2 || i == 3 ? motion.z * 6 : 0.016f);
                block.SetVector("_FinRoot", new Vector4(0.25f, -model.height * 0.18f, (i == 2 ? -1 : 1) * model.depth * 0.88f, i == 2 || i == 3 ? 1 : 0));
                block.SetFloat("_Mouth", f.mouth);
                block.SetFloat("_Height", model.height);
                block.SetFloat("_Fog", species == Data.Player ? 0.025f : 0.035f + Mathf.Clamp01((f.y - 900) / 3500) * 0.045f);
                block.SetColor("_FogColor", World.WaterAt(f.y));
                float visibility = 1;
                if (i == 4 || i == 5)
                {
                    var normal = rotation * (i == 4 ? model.nearNormal : model.farNormal);
                    visibility = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.12f, 0.18f, Vector3.Dot(normal, Vector3.back)));
                    block.SetVector("_EyeCenter", i == 4 ? model.nearEyeCenter : model.farEyeCenter);
                }
                block.SetFloat("_Visibility", visibility);
                parts[i].SetPropertyBlock(block);
            }
            parts[0].sortingOrder = 1; parts[1].sortingOrder = 0;
            parts[2].sortingOrder = f.faceS >= 0 ? 3 : 0;
            parts[3].sortingOrder = f.faceS >= 0 ? 0 : 3;
            parts[4].sortingOrder = parts[5].sortingOrder = 4; parts[6].sortingOrder = 5;
        }

        public void SetVisible(bool visible) { foreach (var part in parts) part.enabled = visible; }

        static int Pattern(string pattern) => pattern switch { "bands" => 1, "stripes" => 2, "tang" => 3, "spots" => 4, "scales" => 5, "bars" => 6, "player" => 7, "shark" => 8, "line" => 9, "finlets" => 10, _ => 0 };

        static Model Build(Species sp, FishArt.Art art)
        {
            float h = art.hh * (sp.shape == "shark" || sp.shape == "long" ? 0.90f : 1);
            float depth = sp.shape switch { "round" => 0.70f, "fat" => 0.46f, "disc" => 0.24f, "tall" => 0.23f, "long" => 0.16f, "slim" => 0.17f, "shark" => 0.33f, "torpedo" => 0.35f, _ => 0.43f };
            float headCenter = sp.shape == "round" ? 0.20f : sp.shape == "shark" ? 0.48f : sp.shape == "long" ? 0.52f : 0.36f;
            float headLength = sp.shape == "round" ? 1.04f : sp.shape == "shark" ? 0.90f : sp.shape == "long" ? 0.85f : 0.96f;
            var model = new Model { height = h, depth = depth, head = new Vector4(headCenter, headLength, h, depth) };
            var b = new Builder();
            const int rings = 36, sides = 28;
            for (int ring = 0; ring <= rings; ring++)
            {
                float u = ring / (float)rings, x = Mathf.Lerp(-1.04f, headCenter + headLength, u), profile = Profile(x, model.head);
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side / (float)sides * U.TAU;
                    b.Vertex(new Vector3(x, Mathf.Cos(angle) * h * profile, Mathf.Sin(angle) * depth * profile), Color.white, Vector2.zero);
                    if (ring < rings && side < sides)
                    {
                        int a = ring * (sides + 1) + side, c = a + sides + 1;
                        b.Tri(a, a + 1, c); b.Tri(a + 1, c + 1, c);
                    }
                }
            }
            int rearCap = b.Vertex(new Vector3(-1.04f, 0, 0), Color.white, Vector2.zero);
            int noseCap = b.Vertex(new Vector3(headCenter + headLength, 0, 0), Color.white, Vector2.zero);
            for (int side = 0; side < sides; side++)
            {
                b.Tri(rearCap, side + 1, side);
                b.Tri(noseCap, rings * (sides + 1) + side, rings * (sides + 1) + side + 1);
            }
            model.body = b.Mesh(sp.key + " rounded body");
            b = new Builder();
            float tailH = sp.Sh.tH * FishArt.HL;
            var tail = sp.Sh.tail == TailKind.Fan
                ? new[] { new Vector2(-0.93f, 0), new Vector2(-1.58f, tailH), new Vector2(-1.72f, 0), new Vector2(-1.58f, -tailH) }
                : new[] { new Vector2(-0.93f, 0), new Vector2(-1.7f, tailH * (sp.Sh.tail == TailKind.Shark ? 1.25f : 1)), new Vector2(-1.46f, 0.04f), new Vector2(-1.68f, -tailH * 0.9f) };
            b.Fin(tail, sp.tailCol, 0.018f, new Vector2(-0.93f, 0), p => Mathf.Clamp01((-p.x - 0.93f) / 0.75f));
            float dorsal = sp.Sh.dH * FishArt.HL;
            float Surface(float x) => h * Profile(x, model.head);
            b.Fin(new[] { new Vector2(-0.83f, Surface(-0.83f) * 0.9f), new Vector2(-0.45f, Surface(-0.45f) + dorsal), new Vector2(0.08f, Surface(0.08f) + dorsal * 0.18f), new Vector2(0.49f, Surface(0.49f) * 0.95f) }, sp.fin, 0.012f, new Vector2(0.10f, h * 0.8f), p => Mathf.Clamp01((p.y - Surface(p.x) * 0.95f) / Mathf.Max(dorsal, 0.1f)));
            float ventral = sp.Sh.aH * FishArt.HL;
            b.Fin(new[] { new Vector2(-0.7f, -Surface(-0.7f) * 0.9f), new Vector2(-0.4f, -Surface(-0.4f) - ventral), new Vector2(0.25f, -Surface(0.25f) * 0.95f) }, sp.fin, 0.012f, new Vector2(0, -h * 0.8f), p => Mathf.Clamp01((-p.y - Surface(p.x) * 0.95f) / Mathf.Max(ventral, 0.1f)));
            model.fins = b.Mesh(sp.key + " caudal dorsal ventral fins");
            model.nearFin = Pectoral(sp, depth, h, -1); model.farFin = Pectoral(sp, depth, h, 1);
            float ex = Mathf.Clamp(art.eyePos.x, 0.5f, 0.83f), ey = Mathf.Clamp(art.eyePos.y, 0.08f, h * 0.58f);
            float profileEye = Profile(ex, model.head);
            float ez = depth * profileEye * Mathf.Sqrt(Mathf.Max(0.1f, 1 - Mathf.Pow(ey / (h * profileEye), 2))) + 0.015f;
            model.nearNormal = new Vector3(0.57f, 0.12f, -0.82f).normalized;
            model.farNormal = new Vector3(0.57f, 0.12f, 0.82f).normalized;
            model.nearEyeCenter = new Vector3(ex, ey, -ez); model.farEyeCenter = new Vector3(ex, ey, ez);
            model.nearEye = Eye(sp, art, model.nearEyeCenter, model.nearNormal);
            model.farEye = Eye(sp, art, model.farEyeCenter, model.farNormal);
            model.mouth = Mouth(sp, model);
            return model;
        }

        static float Profile(float x, Vector4 head)
        {
            if (x >= head.x) return Mathf.Sqrt(Mathf.Max(0.000016f, 1 - Mathf.Pow((x - head.x) / head.y, 2)));
            return Mathf.Lerp(0.10f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-1.04f, head.x, x)));
        }

        static Mesh Pectoral(Species sp, float depth, float h, int side)
        {
            var b = new Builder();
            float span = sp.shape == "shark" ? 0.62f : 0.38f;
            const int radial = 7, across = 20;
            for (int layer = 0; layer < 2; layer++)
                for (int u = 0; u <= radial; u++)
                    for (int t = 0; t <= across; t++)
                    {
                        float weight = u / (float)radial, angle = t / (float)across;
                        var root = Vector3.Lerp(new Vector3(0.38f, h * 0.02f, side * depth * 0.84f), new Vector3(0.05f, -h * 0.53f, side * depth * 0.9f), angle);
                        float spread = Mathf.Pow(Mathf.Max(0, Mathf.Sin(angle * Mathf.PI)), 0.8f);
                        var p = root + new Vector3(-0.55f, -h * 0.08f, side * span) * spread * weight;
                        p.y += Mathf.Sin(weight * Mathf.PI) * spread * 0.065f + (layer == 0 ? 1 : -1) * 0.009f;
                        b.Vertex(p, sp.fin, new Vector2(weight, side), new Vector2(weight, angle * Mathf.PI));
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
            for (int u = 0; u < radial; u++) { b.Edge(u * (across + 1), (u + 1) * (across + 1), count); b.Edge((u + 1) * (across + 1) + across, u * (across + 1) + across, count); }
            for (int t = 0; t < across; t++) { b.Edge(t + 1, t, count); b.Edge(radial * (across + 1) + t, radial * (across + 1) + t + 1, count); }
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " pectoral fin");
        }

        static Mesh Eye(Species sp, FishArt.Art art, Vector3 center, Vector3 normal)
        {
            var b = new Builder();
            var q = Quaternion.FromToRotation(Vector3.forward, normal);
            float rx = Mathf.Clamp(art.eyeSize.x * 0.8f, 0.045f, 0.165f), ry = Mathf.Clamp(art.eyeSize.y * 0.8f, 0.05f, 0.17f);
            b.Ellipsoid(center, new Vector3(rx * 1.10f, ry * 1.10f, 0.035f), q, Color.Lerp(sp.c1, sp.c0, 0.65f), 10, 14);
            b.Ellipsoid(center + normal * 0.018f, new Vector3(rx, ry, 0.038f), q, U.Hex("#f6f8e9"), 10, 14);
            Color iris = sp == Data.Shark ? U.Hex("#866c3c") : Color.Lerp(sp.c1, U.Hex("#327c82"), 0.65f);
            b.Ellipsoid(center + normal * 0.053f, new Vector3(rx * 0.60f, ry * 0.64f, 0.014f), q, iris, 10, 14);
            b.Ellipsoid(center + normal * 0.065f, new Vector3(rx * 0.38f, ry * 0.48f, 0.016f), q, U.Hex("#071d2d"), 8, 12);
            b.Ellipsoid(center + normal * 0.082f + q * new Vector3(-rx * 0.15f, ry * 0.20f, 0), new Vector3(rx * 0.12f, ry * 0.11f, 0.006f), q, Color.white, 6, 10);
            return b.Mesh(sp.key + " globe eye");
        }

        static Mesh Mouth(Species sp, Model model)
        {
            var b = new Builder();
            const int sides = 32;
            int Vertex(Vector2 surface, Color color)
            {
                float z = surface.x * model.depth * 0.74f;
                float y = model.height * (-0.20f + 0.13f * surface.x * surface.x + 0.018f * surface.y);
                float x = model.head.x + model.head.y * Mathf.Sqrt(Mathf.Max(0.02f, 1 - Mathf.Pow(y / model.height, 2) - Mathf.Pow(z / model.depth, 2))) + 0.008f;
                return b.Vertex(new Vector3(x, y, z), color, Vector2.zero, surface);
            }
            int center = Vertex(Vector2.zero, U.Hex("#102533"));
            foreach (float radius in new[] { 0.45f, 0.78f, 0.90f, 1f })
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * U.TAU / sides;
                    Vertex(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, U.Hex("#102533"));
                }
            for (int side = 0; side < sides; side++) b.Tri(center, 1 + (side + 1) % sides, 1 + side);
            for (int ring = 0; ring < 3; ring++)
                for (int side = 0; side < sides; side++)
                {
                    int a = 1 + ring * sides + side, next = 1 + ring * sides + (side + 1) % sides;
                    b.Tri(a, next, a + sides); b.Tri(next, next + sides, a + sides);
                }
            if (sp == Data.Shark || sp.shape == "long")
                for (int tooth = 0; tooth < 6; tooth++)
                {
                    float z = Mathf.Lerp(-0.66f, 0.66f, tooth / 5f);
                    int a = Vertex(new Vector2(z - 0.08f, 0.65f), Color.white);
                    int c = Vertex(new Vector2(z, 0.20f), Color.white);
                    int d = Vertex(new Vector2(z + 0.08f, 0.65f), Color.white);
                    b.Tri(a, d, c);
                }
            return b.Mesh(sp.key + " fitted lip cavity and teeth");
        }

        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Vector2> flex = new List<Vector2>();
            readonly List<Vector2> surface = new List<Vector2>();
            readonly List<int> triangles = new List<int>();
            public int Vertex(Vector3 v, Color color, Vector2 weight, Vector2 uv = default)
            {
                vertices.Add(v); colors.Add(new Color(color.r, color.g, color.b, 1)); flex.Add(weight); surface.Add(uv);
                return vertices.Count - 1;
            }
            public void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, surface); mesh.SetUVs(1, flex); mesh.SetTriangles(triangles, 0);
                // Weld coincident seam positions for normals without welding their UV coordinates.
                var sums = new Dictionary<Vector3Int, Vector3>();
                Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), Mathf.RoundToInt(p.z * 100000));
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    var a = vertices[triangles[i]]; var c = vertices[triangles[i + 1]]; var d = vertices[triangles[i + 2]];
                    var normal = Vector3.Cross(c - a, d - a);
                    foreach (int index in new[] { triangles[i], triangles[i + 1], triangles[i + 2] })
                    {
                        var key = Key(vertices[index]); sums.TryGetValue(key, out var sum); sums[key] = sum + normal;
                    }
                }
                var normals = new List<Vector3>(vertices.Count);
                foreach (var vertex in vertices) { sums.TryGetValue(Key(vertex), out var sum); normals.Add(sum.sqrMagnitude > 1e-15f ? sum.normalized : Vector3.right); }
                mesh.SetNormals(normals); mesh.RecalculateBounds();
                // Include shader-driven flex so Unity cannot cull a bent tail at the edge of the viewport.
                var bounds = mesh.bounds; bounds.Expand(new Vector3(0.15f, 0.5f, 0.9f)); mesh.bounds = bounds;
                mesh.UploadMeshData(true); return mesh;
            }
            public void Fin(Vector2[] polygon, Color color, float thickness, Vector2 origin, System.Func<Vector2, float> rootWeight)
            {
                // Round each corner with a short quadratic arc and keep a consistent outward winding.
                var rounded = new List<Vector2>();
                for (int i = 0; i < polygon.Length; i++)
                {
                    var previous = polygon[(i + polygon.Length - 1) % polygon.Length];
                    var next = polygon[(i + 1) % polygon.Length];
                    var a = Vector2.Lerp(polygon[i], previous, 0.22f); var c = Vector2.Lerp(polygon[i], next, 0.22f);
                    for (int j = 0; j < 5; j++) { float t = j / 4f; rounded.Add((1-t)*(1-t)*a + 2*(1-t)*t*polygon[i] + t*t*c); }
                }
                polygon = rounded.ToArray();
                float area = 0;
                for (int i = 0; i < polygon.Length; i++) { var a = polygon[i]; var c = polygon[(i + 1) % polygon.Length]; area += a.x*c.y - c.x*a.y; }
                if (area < 0) System.Array.Reverse(polygon);
                var faces = Triangulate(polygon);
                var shared = new Dictionary<Vector3Int, int>();
                int Point(Vector2 p, int side)
                {
                    var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), side);
                    if (shared.TryGetValue(key, out var index)) return index;
                    float weight = rootWeight(p);
                    float camber = Mathf.Sin(weight * Mathf.PI) * 0.035f;
                    float z = camber + (side == 0 ? -1 : 1) * thickness * (0.5f + 0.5f * Mathf.Sin(weight * Mathf.PI));
                    index = Vertex(new Vector3(p.x, p.y, z), color, new Vector2(weight, 0), new Vector2(weight, Mathf.Atan2(p.y - origin.y, p.x - origin.x)));
                    shared[key] = index; return index;
                }
                const int divisions = 3;
                for (int side = 0; side < 2; side++)
                    for (int face = 0; face < faces.Count; face += 3)
                    {
                        var a = polygon[faces[face]]; var c = polygon[faces[face + 1]]; var d = polygon[faces[face + 2]];
                        int At(int i, int j) => Point(a + (c - a) * (i / (float)divisions) + (d - a) * (j / (float)divisions), side);
                        void Face(int x, int y, int z) { if (side == 0) Tri(x, z, y); else Tri(x, y, z); }
                        for (int i = 0; i < divisions; i++)
                            for (int j = 0; j < divisions - i; j++)
                            {
                                Face(At(i, j), At(i + 1, j), At(i, j + 1));
                                if (i + j < divisions - 1) Face(At(i + 1, j), At(i + 1, j + 1), At(i, j + 1));
                            }
                    }
                for (int i = 0; i < polygon.Length; i++)
                {
                    var a = polygon[i]; var c = polygon[(i + 1) % polygon.Length];
                    for (int step = 0; step < divisions; step++)
                    {
                        var p = Vector2.Lerp(a, c, step / (float)divisions); var q = Vector2.Lerp(a, c, (step + 1) / (float)divisions);
                        Tri(Point(p, 0), Point(q, 0), Point(p, 1)); Tri(Point(q, 0), Point(q, 1), Point(p, 1));
                    }
                }
            }

            static List<int> Triangulate(Vector2[] polygon)
            {
                var remaining = new List<int>(); var result = new List<int>();
                for (int i = 0; i < polygon.Length; i++) remaining.Add(i);
                float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                while (remaining.Count > 3)
                {
                    bool clipped = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        int a = remaining[(i + remaining.Count - 1) % remaining.Count], c = remaining[i], d = remaining[(i + 1) % remaining.Count];
                        if (Cross(polygon[c] - polygon[a], polygon[d] - polygon[c]) <= 1e-9f) continue;
                        bool contains = false;
                        foreach (int index in remaining)
                        {
                            if (index == a || index == c || index == d) continue;
                            var p = polygon[index];
                            if (Cross(polygon[c] - polygon[a], p - polygon[a]) >= -1e-9f && Cross(polygon[d] - polygon[c], p - polygon[c]) >= -1e-9f && Cross(polygon[a] - polygon[d], p - polygon[d]) >= -1e-9f) { contains = true; break; }
                        }
                        if (contains) continue;
                        result.Add(a); result.Add(c); result.Add(d); remaining.RemoveAt(i); clipped = true; break;
                    }
                    if (!clipped) throw new System.InvalidOperationException("Cannot triangulate fish fin contour.");
                }
                result.AddRange(remaining); return result;
            }
            public void Edge(int a, int c, int offset) { Tri(a, c, a + offset); Tri(c, c + offset, a + offset); }

            public void Ellipsoid(Vector3 center, Vector3 radius, Quaternion rotation, Color color, int rings = 12, int sides = 16)
            {
                int start = vertices.Count;
                for (int ring = 0; ring <= rings; ring++)
                {
                    float theta = ring * Mathf.PI / rings;
                    for (int side = 0; side <= sides; side++)
                    {
                        float phi = side * U.TAU / sides;
                        var v = new Vector3(Mathf.Cos(theta) * radius.x, Mathf.Sin(theta) * Mathf.Cos(phi) * radius.y, Mathf.Sin(theta) * Mathf.Sin(phi) * radius.z);
                        Vertex(center + rotation * v, color, Vector2.zero);
                        if (ring < rings && side < sides)
                        {
                            int a = start + ring * (sides + 1) + side, c = a + sides + 1;
                            Tri(a, c, a + 1); Tri(a + 1, c, c + 1);
                        }
                    }
                }
            }
        }
    }
}
