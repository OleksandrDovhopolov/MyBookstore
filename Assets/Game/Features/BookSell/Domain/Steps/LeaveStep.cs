namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer leaves. A pure-domain duration gate: accumulates dt until its configured leave
    /// duration, then completes — the customer becomes Done when the plan advances past this step.
    /// The View plays the walk-away animation independently (see CustomerVisualRegistry).
    /// </summary>
    public sealed class LeaveStep : ICustomerStep
    {
        private readonly float? _durationOverride;
        private float _elapsed;

        public LeaveStep(float? duration = null)
        {
            _durationOverride = duration;
        }

        public void Enter(Customer self, CustomerContext ctx)
        {
            _elapsed = 0f;
            self.SetPhase(CustomerPhase.Leaving, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _elapsed += dt;
            return _elapsed >= ResolveDuration(ctx.Tuning) ? StepStatus.Completed : StepStatus.Running;
        }

        public float ResolveDuration(SalesTuning tuning)
            => _durationOverride ?? tuning.LeaveDuration;

        public void Exit(Customer self, CustomerContext ctx)
        {
        }
    }
}
