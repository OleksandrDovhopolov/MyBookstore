namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// Marker for steps that are passive purchase intents. Once a passive attempt fails, ADR-0003 says the
    /// customer plans no further passive intents this visit — so every remaining step carrying this marker
    /// is dropped from the plan, while non-passive steps (active request, dialogue, comment) still run.
    ///
    /// A marker (not a type check) so <see cref="Book.Sell.Domain.CustomerPlan"/> stays type-agnostic —
    /// same reason <see cref="IClosingStep"/> exists.
    /// </summary>
    public interface IPassivePurchaseStep : ICustomerStep
    {
    }
}
