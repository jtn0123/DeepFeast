using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Light hole punched into the depth darkness (world coords, world radius, strength).</summary>
    public struct Hole { public float x, y, r, str; public Hole(float x, float y, float r, float s) { this.x = x; this.y = y; this.r = r; str = s; } }

    /// <summary>Camera-relative layers: water gradient, parallax ridges, god rays, surface, darkness, snow, vignette.</summary>
    public sealed class SceneFx
    {
        readonly Transform screen;
        readonly Mesh bgMesh, raysMesh, darkMesh, snowMesh, surfaceMesh;
        readonly MeshRenderer darkMr, raysMr, surfaceMr;
        readonly SpriteRenderer vignette;
        readonly MeshBuilder mb = new MeshBuilder();

        sealed class FarLayer
        {
            public float p, s, baseY, amp, alpha, off; public int seed; public Noise1D n;
            public Transform t; public Mesh kelp;
            public float Ridge(float x) => baseY + n.At(x / 1500) * amp + n.At(x / 380 + 50) * amp * 0.12f;
        }
        readonly FarLayer[] far;

        struct Flake { public float x, y, z, ph; }
        readonly Flake[] snow = new Flake[110];

        public SceneFx(Camera camera, Transform worldRoot)
        {
            screen = new GameObject("Screen").transform;
            screen.SetParent(camera.transform, false);
            screen.localPosition = new Vector3(0, 0, 10);

            bgMesh = Gfx.MeshObject("Water", screen, Layer.Background).mesh;
            var rays = Gfx.MeshObject("Rays", screen, Layer.Rays, true);
            raysMesh = rays.mesh; raysMr = rays.mr;
            var dark = Gfx.MeshObject("Darkness", screen, Layer.Darkness);
            darkMesh = dark.mesh; darkMr = dark.mr;
            var sn = Gfx.MeshObject("Snow", screen, Layer.Snow);
            snowMesh = sn.mesh;
            sn.mr.sharedMaterial = new Material(Gfx.Alpha) { mainTexture = Gfx.Disc.texture };
            vignette = Gfx.SpriteObject("Vignette", screen, Gfx.Vignette, Layer.Vignette);

            var surf = Gfx.MeshObject("Surface", worldRoot, Layer.Sky);
            surfaceMesh = surf.mesh; surfaceMr = surf.mr;

            far = new[]
            {
                new FarLayer { p = 0.3f, s = 0.45f, n = new Noise1D(5), baseY = 2600, amp = 750, alpha = 0.24f, seed = 1, off = 3100 },
                new FarLayer { p = 0.55f, s = 0.68f, n = new Noise1D(9), baseY = 2800, amp = 850, alpha = 0.32f, seed = 2, off = 900 },
            };
            for (int i = 0; i < far.Length; i++) BuildFar(far[i], worldRoot, Layer.Far + i * 10);

            for (int i = 0; i < snow.Length; i++)
                snow[i] = new Flake { x = U.Rand(0, 4000), y = U.Rand(0, 4000), z = U.Rand(0.25f, 1), ph = U.Rand(0, U.TAU) };
        }

        void BuildFar(FarLayer L, Transform parent, int order)
        {
            var (go, mesh, _) = Gfx.MeshObject("Far" + L.seed, parent, order);
            L.t = go.transform;
            L.kelp = Gfx.MeshObject("FarKelp", L.t, order + 1).mesh;
            var col = new Color(2 / 255f, 22 / 255f, 44 / 255f, L.alpha);
            var m = new MeshBuilder();
            const float X0 = -5000, X1 = 15000, STEP = 30, BOT = 9500;
            int cols = 0;
            for (float x = X0; x <= X1; x += STEP, cols++)
            {
                Draw.V(m, x, L.Ridge(x), col);
                Draw.V(m, x, BOT, col);
            }
            for (int i = 0; i < cols - 1; i++) m.Quad(i * 2, i * 2 + 2, i * 2 + 3, i * 2 + 1);

            // rounded rock spires poking above the ridge (only the part above the ridge, so alpha doesn't stack)
            const float B = 460;
            var pts = new List<Vector2>();
            for (int b = Mathf.FloorToInt(X0 / B) - 1; b <= Mathf.FloorToInt(X1 / B) + 1; b++)
            {
                if (U.Hash(b * 3.1f + L.seed * 17) < 0.5f) continue;
                float sx = b * B + U.Hash(b * 7.7f + L.seed) * B, gy = L.Ridge(sx) + 60;
                float hgt = 300 + U.Hash(b * 1.3f + L.seed * 5) * 1000, wid = 60 + U.Hash(b * 2.9f + L.seed) * 130;
                pts.Clear();
                pts.AddRange(Draw.QuadPts(sx - wid, gy, sx - wid * 0.55f, gy - hgt * 0.5f, sx - wid * 0.25f, gy - hgt, 10));
                var q2 = Draw.QuadPts(sx - wid * 0.25f, gy - hgt, sx, gy - hgt * 1.04f, sx + wid * 0.3f, gy - hgt * 0.94f, 8);
                for (int i = 1; i < q2.Count; i++) pts.Add(q2[i]);
                var q3 = Draw.QuadPts(sx + wid * 0.3f, gy - hgt * 0.94f, sx + wid * 0.65f, gy - hgt * 0.4f, sx + wid, gy, 10);
                for (int i = 1; i < q3.Count; i++) pts.Add(q3[i]);
                int first = -1, prev = -1;
                foreach (var p in pts)
                {
                    float ry = L.Ridge(p.x);
                    float top = Mathf.Min(p.y, ry);
                    int a = Draw.V(m, p.x, top, col), c = Draw.V(m, p.x, ry, col);
                    if (prev >= 0) m.Quad(prev, a, c, prev + 1);
                    prev = a;
                    if (first < 0) first = a;
                }
            }
            m.Apply(mesh);
        }

        static float WaveY(float x, float time) => Mathf.Sin(x * 0.012f + time * 1.6f) * 7 + Mathf.Sin(x * 0.031f - time * 2.2f) * 3;

        public void Update(float time, Vector2 cam, Vector3 camUnity, float zoom, float refW, float refH, List<Hole> holes)
        {
            screen.localScale = new Vector3(1 / zoom, -1 / zoom, 1);
            float hw = refW / 2, hh = refH / 2;
            float yTop = cam.y - hh / zoom, yBot = cam.y + hh / zoom;
            float x0 = cam.x - hw / zoom, x1 = cam.x + hw / zoom;

            // water gradient (screen space, ref px, y-down, centred)
            mb.Clear();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f, y = -hh - 4 + (refH + 8) * t;
                var c = World.WaterAt(Mathf.Max(0, Mathf.Lerp(yTop, yBot, t)));
                mb.Vert(-hw - 4, y, c); mb.Vert(hw + 4, y, c);
                if (i > 0) mb.Quad((i - 1) * 2, (i - 1) * 2 + 1, i * 2 + 1, i * 2);
            }
            mb.Apply(bgMesh);

            // parallax layers
            foreach (var L in far)
            {
                float cx = cam.x * L.p + L.off, cy = cam.y;
                L.t.localPosition = new Vector3(camUnity.x - cx * L.s, camUnity.y + cy * L.s, 0);
                L.t.localScale = new Vector3(L.s, L.s, 1);
                float z = zoom * L.s;
                float fx0 = cx - hw / z - 60, fx1 = cx + hw / z + 60;
                mb.Clear();
                var kc = new Color(2 / 255f, 30 / 255f, 40 / 255f, L.alpha * 0.9f);
                const float K = 140;
                for (int b = Mathf.FloorToInt(fx0 / K); b <= Mathf.FloorToInt(fx1 / K) + 1; b++)
                {
                    if (U.Hash(b * 5.3f + L.seed * 9) < 0.72f) continue;
                    float kx = b * K + U.Hash(b + L.seed) * K, gy = L.Ridge(kx) + 30, kh = 300 + U.Hash(b * 9.1f) * 700;
                    float sw = Mathf.Sin(time * 0.6f + b) * 60;
                    var pts = Draw.CubicPts(kx, gy, kx + sw * 0.3f, gy - kh * 0.35f, kx - sw * 0.4f, gy - kh * 0.7f, kx + sw, gy - kh, 12);
                    Draw.Stroke(mb, pts, 10, 10, kc, true);
                }
                mb.Apply(L.kelp);
            }

            // god rays from the surface
            float sy = (0 - cam.y) * zoom + hh;
            float len = Mathf.Max(refH * 1.2f, 1900 * zoom);
            mb.Clear();
            if (sy + len >= 0)
            {
                const float spacing = 150;
                float shift = cam.x * zoom * 0.5f;
                int first = Mathf.FloorToInt(shift / spacing) - 3;
                for (int i = first; i < first + refW / spacing + 7; i++)
                {
                    float h = U.Hash(i * 1.37f);
                    if (h < 0.3f) continue;
                    float x = i * spacing - shift + h * 70 + Mathf.Sin(time * 0.25f + i) * 30 - hw;
                    float w = 18 + h * 60, slant = len * 0.22f, spread = len * (0.08f + h * 0.12f);
                    float a = (0.05f + 0.07f * h) * (0.75f + 0.25f * Mathf.Sin(time * 0.7f + i * 2.1f));
                    float top = sy - hh;
                    Color c0 = new Color(200 / 255f, 250 / 255f, 1, a), c1 = new Color(160 / 255f, 235 / 255f, 1, a * 0.35f), c2 = new Color(160 / 255f, 235 / 255f, 1, 0);
                    float l0 = x - w / 2, r0 = x + w / 2, l2 = x - w / 2 + slant - spread, r2 = x + w / 2 + slant + spread;
                    int v0 = mb.Vert(l0, top, c0), v1 = mb.Vert(r0, top, c0);
                    int v2 = mb.Vert(Mathf.Lerp(l0, l2, 0.6f), top + len * 0.6f, c1), v3 = mb.Vert(Mathf.Lerp(r0, r2, 0.6f), top + len * 0.6f, c1);
                    int v4 = mb.Vert(l2, top + len, c2), v5 = mb.Vert(r2, top + len, c2);
                    mb.Quad(v0, v1, v3, v2); mb.Quad(v2, v3, v5, v4);
                }
            }
            mb.Apply(raysMesh);
            raysMr.enabled = mb.v.Count > 0;

            // sea surface, sky and sun glare
            mb.Clear();
            if (yTop <= 120)
            {
                const float step = 24;
                float sx0 = Mathf.Floor((x0 - 40) / step) * step - step, sx1 = x1 + 40 + step;
                Color skyTop = U.Hex("#8fd6ff"), skyBot = U.Hex("#e6f8ff");
                int prev = -1;
                for (float x = sx0; x <= sx1; x += step)
                {
                    float wy = WaveY(x, time);
                    int a = Draw.V(mb, x, yTop - 100, skyTop);
                    int b = Draw.V(mb, x, wy, Color.Lerp(skyTop, skyBot, Mathf.Clamp01((wy - yTop) / Mathf.Max(1f, -yTop))));
                    if (prev >= 0) mb.Quad(prev, a, b, prev + 1);
                    prev = a;
                }
                // sun glare (clipped to above the water)
                float sunX = cam.x + (refW / zoom) * 0.25f;
                const int GX = 14, GY = 8;
                int g0 = mb.v.Count;
                for (int j = 0; j <= GY; j++)
                    for (int i = 0; i <= GX; i++)
                    {
                        float gx = sunX - 420 + 840f * i / GX, gy = -560 + 560f * j / GY;
                        float d = Mathf.Clamp01(Vector2.Distance(new Vector2(gx, gy), new Vector2(sunX, -140)) / 420f);
                        Draw.V(mb, gx, Mathf.Min(gy, WaveY(gx, time)), new Color(1, 1, 230 / 255f, 0.9f * (1 - d)));
                    }
                for (int j = 0; j < GY; j++)
                    for (int i = 0; i < GX; i++)
                    {
                        int a = g0 + j * (GX + 1) + i;
                        mb.Quad(a, a + 1, a + GX + 2, a + GX + 1);
                    }
                // wave line + under-surface glow
                var line = new List<Vector2>();
                for (float x = sx0; x <= sx1; x += step) line.Add(new Vector2(x, WaveY(x, time)));
                var glowA = new Color(210 / 255f, 1, 1, 0.35f); var glowB = new Color(210 / 255f, 1, 1, 0);
                prev = -1;
                foreach (var p in line)
                {
                    int a = Draw.V(mb, p.x, p.y, glowA), b = Draw.V(mb, p.x, 140, glowB);
                    if (prev >= 0) mb.Quad(prev, a, b, prev + 1);
                    prev = a;
                }
                Draw.Stroke(mb, line, Mathf.Max(3, 2.5f / zoom), Mathf.Max(3, 2.5f / zoom), new Color(1, 1, 1, 0.85f));
            }
            mb.Apply(surfaceMesh);
            surfaceMr.enabled = mb.v.Count > 0;

            // darkness of the deep, with soft holes around light sources
            if (Mathf.Max(World.DarkAt(yTop), World.DarkAt(yBot)) < 0.02f) darkMr.enabled = false;
            else
            {
                darkMr.enabled = true;
                mb.Clear();
                const int GX = 36, GY = 22;
                var dc = new Color(1 / 255f, 6 / 255f, 20 / 255f, 1);
                for (int j = 0; j <= GY; j++)
                {
                    float py = -hh - 6 + (refH + 12) * j / GY;
                    float baseA = World.DarkAt(cam.y + py / zoom);
                    for (int i = 0; i <= GX; i++)
                    {
                        float px = -hw - 6 + (refW + 12) * i / GX;
                        float a = baseA;
                        if (a > 0.001f)
                            foreach (var h in holes)
                            {
                                float hx = (h.x - cam.x) * zoom, hy = (h.y - cam.y) * zoom, R = h.r * zoom;
                                float dx = px - hx, dy = py - hy;
                                float d2 = dx * dx + dy * dy;
                                if (d2 >= R * R) continue;
                                a *= 1 - h.str * (1 - Mathf.Sqrt(d2) / R);
                            }
                        dc.a = a;
                        mb.Vert(px, py, dc);
                    }
                }
                for (int j = 0; j < GY; j++)
                    for (int i = 0; i < GX; i++)
                    {
                        int a = j * (GX + 1) + i;
                        mb.Quad(a, a + 1, a + GX + 2, a + GX + 1);
                    }
                mb.Apply(darkMesh);
            }

            // marine snow
            mb.Clear();
            float SW = refW + 40, SH = refH + 40;
            for (int i = 0; i < snow.Length; i++)
            {
                var p = snow[i];
                float fx = Mod(p.x - cam.x * zoom * p.z, SW) - 20;
                float fy = Mod(p.y - cam.y * zoom * p.z + time * 8 * p.z, SH) - 20;
                float rad = 0.6f + p.z * 1.6f;
                mb.TexQuad(new Vector3(fx + Mathf.Sin(time + p.ph) * 4 - hw, fy - hh, 0), rad, rad, new Color(220 / 255f, 245 / 255f, 1, 0.12f + p.z * 0.3f));
            }
            mb.Apply(snowMesh);

            vignette.transform.localScale = new Vector3(refW + 4, refH + 4, 1);
        }

        static float Mod(float a, float m) => ((a % m) + m) % m;
    }
}
