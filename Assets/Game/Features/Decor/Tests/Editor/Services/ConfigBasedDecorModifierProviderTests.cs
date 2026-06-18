using System;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor.Services
{
    public sealed class ConfigBasedDecorModifierProviderTests
    {
        private const float Epsilon = 1e-5f;

        private static (ConfigBasedDecorModifierProvider provider, FakeConfigsService configs) Build(
            params DecorConfig[] decors)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(decors);
            return (new ConfigBasedDecorModifierProvider(configs), configs);
        }

        private static DecorConfig Decor(string id, params (string genre, float mult)[] mods)
        {
            var mm = new DecorGenreModifier[mods.Length];
            for (var i = 0; i < mods.Length; i++)
                mm[i] = new DecorGenreModifier { Genre = mods[i].genre, Multiplier = mods[i].mult };
            return new DecorConfig
            {
                Id = id,
                DisplayName = id,
                PositionType = DecorPositionType.Standing,
                Size = DecorSize.Small,
                GenreMultipliers = mm,
            };
        }

        [Test]
        public void NoActiveDecor_ReturnsOne()
        {
            var (p, _) = Build();
            Assert.AreEqual(1f, p.GetGenreMultiplier("Fantasy", Array.Empty<string>()), Epsilon);
        }

        [Test]
        public void EmptyGenre_ReturnsOne()
        {
            var (p, _) = Build(Decor("d1", ("Fantasy", 1.5f)));
            Assert.AreEqual(1f, p.GetGenreMultiplier("", new[] { "d1" }), Epsilon);
        }

        [Test]
        public void SingleMatchingDecor_AppliesMultiplier()
        {
            var (p, _) = Build(Decor("d1", ("Fantasy", 1.5f)));
            Assert.AreEqual(1.5f, p.GetGenreMultiplier("Fantasy", new[] { "d1" }), Epsilon);
        }

        [Test]
        public void NonMatchingGenre_ReturnsOne()
        {
            var (p, _) = Build(Decor("d1", ("Fantasy", 1.5f)));
            Assert.AreEqual(1f, p.GetGenreMultiplier("Crime", new[] { "d1" }), Epsilon);
        }

        [Test]
        public void MultipleDecors_Multiplicative()
        {
            var (p, _) = Build(
                Decor("d1", ("Fantasy", 1.5f)),
                Decor("d2", ("Fantasy", 1.2f)));
            Assert.AreEqual(1.5f * 1.2f, p.GetGenreMultiplier("Fantasy", new[] { "d1", "d2" }), Epsilon);
        }

        [Test]
        public void SoftCapMax_ClampsAt3()
        {
            // 1.6 * 1.6 * 1.6 = 4.096 -> clamped to 3.0
            var (p, _) = Build(
                Decor("d1", ("Fantasy", 1.6f)),
                Decor("d2", ("Fantasy", 1.6f)),
                Decor("d3", ("Fantasy", 1.6f)));
            Assert.AreEqual(3.0f, p.GetGenreMultiplier("Fantasy", new[] { "d1", "d2", "d3" }), Epsilon);
        }

        [Test]
        public void SoftCapMin_ClampsAtPoint1()
        {
            // 0.05 -> clamped to 0.1
            var (p, _) = Build(Decor("d1", ("Crime", 0.05f)));
            Assert.AreEqual(0.1f, p.GetGenreMultiplier("Crime", new[] { "d1" }), Epsilon);
        }

        [Test]
        public void NegativeAndPositiveOnDifferentGenres_DoNotInterfere()
        {
            var (p, _) = Build(Decor("d1", ("Fantasy", 1.5f), ("Crime", 0.7f)));
            Assert.AreEqual(1.5f, p.GetGenreMultiplier("Fantasy", new[] { "d1" }), Epsilon);
            Assert.AreEqual(0.7f, p.GetGenreMultiplier("Crime", new[] { "d1" }), Epsilon);
        }

        [Test]
        public void MissingDecorConfig_SilentlyIgnored()
        {
            var (p, _) = Build(Decor("d1", ("Fantasy", 1.5f)));
            // 'd2' is not in configs — silently skipped
            Assert.AreEqual(1.5f, p.GetGenreMultiplier("Fantasy", new[] { "d1", "d2" }), Epsilon);
        }
    }
}
