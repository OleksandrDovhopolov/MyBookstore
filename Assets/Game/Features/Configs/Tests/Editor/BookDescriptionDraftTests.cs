using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Configs.Tests.Editor
{
    /// <summary>
    /// The rewritten descriptions are content under review, so the authored file is checked as content and
    /// the validator itself is checked against deliberately broken drafts — a check nobody has seen fail is
    /// not a check.
    ///
    /// <para>Fixtures write their own seed, books and localization into a temp directory. They must never
    /// fall back to the real files: the fixture data is modelled on book01, so a default pointing at the
    /// production archive would make some of these tests pass for the wrong reason.</para>
    /// </summary>
    public sealed class BookDescriptionDraftTests
    {
        /// <summary>Every key a draft row may carry. Anything else is drift between model, writer and validator.</summary>
        private static readonly HashSet<string> AllowedKeys = new()
        {
            "id", "batch", "status", "titleAtDraft", "authorAtDraft", "recognized", "notes", "new"
        };

        private static readonly HashSet<string> RequiredKeys = new()
        {
            "id", "batch", "status", "titleAtDraft", "authorAtDraft", "new"
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
        public void AuthoredDrafts_AreValid()
        {
            var report = BookDescriptionDraftValidator.Validate();

            Assert.IsEmpty(
                report.Errors,
                $"{BookDescriptionDraftValidator.DraftsPath}:\n{string.Join("\n", report.Errors)}");
        }

        /// <summary>
        /// One test for the whole class of drift: a row keeps exactly the known keys. Catches a leftover
        /// `old` column (nothing rejects unknown keys — no MissingMemberHandling is configured anywhere),
        /// a field added to the model but not to the writer, and a typo in a hand-edited row.
        /// </summary>
        [Test]
        public void AuthoredDrafts_CarryExactlyTheKnownKeys()
        {
            var rows = JArray.Parse(File.ReadAllText(BookDescriptionDraftValidator.DraftsPath));
            Assert.IsNotEmpty(rows);

            foreach (var row in rows.Cast<JObject>())
            {
                var id = (string)row["id"];
                var keys = row.Properties().Select(property => property.Name).ToArray();

                var unknown = keys.Where(key => !AllowedKeys.Contains(key)).ToArray();
                Assert.IsEmpty(unknown, $"'{id}' has unknown key(s): {string.Join(", ", unknown)}");

                var missing = RequiredKeys.Where(key => !keys.Contains(key)).ToArray();
                Assert.IsEmpty(missing, $"'{id}' is missing key(s): {string.Join(", ", missing)}");
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
            var report = ValidateFixture(new[] { Draft("book01"), Draft("book01") });

            Assert.IsTrue(report.Errors.Any(error => error.Contains("more than once")), Dump(report));
        }

        [Test]
        public void ExcludedBook_IsAnError()
        {
            var report = ValidateFixture(Draft("book02", title: "Second Fixture Book", author: "Fixture Author"));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("excluded")), Dump(report));
        }

        /// <summary>Book ids are positional in the seed spreadsheet, so a stale anchor means a shifted draft.</summary>
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
        public void RepeatingTheSeededDescription_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", text: SeededText));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("repeats")), Dump(report));
        }

        [Test]
        public void FirstPerson_IsAnError()
        {
            var report = ValidateFixture(Draft("book01", text: "One of my favourite guides to slow travel, and a fine excuse to start planning a trip you cannot afford."));

            Assert.IsTrue(report.Errors.Any(error => error.Contains("first person")), Dump(report));
        }

        /// <summary>
        /// 26 real book titles contain a first-person word ("Shatter Me", "The Fault in Our Stars"). Since the
        /// rule is error-level, it has to run over masked text or it fires on honest descriptions.
        /// </summary>
        [Test]
        public void FirstPersonInsideTheTitle_IsNotAnError()
        {
            var report = ValidateFixture(Draft(
                "book03",
                text: "Call Me by Your Name is a slow, sunlit account of one summer on the Italian coast, and it never once looks away from how much it costs.",
                title: "Call Me by Your Name",
                author: "Third Fixture Author"));

            Assert.IsEmpty(report.Errors, Dump(report));
        }

        [Test]
        public void SecondPersonAndQuestions_AreAllowed()
        {
            var report = ValidateFixture(Draft(
                "book01",
                text: "Ever wondered where you would go with a year and no obligations? A browsing book of destinations, arranged so you can plan one trip or daydream about thirty."));

            Assert.IsEmpty(report.Errors, Dump(report));
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
            var report = ValidateFixture(Draft("book01", text: "A browsing book that lists " + SharedRun + " and rather a lot else."));

            Assert.IsEmpty(report.Errors, Dump(report));
            Assert.IsTrue(report.Warnings.Any(warning => warning.Contains("word run")), Dump(report));
        }

        /// <summary>
        /// The check that actually enforces clean-room: a writer handed the old corpus echoes some OTHER
        /// book's blurb, which comparing each draft to its own predecessor would never notice.
        /// </summary>
        [Test]
        public void OverlapWithAnotherBooksSeededText_IsWarned()
        {
            var report = ValidateFixture(Draft("book01", text: "A browsing book of destinations " + OtherBookRun + " for the patient traveller."));

            Assert.IsTrue(
                report.Warnings.Any(warning => warning.Contains("book02")),
                "Overlap with another book's seeded description must be reported.\n" + Dump(report));
        }

        [Test]
        public void YearThatContradictsTheCatalog_IsWarned()
        {
            var report = ValidateFixture(Draft("book01", text: "First published in 1954, this browsing book of destinations is arranged for planning one trip or daydreaming about thirty."));

            Assert.IsTrue(report.Warnings.Any(warning => warning.Contains("1954")), Dump(report));
        }

        [Test]
        public void UnrecognizedBook_IsWarnedAndListed()
        {
            var draft = Draft("book01");
            draft.Recognized = false;

            var report = ValidateFixture(draft);

            Assert.IsEmpty(report.Errors, Dump(report));
            CollectionAssert.Contains(report.NeedsFactCheck, "book01");
        }

        /// <summary>
        /// An applied row must match what the game serves. This is the only job live localization still has
        /// here, and it catches an apply that never reached the content files.
        /// </summary>
        [Test]
        public void AppliedRowThatDoesNotMatchLocalization_IsAnError()
        {
            var draft = Draft("book01");
            draft.Status = BookDescriptionDraft.StatusApplied;

            var report = ValidateFixture(draft);

            Assert.IsTrue(report.Errors.Any(error => error.Contains("marked applied")), Dump(report));
        }

        [Test]
        public void AppliedRowThatMatchesLocalization_IsFine()
        {
            var draft = Draft("book01", text: SeededText.Replace("browsing", "wandering"));
            draft.Status = BookDescriptionDraft.StatusApplied;

            var report = ValidateFixture(draft, liveDescription: draft.New);

            Assert.IsEmpty(report.Errors, Dump(report));
        }

        /// <summary>
        /// A description that names its own book legitimately repeats a long run of the old text. Masking the
        /// title and author is what keeps the overlap check usable on honest writing.
        /// </summary>
        [Test]
        public void SharedRun_IgnoresTitleAndAuthor()
        {
            const string title = "A Fixture Guide to Distant Places";
            const string author = "Fixture Author";
            var masked = new[] { title, author };

            Assert.Less(
                BookDescriptionDraftValidator.LongestSharedRun(
                    $"{title} by {author} is a list of destinations.",
                    $"{title} by {author} collects destinations.",
                    masked),
                BookDescriptionDraftValidator.OverlapWindowWords,
                "Title and author must be excluded before counting shared runs.");

            Assert.GreaterOrEqual(
                BookDescriptionDraftValidator.LongestSharedRun(SharedRun, SharedRun, masked),
                BookDescriptionDraftValidator.OverlapWindowWords,
                "Wording shared outside title and author must still be counted.");
        }

        // ---------- fixtures ----------
        // Invented prose on purpose: earlier versions of this file quoted the real seeded text, which made
        // the fixtures indistinguishable from production data.

        private const string GoodText =
            "A browsing book of destinations, arranged so a reader can plan one trip properly or daydream " +
            "about thirty, with just enough practical detail to make either feel possible.";

        private const string SeededText =
            "A browsing catalogue of far-off places for the armchair traveller who may never board a plane.";

        private const string SharedRun = "a browsing catalogue of far-off places for the armchair traveller";

        private const string OtherBookRun = "gathered from decades of letters between two stubborn correspondents";

        private const string FixtureTitle = "A Fixture Guide to Distant Places";
        private const string FixtureAuthor = "Fixture Author";

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
  },
  {
    ""id"": ""book03"",
    ""titleKey"": ""book.book03.title"",
    ""authorKey"": ""book.book03.author"",
    ""descriptionKey"": ""book.book03.description"",
    ""genres"": [""Drama""],
    ""published"": 2007,
    ""pages"": 256,
    ""qualities"": [""Fiction""],
    ""fakeOrReal"": ""Real""
  }
]";

        private static BookDescriptionDraft Draft(
            string id,
            string text = GoodText,
            string title = FixtureTitle,
            string author = FixtureAuthor,
            string status = BookDescriptionDraft.StatusDraft)
            => new()
            {
                Id = id,
                Batch = 1,
                Status = status,
                TitleAtDraft = title,
                AuthorAtDraft = author,
                New = text
            };

        private BookDescriptionDraftReport ValidateFixture(
            BookDescriptionDraft draft,
            string liveDescription = null)
            => ValidateFixture(new[] { draft }, liveDescription);

        private BookDescriptionDraftReport ValidateFixture(
            BookDescriptionDraft[] drafts,
            string liveDescription = null)
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "book_drafts_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);

            var booksPath = Path.Combine(_tempDir, BookDescriptionDraftValidator.BooksFileName);
            var localizationPath = Path.Combine(_tempDir, BookDescriptionDraftValidator.LocalizationFileName);
            var seedPath = Path.Combine(_tempDir, "seed.json");
            var draftsPath = Path.Combine(_tempDir, "drafts.json");

            File.WriteAllText(booksPath, BooksFixture);
            File.WriteAllText(localizationPath, JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                ["book.book01.title"] = FixtureTitle,
                ["book.book01.author"] = FixtureAuthor,
                ["book.book01.description"] = liveDescription ?? SeededText,
                ["book.book02.title"] = "Second Fixture Book",
                ["book.book02.author"] = "Fixture Author",
                ["book.book02.description"] = "Letters " + OtherBookRun + " and a shop that answered them.",
                ["book.book03.title"] = "Call Me by Your Name",
                ["book.book03.author"] = "Third Fixture Author"
            }));
            File.WriteAllText(seedPath, JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["book01"] = new { title = FixtureTitle, author = FixtureAuthor, description = SeededText },
                ["book02"] = new
                {
                    title = "Second Fixture Book",
                    author = "Fixture Author",
                    description = "Letters " + OtherBookRun + " and a shop that answered them."
                },
                ["book03"] = new
                {
                    title = "Call Me by Your Name",
                    author = "Third Fixture Author",
                    description = "A summer on the coast, told years later by someone still counting the days."
                }
            }));
            File.WriteAllText(draftsPath, JsonConvert.SerializeObject(drafts));

            return BookDescriptionDraftValidator.Validate(_tempDir, seedPath, draftsPath);
        }

        private static string Dump(BookDescriptionDraftReport report)
            => $"errors:\n{string.Join("\n", report.Errors)}\nwarnings:\n{string.Join("\n", report.Warnings)}";
    }
}
