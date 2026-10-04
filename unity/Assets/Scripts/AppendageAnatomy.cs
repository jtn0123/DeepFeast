using System.Collections.Generic;
using UnityEngine;

namespace DeepFeast
{
    // Rigid parts grown from the body: the anglerfish's lure, the viperfish's fangs and dorsal
    // filament, and the swordfish's bill. They join the body mesh, so they share its swimming
    // wave and contour; vertex alpha tells the shader to keep their own color, and the flex
    // channel pins each part to the lower jaw or to the skull instead of bending with the bite.
    public sealed partial class FishVolume
    {
        // Vertex alpha below 0.9 keeps the vertex color, below 0.45 skips the inked contour,
        // and below 0.2 the part glows.
        static readonly Color Ivory = new Color(0.95f, 0.93f, 0.84f, 0.4f);
        static readonly Vector2 Skull = new Vector2(0, 1), LowerJaw = new Vector2(1, 0);

        static void AddAppendages(Builder b, Species sp, Model model)
        {
            switch (sp.key)
            {
                case "humpback_anglerfish":
                {
                    // The illicium rises from the snout and arches forward over the mouth.
                    var rod = Curve(new Vector3(0.80f, 0.56f, 0), new Vector3(0.98f, 1.06f, 0), new Vector3(1.38f, 0.96f, 0), 14);
                    var stalk = Color.Lerp(sp.c1, sp.c0, 0.5f); stalk.a = 0.5f;
                    b.Tube(rod, t => Mathf.Lerp(0.020f, 0.011f, t), Vector2.one, stalk, Skull);
                    var esca = sp.glowColor; esca.a = 0.1f;
                    b.Ellipsoid(new Vector3(1.42f, 0.96f, 0), new Vector3(0.075f, 0.070f, 0.070f), Quaternion.identity, esca, 10, 14, default, Skull);
                    break;
                }
                case "viperfish":
                {
                    // Lower fangs reach up outside the closed mouth, past the upper jaw; the upper
                    // pair hangs down behind them.
                    float lift = model.profile.w;
                    foreach (float z in new[] { -0.034f, 0.034f })
                    {
                        b.Tube(Curve(new Vector3(1.22f, lift - 0.07f, z), new Vector3(1.32f, lift + 0.06f, z * 1.2f), new Vector3(1.21f, lift + 0.17f, z * 1.3f), 10),
                            t => Mathf.Lerp(0.017f, 0.0015f, t), Vector2.one, Ivory, LowerJaw);
                        b.Tube(Curve(new Vector3(1.12f, lift + 0.03f, z * 1.5f), new Vector3(1.19f, lift - 0.08f, z * 1.6f), new Vector3(1.17f, lift - 0.20f, z * 1.6f), 8),
                            t => Mathf.Lerp(0.013f, 0.0012f, t), Vector2.one, Ivory, Skull);
                    }
                    // The first dorsal ray is a long, thin filament curving back over the body,
                    // tipped with a small light.
                    float top = CenterY(0.60f, model) + model.height * Profile(0.60f, model) * 0.9f;
                    var ray = Color.Lerp(sp.fin, sp.c2, 0.3f); ray.a = 0.5f;
                    var tip = new Vector3(-0.20f, top + 0.22f, 0);
                    b.Tube(Curve(new Vector3(0.60f, top, 0), new Vector3(0.42f, top + 0.40f, 0), tip, 18),
                        t => Mathf.Lerp(0.010f, 0.004f, t), Vector2.one, ray, Skull);
                    var bead = sp.glowColor; bead.a = 0.1f;
                    b.Ellipsoid(tip, Vector3.one * 0.022f, Quaternion.identity, bead, 8, 10, default, Skull);
                    break;
                }
                case "swordfish":
                {
                    // A long, flat bill from the upper jaw: broad across, thin top to bottom.
                    float lift = model.profile.w + 0.02f;
                    var bill = Color.Lerp(sp.c1, sp.c0, 0.45f); bill.a = 0.5f;
                    b.Tube(Curve(new Vector3(1.34f, lift, 0), new Vector3(1.90f, lift + 0.01f, 0), new Vector3(2.48f, lift - 0.01f, 0), 16),
                        t => Mathf.Lerp(0.062f, 0.004f, Mathf.Pow(t, 0.8f)), new Vector2(0.36f, 1), bill, Skull);
                    break;
                }
            }
        }

        static List<Vector3> Curve(Vector3 a, Vector3 control, Vector3 c, int steps)
        {
            var points = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++) points.Add(SharkCurve(a, control, c, i / (float)steps));
            return points;
        }
    }
}
