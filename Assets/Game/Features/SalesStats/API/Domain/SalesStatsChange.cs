using Game.Configs.Models;

namespace Game.SalesStats.API
{
    /// <summary>
    /// Notification published after a successful sales-stat mutation.
    /// Carries the affected genre, its new running count for that stat, and the current sold total.
    /// </summary>
    public sealed class SalesStatsChange
    {
        public BookGenre Genre { get; }
        public int NewCount { get; }
        public int TotalSold { get; }
        public string BookId { get; }

        public SalesStatsChange(BookGenre genre, int newCount, int totalSold, string bookId)
        {
            Genre = genre;
            NewCount = newCount;
            TotalSold = totalSold;
            BookId = bookId;
        }
    }
}
