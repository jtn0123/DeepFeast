using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeepFeast.EditorTools
{
    public static class MotionValidation
    {
        [MenuItem("Deep Feast/Validate Fish Turns")]
        public static void Check()
        {
            var root = new GameObject("MotionValidation");
            try
            {
                var view = new FishView(root.transform, root.transform);
                var species = new List<Species>(Data.SpeciesMap.Values) { Data.Shark, Data.Player };
                foreach (var sp in species)
                {
                    view.SetSpecies(sp);
                    var meshes = view.root.GetComponentsInChildren<MeshFilter>();
                    bool volume = false;
                    foreach (var mesh in meshes)
                    {
                        if (mesh.sharedMesh == null) continue;
                        var bounds = mesh.sharedMesh.bounds;
                        foreach (var vector in new[] { bounds.center, bounds.extents })
                            if (!float.IsFinite(vector.x) || !float.IsFinite(vector.y) || !float.IsFinite(vector.z))
                                throw new InvalidOperationException($"Invalid generated mesh bounds: {sp.key}, {mesh.sharedMesh.name}.");
                        if (bounds.size.z > 0.1f) volume = true;
                    }
                    if (!volume) throw new InvalidOperationException($"Fish volume failed for {sp.key}: expected rounded mesh geometry with real depth.");
                    Vector3 previous = Vector3.zero;
                    foreach (var facing in new[] { -1f, -0.1f, -0.01f, 0f, 0.01f, 0.1f, 1f })
                    {
                        var f = new Fish { sp = sp, r = 22, face = -1, faceS = facing, wag = 2, y = 1700 };
                        view.Pose(f, 0, FishView.EyeMode.Normal);
                        var scale = view.root.transform.localScale;
                        if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) throw new InvalidOperationException("A 3D turn must not mirror the model: " + sp.key);
                        var heading = view.root.transform.Find("Pivot").localToWorldMatrix.MultiplyVector(Vector3.right).normalized;
                        if (!float.IsFinite(heading.x) || !float.IsFinite(heading.z)) throw new InvalidOperationException("Invalid turn transform: " + sp.key);
                        if (Mathf.Abs(facing) <= 0.01f && previous != Vector3.zero && Vector3.Angle(previous, heading) > 8)
                            throw new InvalidOperationException("Discontinuous midpoint heading: " + sp.key);
                        if (facing == 0 && heading.z > -0.95f) throw new InvalidOperationException("Mid-turn must face the camera: " + sp.key);
                        previous = heading;
                    }
                    foreach (float facing in new[] { -1f, 1f })
                    {
                        view.Pose(new Fish { sp = sp, r = 22, faceS = facing, face = facing, tilt = 0.35f }, 0, FishView.EyeMode.Normal);
                        var heading = view.root.transform.Find("Pivot").localToWorldMatrix.MultiplyVector(Vector3.right).normalized;
                        if (heading.y > -0.25f) throw new InvalidOperationException($"Downward swimming must pitch the nose down in both directions: {sp.key}, facing={facing}.");
                    }
                    // A reversal must not instantly flip the bank, and a completed turn must settle.
                    for (int i = 0; i <= 5; i++)
                        view.Pose(new Fish { sp = sp, r = 22, faceS = Mathf.Cos((80 + i * 9) * Mathf.Deg2Rad) }, 0, FishView.EyeMode.Normal, 1f / 60);
                    var pivot = view.root.transform.Find("Pivot");
                    var beforeReverse = pivot.localRotation;
                    float holdFacing = Mathf.Cos(116 * Mathf.Deg2Rad);
                    var hold = new Fish { sp = sp, r = 22, faceS = holdFacing };
                    view.Pose(hold, 0, FishView.EyeMode.Normal, 1f / 60);
                    float jump = Quaternion.Angle(beforeReverse, pivot.localRotation);
                    if (jump > 15) throw new InvalidOperationException($"Bank reversal snapped for {sp.key}: {jump:0.0} degrees in one frame.");
                    for (int i = 0; i < 40; i++) view.Pose(hold, 0, FishView.EyeMode.Normal, 1f / 60);
                    float residual = Quaternion.Angle(Quaternion.Euler(0, 116, 0), pivot.localRotation);
                    if (residual > 1) throw new InvalidOperationException($"Stationary fish retained turn bank for {sp.key}: {residual:0.0} degrees.");
                    var paused = pivot.localRotation;
                    for (int i = 0; i < 10; i++) view.Pose(hold, 0, FishView.EyeMode.Normal, 0);
                    if (Quaternion.Angle(paused, pivot.localRotation) > 0.01f) throw new InvalidOperationException("Paused animation changed: " + sp.key);
                }
                Debug.Log("[DeepFeast] volume turns passed: 12 species with finite geometry and bounds, mesh depth, positive scales, continuous midpoint heading, correct pitch, smooth bank reversal, settled bank and frozen pause.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
