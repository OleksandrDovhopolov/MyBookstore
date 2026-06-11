using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// End-of-day summary of the Sales phase. Future Results phase will consume this;
    /// for now it feeds the debug log and the EditMode tests.
    /// </summary>
    public sealed class SalesDayResult
    {
        public int Day { get; set; }

        public int CustomersServed { get; set; }       // active + passive
        public int ManualRequests { get; set; }        // active only (including Skipped)
        public int SalesCount { get; set; }            // books actually sold (Normal + Excellent + passive)
        public int GoldEarned { get; set; }

        public int ExcellentCount { get; set; }
        public int NormalCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }

        public List<string> SoldBookIds { get; } = new();
        public List<RecommendationResult> Recommendations { get; } = new();
        public List<PassiveSaleEvent> PassiveSales { get; } = new();
    }
}
