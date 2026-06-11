using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Итог дня продажи. Будущая фаза Results потребляет это; пока — для логов и тестов.
    /// </summary>
    public sealed class SalesDayResult
    {
        public int Day { get; set; }

        public int CustomersServed { get; set; }       // активные + пассивные
        public int ManualRequests { get; set; }        // только активные (включая Skipped)
        public int SalesCount { get; set; }            // фактически проданные книги (Normal + Excellent + пассивные)
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
