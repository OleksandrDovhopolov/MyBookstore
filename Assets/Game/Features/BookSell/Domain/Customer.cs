using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// A customer "brain": an ordered plan of steps advanced by a logical tick. Pure domain —
    /// no Unity types, no wall-clock. The View renders movement/animation from phase changes.
    /// Plan traversal and runtime insertion live in <see cref="CustomerPlan"/>; this class owns the
    /// step lifecycle (Enter/Tick/Exit), the entered flag, and phase notifications.
    /// </summary>
    public sealed class Customer
    {
        private readonly CustomerPlan _plan;
        private bool _entered;

        public string Id { get; }
        public CustomerPhase Phase { get; private set; } = CustomerPhase.Spawned;

        /// <summary>Desire profile (genres) used by the requested-genre passive model. Empty by default
        /// (legacy passive model ignores it).</summary>
        public CustomerProfile Profile { get; }

        /// <summary>Total books this customer has bought during the visit (active recommendations + passive sales).</summary>
        public int PurchasedBookCount { get; private set; }

        public void RegisterPurchasedBook() => PurchasedBookCount++;

        public Customer(string id, IReadOnlyList<ICustomerStep> plan, CustomerProfile profile = null)
        {
            Id = id;
            _plan = new CustomerPlan(plan);   // CustomerPlan copies the list
            Profile = profile ?? CustomerProfile.Empty;
        }

        public bool IsDone => _plan.IsDone;

        public ICustomerStep CurrentStep => _plan.Current;

        /// <summary>Queues <paramref name="step"/> to run right after the current step. Safe to call from
        /// within the current step's Tick (e.g. a director reacting to a sink fact). Returns false if the
        /// plan is done or on a closing step. See <see cref="CustomerPlan.InsertNext"/>.</summary>
        public bool InsertNext(ICustomerStep step) => _plan.InsertNext(step);

        /// <summary>Queues <paramref name="step"/> just before the closing tail. See
        /// <see cref="CustomerPlan.InsertBeforeClosing"/>.</summary>
        public bool InsertBeforeClosing(ICustomerStep step) => _plan.InsertBeforeClosing(step);

        public void SetPhase(CustomerPhase phase, CustomerContext ctx, bool forceNotify = false)
        {
            if (!forceNotify && Phase == phase) return;
            Phase = phase;
            ctx.Sink?.OnPhaseChanged(this, phase);
        }

        /// <summary>Advances the active step by one logical tick; moves to the next step when it completes.</summary>
        public void Tick(CustomerContext ctx, float dt)
        {
            if (_plan.IsDone) return;

            var step = _plan.Current;
            if (!_entered)
            {
                step.Enter(this, ctx);
                _entered = true;
            }

            var status = step.Tick(this, ctx, dt);
            if (status == StepStatus.Completed)
                Advance(ctx);
            else if (status == StepStatus.CompletedAndLeave)
                AbandonRemainingPurchasesAndClose(ctx);
        }

        /// <summary>
        /// Ends the shopping cycle early: exits the current step (freeing any held reservation), skips
        /// the remaining purchase steps, and resumes at the first closing step (CompletePurchase then
        /// Leave) so the completion animation and walk-away still run. The skipped intermediate steps were
        /// never entered, so their Exit is intentionally not called. A defensive fallback finishes
        /// immediately if no closing step lies ahead.
        /// </summary>
        private void AbandonRemainingPurchasesAndClose(CustomerContext ctx)
        {
            _plan.Current.Exit(this, ctx);
            _entered = false;

            if (_plan.SkipToClosing())
                return;   // first closing step Enters and runs normally next tick

            UnityEngine.Debug.LogWarning($"[Customer] plan for '{Id}' has no closing step — leaving instantly.");
            // Defensive: no closing step ahead — finish immediately.
            _plan.Finish();
            SetPhase(CustomerPhase.Leaving, ctx);
            SetPhase(CustomerPhase.Done, ctx);
        }

        /// <summary>
        /// Externally completes the current step (used by the controller when the player resolves an
        /// active minigame). Runs the step's Exit (releasing the lock) and advances the plan.
        /// </summary>
        public void ForceCompleteCurrentStep(CustomerContext ctx)
        {
            if (_plan.IsDone) return;
            Advance(ctx);
        }

        private void Advance(CustomerContext ctx)
        {
            _plan.Current.Exit(this, ctx);
            _plan.Advance();
            _entered = false;
            if (_plan.IsDone) SetPhase(CustomerPhase.Done, ctx);
        }
    }
}
