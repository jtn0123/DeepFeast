using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepFeast.EditorTools
{
    /// <summary>Fail the build if any production species/pose is missing, clipped, opaque-backed, or incorrectly imported.</summary>
    public static class ArtValidation
    {
        [MenuItem("Deep Feast/Validate Production Art")]
        public static void Check()
        {
            var catalog = PaintedArt.Entries;
            Require(catalog?.fish != null && catalog.fish.Length == 12, "Expected all twelve painted fish.");
            Require(catalog.props != null && catalog.props.Length == 16, "Expected six reef props, six plants and four parallax formations.");
            var keys = new HashSet<string>();
            var atlases = new HashSet<string>();
            foreach (var f in catalog.fish)
            {
                Require(keys.Add(f.key), "Duplicate fish: " + f.key);
                Require(f.ppu > 0, "Invalid scale: " + f.key);
                Frame(f.atlas, f.closed); Frame(f.atlas, f.open);
                atlases.Add(f.atlas);
            }
            var legacyKeys = new HashSet<string> { "minnow", "clown", "tang", "angel", "puffer", "parrot", "snapper", "barracuda", "grouper", "tuna", "player", "shark" };
            Require(keys.SetEquals(legacyKeys), "The twelve legacy painted identities must retain their aligned atlas poses.");
            var propKeys = new HashSet<string>();
            foreach (var p in catalog.props)
            {
                Require(propKeys.Add(p.key), "Duplicate prop: " + p.key);
                Require(p.ppu > 0, "Invalid prop scale: " + p.key);
                Frame(p.atlas, p.frame); atlases.Add(p.atlas);
            }
            foreach (var atlas in atlases)
            {
                string path = "Assets/Resources/Concept/" + atlas + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Require(importer != null && importer.alphaIsTransparency && !importer.mipmapEnabled && !importer.isReadable,
                    "Incorrect alpha/mipmap/readability import: " + atlas);
                Require(importer.textureCompression == TextureImporterCompression.Uncompressed, "Atlas compression could damage padded edges: " + atlas);
                var tex = new Texture2D(2, 2);
                Require(tex.LoadImage(File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath, "Resources/Concept/" + atlas + ".png"))), "Cannot decode: " + atlas);
                var px = tex.GetPixels32();
                int clear = 0, solid = 0;
                foreach (var pixel in px) { if (pixel.a == 0) clear++; if (pixel.a > 240) solid++; }
                Require(clear > px.Length / 4 && solid > px.Length / 10, "Atlas must have real transparent padding and solid artwork: " + atlas);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            var species = Data.AllSpecies;
            Require(species.Count == Data.SpeciesMap.Count + Data.SharkVariants.Length + 1, "Complete species catalog is missing a fish, shark or hero.");
            Require(Data.SharkVariants.Length == 3, "Expected white, tiger and shortfin mako shark variants.");
            var allKeys = new HashSet<string>();
            int preparedMeshes = 0, fallbackCount = 0;
            foreach (var sp in species)
            {
                Require(allKeys.Add(sp.key), "Duplicate native species: " + sp.key);
                Require(!string.IsNullOrWhiteSpace(sp.displayName), "Missing display identity: " + sp.key);
                Require(FishArt.Shapes.ContainsKey(sp.shape), "Unknown species body shape: " + sp.key);
                Require(float.IsFinite(sp.chaseSpeedMultiplier) && sp.chaseSpeedMultiplier >= 1 && sp.chaseSpeedMultiplier <= 1.1f,
                    "Unbounded pursuit multiplier: " + sp.key);
                if (!legacyKeys.Contains(sp.key) || sp.IsShark)
                {
                    Require(!string.IsNullOrWhiteSpace(sp.scientificName), "Missing scientific identity: " + sp.key);
                    Require(Uri.TryCreate(sp.referenceUrl, UriKind.Absolute, out var reference) && reference.Scheme == "https",
                        "Missing source reference: " + sp.key);
                }
                if (Data.SpeciesMap.ContainsKey(sp.key))
                    Require(!sp.IsShark && float.IsFinite(sp.min) && float.IsFinite(sp.max) && sp.min > 0 && sp.max >= sp.min,
                        "Invalid normal-spawn species range: " + sp.key);
                var a = FishArt.Get(sp);
                if (!legacyKeys.Contains(sp.key))
                {
                    Require(a.fallbackValidated && !a.whole && !a.painted && a.body != null && a.bodyOpen != null && a.bodyOpen != a.body && a.tail != null,
                        "Missing generated fallback poses/tail for native species: " + sp.key);
                    foreach (var sprite in new[] { a.body, a.bodyOpen, a.tail }) FallbackSprite(sp.key, sprite);
                    Require(Mathf.Abs(a.body.bounds.min.x - a.bodyOpen.bounds.min.x) < 0.01f &&
                        Mathf.Abs(a.body.bounds.size.x - a.bodyOpen.bounds.size.x) < 0.01f, "Unaligned generated poses: " + sp.key);
                    fallbackCount++;
                    continue;
                }
                Require(a.whole && a.painted && a.body != null && a.bodyOpen != null && a.bodyOpen != a.body,
                    "Missing painted closed/open fish: " + sp.key);
                Require(Mathf.Abs(a.body.bounds.min.x - a.bodyOpen.bounds.min.x) < 0.12f, "Unaligned tail anchor: " + sp.key);
                Require(a.body.bounds.size.x > 2.5f && a.body.bounds.size.x < 3.5f, "Unexpected game silhouette scale: " + sp.key);
                if (a.body.vertices.Length == 153) preparedMeshes++;
                if (a.bodyOpen.vertices.Length == 153) preparedMeshes++;
            }
            Require(PaintedArt.PendingMeshCount + preparedMeshes == 24, "Expected 24 swimming meshes, prepared or queued for the first player update.");
            foreach (var key in new[] { "almaco_jack", "goliath_grouper", "atlantic_halibut", "atlantic_mackerel", "mahi_mahi", "skipjack_tuna", "striped_bass", "yellowfin_tuna", "bluefish", "red_drum" })
                Require(Data.SpeciesMap.ContainsKey(key), "Missing requested or companion fish: " + key);
            for (int i = 0; i < Data.SharkVariants.Length * 2; i++)
            {
                var shark = Data.SharkForEncounter(i);
                Require(shark.IsShark && allKeys.Contains(shark.key) && !Data.SpeciesMap.ContainsKey(shark.key), "Shark variants must use the timed encounter pool: " + shark.key);
                Require(shark == Data.SharkVariants[i % Data.SharkVariants.Length], "Shark identity rotation failed.");
            }
            Require(Data.SharkVariants[0].key == "shark" && Data.SharkVariants[1].key == "tiger_shark" && Data.SharkVariants[2].key == "mako_shark",
                "Shark identity rotation must include white, tiger and mako.");
            Require(Data.MakoShark.aggressive && Data.MakoShark.chaseSpeedMultiplier > Data.Shark.chaseSpeedMultiplier,
                "The aggressive mako must have a modest faster pursuit.");
            Require(!Data.SpeciesMap["atlantic_halibut"].canSchool && !Data.SpeciesMap["goliath_grouper"].canSchool,
                "Bottom-dwelling halibut and goliath grouper must not spawn in schooling packs.");
            var shader = Resources.Load<Shader>("Shaders/FishSwim");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Swimming shader is missing or has compiler errors.");
            foreach (var key in new[] { "branch", "brain", "tube", "fan", "rock", "deep" })
                Require(PaintedArt.Prop(key) != null, "Missing prop: " + key);
            foreach (var key in new[] { "kelp-teal", "kelp-olive", "seaweed-red", "seagrass", "anemone-rose", "anemone-deep", "reef-shelf", "reef-arch", "reef-terrace", "reef-abyss" })
            {
                var mesh = EnvironmentArt.Geometry(key);
                Require(mesh != null && mesh.vertexCount == 273, "Missing environment deformation mesh: " + key);
                Require(mesh.bounds.min.y < 0.02f && mesh.bounds.max.y > 0.5f, "Unrooted or undersized environment sprite: " + key);
            }
            var plants = Resources.Load<Shader>("Shaders/PlantSway");
            Require(plants != null && !ShaderUtil.ShaderHasError(plants), "Plant current/fog shader is missing or has compiler errors.");
            var scenery = Resources.Load<Shader>("Shaders/PaintedScenery");
            Require(scenery != null && !ShaderUtil.ShaderHasError(scenery), "Shared scenery lighting shader is missing or has compiler errors.");
            MotionValidation.Check();
            var volume = Resources.Load<Shader>("Shaders/FishVolume");
            Require(volume != null && !ShaderUtil.ShaderHasError(volume), "Volume fish shader is missing or has compiler errors.");
            Debug.Log($"[DeepFeast] production art validation passed: {species.Count} native species, {catalog.fish.Length} legacy painted species, 24 aligned painted pose keys, {fallbackCount} generated fallback species with finite transparent sprites and source metadata, {Data.SharkVariants.Length} rotating shark variants, 16 props, 10 shared environment meshes, transparent atlases, four shaders and volume turns.");
        }

        static void FallbackSprite(string key, Sprite sprite)
        {
            foreach (var vector in new[] { sprite.bounds.center, sprite.bounds.extents })
                Require(float.IsFinite(vector.x) && float.IsFinite(vector.y) && float.IsFinite(vector.z), "Nonfinite generated sprite bounds: " + key);
            Require(sprite.bounds.size.x > 0.1f && sprite.bounds.size.y > 0.1f, "Empty generated silhouette: " + key);
            foreach (var vertex in sprite.vertices)
                Require(float.IsFinite(vertex.x) && float.IsFinite(vertex.y), "Nonfinite generated sprite vertex: " + key);
            Require(sprite.texture != null && sprite.texture.width > 2 && sprite.texture.height > 2,
                "Missing generated sprite texture: " + key);
        }

        static void Frame(string atlas, PaintedArt.Frame f)
        {
            var texture = Resources.Load<Texture2D>("Concept/" + atlas);
            Require(texture != null && f != null, "Missing texture/frame: " + atlas);
            Require(f.x >= 0 && f.y >= 0 && f.width > 0 && f.height > 0 && f.x + f.width <= texture.width && f.y + f.height <= texture.height,
                "Frame outside atlas: " + atlas);
            Require(f.pivotX >= 0 && f.pivotX <= 1 && f.pivotY >= 0 && f.pivotY <= 1, "Invalid frame anchor: " + atlas);
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
