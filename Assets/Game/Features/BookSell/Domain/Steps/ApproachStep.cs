namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer walks up to the cart. A pure-domain duration gate: accumulates dt until
    /// its configured duration, then completes. The View renders the actual movement.
    /// </summary>
    public sealed class ApproachStep : ICustomerStep
    {
        private readonly float? _durationOverride;
        private float _elapsed;

        public ApproachStep(float? duration = null)
        {
            _durationOverride = duration;
        }

        public void Enter(Customer self, CustomerContext ctx)
        {
            _elapsed = 0f;
            self.SetPhase(CustomerPhase.Approaching, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _elapsed += dt;
            return _elapsed >= ResolveDuration(ctx.Tuning) ? StepStatus.Completed : StepStatus.Running;
        }

        public float ResolveDuration(SalesTuning tuning)
            => _durationOverride ?? tuning.ApproachDuration;

        public void Exit(Customer self, CustomerContext ctx)
        {
        }
    }
}
