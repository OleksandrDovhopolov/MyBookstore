namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer walks up to the cart. A pure-domain duration gate: accumulates dt until
    /// its configured duration, then completes. The View renders the actual movement.
    /// </summary>
    public sealed class ApproachStep : ICustomerStep
    {
        private float? _durationOverride;
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

        /// <summary>
        /// Replaces the duration with one the View derived from the actual walking distance
        /// (distance / speed), so two customers covering different distances move at the same visible
        /// speed. Safe to call from the <see cref="CustomerPhase.Approaching"/> notification raised by
        /// <see cref="Enter"/>: no time has been consumed at that point. Ignored once the step has
        /// started ticking, so a half-walked approach can never be stretched or cut.
        /// </summary>
        public bool TryOverrideDuration(float seconds)
        {
            if (_elapsed > 0f || seconds <= 0f) return false;
            _durationOverride = seconds;
            return true;
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
