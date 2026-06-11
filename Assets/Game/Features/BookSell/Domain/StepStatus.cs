namespace Book.Sell.Domain
{
    /// <summary>
    /// Result of ticking one <see cref="ICustomerStep"/>.
    /// </summary>
    public enum StepStatus
    {
        /// <summary>Step is still in progress; keep ticking it.</summary>
        Running = 0,

        /// <summary>Step finished; advance the customer to the next step.</summary>
        Completed = 1,

        /// <summary>Step cannot progress right now (e.g. the shared interaction lock is held by someone else). Stay on it.</summary>
        Blocked = 2
    }
}
