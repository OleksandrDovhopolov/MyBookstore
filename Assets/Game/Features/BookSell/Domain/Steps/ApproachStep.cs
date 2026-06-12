namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer walks up to the cart. A pure-domain duration gate: accumulates dt until
    /// ApproachDuration, then completes. The View renders the actual movement.
    /// </summary>
    public sealed class ApproachStep : ICustomerStep
    {
        private float _elapsed;

        public void Enter(Customer self, CustomerContext ctx)
        {
            _elapsed = 0f;
            self.SetPhase(CustomerPhase.Approaching, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _elapsed += dt;
            return _elapsed >= ctx.Tuning.ApproachDuration ? StepStatus.Completed : StepStatus.Running;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
        }
    }
}
