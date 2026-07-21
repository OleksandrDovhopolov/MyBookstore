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
        Blocked = 2,

        /// <summary>
        /// Step finished; the customer's passive chain ends — remaining passive steps are dropped from the
        /// plan, but non-passive steps (active request, dialogue, comment) and the closing tail still run
        /// (ADR-0003). Not "leave immediately": the customer may still ask for help or talk.
        /// </summary>
        CompletedAndEndPassiveChain = 3
    }
}
