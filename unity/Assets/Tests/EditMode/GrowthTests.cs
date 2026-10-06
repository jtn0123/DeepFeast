using NUnit.Framework;

namespace DeepFeast.Tests
{
    public class GrowthTests
    {
        static float Legend => Data.Tiers[Data.Tiers.Length - 1].r;

        [Test]
        public void TiersRunFromFryToLegendInSizeOrder()
        {
            Assert.AreEqual("Fry", Data.Tiers[0].name);
            Assert.AreEqual("Legend", Data.Tiers[Data.Tiers.Length - 1].name);
            for (int i = 1; i < Data.Tiers.Length; i++) Assert.Greater(Data.Tiers[i].r, Data.Tiers[i - 1].r, Data.Tiers[i].name);
        }

        [Test]
        public void EatingGrowsTheHeroUpToLegend()
        {
            foreach (var tier in Data.Tiers) Assert.Greater(Data.Grow(tier.r, tier.r * 0.8f), tier.r, tier.name);
        }

        [Test]
        public void YoungFishGrowFaster()
        {
            float fry = Data.Tiers[0].r, predator = Data.Tiers[3].r;
            float fryGain = Data.Grow(fry, fry * 0.8f) / fry, predatorGain = Data.Grow(predator, predator * 0.8f) / predator;
            Assert.Greater(fryGain, predatorGain);
        }

        [Test]
        public void GrowthStopsAtTwiceTheLegend() => Assert.AreEqual(Legend * 2, Data.Grow(Legend * 2, Legend * 1.8f), 1e-3f);

        // The endless deep can go on for as long as the player survives; the hero must still fit the sea.
        [Test]
        public void EndlessEatingNeverPassesTwiceTheLegend()
        {
            float r = Legend;
            for (int i = 0; i < 20000; i++) r = Data.Grow(r, r * Data.EAT);
            Assert.LessOrEqual(r, Legend * 2);
            Assert.Greater(r, Legend * 1.5f);
        }

        [Test]
        public void BiggerFishSwimFasterAndSeeMoreSea()
        {
            Assert.Greater(Data.SpeedFor(Legend), Data.SpeedFor(Data.Tiers[0].r));
            Assert.Less(Data.ZoomFor(Legend), Data.ZoomFor(Data.Tiers[0].r));
            Assert.Greater(Data.ZoomFor(Legend * 2), 0);
        }
    }
}
