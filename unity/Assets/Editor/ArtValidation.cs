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
            var species = new List<Species>(Data.SpeciesMap.Values) { Data.Player, Data.Shark };
            int preparedMeshes = 0;
            foreach (var sp in species)
            {
                var a = FishArt.Get(sp);
                Require(a.whole && a.painted && a.body != null && a.bodyOpen != null && a.bodyOpen != a.body,
                    "Missing painted closed/open fish: " + sp.key);
                Require(Mathf.Abs(a.body.bounds.min.x - a.bodyOpen.bounds.min.x) < 0.12f, "Unaligned tail anchor: " + sp.key);
                Require(a.body.bounds.size.x > 2.5f && a.body.bounds.size.x < 3.5f, "Unexpected game silhouette scale: " + sp.key);
                if (a.body.vertices.Length == 153) preparedMeshes++;
                if (a.bodyOpen.vertices.Length == 153) preparedMeshes++;
            }
            Require(PaintedArt.PendingMeshCount + preparedMeshes == 24, "Expected 24 swimming meshes, prepared or queued for the first player update.");
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
            Debug.Log("[DeepFeast] production art validation passed: 12 species, 24 aligned pose keys, 16 props, 10 shared environment meshes, transparent atlases and both animation shaders.");
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
