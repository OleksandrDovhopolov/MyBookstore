using UnityEngine;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// The customer asks for active help (the minigame). First it <see cref="Sub.Think"/>s for
    /// <see cref="SalesTuning.BrowseDuration"/> showing the "Choosing..." HUD — purely a HUD dwell, the
    /// interaction lock is NOT touched during this phase. Then it acquires the shared interaction lock when
    /// free (FIFO) and holds it — staying <see cref="StepStatus.Running"/> — until the controller resolves
    /// it via player input (RecommendBook / Skip) and force-completes the step. While the lock is held by
    /// someone else, the step is <see cref="StepStatus.Blocked"/> (the customer waits).
    ///
    /// <para>
    /// The request itself is chosen at the moment the lock is taken, not when the day was planned: by then
    /// passive sales have already emptied part of the shelf, and a request picked any earlier could have
    /// become unanswerable while the customer was still queueing. <see cref="Request"/> is therefore null
    /// until that point.
    /// </para>
    /// </summary>
    public sealed class ActiveRequestStep : ICustomerStep
    {
        private const string LogPrefix = "[ActiveRequests]";

        private enum Sub { Think, AwaitingHelp }

        private readonly ActiveRequestRuntime _authored;

        private ActiveRequestRuntime _request;
        private Sub _sub;
        private float _t;
        private bool _acquired;

        public ActiveRequestStep()
        {
        }

        /// <summary>Pins a specific request instead of drawing one. For tests and cheat-driven days.</summary>
        public ActiveRequestStep(ActiveRequestRuntime request)
        {
            _authored = request;
        }

        /// <summary>The request being asked for; null until the customer actually enters the minigame.</summary>
        public ActiveRequestRuntime Request => _request;

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
                // Drawn here, against the shelf as it stands right now — see the class summary.
                _request = ResolveRequest(self, ctx);
                if (_request == null)
                {
                    // Nothing to ask for: release the lock we just took and leave without a minigame,
                    // rather than opening an empty one.
                    ctx.Lock.Release(self);
                    Debug.LogWarning($"{LogPrefix} customer '{self.Id}' reached the minigame with no request " +
                                     "available; the visit ends without an active sale.");
                    return StepStatus.Completed;
                }

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

        private ActiveRequestRuntime ResolveRequest(Customer self, CustomerContext ctx)
        {
            if (_authored != null) return _authored;

            return ctx.ActiveRequests?.Draw(self.Profile, ctx.Shelf.AvailableForSelection(), ctx.Random);
        }
    }
}
