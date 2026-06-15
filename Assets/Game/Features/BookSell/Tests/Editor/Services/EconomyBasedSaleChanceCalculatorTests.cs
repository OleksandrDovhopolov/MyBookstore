using System.Collections.Generic;
using System.Text.RegularExpressions;
using Book.Sell.API;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class EconomyBasedSaleChanceCalculatorTests
    {
        private const double Epsilon = 1e-9;

        private static (EconomyBasedSaleChanceCalculator calc, FakeConfigsService configs) Build(
            double baseChance = 0.05,
            double perCopy = 0.05,
            double cap = 0.5,
            double locMultiplier = 1.5,
            IDecorModifierProvider decor = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new EconomyConfig
                {
                    Id = EconomyConfig.SingletonId,
                    BaseSaleChance = baseChance,
                    PerCopyChance = perCopy,
                    CapChance = cap,
                    LocationDemandMultiplier = locMultiplier
                }
            });
            var calc = new EconomyBasedSaleChanceCalculator(configs, decor ?? new NeutralDecorProvider());
            return (calc, configs);
        }

        [Test]
        public void Count_Zero_ReturnsZero()
        {
            var (calc, _) = Build();
            var chance = calc.Compute("Fantasy", 0, SalesTestKit.Location(), null);
            Assert.AreEqual(0d, chance, Epsilon);
        }

        [Test]
        public void Count_One_NonDemandLocation_ReturnsBasePlusPerCopy()
        {
            var (calc, _) = Build(baseChance: 0.05, perCopy: 0.05, cap: 0.5, locMultiplier: 1.5);
            // Genre "Fantasy" is NOT in DemandGenres ("sci-fi") → multiplier is 1.0.
            var chance = calc.Compute("Fantasy", 1, SalesTestKit.Location(demandGenres: new[] { "sci-fi" }), null);
            Assert.AreEqual(0.10d, chance, Epsilon);
        }

        [Test]
        public void Count_One_DemandLocation_AppliesMultiplier()
        {
            var (calc, _) = Build(baseChance: 0.05, perCopy: 0.05, cap: 0.5, locMultiplier: 1.5);
            // Genre "Fantasy" IS in DemandGenres → multiplier is 1.5.
            var chance = calc.Compute("Fantasy", 1, SalesTestKit.Location(demandGenres: new[] { "Fantasy" }), null);
            Assert.AreEqual(0.10d * 1.5d, chance, Epsilon);
        }

        [Test]
        public void Count_LargeEnough_HitsCap_BeforeLocationMultiplier()
        {
            var (calc, _) = Build(baseChance: 0.05, perCopy: 0.05, cap: 0.5, locMultiplier: 1.0);
            // base 0.05 + perCopy 0.05 * 100 = 5.05 → capped at 0.5 → × 1.0 → 0.5.
            var chance = calc.Compute("Fantasy", 100, SalesTestKit.Location(demandGenres: new[] { "sci-fi" }), null);
            Assert.AreEqual(0.5d, chance, Epsilon);
        }

        [Test]
        public void Result_IsClampedAt_One_WhenMultipliersOverflow()
        {
            // cap 0.8, locMult 1.5 → 1.2 → clamp to 1.0.
            var (calc, _) = Build(baseChance: 0.05, perCopy: 0.05, cap: 0.8, locMultiplier: 1.5);
            var chance = calc.Compute("Fantasy", 100, SalesTestKit.Location(demandGenres: new[] { "Fantasy" }), null);
            Assert.AreEqual(1.0d, chance, Epsilon);
        }

        [Test]
        public void NullDemandGenres_TreatedAsNoDemand()
        {
            var (calc, _) = Build();
            var location = new LocationConfig { Id = "loc", DisplayName = "loc", DemandGenres = null, DemandTags = null };
            var chance = calc.Compute("Fantasy", 1, location, null);
            Assert.AreEqual(0.10d, chance, Epsilon);
        }

        [Test]
        public void DecorMultiplier_IsApplied()
        {
            var decor = new ConstantDecorProvider(2f);
            var (calc, _) = Build(decor: decor);
            var chance = calc.Compute("Fantasy", 1, SalesTestKit.Location(demandGenres: new[] { "sci-fi" }), null);
            // f(count) = 0.10, locMod = 1.0, decorMod = 2.0 → 0.20.
            Assert.AreEqual(0.20d, chance, Epsilon);
        }

        [Test]
        public void MissingEconomyConfig_ReturnsZero()
        {
            // No SetAll<EconomyConfig> — Get returns null. The calculator logs an error once and
            // falls back to chance 0; expect that error so the test runner doesn't fail on the log.
            LogAssert.Expect(LogType.Error, new Regex(@"\[Sales\.Chance\].*EconomyConfig.*missing"));

            var configs = new FakeConfigsService();
            var calc = new EconomyBasedSaleChanceCalculator(configs, new NeutralDecorProvider());
            var chance = calc.Compute("Fantasy", 5, SalesTestKit.Location(), null);
            Assert.AreEqual(0d, chance, Epsilon);
        }

        private sealed class ConstantDecorProvider : IDecorModifierProvider
        {
            private readonly float _value;
            public ConstantDecorProvider(float value) => _value = value;
            public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds) => _value;
        }

        // Local fake that replaces the deleted NoopDecorModifierProvider stub from the BookSell impl.
        private sealed class NeutralDecorProvider : IDecorModifierProvider
        {
            public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds) => 1f;
        }
    }
}
