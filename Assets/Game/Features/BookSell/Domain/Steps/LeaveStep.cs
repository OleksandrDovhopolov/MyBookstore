namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer leaves. Instant in the domain; the View plays the walk-away animation.
    /// The customer becomes Done when the plan advances past this step.
    /// </summary>
    public sealed class LeaveStep : ICustomerStep
    {
        public void Enter(Customer self, CustomerContext ctx)
        {
            self.SetPhase(CustomerPhase.Leaving, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
            => StepStatus.Completed;

        public void Exit(Customer self, CustomerContext ctx)
        {
        }
    }
}
