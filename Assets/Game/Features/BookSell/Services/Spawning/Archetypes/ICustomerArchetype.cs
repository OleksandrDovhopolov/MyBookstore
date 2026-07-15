using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// Builds the MIDDLE of a single customer's plan (everything between the mandatory ApproachStep and
    /// the CompletePurchase/Leave closing tail, which <see cref="CustomerPlanBuilder"/> adds). Separates
    /// "what this customer does" from the spawner's day composition ("who/how many show up").
    ///
    /// Called exactly once per customer via the builder's middle delegate, so any <see cref="ISalesRandom"/>
    /// draws happen in order between the approach and leave duration rolls. <see cref="Id"/> is diagnostic
    /// only (debug/log/future config) — no production logic depends on it in this phase.
    /// </summary>
    public interface ICustomerArchetype
    {
        string Id { get; }

        IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random);
    }
}
