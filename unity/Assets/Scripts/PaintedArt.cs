using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Transparent production atlases, with aligned closed/open keys and a deformable swimming mesh.</summary>
    public static class PaintedArt
    {
        [System.Serializable]
        public sealed class Frame
        {
            public float x, y, width, height, pivotX, pivotY;
        }

        [System.Serializable]
        public sealed class FishEntry
        {
            public string key, atlas;
            public float ppu, eyeX, eyeY, eyeRX, eyeRY;
            public Frame closed, open;
        }

        [System.Serializable]
        public sealed class PropEntry
        {
            public string key, atlas;
            public float ppu;
            public Frame frame;
        }

        [System.Serializable]
        public sealed class Catalog { public FishEntry[] fish; public PropEntry[] props; }

        static Catalog catalog;
        static bool loaded;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> props = new Dictionary<string, Sprite>();
        static Material swim, scenery;
        sealed class PendingMesh { public UnityEngine.Sprite sprite; public Vector2[] vertices; public ushort[] triangles; }
        static readonly List<PendingMesh> pending = new List<PendingMesh>();
        public static int PendingMeshCount => pending.Count;

        /// Unity only permits sprite geometry overrides inside its player loop, not editor build callbacks or asset import.
        public static void PrepareMeshes()
        {
            if (pending.Count == 0) return;
            foreach (var mesh in pending)
            {
                mesh.sprite.OverrideGeometry(mesh.vertices, mesh.triangles);
                if (mesh.sprite.vertices.Length != mesh.vertices.Length)
                    throw new System.InvalidOperationException("Unity rejected swimming geometry for " + mesh.sprite.name);
            }
            Debug.Log($"[DeepFeast] prepared {pending.Count} swimming meshes in the player loop.");
            pending.Clear();
        }

        public static Catalog Entries
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    var json = Resources.Load<TextAsset>("Concept/painted-atlas");
                    if (json != null) catalog = JsonUtility.FromJson<Catalog>(json.text);
                }
                return catalog;
            }
        }

        public static Material SwimMaterial => swim ??= new Material(Resources.Load<Shader>("Shaders/FishSwim"));
        public static Material SceneryMaterial => scenery ??= new Material(Resources.Load<Shader>("Shaders/PaintedScenery"));

        static Sprite Sprite(string atlas, Frame f, float ppu, bool deform)
        {
            if (!textures.TryGetValue(atlas, out var tex))
            {
                tex = Resources.Load<Texture2D>("Concept/" + atlas);
                textures[atlas] = tex;
            }
            if (tex == null || f == null) return null;
            // The catalog uses top-left pixel coordinates; Unity sprite rectangles use bottom-left coordinates.
            var rect = new Rect(f.x, tex.height - f.y - f.height, f.width, f.height);
            var sprite = UnityEngine.Sprite.Create(tex, rect, new Vector2(f.pivotX, f.pivotY), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = atlas + (deform ? "_swim" : "_prop");
            if (deform)
            {
                const int NX = 16, NY = 8;
                var v = new Vector2[(NX + 1) * (NY + 1)];
                var triangles = new ushort[NX * NY * 6];
                for (int y = 0; y <= NY; y++)
                    for (int x = 0; x <= NX; x++)
                        v[y * (NX + 1) + x] = new Vector2(f.width * x / NX, f.height * y / NY);
                int i = 0;
                for (int y = 0; y < NY; y++)
                    for (int x = 0; x < NX; x++)
                    {
                        ushort a = (ushort)(y * (NX + 1) + x), b = (ushort)(a + 1), c = (ushort)(a + NX + 1), d = (ushort)(c + 1);
                        triangles[i++] = a; triangles[i++] = b; triangles[i++] = c;
                        triangles[i++] = b; triangles[i++] = d; triangles[i++] = c;
                    }
                // OverrideGeometry accepts pixel coordinates relative to the sprite rectangle, then applies its pivot/PPU.
                pending.Add(new PendingMesh { sprite = sprite, vertices = v, triangles = triangles });
            }
            return sprite;
        }

        public static FishArt.Art Fish(Species sp)
        {
            if (Entries?.fish == null) return null;
            foreach (var f in Entries.fish)
            {
                if (f.key != sp.key) continue;
                var closed = Sprite(f.atlas, f.closed, f.ppu, true);
                var open = Sprite(f.atlas, f.open, f.ppu, true);
                if (closed == null || open == null) return null;
                return new FishArt.Art
                {
                    body = closed, bodyOpen = open, whole = true, painted = true, lids = true,
                    hh = FishArt.HL * sp.Sh.hh, shark = sp.IsShark, fin = sp.fin,
                    eyePos = new Vector2(f.eyeX, f.eyeY), eyeSize = new Vector2(f.eyeRX, f.eyeRY), lid = sp.c0,
                };
            }
            return null;
        }

        public static Sprite Prop(string key)
        {
            if (props.TryGetValue(key, out var result)) return result;
            if (Entries?.props == null) return null;
            foreach (var p in Entries.props)
                if (p.key == key)
                {
                    result = Sprite(p.atlas, p.frame, p.ppu, false);
                    props[key] = result;
                    return result;
                }
            return null;
        }
    }
}
