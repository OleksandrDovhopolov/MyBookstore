using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// The `qualities` vocabulary is a closed set, and this test is what keeps it closed.
    ///
    /// <para>Request conditions compare the whole string
    /// (<c>BookConditionRequestEvaluator.Contains</c>, OrdinalIgnoreCase), so a tag spelled two ways is
    /// two different tags: before this was normalized, 71 books tagged `Non-Fiction` could not answer a
    /// condition asking for `Non Fiction`, and the maturity tag was split four ways across 96 books.
    /// Nothing failed, because nothing checked.</para>
    ///
    /// <para>It also matters because the values reach the player unlocalized —
    /// <c>BookCardView</c> renders them straight into the card's tag label — so a stray editorial remark
    /// inside a tag value is on screen, not just in the data.</para>
    ///
    /// <para>The importer is why this test exists rather than a one-off cleanup:
    /// <c>BooksExcelImporter</c> takes `qualities` from the spreadsheet verbatim and rewrites books.json
    /// wholesale, so a re-import would silently restore every defect.</para>
    /// </summary>
    public sealed class BookQualityVocabularyTests
    {
        /// <summary>
        /// Mirrored in docs/ACTIVE_REQUEST_CONDITIONS.md. Adding a value is a content decision: add it
        /// here and in that doc in the same change, or the request lexicon and the writers drift apart.
        /// </summary>
        private static readonly string[] AllowedQualities =
        {
            "Academic", "Age Rating Mature", "Animals", "Biography", "Coming of Age", "Contemporary",
            "Cooking", "Detective", "Dry", "Dystopia", "Encyclopedic", "Epic", "Female Author",
            "Fiction", "Folklore", "Gore", "Graphic Novel", "Happy Ending", "Historic", "Hobby",
            "Horror", "Humour", "Light Reading", "Long", "Magic", "Manga", "Mystery", "Nature",
            "Niche", "Non Fiction", "Novel", "Outdated", "Philosophical", "Play", "Plot Twist",
            "Poetry", "Political", "Pop Science", "Queer", "Romance", "Science Fiction", "Self Help",
            "Series", "Short", "Space", "Thriller", "Tragic", "Travel Guide", "Very Long",
            "Whodunnit", "YA"
        };

        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        [TestCaseSource(nameof(ContentRoots))]
        public void EveryQuality_IsInTheAllowedSet(string root)
        {
            var unknown = Load(root)
                .SelectMany(book => book.Qualities ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .Where(quality => !AllowedQualities.Contains(quality, StringComparer.Ordinal))
                .OrderBy(quality => quality, StringComparer.Ordinal)
                .ToArray();

            Assert.IsEmpty(
                unknown,
                $"{root}/books.json uses quality values that are not in the vocabulary: "
                + $"{string.Join(", ", unknown.Select(q => $"'{q}'"))}. Either fix the data or add the "
                + "value here and in docs/ACTIVE_REQUEST_CONDITIONS.md.");
        }

        /// <summary>
        /// The defect class that cost the most: two values meaning the same thing, differing only by a
        /// hyphen, a space or capitalisation. Conditions match neither pair member against the other.
        /// </summary>
        [Test]
        public void Vocabulary_HasNoNearDuplicates()
        {
            var collisions = AllowedQualities
                .GroupBy(Flatten, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => string.Join(" / ", group))
                .ToArray();

            Assert.IsEmpty(
                collisions,
                $"Values differing only by separator or case: {string.Join("; ", collisions)}");
        }

        /// <summary>
        /// `Crime`, `Fact` and `Kids` were sitting in `qualities` on nine rows — a genre value in the
        /// wrong field, unreachable by any request, which asks for genres through `genres`.
        /// </summary>
        [Test]
        public void Vocabulary_DoesNotReuseGenreNames()
        {
            var genreNames = Enum.GetNames(typeof(BookGenre));

            var reused = AllowedQualities
                .Where(quality => genreNames.Contains(quality, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            Assert.IsEmpty(reused, $"Genre names used as qualities: {string.Join(", ", reused)}");
        }

        [TestCaseSource(nameof(ContentRoots))]
        public void NoBook_RepeatsAQuality(string root)
        {
            var offenders = Load(root)
                .Where(book => book.Qualities != null
                               && book.Qualities.Length != book.Qualities.Distinct(StringComparer.Ordinal).Count())
                .Select(book => book.Id)
                .ToArray();

            Assert.IsEmpty(offenders, $"{root}/books.json repeats a quality on: {string.Join(", ", offenders)}");
        }

        /// <summary>No tag may smuggle an editorial aside into its value: the player reads these.</summary>
        [Test]
        public void Vocabulary_CarriesNoEditorialRemarks()
        {
            var suspicious = AllowedQualities
                .Where(quality => quality.IndexOfAny(new[] { '[', ']', '(', ')' }) >= 0)
                .ToArray();

            Assert.IsEmpty(suspicious, $"Quality values containing brackets: {string.Join(", ", suspicious)}");
        }

        private static string Flatten(string quality)
            => quality.ToLowerInvariant().Replace("-", " ").Replace(" ", string.Empty);

        private static List<BookConfig> Load(string root)
        {
            var path = Path.Combine(root, "books.json");
            Assert.IsTrue(File.Exists(path), $"{path} not found.");

            var books = JsonConvert.DeserializeObject<List<BookConfig>>(File.ReadAllText(path));
            Assert.IsNotNull(books);
            Assert.IsNotEmpty(books);
            return books;
        }
    }
}
