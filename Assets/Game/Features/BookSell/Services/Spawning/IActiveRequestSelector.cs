using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    public interface IActiveRequestSelector
    {
        ActiveRequestRuntime Draw(CustomerProfile profile, ISalesRandom random);
    }

    public interface IActiveRequestSelectorFactory
    {
        IActiveRequestSelector CreateForDay(IReadOnlyList<ActiveRequestRuntime> pool);
    }
}
