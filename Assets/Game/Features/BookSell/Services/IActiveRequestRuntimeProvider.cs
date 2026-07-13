using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    public interface IActiveRequestRuntimeProvider
    {
        IReadOnlyList<ActiveRequestRuntime> GetRequests();
    }
}
