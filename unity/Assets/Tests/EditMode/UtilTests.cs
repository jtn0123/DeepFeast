using NUnit.Framework;
using UnityEngine;

namespace DeepFeast.Tests
{
    public class UtilTests
    {
        [TearDown]
        public void Unseed() => U.Seed(System.Environment.TickCount);

        [Test]
        public void HexReadsShortLongAndAlphaForms()
        {
            Assert.AreEqual(Color.white, U.Hex("#fff"));
            Assert.AreEqual((Color)new Color32(255, 128, 0, 255), U.Hex("#ff8000"));
            Assert.AreEqual(128 / 255f, U.Hex("#00000080").a, 1e-6f);
        }

        [Test]
        public void HexScalesAlpha() => Assert.AreEqual(0.5f, U.Hex("#ffffff", 0.5f).a, 1e-6f);

        [Test]
        public void ClampSafeClampsInsideOrderedBounds()
        {
            Assert.AreEqual(5, U.ClampSafe(5, 0, 10));
            Assert.AreEqual(0, U.ClampSafe(-1, 0, 10));
            Assert.AreEqual(10, U.ClampSafe(11, 0, 10));
        }

        // A sea too shallow for a fish gives crossed bounds; the fish goes to the middle, not an edge.
        [Test]
        public void ClampSafeTakesTheMiddleOfCrossedBounds() => Assert.AreEqual(5, U.ClampSafe(3, 10, 0));

        [Test]
        public void SeedRepeatsTheSequence()
        {
            U.Seed(42);
            var first = new float[10];
            for (int i = 0; i < first.Length; i++) first[i] = U.Rand();
            U.Seed(42);
            for (int i = 0; i < first.Length; i++) Assert.AreEqual(first[i], U.Rand());
        }
    }
}
