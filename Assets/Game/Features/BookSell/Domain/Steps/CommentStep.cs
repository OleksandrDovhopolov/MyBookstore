using System;
using Book.Sell.API;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// A short event-driven customer comment inserted into the middle of a visit. It does not introduce a
    /// separate customer phase; presentation listens to OnCustomerComment.
    /// </summary>
    public sealed class CommentStep : ICustomerStep
    {
        private readonly CustomerCommentPayload _payload;
        private readonly float? _durationOverride;
        private float _elapsed;

        public CommentStep(CustomerCommentPayload payload, float? duration = null)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _durationOverride = duration;
        }

        public CustomerCommentPayload Payload => _payload;

        public void Enter(Customer self, CustomerContext ctx)
        {
            _elapsed = 0f;
            ctx.Sink?.OnCustomerComment(self, _payload);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _elapsed += dt;
            return _elapsed >= ResolveDuration(ctx.Tuning) ? StepStatus.Completed : StepStatus.Running;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
        }

        private float ResolveDuration(SalesTuning tuning)
            => Math.Max(0f, _durationOverride ?? tuning.CommentDuration);
    }
}
