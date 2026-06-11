using System.Collections.Generic;
using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Mutable state of the sales day. The View renders from this snapshot after each event.
    /// Not serialized (Save is out of scope for this iteration).
    /// </summary>
    public sealed class SalesSessionState
    {
        public int Day { get; set; }
        public string LocationId { get; set; }

        public List<ShelfBook> Shelf { get; } = new();
        public List<RequestConfig> ActiveQueue { get; } = new();

        /// <summary>Index of the current active request inside <see cref="ActiveQueue"/>. -1 until the day starts.</summary>
        public int CurrentRequestIndex { get; set; } = -1;

        public bool DayCompleted { get; set; }
    }
}
