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
        // Identity stays in the catalog and review tools rather than floating labels in normal play.
        public string displayName, scientificName = "", referenceUrl = "";
        public Color c0, c1, c2, fin, tailCol, spot;
        public float min, max, eye;
        public bool canSchool = true, aggressive;
        public float chaseSpeedMultiplier = 1;
        // Relative abundance on the reef, in the kelp forest and in the abyss; spawns weigh them by depth.
        public float reef = 1, kelp = 1, abyss = 1;
        // Small schooling prey: it fills the youngest schools and swims alone only while tiny.
        public bool forage;
        // Photophores light the skin and open a hole in the deep's darkness.
        public float glow;
        public Color glowColor;
        // Where the light sits in model units (x toward the snout, y up); the anglerfish's is its lure.
        public Vector2 glowOffset;
        public bool IsShark => shape == "shark";
        public float ZoneWeight(Habitat.Look zone) => reef * Mathf.Max(0, 1 - zone.kelp - zone.abyss) + kelp * zone.kelp + abyss * zone.abyss;
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
            => new Species { key = key, displayName = key, shape = shape, c0 = U.Hex(a), c1 = U.Hex(b), c2 = U.Hex(c), fin = U.Hex(fin, finA), tailCol = U.Hex(fin, finA), pat = pat, min = min, max = max };

        public static Species Shark, TigerShark, MakoShark, Hammerhead, WhaleShark, Player;
        public static readonly Dictionary<string, Species> SpeciesMap = Build();
        public static readonly List<string> SpeciesKeys = new List<string>(SpeciesMap.Keys);
        public static readonly Species[] SharkVariants = { Shark, TigerShark, MakoShark, Hammerhead };
        public static readonly IReadOnlyList<Species> AllSpecies = CollectAll().AsReadOnly();

        static List<Species> CollectAll()
        {
            var species = new List<Species>(SpeciesMap.Values);
            species.AddRange(SharkVariants);
            species.Add(WhaleShark);
            species.Add(Player);
            return species;
        }

        static Species Identify(Species species, string name, string scientificName, string referenceUrl)
        {
            species.displayName = name;
            species.scientificName = scientificName;
            species.referenceUrl = referenceUrl;
            return species;
        }

        // Successive encounters rotate identities while retaining the same size/timer escalation.
        public static Species SharkForEncounter(int encounter)
        {
            int index = encounter % SharkVariants.Length;
            return SharkVariants[index < 0 ? index + SharkVariants.Length : index];
        }

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
            foreach (var legacy in d.Values)
                legacy.displayName = legacy.key == "clown" ? "Clownfish" : legacy.key == "angel" ? "Angelfish" :
                    legacy.key == "parrot" ? "Parrotfish" : char.ToUpperInvariant(legacy.key[0]) + legacy.key.Substring(1);

            const string noaa = "https://www.fisheries.noaa.gov/species/";
            void AddKnown(Species species, string name, string scientificName, string page)
                => Add(Identify(species, name, scientificName, noaa + page));
            // Radius ranges are game progression units, not biological adult lengths.
            AddKnown(S("almaco_jack", "oval", "#c7b46e", "#38524d", "#efe9ef", "#7d8c5e", 1, "amberjack", 30, 190),
                "Almaco jack", "Seriola rivoliana", "almaco-jack");
            var goliath = S("goliath_grouper", "fat", "#c6a663", "#5a4630", "#efdfb0", "#94794a", 1, "mottled", 70, 600);
            goliath.spot = U.Hex("#38382b"); goliath.canSchool = false;
            AddKnown(goliath, "Atlantic goliath grouper", "Epinephelus itajara", "atlantic-goliath-grouper");
            var halibut = S("atlantic_halibut", "flat", "#a8916a", "#4e4434", "#f5f2e6", "#9a8463", 1, "halibut", 40, 400);
            halibut.spot = U.Hex("#443f32"); halibut.canSchool = false;
            AddKnown(halibut, "Atlantic halibut", "Hippoglossus hippoglossus", "atlantic-halibut");
            AddKnown(S("atlantic_mackerel", "slim", "#7fd6cb", "#155f72", "#f4fbf7", "#5d93a0", 1, "mackerel", 9, 44),
                "Atlantic mackerel", "Scomber scombrus", "atlantic-mackerel");
            var mahi = S("mahi_mahi", "torpedo", "#d3e642", "#0b9d91", "#ffed76", "#1a8b9c", 1, "mahi", 35, 250);
            mahi.tailCol = U.Hex("#e8cf37"); mahi.spot = U.Hex("#165ea4");
            AddKnown(mahi, "Atlantic mahi-mahi", "Coryphaena hippurus", "atlantic-mahi-mahi");
            AddKnown(S("skipjack_tuna", "torpedo", "#a6c4dc", "#2b3a78", "#f4f7f9", "#5a76a6", 1, "skipjack", 40, 260),
                "Atlantic skipjack tuna", "Katsuwonus pelamis", "atlantic-skipjack-tuna");
            AddKnown(S("striped_bass", "oval", "#c4d4c2", "#3f5b52", "#f8f7e9", "#8fa898", 1, "bass", 22, 150),
                "Atlantic striped bass", "Morone saxatilis", "atlantic-striped-bass");
            AddKnown(S("yellowfin_tuna", "torpedo", "#8fc7c0", "#163e74", "#f2f6e4", "#f2cf2e", 1, "yellowfin", 55, 600),
                "Atlantic yellowfin tuna", "Thunnus albacares", "atlantic-yellowfin-tuna");
            AddKnown(S("bluefish", "torpedo", "#8cc4cf", "#246d7e", "#f1f8f7", "#5f96a2", 1, "line", 18, 120),
                "Bluefish", "Pomatomus saltatrix", "bluefish");
            var redDrum = S("red_drum", "oval", "#e2995f", "#8c5130", "#f7e3c6", "#c98a4f", 1, "red_drum", 22, 170);
            redDrum.spot = U.Hex("#171f22");
            Add(Identify(redDrum, "Red drum", "Sciaenops ocellatus", "https://myfwc.com/wildlifehabitats/profiles/saltwater/drums/red-drum/"));

            // Kelp forest and deep-sea residents give each habitat its own cast.
            var sardine = S("pacific_sardine", "slim", "#a9c6cc", "#1d5577", "#f2f7f6", "#8fb0b8", 1, "sardine", 3, 16);
            sardine.spot = U.Hex("#152a3a"); sardine.forage = true;
            AddKnown(sardine, "Pacific sardine", "Sardinops sagax", "pacific-sardine");
            var garibaldi = S("garibaldi", "oval", "#ff7a1c", "#e0520a", "#ffb062", "#ff6a10", 1, "garibaldi", 8, 30);
            garibaldi.canSchool = false;
            Add(Identify(garibaldi, "Garibaldi", "Hypsypops rubicundus", "https://en.wikipedia.org/wiki/Garibaldi_(fish)"));
            var sheephead = S("california_sheephead", "oval", "#d4524c", "#1e1b20", "#f1ebe2", "#8e3a3a", 1, "sheephead", 22, 100);
            sheephead.canSchool = false;
            Add(Identify(sheephead, "California sheephead", "Semicossyphus pulcher",
                "https://www.aquariumofpacific.org/onlinelearningcenter/species/california_sheephead"));
            var lingcod = S("lingcod", "long", "#8f8b5f", "#3f4a37", "#dcd5b2", "#6f7652", 1, "lingcod", 30, 160);
            lingcod.spot = U.Hex("#33361f"); lingcod.canSchool = false;
            AddKnown(lingcod, "Lingcod", "Ophiodon elongatus", "lingcod");
            var lantern = S("lanternfish", "slim", "#6f879c", "#16233a", "#c3d0dc", "#4d6178", 1, "lantern", 3, 14);
            lantern.forage = true; lantern.glow = 1; lantern.glowColor = U.Hex("#8ff4ff");
            Add(Identify(lantern, "Glacier lanternfish", "Benthosema glaciale", "https://en.wikipedia.org/wiki/Benthosema_glaciale"));
            var hatchet = S("hatchetfish", "disc", "#c4d3e2", "#2a3a56", "#eef4fa", "#9fb2c6", 1, "hatchet", 4, 18);
            hatchet.glow = 0.8f; hatchet.glowColor = U.Hex("#7fc8ff");
            Add(Identify(hatchet, "Lovely hatchetfish", "Argyropelecus aculeatus", "https://en.wikipedia.org/wiki/Argyropelecus_aculeatus"));
            var angler = S("humpback_anglerfish", "round", "#5a4650", "#2e2228", "#7a6670", "#3e3036", 1, "angler", 12, 70);
            angler.canSchool = false; angler.glow = 1; angler.glowColor = U.Hex("#a6ffe0"); angler.glowOffset = new Vector2(1.42f, 0.96f);
            Add(Identify(angler, "Humpback anglerfish", "Melanocetus johnsonii", "https://en.wikipedia.org/wiki/Melanocetus_johnsonii"));
            var viper = S("viperfish", "long", "#3c4d58", "#141d26", "#71838c", "#2c3a44", 1, "viper", 18, 110);
            viper.canSchool = false; viper.glow = 0.8f; viper.glowColor = U.Hex("#8fd8ff");
            Add(Identify(viper, "Sloane's viperfish", "Chauliodus sloani", "https://en.wikipedia.org/wiki/Sloane%27s_viperfish"));
            var swordfish = S("swordfish", "torpedo", "#6a5f7e", "#2e2a40", "#ddd6cc", "#4b4560", 1, "swordfish", 60, 700);
            swordfish.canSchool = false;
            AddKnown(swordfish, "Swordfish", "Xiphias gladius", "north-atlantic-swordfish");
            d["minnow"].forage = true;

            // Habitat weights (reef, kelp forest, abyss). Open-water hunters roam all three.
            void Zone(string key, float reef, float kelp, float abyss) { var z = d[key]; z.reef = reef; z.kelp = kelp; z.abyss = abyss; }
            Zone("minnow", 1, 1, 0.15f); Zone("clown", 1, 0, 0); Zone("tang", 1, 0.2f, 0); Zone("angel", 1, 0.2f, 0);
            Zone("puffer", 1, 0.4f, 0); Zone("parrot", 1, 0, 0); Zone("snapper", 1, 0.6f, 0.2f); Zone("barracuda", 1, 0.6f, 0.2f);
            Zone("grouper", 1, 0.8f, 0.3f); Zone("tuna", 0.6f, 0.6f, 0.4f);
            Zone("almaco_jack", 0.8f, 0.6f, 0.3f); Zone("goliath_grouper", 1, 0.5f, 0.2f); Zone("atlantic_halibut", 0.2f, 0.6f, 1);
            Zone("atlantic_mackerel", 0.6f, 1, 0.1f); Zone("mahi_mahi", 1, 0.4f, 0); Zone("skipjack_tuna", 0.7f, 0.7f, 0.2f);
            Zone("striped_bass", 0.3f, 1, 0); Zone("yellowfin_tuna", 0.6f, 0.6f, 0.4f); Zone("bluefish", 0.5f, 1, 0.1f);
            Zone("red_drum", 1, 0.5f, 0);
            Zone("pacific_sardine", 0.6f, 1, 0); Zone("garibaldi", 0.3f, 1, 0); Zone("california_sheephead", 0.3f, 1, 0.1f);
            Zone("lingcod", 0.1f, 1, 0.5f); Zone("lanternfish", 0, 0.2f, 1); Zone("hatchetfish", 0, 0, 1);
            Zone("humpback_anglerfish", 0, 0, 1); Zone("viperfish", 0, 0.05f, 1); Zone("swordfish", 0.6f, 0.6f, 0.6f);

            Shark = Identify(S("shark", "shark", "#879da4", "#334957", "#f2f5ee", "#647e87", 1, "shark", 0, 0),
                "White shark", "Carcharodon carcharias", noaa + "white-shark");
            TigerShark = Identify(S("tiger_shark", "shark", "#a0a982", "#4b5747", "#eeeed6", "#7d8866", 1, "tiger", 0, 0),
                "Tiger shark", "Galeocerdo cuvier", "https://www.fisheries.noaa.gov/feature-story/climate-change-shifting-tiger-shark-populations-northward");
            MakoShark = Identify(S("mako_shark", "shark", "#4f88b8", "#163d6c", "#f1f8f8", "#3b72a0", 1, "mako", 0, 0),
                "Shortfin mako shark", "Isurus oxyrinchus", noaa + "shortfin-mako-shark");
            MakoShark.aggressive = true; MakoShark.chaseSpeedMultiplier = 1.08f;
            Hammerhead = Identify(S("great_hammerhead", "shark", "#8c8f86", "#4f5650", "#eeeee4", "#73786e", 1, "hammer", 0, 0),
                "Great hammerhead", "Sphyrna mokarran", "https://en.wikipedia.org/wiki/Great_hammerhead");
            // The ocean's largest fish: the finale giant, not one of the rotating shark encounters.
            WhaleShark = Identify(S("whale_shark", "shark", "#4f6a86", "#2c3e57", "#e9eef0", "#425a74", 1, "whale", 0, 0),
                "Whale shark", "Rhincodon typus", "https://en.wikipedia.org/wiki/Whale_shark");
            WhaleShark.spot = U.Hex("#e9eef0");
            Player = S("player", "oval", "#3df2d0", "#0a8f99", "#e2fff8", "#ffc93c", 1, "player", 0, 0);
            Player.eye = 0.15f; Player.displayName = "Hero fish";
            return d;
        }

        // Size decides which species fit; the habitat at the spawn point weighs the choice, so each zone
        // keeps its own cast. With nothing in range, the zone's species of the nearest size stands in.
        public static string SpeciesFor(float r, bool school, Habitat.Look zone)
        {
            string pick = null, nearest = null;
            float total = 0, gap = float.MaxValue;
            foreach (var k in SpeciesKeys)
            {
                var s = SpeciesMap[k];
                if (school ? !s.canSchool || r <= 13 && !s.forage : s.forage && r > 6) continue;
                float weight = s.ZoneWeight(zone);
                if (weight < 0.05f) continue;
                if (r >= s.min && r <= s.max)
                {
                    total += weight;
                    if (U.Rand() * total < weight) pick = k;
                }
                else
                {
                    float distance = r < s.min ? s.min - r : r - s.max;
                    if (distance < gap) { gap = distance; nearest = k; }
                }
            }
            return pick ?? nearest ?? "minnow";
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
            ["flat"] = new Shape { hh = 0.60f, nx = 0.78f, ny = 0.95f, ped = 0.15f, tl = 0.33f, tH = 0.36f, dH = 0.18f, aH = 0.18f, tail = TailKind.Fan, eye = 0.07f },
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
            // concept art has its eyes and fins painted in; every fish shows expressions as skin-tinted lids over the eye
            public bool painted, lids, whole;
            public bool fallbackValidated;
            public Vector2 eyeSize;
            public Color lid;
        }

        static readonly Dictionary<string, Art> cache = new Dictionary<string, Art>();
        static Sprite eyeNormal, eyeShark, eyeSharkAngry, finOval, lidBlink, lidHappy, lidAngry;

        public static Art Get(Species sp)
        {
            if (cache.TryGetValue(sp.key, out var a)) return a;
            a = PaintedArt.Fish(sp) ?? LoadConcept(sp) ?? Bake(sp);
            cache[sp.key] = a;
            return a;
        }

        public static void Prewarm()
        {
            foreach (var s in Data.AllSpecies) Get(s);
            _ = EyeNormal; _ = EyeShark; _ = EyeSharkAngry; _ = FinOval;
            _ = LidBlink; _ = LidHappy; _ = LidAngry;
        }

        // ------------------------------------------------------------------ concept art (tools/cut_concepts.py)
        static Art LoadConcept(Species sp)
        {
            var m = ConceptArt.FishMeta(sp.key);
            if (m == null) return null;
            var body = ConceptArt.Load($"fish_{sp.key}_body", m.bodyPX, m.bodyPY, m.ppu);
            var tail = ConceptArt.Load($"fish_{sp.key}_tail", m.tailPX, m.tailPY, m.ppu);
            if (body == null || tail == null) return null;
            var open = m.open ? ConceptArt.Load($"fish_{sp.key}_open", m.bodyPX, m.bodyPY, m.ppu) : null;
            return new Art
            {
                body = body, bodyOpen = open ?? body, tail = tail,
                eyePos = new Vector2(m.eyeX, m.eyeY), eyeSize = new Vector2(m.eyeRX, m.eyeRY), hh = m.hh,
                shark = sp.Sh.tail == TailKind.Shark, fin = sp.fin, painted = true, lids = m.overlay,
                lid = new Color(m.lidR, m.lidG, m.lidB),
            };
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
            bool outline = sp == Data.Player;
            float scale = shark ? 300f : PPU;
            float pad = outline ? 0.3f : 0.08f;
            float minY = -(hh + hl * sh.dH * 1.45f) - pad - (sh.trailing ? hh * 0.2f : 0);
            float maxY = Mathf.Max(hh * 1.95f, hh + hl * sh.aH * 1.45f + (sh.trailing ? hh * 0.2f : 0)) + pad;
            float minX = -hl * 1.0f - pad, maxX = hl * 1.08f + pad;

            var art = new Art { shark = shark, hh = hh, fin = sp.fin, lids = !shark, lid = Color.Lerp(sp.c1, sp.c0, 0.6f) };
            art.body = BakeBody(sp, sh, hl, hh, minX, minY, maxX, maxY, scale, 0f, outline);
            art.bodyOpen = BakeBody(sp, sh, hl, hh, minX, minY, maxX, maxY, scale, 1f, outline);

            float tl = hl * sh.tl, th = hl * sh.tH;
            var tr = new Raster(-tl * 1.35f - pad, -th - pad, hl * 0.12f + pad, th + pad, scale);
            var tm = tr.Fill(TailPath(sh, hl, hh));
            if (outline) tr.Paint(tr.Dilate(tm, OutlinePx(scale)), OutlineCol);
            tr.Paint(tr.Dilate(tm, ContourPx(scale)), Navy);
            Color tc = sp.tailCol;
            tr.Paint(tm, (x, y) =>
            {
                float t = Mathf.Clamp01(-x / (tl * 1.2f));
                var c = Color.Lerp(tc * 0.8f, Color.Lerp(tc, Color.white, 0.22f), t);
                c.a = tc.a * (1 - t * 0.15f);
                return c;
            });
            if (!shark)
            {
                var rays = tr.Mask();
                for (int i = -4; i <= 4; i++)
                    tr.Stroke(new[] { new Vector2(hl * 0.06f, 0), new Vector2(-tl * 1.4f, i / 4f * th * 1.25f) }, hl * 0.016f, rays);
                Raster.Mul(rays, tm);
                tr.Paint(rays, new Color(0, 0.05f, 0.1f, 0.16f));
            }
            art.tail = VerifiedFallbackSprite(tr, sp.key, Vector2.zero, scale);
            art.fallbackValidated = true;

            float ex = hl * (shark ? 0.62f : 0.56f), ey = -hh * 0.22f;
            art.eyePos = new Vector2(ex, -ey);
            art.eyeR = hl * (sp.eye > 0 ? sp.eye : sh.eye) * (shark ? 1 : 1.2f);
            return art;
        }

        static readonly Color OutlineCol = new Color(0.94f, 1f, 1f, 1f);
        static Sprite VerifiedFallbackSprite(Raster raster, string key, Vector2 pivot, float scale)
        {
            // Raster uploads discard CPU pixel copies; inspect the actual generated buffer before upload.
            bool clear = false, visible = false;
            foreach (var pixel in raster.px)
            {
                if (!float.IsFinite(pixel.r) || !float.IsFinite(pixel.g) || !float.IsFinite(pixel.b) || !float.IsFinite(pixel.a))
                    throw new System.InvalidOperationException("Nonfinite generated fish pixels: " + key);
                clear |= pixel.a == 0; visible |= pixel.a > 0.5f;
            }
            if (!clear || !visible) throw new System.InvalidOperationException("Generated fish needs visible pixels and transparent padding: " + key);
            return raster.ToSprite(pivot, scale);
        }
        // string.GetHashCode varies between runtimes; reviews and fallback sprites need repeatable markings.
        static uint StableSeed(string key)
        {
            uint hash = 2166136261;
            foreach (char c in key) hash = unchecked((hash ^ c) * 16777619);
            return hash;
        }
        static int OutlinePx(float scale) => Mathf.RoundToInt(scale * 0.13f);
        // clean navy contours, as in the concept art
        internal static readonly Color Navy = new Color(0.05f, 0.09f, 0.16f, 1f);
        static int ContourPx(float scale) => Mathf.Max(2, Mathf.RoundToInt(scale * 0.022f));

        static Sprite BakeBody(Species sp, Shape sh, float hl, float hh, float minX, float minY, float maxX, float maxY, float scale, float mouth, bool outline)
        {
            var r = new Raster(minX, minY, maxX, maxY, scale);
            bool shark = sh.tail == TailKind.Shark;
            var fins = r.Fill(FinsPath(sh, hl, hh));
            var body = r.Fill(BodyPath(hl, hh, sh));

            // outlines round body and fins, faded out at the tail cut so the tail joins cleanly
            var silhouette = (float[])body.Clone();
            for (int i = 0; i < silhouette.Length; i++) silhouette[i] = Mathf.Max(silhouette[i], fins[i]);
            float cut = -hl * 0.86f + 2f / scale;
            Color Faded(Color c, float x) { c.a = Mathf.Clamp01((x - cut) * scale * 0.5f); return c; }
            if (outline) r.Paint(r.Dilate(silhouette, OutlinePx(scale)), (x, y) => Faded(OutlineCol, x)); // player: white sticker
            r.Paint(r.Dilate(silhouette, ContourPx(scale)), (x, y) => Faded(Navy, x));

            // fins behind the body: darker at the root, with fin rays
            float finSpan = hl * Mathf.Max(sh.dH, sh.aH) + hh * 0.4f;
            r.Paint(fins, (x, y) =>
            {
                float t = Mathf.Clamp01((Mathf.Abs(y) - hh * 0.55f) / finSpan);
                var c = Color.Lerp(sp.fin * 0.8f, Color.Lerp(sp.fin, Color.white, 0.2f), t);
                c.a = sp.fin.a;
                return c;
            });
            if (!shark)
            {
                var rays = r.Mask();
                for (int i = 0; i < 22; i++)
                {
                    float a = i / 22f * U.TAU;
                    r.Stroke(new[] { new Vector2(-hl * 0.1f, 0), new Vector2(-hl * 0.1f + Mathf.Cos(a) * hl * 2.2f, Mathf.Sin(a) * hl * 2.2f) }, hl * 0.016f, rays);
                }
                Raster.Mul(rays, fins);
                r.Paint(rays, new Color(0, 0.05f, 0.1f, 0.16f));
            }

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
                case "mottled":
                case "lingcod":
                case "halibut":
                case "whale":
                    {
                        var rng = new Mulberry(StableSeed(sp.key));
                        var m = r.Mask();
                        int n = sp.pat == "spots" ? 10 + (int)(rng.Next() * 6) : 32;
                        for (int i = 0; i < n; i++)
                        {
                            float sx = (rng.Next() * 1.5f - 0.75f) * hl, sy = (rng.Next() * 1.2f - 0.6f) * hh, sr = (0.05f + rng.Next() * 0.07f) * hl;
                            r.Circle(sx, sy, sr, m);
                        }
                        Clipped(m, U.WithA(sp.spot, 0.75f));
                    }
                    break;
                case "mackerel":
                case "tiger":
                    {
                        var m = r.Mask();
                        int count = sp.pat == "tiger" ? 11 : 24;
                        for (int i = 0; i < count; i++)
                        {
                            float bx = hl * (0.47f - i * 1.3f / count);
                            var wave = new List<Vector2>();
                            for (int j = 0; j <= 9; j++)
                            {
                                float sy = -hh + hh * 0.95f * j / 9;
                                wave.Add(new Vector2(bx + Mathf.Sin(j * 0.9f + i * 0.7f) * hl * 0.035f, sy));
                            }
                            r.Stroke(wave, hl * (sp.pat == "tiger" ? 0.033f : 0.022f), m);
                        }
                        Clipped(m, new Color(0.05f, 0.12f, 0.15f, sp.pat == "tiger" ? 0.58f : 0.75f));
                    }
                    break;
                case "skipjack":
                case "bass":
                    {
                        var m = r.Mask();
                        int count = sp.pat == "skipjack" ? 5 : 7;
                        for (int i = 0; i < count; i++)
                        {
                            float sy = hh * (sp.pat == "skipjack" ? 0.14f + i * 0.15f : -0.57f + i * 0.19f);
                            r.Stroke(Raster.QuadPts(new Vector2(-hl * 0.83f, sy * 0.6f), new Vector2(-hl * 0.08f, sy), new Vector2(hl * 0.56f, sy * 0.78f)), hl * 0.022f, m);
                        }
                        Clipped(m, new Color(0.11f, 0.18f, 0.23f, 0.78f));
                    }
                    break;
                case "amberjack":
                    {
                        Clipped(r.Fill(RectPath(-hl, -hh * 0.05f, hl * 2, hh * 0.13f)), new Color(0.8f, 0.66f, 0.25f, 0.62f));
                        var m = r.Stroke(Raster.QuadPts(new Vector2(hl * 0.98f, 0), new Vector2(hl * 0.54f, -hh * 0.27f), new Vector2(hl * 0.24f, -hh * 0.92f)), hl * 0.05f);
                        Clipped(m, new Color(0.1f, 0.18f, 0.18f, 0.82f));
                    }
                    break;
                case "yellowfin":
                    Clipped(r.Fill(RectPath(-hl, -hh * 0.1f, hl * 2, hh * 0.16f)), U.Hex("#e8d344", 0.7f));
                    break;
                case "mahi":
                    {
                        var m = r.Mask();
                        for (int i = 0; i < 34; i++)
                            r.Circle((Mathf.Sin(i * 3.1f) * 0.74f) * hl, Mathf.Sin(i * 1.7f) * hh * 0.8f, hl * 0.018f, m);
                        Clipped(m, U.WithA(sp.spot, 0.75f));
                    }
                    break;
                case "red_drum":
                    Clipped(r.Ellipse(-hl * 0.74f, 0, hl * 0.07f, hh * 0.13f, 0), sp.spot);
                    break;
                case "sardine":
                    {
                        var m = r.Mask();
                        for (int i = 0; i < 7; i++) r.Circle(hl * (0.45f - i * 0.17f), -hh * 0.18f, hl * 0.03f, m);
                        Clipped(m, U.WithA(sp.spot, 0.8f));
                    }
                    break;
                case "sheephead":
                    Clipped(r.Fill(RectPath(hl * 0.42f, -hh * 1.2f, hl, hh * 2.4f)), U.WithA(sp.c1, 0.92f));
                    Clipped(r.Fill(RectPath(-hl, -hh * 1.2f, hl * 0.42f, hh * 2.4f)), U.WithA(sp.c1, 0.92f));
                    Clipped(r.Ellipse(hl * 0.92f, hh * 0.35f, hl * 0.16f, hh * 0.22f, 0), U.WithA(sp.c2, 0.95f));
                    break;
                case "lantern":
                case "hatchet":
                case "viper":
                    {
                        // Photophore rows along the belly (the raster's y points down).
                        var m = r.Mask();
                        int n = sp.pat == "lantern" ? 9 : sp.pat == "hatchet" ? 11 : 13;
                        float row = sp.pat == "lantern" ? 0.5f : sp.pat == "hatchet" ? 0.82f : 0.70f;
                        for (int i = 0; i < n; i++) r.Circle(hl * (0.6f - i * 1.3f / n), hh * row, hl * 0.025f, m);
                        Clipped(m, U.WithA(sp.glowColor, 0.95f));
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

            // rounded volume: lit from above, shaded underneath, plus a soft top highlight
            var lit = r.Light(body, Mathf.Max(2, Mathf.RoundToInt(hh * scale * 0.32f)), new Vector3(-0.3f, 0.85f, 0.6f), 2f);
            var hi = r.Mask(); var lo = r.Mask();
            for (int i = 0; i < lit.Length; i++) { hi[i] = Mathf.Max(0, lit[i]) * 1.6f * body[i]; lo[i] = Mathf.Min(1, Mathf.Max(0, -lit[i]) * 1.3f) * body[i]; }
            r.Paint(lo, new Color(0, 0.05f, 0.14f, 0.42f));
            r.Paint(hi, new Color(1, 1, 1, 0.3f));
            Clipped(r.Ellipse(hl * 0.05f, -hh * 0.5f, hl * 0.55f, hh * 0.17f, -0.05f), new Color(1, 1, 1, 0.2f));

            if (mouth > 0)
            {
                var mp = new Path().Move(hl * 1.05f, -hh * 0.42f * mouth).Line(hl * (0.92f - 0.3f * mouth), hh * 0.05f).Line(hl * 1.05f, hh * 0.42f * mouth);
                Clipped(r.Fill(mp), U.Hex("#3b0d16"));
            }

            // body edge (separates it from the fins), gills and a smile
            r.Paint(r.InnerRing(body, Mathf.Max(1, Mathf.RoundToInt(scale * 0.014f))), (x, y) => Faded(U.WithA(Navy, 0.75f), x));
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
            if (!shark && mouth == 0)
            {
                var smile = r.Stroke(Raster.QuadPts(new Vector2(hl * 0.97f, hh * 0.1f), new Vector2(hl * 0.88f, hh * 0.3f), new Vector2(hl * 0.77f, hh * 0.12f)), hl * 0.024f);
                Raster.Mul(smile, body);
                r.Paint(smile, U.WithA(Navy, 0.8f));
            }

            if (shark)
            {
                var pf = new Path().Move(hl * 0.3f, hh * 0.55f).Line(hl * 0.3f - hl * 0.32f, hh * 0.55f + hh * 1.3f).Line(hl * 0.3f - hl * 0.18f, hh * 0.55f);
                r.Paint(r.Fill(pf), sp.fin);
            }
            return VerifiedFallbackSprite(r, sp.key, Vector2.zero, scale);
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

        // big cartoon eye in the concept style: navy rim, large pupil, two catch-lights
        public static Sprite EyeNormal => eyeNormal ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1.16f), Navy);
            r.Paint(r.Circle(0, 0, 1), (x, y) => Color.Lerp(Color.white, U.Hex("#d9e4f2"), Mathf.Clamp01(y * 0.8f + 0.2f)));
            r.Paint(r.Circle(0.24f, 0.04f, 0.66f), U.Hex("#0b0f14"));
            Shine(r);
            r.Paint(r.Circle(0.5f, 0.36f, 0.11f), new Color(1, 1, 1, 0.85f));
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

        // lids laid over the eye: white (tinted to the skin by the renderer) with dark lash lines
        static readonly Color Lash = U.Hex("#0b0f14");
        public static Sprite LidBlink => lidBlink ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1.18f), Color.white);
            r.Paint(r.Stroke(Raster.QuadPts(new Vector2(-1, 0.05f), new Vector2(0, 0.45f), new Vector2(1, 0.05f)), 0.3f), Lash);
        });
        public static Sprite LidHappy => lidHappy ??= EyeSprite(r =>
        {
            r.Paint(r.Circle(0, 0, 1.18f), Color.white);
            r.Paint(r.Stroke(Raster.QuadPts(new Vector2(-0.9f, 0.35f), new Vector2(0, -0.45f), new Vector2(0.9f, 0.35f)), 0.32f), Lash);
        });
        public static Sprite LidAngry => lidAngry ??= EyeSprite(r =>
        {
            // heavy upper lid sloping down toward the snout, edged with a scowl line
            var lid = r.Circle(0, 0, 1.18f);
            Raster.Mul(lid, r.Fill(new Path().Poly(new[] { new Vector2(-1.6f, -2), new Vector2(1.6f, -2), new Vector2(1.6f, 0.05f), new Vector2(-1.6f, -0.85f) })));
            r.Paint(lid, Color.white);
            r.Paint(r.Stroke(new[] { new Vector2(-1.35f, -0.97f), new Vector2(1.3f, -0.02f) }, 0.34f), Lash);
        });

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
        readonly SpriteRenderer body, tail, fin, eye, lid;
        readonly SortingGroup group;
        public readonly SpriteRenderer halo;
        FishArt.Art art;
        Species sp;
        readonly MaterialPropertyBlock swimProperties = new MaterialPropertyBlock();
        static readonly int PhaseId = Shader.PropertyToID("_Phase"), EnergyId = Shader.PropertyToID("_Energy"), MouthId = Shader.PropertyToID("_Mouth");
        static readonly int TailFlexId = Shader.PropertyToID("_TailFlex"), FinFlutterId = Shader.PropertyToID("_FinFlutter"),
            LightHeightId = Shader.PropertyToID("_LightHeight"), FogId = Shader.PropertyToID("_Fog"), FogColorId = Shader.PropertyToID("_FogColor");
        Vector4 motion;
        FishVolume volume;

        // Rate, tail flex, pectoral flutter and body drift distinguish propulsive fish from hovering ones.
        static Vector4 Motion(string shape) => shape switch
        {
            "round" => new Vector4(0.7f, 0.035f, 0.045f, 0.018f),
            "long" => new Vector4(1.25f, 0.06f, 0.018f, 0.004f),
            "torpedo" => new Vector4(1.15f, 0.095f, 0.012f, 0.004f),
            "shark" => new Vector4(0.7f, 0.105f, 0.009f, 0.006f),
            "tall" => new Vector4(0.7f, 0.06f, 0.042f, 0.012f),
            "fat" => new Vector4(0.82f, 0.06f, 0.016f, 0.008f),
            "slim" => new Vector4(1.35f, 0.085f, 0.022f, 0.005f),
            "disc" => new Vector4(0.92f, 0.07f, 0.03f, 0.009f),
            _ => new Vector4(1, 0.11f, 0.025f, 0.01f),
        };

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
            lid = Gfx.SpriteObject("Lid", eyeT, null, 4);
            halo = Gfx.SpriteObject("Halo", haloParent, Gfx.Glow, Layer.Halo);
            halo.enabled = false;
        }

        public void SetSpecies(Species s)
        {
            if (sp == s) return;
            sp = s;
            art = FishArt.Get(s);
            motion = Motion(s.shape);
            // Every catalog identity receives its own 3D model, including procedural skin fallbacks.
            volume ??= new FishVolume(pivot);
            volume.SetSpecies(s, art);
            volume.SetVisible(true);
            body.sprite = art.body;
            body.sharedMaterial = art.whole ? PaintedArt.SwimMaterial : Gfx.Alpha;
            body.SetPropertyBlock(null);
            tail.sprite = art.tail;
            tail.enabled = !art.whole;
            tailT.localPosition = new Vector3(-FishArt.HL * 0.84f, 0, 0);
            fin.enabled = HasFin;
            fin.color = art.fin * new Color(1, 1, 1, 0.85f);
            eyeT.localPosition = art.eyePos;
            eyeT.localScale = art.painted ? new Vector3(art.eyeSize.x, art.eyeSize.y, 1) : new Vector3(art.eyeR, art.eyeR, 1);
            eye.sprite = art.painted ? null : art.shark ? FishArt.EyeShark : FishArt.EyeNormal;
            lid.color = art.lid;
            body.enabled = eye.enabled = lid.enabled = tail.enabled = fin.enabled = false;
        }

        bool HasFin => art != null && !art.shark && !art.painted;

        public void SetActive(bool on)
        {
            if (root.activeSelf != on) { if (on) volume?.ResetMotion(); root.SetActive(on); }
            if (!on) halo.enabled = false;
        }

        public enum EyeMode { Normal, Angry, Blink, Happy }

        public void Pose(Fish f, int order, EyeMode eyeMode, float animationDt = -1)
        {
            if (volume != null)
            {
                root.transform.localPosition = U.V3(f.x, f.y);
                root.transform.localScale = Vector3.one;
                float swim = f.wag * motion.x;
                var rotation = volume.Rotation(f.faceS, f.tilt, swim, animationDt < 0 ? Time.deltaTime : animationDt);
                pivot.localRotation = rotation;
                pivot.localPosition = new Vector3(0, Mathf.Sin(swim * 0.65f) * motion.w * f.r, 0);
                float squash = Mathf.Sin(Mathf.Clamp01(f.mouth) * Mathf.PI) * 0.02f;
                pivot.localScale = new Vector3(f.r * (1 - squash), f.r * (1 + squash), f.r);
                group.sortingOrder = order;
                volume.Pose(f, motion, eyeMode, rotation);
                return;
            }
            // Facing interpolation is a turn progress signal, not the fish's physical width.
            float facing = Mathf.Clamp(f.faceS, -1, 1), turn = 1 - Mathf.Abs(facing);
            float direction = facing < 0 ? -1 : facing > 0 ? 1 : f.face < 0 ? -1 : 1;
            float fs = direction * Mathf.Lerp(1, 0.84f, turn);
            var t = root.transform;
            t.localPosition = U.V3(f.x, f.y);
            t.localScale = new Vector3(fs, 1, 1);
            float bank = Mathf.Sin(turn * Mathf.PI) * 5 * f.face;
            pivot.localRotation = Quaternion.Euler(0, 0, -f.tilt * Mathf.Rad2Deg + bank);
            float phase = f.wag * motion.x;
            pivot.localPosition = new Vector3(0, Mathf.Sin(phase * 0.65f) * motion.w * f.r, 0);
            // Anticipation, open jaw and a short recovery squash use the aligned authored keys.
            float anticipation = Mathf.Sin(Mathf.Clamp01(f.mouth) * Mathf.PI) * 0.035f;
            float recovery = Mathf.Sin(Mathf.Clamp01(f.chomp / 0.22f) * Mathf.PI) * 0.065f;
            pivot.localScale = new Vector3(f.r * (1 - anticipation + recovery), f.r * (1 + anticipation - recovery), 1);
            group.sortingOrder = order;

            float w = Mathf.Sin(f.wag) * 0.3f;
            float hh = art.hh;
            tailT.localPosition = new Vector3(-FishArt.HL * 0.84f, -w * hh * 0.45f, 0);
            tailT.localRotation = Quaternion.Euler(0, 0, -w * 1.3f * Mathf.Rad2Deg);
            body.sprite = f.mouth > 0.4f ? art.bodyOpen : art.body;
            if (art.whole)
            {
                swimProperties.SetFloat(PhaseId, phase);
                swimProperties.SetFloat(EnergyId, Mathf.Clamp(0.6f + new Vector2(f.vx, f.vy).magnitude / Mathf.Max(1, f.r) * 0.02f, 0.6f, 1.25f));
                swimProperties.SetFloat(MouthId, f.mouth);
                swimProperties.SetFloat(TailFlexId, motion.y);
                swimProperties.SetFloat(FinFlutterId, motion.z);
                swimProperties.SetFloat(LightHeightId, art.hh * 2);
                swimProperties.SetFloat(FogId, sp == Data.Player ? 0.025f : 0.035f + Mathf.Clamp01((f.y - 900) / 3500) * 0.045f);
                swimProperties.SetColor(FogColorId, World.WaterAt(f.y));
                body.SetPropertyBlock(swimProperties);
            }
            if (HasFin)
            {
                finT.localPosition = new Vector3(FishArt.HL * 0.22f, -hh * 0.25f, 0);
                finT.localRotation = Quaternion.Euler(0, 0, -(0.6f + Mathf.Sin(f.wag * 1.3f) * 0.35f) * Mathf.Rad2Deg);
                finT.localScale = new Vector3(FishArt.HL * 0.17f, Mathf.Max(hh * 0.12f, 0.02f), 1);
            }
            if (art.shark && !art.painted) eye.sprite = eyeMode == EyeMode.Angry ? FishArt.EyeSharkAngry : FishArt.EyeShark;
            lid.sprite = !art.lids ? null : eyeMode switch
            {
                EyeMode.Angry => FishArt.LidAngry,
                EyeMode.Blink => FishArt.LidBlink,
                EyeMode.Happy => FishArt.LidHappy,
                _ => null,
            };
        }

        public void SetVisible(bool v)
        {
            if (volume != null) { volume.SetVisible(v); return; }
            body.enabled = eye.enabled = lid.enabled = v;
            tail.enabled = v && !art.whole;
            fin.enabled = v && HasFin;
        }
    }
}
