using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Persistent player shelf/stock state. Runtime SalesShelf is rebuilt from this/day setup,
    /// while sold books stay excluded from future preparation.
    /// </summary>
    public sealed class SalesShelfState
    {
        public List<string> ShelfBookIds { get; set; } = new();
        public List<string> SoldBookIds { get; set; } = new();
    }
}
