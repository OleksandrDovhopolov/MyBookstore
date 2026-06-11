namespace Book.Sell.Domain
{
    /// <summary>
    /// One resumable step in a customer's plan. Unlike a one-shot command, a step has a running
    /// status and can stay <see cref="StepStatus.Blocked"/> across ticks (e.g. waiting for the
    /// shared interaction lock). Pure domain — no Unity types, advanced by a logical tick.
    /// <paramref name="self"/> is the owning customer (used as the lock token and in sink callbacks).
    /// </summary>
    public interface ICustomerStep
    {
        void Enter(Customer self, CustomerContext ctx);

        StepStatus Tick(Customer self, CustomerContext ctx, float dt);

        /// <summary>
        /// Called when the step finishes OR the customer is force-completed by the controller. Must
        /// release any resources the step holds (shelf reservations, the interaction lock).
        /// </summary>
        void Exit(Customer self, CustomerContext ctx);
    }
}
