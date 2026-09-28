using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Editor;
using Game.Configs.Models;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// The rewritten descriptions in book_descriptions_v2.json are content under review, so the authored
    /// file is checked as content (both roots must be clean) and the validator itself is checked against
    /// deliberately broken drafts — a check nobody has seen fail is not a check.
    /// </summary>
    public sealed class BookDescriptionDraftTests
    {
        private static readonly string[] ContentRoots =
        {
            Path.Combine("Assets", "Configs"),
            Path.Combine("Assets", "StreamingAssets", "Configs")
        };

        private string _tempDir;

        [TearDown]
        public void TearDown()
        {
            if (_tempDir != null && Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
            _tempDir = null;
        }

        [Test]
        public void AuthoredDrafts_AreValidInEveryContentRoot()
        {
            foreach (var root in ContentRoots)
            {
                var report = BookDescriptionDraftValidator.Validate(root);

                Assert.IsEmpty(
                    report.Errors,
                    $"{root}/{BookDescriptionDraftValidator.DraftsFileName}:\n{string.Join("\n", report.Errors)}");
            }
        }

        [Test]
        public void AuthoredDrafts_AreIdenticalInBothContentRoots()
        {
            var authored = ReadDrafts(ContentRoots[0]);
            var bundled = ReadDrafts(ContentRoots[1]);

            CollectionAssert.AreEqual(
                authored.Select(draft => draft.Id).ToArray(),
                bundled.Select(draft => draft.Id).ToArray(),
                "Bundled drafts are out of sync with Assets/Configs. Run Tools/Configs/Sync Bundled Defaults.");

            foreach (var draft in authored)
            {
                var copy = bundled.SingleOrDefault(other => other.Id == draft.Id);
                Assert.IsNotNull(copy, $"'{draft.Id}' is missing from the bundled copy.");
                Assert.AreEqual(draft.New, copy.New, $"'{draft.Id}' differs between content roots.");
            }
        }

        [Test]
        public void UnknownBookId_IsAnError()
        {
            var report = ValidateFixture(Draft("book999"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("book999")), Dump(report));
        }

        [Test]
        public void DuplicateBookId_IsAnError()
        {
            var report = ValidateFixture(Draft("book01"), Draft("book01"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("more than once")), Dump(report));
        }

        [Test]
        public void ExcludedBook_IsAnError()
        {
            var report = ValidateFixture(Draft("book02", title: "Fake Title", author: "Fake Author"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("excluded")), Dump(report));
        }

        /// <summary>Book ids are positional in the seed sheet, so a stale anchor means a shifted draft.</summary>
        [Test]
        public void AnchorThatNoLongerMatchesTheCatalog_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", title: "Some Other Book"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("shifted")), Dump(report));
        }

        [Test]
        public void EmptyNewText_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", text: "   "));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("empty")), Dump(report));
        }

        [Test]
        public void RichTextMarkup_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", text: GoodText + " <b>bold</b>"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("rich-text")), Dump(report));
        }

        [Test]
        public void UnknownStatus_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", status: "almost-done"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("status")), Dump(report));
        }

        [Test]
        public void RepeatingTheOldDescription_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", text: OldText));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("repeats")), Dump(report));
        }

        [Test]
        public void ShortText_IsOnlyAWarning()
        {
            var report = ValidateFixture(Draft("book01", text: "A short travel guide."));

            Assert.IsEmpty(report.Errors, Dump(report));
            Assert.IsTrue(report.Warnings.Any(warning => warning.Contains("shorter")), Dump(report));
        }

        [Test]
        public void OverlappingWordRun_IsOnlyAWarning()
        {
            var report = ValidateFixture(Draft("book01", text: "A guide that lists " + SharedRun + " and more besides."));

            Assert.IsEmpty(report.Errors, Dump(report));
            Assert.IsTrue(report.Warnings.Any(warning => warning.Contains("word run")), Dump(report));
        }

        /// <summary>
        /// A description that names its own book legitimately repeats a long run of the old text. Masking the
        /// title and author is what keeps the overlap check usable on honest writing.
        /// </summary>
        [Test]
        public void SharedRun_IgnoresTitleAndAuthor()
        {
            const string title = "1,000 Places to See Before You Die";
            const string author = "Patricia Schultz";
            var masked = new[] { title, author };

            Assert.Less(
                BookDescriptionDraftValidator.LongestSharedRun(
                    $"{title} by {author} is a list of destinations.",
                    $"{title} by {author} collects destinations.",
                    masked),
                BookDescriptionDraftValidator.OverlapWindowWords,
                "Title and author must be excluded before counting shared runs.");

            Assert.GreaterOrEqual(
                BookDescriptionDraftValidator.LongestSharedRun(
                    "a wide variety of stunning places around the globe",
                    "a wide variety of stunning places around the globe",
                    masked),
                BookDescriptionDraftValidator.OverlapWindowWords,
                "Wording shared outside title and author must still be counted.");
        }

        // ---------- fixtures ----------

        private const string GoodText =
            "A destination list that sorts the world's landmarks, coastlines and quiet corners into trips " +
            "worth taking, with enough practical detail to plan one and enough restraint to keep it readable.";

        private const string OldText =
            "This original bucket list contains a wide variety of stunning places around the globe.";

        private const string SharedRun = "a wide variety of stunning places around the globe";

        private const string BooksFixture = @"[
  {
    ""id"": ""book01"",
    ""titleKey"": ""book.book01.title"",
    ""authorKey"": ""book.book01.author"",
    ""descriptionKey"": ""book.book01.description"",
    ""genres"": [""Travel""],
    ""published"": 2003,
    ""pages"": 974,
    ""qualities"": [""Non Fiction""],
    ""fakeOrReal"": ""Real""
  },
  {
    ""id"": ""book02"",
    ""titleKey"": ""book.book02.title"",
    ""authorKey"": ""book.book02.author"",
    ""descriptionKey"": ""book.book02.description"",
    ""genres"": [""Fact""],
    ""published"": 2020,
    ""pages"": 100,
    ""qualities"": [""Non Fiction""],
    ""fakeOrReal"": ""Fake""
  }
]";

        private static readonly string LocalizationFixture = JsonConvert.SerializeObject(
            new Dictionary<string, string>
            {
                ["book.book01.title"] = "1,000 Places to See Before You Die",
                ["book.book01.author"] = "Patricia Schultz",
                ["book.book01.description"] = OldText,
                ["book.book02.title"] = "Fake Title",
                ["book.book02.author"] = "Fake Author",
                ["book.book02.description"] = "Something from another setting."
            });

        private static BookDescriptionDraftConfig Draft(
            string id,
            string text = GoodText,
            string title = "1,000 Places to See Before You Die",
            string author = "Patricia Schultz",
            string status = BookDescriptionDraftConfig.StatusDraft)
            => new()
            {
                Id = id,
                Batch = 1,
                Status = status,
                TitleAtDraft = title,
                AuthorAtDraft = author,
                New = text
            };

        private BookDescriptionDraftReport ValidateFixture(params BookDescriptionDraftConfig[] drafts)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "book_drafts_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);

            File.WriteAllText(Path.Combine(_tempDir, BookDescriptionDraftValidator.BooksFileName), BooksFixture);
            File.WriteAllText(
                Path.Combine(_tempDir, BookDescriptionDraftValidator.LocalizationFileName), LocalizationFixture);
            File.WriteAllText(
                Path.Combine(_tempDir, BookDescriptionDraftValidator.DraftsFileName),
                JsonConvert.SerializeObject(drafts));

            return BookDescriptionDraftValidator.Validate(_tempDir);
        }

        private static List<BookDescriptionDraftConfig> ReadDrafts(string root)
        {
            var path = Path.Combine(root, BookDescriptionDraftValidator.DraftsFileName);
            Assert.IsTrue(File.Exists(path), $"{path} not found.");

            return JsonConvert.DeserializeObject<List<BookDescriptionDraftConfig>>(File.ReadAllText(path))
                   ?? new List<BookDescriptionDraftConfig>();
        }

        private static string Dump(BookDescriptionDraftReport report)
            => $"errors:\n{string.Join("\n", report.Errors)}\nwarnings:\n{string.Join("\n", report.Warnings)}";
    }
}
