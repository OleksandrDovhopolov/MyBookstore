using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;
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

        [Test]
        public void BookConfig_DeserializeNewSchema_PopulatesDescriptionGenresAndQualities()
        {
            const string json = @"{
  ""id"": ""book_001"",
  ""title"": ""Sea Winter"",
  ""author"": ""Ada Reed"",
  ""description"": ""[description_book_001]"",
  ""genres"": [""Drama"", ""Classic""],
  ""rarityWeight"": 0.35,
  ""published"": 1893,
  ""pages"": 189,
  ""qualities"": [""Female Author"", ""history""]
}";

            var book = JsonConvert.DeserializeObject<BookConfig>(json);

            Assert.IsNotNull(book);
            Assert.AreEqual("[description_book_001]", book.Description);
            CollectionAssert.AreEqual(new[] { "Drama", "Classic" }, book.Genres);
            CollectionAssert.AreEqual(new[] { "Female Author", "history" }, book.Qualities);
            Assert.AreEqual("Drama", book.PrimaryGenre);
            Assert.IsTrue(book.IsFemaleAuthor);
            Assert.AreEqual(10, BookConfig.FixedPriceGold);
        }

        [Test]
        public void BookConfig_DeserializeWithoutRarityWeight_UsesDefault()
        {
            const string json = @"{
  ""id"": ""book01"",
  ""title"": ""Sea Winter"",
  ""author"": ""Ada Reed"",
  ""description"": ""[description_book_001]"",
  ""genres"": [""Drama""],
  ""published"": 1893,
  ""pages"": 189,
  ""qualities"": [""Female Author""]
}";

            var book = JsonConvert.DeserializeObject<BookConfig>(json);

            Assert.IsNotNull(book);
            Assert.AreEqual(0.5f, book.RarityWeight);
        }

        [Test]
        public void BookConfig_DeserializeFakeOrReal_PopulatesFutureField()
        {
            const string json = @"{
  ""id"": ""book01"",
  ""title"": ""Sea Winter"",
  ""author"": ""Ada Reed"",
  ""description"": ""[description_book_001]"",
  ""genres"": [""Drama""],
  ""published"": 1893,
  ""pages"": 189,
  ""qualities"": [""Female Author""],
  ""fakeOrReal"": ""Fake""
}";

            var book = JsonConvert.DeserializeObject<BookConfig>(json);

            Assert.IsNotNull(book);
            Assert.AreEqual("Fake", book.FakeOrReal);
        }

        [TestCase("Assets/Configs/books.json")]
        [TestCase("Assets/StreamingAssets/Configs/books.json")]
        public void BookConfig_LegacyBooksJson_StillDeserializes(string path)
        {
            var books = JsonConvert.DeserializeObject<BookConfig[]>(File.ReadAllText(path));

            Assert.IsNotNull(books);
            Assert.Greater(books.Length, 0);
            Assert.IsTrue(books.All(book => !string.IsNullOrWhiteSpace(book.Id)));
        }

        [Test]
        public void ConfigsService_LoadsBookConfigFromConvertedCatalog()
        {
            var source = new FakeConfigSource
            {
                ConvertedBooks = @"[
  {
    ""id"": ""book01"",
    ""title"": ""Converted"",
    ""author"": ""Ada Reed"",
    ""description"": ""Generated catalog"",
    ""genres"": [""Travel""],
    ""published"": 2001,
    ""pages"": 123,
    ""qualities"": [""Nature""],
    ""fakeOrReal"": ""Real""
  }
]",
                LegacyBooks = @"[
  {
    ""id"": ""Book1"",
    ""title"": ""Legacy"",
    ""author"": ""Old"",
    ""description"": ""Legacy catalog"",
    ""genres"": [""Crime""],
    ""rarityWeight"": 0.1,
    ""published"": 1999,
    ""pages"": 321,
    ""qualities"": [""Detective""]
  }
]"
            };
            var service = new ConfigsService(source, overrides: null);
            service.WarmupAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();

            var books = service.GetAll<BookConfig>();

            Assert.AreEqual(1, books.Count);
            Assert.AreEqual("book01", books[0].Id);
            Assert.AreEqual("Converted", books[0].Title);
        }

        private sealed class FakeConfigSource : IConfigSource
        {
            public string ConvertedBooks;
            public string LegacyBooks;

            public Cysharp.Threading.Tasks.UniTask WarmupAsync(System.Threading.CancellationToken ct)
                => Cysharp.Threading.Tasks.UniTask.CompletedTask;

            public string GetRaw(string fileName)
            {
                if (fileName == "books_converted") return ConvertedBooks;
                if (fileName == "books") return LegacyBooks;
                return null;
            }
        }
    }
}
