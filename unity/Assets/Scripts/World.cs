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
            C(45, 184, 201), C(20, 139, 177), C(18, 91, 116), C(13, 49, 82), C(12, 24, 61), C(6, 14, 37),
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
        public readonly List<Hole> reefLights = new List<Hole>();
        /// Seabed vents that trickle bubble columns (x, floor y).
        public readonly List<Vector2> vents = new List<Vector2>();
        readonly List<Swayer> swayers = new List<Swayer>();
        readonly List<EnvironmentArt.Instance> plants = new List<EnvironmentArt.Instance>();
        readonly List<EnvironmentArt.Instance> landmarks = new List<EnvironmentArt.Instance>();
        bool paintedKelp, paintedGrass, paintedAnemones;

        // kelp palettes, [0] sea green and [1] olive
        static readonly Color KelpRim = new Color(0.03f, 0.13f, 0.12f, 0.92f);
        static readonly Color[] KELP_STEM_A = { U.Hex("#1d6a3e"), U.Hex("#2c6a26") }, KELP_STEM_B = { U.Hex("#52c98a"), U.Hex("#78c94a") };
        static readonly Color[] KELP_TOP_A = { U.Hex("#2fa36a"), U.Hex("#4fa83a") }, KELP_TOP_B = { U.Hex("#7ee3a8"), U.Hex("#a6e46a") };
        static readonly Color[] KELP_UNDER_A = { U.Hex("#155a3c"), U.Hex("#215f22") }, KELP_UNDER_B = { U.Hex("#2f9466"), U.Hex("#4f9a34") };
        static readonly string[] CORAL ={ "#ff6f91", "#ff9f5a", "#c77dff", "#ff4d6d", "#48cae4", "#ffd166", "#f15bb5", "#7bf1a8" };

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
            BuildPlants();
            BuildFloorMesh();
            kelpMesh = Gfx.MeshObject("Kelp", root, Layer.Kelp).mesh;
            frontMesh = Gfx.MeshObject("Front", root, Layer.Front).mesh;
            var caustics = Gfx.MeshObject("Caustics", root, Layer.Caustic, true);
            causticMesh = caustics.mesh;
            caustics.mr.sharedMaterial = new Material(Gfx.Additive) { mainTexture = Gfx.Caustics };
        }

        void GenFloor()
        {
            var n1 = new Noise1D(11); var n2 = new Noise1D(23); var n3 = new Noise1D(37); var n4 = new Noise1D(53); var n5 = new Noise1D(71);
            int N = Mathf.CeilToInt(W / FSTEP) + 2;
            floorS = new float[N];
            for (int i = 0; i < N; i++)
            {
                float x = i * FSTEP;
                // The last two octaves break long slopes into shoulders and lumps; without them a
                // zoomed-out ramp reads as a ruler-straight wedge of sand.
                floorS[i] = Mathf.Clamp(ProfileY(x) + 260 * n1.At(x / 900) + 140 * n2.At(x / 300) + 30 * n3.At(x / 90)
                    + 46 * n4.At(x / 170) + 11 * n5.At(x / 46), 700, 4180);
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

            // rocks: a handful of baked variants, scaled & flipped per placement, sometimes in little clusters
            var rockSprites = new List<Sprite>();
            var rr = new Mulberry(77);
            var paintedRock = PaintedArt.Prop("rock");
            if (paintedRock == null)
                for (int i = 0; i < 10; i++) rockSprites.Add(DecorArt.Rock(rr, i % 2 == 0 ? 0.2f : 0.8f));
            var rocksT = new GameObject("Rocks").transform;
            rocksT.SetParent(root, false);
            int ri = 0;
            void PlaceRock(float x, float s)
            {
                var sr = Gfx.SpriteObject("Rock", rocksT, paintedRock ?? R.Pick(rockSprites), Layer.Rocks + ri++);
                if (paintedRock != null) sr.sharedMaterial = PaintedArt.SceneryMaterial;
                float y = Mathf.Max(FloorY(x), (FloorY(x - s) + FloorY(x + s)) * 0.5f);
                sr.transform.localPosition = U.V3(x, y + (paintedRock == null ? s * 0.3f : 3));
                sr.transform.localScale = new Vector3(R.Next() < 0.5f ? -s : s, s, 1);
                sr.color = Color.Lerp(Color.white, WaterAt(y), Mathf.Clamp01((y - 1200) / 3500) * 0.38f);
                ContactShadow(rocksT, x, y + 2, s * 1.2f, s * 0.17f);
            }
            for (float x = 80; x < W; x += 150 + R.Next() * 460)
            {
                float s = 28 + R.Next() * 72;
                PlaceRock(x, s);
                if (R.Next() < 0.45f) PlaceRock(x + (R.Next() < 0.5f ? -1 : 1) * s * (0.9f + R.Next() * 0.4f), s * (0.35f + R.Next() * 0.25f));
            }
            // Steep faces show bedrock through the sand: low outcrops set into the slope, leaning with
            // it and tinted toward the sediment around them. They have their own random stream so the
            // rest of the decor keeps its layout.
            var outcrops = new Mulberry(313);
            for (float x = 140; x < W - 140; x += 150 + outcrops.Next() * 210)
            {
                float fy = FloorY(x), slope = (FloorY(x + 40) - FloorY(x - 40)) / 80;
                float s = 34 + outcrops.Next() * 58, sink = 0.35f + outcrops.Next() * 0.9f, flip = outcrops.Next(), skip = outcrops.Next();
                if (Mathf.Abs(slope) < 0.42f || fy > 3700 || skip < 0.25f) continue;
                var sprite = paintedRock ?? rockSprites[(int)(flip * rockSprites.Count) % rockSprites.Count];
                var sr = Gfx.SpriteObject("Outcrop", rocksT, sprite, Layer.Rocks + ri++);
                if (paintedRock != null) sr.sharedMaterial = PaintedArt.SceneryMaterial;
                float y = fy + s * sink;
                sr.transform.localPosition = U.V3(x, y);
                sr.transform.localScale = new Vector3(flip < 0.5f ? -s : s, s * 0.72f, 1);
                sr.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan(slope) * Mathf.Rad2Deg * 0.45f);
                var sediment = FloorCol(y) * 0.78f; sediment.a = 1;
                sr.color = Color.Lerp(Color.Lerp(Color.white, sediment, 0.42f), WaterAt(y), Mathf.Clamp01((y - 1200) / 3500) * 0.38f);
            }

            // coral reef on shallow floors
            var coralT = new GameObject("Coral").transform;
            coralT.SetParent(root, false);
            var cr = new Mulberry(99);
            var branch = new List<Sprite>(); var fan = new List<Sprite>(); var brain = new List<Sprite>(); var tube = new List<Sprite>();
            // Bake the procedural variants only when their painted replacement is absent.
            if (PaintedArt.Prop("branch") == null)
                for (int i = 0; i < 8; i++) branch.Add(DecorArt.Branch(cr, U.Hex(cr.Pick(CORAL)), U.Hex(cr.Pick(new[] { "#fff2b3", "#ffffff", "#ffd6e8" }))));
            if (PaintedArt.Prop("fan") == null)
                for (int i = 0; i < 6; i++) fan.Add(DecorArt.Fan(cr, U.Hex(cr.Pick(CORAL))));
            if (PaintedArt.Prop("brain") == null)
                for (int i = 0; i < 6; i++) brain.Add(DecorArt.Brain(cr, U.Hex(cr.Pick(CORAL))));
            if (PaintedArt.Prop("tube") == null)
                for (int i = 0; i < 6; i++) tube.Add(DecorArt.Tube(cr, U.Hex(cr.Pick(CORAL))));
            Sprite Painted(string key, List<Sprite> fallback) => PaintedArt.Prop(key) ?? R.Pick(fallback);
            // the concept-art coral group stands in now and then as a hero piece
            var reef = ConceptArt.Reef();
            float lastReef = -1e4f;
            int ci = 0;
            for (float x = 120; x < W - 120; x += 40 + R.Next() * 120)
            {
                float fy = FloorY(x);
                if (fy > 2950 || R.Next() < 0.22f) continue;
                float k = R.Next(), s = 40 + R.Next() * 95, y = fy + 6;
                float patch = Mathf.PerlinNoise(x / 540f, 2.7f);
                s *= Mathf.Lerp(0.62f, 1.05f, Mathf.SmoothStep(0, 1, patch));
                float rs = 50 + R.Next() * 30;
                if (reef != null && k > 0.9f && x - lastReef > 900 && Mathf.Abs(FloorY(x + rs) - FloorY(x - rs)) < rs * 0.5f)
                {
                    var hero = Gfx.SpriteObject("Reef", coralT, reef, Layer.Coral + ci++);
                    hero.transform.localPosition = U.V3(x, Mathf.Max(fy, Mathf.Max(FloorY(x - rs), FloorY(x + rs))) + 4);
                    hero.transform.localScale = new Vector3(R.Next() < 0.5f ? -rs : rs, rs, 1);
                    ContactShadow(coralT, x, fy + 3, rs * 1.1f, rs * 0.13f);
                    lastReef = x;
                    x += rs;
                    continue;
                }
                Sprite spr; float amp = 0, speed = 0;
                if (k < 0.38f) { spr = Painted("branch", branch); amp = 0.018f; speed = 0.6f; }
                else if (k < 0.58f) { spr = Painted("fan", fan); amp = 0.035f; speed = 0.5f; s *= 0.62f; }
                else if (k < 0.8f) { spr = Painted("brain", brain); y += 4; s *= 0.75f; }
                else spr = Painted("tube", tube);
                var sr = Gfx.SpriteObject("Coral", coralT, spr, Layer.Coral + ci++);
                if (spr.name.Contains("atlas-")) sr.sharedMaterial = PaintedArt.SceneryMaterial;
                sr.transform.localPosition = U.V3(x, y);
                sr.transform.localScale = new Vector3(R.Next() < 0.5f ? -s : s, s, 1);
                sr.color = Color.Lerp(Color.white, WaterAt(fy), Mathf.Clamp01((fy - 1100) / 2500) * 0.28f);
                ContactShadow(coralT, x, fy + 3, s * 0.7f, s * 0.12f);
                if (amp > 0) swayers.Add(new Swayer { t = sr.transform, amp = amp, speed = speed, phase = R.Next() * U.TAU + x * 0.01f });
            }

            // Low cool reef groups give the abyss a habitat while keeping the swimming corridor open.
            var deepReef = PaintedArt.Prop("deep");
            if (deepReef != null)
                for (float x = 280; x < W - 200; x += 360 + R.Next() * 300)
                {
                    float fy = FloorY(x);
                    if (fy < 3050) continue;
                    float s = 75 + R.Next() * 50;
                    var sr = Gfx.SpriteObject("AbyssReef", coralT, deepReef, Layer.Coral + ci++);
                    sr.sharedMaterial = PaintedArt.SceneryMaterial;
                    sr.transform.localPosition = U.V3(x, fy + 4);
                    sr.transform.localScale = new Vector3(R.Next() < 0.5f ? -s : s, s, 1);
                    ContactShadow(coralT, x, fy + 3, s, s * 0.14f);
                    reefLights.Add(new Hole(x, fy - s * 0.45f, s * 1.9f, 0.26f));
                    var glow = Gfx.SpriteObject("ReefBioluminescence", root, Gfx.Glow, Layer.Glow, true);
                    glow.transform.localPosition = U.V3(x, fy - s * 0.5f);
                    glow.transform.localScale = new Vector3(s * 2.6f, s * 1.7f, 1);
                    glow.color = new Color(0.2f, 0.75f, 0.9f, 0.07f);
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

            for (float x = 400; x < W - 400; x += 500 + R.Next() * 900) vents.Add(new Vector2(x, FloorY(x) + 4));
        }

        static void ContactShadow(Transform parent, float x, float y, float rx, float ry)
        {
            var shadow = Gfx.SpriteObject("ContactShadow", parent, Gfx.Glow, Layer.Rocks - 1);
            shadow.transform.localPosition = U.V3(x, y);
            shadow.transform.localScale = new Vector3(rx * 2, ry * 2, 1);
            shadow.color = new Color(0.04f, 0.12f, 0.17f, 0.3f);
        }

        void BuildPlants()
        {
            var vegetation = new GameObject("PaintedVegetation").transform;
            vegetation.SetParent(root, false);
            paintedKelp = EnvironmentArt.Geometry("kelp-teal") != null && EnvironmentArt.Geometry("kelp-olive") != null;
            paintedGrass = EnvironmentArt.Geometry("seagrass") != null;
            paintedAnemones = EnvironmentArt.Geometry("anemone-rose") != null && EnvironmentArt.Geometry("anemone-deep") != null;
            if (paintedKelp)
            {
                float lastX = -1000;
                foreach (var k in kelps)
                {
                    if (k.x - lastX < 92) continue;
                    lastX = k.x;
                    float height = Mathf.Min(680, k.h * (0.64f + U.Hash(k.x * 0.73f) * 0.12f)), rootRadius = height * 0.14f;
                    // Bury the wide painted footing to the lower side of a slope, behind the terrain mesh.
                    float y = Mathf.Max(k.y, Mathf.Max(FloorY(k.x - rootRadius), FloorY(k.x + rootRadius))) + 2;
                    plants.Add(EnvironmentArt.Place(k.tone < 0.5f ? "kelp-teal" : "kelp-olive", vegetation, Layer.Kelp,
                        k.x, y, height, k.phase > Mathf.PI, k.phase, 0.055f));
                }
            }
            if (paintedGrass)
                foreach (var g in grasses)
                    plants.Add(EnvironmentArt.Place("seagrass", vegetation, Layer.Front, g.x, g.y, g.s * 1.15f,
                        g.phase > Mathf.PI, g.phase, 0.045f));
            if (paintedAnemones)
                foreach (var a in anemones)
                    plants.Add(EnvironmentArt.Place(a.deep ? "anemone-deep" : "anemone-rose", vegetation, Layer.Front + 1,
                        a.x, a.y, a.s * 1.05f, a.phase > Mathf.PI, a.phase, 0.04f));
            // A separate seed leaves existing terrain, reef placements, creatures and gameplay unchanged.
            if (EnvironmentArt.Geometry("seaweed-red") != null)
            {
                var r = new Mulberry(307);
                for (float x = 220; x < W - 220; x += 260 + r.Next() * 380)
                {
                    float y = FloorY(x);
                    if (y > 3050 || r.Next() < 0.32f) continue;
                    float height = 70 + r.Next() * 90;
                    plants.Add(EnvironmentArt.Place("seaweed-red", vegetation, Layer.Coral - 1, x, y + 5, height,
                        r.Next() < 0.5f, r.Next() * U.TAU, 0.035f));
                    ContactShadow(vegetation, x, y + 2, height * 0.26f, height * 0.035f);
                }
            }
            Debug.Log($"[DeepFeast] painted vegetation: {plants.Count} rooted instances, shared meshes and atlas materials.");
            void Landmark(string key, float x, float size)
            {
                float baseY = Mathf.Max(FloorY(x), Mathf.Max(FloorY(x - size), FloorY(x + size))) + 6;
                var instance = EnvironmentArt.Place(key, vegetation, Layer.Kelp - 1, x, baseY, size, false, 0, 0);
                if (instance != null) landmarks.Add(instance);
            }
            Landmark("reef-arch", 5480, 290);
            Landmark("reef-terrace", 7270, 250);
            Landmark("reef-abyss", 11120, 300);
        }

        static readonly Color[] FLOOR_C = { new Color32(0xf0, 0xdc, 0xa4, 255), new Color32(0xc9, 0xae, 0x74, 255), new Color32(0x7d, 0x68, 0x44, 255), new Color32(0x3a, 0x31, 0x28, 255) };
        static readonly float[] FLOOR_T = { 0, 0.3f, 0.65f, 1 };
        static Color FloorCol(float y)
        {
            float t = Mathf.Clamp01((y - 1300f) / 3100f);
            int i = 0;
            while (i < 2 && t > FLOOR_T[i + 1]) i++;
            var c = Color.Lerp(FLOOR_C[i], FLOOR_C[i + 1], (t - FLOOR_T[i]) / (FLOOR_T[i + 1] - FLOOR_T[i]));
            // deep sand takes on the colour of the water in front of it
            return Color.Lerp(c, WaterAt(y), t * 0.38f);
        }

        void BuildFloorMesh()
        {
            // sand body: depth-tinted, darker further below the surface edge, textured with tiling grain
            var (_, mesh, mr) = Gfx.MeshObject("Floor", root, Layer.Floor);
            mr.sharedMaterial = new Material(Gfx.Alpha) { mainTexture = Gfx.Sand };
            var m = new MeshBuilder();
            // Rows by depth below the surface: dense near the face, where the sediment bands lie.
            float[] rowDepth = { 0, 12, 28, 42, 54, 68, 92, 122, 152, 180, 210, 260, 340, 460, 640, 900, 1350, 2100, 3300, 5200 };
            int ROWS = rowDepth.Length - 1;
            const float BOTTOM = 5200f, TEX = 1 / 240f;
            int cols = floorS.Length;
            static float Bump(float d, float center, float width) { float u = (d - center) / width; return Mathf.Exp(-u * u); }
            for (int i = 0; i < cols; i++)
            {
                float x = i * FSTEP, fy = floorS[i];
                // Two wet, darker bands follow the face a little below it, wandering in depth.
                float wander = (Mathf.PerlinNoise(x / 260f, 0.5f) - 0.5f) * 36;
                for (int k = 0; k <= ROWS; k++)
                {
                    float y = k == ROWS ? BOTTOM : Mathf.Min(fy + rowDepth[k], BOTTOM), below = y - fy;
                    float shade = Mathf.Lerp(1.05f, 0.68f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(below / 560f)));
                    float band = Bump(below, 50 + wander, 15) * 0.08f + Bump(below, 175 + wander * 1.6f, 28) * 0.065f;
                    // Broad patches of coarser sand keep a large slope from reading as one flat fill.
                    float patch = (Mathf.PerlinNoise(x / 620f + 3.1f, y / 410f + 7.7f) - 0.5f) * 0.18f;
                    var c = FloorCol(y) * (shade * (1 - band) * (1 + patch)); c.a = 1;
                    m.Vert(x, -y, c, x * TEX, -y * TEX);
                }
            }
            for (int i = 0; i < cols - 1; i++)
                for (int k = 0; k < ROWS; k++)
                {
                    int a = i * (ROWS + 1) + k, b = (i + 1) * (ROWS + 1) + k;
                    m.Quad(a, b, b + 1, a + 1);
                }
            m.Apply(mesh);
            mesh.UploadMeshData(false);

            // pebbles, shells and the sunlit lip on top (untextured)
            var (_, detail, _) = Gfx.MeshObject("FloorDetail", root, Layer.Floor + 1);
            var d = new MeshBuilder();
            for (int i = 0; i < cols - 1; i++)
            {
                float x = i * FSTEP, h = U.Hash(x * 0.37f);
                if (h < 0.5f) continue;
                float depth = U.Hash(x); // most pebbles lie near the face, a few deep in a large slope
                float px = x + h * FSTEP, py = FloorY(px) + 8 + depth * depth * 240, pr = 2 + U.Hash(x * 1.7f) * 6;
                var baseC = FloorCol(py);
                var dark = Color.Lerp(baseC, new Color(0.25f, 0.2f, 0.15f), 0.45f); dark.a = 0.85f;
                var lite = Color.Lerp(baseC, Color.white, 0.35f); lite.a = 0.7f;
                Draw.Ellipse(d, px, py, pr, pr * 0.62f, 0, dark, 10);
                Draw.Ellipse(d, px - pr * 0.25f, py - pr * 0.22f, pr * 0.5f, pr * 0.24f, 0, lite, 8);
            }
            // Broken crescent ripples and occasional ribbed shells give the beach a quieter material scale.
            var sandR = new Mulberry(981);
            for (float x = 70; x < W - 70; x += 110 + sandR.Next() * 150)
            {
                float width = 35 + sandR.Next() * 70;
                for (int row = 0; row < 3; row++)
                {
                    if (sandR.Next() < 0.32f) continue;
                    float depth = 18 + row * 42 + sandR.Next() * 12;
                    var ripple = new List<Vector2>(13);
                    for (int i = 0; i <= 12; i++)
                    {
                        float t = i / 12f, px = x + (t - 0.5f) * width;
                        ripple.Add(new Vector2(px, FloorY(px) + depth + Mathf.Sin(t * Mathf.PI) * 5));
                    }
                    var shade = FloorCol(FloorY(x));
                    Draw.Stroke(d, ripple, 1.8f, 0.6f, U.WithA(shade * 0.64f, 0.19f), U.WithA(shade * 0.64f, 0.03f));
                    for (int i = 0; i < ripple.Count; i++) ripple[i] += new Vector2(0, 2.2f);
                    Draw.Stroke(d, ripple, 2, 0.4f, new Color(1, 0.96f, 0.83f, 0.15f), new Color(1, 0.96f, 0.83f, 0.02f));
                }
                if (sandR.Next() > 0.38f) continue;
                float sx = x + width * 0.25f, sy = FloorY(sx) + 10 + sandR.Next() * 32, sr = 4 + sandR.Next() * 4;
                var shell = Color.Lerp(FloorCol(sy), new Color(0.96f, 0.88f, 0.73f), 0.4f);
                Draw.Ellipse(d, sx, sy + sr * 0.18f, sr * 1.2f, sr * 0.58f, 0, U.WithA(shell * 0.55f, 0.38f), 12);
                Draw.Ellipse(d, sx, sy, sr, sr * 0.65f, -0.15f, shell, 14);
                for (int rib = 0; rib < 5; rib++)
                {
                    float angle = -Mathf.PI + 0.35f + rib * 0.6f;
                    var line = Draw.QuadPts(sx, sy + sr * 0.4f, sx + Mathf.Cos(angle) * sr * 0.35f, sy - sr * 0.1f,
                        sx + Mathf.Cos(angle) * sr * 0.85f, sy + Mathf.Sin(angle) * sr * 0.48f, 4);
                    Draw.Stroke(d, line, 0.65f, 0.4f, U.WithA(shell * 0.7f, 0.45f));
                }
            }
            var lip = new List<Vector2>(cols);
            for (int i = 0; i < cols; i++) lip.Add(new Vector2(i * FSTEP, floorS[i] + 2f));
            Draw.Stroke(d, lip, 3.5f, 3.5f, new Color(1f, 245 / 255f, 210 / 255f, 0.3f));
            var edge = new List<Vector2>(cols);
            for (int i = 0; i < cols; i++) edge.Add(new Vector2(i * FSTEP, floorS[i] - 0.5f));
            Draw.Stroke(d, edge, 2.5f, 2.5f, new Color(0.25f, 0.2f, 0.12f, 0.25f));
            d.Apply(detail);
            detail.UploadMeshData(false);
        }

        // ------------------------------------------------------------------ per frame
        public void UpdateView(float time, float x0, float x1, float yBot)
        {
            foreach (var s in swayers)
                s.t.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(time * s.speed + s.phase) * s.amp * Mathf.Rad2Deg);
            foreach (var p in plants)
            {
                bool visible = p.x + p.radius > x0 - 60 && p.x - p.radius < x1 + 60;
                p.renderer.enabled = visible;
                if (!visible) continue;
                float y = -p.transform.localPosition.y;
                float height = p.transform.localScale.y;
                float fog = height > 200 ? 0.2f + Mathf.Clamp01((y - 850) / 3400) * 0.38f : Mathf.Clamp01((y - 850) / 3400) * 0.22f;
                p.Pose(time, Color.white, fog, WaterAt(y - height * 0.4f));
            }
            foreach (var p in landmarks)
            {
                p.renderer.enabled = p.x + p.radius > x0 - 60 && p.x - p.radius < x1 + 60;
                if (p.renderer.enabled) p.Pose(0, Color.white, 0.4f, WaterAt(-p.transform.localPosition.y - 150));
            }

            // kelp
            mb.Clear();
            var pts = new List<Vector2>(17);
            foreach (var d in kelps)
            {
                if (paintedKelp) break;
                if (d.x < x0 - 120 || d.x > x1 + 120) continue;
                const int segs = 12;
                float sl = d.h / segs, x = d.x, y = d.y;
                pts.Clear();
                pts.Add(new Vector2(x, y));
                for (int i = 1; i <= segs; i++)
                {
                    float ang = -Mathf.PI / 2 + Mathf.Sin(time * 0.8f + d.phase + i * 0.32f) * 0.16f * (0.3f + i / (float)segs);
                    x += Mathf.Cos(ang) * sl; y += Mathf.Sin(ang) * sl;
                    pts.Add(new Vector2(x, y));
                }
                // kelp sits behind the play area, so it fades into the water, more so the deeper it grows
                int tone = d.tone >= 0.5f ? 1 : 0;
                var water = WaterAt(d.y - d.h * 0.5f);
                float fog = 0.2f + 0.45f * Mathf.Clamp01((d.y - 1300) / 2400);
                Color Fog(Color c) { var f = Color.Lerp(c, water, fog); f.a = c.a; return f; }
                Color rim = Fog(KelpRim), stemA = Fog(KELP_STEM_A[tone]), stemB = Fog(KELP_STEM_B[tone]);
                Color topA = Fog(KELP_TOP_A[tone]), topB = Fog(KELP_TOP_B[tone]), underA = Fog(KELP_UNDER_A[tone]), underB = Fog(KELP_UNDER_B[tone]);
                float rimW = Mathf.Max(1.4f, d.w * 0.12f);
                Draw.Stroke(mb, pts, d.w + rimW * 2, d.w * 0.25f + rimW * 2, rim, rim, true);
                Draw.Stroke(mb, pts, d.w, d.w * 0.25f, stemA, stemB, true);
                // broad leaves arch up off the stem and droop at the tip, darker low down
                int last = pts.Count - 1;
                for (int i = 1; i <= last; i++)
                {
                    float s = i % 2 == 0 ? 1 : -1, t = i / (float)last;
                    if (i == last) s = d.phase > Mathf.PI ? 1 : -1;
                    float L = (d.w * 3f + sl * 1.05f) * (1.1f - t * 0.35f), lw = L * 0.5f;
                    float rot = s * Mathf.Sin(time * 1.2f + d.phase + i) * 0.2f, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                    // the crown leaf curls up off the top of the stem; the rest reach out
                    float cx = i == last ? 0.15f : 0.4f, cy = i == last ? -0.75f : -0.6f;
                    float bx = i == last ? 0.6f : 1f, by = i == last ? -0.7f : -0.12f;
                    var p = pts[i];
                    var c = p + new Vector2(s * cx * cs - cy * sn, s * cx * sn + cy * cs) * L;
                    var b = p + new Vector2(s * bx * cs - by * sn, s * bx * sn + by * cs) * L;
                    Draw.Leaf(mb, p, c, b, lw, Color.Lerp(topA, topB, t), Color.Lerp(underA, underB, t), rim, rimW, 7);
                }
            }
            mb.Apply(kelpMesh);

            // grass + anemones
            mb.Clear();
            Color grassA = U.Hex("#2c6e3e"), grassB = U.Hex("#6cc070");
            foreach (var d in grasses)
            {
                if (paintedGrass) break;
                if (d.x < x0 - 80 || d.x > x1 + 80) continue;
                for (int i = 0; i < d.n; i++)
                {
                    float bx = d.x + (i - d.n / 2f) * d.s * 0.12f;
                    float sw = Mathf.Sin(time * 1.3f + d.phase + i) * d.s * 0.25f;
                    var q = Draw.QuadPts(bx, d.y, bx + sw * 0.3f, d.y - d.s * 0.6f, bx + sw, d.y - d.s * (0.8f + (i % 3) * 0.15f), 7);
                    Draw.Stroke(mb, q, d.s * 0.09f, d.s * 0.04f, grassA, grassB, true);
                }
            }
            foreach (var d in anemones)
            {
                if (paintedAnemones) break;
                if (d.x < x0 - 100 || d.x > x1 + 100) continue;
                var col = U.Hsl(d.hue, d.deep ? 0.9f : 0.75f, d.deep ? 0.68f : 0.66f);
                var baseC = U.Hsl(d.hue, 0.6f, 0.4f);
                Draw.Ellipse(mb, d.x, d.y - d.s * 0.12f, d.s * 0.32f, d.s * 0.2f, 0, U.Hsl(d.hue, 0.45f, 0.35f), 12);
                var tipC = U.Hsl(d.hue, 1f, 0.85f);
                for (int i = 0; i < d.n; i++)
                {
                    float a = -Mathf.PI + 0.35f + (i / (float)(d.n - 1)) * (Mathf.PI - 0.7f);
                    float sw = Mathf.Sin(time * 1.4f + d.phase + i * 0.6f) * 0.3f;
                    float ex = d.x + Mathf.Cos(a + sw * 0.4f) * d.s * 0.75f, ey = d.y - d.s * 0.2f + Mathf.Sin(a) * d.s * 0.75f;
                    var q = Draw.QuadPts(d.x + Mathf.Cos(a) * d.s * 0.2f, d.y - d.s * 0.2f,
                        d.x + Mathf.Cos(a) * d.s * 0.5f + sw * d.s * 0.2f, d.y - d.s * 0.2f + Mathf.Sin(a) * d.s * 0.4f, ex, ey, 7);
                    Draw.Stroke(mb, q, d.s * 0.09f, d.s * 0.06f, baseC, col, false);
                    Draw.Circle(mb, ex, ey, d.s * 0.05f, tipC, 8);
                }
            }
            mb.Apply(frontMesh);

            // One low-contrast, broken caustic layer; the sand remains a material rather than a bright wire net.
            mb.Clear();
            const float CS = 40;
            float s0 = Mathf.Floor(x0 / CS) * CS - CS;
            for (int layer = 0; layer < 1; layer++)
            {
                float sc = 1 / 520f;
                float ox = time * 0.012f, oy = time * 0.008f;
                int prev = -1;
                for (float x = s0; x <= x1 + CS; x += CS)
                {
                    float y = FloorY(x);
                    float k = Mathf.Clamp01(1 - (y - 900) / 2300) * 0.13f;
                    float wob = Mathf.Sin(time * 0.9f + x * 0.004f) * 0.02f;
                    int a = mb.Vert(x, -(y - 1), new Color(0.9f, 1, 0.95f, k), x * sc + ox + wob, y * sc * 2.4f + oy);
                    mb.Vert(x, -(y + 170), new Color(0.9f, 1, 0.95f, 0), x * sc + ox - wob, (y + 170) * sc * 2.4f + oy);
                    if (prev >= 0) mb.Quad(prev, a, a + 1, prev + 1);
                    prev = a;
                }
            }
            mb.Apply(causticMesh);
        }
    }

    /// <summary>Baked decor sprites (rocks, coral). Shape space: s = 1, pivot at the base.</summary>
    public static class DecorArt
    {
        const float PPU = 150f;

        static readonly Vector3 Sun = new Vector3(-0.45f, 0.8f, 0.5f);

        /// Coral contour width in pixels, in the same navy as the fish outlines.
        const int RIM = 4;

        public static Sprite Rock(Mulberry R, float tone)
        {
            int n = 11;
            var ang = new float[n]; var mul = new float[n];
            for (int i = 0; i < n; i++) { ang[i] = i / (float)n * U.TAU; mul[i] = 1 + (R.Next() * 2 - 1) * 0.2f; }
            Vector2 P(int i)
            {
                i = (i % n + n) % n;
                float x = Mathf.Cos(ang[i]) * mul[i] * 1.25f, y = Mathf.Sin(ang[i]) * mul[i] * 0.8f;
                if (y > 0.4f) y = 0.4f + (y - 0.4f) * 0.35f; // flat underside, buried in the sand
                return new Vector2(x, y);
            }
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
            Color lite, mid, dark;
            if (tone < 0.5f) { lite = U.Hex("#b4bcc6"); mid = U.Hex("#6b7480"); dark = U.Hex("#252a33"); }
            else { lite = U.Hex("#c4b096"); mid = U.Hex("#7a6854"); dark = U.Hex("#2e2620"); }
            var lit = r.Light(m, Mathf.RoundToInt(PPU * 0.2f), Sun, 2.4f);
            int seed = (int)(R.Next() * 1000);
            float sx = R.Next() * 50;
            r.Paint(m, (i, x, y) =>
            {
                float mottle = Noise2.Fbm(x * 2.6f + sx, y * 2.6f, 4, seed);
                float grain = Noise2.At(x * 14 + sx, y * 14, seed + 7);
                float v = 0.52f + lit[i] * 1.5f + (mottle - 0.5f) * 0.45f + (grain - 0.5f) * 0.12f - Mathf.Max(0, y + 0.1f) * 0.45f;
                v = Mathf.Clamp01(v);
                return v < 0.5f ? Color.Lerp(dark, mid, v * 2) : Color.Lerp(mid, lite, v * 2 - 1);
            });
            // strata cracks: dark groove with a lit lip just above
            var cracks = r.Mask(); var lips = r.Mask();
            int nc = 2 + (int)(R.Next() * 2);
            for (int k = 0; k < nc; k++)
            {
                var pts = new List<Vector2>();
                float cx = -1.1f + R.Next() * 0.6f, cy = -0.45f + R.Next() * 0.6f, dir = (R.Next() - 0.5f) * 0.5f;
                for (int j = 0; j < 7; j++) { pts.Add(new Vector2(cx, cy)); cx += 0.18f + R.Next() * 0.12f; cy += dir * 0.18f + (R.Next() - 0.5f) * 0.08f; }
                r.Stroke(pts, 0.035f, cracks);
                for (int j = 0; j < pts.Count; j++) pts[j] += new Vector2(0, -0.03f);
                r.Stroke(pts, 0.022f, lips);
            }
            Raster.Sub(lips, cracks);
            Raster.Mul(cracks, m); Raster.Mul(lips, m);
            r.Paint(lips, new Color(1, 1, 1, 0.16f));
            r.Paint(cracks, new Color(0.05f, 0.05f, 0.08f, 0.4f));
            // algae cap and barnacles on the sunlit top
            var algae = tone < 0.5f ? new Color(0.36f, 0.66f, 0.38f) : new Color(0.58f, 0.66f, 0.3f);
            r.Paint(m, (i, x, y) =>
            {
                float nz = Noise2.Fbm(x * 4 + sx, y * 4, 3, seed + 3);
                float a = Mathf.Clamp01((-y - 0.3f) * 3.5f) * Mathf.Clamp01((nz - 0.42f) * 6) * Mathf.Clamp01(lit[i] * 5 + 0.7f);
                var c = Color.Lerp(algae, Color.white, Mathf.Max(0, lit[i]) * 0.6f);
                c.a = a * 0.75f;
                return c;
            });
            var barn = r.Mask();
            for (int k = 0; k < 9; k++) r.Circle(-1 + R.Next() * 2, -0.55f + R.Next() * 0.8f, 0.018f + R.Next() * 0.025f, barn);
            Raster.Mul(barn, m);
            r.Paint(barn, new Color(0.95f, 0.93f, 0.85f, 0.55f));
            r.Paint(r.InnerRing(m, 3), new Color(8 / 255f, 12 / 255f, 22 / 255f, 0.45f));
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
                float w = 0.15f * Mathf.Pow(0.72f, l);
                foreach (var s in levels[l]) r.Stroke(new[] { new Vector2(s.x, s.y), new Vector2(s.z, s.w) }, w, m);
            }
            var tips = r.Mask();
            foreach (var s in levels[levels.Count - 1]) r.Circle(s.z, s.w, 0.05f, tips);
            var outline = (float[])m.Clone();
            for (int i = 0; i < outline.Length; i++) outline[i] = Mathf.Max(outline[i], tips[i]);
            r.Paint(r.Dilate(outline, RIM), FishArt.Navy);
            var lit = r.Light(m, Mathf.RoundToInt(PPU * 0.03f), Sun, 2.5f);
            r.Paint(m, (i, x, y) =>
            {
                var c = Color.Lerp(col * 0.62f, Color.Lerp(col, Color.white, 0.12f), Mathf.Clamp01(-y * 1.4f));
                c = Color.Lerp(c, lit[i] > 0 ? Color.white : new Color(0.15f, 0.02f, 0.12f), Mathf.Abs(lit[i]) * 1.3f);
                c.a = 1;
                return c;
            });
            // polyps dotted along the branches
            var dots = r.Mask();
            foreach (var l in levels)
                foreach (var s in l)
                    for (float t = 0.25f; t < 1; t += 0.3f)
                        r.Circle(Mathf.Lerp(s.x, s.z, t) + (R.Next() - 0.5f) * 0.03f, Mathf.Lerp(s.y, s.w, t), 0.012f, dots);
            Raster.Mul(dots, m);
            r.Paint(dots, U.WithA(tip, 0.6f));
            // pearly polyp heads: a shaded ball, a lit cap and a catch-light
            r.Paint(tips, Color.Lerp(tip, col, 0.35f));
            var cap = r.Mask();
            foreach (var s in levels[levels.Count - 1]) r.Circle(s.z - 0.01f, s.w - 0.012f, 0.035f, cap);
            Raster.Mul(cap, tips);
            r.Paint(cap, tip);
            var shine = r.Mask();
            foreach (var s in levels[levels.Count - 1]) r.Circle(s.z - 0.019f, s.w - 0.022f, 0.012f, shine);
            r.Paint(shine, new Color(1, 1, 1, 0.9f));
            return r.ToSprite(Vector2.zero, PPU);
        }

        public static Sprite Fan(Mulberry R, Color col)
        {
            const float S = 1.1f;
            int n = 13 + (int)(R.Next() * 5);
            var ribs = new List<Vector2>();
            for (int i = 0; i < n; i++) ribs.Add(new Vector2(-Mathf.PI + 0.3f + (i / (float)(n - 1)) * (Mathf.PI - 0.6f), 0.78f + R.Next() * 0.22f));
            var r = new Raster(-S * 1.05f, -S * 1.05f, S * 1.05f, 0.08f, PPU);
            var poly = new Path().Move(0, 0);
            foreach (var rb in ribs) poly.Line(Mathf.Cos(rb.x) * S * rb.y, Mathf.Sin(rb.x) * S * rb.y);
            var membrane = r.Fill(poly);
            Color deep = Color.Lerp(col, Color.black, 0.35f), edge = Color.Lerp(col, Color.white, 0.25f);
            r.Paint(membrane, (x, y) => { float d = Mathf.Sqrt(x * x + y * y) / S; var c = Color.Lerp(deep, col, d); c.a = 0.18f + d * 0.3f; return c; });
            var m = r.Mask();
            foreach (var rb in ribs)
            {
                float a = rb.x, k = rb.y, bend = (R.Next() - 0.5f) * 0.2f;
                r.Stroke(Raster.QuadPts(Vector2.zero, new Vector2(Mathf.Cos(a + bend) * S * k * 0.5f, Mathf.Sin(a + bend) * S * k * 0.5f), new Vector2(Mathf.Cos(a) * S * k, Mathf.Sin(a) * S * k), 12), S * 0.024f, m);
            }
            foreach (var k in new[] { 0.28f, 0.44f, 0.58f, 0.71f, 0.83f })
            {
                var arc = new List<Vector2>();
                for (int i = 0; i <= 32; i++)
                {
                    float a = -Mathf.PI + 0.35f + (Mathf.PI - 0.7f) * i / 32f, wob = 1 + Mathf.Sin(i * 1.7f + k * 20) * 0.03f;
                    arc.Add(new Vector2(Mathf.Cos(a) * S * k * wob, Mathf.Sin(a) * S * k * wob));
                }
                r.Stroke(arc, S * 0.012f, m);
            }
            Raster.Mul(m, r.Dilate(membrane, 2));
            r.Paint(m, (x, y) => Color.Lerp(deep, edge, Mathf.Sqrt(x * x + y * y) / S));
            var trunk = r.Stroke(new[] { new Vector2(0, 0.06f), new Vector2(0, -0.14f) }, S * 0.07f);
            r.Paint(trunk, deep);
            return r.ToSprite(Vector2.zero, PPU);
        }

        public static Sprite Brain(Mulberry R, Color col)
        {
            var r = new Raster(-0.75f, -0.53f, 0.75f, 0.04f, PPU);
            var path = new Path().Move(-0.7f, 0);
            for (int i = 1; i <= 32; i++) { float a = Mathf.PI + Mathf.PI * i / 32f; path.Line(Mathf.Cos(a) * 0.7f, Mathf.Sin(a) * 0.48f); }
            var m = r.Fill(path);
            r.Paint(r.Dilate(m, RIM), FishArt.Navy);
            var dark = Color.Lerp(col, new Color(0.16f, 0.06f, 0.14f), 0.6f);
            float ox = R.Next() * 64, oy = R.Next() * 64, rot = R.Next() * U.TAU, cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            r.Paint(m, (x, y) =>
            {
                // seen side-on the dome is a hemisphere, so its normal falls straight out of the position
                float px = x / 0.7f, py = y / 0.48f, rr = px * px + py * py;
                float nz = Mathf.Sqrt(Mathf.Max(0, 1 - rr));
                float dome = -0.45f * px - 0.8f * py + 0.5f * nz - 0.5f;
                float foot = 1 - U.Smooth(Mathf.Clamp01(-py / 0.35f));
                // labyrinth folds, packed tighter toward the rim as if wrapped round the dome
                float k = 58 * (1 + 0.5f * rr);
                float h = Noise2.Maze(ox + (x * cs - y * sn) * k, oy + (x * sn + y * cs) * k, out float dx, out float dy);
                float gx = dx * cs + dy * sn, gy = dy * cs - dx * sn;
                // broad rounded ridges with a bright crest, split by narrow dark grooves
                float ridge = U.Smooth(Mathf.Clamp01((h - 0.05f) / 0.32f));
                float crest = U.Smooth(Mathf.Clamp01((h - 0.7f) / 0.3f));
                float v = 0.3f + ridge * 0.42f + crest * 0.12f + dome * 0.45f - foot * 0.22f + (0.45f * gx + 0.8f * gy) * 0.9f;
                var c = v < 0.6f ? Color.Lerp(dark, col, v / 0.6f) : Color.Lerp(col, Color.white, (v - 0.6f) * 0.9f);
                c.a = 1;
                return c;
            });
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
            var r = new Raster(-span, -maxH - 0.08f, span, 0.05f, PPU);
            Color shade = Color.Lerp(col, new Color(0.15f, 0.03f, 0.1f), 0.55f), lite = Color.Lerp(col, Color.white, 0.4f);
            foreach (var t in tubes)
            {
                float x = t.x, h = t.y, w = t.z;
                var m = r.Fill(RoundRect(x - w, -h, w * 2, h + 0.02f, w * 0.6f));
                var rim = r.Ellipse(x, -h + w * 0.25f, w * 1.08f, w * 0.38f, 0);
                var outline = (float[])m.Clone();
                for (int i = 0; i < outline.Length; i++) outline[i] = Mathf.Max(outline[i], rim[i]);
                r.Paint(r.Dilate(outline, RIM), FishArt.Navy);
                r.Paint(m, (px, py) =>
                {
                    // cylinder: lit on the left, shaded on the right, darker toward the base
                    float u = (px - x) / w;
                    float l = Mathf.Clamp01(1 - Mathf.Abs(u + 0.35f) * 1.3f);
                    var c = u > 0.2f ? Color.Lerp(col, shade, (u - 0.2f) * 1.1f) : Color.Lerp(col, lite, l * 0.6f);
                    c = Color.Lerp(c, shade, Mathf.Clamp01((py + h * 0.3f) / (h * 0.9f)) * 0.5f);
                    c.a = 1;
                    return c;
                });
                // pitted sponge: dark pores, each with a lit lower lip
                var lips = r.Mask(); var pits = r.Mask();
                int pores = Mathf.RoundToInt(h * 26);
                for (int k = 0; k < pores; k++)
                {
                    float px = x + (R.Next() * 2 - 1) * w * 0.72f, py = -h * (0.12f + R.Next() * 0.8f), pr = w * (0.07f + R.Next() * 0.06f);
                    r.Circle(px, py + pr * 0.4f, pr, lips);
                    r.Circle(px, py, pr, pits);
                }
                Raster.Mul(lips, m); Raster.Mul(pits, m);
                r.Paint(lips, U.WithA(lite, 0.35f));
                r.Paint(pits, U.WithA(shade, 0.7f));
                // flared rim and dark mouth
                r.Paint(rim, lite);
                r.Paint(r.Ellipse(x, -h + w * 0.25f, w * 0.78f, w * 0.26f, 0), new Color(30 / 255f, 10 / 255f, 20 / 255f, 0.85f));
                // tiny polyps peeking out
                for (int k = 0; k < 3; k++)
                    r.Paint(r.Circle(x + (k - 1) * w * 0.4f, -h + w * 0.05f - (k == 1 ? w * 0.18f : 0), w * 0.12f), Color.Lerp(lite, Color.white, 0.5f));
            }
            return r.ToSprite(Vector2.zero, PPU);
        }
    }
}
