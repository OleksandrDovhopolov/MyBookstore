using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// End-of-day summary of the Sales phase. Consumed by the Results phase
    /// (lives in the DayCycle feature) via Save module "book_sell.last_day_result".
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
