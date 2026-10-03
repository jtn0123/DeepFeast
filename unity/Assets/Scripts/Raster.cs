using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Vector path made of flattened polygons, in y-down "shape" space.</summary>
    public sealed class Path
    {
        public readonly List<List<Vector2>> polys = new List<List<Vector2>>();
        List<Vector2> cur;
        Vector2 p;

        public Path Move(float x, float y)
        {
            cur = new List<Vector2> { new Vector2(x, y) };
            polys.Add(cur);
            p = new Vector2(x, y);
            return this;
        }
        public Path Line(float x, float y)
        {
            cur.Add(new Vector2(x, y));
            p = new Vector2(x, y);
            return this;
        }
        public Path Quad(float cx, float cy, float x, float y, int n = 14)
        {
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, m = 1 - t;
                cur.Add(new Vector2(m * m * p.x + 2 * m * t * cx + t * t * x, m * m * p.y + 2 * m * t * cy + t * t * y));
            }
            p = new Vector2(x, y);
            return this;
        }
        public Path Cubic(float c1x, float c1y, float c2x, float c2y, float x, float y, int n = 22)
        {
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, m = 1 - t;
                float a = m * m * m, b = 3 * m * m * t, c = 3 * m * t * t, d = t * t * t;
                cur.Add(new Vector2(a * p.x + b * c1x + c * c2x + d * x, a * p.y + b * c1y + c * c2y + d * y));
            }
            p = new Vector2(x, y);
            return this;
        }
        public Path Poly(IList<Vector2> pts)
        {
            Move(pts[0].x, pts[0].y);
            for (int i = 1; i < pts.Count; i++) Line(pts[i].x, pts[i].y);
            return this;
        }
    }

    /// <summary>
    /// Tiny software rasteriser used to paint all game art at startup
    /// (fish, coral, anemones...). Masks are float coverage buffers; Paint()
    /// composites a colour through a mask. Shape space is y-down.
    /// </summary>
    public sealed class Raster
    {
        public readonly int W, H;
        readonly float minX, minY, scale;
        public readonly Color[] px;
        readonly float[] acc;
        readonly List<Vector2> xs = new List<Vector2>(64);

        public Raster(float minX, float minY, float maxX, float maxY, float scale)
        {
            this.minX = minX; this.minY = minY; this.scale = scale;
            W = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) * scale));
            H = Mathf.Max(2, Mathf.CeilToInt((maxY - minY) * scale));
            px = new Color[W * H];
            acc = new float[W + 2];
        }

        public float[] Mask() => new float[W * H];
        float PX(float x) => (x - minX) * scale;
        float PY(float y) => (y - minY) * scale;
        int Idx(int i, int j) => (H - 1 - j) * W + i;
        public float ShapeX(int i) => minX + (i + 0.5f) / scale;
        public float ShapeY(int row) => minY + (H - 1 - row + 0.5f) / scale;

        /// Scanline fill with non-zero winding and 4x vertical / analytic horizontal AA.
        public float[] Fill(Path path, float[] mask = null, float alpha = 1f)
        {
            mask ??= Mask();
            var edges = new List<Vector4>();
            float ymin = float.MaxValue, ymax = float.MinValue;
            foreach (var poly in path.polys)
            {
                int n = poly.Count;
                if (n < 3) continue;
                for (int k = 0; k < n; k++)
                {
                    Vector2 a = poly[k], b = poly[(k + 1) % n];
                    var e = new Vector4(PX(a.x), PY(a.y), PX(b.x), PY(b.y));
                    edges.Add(e);
                    ymin = Mathf.Min(ymin, Mathf.Min(e.y, e.w));
                    ymax = Mathf.Max(ymax, Mathf.Max(e.y, e.w));
                }
            }
            if (edges.Count == 0) return mask;
            const int SS = 4;
            int j0 = Mathf.Max(0, Mathf.FloorToInt(ymin)), j1 = Mathf.Min(H - 1, Mathf.CeilToInt(ymax));
            for (int j = j0; j <= j1; j++)
            {
                Array.Clear(acc, 0, acc.Length);
                bool any = false;
                for (int s = 0; s < SS; s++)
                {
                    float sy = j + (s + 0.5f) / SS;
                    xs.Clear();
                    foreach (var e in edges)
                    {
                        float y0 = e.y, y1 = e.w;
                        if (y0 == y1) continue;
                        bool down = y1 > y0;
                        float lo = down ? y0 : y1, hi = down ? y1 : y0;
                        if (sy < lo || sy >= hi) continue;
                        float t = (sy - y0) / (y1 - y0);
                        xs.Add(new Vector2(e.x + (e.z - e.x) * t, down ? 1 : -1));
                    }
                    if (xs.Count < 2) continue;
                    xs.Sort((p, q) => p.x.CompareTo(q.x));
                    int wind = 0;
                    for (int k = 0; k < xs.Count - 1; k++)
                    {
                        wind += (int)xs[k].y;
                        if (wind == 0) continue;
                        float xa = Mathf.Max(0, xs[k].x), xb = Mathf.Min(W, xs[k + 1].x);
                        if (xb <= xa) continue;
                        any = true;
                        int ia = (int)xa, ib = Mathf.Min(W - 1, (int)xb);
                        for (int i = ia; i <= ib; i++)
                        {
                            float ov = Mathf.Min(xb, i + 1) - Mathf.Max(xa, i);
                            if (ov > 0) acc[i] += ov / SS;
                        }
                    }
                }
                if (!any) continue;
                for (int i = 0; i < W; i++)
                {
                    float a = Mathf.Min(1f, acc[i]) * alpha;
                    if (a <= 0) continue;
                    int id = Idx(i, j);
                    if (a > mask[id]) mask[id] = a;
                }
            }
            return mask;
        }

        public float[] Circle(float cx, float cy, float r, float[] mask = null, float alpha = 1f)
            => Ellipse(cx, cy, r, r, 0, mask, alpha);

        public float[] Ellipse(float cx, float cy, float rx, float ry, float rot, float[] mask = null, float alpha = 1f)
        {
            mask ??= Mask();
            float pcx = PX(cx), pcy = PY(cy), prx = rx * scale, pry = ry * scale;
            float rr = Mathf.Max(prx, pry) + 2;
            int i0 = Mathf.Max(0, (int)(pcx - rr)), i1 = Mathf.Min(W - 1, (int)(pcx + rr));
            int j0 = Mathf.Max(0, (int)(pcy - rr)), j1 = Mathf.Min(H - 1, (int)(pcy + rr));
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot), mn = Mathf.Max(0.5f, Mathf.Min(prx, pry));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float dx = i + 0.5f - pcx, dy = j + 0.5f - pcy;
                    float lx = dx * cs + dy * sn, ly = -dx * sn + dy * cs;
                    float k = Mathf.Sqrt((lx * lx) / (prx * prx) + (ly * ly) / (pry * pry));
                    float a = Mathf.Clamp01((1 - k) * mn + 0.5f) * alpha;
                    if (a <= 0) continue;
                    int id = Idx(i, j);
                    if (a > mask[id]) mask[id] = a;
                }
            return mask;
        }

        /// Thick polyline with round joins.
        public float[] Stroke(IList<Vector2> pts, float w, float[] mask = null, float alpha = 1f)
        {
            mask ??= Mask();
            float h = w * 0.5f;
            for (int k = 0; k < pts.Count - 1; k++)
            {
                Vector2 a = pts[k], b = pts[k + 1], d = b - a;
                if (d.sqrMagnitude < 1e-10f) continue;
                Vector2 n = new Vector2(-d.y, d.x).normalized * h;
                var p = new Path().Move(a.x + n.x, a.y + n.y).Line(b.x + n.x, b.y + n.y).Line(b.x - n.x, b.y - n.y).Line(a.x - n.x, a.y - n.y);
                Fill(p, mask, alpha);
            }
            foreach (var q in pts) Circle(q.x, q.y, h, mask, alpha);
            return mask;
        }

        public static List<Vector2> QuadPts(Vector2 a, Vector2 c, Vector2 b, int n = 16)
        {
            var l = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, m = 1 - t;
                l.Add(m * m * a + 2 * m * t * c + t * t * b);
            }
            return l;
        }

        public static void Mul(float[] a, float[] b) { for (int i = 0; i < a.Length; i++) a[i] *= b[i]; }
        public static void Sub(float[] a, float[] b) { for (int i = 0; i < a.Length; i++) a[i] = Mathf.Max(0, a[i] - b[i]); }

        /// Ring just inside the mask edge (mask minus its erosion) – used for outlines.
        public float[] InnerRing(float[] m, int rad)
        {
            var tmp = new float[m.Length];
            var ero = new float[m.Length];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float mn = 1;
                    for (int k = -rad; k <= rad; k++) { int xx = x + k; mn = Mathf.Min(mn, xx < 0 || xx >= W ? 0 : m[y * W + xx]); }
                    tmp[y * W + x] = mn;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float mn = 1;
                    for (int k = -rad; k <= rad; k++) { int yy = y + k; mn = Mathf.Min(mn, yy < 0 || yy >= H ? 0 : tmp[yy * W + x]); }
                    ero[y * W + x] = mn;
                }
            var ring = new float[m.Length];
            for (int i = 0; i < m.Length; i++) ring[i] = Mathf.Max(0, m[i] - ero[i]);
            return ring;
        }

        public void Paint(float[] mask, Color c)
        {
            for (int i = 0; i < px.Length; i++)
            {
                float a = mask[i] * c.a;
                if (a <= 0.0005f) continue;
                Over(i, c, a);
            }
        }

        /// Paint with a colour that depends on shape-space position.
        public void Paint(float[] mask, Func<float, float, Color> colorAt)
        {
            for (int row = 0; row < H; row++)
            {
                float y = ShapeY(row);
                for (int i = 0; i < W; i++)
                {
                    int id = row * W + i;
                    if (mask[id] <= 0.0005f) continue;
                    var c = colorAt(ShapeX(i), y);
                    Over(id, c, mask[id] * c.a);
                }
            }
        }

        void Over(int i, Color c, float a)
        {
            var d = px[i];
            float oa = a + d.a * (1 - a);
            if (oa <= 0) return;
            float k = d.a * (1 - a);
            px[i] = new Color((c.r * a + d.r * k) / oa, (c.g * a + d.g * k) / oa, (c.b * a + d.b * k) / oa, oa);
        }

        /// Copies colour into transparent neighbours so bilinear filtering has no dark fringe.
        void Bleed()
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var src = (Color[])px.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int id = y * W + x;
                        if (src[id].a > 0.02f) continue;
                        float r = 0, g = 0, b = 0, n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                                var s = src[yy * W + xx];
                                if (s.a <= 0.02f) continue;
                                r += s.r; g += s.g; b += s.b; n++;
                            }
                        if (n > 0) px[id] = new Color(r / n, g / n, b / n, src[id].a);
                    }
            }
        }

        public Texture2D ToTexture()
        {
            Bleed();
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        /// pivot is given in shape space; ppuUnits = how many shape units map to one Unity unit.
        public Sprite ToSprite(Vector2 pivotShape, float ppu)
        {
            var tex = ToTexture();
            var piv = new Vector2((pivotShape.x - minX) * scale / W, 1f - (pivotShape.y - minY) * scale / H);
            return Sprite.Create(tex, new Rect(0, 0, W, H), piv, ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
