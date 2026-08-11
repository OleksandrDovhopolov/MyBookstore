using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Rewards.Editor;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Rewards.Tests.Editor
{
    /// <summary>
    /// Covers the check that would have caught the swapped-catalog regression: a books file with no
    /// <c>rarityWeight</c> leaves every book on the 0.5 C# default, so <c>book_box_rare_8</c>
    /// (<c>RarityWeight &gt;= 0.6</c>) matches nothing and the lot silently takes gold for no books.
    /// <para>
    /// Writes throwaway configs into a temp dir and points the validator at it, so the tests never depend
    /// on the shipping content — those numbers change with every content edit.
    /// </para>
    /// </summary>
    public sealed class BookBoxPoolValidatorTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "bookbox_validator_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        // Written as raw JSON (not typed DTOs) so a test can omit a field entirely — which is the whole
        // point: the regression came from an absent field, not a wrong one.
        private void WriteBooks(params string[] booksJson)
            => File.WriteAllText(Path.Combine(_dir, "books.json"), "[" + string.Join(",", booksJson) + "]");

        private void WriteShop(params string[] rewardIds)
            => File.WriteAllText(Path.Combine(_dir, "shop.json"), JsonConvert.SerializeObject(
                rewardIds.Select((id, i) => new { id = $"lot_{i}", storefrontId = "s", rewardId = id })));

        private static string BookJson(string id, string genre, double? rarity)
            => rarity.HasValue
                ? $@"{{""id"":""{id}"",""title"":""{id}"",""genres"":[""{genre}""],""rarityWeight"":{rarity.Value}}}"
                : $@"{{""id"":""{id}"",""title"":""{id}"",""genres"":[""{genre}""]}}";

        private static string Errors(BookBoxPoolValidationReport report)
            => string.Join(" | ", report.Errors);

        [Test]
        public void CatalogWithoutRarityWeight_FlagsRareBoxAsUnfillable()
        {
            // The real regression: no rarityWeight anywhere → every book defaults to 0.5 → rare box (>=0.6)
            // matches zero books.
            var books = new List<string>();
            for (var i = 0; i < 30; i++)
                books.Add(BookJson($"b{i:D2}", i % 2 == 0 ? "Drama" : "Fantasy", rarity: null));

            WriteBooks(books.ToArray());
            WriteShop("book_box_common_15", "book_box_rare_8");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsTrue(report.HasErrors, "A rare box that matches no book must fail the gate.");
            Assert.AreEqual(0, report.MatchesByBox["book_box_rare_8"]);
            StringAssert.Contains("book_box_rare_8", Errors(report));
            StringAssert.Contains("matches 0 of 30 books", Errors(report));
            Assert.AreEqual(1, report.Errors.Count,
                "Only the sold-but-unfillable box fails; unsold rules must stay quiet. " + Errors(report));
        }

        [Test]
        public void CatalogWithRarityWeight_RareBoxPasses()
        {
            var books = new List<string>();
            for (var i = 0; i < 30; i++)
                books.Add(BookJson($"b{i:D2}", i % 2 == 0 ? "Drama" : "Fantasy", rarity: 0.9));

            WriteBooks(books.ToArray());
            WriteShop("book_box_common_15", "book_box_rare_8");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsFalse(report.HasErrors, Errors(report));
            Assert.AreEqual(30, report.MatchesByBox["book_box_rare_8"]);
        }

        [Test]
        public void FewerMatchesThanRolls_IsReportedAsUnderDelivery()
        {
            // 4 books, common box rolls 15 → even a fresh save cannot fill it.
            WriteBooks(
                BookJson("b1", "Drama", 0.9), BookJson("b2", "Drama", 0.9),
                BookJson("b3", "Fantasy", 0.9), BookJson("b4", "Fantasy", 0.9));
            WriteShop("book_box_common_15");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("under-delivers", Errors(report));
        }

        [Test]
        public void ShopLotWithUnknownBoxId_IsAnError()
        {
            // Several genre rules match nothing here, but no lot sells them, so they must not add errors.
            // Only the unknown reward id may fail here.
            var books = Enumerable.Range(0, 20).Select(i => BookJson($"b{i:D2}", "Drama", 0.9)).ToArray();
            WriteBooks(books);
            WriteShop("book_box_common_15", "book_box_does_not_exist");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("book_box_does_not_exist", Errors(report));
            StringAssert.Contains("no BookBoxPoolRules rule", Errors(report));
            Assert.AreEqual(1, report.Errors.Count,
                "An unsold rule matching 0 books must not fail the gate. " + Errors(report));
        }

        [Test]
        public void RuleNoLotSells_WithMatches_IsListedButNotAnError()
        {
            // Only the common box is sold; the genre boxes still have books, so they are noted, not failed.
            var books = new List<string>();
            for (var i = 0; i < 20; i++)
                books.Add(BookJson($"b{i:D2}", i % 2 == 0 ? "Drama" : "Fantasy", rarity: 0.9));

            WriteBooks(books.ToArray());
            WriteShop("book_box_common_15");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsFalse(report.HasErrors, Errors(report));
            CollectionAssert.Contains(report.UnsoldBoxes, "book_box_rare_8");
            CollectionAssert.Contains(report.UnsoldBoxes, "book_box_genre_drama_8");
        }

        [Test]
        public void MissingBooksFile_IsReported_NotThrown()
        {
            WriteShop("book_box_common_15");

            var report = BookBoxPoolValidator.Validate(_dir);

            Assert.IsTrue(report.HasErrors);
            StringAssert.Contains("File not found", Errors(report));
        }
    }
}
