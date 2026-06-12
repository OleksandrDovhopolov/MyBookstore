using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Picks a book for a passive ("background") sale: any shelf book that matches the
    /// current location's demand. Returns the matched demand items together with the book
    /// so the View can explain "why it sold".
    /// </summary>
    public interface IPassiveSaleSelector
    {
        /// <summary>
        /// Returns a candidate (book + matched demand), or null when nothing on the shelf
        /// matches the location's demand.
        /// </summary>
        PassiveSaleCandidate PickPassiveSale(
            IReadOnlyList<ShelfBook> shelf,
            LocationConfig location,
            ISalesRandom random);
    }
}
