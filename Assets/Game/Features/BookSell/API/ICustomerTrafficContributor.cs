namespace Book.Sell.API
{
    /// <summary>
    /// A single source of influence on the day's regular customer count (location, decor, and future
    /// weather/calendar/events). All registered contributors run every day and write into a shared
    /// <see cref="CustomerTrafficAccumulator"/> — this is an ordered contributor pipeline, NOT a
    /// Chain of Responsibility (no early exit, no single owner of the final number).
    ///
    /// Kept in Book.Sell.API so external features can implement it without referencing the Book.Sell
    /// impl assembly (same pattern as <see cref="IDecorModifierProvider"/>).
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public interface ICustomerTrafficContributor
    {
        void Contribute(CustomerTrafficContext context, CustomerTrafficAccumulator accumulator);
    }
}
