using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Light hole punched into the depth darkness (world coords, world radius, strength).</summary>
    public struct Hole { public float x, y, r, str; public Hole(float x, float y, float r, float s) { this.x = x; this.y = y; this.r = r; str = s; } }

    /// <summary>
    /// Camera-relative layers: water gradient, misty parallax reef, distant fish schools, god rays,
    /// surface (sky, glare, caustics), darkness, marine snow, out-of-focus motes and the vignette.
    /// </summary>
    public sealed class SceneFx
    {
        readonly Transform screen;
        readonly Mesh bgMesh, raysMesh, darkMesh, snowMesh, moteMesh, schoolMesh, surfaceMesh, surfCausticMesh;
        readonly MeshRenderer darkMr, raysMr, surfaceMr, surfCausticMr;
        readonly SpriteRenderer vignette, sunBloom;
        readonly MeshBuilder mb = new MeshBuilder();
        float lastTime;

        sealed class FarLayer
        {
            public float p, s, baseY, amp, alpha, off; public int seed; public Noise1D n;
            public Transform t; public Mesh kelp;
            public readonly List<(EnvironmentArt.Instance reef, EnvironmentArt.Instance abyss)> paintings = new List<(EnvironmentArt.Instance, EnvironmentArt.Instance)>();
            public float Ridge(float x) => baseY + n.At(x / 1500) * amp + n.At(x / 380 + 50) * amp * 0.12f;
        }
        readonly FarLayer[] far;

        struct Flake { public float x, y, z, ph; }
        readonly Flake[] snow = new Flake[120];
        readonly Flake[] motes = new Flake[14];

        /// A distant school: lives in world depth, drawn with parallax factor k.
        sealed class BgSchool { public float x, y, vx, k, size, alpha; public Vector3[] fish; }
        readonly BgSchool[] schools = new BgSchool[5];
        bool schoolsPlaced;

        static readonly Color FarCol = new Color(2 / 255f, 26 / 255f, 50 / 255f);

        public SceneFx(Camera camera, Transform worldRoot)
        {
            screen = new GameObject("Screen").transform;
            screen.SetParent(camera.transform, false);
            screen.localPosition = new Vector3(0, 0, 10);

            bgMesh = Gfx.MeshObject("Water", screen, Layer.Background).mesh;
            schoolMesh = Gfx.MeshObject("BgSchools", screen, Layer.BgFish).mesh;
            var rays = Gfx.MeshObject("Rays", screen, Layer.Rays, true);
            raysMesh = rays.mesh; raysMr = rays.mr;
            var dark = Gfx.MeshObject("Darkness", screen, Layer.Darkness);
            darkMesh = dark.mesh; darkMr = dark.mr;
            var sn = Gfx.MeshObject("Snow", screen, Layer.Snow);
            snowMesh = sn.mesh;
            sn.mr.sharedMaterial = new Material(Gfx.Alpha) { mainTexture = Gfx.Disc.texture };
            var mo = Gfx.MeshObject("Motes", screen, Layer.Snow + 1);
            moteMesh = mo.mesh;
            mo.mr.sharedMaterial = new Material(Gfx.Alpha) { mainTexture = Gfx.Glow.texture };
            vignette = Gfx.SpriteObject("Vignette", screen, Gfx.Vignette, Layer.Vignette);

            var surf = Gfx.MeshObject("Surface", worldRoot, Layer.Sky);
            surfaceMesh = surf.mesh; surfaceMr = surf.mr;
            var sc = Gfx.MeshObject("SurfaceCaustics", worldRoot, Layer.Rays + 2, true);
            surfCausticMesh = sc.mesh; surfCausticMr = sc.mr;
            sc.mr.sharedMaterial = new Material(Gfx.Additive) { mainTexture = Gfx.Caustics };
            sunBloom = Gfx.SpriteObject("SunBloom", worldRoot, Gfx.Glow, Layer.Rays + 1, true);

            far = new[]
            {
                new FarLayer { p = 0.18f, s = 0.3f, n = new Noise1D(3), baseY = 1360, amp = 400, alpha = 0.17f, seed = 1, off = 5200 },
                new FarLayer { p = 0.32f, s = 0.46f, n = new Noise1D(5), baseY = 1610, amp = 480, alpha = 0.23f, seed = 2, off = 3100 },
                new FarLayer { p = 0.55f, s = 0.68f, n = new Noise1D(9), baseY = 1930, amp = 520, alpha = 0.29f, seed = 3, off = 900 },
            };
            if (EnvironmentArt.Geometry("reef-shelf") != null)
            {
                far[0].baseY = 1220; far[0].amp = 140;
                far[1].baseY = 1490; far[1].amp = 180;
                far[2].baseY = 1840; far[2].amp = 220;
            }
            for (int i = 0; i < far.Length; i++) BuildFar(far[i], worldRoot, Layer.Far + i * 10);

            for (int i = 0; i < snow.Length; i++)
                snow[i] = new Flake { x = U.Rand(0, 4000), y = U.Rand(0, 4000), z = U.Rand(0.25f, 1), ph = U.Rand(0, U.TAU) };
            for (int i = 0; i < motes.Length; i++)
                motes[i] = new Flake { x = U.Rand(0, 4000), y = U.Rand(0, 4000), z = U.Rand(1.3f, 2.2f), ph = U.Rand(0, U.TAU) };
            for (int i = 0; i < schools.Length; i++) schools[i] = new BgSchool();
        }

        // ------------------------------------------------------------------ static far layers
        void BuildFar(FarLayer L, Transform parent, int order)
        {
            var (go, mesh, _) = Gfx.MeshObject("Far" + L.seed, parent, order);
            L.t = go.transform;
            L.kelp = Gfx.MeshObject("FarKelp", L.t, order + 1).mesh;
            Color top = U.WithA(FarCol, L.alpha * 0.55f), mid = U.WithA(FarCol, L.alpha), bot = U.WithA(FarCol, L.alpha * 1.25f);
            var m = new MeshBuilder();
            const float X0 = -5000, X1 = 15000, STEP = 30, BOT = 9500;
            int cols = 0;
            for (float x = X0; x <= X1; x += STEP, cols++)
            {
                float ry = L.Ridge(x);
                Draw.V(m, x, ry, Color.Lerp(top, mid, 0.5f));
                Draw.V(m, x, ry + 500, mid);
                Draw.V(m, x, BOT, bot);
            }
            for (int i = 0; i < cols - 1; i++)
            {
                int a = i * 3, b = a + 3;
                m.Quad(a, b, b + 1, a + 1);
                m.Quad(a + 1, b + 1, b + 2, a + 2);
            }

            if (EnvironmentArt.Geometry("reef-shelf") != null)
            {
                var r = new Mulberry((uint)(L.seed * 1777));
                var keys = new[] { "reef-shelf", "reef-arch", "reef-terrace" };
                for (float x = X0; x < X1;)
                {
                    float size = 520 + L.seed * 80 + r.Next() * 280;
                    bool flip = r.Next() < 0.5f;
                    var reef = EnvironmentArt.Place(keys[(int)(r.Next() * keys.Length)], L.t, order + 1,
                        x, L.Ridge(x) + 20, size, flip, 0, 0);
                    var abyss = EnvironmentArt.Place("reef-abyss", L.t, order + 2, x, L.Ridge(x) + 20, size, flip, 0, 0);
                    L.paintings.Add((reef, abyss));
                    x += size * 2.4f * (0.95f + r.Next() * 0.25f);
                }
                m.Apply(mesh);
                return;
            }

            // rounded boulders and the odd pinnacle on the ridge (only the part above the ridge, so alpha doesn't stack)
            const float B = 420;
            var pts = new List<Vector2>();
            for (int b = Mathf.FloorToInt(X0 / B) - 1; b <= Mathf.FloorToInt(X1 / B) + 1; b++)
            {
                if (U.Hash(b * 3.1f + L.seed * 17) < 0.35f) continue;
                float sx = b * B + U.Hash(b * 7.7f + L.seed) * B;
                bool pinnacle = U.Hash(b * 4.3f + L.seed * 3) < 0.24f;
                float wid = pinnacle ? 120 + U.Hash(b * 2.9f + L.seed) * 120 : 200 + U.Hash(b * 2.9f + L.seed) * 300;
                float hgt = pinnacle ? 350 + U.Hash(b * 1.3f + L.seed * 5) * 500 : 150 + U.Hash(b * 1.3f + L.seed * 5) * 320;
                pts.Clear();
                const int N = 40;
                for (int i = 0; i <= N; i++)
                {
                    float t = i / (float)N * 2 - 1;
                    float prof = Mathf.Pow(Mathf.Max(0, Mathf.Cos(t * Mathf.PI * 0.5f)), 0.8f);
                    // Continuous contour variations avoid the jagged polygon tops of independent per-vertex noise.
                    float bump = 1 + Mathf.Sin(t * 7 + b * 1.7f) * 0.035f + Mathf.Sin(t * 13 + b) * 0.015f;
                    pts.Add(new Vector2(sx + t * wid, L.Ridge(sx + t * wid) + 40 - hgt * prof * bump));
                }
                int prev = -1;
                foreach (var p in pts)
                {
                    float ry = L.Ridge(p.x);
                    float topY = Mathf.Min(p.y, ry);
                    float k = Mathf.Clamp01((ry - topY) / Mathf.Max(1, hgt));
                    int a = Draw.V(m, p.x, topY, Color.Lerp(Color.Lerp(top, mid, 0.5f), top, k)), c = Draw.V(m, p.x, ry, Color.Lerp(top, mid, 0.5f));
                    if (prev >= 0) m.Quad(prev, a, c, prev + 1);
                    prev = a;
                }
            }

            // distant branching coral silhouettes on the nearer layers
            if (L.seed >= 2)
            {
                var R = new Mulberry((uint)(L.seed * 977));
                var segs = new List<Vector4>();
                for (float x = X0; x < X1; x += 260 + R.Next() * 700)
                {
                    if (R.Next() < 0.45f) continue;
                    segs.Clear();
                    Tree(R, x, L.Ridge(x) + 20, -Mathf.PI / 2 + (R.Next() - 0.5f) * 0.3f, 70 + R.Next() * 80, 0, segs);
                    var c = Color.Lerp(top, mid, 0.5f);
                    var line = new List<Vector2>(2);
                    foreach (var s in segs)
                    {
                        line.Clear();
                        line.Add(new Vector2(s.x, s.y)); line.Add(new Vector2(s.z, s.w));
                        float w = Mathf.Clamp(Vector2.Distance(line[0], line[1]) * 0.16f, 5, 18);
                        Draw.Stroke(m, line, w, w * 0.75f, c);
                    }
                }
            }
            m.Apply(mesh);
        }

        static void Tree(Mulberry R, float x, float y, float ang, float len, int lvl, List<Vector4> segs)
        {
            float x2 = x + Mathf.Cos(ang) * len, y2 = y + Mathf.Sin(ang) * len;
            segs.Add(new Vector4(x, y, x2, y2));
            if (lvl >= 3) return;
            int n = 2 + (R.Next() < 0.3f ? 1 : 0);
            for (int i = 0; i < n; i++)
                Tree(R, x2, y2, ang + (i - (n - 1) / 2f) * 0.6f + (R.Next() - 0.5f) * 0.4f, len * (0.65f + R.Next() * 0.2f), lvl + 1, segs);
        }

        static float WaveY(float x, float time) => Mathf.Sin(x * 0.012f + time * 1.6f) * 7 + Mathf.Sin(x * 0.031f - time * 2.2f) * 3;

        // ------------------------------------------------------------------ per frame
        public void Update(float time, Vector2 cam, Vector3 camUnity, float zoom, float refW, float refH, List<Hole> holes)
        {
            float dt = Mathf.Clamp(time - lastTime, 0, 0.1f);
            lastTime = time;
            screen.localScale = new Vector3(1 / zoom, -1 / zoom, 1);
            float hw = refW / 2, hh = refH / 2;
            float yTop = cam.y - hh / zoom, yBot = cam.y + hh / zoom;
            float x0 = cam.x - hw / zoom, x1 = cam.x + hw / zoom;

            // water gradient (screen space, ref px, y-down, centred)
            mb.Clear();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f, y = -hh - 4 + (refH + 8) * t;
                var c = World.WaterAt(Mathf.Max(0, Mathf.Lerp(yTop, yBot, t)));
                mb.Vert(-hw - 4, y, c); mb.Vert(hw + 4, y, c);
                if (i > 0) mb.Quad((i - 1) * 2, (i - 1) * 2 + 1, i * 2 + 1, i * 2);
            }
            mb.Apply(bgMesh);

            // parallax layers
            foreach (var L in far)
            {
                float cx = cam.x * L.p + L.off;
                // Carry the distant reef horizon into the abyss instead of filling it with a flat solid silhouette.
                float deep = Mathf.SmoothStep(0, 1, Mathf.Clamp01((cam.y - 2400) / 1200));
                float cy = Mathf.Lerp(cam.y, 1750, deep);
                L.t.localPosition = new Vector3(camUnity.x - cx * L.s, camUnity.y + cy * L.s, 0);
                L.t.localScale = new Vector3(L.s, L.s, 1);
                float z = zoom * L.s;
                float fx0 = cx - hw / z - 60, fx1 = cx + hw / z + 60;
                if (L.paintings.Count > 0)
                {
                    float fog = 0.87f - L.seed * 0.06f;
                    var water = World.WaterAt(cam.y + 180);
                    foreach (var p in L.paintings)
                    {
                        bool visible = p.reef.x + p.reef.radius > fx0 && p.reef.x - p.reef.radius < fx1;
                        p.reef.renderer.enabled = visible && deep < 0.999f;
                        p.abyss.renderer.enabled = visible && deep > 0.001f;
                        if (p.reef.renderer.enabled) p.reef.Pose(0, new Color(1, 1, 1, (0.23f + L.seed * 0.05f) * (1 - deep)), fog, water);
                        if (p.abyss.renderer.enabled) p.abyss.Pose(0, new Color(1, 1, 1, (0.23f + L.seed * 0.05f) * deep), fog, water);
                    }
                    continue;
                }
                mb.Clear();
                var kc = U.WithA(FarCol, L.alpha * 0.65f);
                const float K = 140;
                for (int b = Mathf.FloorToInt(fx0 / K); b <= Mathf.FloorToInt(fx1 / K) + 1; b++)
                {
                    if (U.Hash(b * 5.3f + L.seed * 9) < 0.82f) continue;
                    float kx = b * K + U.Hash(b + L.seed) * K, gy = L.Ridge(kx) + 30, kh = 240 + U.Hash(b * 9.1f) * 580;
                    float sw = Mathf.Sin(time * 0.6f + b) * 60;
                    var pts = new List<Vector2>(Draw.CubicPts(kx, gy, kx + sw * 0.3f, gy - kh * 0.35f, kx - sw * 0.4f, gy - kh * 0.7f, kx + sw, gy - kh, 12));
                    Draw.Stroke(mb, pts, 12, 6, kc, U.WithA(kc, L.alpha * 0.45f), true);
                    for (int leaf = 2; leaf < pts.Count; leaf += 2)
                    {
                        var a = pts[leaf];
                        float side = leaf % 4 == 0 ? 1 : -1, length = 75 + kh * 0.06f;
                        Draw.Leaf(mb, a, a + new Vector2(side * length * 0.55f, -length * 0.55f),
                            a + new Vector2(side * length, -length * 0.18f), length * 0.48f,
                            U.WithA(kc, kc.a * 0.8f), kc, U.WithA(kc, kc.a * 0.45f), 0.7f, 6);
                    }
                }
                mb.Apply(L.kelp);
            }

            UpdateSchools(time, dt, cam, zoom, hw, hh);

            // god rays from the surface: soft-edged additive wedges
            float top = -cam.y * zoom;
            float len = Mathf.Max(refH * 1.2f, 1900 * zoom);
            mb.Clear();
            if (top + len >= -hh)
            {
                const float spacing = 170;
                float shift = cam.x * zoom * 0.5f;
                int first = Mathf.FloorToInt(shift / spacing) - 4;
                for (int i = first; i < first + refW / spacing + 9; i++)
                {
                    float h = U.Hash(i * 1.37f);
                    if (h < 0.35f) continue;
                    float cx = i * spacing - shift + h * 80 + Mathf.Sin(time * 0.25f + i) * 30 - hw;
                    float w = 50 + h * 120, slant = len * 0.2f, spread = len * (0.05f + h * 0.1f);
                    float a = (0.045f + 0.07f * h) * (0.8f + 0.2f * Mathf.Sin(time * 0.35f + i * 2.1f));
                    var c0 = new Color(0.82f, 0.98f, 1, a); var c1 = new Color(0.7f, 0.94f, 1, a * 0.4f); var z0 = new Color(0.7f, 0.94f, 1, 0);
                    int b0 = mb.v.Count;
                    for (int row = 0; row < 3; row++)
                    {
                        float t = row == 0 ? 0 : row == 1 ? 0.5f : 1;
                        float y = top + len * t, x = cx + slant * t, half = w / 2 + spread * t;
                        mb.Vert(x - half, y, z0); mb.Vert(x, y, row == 0 ? c0 : row == 1 ? c1 : z0); mb.Vert(x + half, y, z0);
                    }
                    for (int row = 0; row < 2; row++)
                    {
                        int r0 = b0 + row * 3, r1 = r0 + 3;
                        mb.Quad(r0, r0 + 1, r1 + 1, r1); mb.Quad(r0 + 1, r0 + 2, r1 + 2, r1 + 1);
                    }
                }
            }
            mb.Apply(raysMesh);
            raysMr.enabled = mb.v.Count > 0;

            // warm bloom under the sun
            float sunX = cam.x + (refW / zoom) * 0.25f;
            float bloomK = Mathf.Clamp01(1 - yTop / 900f);
            sunBloom.enabled = bloomK > 0.01f;
            if (sunBloom.enabled)
            {
                sunBloom.transform.localPosition = U.V3(sunX, 60);
                sunBloom.transform.localScale = new Vector3(2400, 1500, 1);
                sunBloom.color = new Color(0.85f, 1, 0.95f, 0.16f * bloomK);
            }

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

            // dancing caustic net just under the surface
            mb.Clear();
            if (yTop <= 640)
            {
                const float step = 48;
                float sx0 = Mathf.Floor((x0 - 60) / step) * step - step, sx1 = x1 + 60 + step;
                float[] rows = { 0, 50, 170, 380 };
                float[] alphas = { 0.05f, 0.03f, 0.01f, 0 };
                for (int layer = 0; layer < 2; layer++)
                {
                    float sc = layer == 0 ? 1 / 340f : 1 / 230f;
                    float ox = layer == 0 ? time * 0.02f : -time * 0.016f, oy = layer == 0 ? time * 0.012f : time * 0.018f;
                    int b0 = mb.v.Count, cols = 0;
                    for (float x = sx0; x <= sx1; x += step, cols++)
                    {
                        float wy = WaveY(x, time) + 2;
                        for (int k = 0; k < rows.Length; k++)
                        {
                            float y = wy + rows[k];
                            float wob = Mathf.Sin(time * 0.8f + x * 0.005f + y * 0.004f) * 0.025f;
                            mb.Vert(x, -y, new Color(0.85f, 1, 1, alphas[k] * (layer == 0 ? 1 : 0.8f)), x * sc + ox + wob, y * sc * 1.6f + oy - wob);
                        }
                    }
                    int R = rows.Length;
                    for (int i = 0; i < cols - 1; i++)
                        for (int k = 0; k < R - 1; k++)
                        {
                            int a = b0 + i * R + k, b = a + R;
                            mb.Quad(a, b, b + 1, a + 1);
                        }
                }
            }
            mb.Apply(surfCausticMesh);
            surfCausticMr.enabled = mb.v.Count > 0;

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
            var habitat = Habitat.At(cam.y);
            float SW = refW + 40, SH = refH + 40;
            for (int i = 0; i < snow.Length; i++)
            {
                var p = snow[i];
                float fx = Mod(p.x - cam.x * zoom * p.z, SW) - 20;
                float fy = Mod(p.y - cam.y * zoom * p.z + time * Mathf.Lerp(8, 3, habitat.kelp) * p.z, SH) - 20;
                float rad = 0.6f + p.z * 1.6f;
                var drift = Color.Lerp(new Color(0.86f, 0.96f, 1), new Color(0.75f, 0.88f, 0.61f), habitat.kelp);
                drift = Color.Lerp(drift, new Color(0.67f, 0.78f, 1), habitat.abyss);
                drift.a = 0.09f + p.z * Mathf.Lerp(0.24f, 0.35f, habitat.abyss);
                mb.TexQuad(new Vector3(fx + Mathf.Sin(time + p.ph) * (4 + habitat.kelp * 7) - hw, fy - hh, 0), rad, rad, drift);
            }
            mb.Apply(snowMesh);

            // big out-of-focus motes drifting past the lens
            mb.Clear();
            float MW = refW + 200, MH = refH + 200;
            for (int i = 0; i < motes.Length; i++)
            {
                var p = motes[i];
                float fx = Mod(p.x - cam.x * zoom * p.z - time * 6, MW) - 100;
                float fy = Mod(p.y - cam.y * zoom * p.z + time * 10, MH) - 100;
                float rad = 7 + (p.z - 1.3f) * 14 + Mathf.Sin(time * 0.7f + p.ph) * 1.5f;
                mb.TexQuad(new Vector3(fx - hw, fy - hh, 0), rad, rad, new Color(0.85f, 0.97f, 1, 0.07f + 0.04f * Mathf.Sin(time * 0.9f + p.ph)));
            }
            mb.Apply(moteMesh);

            vignette.transform.localScale = new Vector3(refW + 4, refH + 4, 1);
        }

        // ------------------------------------------------------------------ distant schools
        void PlaceSchool(BgSchool s, Vector2 cam, float zoom, float hw, float hh, bool anywhere)
        {
            s.k = U.Rand(0.32f, 0.6f);
            s.size = U.Rand(9, 16);
            s.alpha = Mathf.Lerp(0.1f, 0.2f, (s.k - 0.32f) / 0.28f);
            int dir = U.Chance(0.5f) ? 1 : -1;
            s.vx = dir * U.Rand(30, 60);
            float z = zoom * s.k;
            float spanX = s.size * 7;
            s.x = anywhere ? cam.x + U.Rand(-1, 1) * hw / z : cam.x - dir * (hw / z + spanX * 1.2f);
            s.y = Mathf.Max(200, cam.y + U.Rand(-0.7f, 0.7f) * hh / z);
            int n = U.RandInt(12, 26);
            s.fish = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = U.Rand(0, U.TAU), d = Mathf.Sqrt(U.Rand());
                s.fish[i] = new Vector3(Mathf.Cos(a) * d * spanX, Mathf.Sin(a) * d * spanX * 0.4f, U.Rand(0, U.TAU));
            }
        }

        void UpdateSchools(float time, float dt, Vector2 cam, float zoom, float hw, float hh)
        {
            if (!schoolsPlaced)
            {
                schoolsPlaced = true;
                foreach (var s in schools) PlaceSchool(s, cam, zoom, hw, hh, true);
            }
            mb.Clear();
            foreach (var s in schools)
            {
                float z = zoom * s.k;
                s.x += s.vx * dt;
                s.y += Mathf.Sin(time * 0.3f + s.k * 20) * 6 * dt;
                float sx = (s.x - cam.x) * z, sy = (s.y - cam.y) * z, ext = s.size * 9 * z;
                bool gone = (s.vx > 0 ? sx > hw + ext : sx < -hw - ext) || Mathf.Abs(sx) > hw + ext * 3 || Mathf.Abs(sy) > hh * 2.2f + ext;
                if (gone) { PlaceSchool(s, cam, zoom, hw, hh, false); continue; }
                if (Mathf.Abs(sy) > hh + ext) continue;
                float dir = Mathf.Sign(s.vx);
                var col = new Color(FarCol.r, FarCol.g, FarCol.b, s.alpha);
                foreach (var f in s.fish)
                {
                    float fx = sx + (f.x + Mathf.Sin(time * 0.7f + f.z) * s.size * 0.8f) * z;
                    float fy = sy + (f.y + Mathf.Cos(time * 0.9f + f.z) * s.size * 0.35f) * z;
                    float L = s.size * z * (0.8f + 0.4f * Mathf.Abs(Mathf.Sin(f.z * 3.7f)));
                    BgFish(fx, fy, L, dir, Mathf.Sin(time * 7 + f.z * 5) * L * 0.08f, col);
                }
            }
            mb.Apply(schoolMesh);
        }

        /// Simple fish silhouette in screen space (y-down ref px).
        void BgFish(float x, float y, float L, float dir, float wag, Color c)
        {
            const int N = 10;
            int center = mb.Vert(x, y, c), first = mb.v.Count;
            for (int i = 0; i < N; i++)
            {
                float a = i * U.TAU / N;
                mb.Vert(x + Mathf.Cos(a) * L * 0.5f, y + Mathf.Sin(a) * L * 0.19f, c);
            }
            for (int i = 0; i < N; i++) mb.Tri(center, first + i, first + (i + 1) % N);
            int t0 = mb.Vert(x - dir * L * 0.4f, y, c);
            int t1 = mb.Vert(x - dir * L * 0.78f, y - L * 0.2f + wag, c);
            int t2 = mb.Vert(x - dir * L * 0.7f, y + wag * 0.5f, c);
            int t3 = mb.Vert(x - dir * L * 0.78f, y + L * 0.2f + wag, c);
            mb.Tri(t0, t1, t2); mb.Tri(t0, t2, t3);
        }

        static float Mod(float a, float m) => ((a % m) + m) % m;
    }
}
