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
        float previousYaw, turnSign = 1;
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
            if (!cache.TryGetValue(sp.key, out model)) cache[sp.key] = model = Build(sp, artwork);
            var meshes = new[] { model.body, model.fins, model.nearFin, model.farFin, model.nearEye, model.farEye, model.mouth };
            for (int i = 0; i < parts.Length; i++) filters[i].sharedMesh = meshes[i];
        }

        public Quaternion Rotation(float facing, float tilt, float phase)
        {
            Yaw = Mathf.Acos(Mathf.Clamp(facing, -1, 1)) * Mathf.Rad2Deg;
            float delta = Yaw - previousYaw;
            if (Mathf.Abs(delta) > 0.001f) turnSign = Mathf.Sign(delta);
            previousYaw = Yaw;
            float bank = Mathf.Sin(Yaw * Mathf.Deg2Rad) * turnSign * 13;
            // Pitch in the fish's local frame before yaw, so descending fish point down on either heading.
            return Quaternion.Euler(0, Yaw, 0) * Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * Quaternion.Euler(bank + Mathf.Sin(phase) * 1.2f, 0, 0);
        }

        public void Pose(Fish f, Vector4 motion, FishView.EyeMode expression, Quaternion rotation)
        {
            Sprite sprite = f.mouth > 0.4f ? art.bodyOpen : art.body;
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
                block.SetFloat("_Pattern", Pattern(species.pat));
                block.SetFloat("_Style", Style == Look.Sculpted ? 1 : 0);
                block.SetFloat("_FrontView", Mathf.Pow(sin, 8));
                block.SetFloat("_Facing", f.faceS);
                block.SetFloat("_Part", i == 0 ? 0 : i < 4 ? 1 : i < 6 ? 2 : 3);
                block.SetFloat("_Expression", expression == FishView.EyeMode.Blink ? 1 : expression == FishView.EyeMode.Angry ? 2 : expression == FishView.EyeMode.Happy ? 3 : 0);
                block.SetFloat("_Phase", f.wag * motion.x);
                block.SetFloat("_TailFlex", motion.y * 1.5f);
                block.SetFloat("_Energy", Mathf.Clamp(0.6f + new Vector2(f.vx, f.vy).magnitude / Mathf.Max(1, f.r) * 0.02f, 0.6f, 1.25f));
                block.SetFloat("_TurnBend", sin * turnSign * 0.22f);
                block.SetFloat("_Flutter", i == 2 || i == 3 ? motion.z * 3 : 0.012f);
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

        static int Pattern(string pattern) => pattern switch { "bands" => 1, "stripes" => 2, "tang" => 3, "spots" => 4, "scales" => 5, "bars" => 6, "player" => 7, _ => 0 };

        static Model Build(Species sp, FishArt.Art art)
        {
            float h = art.hh;
            float depth = sp.shape switch { "round" => 0.66f, "fat" => 0.46f, "disc" => 0.24f, "tall" => 0.23f, "long" => 0.14f, "slim" => 0.17f, "shark" => 0.32f, "torpedo" => 0.33f, _ => 0.38f };
            var model = new Model { height = h, depth = depth };
            var b = new Builder();
            const int rings = 32, sides = 24;
            for (int ring = 0; ring <= rings; ring++)
            {
                float u = ring / (float)rings, x = Mathf.Lerp(-1.02f, 1.22f, u), profile = Profile(u);
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
            model.body = b.Mesh(sp.key + " rounded body");
            b = new Builder();
            float tailH = sp.Sh.tH * FishArt.HL;
            var tail = sp.Sh.tail == TailKind.Fan
                ? new[] { new Vector2(-0.93f, 0), new Vector2(-1.58f, tailH), new Vector2(-1.72f, 0), new Vector2(-1.58f, -tailH) }
                : new[] { new Vector2(-0.93f, 0), new Vector2(-1.7f, tailH * (sp.Sh.tail == TailKind.Shark ? 1.25f : 1)), new Vector2(-1.46f, 0.04f), new Vector2(-1.68f, -tailH * 0.9f) };
            b.Fin(tail, sp.tailCol, 0, 0.025f);
            float dorsal = sp.Sh.dH * FishArt.HL;
            float Surface(float x) => h * Profile(Mathf.InverseLerp(-1.02f, 1.22f, x));
            b.Fin(new[] { new Vector2(-0.83f, Surface(-0.83f) * 0.85f), new Vector2(-0.45f, Surface(-0.45f) + dorsal), new Vector2(0.08f, Surface(0.08f) + dorsal * 0.18f), new Vector2(0.49f, Surface(0.49f) * 0.9f) }, sp.fin, 0, 0.018f);
            b.Fin(new[] { new Vector2(-0.7f, -Surface(-0.7f) * 0.85f), new Vector2(-0.4f, -Surface(-0.4f) - sp.Sh.aH * FishArt.HL), new Vector2(0.25f, -Surface(0.25f) * 0.9f) }, sp.fin, 0, 0.018f);
            model.fins = b.Mesh(sp.key + " caudal dorsal ventral fins");
            model.nearFin = Pectoral(sp, depth, h, -1); model.farFin = Pectoral(sp, depth, h, 1);
            float ex = Mathf.Clamp(art.eyePos.x, 0.5f, 0.83f), ey = Mathf.Clamp(art.eyePos.y, 0.08f, h * 0.58f);
            float profileEye = Profile(Mathf.InverseLerp(-1.02f, 1.22f, ex));
            float ez = depth * profileEye * Mathf.Sqrt(Mathf.Max(0.1f, 1 - Mathf.Pow(ey / (h * profileEye), 2))) + 0.015f;
            model.nearNormal = new Vector3(0.57f, 0.12f, -0.82f).normalized;
            model.farNormal = new Vector3(0.57f, 0.12f, 0.82f).normalized;
            model.nearEyeCenter = new Vector3(ex, ey, -ez); model.farEyeCenter = new Vector3(ex, ey, ez);
            model.nearEye = Eye(sp, art, model.nearEyeCenter, model.nearNormal);
            model.farEye = Eye(sp, art, model.farEyeCenter, model.farNormal);
            b = new Builder();
            b.Ellipsoid(new Vector3(1.22f, -h * 0.17f, 0), new Vector3(0.035f, h * 0.10f, depth * 0.35f), Quaternion.identity, U.Hex("#123041"));
            for (int side = -1; side <= 1; side += 2)
            {
                var normal = new Vector3(0.85f, 0, side * 0.55f).normalized;
                b.Ellipsoid(new Vector3(1.07f, -h * 0.17f, side * depth * 0.35f), new Vector3(0.10f, h * 0.027f, 0.012f), Quaternion.FromToRotation(Vector3.forward, normal), U.Hex("#123041"));
            }
            model.mouth = b.Mesh(sp.key + " muzzle mouth");
            return model;
        }

        static float Profile(float u) => Mathf.Max(0.006f, Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), 0.62f) * Mathf.Lerp(0.60f, 1.13f, u));

        static Mesh Pectoral(Species sp, float depth, float h, int side)
        {
            var b = new Builder();
            float span = sp.shape == "shark" ? 0.62f : 0.38f;
            var points = new[] { new Vector3(0.36f, -h * 0.08f, side * depth * 0.82f), new Vector3(-0.45f, -h * 0.60f, side * (depth + span)), new Vector3(-0.15f, -h * 0.65f, side * depth * 0.85f) };
            b.SolidTriangle(points, sp.fin, side);
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " pectoral fin");
        }

        static Mesh Eye(Species sp, FishArt.Art art, Vector3 center, Vector3 normal)
        {
            var b = new Builder();
            var q = Quaternion.FromToRotation(Vector3.forward, normal);
            float rx = Mathf.Clamp(art.eyeSize.x * 0.8f, 0.045f, 0.165f), ry = Mathf.Clamp(art.eyeSize.y * 0.8f, 0.05f, 0.17f);
            b.Ellipsoid(center, new Vector3(rx, ry, 0.055f), q, U.Hex("#f6f8e9"));
            b.Ellipsoid(center + normal * 0.051f, new Vector3(rx * 0.50f, ry * 0.58f, 0.024f), q, U.Hex("#071d2d"));
            b.Ellipsoid(center + normal * 0.074f + q * new Vector3(-rx * 0.17f, ry * 0.21f, 0), new Vector3(rx * 0.16f, ry * 0.15f, 0.01f), q, Color.white);
            return b.Mesh(sp.key + " globe eye");
        }

        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Color> colors = new List<Color>();
            readonly List<Vector2> flex = new List<Vector2>();
            readonly List<int> triangles = new List<int>();
            public void Vertex(Vector3 v, Color color, Vector2 weight) { vertices.Add(v); colors.Add(new Color(color.r, color.g, color.b, 1)); flex.Add(weight); }
            public void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(1, flex); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                // Include shader-driven flex so Unity cannot cull a bent tail at the edge of the viewport.
                var bounds = mesh.bounds; bounds.Expand(new Vector3(0.1f, 0.15f, 0.85f)); mesh.bounds = bounds;
                mesh.UploadMeshData(true); return mesh;
            }
            public void Fin(Vector2[] polygon, Color color, float z, float thickness)
            {
                // Round each corner with a short quadratic arc and keep a consistent outward winding.
                var rounded = new List<Vector2>();
                for (int i = 0; i < polygon.Length; i++)
                {
                    var previous = polygon[(i + polygon.Length - 1) % polygon.Length];
                    var next = polygon[(i + 1) % polygon.Length];
                    var a = Vector2.Lerp(polygon[i], previous, 0.12f); var c = Vector2.Lerp(polygon[i], next, 0.12f);
                    for (int j = 0; j < 4; j++) { float t = j / 3f; rounded.Add((1-t)*(1-t)*a + 2*(1-t)*t*polygon[i] + t*t*c); }
                }
                polygon = rounded.ToArray();
                float area = 0;
                for (int i = 0; i < polygon.Length; i++) { var a = polygon[i]; var c = polygon[(i + 1) % polygon.Length]; area += a.x*c.y - c.x*a.y; }
                if (area < 0) System.Array.Reverse(polygon);
                int start = vertices.Count, n = polygon.Length;
                for (int side = 0; side < 2; side++)
                    for (int i = 0; i < n; i++) Vertex(new Vector3(polygon[i].x, polygon[i].y, z + (side == 0 ? -thickness : thickness)), color, new Vector2(i == 0 ? 0 : 1, 0));
                for (int i = 1; i < n - 1; i++) { Tri(start, start + i + 1, start + i); Tri(start + n, start + n + i, start + n + i + 1); }
                for (int i = 0; i < n; i++) { int j = (i + 1) % n; Tri(start + i, start + j, start + n + i); Tri(start + j, start + n + j, start + n + i); }
            }
            public void SolidTriangle(Vector3[] p, Color color, int side)
            {
                int start = vertices.Count;
                for (int layer = 0; layer < 2; layer++) for (int i = 0; i < 3; i++) Vertex(p[i] + new Vector3(0, (layer == 0 ? -1 : 1) * 0.015f, 0), color, new Vector2(i == 0 ? 0 : 1, side));
                Tri(start, start + 2, start + 1); Tri(start + 3, start + 4, start + 5);
                for (int i = 0; i < 3; i++) { int j = (i + 1) % 3; Tri(start + i, start + j, start + i + 3); Tri(start + j, start + j + 3, start + i + 3); }
            }
            public void Ellipsoid(Vector3 center, Vector3 radius, Quaternion rotation, Color color)
            {
                const int rings = 12, sides = 16;
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
