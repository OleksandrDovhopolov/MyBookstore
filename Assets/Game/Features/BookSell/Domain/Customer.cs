using System.Collections.Generic;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Domain
{
    /// <summary>
    /// A customer "brain": an ordered plan of steps advanced by a logical tick. Pure domain —
    /// no Unity types, no wall-clock. The View renders movement/animation from phase changes.
    /// </summary>
    public sealed class Customer
    {
        private readonly List<ICustomerStep> _plan;
        private int _index;
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
            _plan = new List<ICustomerStep>(plan);
            Profile = profile ?? CustomerProfile.Empty;
        }

        public bool IsDone => _index >= _plan.Count;

        public ICustomerStep CurrentStep => _index < _plan.Count ? _plan[_index] : null;

        public void SetPhase(CustomerPhase phase, CustomerContext ctx, bool forceNotify = false)
        {
            if (!forceNotify && Phase == phase) return;
            Phase = phase;
            ctx.Sink?.OnPhaseChanged(this, phase);
        }

        /// <summary>Advances the active step by one logical tick; moves to the next step when it completes.</summary>
        public void Tick(CustomerContext ctx, float dt)
        {
            if (IsDone) return;

            var step = _plan[_index];
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
        /// the remaining purchase steps, and resumes at the first closing step (<see cref="IClosingStep"/>
        /// — CompletePurchase then Leave) so the completion animation and walk-away still run. The skipped
        /// intermediate steps were never entered, so their Exit is intentionally not called. A defensive
        /// fallback finishes immediately if no closing step lies ahead.
        /// </summary>
        private void AbandonRemainingPurchasesAndClose(CustomerContext ctx)
        {
            _plan[_index].Exit(this, ctx);

            for (var i = _index + 1; i < _plan.Count; i++)
            {
                if (_plan[i] is IClosingStep)
                {
                    _index = i;        // first closing step Enters and runs normally next tick
                    _entered = false;
                    return;
                }
            }

            UnityEngine.Debug.LogWarning($"[Customer] plan for '{Id}' has no closing step — leaving instantly.");
            // Defensive: no closing step ahead — finish immediately.
            _index = _plan.Count;
            _entered = false;
            SetPhase(CustomerPhase.Leaving, ctx);
            SetPhase(CustomerPhase.Done, ctx);
        }

        /// <summary>
        /// Externally completes the current step (used by the controller when the player resolves an
        /// active minigame). Runs the step's Exit (releasing the lock) and advances the plan.
        /// </summary>
        public void ForceCompleteCurrentStep(CustomerContext ctx)
        {
            if (IsDone) return;
            Advance(ctx);
        }

        private void Advance(CustomerContext ctx)
        {
            _plan[_index].Exit(this, ctx);
            _index++;
            _entered = false;
            if (IsDone) SetPhase(CustomerPhase.Done, ctx);
        }
    }
}
