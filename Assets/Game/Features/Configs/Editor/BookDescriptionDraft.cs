using System;

namespace Game.Configs.Editor
{
    /// <summary>
    /// One rewritten book description, batch by batch, before it replaces the live text in
    /// localization_books_en.json. The live catalogue text was seeded from an external spreadsheet and is
    /// being rewritten from scratch (CONTENT-1 in docs/RELEASE_TASKS.md, rules in
    /// docs/content/BOOK_DESCRIPTION_STYLE.md).
    ///
    /// <para>File: docs/content/book_descriptions.json — deliberately NOT under Assets/Configs. Everything
    /// there is force-copied into StreamingAssets, shipped in the APK, loaded into memory at boot and
    /// published to the config server as a section; drafts are authoring material that wants none of that.
    /// Hence no <c>IConfig</c> and no <c>[ConfigFile]</c>: this is Editor-side data.</para>
    ///
    /// <para><see cref="TitleAtDraft"/> / <see cref="AuthorAtDraft"/> are not decoration: book ids are
    /// positional in the seed spreadsheet (row N becomes bookN, see <see cref="BooksExcelImporter"/>), so
    /// inserting a row re-keys every book after it. The anchor is what catches a draft that has drifted
    /// onto another book.</para>
    ///
    /// <para>There is no `old` column. The text being replaced lives once, in the frozen archive
    /// docs/content/book_descriptions_seed_en.json, which is also what the validator compares against —
    /// comparing against live localization would break the moment a batch is applied.</para>
    /// </summary>
    [Serializable]
    public sealed class BookDescriptionDraft
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

        /// <summary>Title as the catalogue had it when the draft was written — identity anchor.</summary>
        public string TitleAtDraft { get; set; }

        /// <summary>Author as the catalogue had it when the draft was written — identity anchor.</summary>
        public string AuthorAtDraft { get; set; }

        /// <summary>
        /// Whether the writer actually knew this book. False routes the row to review and forbids plot
        /// claims in the text — a self-report, so the validator backs it with checks that do not rely on it.
        /// </summary>
        public bool Recognized { get; set; } = true;

        /// <summary>Hand-written remark. Never machine-filled: a prefilled note goes stale on the next edit.</summary>
        public string Notes { get; set; }

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
