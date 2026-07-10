using System;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    /// <summary>
    /// Tests for <see cref="CustomerTrafficResolver"/>: hard override (exact, bypasses modifiers + clamp),
    /// percent pipeline (summed, not compounded), default fallback, clamp, and the empty-contributor case.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficResolverTests
    {
        private sealed class PercentContributor : ICustomerTrafficContributor
        {
            private readonly float _percentDelta;
            public PercentContributor(float percentDelta) => _percentDelta = percentDelta;
            public void Contribute(CustomerTrafficContext ctx, CustomerTrafficAccumulator acc)
                => acc.Add(_percentDelta, "test");
        }

        private static SalesSessionSetup Setup(int day)
            => new SalesSessionSetup(day, "loc", Array.Empty<string>(), Array.Empty<string>());

        private static FakeConfigsService ConfigsWith(params DayConfig[] days)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(days);
            return configs;
        }

        [Test]
        public void HardOverride_ReturnsExactCount_IgnoringModifiersAndClamp()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d1", DayIndex = 1, CustomerCount = 3, ApplyModifiers = false });
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 10, MaxCustomerCount = 50 },
                configs,
                new ICustomerTrafficContributor[] { new PercentContributor(0.5f) });

            var result = resolver.Resolve(Setup(1), new SalesTuning());

            Assert.IsTrue(result.IsHardOverride);
            Assert.AreEqual(3, result.FinalCount); // +50% ignored, Min=10 clamp ignored
        }

        [Test]
        public void ModifiersDay_AppliesSummedPercentDeltas_RoundedAwayFromZero()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d2", DayIndex = 2, CustomerCount = 10, ApplyModifiers = true });
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 0, MaxCustomerCount = 100 },
                configs,
                new ICustomerTrafficContributor[] { new PercentContributor(0.20f), new PercentContributor(-0.05f) });

            var result = resolver.Resolve(Setup(2), new SalesTuning());

            Assert.IsFalse(result.IsHardOverride);
            Assert.AreEqual(12, result.FinalCount); // 10 * (1 + 0.15) = 11.5 -> 12
        }

        [Test]
        public void TwoPercentSources_AreSummed_NotCompounded()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d", DayIndex = 1, CustomerCount = 100, ApplyModifiers = true });
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 0, MaxCustomerCount = 1000 },
                configs,
                new ICustomerTrafficContributor[] { new PercentContributor(-0.30f), new PercentContributor(-0.30f) });

            var result = resolver.Resolve(Setup(1), new SalesTuning());

            Assert.AreEqual(40, result.FinalCount); // summed: 100*(1-0.6)=40  (compounded would be 49)
        }

        [Test]
        public void UnknownDay_UsesDefaultCount_WithModifiersOn()
        {
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { DefaultCustomerCount = 8, MinCustomerCount = 0, MaxCustomerCount = 100 },
                ConfigsWith(),
                new ICustomerTrafficContributor[] { new PercentContributor(0.25f) });

            var result = resolver.Resolve(Setup(99), new SalesTuning());

            Assert.AreEqual(10, result.FinalCount); // 8 * 1.25 = 10
        }

        [Test]
        public void AbsentApplyModifiers_DefaultsToModifiersOn()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d", DayIndex = 1, CustomerCount = 10 }); // ApplyModifiers null
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 0, MaxCustomerCount = 100 },
                configs,
                new ICustomerTrafficContributor[] { new PercentContributor(0.10f) });

            var result = resolver.Resolve(Setup(1), new SalesTuning());

            Assert.IsFalse(result.IsHardOverride);
            Assert.AreEqual(11, result.FinalCount);
        }

        [Test]
        public void FinalCount_ClampsToMax_OnNonHardDays()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d", DayIndex = 1, CustomerCount = 10, ApplyModifiers = true });
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 0, MaxCustomerCount = 5 },
                configs,
                new ICustomerTrafficContributor[] { new PercentContributor(1.0f) }); // 10*2=20 -> clamp 5

            Assert.AreEqual(5, resolver.Resolve(Setup(1), new SalesTuning()).FinalCount);
        }

        [Test]
        public void EmptyContributorList_ReturnsBaseline()
        {
            var configs = ConfigsWith(new DayConfig { Id = "d", DayIndex = 1, CustomerCount = 7, ApplyModifiers = true });
            var resolver = new CustomerTrafficResolver(
                new SalesTrafficSettings { MinCustomerCount = 0, MaxCustomerCount = 100 },
                configs,
                Array.Empty<ICustomerTrafficContributor>());

            Assert.AreEqual(7, resolver.Resolve(Setup(1), new SalesTuning()).FinalCount);
        }
    }
}
