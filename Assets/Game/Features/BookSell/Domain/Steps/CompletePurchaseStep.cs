namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// Closing step that celebrates the customer's passive purchases. If the customer bought at least
    /// <see cref="MinBooksForCompletion"/> books in passive mode it raises a completion fact (for the
    /// HUD) and holds for a configured duration so the animation can play; otherwise it completes
    /// instantly and the plan moves on. Active-recommendation sales are not counted (see
    /// <see cref="Customer.PassivePurchaseCount"/>).
    /// </summary>
    public sealed class CompletePurchaseStep : IClosingStep
    {
        private const int MinBooksForCompletion = 1;

        private readonly float? _durationOverride;
        private float _elapsed;
        private bool _qualifies;

        public CompletePurchaseStep(float? duration = null)
        {
            _durationOverride = duration;
        }

        public void Enter(Customer self, CustomerContext ctx)
        {
            _elapsed = 0f;
            _qualifies = self.PassivePurchaseCount >= MinBooksForCompletion;
            if (_qualifies)
                ctx.Sink?.OnPurchaseCompleted(self, self.PassivePurchaseCount);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            if (!_qualifies) return StepStatus.Completed;

            _elapsed += dt;
            return _elapsed >= ResolveDuration(ctx.Tuning) ? StepStatus.Completed : StepStatus.Running;
        }

        public float ResolveDuration(SalesTuning tuning)
            => _durationOverride ?? tuning.CompletePurchaseDuration;

        public void Exit(Customer self, CustomerContext ctx)
        {
        }
    }
}
