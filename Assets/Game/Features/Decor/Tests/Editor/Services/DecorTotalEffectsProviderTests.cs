using System;
using System.Linq;
using Book.Sell.API;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor.Services
{
    public sealed class DecorTotalEffectsProviderTests
    {
        private const float Epsilon = 1e-4f;

        [Test]
        public void NeutralMultiplier_IsOmitted()
        {
            var provider = Build(Decor("d1", traffic: 0f));

            var effects = provider.GetTotalEffects(new[] { "d1" });

            Assert.AreEqual(0, effects.Count);
        }

        [Test]
        public void GenreMultiplier_IsReturnedAsSignedPercent()
        {
            var provider = Build(Decor("d1", ("Fantasy", 1.5f)));

            var effect = provider.GetTotalEffects(new[] { "d1" }).Single();

            Assert.AreEqual(DecorEffectKind.GenreSaleChance, effect.Kind);
            Assert.AreEqual("Fantasy", effect.Subject);
            Assert.AreEqual(50f, effect.Percent, Epsilon);
        }

        [Test]
        public void MultipleDecors_UseModifierProviderMultiplicationAndClamp()
        {
            var provider = Build(
                Decor("d1", ("Fantasy", 1.6f)),
                Decor("d2", ("Fantasy", 1.6f)),
                Decor("d3", ("Fantasy", 1.6f)));

            var effect = provider.GetTotalEffects(new[] { "d1", "d2", "d3" }).Single();

            Assert.AreEqual(200f, effect.Percent, Epsilon);
        }

        [Test]
        public void Traffic_IsSummedLikeDecorTrafficContributor()
        {
            var provider = Build(
                Decor("d1", traffic: 0.10f),
                Decor("d2", traffic: 0.05f));

            var effect = provider.GetTotalEffects(new[] { "d1", "d2" })
                .Single(e => e.Kind == DecorEffectKind.CustomerTraffic);

            Assert.AreEqual(15f, effect.Percent, Epsilon);
        }

        private static DecorTotalEffectsProvider Build(params DecorConfig[] decors)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(decors);
            IDecorModifierProvider modifiers = new ConfigBasedDecorModifierProvider(configs);
            return new DecorTotalEffectsProvider(configs, modifiers);
        }

        private static DecorConfig Decor(string id, params (string genre, float mult)[] mods)
            => Decor(id, traffic: 0f, mods);

        private static DecorConfig Decor(string id, float traffic, params (string genre, float mult)[] mods)
        {
            var genreMultipliers = new DecorGenreModifier[mods.Length];
            for (var i = 0; i < mods.Length; i++)
            {
                genreMultipliers[i] = new DecorGenreModifier
                {
                    Genre = mods[i].genre,
                    Multiplier = mods[i].mult
                };
            }

            return new DecorConfig
            {
                Id = id,
                DisplayName = id,
                PositionType = DecorPositionType.Standing,
                Size = DecorSize.Small,
                GenreMultipliers = genreMultipliers,
                CustomerTrafficPercentDelta = traffic
            };
        }
    }
}
