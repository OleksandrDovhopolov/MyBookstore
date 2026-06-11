using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Services;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>Returns a fixed, hand-built customer list — lets controller tests control exact plans.</summary>
    public sealed class StubCustomerSpawner : ICustomerSpawner
    {
        private readonly IReadOnlyList<Customer> _customers;

        public StubCustomerSpawner(IReadOnlyList<Customer> customers) => _customers = customers;

        public IReadOnlyList<Customer> BuildCustomers(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
            => _customers;
    }
}
