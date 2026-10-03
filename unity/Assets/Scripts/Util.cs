using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Small math / random helpers shared by every system.</summary>
    public static class U
    {
        public const float TAU = Mathf.PI * 2f;
        static readonly System.Random rng = new System.Random();

        public static float Rand() => (float)rng.NextDouble();
        public static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        public static int RandInt(int a, int bInclusive) => rng.Next(a, bInclusive + 1);
        public static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];
        public static bool Chance(float p) => rng.NextDouble() < p;

        public static float Hash(float n)
        {
            double s = Math.Sin(n * 127.1 + 311.7) * 43758.5453;
            return (float)(s - Math.Floor(s));
        }

        public static Color Hex(string h, float a = 1f)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            c.a *= a;
            return c;
        }

        public static Color WithA(Color c, float a) { c.a = a; return c; }
        public static Color Hsl(float h, float s, float l, float a = 1f)
        {
            h = ((h % 360f) + 360f) % 360f / 360f;
            float q = l < 0.5f ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            float Hue(float t)
            {
                if (t < 0) t += 1; if (t > 1) t -= 1;
                if (t < 1f / 6) return p + (q - p) * 6 * t;
                if (t < 0.5f) return q;
                if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
                return p;
            }
            return new Color(Hue(h + 1f / 3), Hue(h), Hue(h - 1f / 3), a);
        }

        public static float Smooth(float t) => t * t * (3 - 2 * t);
        public static float Damp(float rate, float dt) => Mathf.Min(1f, dt * rate);

        /// Game space is y-down (like the web prototype); Unity is y-up.
        public static Vector3 V3(float x, float y, float z = 0f) => new Vector3(x, -y, z);
    }

    /// <summary>Deterministic PRNG so the world is the same every run.</summary>
    public sealed class Mulberry
    {
        uint a;
        public Mulberry(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = a;
                t = (t ^ (t >> 15)) * (1u | t);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }
        public T Pick<T>(IList<T> list) => list[Mathf.Min(list.Count - 1, (int)(Next() * list.Count))];
    }

    public sealed class Noise1D
    {
        const int N = 512;
        readonly float[] v = new float[N];
        public Noise1D(uint seed)
        {
            var r = new Mulberry(seed);
            for (int i = 0; i < N; i++) v[i] = r.Next() * 2f - 1f;
        }
        public float At(float x)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i, s = f * f * (3 - 2 * f);
            float a = v[((i % N) + N) % N], b = v[(((i + 1) % N) + N) % N];
            return a + (b - a) * s;
        }
    }

    /// <summary>Hash-based 2D value noise in [0,1]; a period > 0 makes it tile every `period` lattice cells.</summary>
    public static class Noise2
    {
        static float Lattice(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        static int Wrap(int a, int p) => ((a % p) + p) % p;

        public static float At(float x, float y, int seed = 0, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), x1 = x0 + 1, y1 = y0 + 1;
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            if (period > 0) { x0 = Wrap(x0, period); x1 = Wrap(x1, period); y0 = Wrap(y0, period); y1 = Wrap(y1, period); }
            float a = Lattice(x0, y0, seed), b = Lattice(x1, y0, seed), c = Lattice(x0, y1, seed), d = Lattice(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int octaves = 4, int seed = 0, int period = 0)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                s += At(x, y, seed + o * 31, period) * amp;
                norm += amp; amp *= 0.5f; x *= 2; y *= 2;
                if (period > 0) period *= 2;
            }
            return s / norm;
        }

        // Tileable Gray-Scott labyrinth (F .029, k .057), baked once: ~11-cell stripe period.
        const int MZ = 64;
        static float[] maze, mazeDx, mazeDy;

        static void BakeMaze()
        {
            int n = MZ * MZ;
            float[] u = new float[n], v = new float[n], u2 = new float[n], v2 = new float[n];
            for (int i = 0; i < n; i++) u[i] = 1;
            for (int q = 0; q < 30; q++)
            {
                int cx = (int)(Lattice(q, 0, 77) * MZ), cy = (int)(Lattice(q, 1, 77) * MZ);
                for (int y = -2; y <= 2; y++)
                    for (int x = -2; x <= 2; x++)
                    {
                        int j = Wrap(cy + y, MZ) * MZ + Wrap(cx + x, MZ);
                        u[j] = 0.5f; v[j] = 0.25f + Lattice(q, x * 5 + y, 78) * 0.1f;
                    }
            }
            const float F = 0.029f, K = 0.057f;
            for (int t = 0; t < 2500; t++)
            {
                for (int y = 0; y < MZ; y++)
                {
                    int ym = Wrap(y - 1, MZ) * MZ, yp = Wrap(y + 1, MZ) * MZ, yc = y * MZ;
                    for (int x = 0; x < MZ; x++)
                    {
                        int xm = Wrap(x - 1, MZ), xp = Wrap(x + 1, MZ), i = yc + x;
                        float lu = u[ym + x] + u[yp + x] + u[yc + xm] + u[yc + xp] - 4 * u[i];
                        float lv = v[ym + x] + v[yp + x] + v[yc + xm] + v[yc + xp] - 4 * v[i];
                        float uvv = u[i] * v[i] * v[i];
                        u2[i] = u[i] + 0.2f * lu - uvv + F * (1 - u[i]);
                        v2[i] = v[i] + 0.1f * lv + uvv - (F + K) * v[i];
                    }
                }
                (u, u2) = (u2, u);
                (v, v2) = (v2, v);
            }
            maze = new float[n]; mazeDx = new float[n]; mazeDy = new float[n];
            for (int i = 0; i < n; i++) maze[i] = Mathf.Clamp01(v[i] * 3.2f);
            for (int y = 0; y < MZ; y++)
                for (int x = 0; x < MZ; x++)
                {
                    mazeDx[y * MZ + x] = (maze[y * MZ + Wrap(x + 1, MZ)] - maze[y * MZ + Wrap(x - 1, MZ)]) * 0.5f;
                    mazeDy[y * MZ + x] = (maze[Wrap(y + 1, MZ) * MZ + x] - maze[Wrap(y - 1, MZ) * MZ + x]) * 0.5f;
                }
        }

        static float Bilerp(float[] f, int x0, int y0, int x1, int y1, float fx, float fy)
            => Mathf.Lerp(Mathf.Lerp(f[y0 * MZ + x0], f[y0 * MZ + x1], fx), Mathf.Lerp(f[y1 * MZ + x0], f[y1 * MZ + x1], fx), fy);

        /// Brain-coral meanders in 0..1 (1 = ridge) at cell coords, with the slope per cell.
        public static float Maze(float x, float y, out float dx, out float dy)
        {
            if (maze == null) BakeMaze();
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            x0 = Wrap(x0, MZ); y0 = Wrap(y0, MZ);
            int x1 = (x0 + 1) % MZ, y1 = (y0 + 1) % MZ;
            dx = Bilerp(mazeDx, x0, y0, x1, y1, fx, fy);
            dy = Bilerp(mazeDy, x0, y0, x1, y1, fx, fy);
            return Bilerp(maze, x0, y0, x1, y1, fx, fy);
        }
    }

    /// <summary>Accumulates vertex-coloured triangles and writes them into a Mesh.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public void Clear() { v.Clear(); c.Clear(); uv.Clear(); t.Clear(); }
        public int Vert(float x, float y, Color col, float u = 0.5f, float w = 0.5f)
        {
            v.Add(new Vector3(x, y, 0f)); c.Add(col); uv.Add(new Vector2(u, w));
            return v.Count - 1;
        }
        public void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); }
        public void Quad(int a, int b, int d, int e) { Tri(a, b, d); Tri(a, d, e); }

        /// Quad with a uv-mapped texture (used for snow / ray sprites).
        public void TexQuad(Vector3 center, float hw, float hh, Color col)
        {
            int a = Vert(center.x - hw, center.y - hh, col, 0, 0);
            int b = Vert(center.x + hw, center.y - hh, col, 1, 0);
            int d = Vert(center.x + hw, center.y + hh, col, 1, 1);
            int e = Vert(center.x - hw, center.y + hh, col, 0, 1);
            Quad(a, b, d, e);
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetColors(c);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
        }
    }

    /// <summary>Materials and stock procedural textures.</summary>
    public static class Gfx
    {
        static Material alpha, additive;
        public static Material Alpha => alpha ??= new Material(Shader.Find("Sprites/Default"));
        public static Material Additive => additive ??= new Material(Resources.Load<Shader>("Shaders/SpriteAdditive"));

        static Texture2D white;
        public static Texture2D White
        {
            get
            {
                if (white != null) return white;
                white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                white.SetPixels(px); white.Apply();
                return white;
            }
        }

        static Sprite glow, disc, ring, bubble, arrow, rounded, vignette, spark;
        /// Soft radial falloff, radius 0.5 units.
        public static Sprite Glow => glow ??= MakeRadial(128, d => { float t = Mathf.Clamp01(1 - d); return t * t; }, 128);
        /// Hard anti-aliased disc, radius 0.5 units.
        public static Sprite Disc => disc ??= MakeRadial(64, d => Mathf.Clamp01((1 - d) * 32f), 64);
        public static Sprite Ring => ring ??= MakeRadial(128, d => Mathf.Clamp01(1 - Mathf.Abs(d - 0.9f) * 20f), 128);
        public static Sprite Spark => spark ??= MakeRadial(64, d => Mathf.Pow(Mathf.Clamp01(1 - d), 3f), 64);

        public static Sprite Bubble
        {
            get
            {
                if (bubble != null) return bubble;
                var r = new Raster(-0.5f, -0.5f, 0.5f, 0.5f, 96);
                var ringM = r.Circle(0, 0, 0.46f);
                var inner = r.Circle(0, 0, 0.38f);
                for (int i = 0; i < ringM.Length; i++) ringM[i] = Mathf.Max(0, ringM[i] - inner[i]);
                r.Paint(ringM, new Color(0.86f, 0.98f, 1f, 0.75f));
                r.Paint(inner, new Color(0.8f, 0.95f, 1f, 0.12f));
                r.Paint(r.Circle(-0.17f, -0.17f, 0.1f), new Color(1, 1, 1, 0.85f));
                return bubble = r.ToSprite(new Vector2(0.5f, 0.5f), 96);
            }
        }

        public static Sprite Arrow
        {
            get
            {
                if (arrow != null) return arrow;
                var r = new Raster(-0.6f, -0.5f, 0.6f, 0.5f, 64);
                var p = new Path().Move(0.55f, 0).Line(-0.35f, -0.45f).Line(-0.18f, 0).Line(-0.35f, 0.45f);
                var m = r.Fill(p);
                r.Paint(m, Color.white);
                return arrow = r.ToSprite(new Vector2(0.5f, 0.5f), 64);
            }
        }

        /// 9-sliceable rounded rectangle for UI panels and bars.
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int S = 64, R = 24;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(R - d + 0.5f));
                    }
                tex.SetPixels(px); tex.Apply();
                return rounded = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(R, R, R, R));
            }
        }

        public static Sprite Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int S = 256;
                var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color[S * S];
                var col = new Color(0f, 0.03f, 0.09f);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                        float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                        px[y * S + x] = new Color(col.r, col.g, col.b, a * a * 0.65f);
                    }
                tex.SetPixels(px); tex.Apply();
                return vignette = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            }
        }

        static Texture2D sand, caustics;

        /// Tileable sand grain, near-white so it can be tinted by vertex colour.
        public static Texture2D Sand
        {
            get
            {
                if (sand != null) return sand;
                const int S = 256;
                var px = new Color[S * S];
                var r = new Mulberry(55);
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        float mottle = Noise2.Fbm(u * 6, v * 6, 4, 7, 6);
                        float grain = Noise2.At(x * 0.5f, y * 0.5f, 9, S / 2);
                        float k = 0.84f + mottle * 0.2f + (grain - 0.5f) * 0.1f;
                        float sp = r.Next();
                        if (sp < 0.012f) k *= 0.72f;
                        else if (sp < 0.024f) k = 1.06f;
                        k = Mathf.Min(1, k);
                        px[y * S + x] = new Color(k, k * 0.985f, k * 0.96f, 1);
                    }
                return sand = Tiled(px, S);
            }
        }

        /// Tileable caustic net (bright Voronoi cell borders) stored in alpha.
        public static Texture2D Caustics
        {
            get
            {
                if (caustics != null) return caustics;
                const int S = 256, G = 6;
                var r = new Mulberry(31);
                var pts = new Vector2[G * G];
                for (int j = 0; j < G; j++)
                    for (int i = 0; i < G; i++)
                        pts[j * G + i] = new Vector2((i + 0.15f + r.Next() * 0.7f) / G, (j + 0.15f + r.Next() * 0.7f) / G);
                var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                        u += (Noise2.At(u * 5, v * 5, 3, 5) - 0.5f) * 0.07f;
                        v += (Noise2.At(u * 5 + 17, v * 5, 4, 5) - 0.5f) * 0.07f;
                        int ci = Mathf.FloorToInt(u * G), cj = Mathf.FloorToInt(v * G);
                        float f1 = 9, f2 = 9;
                        for (int dj = -1; dj <= 1; dj++)
                            for (int di = -1; di <= 1; di++)
                            {
                                int ii = ci + di, jj = cj + dj;
                                int wi = ((ii % G) + G) % G, wj = ((jj % G) + G) % G;
                                var p = pts[wj * G + wi] + new Vector2((ii - wi) / (float)G, (jj - wj) / (float)G);
                                float d = (p - new Vector2(u, v)).magnitude;
                                if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                            }
                        float a = Mathf.Pow(Mathf.Clamp01(1 - (f2 - f1) / 0.032f), 2.2f);
                        px[y * S + x] = new Color(1, 1, 1, a);
                    }
                return caustics = Tiled(px, S);
            }
        }

        static Texture2D Tiled(Color[] px, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        static Sprite MakeRadial(int size, Func<float, float> alphaOf, float ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    px[y * size + x] = new Color(1, 1, 1, alphaOf(d));
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
        }

        public static (GameObject go, Mesh mesh, MeshRenderer mr) MeshObject(string name, Transform parent, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            mr.sharedMaterial = additive ? Additive : Alpha;
            mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return (go, mesh, mr);
        }

        public static SpriteRenderer SpriteObject(string name, Transform parent, Sprite sprite, int order, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (additive) sr.sharedMaterial = Additive;
            return sr;
        }
    }

    /// Sorting orders, back to front.
    public static class Layer
    {
        public const int Background = -32000, Far = -31000, BgFish = -30500, Rays = -30000, Sky = -29000;
        public const int Kelp = -3000, Floor = -2900, Caustic = -2890, Rocks = -2800, Coral = -2500, Front = -2100;
        public const int Jelly = -2000, Halo = -1500, FishBase = 0, Shark = 20500, Darkness = 21000, Glow = 21500;
        public const int Pearl = 21800, Player = 22000, Shield = 22100, Particles = 23000, Snow = 23500, Vignette = 24000;
    }
}
