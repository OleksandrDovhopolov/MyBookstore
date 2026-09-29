using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Game.Configs.Editor;
using Game.Configs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// The seeded book text is frozen once in docs/content/book_descriptions_seed_en.json and is what every
    /// rewrite is compared against. Two things must stay true: the archive does not drift, and it never
    /// finds its way into the shipped content.
    /// </summary>
    public sealed class BookDescriptionSeedTests
    {
        /// <summary>
        /// Normalized hash, not a hash of the bytes: git runs with autocrlf, so a raw file digest is stable
        /// only inside one working tree. This one is over the ordered id + title + author + description join
        /// with carriage returns stripped, so re-indenting the file cannot change it either.
        /// </summary>
        private const string ExpectedNormalizedSha256 =
            "0611125a473126fb08e2664a6138660212028d323ca8fa20ce064b7ce8c7e048";

        private const int ExpectedRows = 663;

        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        [Test]
        public void Seed_IsFrozen()
        {
            var root = LoadSeed();

            Assert.AreEqual(ExpectedRows, root.Count, "The seed archive is a snapshot; rows must not be added or removed.");
            Assert.AreEqual(
                ExpectedNormalizedSha256,
                NormalizedHash(root),
                "The seed archive changed. It is the frozen record of the text being replaced — restore it "
                + "instead of updating this hash, unless the archive itself was wrong.");
        }

        [Test]
        public void Seed_CoversEveryDraftedBook()
        {
            var root = LoadSeed();
            var drafts = JArray.Parse(File.ReadAllText(BookDescriptionDraftValidator.DraftsPath));

            var missing = drafts
                .Select(draft => (string)draft["id"])
                .Where(id => root[id] == null)
                .ToArray();

            Assert.IsEmpty(missing, $"Drafted books with no row in the seed archive: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// `Seed_IsFrozen` proves the archive has not changed. It cannot prove the archive is still the
        /// ORIGINAL text — once the rewrites were applied, live localization stopped being a second copy to
        /// compare against. Except for the excluded books: those 43 descriptions are never rewritten
        /// (they belong to another product's setting, see docs/content/BOOK_CONTENT_QUESTIONS.md), so they
        /// remain a standing sample of the seeded text. If the archive and the localization file ever drift
        /// together — a bad merge, a re-import, a well-meaning "fix" — this is what notices.
        /// </summary>
        [Test]
        public void Seed_StillMatchesTheUnrewrittenDescriptions()
        {
            var root = LoadSeed();
            var books = JsonConvert.DeserializeObject<List<BookConfig>>(
                File.ReadAllText(Path.Combine(ContentRoots[0], "books.json")));
            var localization = JObject.Parse(
                File.ReadAllText(Path.Combine(ContentRoots[0], "localization_books_en.json")));

            var excluded = books.Where(book => book.IsExcludedFromCatalog).ToArray();
            Assert.IsNotEmpty(excluded, "No excluded books left; this tripwire needs a new sample.");

            foreach (var book in excluded)
            {
                var archived = (string)root[book.Id]?["description"];
                var live = (string)localization[book.DescriptionKey];

                Assert.AreEqual(
                    archived,
                    live,
                    $"'{book.Id}' is excluded from the catalogue, so its description is never rewritten — "
                    + "the archive and localization disagreeing means one of them drifted.");
            }
        }

        [Test]
        public void Seed_HasTitleAndAuthorAnchors()
        {
            var root = LoadSeed();

            foreach (var property in root.Properties())
            {
                var entry = (JObject)property.Value;
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace((string)entry["title"]),
                    $"{property.Name} has no title. Book ids are positional in the seed spreadsheet, so the "
                    + "title and author are what tie an archived row to a real book.");
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)entry["author"]), $"{property.Name} has no author.");
                Assert.IsFalse(string.IsNullOrWhiteSpace((string)entry["description"]), $"{property.Name} has no description.");
            }
        }

        /// <summary>
        /// The archive holds the copied text. Under Assets/Configs it would be force-copied into
        /// StreamingAssets, shipped in the APK and pulled into memory at boot — and a `localization_`-prefixed
        /// name would even slip past the orphan scan and the section invariant. Keep it out by name and by test.
        /// </summary>
        [Test]
        public void SeedAndDrafts_AreNotPartOfShippedContent()
        {
            foreach (var root in ContentRoots)
            {
                foreach (var forbidden in new[]
                         {
                             "book_descriptions_seed_en.json",
                             "localization_books_en.seed.json",
                             "book_descriptions.json",
                             "book_descriptions_v2.json"
                         })
                {
                    var path = Path.Combine(root, forbidden);
                    Assert.IsFalse(
                        File.Exists(path),
                        $"{path} must not ship: authoring material for the rewrite lives in docs/content.");
                }
            }
        }

        private static JObject LoadSeed()
        {
            var path = BookDescriptionDraftValidator.SeedPath;
            Assert.IsTrue(File.Exists(path), $"{path} not found.");
            return JObject.Parse(File.ReadAllText(path));
        }

        private static string NormalizedHash(JObject root)
        {
            var payload = string.Join(
                "\n",
                root.Properties().Select(property =>
                {
                    var entry = (JObject)property.Value;
                    return string.Join(
                        "\u001f",
                        property.Name,
                        (string)entry["title"],
                        (string)entry["author"],
                        (string)entry["description"]);
                })).Replace("\r", string.Empty);

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
