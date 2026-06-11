using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Picks a book for a passive ("background") sale: any shelf book that matches the
    /// current location's demand.
    /// </summary>
    public interface IPassiveSaleSelector
    {
        /// <summary>
        /// Returns a book to sell passively, or null when nothing on the shelf matches the location's demand.
        /// </summary>
        ShelfBook PickPassiveSale(IReadOnlyList<ShelfBook> shelf, LocationConfig location, ISalesRandom random);
    }
}
