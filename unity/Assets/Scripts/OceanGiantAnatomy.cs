using UnityEngine;

namespace DeepFeast
{
    // Two body plans outside the ring-bodied fishes' range. The ocean sunfish is a tall disc cut
    // off behind its dorsal and anal fins, finished by the scalloped clavus instead of a tail.
    // The giant manta's head, body and wings are one flat disc, so the skin pattern, the contour
    // and the wing beat (in the shader) run across all of it; its cephalic lobes take the place
    // of the pectoral parts.
    public sealed partial class FishVolume
    {
        public const string Manta = "giant_manta_ray";
        // The manta banks its back toward the viewer, so the side camera sees the spread wings.
        public const float MantaBank = 70;
        const float MantaTipX = -0.30f, MantaSpan = 1.45f, MantaHead = 0.42f;

        static Model MantaBody() => new Model
        {
            height = 0.20f, depth = MantaHead,
            head = new Vector4(0.22f, 0.32f, 0.20f, MantaHead),
            profile = new Vector4(1.4f, 0.35f, 0.10f, 0),
        };

        // Half span of the disc: the rounded head, a convex leading edge out to the swept-back,
        // pointed wing tip and a concave trailing edge back to the root of the tail.
        static float MantaWidth(float x, Model model)
        {
            if (x >= model.head.x) return MantaHead * Profile(x, model);
            if (x >= MantaTipX) return MantaHead + (MantaSpan - MantaHead) * Mathf.Pow(Mathf.InverseLerp(model.head.x, MantaTipX, x), 0.7f);
            return 0.12f + (MantaSpan - 0.12f) * Mathf.Pow(1 - Mathf.InverseLerp(MantaTipX, -1.04f, x), 2.2f);
        }

        // Thick over the body, thin across the wings.
        static float MantaTaper(float z) => Mathf.Lerp(1, 0.28f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.25f, 1.1f, Mathf.Abs(z))));

        // Cephalic lobes: rolled flaps reaching forward from each side of the head, curling inwards.
        static Mesh MantaLobe(Species sp, Model model, int side, out Vector3 root)
        {
            var b = new Builder();
            root = new Vector3(0.34f, CenterY(0.34f, model), side * (MantaWidth(0.34f, model) - 0.03f));
            var spine = Curve(root, new Vector3(0.66f, root.y - 0.01f, side * 0.47f), new Vector3(0.76f, root.y - 0.07f, side * 0.31f), 14);
            var color = Color.Lerp(sp.c1, sp.c0, 0.3f);
            b.Tube(spine, t => Mathf.Lerp(0.07f, 0.03f, t * t), new Vector2(0.55f, 1), color, Vector2.zero, 12, t => new Vector2(t, side));
            return b.Mesh(sp.key + (side < 0 ? " near" : " far") + " cephalic lobe");
        }

        // The clavus: a short, scalloped fringe across the whole truncated rear of the sunfish.
        static void Clavus(Builder b, Species sp, Model model)
        {
            float stalk = model.height * model.profile.z;
            b.Membrane(sp.tailCol, 10, 48, 4, (weight, along) =>
            {
                float s = along * 2 - 1, lobe = 0.5f - 0.5f * Mathf.Cos(along * Mathf.PI * 2 * 6);
                var root = new Vector3(-1.04f, s * stalk, 0);
                var rim = new Vector3(-1.04f - (0.13f + 0.05f * lobe) * Mathf.Sqrt(1 - 0.75f * s * s), s * stalk * 1.04f, 0);
                var p = Vector3.Lerp(root, rim, weight);
                p.z = Mathf.Sin(weight * Mathf.PI) * 0.02f;
                return p;
            });
        }
    }
}
