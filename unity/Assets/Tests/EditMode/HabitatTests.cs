using NUnit.Framework;
using UnityEngine;

namespace DeepFeast.Tests
{
    public class HabitatTests
    {
        [TestCase(0f, "CORAL REEF")]
        [TestCase(2000f, "KELP FOREST")]
        [TestCase(4000f, "THE ABYSS")]
        public void NamesEachZone(float depth, string name) => Assert.AreEqual(name, Habitat.At(depth).name);

        // The scenery, music and spawns follow these weights; a jump would show as a hard seam.
        [Test]
        public void ZonesBlendWithoutJumps()
        {
            var previous = Habitat.At(0);
            for (float depth = 10; depth <= 4400; depth += 10)
            {
                var look = Habitat.At(depth);
                Assert.LessOrEqual(look.kelp + look.abyss, 1 + 1e-5f, $"depth {depth}");
                Assert.Less(Mathf.Abs(look.kelp - previous.kelp), 0.05f, $"kelp at {depth}");
                Assert.Less(Mathf.Abs(look.abyss - previous.abyss), 0.05f, $"abyss at {depth}");
                previous = look;
            }
        }
    }
}
