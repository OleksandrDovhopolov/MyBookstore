using Game.Configs.Models;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer asks for active help (the minigame). Acquires the shared interaction lock when
    /// free (FIFO), then holds it — staying <see cref="StepStatus.Running"/> — until the controller
    /// resolves it via player input (RecommendBook / Skip) and force-completes the step. While the
    /// lock is held by someone else, the step is <see cref="StepStatus.Blocked"/> (the customer waits).
    /// </summary>
    public sealed class ActiveRequestStep : ICustomerStep
    {
        private readonly RequestConfig _request;
        private bool _acquired;

        public ActiveRequestStep(RequestConfig request)
        {
            _request = request;
        }

        public RequestConfig Request => _request;

        public void Enter(Customer self, CustomerContext ctx)
        {
            self.SetPhase(CustomerPhase.AwaitingHelp, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            if (_acquired) return StepStatus.Running;   // holding the lock, awaiting player input

            if (ctx.Lock.TryAcquire(self))
            {
                _acquired = true;
                self.SetPhase(CustomerPhase.InMinigame, ctx);
                ctx.Sink?.OnActiveRequestStarted(self, _request);
                return StepStatus.Running;
            }

            // Lock held by someone else; TryAcquire enqueued us FIFO. Wait.
            return StepStatus.Blocked;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
            // Releases the lock if we held it, or removes us from the FIFO queue if we were only waiting.
            ctx.Lock.Release(self);
            _acquired = false;
        }
    }
}
