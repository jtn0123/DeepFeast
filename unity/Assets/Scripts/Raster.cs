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
        /// Sprite detail: art is painted at this many times its base resolution and its sprites map
        /// the extra pixels into the same size, so it only gets sharper. Radii given in pixels
        /// (blur, outline, lighting) are base pixels and grow with it.
        public static float Detail = 1;

        public readonly int W, H;
        readonly float minX, minY, scale;
        public readonly Color[] px;
        readonly float[] acc;
        readonly List<Vector2> xs = new List<Vector2>(64);

        public Raster(float minX, float minY, float maxX, float maxY, float scale)
        {
            this.minX = minX; this.minY = minY; this.scale = scale * Detail;
            W = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) * this.scale));
            H = Mathf.Max(2, Mathf.CeilToInt((maxY - minY) * this.scale));
            px = new Color[W * H];
            acc = new float[W + 2];
        }

        public float[] Mask() => new float[W * H];
        float PX(float x) => (x - minX) * scale;
        float PY(float y) => (y - minY) * scale;
        int Idx(int i, int j) => (H - 1 - j) * W + i;
        public float ShapeX(int i) => minX + (i + 0.5f) / scale;
        public float ShapeY(int row) => minY + (H - 1 - row + 0.5f) / scale;
        // A radius in base pixels, in this raster's pixels.
        static int Px(int rad) => rad <= 0 ? rad : Mathf.Max(1, Mathf.RoundToInt(rad * Detail));

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
            // acc stays zeroed between rows: each row clears just the span it touched.
            for (int j = j0; j <= j1; j++)
            {
                int left = W, right = -1;
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
                        int ia = (int)xa, ib = Mathf.Min(W - 1, (int)xb);
                        if (ia < left) left = ia;
                        if (ib > right) right = ib;
                        for (int i = ia; i <= ib; i++)
                        {
                            float ov = Mathf.Min(xb, i + 1) - Mathf.Max(xa, i);
                            if (ov > 0) acc[i] += ov / SS;
                        }
                    }
                }
                for (int i = left; i <= right; i++)
                {
                    float a = Mathf.Min(1f, acc[i]) * alpha;
                    acc[i] = 0;
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
            var ring = Erode(m, Px(rad));
            for (int i = 0; i < m.Length; i++) { float v = m[i] - ring[i]; ring[i] = v > 0 ? v : 0; }
            return ring;
        }

        // The minimum over a square of side 2 rad + 1, outside counting as empty: rows, then
        // columns, a few steps per pixel at any radius (van Herk / Gil-Werman).
        float[] Erode(float[] m, int rad)
        {
            var tmp = new float[m.Length];
            var ero = new float[m.Length];
            int n = Mathf.Max(W, H);
            float[] g = new float[n], h = new float[n];
            for (int y = 0; y < H; y++) MinLine(m, tmp, y * W, 1, W, rad, g, h);
            for (int x = 0; x < W; x++) MinLine(tmp, ero, x, W, H, rad, g, h);
            return ero;
        }

        static void MinLine(float[] src, float[] dst, int start, int stride, int len, int rad, float[] g, float[] h)
        {
            int k = 2 * rad + 1;
            // g: running minimum from the start of each block of k; h: from the end of it.
            for (int i = 0, b = 0, at = start; i < len; i++, at += stride)
            {
                float v = src[at];
                g[i] = b == 0 || v < g[i - 1] ? v : g[i - 1];
                if (++b == k) b = 0;
            }
            for (int i = len - 1, at = start + (len - 1) * stride; i >= 0; i--, at -= stride)
            {
                float v = src[at];
                h[i] = i == len - 1 || (i + 1) % k == 0 || v < h[i + 1] ? v : h[i + 1];
            }
            // Any window that reaches past the ends takes in empty space.
            for (int i = 0, at = start; i < len; i++, at += stride)
            {
                int a = i - rad, z = i + rad;
                if (a < 0 || z >= len) { dst[at] = 0; continue; }
                float p = h[a], q = g[z];
                dst[at] = p < q ? p : q;
            }
        }

        /// Separable box blur (3 passes ≈ gaussian); radius in base pixels, outside counts as empty.
        public float[] Blur(float[] m, int rad, int passes = 3) => BoxBlur(m, Px(rad), passes);

        float[] BoxBlur(float[] m, int rad, int passes)
        {
            var a = (float[])m.Clone();
            var b = new float[m.Length];
            var col = new float[W];
            float inv = 1f / (2 * rad + 1);
            for (int p = 0; p < passes; p++)
            {
                for (int y = 0; y < H; y++)
                {
                    int o = y * W;
                    float s = 0;
                    for (int k = 0; k <= rad && k < W; k++) s += a[o + k];
                    for (int x = 0; x < W; x++)
                    {
                        b[o + x] = s * inv;
                        if (x + rad + 1 < W) s += a[o + x + rad + 1];
                        if (x - rad >= 0) s -= a[o + x - rad];
                    }
                }
                // Columns keep one running sum each and walk down the rows, in memory order.
                Array.Clear(col, 0, W);
                for (int k = 0; k <= rad && k < H; k++)
                    for (int x = 0, o = k * W; x < W; x++) col[x] += b[o + x];
                for (int y = 0; y < H; y++)
                {
                    int o = y * W, add = (y + rad + 1) * W, sub = (y - rad) * W;
                    for (int x = 0; x < W; x++) a[o + x] = col[x] * inv;
                    if (y + rad + 1 < H) for (int x = 0; x < W; x++) col[x] += b[add + x];
                    if (y - rad >= 0) for (int x = 0; x < W; x++) col[x] -= b[sub + x];
                }
            }
            return a;
        }

        /// Pseudo-3D shading: the blurred mask is treated as a height field and lit from `light`
        /// (image space, y up). Returns n·L − L.z per pixel: 0 on flat tops, + on lit slopes, − in shade.
        public float[] Light(float[] mask, int rad, Vector3 light, float depth = 2f)
        {
            var h = BoxBlur(mask, Px(Mathf.Max(1, rad)), 3);
            var L = light.normalized;
            var s = new float[mask.Length];
            // The finer the pixels, the gentler each step of the blurred slope.
            float k = rad * Detail * depth;
            for (int y = 0; y < H; y++)
            {
                int up = (y < H - 1 ? y + 1 : y) * W, dn = (y > 0 ? y - 1 : y) * W, o = y * W;
                for (int x = 0; x < W; x++)
                {
                    int i = o + x;
                    if (mask[i] <= 0) continue;
                    float nx = -(h[o + (x < W - 1 ? x + 1 : x)] - h[o + (x > 0 ? x - 1 : x)]) * 0.5f * k;
                    float ny = -(h[up + x] - h[dn + x]) * 0.5f * k;
                    s[i] = (nx * L.x + ny * L.y + L.z) / Mathf.Sqrt(nx * nx + ny * ny + 1) - L.z;
                }
            }
            return s;
        }

        /// Soft, rounded dilation of a mask by roughly `rad` pixels.
        public float[] Dilate(float[] m, int rad)
        {
            var b = BoxBlur(m, Px(rad / 2 + 1), 2);
            var d = new float[m.Length];
            for (int i = 0; i < m.Length; i++) d[i] = Mathf.Max(m[i], Mathf.Clamp01((b[i] - 0.03f) / 0.1f));
            return d;
        }

        /// Paint with a colour that depends on the pixel index and shape-space position.
        public void Paint(float[] mask, Func<int, float, float, Color> colorAt)
        {
            for (int row = 0; row < H; row++)
            {
                float y = ShapeY(row);
                for (int i = 0; i < W; i++)
                {
                    int id = row * W + i;
                    if (mask[id] <= 0.0005f) continue;
                    var c = colorAt(id, ShapeX(i), y);
                    Over(id, c, mask[id] * c.a);
                }
            }
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

        /// Spreads the art's edge colours a few pixels out into the transparent space around it, so
        /// filtering never pulls in a dark fringe, and gives the rest of that space the art's
        /// average colour so the smaller mips stay clean too.
        void Bleed()
        {
            int n = px.Length;
            var done = new bool[n];
            float sr = 0, sg = 0, sb = 0, sa = 0;
            for (int i = 0; i < n; i++)
            {
                var c = px[i];
                if (c.a <= 0.02f) continue;
                done[i] = true;
                sr += c.r * c.a; sg += c.g * c.a; sb += c.b * c.a; sa += c.a;
            }
            if (sa <= 0) return;

            var ring = new List<int>();
            var queued = new bool[n];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int id = y * W + x;
                    if (!done[id] && TouchesDone(done, x, y)) { ring.Add(id); queued[id] = true; }
                }
            var next = new List<int>();
            var grown = new List<Color>();
            for (int step = 0, steps = Mathf.CeilToInt(3 * Detail); step < steps && ring.Count > 0; step++)
            {
                grown.Clear();
                foreach (int id in ring)
                {
                    int x = id % W, y = id / W;
                    float r = 0, g = 0, b = 0, k = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            int j = yy * W + xx;
                            if (!done[j]) continue;
                            var c = px[j];
                            r += c.r; g += c.g; b += c.b; k++;
                        }
                    grown.Add(new Color(r / k, g / k, b / k, px[id].a));
                }
                next.Clear();
                for (int i = 0; i < ring.Count; i++) { px[ring[i]] = grown[i]; done[ring[i]] = true; }
                foreach (int id in ring)
                {
                    int x = id % W, y = id / W;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            int j = yy * W + xx;
                            if (done[j] || queued[j]) continue;
                            queued[j] = true;
                            next.Add(j);
                        }
                }
                (ring, next) = (next, ring);
            }
            var fill = new Color(sr / sa, sg / sa, sb / sa, 0);
            for (int i = 0; i < n; i++) if (!done[i]) px[i] = new Color(fill.r, fill.g, fill.b, px[i].a);
        }

        bool TouchesDone(bool[] done, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && yy >= 0 && xx < W && yy < H && done[yy * W + xx]) return true;
                }
            return false;
        }

        /// Generated textures and their pixels so far, for the startup report.
        public static int Baked { get; private set; }
        public static long BakedPixels { get; private set; }

        /// When set (harness flag -dumpart), every baked texture is also written here as a PNG.
        public static string DumpDir;
        static int dumpN;

        bool finished;

        /// Readies the pixels for upload. Thread-safe, so painting threads can do it themselves.
        public void Finish()
        {
            if (finished) return;
            Bleed();
            finished = true;
        }

        public Texture2D ToTexture()
        {
            Finish();
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            Baked++; BakedPixels += (long)W * H;
            t.SetPixels(px);
            if (DumpDir != null) System.IO.File.WriteAllBytes(System.IO.Path.Combine(DumpDir, $"{dumpN++:000}_{W}x{H}.png"), t.EncodeToPNG());
            t.Apply(true, true);
            return t;
        }

        /// pivot is given in shape space; ppuUnits = how many shape units map to one Unity unit.
        public Sprite ToSprite(Vector2 pivotShape, float ppu)
        {
            var tex = ToTexture();
            var piv = new Vector2((pivotShape.x - minX) * scale / W, 1f - (pivotShape.y - minY) * scale / H);
            return Sprite.Create(tex, new Rect(0, 0, W, H), piv, ppu * Detail, 0, SpriteMeshType.FullRect);
        }
    }
}
