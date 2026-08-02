using Game.Configs.Models;
using Game.Inventory.API;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using Game.Shop.API;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor.Services
{
    public sealed class DecorConfigValidatorTests
    {
        private static DecorConfigValidator Build(
            DecorConfig[] decors = null,
            BookShopConfig[] shops = null,
            BookConfig[] books = null,
            ShopConfig[] lots = null,
            QuestConfig[] quests = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(decors ?? System.Array.Empty<DecorConfig>());
            configs.SetAll(shops ?? System.Array.Empty<BookShopConfig>());
            configs.SetAll(books ?? System.Array.Empty<BookConfig>());
            configs.SetAll(lots ?? System.Array.Empty<ShopConfig>());
            configs.SetAll(quests ?? System.Array.Empty<QuestConfig>());
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
                        GenreMultipliers = new[] { new DecorGenreModifier { Genre = "Fantasy", Multiplier = 1.5f } },
                    }
                },
                books: new[] { new BookConfig { Id = "b1", Genres = new[] { "Fantasy" } } });

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
                books: new[] { new BookConfig { Id = "b1", Genres = new[] { "Fantasy" } } });
            var report = v.Validate();
            Assert.IsFalse(report.HasErrors);
            Assert.IsTrue(report.HasWarnings);
            StringAssert.Contains("unknown genre", string.Join("|", report.Warnings));
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

        [Test]
        public void ShopLotGrantsUnknownDecor_Errors()
        {
            var v = Build(lots: new[] { Lot("lot_ghost", "ghost") });

            var report = v.Validate();

            Assert.IsTrue(report.HasErrors);
            var errors = string.Join("|", report.Errors);
            StringAssert.Contains("Shop lot 'lot_ghost'", errors);
            StringAssert.Contains("ghost", errors);
        }

        [Test]
        public void QuestGrantsUnknownDecor_Errors()
        {
            var v = Build(quests: new[] { Quest("quest_ghost", "ghost") });

            var report = v.Validate();

            Assert.IsTrue(report.HasErrors);
            var errors = string.Join("|", report.Errors);
            StringAssert.Contains("Quest 'quest_ghost'", errors);
            StringAssert.Contains("ghost", errors);
        }

        [Test]
        public void NonDecorCategoryRewardItem_Ignored()
        {
            var v = Build(lots: new[]
            {
                new ShopConfig
                {
                    Id = "book_lot",
                    StorefrontId = NewspaperShopLotIds.StorefrontBooks,
                    RewardItems = new[]
                    {
                        new RewardItemData { Id = "missing_book", Category = InventoryCategories.Book, Amount = 1 }
                    }
                }
            });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
        }

        [Test]
        public void DecorGrantedByShopLot_NoUnreachableWarning()
        {
            var v = Build(
                decors: new[] { Decor("d1") },
                lots: new[] { Lot("decor_lot", "d1") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            var warnings = string.Join("|", report.Warnings);
            Assert.IsFalse(warnings.Contains("unreachable"), warnings);
        }

        [Test]
        public void DecorGrantedByQuestOnly_NoUnreachableWarning()
        {
            var v = Build(
                decors: new[] { Decor("harper_castle_donation_box") },
                quests: new[] { Quest("sand_inspiration", "harper_castle_donation_box") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            var warnings = string.Join("|", report.Warnings);
            Assert.IsFalse(warnings.Contains("unreachable"), warnings);
        }

        [Test]
        public void DecorWithNoLotOrQuest_Warns()
        {
            var v = Build(decors: new[] { Decor("d1") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            Assert.IsTrue(report.HasWarnings);
            StringAssert.Contains("unreachable", string.Join("|", report.Warnings));
        }

        [Test]
        public void DecorLotInWrongStorefront_Warns()
        {
            var v = Build(
                decors: new[] { Decor("d1") },
                lots: new[] { Lot("wrong_storefront", "d1", "classic.decor") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            var warnings = string.Join("|", report.Warnings);
            StringAssert.Contains("wrong_storefront", warnings);
            StringAssert.Contains("expected 'newspaper.decor'", warnings);
        }

        [Test]
        public void DecorReferencedWithDifferentCase_NoWarning()
        {
            var v = Build(
                decors: new[] { Decor("Old_Lamp") },
                lots: new[] { Lot("decor_lot", "old_lamp") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            var warnings = string.Join("|", report.Warnings);
            Assert.IsFalse(warnings.Contains("unreachable"), warnings);
        }

        [Test]
        public void NullRewardItems_NoThrow()
        {
            var v = Build(lots: new[]
            {
                new ShopConfig { Id = "null_items", StorefrontId = NewspaperShopLotIds.StorefrontDecor, RewardItems = null },
                new ShopConfig { Id = "null_item", StorefrontId = NewspaperShopLotIds.StorefrontDecor, RewardItems = new RewardItemData[] { null } }
            });

            Assert.DoesNotThrow(() => v.Validate());
        }

        [Test]
        public void NullQuestRewards_NoThrow()
        {
            var v = Build(quests: new[]
            {
                new QuestConfig { Id = "null_rewards", Rewards = null },
                new QuestConfig { Id = "null_reward", Rewards = new QuestRewardConfig[] { null } }
            });

            Assert.DoesNotThrow(() => v.Validate());
        }

        [Test]
        public void SameDecorGrantedByTwoLots_NoDuplicateError()
        {
            var v = Build(
                decors: new[] { Decor("d1") },
                lots: new[] { Lot("decor_lot_a", "d1"), Lot("decor_lot_b", "d1") });

            var report = v.Validate();

            Assert.IsFalse(report.HasErrors, "got errors: " + report.FormatErrors());
            var warnings = string.Join("|", report.Warnings);
            Assert.IsFalse(warnings.Contains("unreachable"), warnings);
        }

        private static DecorConfig Decor(string id) =>
            new DecorConfig
            {
                Id = id,
                DisplayName = id,
                PositionType = DecorPositionType.Standing,
                Size = DecorSize.Small,
                GenreMultipliers = System.Array.Empty<DecorGenreModifier>()
            };

        private static ShopConfig Lot(
            string lotId,
            string decorId,
            string storefrontId = NewspaperShopLotIds.StorefrontDecor) =>
            new ShopConfig
            {
                Id = lotId,
                StorefrontId = storefrontId,
                RewardItems = new[]
                {
                    new RewardItemData { Id = decorId, Category = InventoryCategories.Decor, Amount = 1 }
                }
            };

        private static QuestConfig Quest(string questId, string decorId) =>
            new QuestConfig
            {
                Id = questId,
                Rewards = new[]
                {
                    new QuestRewardConfig
                    {
                        Kind = "InventoryItem",
                        Id = decorId,
                        Category = InventoryCategories.Decor,
                        Amount = 1
                    }
                }
            };
    }
}
