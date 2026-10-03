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
        public const int Background = -32000, Far = -31000, Rays = -30000, Sky = -29000;
        public const int Kelp = -3000, Floor = -2900, Caustic = -2890, Rocks = -2800, Coral = -2500, Front = -2100;
        public const int Jelly = -2000, Halo = -1500, FishBase = 0, Shark = 20500, Darkness = 21000, Glow = 21500;
        public const int Pearl = 21800, Player = 22000, Shield = 22100, Particles = 23000, Snow = 23500, Vignette = 24000;
    }
}
