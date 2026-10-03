using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    public interface IActiveRequestSelector
    {
        /// <summary>
        /// Picks the request this customer will ask for. Called late — when the customer actually enters the
        /// minigame, not when the day is planned — so <paramref name="shelf"/> is what is still on sale right
        /// now. Returns null when the day's pool is empty.
        /// </summary>
        /// <param name="shelf">Books available for selection at this moment; passive sales have already
        /// removed whatever was sold earlier in the day.</param>
        ActiveRequestRuntime Draw(CustomerProfile profile, IReadOnlyList<ShelfBook> shelf, ISalesRandom random);
    }

    public interface IActiveRequestSelectorFactory
    {
        IActiveRequestSelector CreateForDay(IReadOnlyList<ActiveRequestRuntime> pool);
    }
}
