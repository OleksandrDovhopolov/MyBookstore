using Game.Configs.Models;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor.Services
{
    public sealed class DecorConfigValidatorTests
    {
        private static DecorConfigValidator Build(
            DecorConfig[] decors = null,
            BookShopConfig[] shops = null,
            BookConfig[] books = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(decors ?? System.Array.Empty<DecorConfig>());
            configs.SetAll(shops ?? System.Array.Empty<BookShopConfig>());
            configs.SetAll(books ?? System.Array.Empty<BookConfig>());
            return new DecorConfigValidator(configs);
        }

        [Test]
        public void ValidConfig_NoErrors()
        {
            var v = Build(
                decors: new[]
                {
                    new DecorConfig
                    {
                        Id = "d1",
                        DisplayName = "Test",
                        PositionType = DecorPositionType.Standing,
                        Size = DecorSize.Small,
                        Rarity = DecorRarity.Common,
                        GenreMultipliers = new[] { new DecorGenreModifier { Genre = "Fantasy", Multiplier = 1.5f } },
                    }
                },
                books: new[] { new BookConfig { Id = "b1", Genre = "Fantasy" } });

            var report = v.Validate();
            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
        }

        [Test]
        public void EmptyId_Errors()
        {
            var v = Build(decors: new[] { new DecorConfig { Id = "", DisplayName = "x", PositionType = DecorPositionType.Standing, Size = DecorSize.Small } });
            var report = v.Validate();
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void DuplicateId_Errors()
        {
            var v = Build(decors: new[]
            {
                new DecorConfig { Id = "dup", DisplayName = "a", PositionType = DecorPositionType.Standing, Size = DecorSize.Small },
                new DecorConfig { Id = "dup", DisplayName = "b", PositionType = DecorPositionType.Standing, Size = DecorSize.Small },
            });
            var report = v.Validate();
            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("Duplicate", string.Join("|", report.Errors));
        }

        [Test]
        public void NegativeMultiplier_Errors()
        {
            var v = Build(decors: new[]
            {
                new DecorConfig
                {
                    Id = "d1", DisplayName = "x",
                    PositionType = DecorPositionType.Standing, Size = DecorSize.Small,
                    GenreMultipliers = new[] { new DecorGenreModifier { Genre = "X", Multiplier = -1f } }
                }
            });
            var report = v.Validate();
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void UnknownGenre_Warns()
        {
            var v = Build(
                decors: new[]
                {
                    new DecorConfig
                    {
                        Id = "d1", DisplayName = "x",
                        PositionType = DecorPositionType.Standing, Size = DecorSize.Small,
                        GenreMultipliers = new[] { new DecorGenreModifier { Genre = "Mystery", Multiplier = 1.5f } }
                    }
                },
                books: new[] { new BookConfig { Id = "b1", Genre = "Fantasy" } });
            var report = v.Validate();
            Assert.IsFalse(report.HasErrors);
            Assert.IsTrue(report.HasWarnings);
        }

        [Test]
        public void DuplicateSlotId_Errors()
        {
            var v = Build(shops: new[]
            {
                new BookShopConfig
                {
                    Id = "shop1",
                    DecorSlots = new[]
                    {
                        new DecorSlot { Id = "s1", PositionType = DecorPositionType.Standing, MaxSize = DecorSize.Small },
                        new DecorSlot { Id = "s1", PositionType = DecorPositionType.Wall,     MaxSize = DecorSize.Small },
                    }
                }
            });
            var report = v.Validate();
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void EmptySlotId_Errors()
        {
            var v = Build(shops: new[]
            {
                new BookShopConfig
                {
                    Id = "shop1",
                    DecorSlots = new[]
                    {
                        new DecorSlot { Id = "", PositionType = DecorPositionType.Standing, MaxSize = DecorSize.Small },
                    }
                }
            });
            var report = v.Validate();
            Assert.IsTrue(report.HasErrors);
        }
    }
}
