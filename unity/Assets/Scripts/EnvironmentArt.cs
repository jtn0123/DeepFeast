using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    /// <summary>Shared painted environment meshes, rooted current motion and depth fog. Source atlas alpha stays intact.</summary>
    public static class EnvironmentArt
    {
        sealed class ShapeData { public Mesh mesh; public Material material; public float height; }
        static readonly Dictionary<string, ShapeData> shapes = new Dictionary<string, ShapeData>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly int Phase = Shader.PropertyToID("_Phase"), Bend = Shader.PropertyToID("_Bend"), Height = Shader.PropertyToID("_Height"),
            Tint = Shader.PropertyToID("_Color"), Fog = Shader.PropertyToID("_Fog"), FogColor = Shader.PropertyToID("_FogColor");

        public sealed class Instance
        {
            public Transform transform;
            public MeshRenderer renderer;
            public float x, radius, phase, bend;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public void Pose(float time, Color tint, float fog, Color water)
            {
                block.SetFloat(Phase, time * 0.85f + phase);
                block.SetFloat(Bend, bend);
                block.SetColor(Tint, tint);
                block.SetFloat(Fog, fog);
                block.SetColor(FogColor, water);
                renderer.SetPropertyBlock(block);
            }
            internal void HeightValue(float height) { block.SetFloat(Height, height); }
        }

        static ShapeData Shape(string key)
        {
            if (shapes.TryGetValue(key, out var found)) return found;
            if (PaintedArt.Entries?.props == null) return null;
            foreach (var entry in PaintedArt.Entries.props)
            {
                if (entry.key != key) continue;
                var texture = Resources.Load<Texture2D>("Concept/" + entry.atlas);
                if (texture == null) return null;
                if (!materials.TryGetValue(entry.atlas, out var material))
                {
                    material = new Material(Resources.Load<Shader>("Shaders/PlantSway")) { mainTexture = texture };
                    materials.Add(entry.atlas, material);
                }
                var f = entry.frame;
                var mb = new MeshBuilder();
                const int NX = 12, NY = 20;
                for (int y = 0; y <= NY; y++)
                    for (int x = 0; x <= NX; x++)
                    {
                        float u = x / (float)NX, v = y / (float)NY;
                        mb.Vert((u - f.pivotX) * f.width / entry.ppu, (v - f.pivotY) * f.height / entry.ppu, Color.white,
                            (f.x + u * f.width) / texture.width, (texture.height - f.y - f.height + v * f.height) / texture.height);
                    }
                for (int y = 0; y < NY; y++)
                    for (int x = 0; x < NX; x++)
                    {
                        int a = y * (NX + 1) + x;
                        mb.Quad(a, a + 1, a + NX + 2, a + NX + 1);
                    }
                var mesh = new Mesh { name = "Painted_" + key };
                mb.Apply(mesh);
                mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + new Vector3(0.25f, 0.1f, 0));
                mesh.UploadMeshData(true);
                found = new ShapeData { mesh = mesh, material = material, height = f.height / entry.ppu };
                shapes.Add(key, found);
                return found;
            }
            return null;
        }

        public static Mesh Geometry(string key) => Shape(key)?.mesh;

        public static Instance Place(string key, Transform parent, int order, float x, float y, float scale, bool flip, float phase, float bend)
        {
            var shape = Shape(key);
            if (shape == null) return null;
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = U.V3(x, y);
            go.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1);
            go.AddComponent<MeshFilter>().sharedMesh = shape.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shape.material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var result = new Instance { transform = go.transform, renderer = renderer, x = x, radius = scale * shape.mesh.bounds.extents.x,
                phase = phase, bend = bend };
            result.HeightValue(shape.height);
            result.Pose(0, Color.white, 0, Color.white);
            return result;
        }
    }
}
