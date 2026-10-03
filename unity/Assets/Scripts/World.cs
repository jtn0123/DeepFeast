using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Seabed, decor and everything that lives in world space but isn't a creature.</summary>
    public sealed class World
    {
        public const float W = 14000f, H = 4400f, FSTEP = 20f;
        float[] floorS;

        static readonly float[,] PROFILE =
        {
            {0, 3600}, {1500, 3950}, {2600, 2950}, {3800, 2500}, {4700, 1350}, {5500, 950}, {6300, 1150},
            {7000, 1900}, {7800, 1650}, {8600, 1050}, {9300, 1500}, {10200, 3300}, {11000, 4100}, {11800, 3950},
            {12600, 2800}, {13400, 2400}, {14000, 2650},
        };

        static readonly Color[] WATER_C =
        {
            C(72, 202, 230), C(28, 150, 198), C(14, 98, 156), C(9, 62, 114), C(6, 36, 78), C(4, 20, 46),
        };
        static readonly float[] WATER_Y = { 0, 700, 1700, 2800, 3800, 4600 };
        static Color C(int r, int g, int b, float a = 1) => new Color(r / 255f, g / 255f, b / 255f, a);

        public static Color WaterAt(float y)
        {
            if (y <= 0) return WATER_C[0];
            for (int i = 0; i < WATER_Y.Length - 1; i++)
                if (y <= WATER_Y[i + 1]) return Color.Lerp(WATER_C[i], WATER_C[i + 1], (y - WATER_Y[i]) / (WATER_Y[i + 1] - WATER_Y[i]));
            return WATER_C[WATER_C.Length - 1];
        }
        public static float DarkAt(float y) => Mathf.Pow(Mathf.Clamp01((y - 900f) / 3300f), 1.2f) * 0.58f;

        static float ProfileY(float x)
        {
            int n = PROFILE.GetLength(0);
            for (int i = 0; i < n - 1; i++)
            {
                float x0 = PROFILE[i, 0], y0 = PROFILE[i, 1], x1 = PROFILE[i + 1, 0], y1 = PROFILE[i + 1, 1];
                if (x <= x1) { float t = (x - x0) / (x1 - x0); return Mathf.Lerp(y0, y1, t * t * (3 - 2 * t)); }
            }
            return PROFILE[n - 1, 1];
        }

        public float FloorY(float x)
        {
            float fx = Mathf.Clamp(x, 0, W) / FSTEP;
            int i = (int)fx;
            float f = fx - i;
            return Mathf.Lerp(floorS[i], floorS[Mathf.Min(i + 1, floorS.Length - 1)], f);
        }

        public bool InWater(float x, float y, float r) => x > 120 && x < W - 120 && y > 70 + r && y < FloorY(x) - r - 25;

        public Vector2 FindStart()
        {
            float best = W / 2;
            for (float x = 2500; x <= W - 2500; x += 40) if (FloorY(x) < FloorY(best)) best = x;
            return new Vector2(best, Mathf.Max(160, FloorY(best) - 260));
        }

        // ------------------------------------------------------------------ decor data
        sealed class Kelp { public float x, y, h, w, phase, tone; }
        sealed class Grass { public float x, y, s, phase; public int n; }
        public sealed class Anemone { public float x, y, s, hue, phase; public bool deep; public int n; }
        sealed class Swayer { public Transform t; public float amp, speed, phase; }

        readonly List<Kelp> kelps = new List<Kelp>();
        readonly List<Grass> grasses = new List<Grass>();
        public readonly List<Anemone> anemones = new List<Anemone>();
        public readonly List<Anemone> glowAnemones = new List<Anemone>();
        readonly List<Swayer> swayers = new List<Swayer>();

        static readonly string[] CORAL = { "#ff6f91", "#ff9f5a", "#c77dff", "#ff4d6d", "#48cae4", "#ffd166", "#f15bb5", "#7bf1a8" };

        readonly Transform root;
        readonly MeshBuilder mb = new MeshBuilder();
        Mesh kelpMesh, frontMesh, causticMesh;
        readonly List<SpriteRenderer> glowSprites = new List<SpriteRenderer>();

        public World(Transform parent)
        {
            root = new GameObject("World").transform;
            root.SetParent(parent, false);
            GenFloor();
            GenDecor();
            BuildFloorMesh();
            kelpMesh = Gfx.MeshObject("Kelp", root, Layer.Kelp).mesh;
            frontMesh = Gfx.MeshObject("Front", root, Layer.Front).mesh;
            causticMesh = Gfx.MeshObject("Caustics", root, Layer.Caustic, true).mesh;
        }

        void GenFloor()
        {
            var n1 = new Noise1D(11); var n2 = new Noise1D(23); var n3 = new Noise1D(37);
            int N = Mathf.CeilToInt(W / FSTEP) + 2;
            floorS = new float[N];
            for (int i = 0; i < N; i++)
            {
                float x = i * FSTEP;
                floorS[i] = Mathf.Clamp(ProfileY(x) + 260 * n1.At(x / 900) + 140 * n2.At(x / 300) + 30 * n3.At(x / 90), 700, 4180);
            }
        }

        void GenDecor()
        {
            var R = new Mulberry(4242);

            for (float x = 200; x < W - 200; x += 70 + R.Next() * 260)
            {
                float fy = FloorY(x);
                if (fy < 1100 || fy > 3750 || R.Next() < 0.45f) continue;
                int n = 2 + (int)(R.Next() * 4);
                for (int k = 0; k < n; k++)
                {
                    float kx = x + k * (18 + R.Next() * 34);
                    kelps.Add(new Kelp
                    {
                        x = kx, y = FloorY(kx) + 12, h = Mathf.Min(fy - 120, 280 + R.Next() * (fy > 2600 ? 950 : 620)),
                        w = 9 + R.Next() * 9, phase = R.Next() * U.TAU, tone = R.Next(),
                    });
                }
            }

            // rocks: a handful of baked variants, scaled & flipped per placement
            var rockSprites = new List<Sprite>();
            var rr = new Mulberry(77);
            for (int i = 0; i < 10; i++) rockSprites.Add(DecorArt.Rock(rr, i % 2 == 0 ? 0.2f : 0.8f));
            var rocksT = new GameObject("Rocks").transform;
            rocksT.SetParent(root, false);
            int ri = 0;
            for (float x = 80; x < W; x += 130 + R.Next() * 420)
            {
                float s = 40 + R.Next() * 110;
                var sr = Gfx.SpriteObject("Rock", rocksT, R.Pick(rockSprites), Layer.Rocks + ri++);
                sr.transform.localPosition = U.V3(x, FloorY(x) + s * 0.32f);
                sr.transform.localScale = new Vector3(R.Next() < 0.5f ? -s : s, s, 1);
            }

            // coral reef on shallow floors
            var coralT = new GameObject("Coral").transform;
            coralT.SetParent(root, false);
            var cr = new Mulberry(99);
            var branch = new List<Sprite>(); var fan = new List<Sprite>(); var brain = new List<Sprite>(); var tube = new List<Sprite>();
            for (int i = 0; i < 8; i++) branch.Add(DecorArt.Branch(cr, U.Hex(cr.Pick(CORAL)), U.Hex(cr.Pick(new[] { "#fff2b3", "#ffffff", "#ffd6e8" }))));
            for (int i = 0; i < 6; i++) fan.Add(DecorArt.Fan(cr, U.Hex(cr.Pick(CORAL))));
            for (int i = 0; i < 6; i++) brain.Add(DecorArt.Brain(cr, U.Hex(cr.Pick(CORAL))));
            for (int i = 0; i < 6; i++) tube.Add(DecorArt.Tube(cr, U.Hex(cr.Pick(CORAL))));
            int ci = 0;
            for (float x = 120; x < W - 120; x += 40 + R.Next() * 120)
            {
                float fy = FloorY(x);
                if (fy > 2950 || R.Next() < 0.22f) continue;
                float k = R.Next(), s = 40 + R.Next() * 95, y = fy + 6;
                Sprite spr; float amp = 0, speed = 0;
                if (k < 0.38f) { spr = R.Pick(branch); amp = 0.02f; speed = 0.7f; }
                else if (k < 0.58f) { spr = R.Pick(fan); amp = 0.05f; speed = 0.6f; }
                else if (k < 0.8f) { spr = R.Pick(brain); y += 4; s *= 0.9f; }
                else spr = R.Pick(tube);
                var sr = Gfx.SpriteObject("Coral", coralT, spr, Layer.Coral + ci++);
                sr.transform.localPosition = U.V3(x, y);
                sr.transform.localScale = new Vector3(R.Next() < 0.5f ? -s : s, s, 1);
                if (amp > 0) swayers.Add(new Swayer { t = sr.transform, amp = amp, speed = speed, phase = R.Next() * U.TAU + x * 0.01f });
            }

            for (float x = 150; x < W - 150; x += 160 + R.Next() * 480)
            {
                if (R.Next() < 0.4f) continue;
                float fy = FloorY(x);
                bool deep = fy > 3150;
                float hue = deep ? R.Pick(new float[] { 185, 285, 160, 320 }) : R.Pick(new float[] { 345, 20, 35, 300 });
                var a = new Anemone { x = x, y = fy + 4, s = 26 + R.Next() * 44, hue = hue, deep = deep, phase = R.Next() * U.TAU, n = 9 + (int)(R.Next() * 6) };
                anemones.Add(a);
                if (deep)
                {
                    glowAnemones.Add(a);
                    var g = Gfx.SpriteObject("AnemGlow", root, Gfx.Glow, Layer.Glow, true);
                    g.transform.localPosition = U.V3(a.x, a.y - a.s * 0.4f);
                    float d = a.s * 1.6f * 2;
                    g.transform.localScale = new Vector3(d, d, 1);
                    g.color = U.Hsl(a.hue, 1f, 0.65f, 0.3f);
                    glowSprites.Add(g);
                }
            }

            for (float x = 40; x < W - 40; x += 28 + R.Next() * 110)
            {
                float fy = FloorY(x);
                if (fy > 3400 || R.Next() < 0.35f) continue;
                grasses.Add(new Grass { x = x, y = fy + 6, s = 22 + R.Next() * 46, n = 3 + (int)(R.Next() * 4), phase = R.Next() * U.TAU });
            }
        }

        static readonly Color[] FLOOR_C = { new Color32(0xf0, 0xdc, 0xa4, 255), new Color32(0xc9, 0xae, 0x74, 255), new Color32(0x7d, 0x68, 0x44, 255), new Color32(0x3a, 0x31, 0x28, 255) };
        static readonly float[] FLOOR_T = { 0, 0.3f, 0.65f, 1 };
        static Color FloorCol(float y)
        {
            float t = Mathf.Clamp01((y - 1300f) / 3100f);
            for (int i = 0; i < 3; i++)
                if (t <= FLOOR_T[i + 1]) return Color.Lerp(FLOOR_C[i], FLOOR_C[i + 1], (t - FLOOR_T[i]) / (FLOOR_T[i + 1] - FLOOR_T[i]));
            return FLOOR_C[3];
        }

        void BuildFloorMesh()
        {
            var (_, mesh, _) = Gfx.MeshObject("Floor", root, Layer.Floor);
            var m = new MeshBuilder();
            const int ROWS = 7;
            const float BOTTOM = 5200f;
            int cols = floorS.Length;
            for (int i = 0; i < cols; i++)
            {
                float x = i * FSTEP, fy = floorS[i];
                for (int k = 0; k <= ROWS; k++)
                {
                    float t = k / (float)ROWS;
                    float y = Mathf.Lerp(fy, BOTTOM, t * t);
                    Draw.V(m, x, y, FloorCol(y));
                }
            }
            for (int i = 0; i < cols - 1; i++)
                for (int k = 0; k < ROWS; k++)
                {
                    int a = i * (ROWS + 1) + k, b = (i + 1) * (ROWS + 1) + k;
                    m.Quad(a, b, b + 1, a + 1);
                }
            // pebbles
            var peb = new Color(80 / 255f, 60 / 255f, 40 / 255f, 0.25f);
            for (int i = 0; i < cols - 1; i++)
            {
                float x = i * FSTEP, h = U.Hash(x * 0.37f);
                if (h < 0.55f) continue;
                float px = x + h * FSTEP, py = FloorY(px) + 8 + U.Hash(x) * 60, pr = 2 + U.Hash(x * 1.7f) * 5;
                Draw.Ellipse(m, px, py, pr, pr * 0.6f, 0, peb, 8);
            }
            // sunlit lip
            var lip = new List<Vector2>(cols);
            for (int i = 0; i < cols; i++) lip.Add(new Vector2(i * FSTEP, floorS[i] + 1.5f));
            Draw.Stroke(m, lip, 5f, 5f, new Color(1f, 245 / 255f, 210 / 255f, 0.35f));
            m.Apply(mesh);
            mesh.UploadMeshData(false);
        }

        // ------------------------------------------------------------------ per frame
        public void UpdateView(float time, float x0, float x1, float yBot)
        {
            foreach (var s in swayers)
                s.t.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(time * s.speed + s.phase) * s.amp * Mathf.Rad2Deg);

            // kelp
            mb.Clear();
            var pts = new List<Vector2>(17);
            foreach (var d in kelps)
            {
                if (d.x < x0 - 120 || d.x > x1 + 120) continue;
                const int segs = 16;
                float sl = d.h / segs, x = d.x, y = d.y;
                pts.Clear();
                pts.Add(new Vector2(x, y));
                for (int i = 1; i <= segs; i++)
                {
                    float ang = -Mathf.PI / 2 + Mathf.Sin(time * 0.8f + d.phase + i * 0.32f) * 0.16f * (0.3f + i / (float)segs);
                    x += Mathf.Cos(ang) * sl; y += Mathf.Sin(ang) * sl;
                    pts.Add(new Vector2(x, y));
                }
                var col = d.tone < 0.5f ? U.Hex("#2e8b57") : U.Hex("#4a9a3c");
                Draw.Stroke(mb, pts, d.w, d.w * 0.25f, col, true);
                var lc = d.tone < 0.5f ? U.Hex("#3aa06a") : U.Hex("#62b04e");
                for (int i = 2; i < pts.Count - 1; i += 2)
                {
                    float s = i % 4 == 0 ? 1 : -1, rot = s * 0.6f + Mathf.Sin(time * 1.2f + d.phase + i) * 0.25f;
                    Draw.Ellipse(mb, pts[i].x + s * d.w * 0.9f, pts[i].y, d.w * 1.1f, d.w * 0.38f, rot, lc, 10);
                }
            }
            mb.Apply(kelpMesh);

            // grass + anemones
            mb.Clear();
            var grassC = U.Hex("#3f9a55");
            foreach (var d in grasses)
            {
                if (d.x < x0 - 80 || d.x > x1 + 80) continue;
                for (int i = 0; i < d.n; i++)
                {
                    float bx = d.x + (i - d.n / 2f) * d.s * 0.12f;
                    float sw = Mathf.Sin(time * 1.3f + d.phase + i) * d.s * 0.25f;
                    var q = Draw.QuadPts(bx, d.y, bx + sw * 0.3f, d.y - d.s * 0.6f, bx + sw, d.y - d.s * (0.8f + (i % 3) * 0.15f), 7);
                    Draw.Stroke(mb, q, d.s * 0.07f, d.s * 0.07f, grassC, true);
                }
            }
            foreach (var d in anemones)
            {
                if (d.x < x0 - 100 || d.x > x1 + 100) continue;
                var col = U.Hsl(d.hue, d.deep ? 0.9f : 0.75f, d.deep ? 0.65f : 0.62f);
                Draw.Ellipse(mb, d.x, d.y - d.s * 0.12f, d.s * 0.32f, d.s * 0.2f, 0, U.Hsl(d.hue, 0.45f, 0.35f), 12);
                var tipC = U.Hsl(d.hue, 1f, 0.85f);
                for (int i = 0; i < d.n; i++)
                {
                    float a = -Mathf.PI + 0.35f + (i / (float)(d.n - 1)) * (Mathf.PI - 0.7f);
                    float sw = Mathf.Sin(time * 1.4f + d.phase + i * 0.6f) * 0.3f;
                    float ex = d.x + Mathf.Cos(a + sw * 0.4f) * d.s * 0.75f, ey = d.y - d.s * 0.2f + Mathf.Sin(a) * d.s * 0.75f;
                    var q = Draw.QuadPts(d.x + Mathf.Cos(a) * d.s * 0.2f, d.y - d.s * 0.2f,
                        d.x + Mathf.Cos(a) * d.s * 0.5f + sw * d.s * 0.2f, d.y - d.s * 0.2f + Mathf.Sin(a) * d.s * 0.4f, ex, ey, 7);
                    Draw.Stroke(mb, q, d.s * 0.07f, d.s * 0.07f, col, false);
                    Draw.Circle(mb, ex, ey, d.s * 0.05f, tipC, 8);
                }
            }
            mb.Apply(frontMesh);

            // caustic shimmer on shallow sand
            mb.Clear();
            var cc = new Color(200 / 255f, 1f, 1f, 0.06f);
            float s0 = Mathf.Floor(x0 / (FSTEP * 3)) * FSTEP * 3;
            for (float x = s0; x <= x1; x += FSTEP * 3)
            {
                float y = FloorY(x);
                if (y > 2600 || y > yBot + 40) continue;
                float s = 14 + 10 * Mathf.Sin(time * 1.3f + x * 0.05f);
                Draw.Ellipse(mb, x, y + 14, s, s * 0.3f, 0, cc, 10);
            }
            mb.Apply(causticMesh);
        }
    }

    /// <summary>Baked decor sprites (rocks, coral). Shape space: s = 1, pivot at the base.</summary>
    public static class DecorArt
    {
        const float PPU = 150f;

        public static Sprite Rock(Mulberry R, float tone)
        {
            int n = 9;
            var ang = new float[n]; var mul = new float[n];
            for (int i = 0; i < n; i++) { ang[i] = i / (float)n * U.TAU; mul[i] = 1 + (R.Next() * 2 - 1) * 0.22f; }
            Vector2 P(int i) { i = (i % n + n) % n; return new Vector2(Mathf.Cos(ang[i]) * mul[i] * 1.25f, Mathf.Sin(ang[i]) * mul[i] * 0.8f); }
            var path = new Path();
            Vector2 p0 = P(0), p1 = P(1);
            path.Move((p0.x + p1.x) / 2, (p0.y + p1.y) / 2);
            for (int i = 1; i <= n; i++)
            {
                Vector2 a = P(i), b = P(i + 1);
                path.Quad(a.x, a.y, (a.x + b.x) / 2, (a.y + b.y) / 2);
            }
            var r = new Raster(-1.6f, -1.05f, 1.6f, 1.05f, PPU);
            var m = r.Fill(path);
            Color[] c = tone < 0.5f
                ? new[] { U.Hex("#a3a9b4"), U.Hex("#5d6370"), U.Hex("#2b2f38") }
                : new[] { U.Hex("#b19d86"), U.Hex("#6c5c4c"), U.Hex("#332b24") };
            Vector2 A = new Vector2(-0.7f, -1f), B = new Vector2(0.5f, 0.4f), AB = B - A;
            r.Paint(m, (x, y) =>
            {
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - A, AB) / AB.sqrMagnitude);
                return t < 0.5f ? Color.Lerp(c[0], c[1], t * 2) : Color.Lerp(c[1], c[2], t * 2 - 1);
            });
            void Clip(float[] k, Color col) { Raster.Mul(k, m); r.Paint(k, col); }
            Clip(r.Ellipse(-0.4f, -0.62f, 0.55f, 0.2f, -0.25f), new Color(1, 1, 1, 0.13f));
            var spots = r.Mask();
            for (int i = 0; i < 7; i++) r.Circle((R.Next() - 0.5f) * 1.8f, -R.Next() * 0.7f, 0.04f + R.Next() * 0.06f, spots);
            Clip(spots, new Color(0, 0, 0, 0.16f));
            Clip(r.Ellipse(-0.05f, -0.82f, 0.75f, 0.2f, 0), tone < 0.5f ? new Color(90 / 255f, 175 / 255f, 100 / 255f, 0.55f) : new Color(150 / 255f, 170 / 255f, 80 / 255f, 0.5f));
            r.Paint(r.InnerRing(m, 4), new Color(8 / 255f, 14 / 255f, 24 / 255f, 0.4f));
            return r.ToSprite(Vector2.zero, PPU);
        }

        static void GenBranch(Mulberry R, float x, float y, float ang, float len, int lvl, int maxLvl, List<List<Vector4>> levels)
        {
            float x2 = x + Mathf.Cos(ang) * len, y2 = y + Mathf.Sin(ang) * len;
            while (levels.Count <= lvl) levels.Add(new List<Vector4>());
            levels[lvl].Add(new Vector4(x, y, x2, y2));
            if (lvl < maxLvl)
            {
                int n = 2 + (R.Next() < 0.35f ? 1 : 0);
                for (int i = 0; i < n; i++)
                    GenBranch(R, x2, y2, ang + (i - (n - 1) / 2f) * 0.55f + (R.Next() - 0.5f) * 0.5f, len * (0.62f + R.Next() * 0.2f), lvl + 1, maxLvl, levels);
            }
        }

        public static Sprite Branch(Mulberry R, Color col, Color tip)
        {
            var levels = new List<List<Vector4>>();
            GenBranch(R, 0, 0, -Mathf.PI / 2 + (R.Next() - 0.5f) * 0.4f, 0.42f, 0, 3, levels);
            float minX = 0, maxX = 0, minY = 0;
            foreach (var l in levels) foreach (var s in l) { minX = Mathf.Min(minX, s.z); maxX = Mathf.Max(maxX, s.z); minY = Mathf.Min(minY, s.w); }
            var r = new Raster(minX - 0.12f, minY - 0.12f, maxX + 0.12f, 0.1f, PPU);
            var m = r.Mask();
            for (int l = 0; l < levels.Count; l++)
            {
                float w = 0.13f * Mathf.Pow(0.7f, l);
                foreach (var s in levels[l]) r.Stroke(new[] { new Vector2(s.x, s.y), new Vector2(s.z, s.w) }, w, m);
            }
            r.Paint(m, (x, y) => { var c = Color.Lerp(col * 0.78f, col, Mathf.Clamp01(-y * 1.6f)); c.a = 1; return c; });
            var tips = r.Mask();
            foreach (var s in levels[levels.Count - 1]) r.Circle(s.z, s.w, 0.035f, tips);
            r.Paint(tips, tip);
            return r.ToSprite(Vector2.zero, PPU);
        }

        public static Sprite Fan(Mulberry R, Color col)
        {
            const float S = 1.1f;
            int n = 9 + (int)(R.Next() * 4);
            var ribs = new List<Vector2>();
            for (int i = 0; i < n; i++) ribs.Add(new Vector2(-Mathf.PI + 0.35f + (i / (float)(n - 1)) * (Mathf.PI - 0.7f), 0.75f + R.Next() * 0.25f));
            var r = new Raster(-S * 1.05f, -S * 1.05f, S * 1.05f, 0.08f, PPU);
            var poly = new Path().Move(0, 0);
            foreach (var rb in ribs) poly.Line(Mathf.Cos(rb.x) * S * rb.y, Mathf.Sin(rb.x) * S * rb.y);
            r.Paint(r.Fill(poly), U.WithA(col, 0.35f));
            var m = r.Mask();
            foreach (var rb in ribs)
            {
                float a = rb.x, k = rb.y;
                r.Stroke(Raster.QuadPts(Vector2.zero, new Vector2(Mathf.Cos(a + 0.1f) * S * k * 0.5f, Mathf.Sin(a + 0.1f) * S * k * 0.5f), new Vector2(Mathf.Cos(a) * S * k, Mathf.Sin(a) * S * k), 10), S * 0.03f, m);
            }
            foreach (var k in new[] { 0.4f, 0.62f, 0.82f })
            {
                var arc = new List<Vector2>();
                for (int i = 0; i <= 24; i++) { float a = -Mathf.PI + 0.4f + (Mathf.PI - 0.8f) * i / 24f; arc.Add(new Vector2(Mathf.Cos(a) * S * k, Mathf.Sin(a) * S * k)); }
                r.Stroke(arc, S * 0.015f, m);
            }
            r.Paint(m, col);
            return r.ToSprite(Vector2.zero, PPU);
        }

        public static Sprite Brain(Mulberry R, Color col)
        {
            var r = new Raster(-0.75f, -0.53f, 0.75f, 0.04f, PPU);
            var path = new Path().Move(-0.7f, 0);
            for (int i = 1; i <= 32; i++) { float a = Mathf.PI + Mathf.PI * i / 32f; path.Line(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.48f); }
            var m = r.Fill(path);
            var dark = new Color(40 / 255f, 20 / 255f, 40 / 255f);
            r.Paint(m, (x, y) =>
            {
                float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(-0.2f, -0.4f)) / 0.95f);
                return t < 0.15f ? Color.Lerp(Color.white, col, t / 0.15f) : Color.Lerp(col, dark, (t - 0.15f) / 0.85f);
            });
            float seed = R.Next() * 100;
            var lines = r.Mask();
            for (int k = 0; k < 5; k++)
            {
                float yy = -(0.08f + k * 0.09f);
                var pts = new List<Vector2>();
                for (float xx = -0.7f; xx <= 0.7f; xx += 0.035f) pts.Add(new Vector2(xx, yy + Mathf.Sin(xx * 18 + seed + k) * 0.025f));
                r.Stroke(pts, 0.035f, lines);
            }
            Raster.Mul(lines, m);
            r.Paint(lines, new Color(0, 0, 0, 0.22f));
            return r.ToSprite(Vector2.zero, PPU);
        }

        static Path RoundRect(float x, float y, float w, float h, float rad)
        {
            var p = new Path().Move(x + rad, y);
            p.Line(x + w - rad, y).Quad(x + w, y, x + w, y + rad);
            p.Line(x + w, y + h - rad).Quad(x + w, y + h, x + w - rad, y + h);
            p.Line(x + rad, y + h).Quad(x, y + h, x, y + h - rad);
            p.Line(x, y + rad).Quad(x, y, x + rad, y);
            return p;
        }

        public static Sprite Tube(Mulberry R, Color col)
        {
            int n = 2 + (int)(R.Next() * 3);
            var tubes = new List<Vector3>();
            float maxH = 0;
            for (int i = 0; i < n; i++)
            {
                var t = new Vector3((i - (n - 1) / 2f) * 0.22f + (R.Next() - 0.5f) * 0.1f, 0.5f + R.Next() * 0.7f, 0.1f + R.Next() * 0.06f);
                tubes.Add(t);
                maxH = Mathf.Max(maxH, t.y);
            }
            float span = (n - 1) / 2f * 0.22f + 0.3f;
            var r = new Raster(-span, -maxH - 0.05f, span, 0.05f, PPU);
            var white = Color.white;
            foreach (var t in tubes)
            {
                float x = t.x, h = t.y, w = t.z;
                var m = r.Fill(RoundRect(x - w, -h, w * 2, h + 0.02f, w * 0.6f));
                r.Paint(m, (px, py) => Color.Lerp(col, white, 0.2f * Mathf.Clamp01(1 - Mathf.Abs(px - x) / w)));
                r.Paint(r.Ellipse(x, -h + w * 0.25f, w * 0.75f, w * 0.3f, 0), new Color(30 / 255f, 10 / 255f, 20 / 255f, 0.7f));
            }
            return r.ToSprite(Vector2.zero, PPU);
        }
    }
}
