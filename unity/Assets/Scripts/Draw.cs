using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>
    /// Immediate-style vector helpers that append to a MeshBuilder.
    /// All inputs are in game space (y-down); output vertices are Unity space.
    /// </summary>
    public static class Draw
    {
        static readonly List<Vector2> tmp = new List<Vector2>(64);

        public static int V(MeshBuilder m, float x, float y, Color c) => m.Vert(x, -y, c);

        public static void Ellipse(MeshBuilder m, float cx, float cy, float rx, float ry, float rot, Color c, int n = 14)
        {
            int center = V(m, cx, cy, c);
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            int first = m.v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * U.TAU / n, lx = Mathf.Cos(a) * rx, ly = Mathf.Sin(a) * ry;
                V(m, cx + lx * cs - ly * sn, cy + lx * sn + ly * cs, c);
            }
            for (int i = 0; i < n; i++) m.Tri(center, first + i, first + (i + 1) % n);
        }

        public static void Circle(MeshBuilder m, float cx, float cy, float r, Color c, int n = 10) => Ellipse(m, cx, cy, r, r, 0, c, n);

        /// Radial gradient disc: centre colour fading to edge colour.
        public static void RadialDisc(MeshBuilder m, float cx, float cy, float r, Color inner, Color outer, int n = 18)
        {
            int center = V(m, cx, cy, inner);
            int first = m.v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = i * U.TAU / n;
                V(m, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, outer);
            }
            for (int i = 0; i < n; i++) m.Tri(center, first + i, first + (i + 1) % n);
        }

        /// Thick polyline. width may taper from w0 to w1. Optional round end cap.
        public static void Stroke(MeshBuilder m, IList<Vector2> pts, float w0, float w1, Color c, bool capEnd = false, bool capStart = false)
            => Stroke(m, pts, w0, w1, c, c, capEnd, capStart);

        /// Thick polyline whose colour runs from c0 at the start to c1 at the end.
        public static void Stroke(MeshBuilder m, IList<Vector2> pts, float w0, float w1, Color c0, Color c1, bool capEnd = false, bool capStart = false)
        {
            int n = pts.Count;
            if (n < 2) return;
            int first = m.v.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 d = b - a;
                float l = d.magnitude;
                Vector2 nrm = l > 1e-6f ? new Vector2(-d.y / l, d.x / l) : Vector2.up;
                float t = i / (float)(n - 1);
                float hw = Mathf.Lerp(w0, w1, t) * 0.5f;
                var c = Color.Lerp(c0, c1, t);
                var p = pts[i];
                V(m, p.x + nrm.x * hw, p.y + nrm.y * hw, c);
                V(m, p.x - nrm.x * hw, p.y - nrm.y * hw, c);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = first + i * 2;
                m.Quad(a, a + 2, a + 3, a + 1);
            }
            if (capEnd) Circle(m, pts[n - 1].x, pts[n - 1].y, w1 * 0.5f, c1, 8);
            if (capStart) Circle(m, pts[0].x, pts[0].y, w0 * 0.5f, c0, 8);
        }

        /// Ribbon whose width tapers w0 → w1 and ripples by ±frill (a frilly edge), coloured c0 → c1.
        public static void Ribbon(MeshBuilder m, IList<Vector2> pts, float w0, float w1, float frill, float phase, Color c0, Color c1)
        {
            int n = pts.Count;
            if (n < 2) return;
            int first = m.v.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[Mathf.Max(0, i - 1)], b = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 d = b - a;
                float l = d.magnitude;
                Vector2 nrm = l > 1e-6f ? new Vector2(-d.y / l, d.x / l) : Vector2.up;
                float t = i / (float)(n - 1);
                float hw = Mathf.Lerp(w0, w1, t) * (1 + frill * Mathf.Sin(t * 26 + phase)) * 0.5f;
                var c = Color.Lerp(c0, c1, t);
                var p = pts[i];
                V(m, p.x + nrm.x * hw, p.y + nrm.y * hw, c);
                V(m, p.x - nrm.x * hw, p.y - nrm.y * hw, c);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = first + i * 2;
                m.Quad(a, a + 2, a + 3, a + 1);
            }
        }

        /// Leaf blade along the quadratic curve a → c → b, widest a third of the way out and pointed at b.
        /// The half facing up takes the top colour and the other half the under colour, split by a midrib, inside a dark rim.
        public static void Leaf(MeshBuilder m, Vector2 a, Vector2 c, Vector2 b, float w, Color top, Color under, Color rim, float rimW, int n = 9)
        {
            // with y down the left-hand normal of a rightward leaf points down, so that side is the underside
            Color plus = b.x >= a.x ? under : top, minus = b.x >= a.x ? top : under;
            Color vein = Color.Lerp(under, rim, 0.55f);
            for (int pass = 0; pass < 2; pass++)
            {
                int first = m.v.Count;
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n, u = 1 - t;
                    var p = u * u * a + 2 * u * t * c + t * t * b;
                    var d = 2 * u * (c - a) + 2 * t * (b - c);
                    float l = d.magnitude;
                    var nrm = l > 1e-6f ? new Vector2(-d.y / l, d.x / l) : Vector2.up;
                    float hw = w * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.65f));
                    if (pass == 0)
                    {
                        // rim: the blade grown by rimW, drawn out to a point past the tip
                        if (i == n) { p += d / Mathf.Max(l, 1e-6f) * rimW * 1.6f; hw = 0; }
                        else hw += rimW;
                        V(m, p.x + nrm.x * hw, p.y + nrm.y * hw, rim);
                        V(m, p.x - nrm.x * hw, p.y - nrm.y * hw, rim);
                    }
                    else
                    {
                        // two halves with their own centre vertices so the colours meet in a crisp line
                        V(m, p.x + nrm.x * hw, p.y + nrm.y * hw, plus);
                        V(m, p.x, p.y, Color.Lerp(plus, Color.white, 0.1f));
                        V(m, p.x, p.y, Color.Lerp(minus, Color.white, 0.1f));
                        V(m, p.x - nrm.x * hw, p.y - nrm.y * hw, minus);
                    }
                }
                int stride = pass == 0 ? 2 : 4;
                for (int i = 0; i < n; i++)
                {
                    int k = first + i * stride;
                    m.Quad(k, k + stride, k + stride + 1, k + 1);
                    if (pass == 1) m.Quad(k + 2, k + stride + 2, k + stride + 3, k + 3);
                }
            }
            // midrib fading out before the tip
            var rib = QuadPts(a.x, a.y, c.x, c.y, b.x, b.y, n);
            int keep = n * 4 / 5 + 1;
            rib.RemoveRange(keep, rib.Count - keep);
            Stroke(m, rib, rimW * 0.9f, rimW * 0.2f, vein);
        }

        public static List<Vector2> QuadPts(float ax, float ay, float cx, float cy, float bx, float by, int n = 8)
        {
            tmp.Clear();
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                tmp.Add(new Vector2(u * u * ax + 2 * u * t * cx + t * t * bx, u * u * ay + 2 * u * t * cy + t * t * by));
            }
            return tmp;
        }

        public static List<Vector2> CubicPts(float ax, float ay, float c1x, float c1y, float c2x, float c2y, float bx, float by, int n = 10)
        {
            tmp.Clear();
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                float a = u * u * u, b = 3 * u * u * t, c = 3 * u * t * t, d = t * t * t;
                tmp.Add(new Vector2(a * ax + b * c1x + c * c2x + d * bx, a * ay + b * c1y + c * c2y + d * by));
            }
            return tmp;
        }

        /// Convex-ish polygon as a fan from its first point.
        public static void Fan(MeshBuilder m, IList<Vector2> pts, Color c)
        {
            int first = m.v.Count;
            foreach (var p in pts) V(m, p.x, p.y, c);
            for (int i = 1; i < pts.Count - 1; i++) m.Tri(first, first + i, first + i + 1);
        }

        /// Axis-aligned rect with a vertical gradient (top colour → bottom colour).
        public static void RectV(MeshBuilder m, float x0, float y0, float x1, float y1, Color top, Color bot)
        {
            int a = V(m, x0, y0, top), b = V(m, x1, y0, top), c = V(m, x1, y1, bot), d = V(m, x0, y1, bot);
            m.Quad(a, b, c, d);
        }
    }
}
