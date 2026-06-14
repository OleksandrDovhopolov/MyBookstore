using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Picks a book for a passive ("background") sale via a two-stage probabilistic model
    /// (ADR-0004): per-genre chance gate, then weighted pick by <c>RarityWeight</c>.
    /// Active decor ids enter the chance formula through <see cref="IDecorModifierProvider"/>.
    /// Returns a candidate (book + matched genre), or null when no genre passes the gate.
    /// </summary>
    public interface IPassiveSaleSelector
    {
        PassiveSaleCandidate PickPassiveSale(
            IReadOnlyList<ShelfBook> shelf,
            LocationConfig location,
            IReadOnlyList<string> activeDecorIds,
            ISalesRandom random);
    }
}
