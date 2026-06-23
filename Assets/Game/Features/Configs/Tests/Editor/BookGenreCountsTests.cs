using System.Collections.Generic;
using System.Linq;
using Game.Configs.Models;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class BookGenreCountsTests
    {
        [Test]
        public void Normalize_Null_ReturnsAllGenresInEnumOrderWithZeroCounts()
        {
            var normalized = BookGenreCounts.Normalize(null);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Classic",
                    "Crime",
                    "Drama",
                    "Fact",
                    "Fantasy",
                    "Kids",
                    "Travel"
                },
                normalized.Keys.ToArray());

            Assert.That(normalized.Values.All(value => value == 0), Is.True);
        }

        [Test]
        public void Normalize_PartialInput_FillsMissingGenresWithZero()
        {
            var normalized = BookGenreCounts.Normalize(new Dictionary<string, int>
            {
                ["Fantasy"] = 3,
                ["Crime"] = 1
            });

            Assert.AreEqual(0, normalized["Classic"]);
            Assert.AreEqual(1, normalized["Crime"]);
            Assert.AreEqual(0, normalized["Drama"]);
            Assert.AreEqual(0, normalized["Fact"]);
            Assert.AreEqual(3, normalized["Fantasy"]);
            Assert.AreEqual(0, normalized["Kids"]);
            Assert.AreEqual(0, normalized["Travel"]);
        }

        [Test]
        public void Normalize_MixedCaseKeys_MergesIntoCanonicalGenreEntry()
        {
            var normalized = BookGenreCounts.Normalize(new Dictionary<string, int>
            {
                ["Fantasy"] = 1,
                ["fantasy"] = 2,
                ["FANTASY"] = 3
            });

            Assert.AreEqual(6, normalized["Fantasy"]);
            Assert.AreEqual(7, normalized.Count);
        }

        [Test]
        public void Normalize_UnknownKeys_IgnoresThem()
        {
            var normalized = BookGenreCounts.Normalize(new Dictionary<string, int>
            {
                ["Mystery"] = 10,
                ["Crime"] = 2
            });

            Assert.IsFalse(normalized.ContainsKey("Mystery"));
            Assert.AreEqual(2, normalized["Crime"]);
        }

        [Test]
        public void Normalize_ReturnsExactEnumOrder()
        {
            var normalized = BookGenreCounts.Normalize(new Dictionary<string, int>
            {
                ["Travel"] = 5,
                ["Classic"] = 1,
                ["Kids"] = 4
            });

            CollectionAssert.AreEqual(
                System.Enum.GetValues(typeof(BookGenre))
                    .Cast<BookGenre>()
                    .Select(genre => genre.ToConfigValue())
                    .ToArray(),
                normalized.Keys.ToArray());
        }
    }
}
