namespace Game.Configs.Models
{
    /// <summary>
    /// Sales-shape customer request. Replaced the earlier quest-style structure
    /// (bookId / rewardSoft / timeLimitSeconds), which was not about sales at all.
    /// File: requests.json (JSON array).
    /// Spec: docs/INPROGRESS/Продажа.md, plan: docs/INPROGRESS/SalesFeatureImplementationPlan.md.
    /// </summary>
    [ConfigFile("requests")]
    public sealed class RequestConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Customer's line — the player reads it and tries to figure out what fits.</summary>
        public string Text { get; set; }

        /// <summary>Desired genre(s). Exact match — +3 to score. An array lets the request accept synonyms / alternatives.</summary>
        public string[] DesiredGenres { get; set; }

        /// <summary>Desired themes / tags (+2 per match).</summary>
        public string[] DesiredTags { get; set; }

        /// <summary>Desired tone / mood (+1 per match).</summary>
        public string[] DesiredMood { get; set; }

        /// <summary>Maximum book price the customer is willing to pay. 0 means no limit (price scoring is skipped).</summary>
        public int MaxPrice { get; set; }

        /// <summary>Difficulty (1..5) for designer balancing and UI display only. Scoring does not use it.</summary>
        public RequestDifficulty Difficulty { get; set; }

        /// <summary>Bonus gold on top of the book's BasePrice when the recommendation tier is Excellent.</summary>
        public int BaseRewardGold { get; set; }
    }
}
