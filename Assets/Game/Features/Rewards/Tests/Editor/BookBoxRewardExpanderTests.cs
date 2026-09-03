using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using Game.Rewards.Services;
using Game.Rewards.Tests.Editor.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Rewards.Tests.Editor
{
    public sealed class BookBoxRewardExpanderTests
    {
        private static BookConfig Book(string id, string genre, float rarity) =>
            new BookConfig
            {
                Id = id,
                TitleKey = $"book.{id}.title",
                AuthorKey = $"book.{id}.author",
                Genres = new[] { genre },
                RarityWeight = rarity,
                Qualities = new string[0]
            };

        private static (BookBoxRewardExpander svc, FakeConfigsService cfg, FakeInventoryService inv, FakeRewardRandom rng) Build(
            IReadOnlyList<BookConfig> pool)
        {
            var cfg = new FakeConfigsService().Seed(pool);
            var inv = new FakeInventoryService();
            var rng = new FakeRewardRandom();
            return (new BookBoxRewardExpander(cfg, inv, rng), cfg, inv, rng);
        }

        [Test]
        public void CanExpand_BookBoxId_True()
        {
            var (svc, _, _, _) = Build(new BookConfig[0]);
            Assert.IsTrue(svc.CanExpand(new RewardSpec("book_box_common_15", new RewardItem[0])));
            Assert.IsTrue(svc.CanExpand(new RewardSpec("book_box_anything", new RewardItem[0])));
        }

        [Test]
        public void CanExpand_NonBookBoxId_False()
        {
            var (svc, _, _, _) = Build(new BookConfig[0]);
            Assert.IsFalse(svc.CanExpand(new RewardSpec("decor_vintage_globe", new RewardItem[0])));
            Assert.IsFalse(svc.CanExpand(new RewardSpec("anything_else", new RewardItem[0])));
        }

        [Test]
        public void Expand_Common15_Returns15ItemsAllBookCategory()
        {
            // 20-book pool, mixed genres + rarities. With default RNG (NextDouble=0.0) the expander
            // always picks the first remaining candidate, yielding the pool in order.
            var pool = new List<BookConfig>();
            for (var i = 0; i < 20; i++)
                pool.Add(Book($"book_{i:D3}", "Drama", 0.3f + i * 0.02f));

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_common_15", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("book_box_common_15", result.Id);
            Assert.AreEqual(15, result.Items.Count);
            for (var i = 0; i < result.Items.Count; i++)
            {
                Assert.AreEqual(RewardKind.InventoryItem, result.Items[i].Kind);
                Assert.AreEqual(InventoryCategories.Book, result.Items[i].Category);
                Assert.AreEqual(1, result.Items[i].Amount);
            }

            // Without replacement → all 15 ids are distinct.
            var distinct = result.Items.Select(it => it.Id).Distinct().Count();
            Assert.AreEqual(15, distinct);
        }

        [Test]
        public void Expand_Rare8_OnlyHighRarityBooks()
        {
            // 5 rare (>=0.6) + 5 common (<0.6). Result must contain only rare ones.
            var pool = new List<BookConfig>
            {
                Book("rare_1", "Drama", 0.7f),
                Book("rare_2", "Drama", 0.8f),
                Book("rare_3", "Drama", 0.65f),
                Book("rare_4", "Drama", 0.9f),
                Book("rare_5", "Drama", 0.6f),
                Book("common_1", "Drama", 0.2f),
                Book("common_2", "Drama", 0.3f),
                Book("common_3", "Drama", 0.4f),
                Book("common_4", "Drama", 0.5f),
                Book("common_5", "Drama", 0.55f),
            };

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_rare_8", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            // Pool of 5 rare → rolls clamp to 5.
            Assert.AreEqual(5, result.Items.Count, "Rolls clamp to pool size for without-replacement sampling.");
            foreach (var item in result.Items)
                Assert.IsTrue(item.Id.StartsWith("rare_"), $"Expected rare book, got {item.Id}");
        }

        [Test]
        public void Expand_GenreFantasy8_FiltersByGenreOnly()
        {
            // Pool has two Fantasy books. Rolls clamp to the matching pool size.
            var pool = new List<BookConfig>
            {
                Book("fantasy_first", "Fantasy", 0.7f),
                Book("fantasy_second", "Fantasy", 0.6f),
                Book("drama", "Drama", 0.5f),
                Book("crime", "Crime", 0.8f),
            };

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_genre_fantasy_8", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(2, result.Items.Count);
            Assert.AreEqual("fantasy_first", result.Items[0].Id);
            Assert.AreEqual("fantasy_second", result.Items[1].Id);
        }

        [Test]
        public void Expand_GenreClassic8_ReturnsEightBooksFromGenre()
        {
            var pool = new List<BookConfig>();
            for (var i = 0; i < 10; i++)
                pool.Add(Book($"classic_{i:D2}", "Classic", 0.4f + i * 0.01f));
            for (var i = 0; i < 5; i++)
                pool.Add(Book($"drama_{i:D2}", "Drama", 0.7f));

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_genre_classic_8", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(8, result.Items.Count);
            foreach (var item in result.Items)
                StringAssert.StartsWith("classic_", item.Id);
        }

        [Test]
        public void Expand_GenreHeartfelt_FiltersByDramaOnly()
        {
            var pool = new List<BookConfig>
            {
                Book("drama_first", "Drama", 0.7f),
                Book("drama_second", "Drama", 0.8f),
                Book("classic", "Classic", 0.9f),
                Book("fantasy", "Fantasy", 0.6f),
            };

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_genre_heartfelt_1", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("drama_first", result.Items[0].Id);
        }

        [Test]
        public void Expand_UnknownBoxId_ReturnsEmptySpec()
        {
            var (svc, _, _, _) = Build(new BookConfig[0]);
            var spec = new RewardSpec("book_box_unknown_thing", new RewardItem[0]);

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(spec.Id, result.Id);
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Expand_EmptyPoolMatchingFilter_ReturnsEmptySpec()
        {
            // book_box_rare_8 requires RarityWeight >= 0.6 — pool has none.
            var pool = new List<BookConfig>
            {
                Book("common_a", "Drama", 0.2f),
                Book("common_b", "Drama", 0.4f),
            };

            var (svc, _, _, _) = Build(pool);
            var spec = new RewardSpec("book_box_rare_8", new RewardItem[0]);

            // An empty pool means the player paid and got nothing, so it logs an error, not a warning.
            // The message must name the "no book matches the rule" cause — that is the content bug the
            // build gate (BookBoxPoolValidator) exists to catch, and it needs a different fix than the
            // "everything already owned" case below.
            LogAssert.Expect(LogType.Error, new Regex("no book in the catalog matches the rule"));

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Expand_ExcludesOwnedBooks_FromPool()
        {
            // 10-book pool. Player owns 7 of them — expand common_15 must roll only from the 3
            // remaining and return at most 3 items.
            var pool = new List<BookConfig>();
            for (var i = 0; i < 10; i++)
                pool.Add(Book($"book_{i:D2}", "Drama", 0.3f + i * 0.02f));

            var (svc, _, inv, _) = Build(pool);

            // Seed first 7 ids into inventory.
            for (var i = 0; i < 7; i++)
                inv.AddAsync($"book_{i:D2}", InventoryCategories.Book, 1, CancellationToken.None)
                   .GetAwaiter().GetResult();

            var spec = new RewardSpec("book_box_common_15", new RewardItem[0]);
            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(3, result.Items.Count, "Pool of 3 unowned → 3 books delivered (clamped).");
            foreach (var item in result.Items)
            {
                Assert.IsTrue(item.Id == "book_07" || item.Id == "book_08" || item.Id == "book_09",
                    $"Got owned id {item.Id}; expected only unowned book_07/08/09.");
            }
        }

        [Test]
        public void Expand_AllBooksOwned_ReturnsEmptySpec()
        {
            // 5-book pool. Player owns all 5 — pool collapses to 0 → empty spec.
            var pool = new List<BookConfig>
            {
                Book("a", "Drama", 0.7f),
                Book("b", "Drama", 0.7f),
                Book("c", "Drama", 0.7f),
                Book("d", "Drama", 0.7f),
                Book("e", "Drama", 0.7f),
            };

            var (svc, _, inv, _) = Build(pool);
            foreach (var b in pool)
                inv.AddAsync(b.Id, InventoryCategories.Book, 1, CancellationToken.None)
                   .GetAwaiter().GetResult();

            var spec = new RewardSpec("book_box_rare_8", new RewardItem[0]);

            // Same empty result, different cause: the rule does match books, the player just owns them all.
            // This one is a legitimate end-state (Unique books, collection complete), not a data bug.
            LogAssert.Expect(LogType.Error, new Regex("already owns all of them"));

            var result = svc.ExpandAsync(spec, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(spec.Id, result.Id, "Granted spec id matches request even when empty.");
            Assert.AreEqual(0, result.Items.Count);
        }
    }
}
