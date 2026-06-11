using System.Collections.Generic;

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

        public Customer(string id, IReadOnlyList<ICustomerStep> plan)
        {
            Id = id;
            _plan = new List<ICustomerStep>(plan);
        }

        public bool IsDone => _index >= _plan.Count;

        public ICustomerStep CurrentStep => _index < _plan.Count ? _plan[_index] : null;

        public void SetPhase(CustomerPhase phase, CustomerContext ctx)
        {
            if (Phase == phase) return;
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
