using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Builds the day's customers (each with its own plan of steps). Behind an interface because the
    /// generation policy changes often and will be owned by a separate system later. The simulation
    /// does not know how many there are or how they were chosen.
    /// </summary>
    public interface ICustomerSpawner
    {
        IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random);
    }
}
