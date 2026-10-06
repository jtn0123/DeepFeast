using NUnit.Framework;
using DeepFeast.EditorTools;

namespace DeepFeast.Tests
{
    // The checks every build runs first, here so they also run with the tests and in CI without a build.
    public class ValidationTests
    {
        // Every painted species, pose, prop, atlas import and shader, then the fish-turn checks.
        [Test]
        public void ProductionArtPasses() => ArtValidation.Check();
    }
}
