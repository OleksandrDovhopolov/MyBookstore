using Game.Configs.Models;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer asks for active help (the minigame). First it <see cref="Sub.Think"/>s for
    /// <see cref="SalesTuning.BrowseDuration"/> showing the "Choosing..." HUD — purely a HUD dwell, the
    /// interaction lock is NOT touched during this phase. Then it acquires the shared interaction lock when
    /// free (FIFO) and holds it — staying <see cref="StepStatus.Running"/> — until the controller resolves
    /// it via player input (RecommendBook / Skip) and force-completes the step. While the lock is held by
    /// someone else, the step is <see cref="StepStatus.Blocked"/> (the customer waits).
    /// </summary>
    public sealed class ActiveRequestStep : ICustomerStep
    {
        private enum Sub { Think, AwaitingHelp }

        private readonly RequestConfig _request;
        private Sub _sub;
        private float _t;
        private bool _acquired;

        public ActiveRequestStep(RequestConfig request)
        {
            _request = request;
        }

        public RequestConfig Request => _request;

        public void Enter(Customer self, CustomerContext ctx)
        {
            _sub = Sub.Think;
            _t = 0f;
            // Think first: show "Choosing..." for a while (same as passive Browse). No lock yet.
            self.SetPhase(CustomerPhase.Browsing, ctx, forceNotify: true);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            if (_sub == Sub.Think)
            {
                _t += dt;
                if (_t < ctx.Tuning.BrowseDuration) return StepStatus.Running;   // HUD-only dwell, no lock

                _sub = Sub.AwaitingHelp;
                self.SetPhase(CustomerPhase.AwaitingHelp, ctx);
                // fall through and try to enter the minigame this same tick
            }

            if (_acquired) return StepStatus.Running;   // holding the lock, awaiting player input

            if (ctx.Shelf.AvailableForSelection().Count == 0)
                return StepStatus.Completed;

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
            // No-op when we never got past the Think phase.
            ctx.Lock.Release(self);
            _acquired = false;
        }
    }
}
