using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Strategy that resolves one passive purchase attempt: which genre was attempted and whether it
    /// sold (and the book). The shared <c>PassivePurchaseStep</c> flow drives browse/commit/feedback;
    /// this only decides the candidate. Two impls: legacy shelf-roll and requested-genre.
    /// </summary>
    public interface IPassivePurchaseResolver
    {
        PassiveAttemptResult Resolve(Customer self, CustomerContext ctx, IReadOnlyList<ShelfBook> available);
    }
}
