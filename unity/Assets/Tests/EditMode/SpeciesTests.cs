using NUnit.Framework;

namespace DeepFeast.Tests
{
    public class SpeciesTests
    {
        [TearDown]
        public void Unseed() => U.Seed(System.Environment.TickCount);

        // The variants are read from fields that Data's species table fills in, so they depend on the
        // order of Data's static fields.
        [Test]
        public void EverySharkVariantIsBuilt()
        {
            Assert.AreEqual(4, Data.SharkVariants.Length);
            foreach (var shark in Data.SharkVariants)
            {
                Assert.IsNotNull(shark);
                Assert.IsTrue(shark.IsShark, shark.key);
            }
        }

        [Test]
        public void EverySpeciesHasItsKeyASizeRangeAndAHome()
        {
            var zones = new[] { Habitat.At(0), Habitat.At(2000), Habitat.At(4000) };
            foreach (var pair in Data.SpeciesMap)
            {
                var s = pair.Value;
                Assert.AreEqual(pair.Key, s.key);
                Assert.Greater(s.min, 0, pair.Key);
                Assert.LessOrEqual(s.min, s.max, pair.Key);
                Assert.IsTrue(System.Array.Exists(zones, z => s.ZoneWeight(z) >= 0.05f), $"{pair.Key} never spawns");
            }
        }

        // Spawning picks a species that fits the size asked for whenever the zone has one, and schools
        // only ever pick fish that school (the smallest schools, forage fish).
        [TestCase(0f, false)]
        [TestCase(2000f, false)]
        [TestCase(4000f, false)]
        [TestCase(0f, true)]
        [TestCase(2000f, true)]
        [TestCase(4000f, true)]
        public void SpawnsPickAFittingSpecies(float depth, bool school)
        {
            U.Seed(1);
            var zone = Habitat.At(depth);
            foreach (float r in new[] { 5f, 9f, 14f, 22f, 35f, 55f, 85f, 130f, 200f })
            {
                var key = Data.SpeciesFor(r, school, zone);
                Assert.IsTrue(Data.SpeciesMap.ContainsKey(key), key);
                var picked = Data.SpeciesMap[key];
                if (school)
                {
                    Assert.IsTrue(picked.canSchool, $"{key} schooling at r={r}");
                    if (r <= 13) Assert.IsTrue(picked.forage, $"{key} in a small school");
                }
                bool anyFits = false;
                foreach (var s in Data.SpeciesMap.Values)
                {
                    bool excluded = school ? !s.canSchool || r <= 13 && !s.forage : s.forage && r > 6;
                    if (!excluded && s.ZoneWeight(zone) >= 0.05f && r >= s.min && r <= s.max) anyFits = true;
                }
                if (anyFits) Assert.IsTrue(r >= picked.min && r <= picked.max, $"{key} for r={r} at depth {depth}");
            }
        }
    }
}
