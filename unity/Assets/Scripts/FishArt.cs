using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepFeast
{
    public enum TailKind { Fork, Lunate, Fan, Shark }

    public sealed class Shape
    {
        public float hh, nx, ny, ped, tl, tH, dH, aH, eye;
        public TailKind tail;
        public bool trailing;
    }

    public sealed class Species
    {
        public string key, shape, pat;
        public Color c0, c1, c2, fin, tailCol, spot;
        public float min, max, eye;
        public Shape Sh => FishArt.Shapes[shape];
    }

    public sealed class Tier
    {
        public string name, blurb;
        public float r;
        public Tier(string n, float r, string b) { name = n; this.r = r; blurb = b; }
    }

    /// <summary>Static game data ported from the prototype.</summary>
    public static class Data
    {
        public const float EAT = 0.9f, DANGER = 1.1f, GROW = 0.16f;

        public static readonly Tier[] Tiers =
        {
            new Tier("Fry", 12, "Snack on minnows. Avoid everything else."),
            new Tier("Minnow", 19, "Clownfish are on the menu."),
            new Tier("Hunter", 30, "Angelfish and tangs fear you now."),
            new Tier("Predator", 47, "Puffers and snappers beware."),
            new Tier("Apex", 73, "Barracuda bow before you."),
            new Tier("Leviathan", 113, "Sharks are prey now. Hunt them!"),
            new Tier("Legend", 175, "You rule the deep."),
        };

        /// Visible ref-px per world unit (820 ref px = the short screen axis).
        public static float ZoomFor(float r) => (24f + (r - 12f) * 0.3f) / r;
        public static float SpeedFor(float r) => (235f + (r - 12f) * 0.25f) / ZoomFor(r);

        static Species S(string key, string shape, string a, string b, string c, string fin, float finA, string pat, float min, float max)
            => new Species { key = key, shape = shape, c0 = U.Hex(a), c1 = U.Hex(b), c2 = U.Hex(c), fin = U.Hex(fin, finA), tailCol = U.Hex(fin, finA), pat = pat, min = min, max = max };

        public static readonly Dictionary<string, Species> SpeciesMap = Build();
        public static readonly List<string> SpeciesKeys = new List<string>(SpeciesMap.Keys);
        public static Species Shark, Player;

        static Dictionary<string, Species> Build()
        {
            var d = new Dictionary<string, Species>();
            void Add(Species s) => d[s.key] = s;
            Add(S("minnow", "slim", "#cfe9ff", "#5b8fc4", "#f4fbff", "#9cc7ee", 0.85f, "line", 3, 14));
            Add(S("clown", "oval", "#ff8c2b", "#c94c0c", "#ffd0a0", "#ff7a1a", 1, "bands", 7, 22));
            var tang = S("tang", "disc", "#2f86ff", "#1340a8", "#8cc2ff", "#1d5fe0", 1, "tang", 9, 28);
            tang.tailCol = U.Hex("#ffd23f");
            Add(tang);
            Add(S("angel", "tall", "#ffe55c", "#d9a300", "#fff6c2", "#ffd21f", 1, "stripes", 13, 38));
            var puffer = S("puffer", "round", "#e9d585", "#9c8640", "#fffbe3", "#d8c06a", 1, "spots", 16, 48);
            puffer.spot = U.Hex("#5a4818");
            Add(puffer);
            Add(S("parrot", "oval", "#3ee0a8", "#1a8f8a", "#c8ffe9", "#ff7ac8", 1, "scales", 20, 60));
            Add(S("snapper", "oval", "#ff5f6f", "#b4253a", "#ffd0d4", "#ff7a88", 1, "none", 22, 64));
            Add(S("barracuda", "long", "#b4c3d1", "#5d6e80", "#eef4f9", "#8a9bab", 1, "bars", 34, 130));
            var grouper = S("grouper", "fat", "#9a7650", "#5d432a", "#e2cba9", "#7d5d3c", 1, "spots", 48, 180);
            grouper.spot = U.Hex("#3a2814");
            Add(grouper);
            Add(S("tuna", "torpedo", "#4d6d9c", "#1d2f52", "#e3e9f2", "#5a7bb0", 1, "finlets", 70, 600));
            Shark = S("shark", "shark", "#62788b", "#283746", "#f2f5f7", "#4a5f72", 1, "shark", 0, 0);
            Player = S("player", "oval", "#3df2d0", "#0a8f99", "#e2fff8", "#ffc93c", 1, "player", 0, 0);
            Player.eye = 0.15f;
            return d;
        }

        public static string SpeciesFor(float r, bool school)
        {
            var keys = new List<string>();
            foreach (var k in SpeciesKeys)
            {
                var s = SpeciesMap[k];
                if (k == "minnow" && !school && r > 6) continue;
                if (r >= s.min && r <= s.max) keys.Add(k);
            }
            if (keys.Count == 0) return r < 5 ? "minnow" : "tuna";
            return U.Pick(keys);
        }
    }

    /// <summary>Bakes every fish into sprites (body, open-mouth body, tail, eyes, fin).</summary>
    public static class FishArt
    {
        public static readonly Dictionary<string, Shape> Shapes = new Dictionary<string, Shape>
        {
            ["slim"] = new Shape { hh = 0.36f, nx = 0.92f, ny = 0.95f, ped = 0.2f, tl = 0.48f, tH = 0.38f, dH = 0.18f, aH = 0.12f, tail = TailKind.Fork, eye = 0.11f },
            ["oval"] = new Shape { hh = 0.50f, nx = 0.95f, ny = 1.0f, ped = 0.2f, tl = 0.45f, tH = 0.42f, dH = 0.22f, aH = 0.15f, tail = TailKind.Fork, eye = 0.11f },
            ["disc"] = new Shape { hh = 0.66f, nx = 0.85f, ny = 1.05f, ped = 0.18f, tl = 0.36f, tH = 0.40f, dH = 0.16f, aH = 0.14f, tail = TailKind.Lunate, eye = 0.1f },
            ["tall"] = new Shape { hh = 0.70f, nx = 0.8f, ny = 1.0f, ped = 0.18f, tl = 0.38f, tH = 0.38f, dH = 0.72f, aH = 0.72f, tail = TailKind.Fan, eye = 0.1f, trailing = true },
            ["round"] = new Shape { hh = 0.80f, nx = 1.05f, ny = 1.15f, ped = 0.22f, tl = 0.30f, tH = 0.30f, dH = 0.12f, aH = 0.10f, tail = TailKind.Fan, eye = 0.14f },
            ["long"] = new Shape { hh = 0.20f, nx = 0.6f, ny = 0.8f, ped = 0.3f, tl = 0.40f, tH = 0.32f, dH = 0.20f, aH = 0.14f, tail = TailKind.Fork, eye = 0.07f },
            ["fat"] = new Shape { hh = 0.56f, nx = 0.98f, ny = 1.05f, ped = 0.2f, tl = 0.38f, tH = 0.40f, dH = 0.20f, aH = 0.14f, tail = TailKind.Fan, eye = 0.09f },
            ["torpedo"] = new Shape { hh = 0.36f, nx = 0.8f, ny = 0.95f, ped = 0.14f, tl = 0.45f, tH = 0.50f, dH = 0.22f, aH = 0.18f, tail = TailKind.Lunate, eye = 0.08f },
            ["shark"] = new Shape { hh = 0.30f, nx = 0.5f, ny = 0.7f, ped = 0.14f, tl = 0.60f, tH = 0.55f, dH = 0.42f, aH = 0.12f, tail = TailKind.Shark, eye = 0.055f },
        };

        public const float HL = 1.3f; // half length of a fish with r = 1

        public sealed class Art
        {
            public Sprite body, bodyOpen, tail;
            public Vector2 eyePos; // unity-local (y-up)
            public float eyeR, hh;
            public bool shark;
            public Color fin;
        }

        static readonly Dictionary<string, Art> cache = new Dictionary<string, Art>();
        static Sprite eyeNormal, eyeAngry, eyeShark, eyeSharkAngry, eyeBlink, eyeHappy, finOval;

        public static Art Get(Species sp)
        {
            if (cache.TryGetValue(sp.key, out var a)) return a;
            a = Bake(sp);
            cache[sp.key] = a;
            return a;
        }

        public static void Prewarm()
        {
            foreach (var s in Data.SpeciesMap.Values) Get(s);
            Get(Data.Shark); Get(Data.Player);
            _ = EyeNormal; _ = EyeAngry; _ = EyeShark; _ = EyeSharkAngry; _ = EyeBlink; _ = EyeHappy; _ = FinOval;
        }

        // ------------------------------------------------------------------ paths
        static Path BodyPath(float hl, float hh, Shape sh)
        {
            float ped = sh.ped;
            return new Path().Move(hl, 0)
                .Cubic(hl * sh.nx, -hh * sh.ny, -hl * 0.3f, -hh * 1.12f, -hl * 0.86f, -hh * ped)
                .Line(-hl * 0.86f, hh * ped)
                .Cubic(-hl * 0.3f, hh * 1.12f, hl * sh.nx, hh * sh.ny, hl, 0);
        }

        static Path TailPath(Shape sh, float hl, float hh)
        {
            float tl = hl * sh.tl, th = hl * sh.tH;
            var p = new Path();
            switch (sh.tail)
            {
                case TailKind.Fork:
                    p.Move(hl * 0.08f, -hh * 0.2f).Quad(-tl * 0.45f, -th * 0.45f, -tl, -th).Quad(-tl * 0.6f, -th * 0.15f, -tl * 0.5f, 0)
                     .Quad(-tl * 0.6f, th * 0.15f, -tl, th).Quad(-tl * 0.45f, th * 0.45f, hl * 0.08f, hh * 0.2f);
                    break;
                case TailKind.Lunate:
                    p.Move(hl * 0.06f, -hh * 0.14f).Quad(-tl * 0.5f, -th * 0.25f, -tl, -th).Quad(-tl * 0.5f, -th * 0.2f, -tl * 0.36f, 0)
                     .Quad(-tl * 0.5f, th * 0.2f, -tl, th).Quad(-tl * 0.5f, th * 0.25f, hl * 0.06f, hh * 0.14f);
                    break;
                case TailKind.Fan:
                    p.Move(hl * 0.06f, -hh * 0.22f).Line(-tl * 0.85f, -th).Quad(-tl * 1.3f, 0, -tl * 0.85f, th).Line(hl * 0.06f, hh * 0.22f);
                    break;
                default:
                    p.Move(hl * 0.06f, -hh * 0.16f).Quad(-tl * 0.5f, -th * 0.45f, -tl, -th).Quad(-tl * 0.62f, -th * 0.2f, -tl * 0.44f, hh * 0.08f)
                     .Quad(-tl * 0.56f, th * 0.3f, -tl * 0.62f, th * 0.58f).Quad(-tl * 0.3f, th * 0.22f, hl * 0.06f, hh * 0.16f);
                    break;
            }
            return p;
        }

        static Path FinsPath(Shape sh, float hl, float hh)
        {
            float dH = hl * sh.dH, aH = hl * sh.aH;
            var p = new Path();
            if (sh.tail == TailKind.Shark)
            {
                p.Move(hl * 0.22f, -hh * 0.6f).Quad(hl * 0.04f, -hh * 0.9f - dH * 0.6f, -hl * 0.14f, -hh * 0.75f - dH).Quad(-hl * 0.1f, -hh * 0.85f, -hl * 0.3f, -hh * 0.55f);
                p.Move(-hl * 0.62f, -hh * 0.3f).Line(-hl * 0.7f, -hh * 0.3f - dH * 0.22f).Line(-hl * 0.76f, -hh * 0.25f);
            }
            else if (sh.trailing)
            {
                p.Move(hl * 0.3f, -hh * 0.6f).Quad(0, -hh * 0.8f - dH, -hl * 0.95f, -hh * 0.6f - dH * 1.15f).Quad(-hl * 0.55f, -hh * 0.6f, -hl * 0.7f, -hh * 0.25f);
                p.Move(hl * 0.1f, hh * 0.6f).Quad(-hl * 0.1f, hh * 0.8f + aH, -hl * 0.95f, hh * 0.6f + aH * 1.15f).Quad(-hl * 0.55f, hh * 0.6f, -hl * 0.7f, hh * 0.25f);
            }
            else
            {
                p.Move(hl * 0.28f, -hh * 0.6f).Quad(hl * 0.05f, -hh * 0.8f - dH * 1.3f, -hl * 0.4f, -hh * 0.7f - dH).Quad(-hl * 0.5f, -hh * 0.7f, -hl * 0.62f, -hh * 0.35f);
                p.Move(-hl * 0.1f, hh * 0.6f).Quad(-hl * 0.3f, hh * 0.75f + aH * 1.2f, -hl * 0.55f, hh * 0.6f + aH).Quad(-hl * 0.6f, hh * 0.55f, -hl * 0.68f, hh * 0.3f);
            }
            return p;
        }

        static Path RectPath(float x, float y, float w, float h, float rot = 0, float cx = 0, float cy = 0)
        {
            var pts = new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) };
            if (rot != 0)
            {
                float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
                for (int i = 0; i < 4; i++)
                {
                    var p = pts[i];
                    pts[i] = new Vector2(cx + p.x * c - p.y * s, cy + p.x * s + p.y * c);
                }
            }
            return new Path().Poly(pts);
        }

        static List<Vector2> Arc(float cx, float cy, float r, float a0, float a1, int n = 18)
        {
            var l = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = a0 + (a1 - a0) * i / n;
                l.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return l;
        }

        // ------------------------------------------------------------------ baking
        const float PPU = 150f; // raster pixels per shape unit (r = 1)

        static Art Bake(Species sp)
        {
            var sh = sp.Sh;
            float hl = HL, hh = hl * sh.hh;
            bool shark = sh.tail == TailKind.Shark;
            float scale = shark ? 300f : PPU;
            float pad = 0.08f;
            float minY = -(hh + hl * sh.dH * 1.45f) - pad - (sh.trailing ? hh * 0.2f : 0);
            float maxY = Mathf.Max(hh * 1.95f, hh + hl * sh.aH * 1.45f + (sh.trailing ? hh * 0.2f : 0)) + pad;
            float minX = -hl * 1.0f - pad, maxX = hl * 1.08f + pad;

            var art = new Art { shark = shark, hh = hh, fin = sp.fin };
            art.body = BakeBody(sp, sh, hl, hh, minX, minY, maxX, maxY, scale, 0f);
            art.bodyOpen = BakeBody(sp, sh, hl, hh, minX, minY, maxX, maxY, scale, 1f);

            float tl = hl * sh.tl, th = hl * sh.tH;
            var tr = new Raster(-tl * 1.35f - pad, -th - pad, hl * 0.12f + pad, th + pad, scale);
            var tm = tr.Fill(TailPath(sh, hl, hh));
            tr.Paint(tm, sp.tailCol);
            tr.Paint(tr.InnerRing(tm, Mathf.Max(1, Mathf.RoundToInt(scale * 0.012f))), new Color(0, 0.07f, 0.14f, 0.25f));
            art.tail = tr.ToSprite(Vector2.zero, scale);

            float ex = hl * (shark ? 0.62f : 0.56f), ey = -hh * 0.22f;
            art.eyePos = new Vector2(ex, -ey);
            art.eyeR = hl * (sp.eye > 0 ? sp.eye : sh.eye);
            return art;
        }

        static Sprite BakeBody(Species sp, Shape sh, float hl, float hh, float minX, float minY, float maxX, float maxY, float scale, float mouth)
        {
            var r = new Raster(minX, minY, maxX, maxY, scale);
            bool shark = sh.tail == TailKind.Shark;

            // fins behind the body
            r.Paint(r.Fill(FinsPath(sh, hl, hh)), sp.fin);

            var body = r.Fill(BodyPath(hl, hh, sh));
            Color top = sp.c1, mid = sp.c0, bot = sp.c2;
            r.Paint(body, (x, y) =>
            {
                float t = Mathf.Clamp01((y + hh) / (2 * hh));
                return t < 0.45f ? Color.Lerp(top, mid, t / 0.45f) : Color.Lerp(mid, bot, (t - 0.45f) / 0.55f);
            });

            void Clipped(float[] m, Color c) { Raster.Mul(m, body); r.Paint(m, c); }

            switch (sp.pat)
            {
                case "line":
                    Clipped(r.Fill(RectPath(-hl, -hh * 0.12f, hl * 2, hh * 0.16f)), new Color(1, 1, 1, 0.6f));
                    Clipped(r.Fill(RectPath(-hl, -hh, hl * 2, hh * 0.45f)), new Color(40 / 255f, 80 / 255f, 140 / 255f, 0.35f));
                    break;
                case "bands":
                    foreach (var bx in new[] { hl * 0.42f, -hl * 0.12f, -hl * 0.64f })
                    {
                        float bw = hl * 0.17f;
                        Clipped(r.Ellipse(bx, 0, bw * 0.75f, hh * 1.3f, 0.08f), U.Hex("#1b1210"));
                        Clipped(r.Ellipse(bx, 0, bw * 0.5f, hh * 1.3f, 0.08f), Color.white);
                    }
                    break;
                case "tang":
                    {
                        var m = r.Stroke(Raster.QuadPts(new Vector2(hl * 0.42f, -hh * 0.4f), new Vector2(-hl * 0.1f, -hh * 0.05f), new Vector2(-hl * 0.55f, -hh * 0.55f)), hh * 0.26f);
                        r.Stroke(Raster.QuadPts(new Vector2(-hl * 0.05f, -hh * 0.2f), new Vector2(-hl * 0.4f, hh * 0.35f), new Vector2(-hl * 0.75f, hh * 0.05f)), hh * 0.26f, m);
                        Clipped(m, U.Hex("#0a1638"));
                    }
                    break;
                case "stripes":
                    {
                        var m = r.Mask();
                        foreach (var bx in new[] { hl * 0.35f, -hl * 0.1f, -hl * 0.55f })
                            r.Fill(RectPath(-hl * 0.05f, -hh * 1.5f, hl * 0.1f, hh * 3, 0.25f, bx, 0), m);
                        Clipped(m, new Color(30 / 255f, 30 / 255f, 30 / 255f, 0.85f));
                    }
                    break;
                case "spots":
                    {
                        var rng = new Mulberry((uint)sp.key.GetHashCode());
                        var m = r.Mask();
                        int n = 10 + (int)(rng.Next() * 6);
                        for (int i = 0; i < n; i++)
                        {
                            float sx = (rng.Next() * 1.5f - 0.75f) * hl, sy = (rng.Next() * 1.2f - 0.6f) * hh, sr = (0.05f + rng.Next() * 0.07f) * hl;
                            r.Circle(sx, sy, sr, m);
                        }
                        Clipped(m, U.WithA(sp.spot, 0.75f));
                    }
                    break;
                case "scales":
                    {
                        var m = r.Mask();
                        float st = hl * 0.16f;
                        for (float x = -hl * 0.8f; x < hl * 0.5f; x += st)
                            for (float y = -hh; y < hh; y += st * 0.8f)
                            {
                                float ox = (Mathf.RoundToInt(y / (st * 0.8f)) & 1) * st * 0.5f;
                                r.Stroke(Arc(x + ox, y, st * 0.5f, 0, Mathf.PI * 0.9f, 8), hl * 0.025f, m);
                            }
                        Clipped(m, new Color(0, 60 / 255f, 60 / 255f, 0.25f));
                        Clipped(r.Ellipse(hl * 0.72f, hh * 0.15f, hl * 0.22f, hh * 0.35f, 0), new Color(1, 120 / 255f, 200 / 255f, 0.55f));
                    }
                    break;
                case "bars":
                    {
                        var m = r.Mask();
                        for (int i = 0; i < 9; i++) r.Fill(RectPath(hl * 0.45f - i * hl * 0.15f, -hh, hl * 0.07f, hh * 0.95f), m);
                        Clipped(m, new Color(40 / 255f, 55 / 255f, 70 / 255f, 0.32f));
                    }
                    break;
                case "finlets":
                    Clipped(r.Fill(RectPath(-hl, hh * 0.05f, hl * 2, hh * 0.12f)), new Color(1, 1, 1, 0.25f));
                    break;
                case "player":
                    {
                        var m = r.Stroke(Raster.QuadPts(new Vector2(hl * 0.5f, -hh * 0.52f), new Vector2(-hl * 0.1f, -hh * 0.3f), new Vector2(-hl * 0.88f, -hh * 0.05f)), hh * 0.17f);
                        Clipped(m, U.Hex("#ffd447"));
                        var d = r.Mask();
                        r.Circle(-0.2f * hl, 0.25f * hh, 0.05f * hl, d);
                        r.Circle(-0.45f * hl, 0.1f * hh, 0.04f * hl, d);
                        r.Circle(0.05f * hl, 0.38f * hh, 0.035f * hl, d);
                        Clipped(d, new Color(1, 1, 1, 0.55f));
                    }
                    break;
            }

            // soft top highlight
            Clipped(r.Ellipse(hl * 0.05f, -hh * 0.5f, hl * 0.55f, hh * 0.17f, -0.05f), new Color(1, 1, 1, 0.2f));

            if (mouth > 0)
            {
                var mp = new Path().Move(hl * 1.05f, -hh * 0.42f * mouth).Line(hl * (0.92f - 0.3f * mouth), hh * 0.05f).Line(hl * 1.05f, hh * 0.42f * mouth);
                Clipped(r.Fill(mp), U.Hex("#3b0d16"));
            }

            // outline + gills
            r.Paint(r.InnerRing(body, Mathf.Max(1, Mathf.RoundToInt(scale * 0.016f))), new Color(0, 18 / 255f, 36 / 255f, 0.42f));
            float lw = hl * 0.028f;
            var g = r.Mask();
            if (shark)
            {
                for (int i = 0; i < 4; i++)
                {
                    float gx = hl * (0.4f - i * 0.06f);
                    r.Stroke(Raster.QuadPts(new Vector2(gx, -hh * 0.35f), new Vector2(gx - hl * 0.03f, 0), new Vector2(gx, hh * 0.3f)), lw, g);
                }
            }
            else r.Stroke(Arc(hl * 0.62f, 0, hl * 0.28f, Mathf.PI * 0.72f, Mathf.PI * 1.28f), lw, g);
            r.Paint(g, new Color(0, 0, 0, 0.22f));

            if (shark)
            {
                var pf = new Path().Move(hl * 0.3f, hh * 0.55f).Line(hl * 0.3f - hl * 0.32f, hh * 0.55f + hh * 1.3f).Line(hl * 0.3f - hl * 0.18f, hh * 0.55f);
                r.Paint(r.Fill(pf), sp.fin);
            }
            return r.ToSprite(Vector2.zero, scale);
        }

        // ------------------------------------------------------------------ eyes (unit radius)
        static Sprite EyeSprite(System.Action<Raster> draw)
        {
            var r = new Raster(-1.7f, -2.1f, 1.7f, 1.4f, 40);
            draw(r);
            return r.ToSprite(Vector2.zero, 40);
        }
        static void Brow(Raster r) => r.Paint(r.Stroke(new[] { new Vector2(-1.3f, -1.7f), new Vector2(1.1f, -0.95f) }, 0.45f), U.Hex("#1a0d0d"));
        static void Shine(Raster r) => r.Paint(r.Circle(0.05f, -0.32f, 0.26f), Color.white);

        public static Sprite EyeNormal => eyeNormal ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1), Color.white);
            r.Paint(r.Circle(0.25f, 0, 0.6f), U.Hex("#0b0f14"));
            Shine(r);
        });
        public static Sprite EyeAngry => eyeAngry ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1), Color.white);
            r.Paint(r.Circle(0.22f, 0, 0.66f), U.Hex("#e0283c"));
            r.Paint(r.Circle(0.25f, 0, 0.36f), U.Hex("#0b0f14"));
            Shine(r);
            Brow(r);
        });
        public static Sprite EyeShark => eyeShark ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1), U.Hex("#0d1116"));
            Shine(r);
        });
        public static Sprite EyeSharkAngry => eyeSharkAngry ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1), U.Hex("#0d1116"));
            Shine(r);
            Brow(r);
        });
        public static Sprite EyeBlink => eyeBlink ??= EyeSprite(r =>
            r.Paint(r.Stroke(new[] { new Vector2(-1, 0), new Vector2(1, 0) }, 0.35f), U.Hex("#0b0f14")));
        public static Sprite EyeHappy => eyeHappy ??= EyeSprite(r =>
            r.Paint(r.Stroke(new[] { new Vector2(-0.9f, 0.3f), new Vector2(0, -0.5f), new Vector2(0.9f, 0.3f) }, 0.38f), U.Hex("#0b0f14")));

        /// White oval, rx = ry = 1, centred 0.7 behind the pivot (pectoral fin).
        public static Sprite FinOval => finOval ??= MakeFin();
        static Sprite MakeFin()
        {
            var r = new Raster(-1.8f, -1.05f, 0.4f, 1.05f, 48);
            r.Paint(r.Circle(-0.7f, 0, 1), Color.white);
            return r.ToSprite(Vector2.zero, 48);
        }

        /// Small player icon for the lives display.
        public static Sprite LifeIcon()
        {
            var r = new Raster(0, 0, 32, 20, 4);
            r.Paint(r.Fill(new Path().Move(2, 10).Line(8, 4).Line(8, 16)), U.Hex("#ffc93c"));
            r.Paint(r.Ellipse(18, 10, 12, 7.5f, 0), U.Hex("#3df2d0"));
            r.Paint(r.Stroke(Raster.QuadPts(new Vector2(12, 4.5f), new Vector2(18, 7), new Vector2(27, 8)), 2), U.Hex("#ffd447"));
            r.Paint(r.Circle(24, 8.5f, 2.4f), Color.white);
            r.Paint(r.Circle(24.6f, 8.5f, 1.4f), U.Hex("#0b0f14"));
            return r.ToSprite(new Vector2(16, 10), 4);
        }
    }

    /// <summary>GameObject rig for one fish: root(flip) → pivot(tilt, size) → tail/body/fin/eye.</summary>
    public sealed class FishView
    {
        public readonly GameObject root;
        readonly Transform pivot, tailT, finT, eyeT;
        readonly SpriteRenderer body, tail, fin, eye;
        readonly SortingGroup group;
        public readonly SpriteRenderer halo;
        FishArt.Art art;
        Species sp;

        public FishView(Transform parent, Transform haloParent)
        {
            root = new GameObject("Fish");
            root.transform.SetParent(parent, false);
            group = root.AddComponent<SortingGroup>();
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            tail = Gfx.SpriteObject("Tail", pivot, null, 0);
            tailT = tail.transform;
            body = Gfx.SpriteObject("Body", pivot, null, 1);
            fin = Gfx.SpriteObject("Fin", pivot, FishArt.FinOval, 2);
            finT = fin.transform;
            eye = Gfx.SpriteObject("Eye", pivot, FishArt.EyeNormal, 3);
            eyeT = eye.transform;
            halo = Gfx.SpriteObject("Halo", haloParent, Gfx.Glow, Layer.Halo);
            halo.enabled = false;
        }

        public void SetSpecies(Species s)
        {
            if (sp == s) return;
            sp = s;
            art = FishArt.Get(s);
            body.sprite = art.body;
            tail.sprite = art.tail;
            tailT.localPosition = new Vector3(-FishArt.HL * 0.84f, 0, 0);
            fin.enabled = !art.shark;
            fin.color = art.fin * new Color(1, 1, 1, 0.85f);
            eyeT.localPosition = art.eyePos;
            eyeT.localScale = new Vector3(art.eyeR, art.eyeR, 1);
        }

        public void SetActive(bool on) { if (root.activeSelf != on) root.SetActive(on); if (!on) halo.enabled = false; }

        public enum EyeMode { Normal, Angry, Blink, Happy }

        public void Pose(Fish f, int order, EyeMode eyeMode)
        {
            float fs = f.faceS;
            if (Mathf.Abs(fs) < 0.06f) fs = fs < 0 ? -0.06f : 0.06f;
            var t = root.transform;
            t.localPosition = U.V3(f.x, f.y);
            t.localScale = new Vector3(fs, 1, 1);
            pivot.localRotation = Quaternion.Euler(0, 0, -f.tilt * Mathf.Rad2Deg);
            pivot.localScale = new Vector3(f.r, f.r, 1);
            group.sortingOrder = order;

            float w = Mathf.Sin(f.wag) * 0.3f;
            float hh = art.hh;
            tailT.localPosition = new Vector3(-FishArt.HL * 0.84f, -w * hh * 0.45f, 0);
            tailT.localRotation = Quaternion.Euler(0, 0, -w * 1.3f * Mathf.Rad2Deg);
            body.sprite = f.mouth > 0.4f ? art.bodyOpen : art.body;
            if (!art.shark)
            {
                finT.localPosition = new Vector3(FishArt.HL * 0.22f, -hh * 0.25f, 0);
                finT.localRotation = Quaternion.Euler(0, 0, -(0.6f + Mathf.Sin(f.wag * 1.3f) * 0.35f) * Mathf.Rad2Deg);
                finT.localScale = new Vector3(FishArt.HL * 0.17f, Mathf.Max(hh * 0.12f, 0.02f), 1);
            }
            Sprite e;
            if (art.shark) e = eyeMode == EyeMode.Angry ? FishArt.EyeSharkAngry : FishArt.EyeShark;
            else e = eyeMode switch
            {
                EyeMode.Angry => FishArt.EyeAngry,
                EyeMode.Blink => FishArt.EyeBlink,
                EyeMode.Happy => FishArt.EyeHappy,
                _ => FishArt.EyeNormal,
            };
            eye.sprite = e;
        }

        public void SetVisible(bool v)
        {
            body.enabled = tail.enabled = eye.enabled = v;
            fin.enabled = v && art != null && !art.shark;
        }
    }
}
