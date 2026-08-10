using System.Collections.Generic;
using System.IO;
using System.Linq;
using Book.Sell.Services;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class ShelfPresetCoverageTests
    {
        private const string DefaultPresetId = "preset_default_42";

        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private static readonly string[] ExpectedGenres =
        {
            "Classic",
            "Crime",
            "Drama",
            "Fact",
            "Fantasy",
            "Kids",
            "Travel"
        };

        [Test]
        public void Presets_ReferenceExistingBooks_AndCoverEveryEnabledRequest()
        {
            foreach (var root in ContentRoots)
            {
                var books = Load<BookConfig>(root, "books.json");
                var requests = Load<RequestDefinitionConfig>(root, "sample_requests.json")
                    .Where(request => request != null && request.Enabled)
                    .ToArray();
                var presets = Load<ShelfPresetConfig>(root, "shelf_presets.json");

                var booksById = books.ToDictionary(book => book.Id, book => book);
                var evaluator = new BookConditionRequestEvaluator();

                foreach (var preset in presets)
                {
                    var bookIds = preset.BookIds ?? new string[0];
                    Assert.IsNotEmpty(bookIds, $"{root}/{preset.Id} must contain at least one book id.");

                    var missing = bookIds
                        .Where(id => !booksById.ContainsKey(id))
                        .ToArray();
                    Assert.IsEmpty(missing, $"{root}/{preset.Id} references missing book id(s): {string.Join(", ", missing)}");

                    var shelfBooks = bookIds.Select(id => booksById[id]).ToArray();
                    var uncovered = new List<string>();

                    foreach (var request in requests)
                    {
                        Assert.IsTrue(
                            evaluator.IsValid(request, out var reason),
                            $"{root}/{request.Id} must be a valid request: {reason}");

                        if (!shelfBooks.Any(book => evaluator.Evaluate(book, request).IsMatch))
                            uncovered.Add(request.Id);
                    }

                    Assert.IsEmpty(
                        uncovered,
                        $"{root}/{preset.Id} does not cover enabled request(s): {string.Join(", ", uncovered)}");
                }
            }
        }

        [Test]
        public void DefaultPreset_CoversAllPrimaryGenres()
        {
            foreach (var root in ContentRoots)
            {
                var books = Load<BookConfig>(root, "books.json")
                    .ToDictionary(book => book.Id, book => book);
                var preset = Load<ShelfPresetConfig>(root, "shelf_presets.json")
                    .Single(p => p.Id == DefaultPresetId);

                var bookIds = preset.BookIds ?? new string[0];
                var genres = bookIds
                    .Select(id => books[id].PrimaryGenre)
                    .Where(genre => !string.IsNullOrEmpty(genre))
                    .Distinct()
                    .OrderBy(genre => genre)
                    .ToArray();

                CollectionAssert.AreEquivalent(ExpectedGenres, genres, root);
            }
        }

        private static T[] Load<T>(string root, string fileName)
            => JsonConvert.DeserializeObject<T[]>(
                   File.ReadAllText(Path.Combine(root, fileName)))
               ?? new T[0];
    }
}
