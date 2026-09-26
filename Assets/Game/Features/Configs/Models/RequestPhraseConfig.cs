using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// One entry of the active-request phrase lexicon. The composer turns a request's boolean
    /// <see cref="RequestConditionGroup"/> into a human sentence by looking every condition leaf up here
    /// and stitching the resulting fragments together.
    ///
    /// <para>File: request_phrases.json (JSON array). The config loader takes one type per file, so every
    /// flavour of phrase lives in this array and is discriminated by <see cref="Kind"/>.</para>
    ///
    /// <para>Phrases themselves are never stored here — only localization keys. See
    /// docs/INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md for the authored lexicon and the slot model.</para>
    /// </summary>
    [ConfigFile("request_phrases")]
    public sealed class RequestPhraseConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>
        /// Discriminator: <c>term</c> (a genre/quality value), <c>band</c> (a numeric condition),
        /// <c>combo</c> (two terms collapsing into one phrase), or one of the wrapper pools
        /// <c>opener</c> / <c>lead</c> / <c>anchor</c>.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>Condition type the entry answers to: genres, qualities, pages, publicationYear. Terms and bands only.</summary>
        public string Type { get; set; }

        /// <summary>
        /// Condition operator. Bands only, and matched exactly — there are eleven authored bands and no
        /// range arithmetic. Terms ignore it: <c>equal</c> and <c>contains</c> are the same predicate for
        /// list fields (see BookConditionRequestEvaluator.EvaluateList).
        /// </summary>
        public string Operator { get; set; }

        /// <summary>Term: the genre/quality string. Band: the scalar bound for a non-<c>between</c> operator.</summary>
        public JToken Value { get; set; }

        /// <summary>Band with <c>between</c>: inclusive lower bound.</summary>
        public double? Min { get; set; }

        /// <summary>Band with <c>between</c>: inclusive upper bound.</summary>
        public double? Max { get; set; }

        /// <summary>Combo: ids of the term entries this phrase replaces. Both must be present for it to fire.</summary>
        public string[] Terms { get; set; }

        /// <summary>Localization key for the phrase as it reads when the condition must hold.</summary>
        public string PositiveKey { get; set; }

        /// <summary>
        /// Localization key for the phrase as it reads inside a <c>none</c> group ("but nothing gory").
        /// Absent when the exclusion cannot be voiced politely — the composer then drops the fragment.
        /// </summary>
        public string NegativeKey { get; set; }
    }
}
