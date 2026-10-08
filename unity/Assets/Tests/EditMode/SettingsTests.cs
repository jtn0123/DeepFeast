using System;
using NUnit.Framework;

namespace DeepFeast.Tests
{
    // Only test-run loads (save = false): tests never read or write the editor's saved settings.
    public class SettingsTests
    {
        [SetUp, TearDown]
        public void Reset() => GameSettings.Load(false, null);

        [Test]
        public void TestRunsStartFromDefaults()
        {
            Assert.AreEqual(100, GameSettings.Data.textSize);
            Assert.IsFalse(GameSettings.Data.dashToggle);
            Assert.IsFalse(GameSettings.Data.reduceFlashing);
        }

        [Test]
        public void OverridesSetOptionsByName()
        {
            GameSettings.Load(false, "dashToggle=true, textSize=150");
            Assert.IsTrue(GameSettings.Data.dashToggle);
            Assert.AreEqual(150, GameSettings.Data.textSize);
        }

        [Test]
        public void UnknownOverridesFail() => Assert.Throws<ArgumentException>(() => GameSettings.Load(false, "bogus=1"));

        [Test]
        public void OutOfRangeValuesFallBackOrClamp()
        {
            GameSettings.Load(false, "textSize=137,master=250,shake=-5,maxFps=7");
            Assert.AreEqual(100, GameSettings.Data.textSize);
            Assert.AreEqual(100, GameSettings.Data.master);
            Assert.AreEqual(0, GameSettings.Data.shake);
            Assert.AreEqual(120, GameSettings.Data.maxFps);
        }

        [Test]
        public void ReduceFlashingSlowsBlinksToOncePerSecond()
        {
            Assert.AreEqual(4, GameSettings.FlashHz(4));
            GameSettings.Load(false, "reduceFlashing=true");
            Assert.AreEqual(1, GameSettings.FlashHz(4));
            Assert.AreEqual(0.5f, GameSettings.FlashHz(0.5f));
        }

        [Test]
        public void BlinksAlternateOffAndOn()
        {
            Assert.IsTrue(GameSettings.BlinkOff(0.1f, 1));
            Assert.IsFalse(GameSettings.BlinkOff(0.6f, 1));
            Assert.IsTrue(GameSettings.BlinkOff(1.1f, 1));
        }
    }
}
