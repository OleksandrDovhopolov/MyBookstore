using System;

namespace Game.Configs.Models
{
    /// <summary>
    /// Rewritten book descriptions, batch by batch, before they replace the live text in
    /// localization_books_en.json. The live catalogue text was seeded from an external sheet and is being
    /// rewritten from scratch (CONTENT-1 in docs/RELEASE_TASKS.md); this file is where a batch is authored
    /// and reviewed, so nothing half-written reaches the game.
    ///
    /// <para>Nothing at runtime reads this section — it exists for the authoring and review pass. Once a
    /// batch is applied, its rows are what the review was signed off on.</para>
    ///
    /// <para><see cref="TitleAtDraft"/> / <see cref="AuthorAtDraft"/> are not decoration: book ids are
    /// positional in the seed sheet (row N becomes bookN, see BooksExcelImporter), so inserting a row
    /// re-keys every book after it. The anchor is what catches a draft that has drifted onto another book.</para>
    ///
    /// File: book_descriptions_v2.json (JSON array).
    /// </summary>
    [ConfigFile("book_descriptions_v2")]
    public sealed class BookDescriptionDraftConfig : IConfig
    {
        public const string StatusDraft = "draft";
        public const string StatusReviewed = "reviewed";
        public const string StatusApplied = "applied";

        /// <summary>Allowed <see cref="Status"/> values; the validator rejects anything else.</summary>
        public static readonly string[] AllowedStatuses = { StatusDraft, StatusReviewed, StatusApplied };

        /// <summary>The book id this draft rewrites, e.g. "book165".</summary>
        public string Id { get; set; }

        /// <summary>Rewrite batch this row belongs to, 1-based.</summary>
        public int Batch { get; set; }

        /// <summary>One of <see cref="AllowedStatuses"/>.</summary>
        public string Status { get; set; }

        /// <summary>Title as localization had it when the draft was written — identity anchor.</summary>
        public string TitleAtDraft { get; set; }

        /// <summary>Author as localization had it when the draft was written — identity anchor.</summary>
        public string AuthorAtDraft { get; set; }

        /// <summary>
        /// The description being replaced, filled in only AFTER the new text is written, never before:
        /// the rewrite is clean-room, so the old wording must not be an input to it.
        /// </summary>
        public string Old { get; set; }

        /// <summary>The rewritten description.</summary>
        public string New { get; set; }

        /// <summary>Localization key this draft targets once applied.</summary>
        public string ResolveDescriptionKey()
            => string.IsNullOrWhiteSpace(Id) ? null : $"book.{Id}.description";

        public static bool IsAllowedStatus(string status)
        {
            for (var i = 0; i < AllowedStatuses.Length; i++)
            {
                if (string.Equals(AllowedStatuses[i], status, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
