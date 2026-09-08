using System.Collections.Generic;
using System.Linq;
using Game.Configs.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    public sealed class BooksExcelImporterTests
    {
        [Test]
        public void ConvertRows_MapsExcelRowToBookJson()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "  The Book  "),
                    ("Description", "  About books  "),
                    ("Published", 2001d),
                    ("Pages", 123d),
                    ("Author", "  Ada  "),
                    ("Genres", " fantasy, Crime "),
                    ("Qualities", " Fiction, Magic "),
                    ("Fake or Real", " Real "))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            var book = (JObject)result.Books[0];
            Assert.AreEqual("book01", book.Value<string>("id"));
            Assert.AreEqual("book.book01.title", book.Value<string>("titleKey"));
            Assert.AreEqual("book.book01.author", book.Value<string>("authorKey"));
            Assert.AreEqual("book.book01.description", book.Value<string>("descriptionKey"));
            CollectionAssert.AreEqual(new[] { "Fantasy", "Crime" }, book["genres"].ToObject<string[]>());
            CollectionAssert.AreEqual(new[] { "Fiction", "Magic" }, book["qualities"].ToObject<string[]>());
            Assert.AreEqual(2001, book.Value<int>("published"));
            Assert.AreEqual(123, book.Value<int>("pages"));
            Assert.AreEqual("Real", book.Value<string>("fakeOrReal"));
            Assert.AreEqual(0.5d, book.Value<double>("rarityWeight"));

            Assert.AreEqual("The Book", result.Localization.Value<string>("book.book01.title"));
            Assert.AreEqual("Ada", result.Localization.Value<string>("book.book01.author"));
            Assert.AreEqual("About books", result.Localization.Value<string>("book.book01.description"));
        }

        [Test]
        public void ConvertRows_ReadsRarityWeightColumn()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Rare Book"),
                    ("Description", "Description"),
                    ("Published", 2001d),
                    ("Pages", 123d),
                    ("Author", "Ada"),
                    ("Genres", "Fantasy"),
                    ("Qualities", "Magic"),
                    ("Fake or Real", "Real"),
                    ("RarityWeight", 0.75d))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual(0.75d, ((JObject)result.Books[0]).Value<double>("rarityWeight"));
        }

        [Test]
        public void ConvertRows_ReadsRarityAlias()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Rare Book"),
                    ("Description", "Description"),
                    ("Published", 2001d),
                    ("Pages", 123d),
                    ("Author", "Ada"),
                    ("Genres", "Fantasy"),
                    ("Qualities", "Magic"),
                    ("Fake or Real", "Real"),
                    ("Rarity", "0.7"))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual(0.7d, ((JObject)result.Books[0]).Value<double>("rarityWeight"));
        }

        [Test]
        public void ConvertRows_RarityWeightColumnWinsOverRarityAlias()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Rare Book"),
                    ("Description", "Description"),
                    ("Published", 2001d),
                    ("Pages", 123d),
                    ("Author", "Ada"),
                    ("Genres", "Fantasy"),
                    ("Qualities", "Magic"),
                    ("Fake or Real", "Real"),
                    ("RarityWeight", 0.8d),
                    ("Rarity", 0.6d))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual(0.8d, ((JObject)result.Books[0]).Value<double>("rarityWeight"));
        }

        [Test]
        public void ConvertRows_InvalidRarity_SkipsRowWithWarning()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Bad Rarity"),
                    ("Description", "Description"),
                    ("Published", 2001d),
                    ("Pages", 123d),
                    ("Author", "Ada"),
                    ("Genres", "Fantasy"),
                    ("Qualities", "Magic"),
                    ("Fake or Real", "Real"),
                    ("RarityWeight", "very rare"))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual(0, result.Books.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("Row 2 'Bad Rarity' skipped"));
            Assert.IsTrue(result.Warnings[0].Contains("RarityWeight is not numeric"));
        }

        [Test]
        public void ConvertRows_GeneratesTwoDigitIdsWithoutTruncatingHundreds()
        {
            var rows = Enumerable.Range(0, 100)
                .Select(i => Row(i + 2,
                    ("Title", $"Book {i}"),
                    ("Description", "Description"),
                    ("Published", 2000d),
                    ("Pages", 100d),
                    ("Author", "Author"),
                    ("Genres", "Travel"),
                    ("Qualities", "Nature"),
                    ("Fake or Real", "Fake")))
                .ToArray();

            var result = BooksExcelImporter.ConvertRows(rows);

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual("book01", ((JObject)result.Books[0]).Value<string>("id"));
            Assert.AreEqual("book02", ((JObject)result.Books[1]).Value<string>("id"));
            Assert.AreEqual("book100", ((JObject)result.Books[99]).Value<string>("id"));
        }

        [Test]
        public void ConvertRows_MissingHeader_BlocksConversion()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Book"),
                    ("Description", "Description"),
                    ("Published", 2000d),
                    ("Pages", 100d),
                    ("Author", "Author"),
                    ("Genres", "Travel"),
                    ("Fake or Real", "Real"))
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("Missing required header 'Qualities'")));
            Assert.AreEqual(0, result.Books.Count);
            Assert.AreEqual(0, result.Localization.Count);
        }

        [Test]
        public void ConvertRows_InvalidValues_SkipsRowsWithWarnings()
        {
            var result = BooksExcelImporter.ConvertRows(new[]
            {
                Row(2,
                    ("Title", "Bad Book"),
                    ("Description", "Description"),
                    ("Published", 2000.5d),
                    ("Pages", 100.25d),
                    ("Author", "Author"),
                    ("Genres", "Mystery"),
                    ("Qualities", ""),
                    ("Fake or Real", "Real")),
                Row(3,
                    ("Title", "Good Book"),
                    ("Description", "Description"),
                    ("Published", 2000d),
                    ("Pages", 100d),
                    ("Author", "Author"),
                    ("Genres", "Travel"),
                    ("Qualities", "Nature"),
                    ("Fake or Real", "Real"))
            });

            Assert.IsTrue(result.Success, string.Join("\n", result.Errors));
            Assert.AreEqual(1, result.Books.Count);
            Assert.AreEqual("book02", ((JObject)result.Books[0]).Value<string>("id"));
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.IsTrue(result.Warnings[0].Contains("Row 2 'Bad Book' skipped"));
            Assert.IsTrue(result.Warnings[0].Contains("Published must be an integer"));
            Assert.IsTrue(result.Warnings[0].Contains("Pages must be an integer"));
            Assert.IsTrue(result.Warnings[0].Contains("Genres contains unknown value 'Mystery'"));
            Assert.IsTrue(result.Warnings[0].Contains("Qualities is empty"));
        }

        private static BooksExcelRow Row(int number, params (string Key, object Value)[] values)
            => new(number, values.ToDictionary(v => v.Key, v => v.Value));
    }
}
