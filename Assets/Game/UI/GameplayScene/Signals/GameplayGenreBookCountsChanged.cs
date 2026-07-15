using System.Collections.Generic;

namespace Game.UI
{
    public readonly struct GameplayGenreBookCountsChanged
    {
        public IReadOnlyDictionary<string, int> Counts { get; }
        public IReadOnlyDictionary<string, int> PurchasedCounts { get; }
        public bool ShowPurchasedCounts { get; }

        public GameplayGenreBookCountsChanged(IReadOnlyDictionary<string, int> counts)
            : this(counts, null, false)
        {
        }

        public GameplayGenreBookCountsChanged(
            IReadOnlyDictionary<string, int> counts,
            IReadOnlyDictionary<string, int> purchasedCounts,
            bool showPurchasedCounts)
        {
            Counts = counts;
            PurchasedCounts = purchasedCounts;
            ShowPurchasedCounts = showPurchasedCounts;
        }
    }
}
