namespace Book.Sell.API
{
    /// <summary>
    /// A single source of influence on how many customers arrive with an active request (location, decor,
    /// and future weather/calendar/events). Same pipeline contract as
    /// <see cref="ICustomerTrafficContributor"/>: every contributor runs, writes percent deltas into a
    /// shared accumulator, no early exit.
    ///
    /// Deliberately a SEPARATE interface from <see cref="ICustomerTrafficContributor"/> so a decor item can
    /// pull in more people without also making more of them ask for help (and vice versa) — the two knobs
    /// are independent. The context/accumulator types are reused as-is: they are a plain
    /// "day inputs in, percent deltas out" bag with nothing customer-specific in them.
    /// </summary>
    public interface IActiveRequestCountContributor
    {
        void Contribute(CustomerTrafficContext context, CustomerTrafficAccumulator accumulator);
    }
}
