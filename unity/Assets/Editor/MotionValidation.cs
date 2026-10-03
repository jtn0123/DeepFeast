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
                    foreach (var facing in new[] { -1f, -0.1f, -0.01f, 0f, 0.01f, 0.1f, 1f })
                    {
                        var f = new Fish { sp = sp, r = 22, face = -1, faceS = facing, wag = 2, y = 1700 };
                        view.Pose(f, 0, FishView.EyeMode.Normal);
                        float width = Mathf.Abs(view.root.transform.localScale.x);
                        if (width < 0.8f || !float.IsFinite(width))
                            throw new InvalidOperationException($"Turn visibility failed for {sp.key}: facing={facing}, rendered width ratio={width:0.000}; expected >=0.800.");
                    }
                }
                Debug.Log("[DeepFeast] turn visibility passed: 12 species at 7 facing samples, width >=80%, finite transforms.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
