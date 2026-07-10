using Book.Sell.API;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>Tests for <see cref="LocationTrafficContributor"/> reading LocationConfig.CustomerTrafficPercentDelta.</summary>
    public sealed class LocationTrafficContributorTests
    {
        private static (LocationTrafficContributor contributor, CustomerTrafficAccumulator acc) Make(params LocationConfig[] locations)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(locations);
            return (new LocationTrafficContributor(configs), new CustomerTrafficAccumulator());
        }

        [Test]
        public void EmitsConfiguredPercentDelta()
        {
            var (contributor, acc) = Make(new LocationConfig { Id = "loc", CustomerTrafficPercentDelta = 0.2f });
            contributor.Contribute(new CustomerTrafficContext(1, "loc", null), acc);
            Assert.AreEqual(0.2f, acc.TotalPercentDelta(), 0.0001f);
        }

        [Test]
        public void NeutralLocation_ContributesNothing()
        {
            var (contributor, acc) = Make(new LocationConfig { Id = "loc", CustomerTrafficPercentDelta = 0f });
            contributor.Contribute(new CustomerTrafficContext(1, "loc", null), acc);
            Assert.AreEqual(0, acc.Contributions.Count);
        }

        [Test]
        public void UnknownOrNullLocation_ContributesNothing()
        {
            var (contributor, acc) = Make(new LocationConfig { Id = "loc", CustomerTrafficPercentDelta = 0.5f });
            contributor.Contribute(new CustomerTrafficContext(1, "missing", null), acc);
            contributor.Contribute(new CustomerTrafficContext(1, null, null), acc);
            Assert.AreEqual(0, acc.Contributions.Count);
        }
    }
}
