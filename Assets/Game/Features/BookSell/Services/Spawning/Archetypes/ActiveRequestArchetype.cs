using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Services
{
    /// <summary>
    /// A single active recommendation request. The request itself is drawn by the step from the day's
    /// selector once the customer reaches the minigame, so the archetype carries no payload.
    /// No random consumed.
    /// </summary>
    public sealed class ActiveRequestArchetype : ICustomerArchetype
    {
        public string Id => "active";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
            => new ICustomerStep[] { new ActiveRequestStep() };
    }
}
