using NUnit.Framework;

namespace DeepFeast.Tests
{
    // A test run must never touch the player's best score, Fishdex, settings or mute switch.
    public class HarnessTests
    {
        [Test]
        public void PlainLaunchesSave()
        {
            Assert.IsFalse(Game.IsTestRun(new[] { "Deep Feast" }, false));
            Assert.IsFalse(Game.IsTestRun(new[] { "Deep Feast", "-mute" }, false));
        }

        [TestCase("-autoplay")]
        [TestCase("-padtest")]
        [TestCase("-quitafter")]
        [TestCase("-set")]
        [TestCase("-seed")]
        public void HarnessFlagsMakeATestRun(string flag) => Assert.IsTrue(Game.IsTestRun(new[] { "Deep Feast", flag, "1" }, false));

        [Test]
        public void HeadlessRunsAreTestRuns() => Assert.IsTrue(Game.IsTestRun(new[] { "Deep Feast" }, true));
    }
}
