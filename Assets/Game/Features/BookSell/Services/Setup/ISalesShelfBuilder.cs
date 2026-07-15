using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    public interface ISalesShelfBuilder
    {
        SalesShelf Build(IReadOnlyList<string> bookIds);
    }
}
