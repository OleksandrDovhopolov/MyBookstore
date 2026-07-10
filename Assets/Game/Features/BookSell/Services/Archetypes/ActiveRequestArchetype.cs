using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// A single active recommendation request. Empty middle when <paramref name="request"/> is null,
    /// matching ActiveRequestsOnlyCustomerSpawner's "requests.Count > 0" guard. No random consumed.
    /// </summary>
    public sealed class ActiveRequestArchetype : ICustomerArchetype
    {
        private readonly RequestConfig _request;

        public ActiveRequestArchetype(RequestConfig request)
        {
            _request = request;
        }

        public string Id => "active";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var steps = new List<ICustomerStep>(1);
            if (_request != null)
                steps.Add(new ActiveRequestStep(_request));
            return steps;
        }
    }
}
