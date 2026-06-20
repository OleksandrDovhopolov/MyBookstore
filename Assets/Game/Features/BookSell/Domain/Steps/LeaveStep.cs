namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer leaves. A pure-domain duration gate: accumulates dt until its configured leave
    /// duration, then completes — the customer becomes Done when the plan advances past this step.
    /// The View plays the walk-away animation independently (see CustomerVisualRegistry). On entry it
    /// clears the customer's thought bubble so it walks away without any HUD.
    /// </summary>
    public sealed class LeaveStep : IClosingStep
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
            // Any purchase/fail feedback already had its dwell in the prior steps; drop the bubble so
            // the customer leaves with a clean HUD.
            ctx.Sink?.OnHideThoughtBubble(self);
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
