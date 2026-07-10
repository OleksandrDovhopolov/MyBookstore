using System;
using System.Collections.Generic;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Mutable, index-based traversal of a customer's ordered steps with safe runtime insertion.
    /// Pure domain — owns only the list/index/insertion math; it never calls Enter/Tick/Exit (the step
    /// lifecycle stays in <see cref="Customer"/>, which also tracks whether Current has been entered).
    ///
    /// Invariants:
    /// - Inserted steps never land after the closing tail: inserting while <see cref="Current"/> is an
    ///   <see cref="IClosingStep"/> is rejected (returns false).
    /// - Multiple inserts before the next <see cref="Advance"/> keep call order (FIFO).
    /// </summary>
    public sealed class CustomerPlan
    {
        private readonly List<ICustomerStep> _steps;
        private int _index;

        // Position the next InsertNext should write to, so repeated inserts in one tick stay FIFO.
        // -1 means "no inserts since the last position change" → start at _index + 1.
        private int _insertCursor = -1;

        public CustomerPlan(IReadOnlyList<ICustomerStep> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = new List<ICustomerStep>(steps);   // copy: the external list must not mutate the plan
        }

        public bool IsDone => _index >= _steps.Count;

        public ICustomerStep Current => _index < _steps.Count ? _steps[_index] : null;

        public void Advance()
        {
            _index++;
            _insertCursor = -1;
        }

        /// <summary>Forces the plan to a completed state (no current step). Used by the abort fallback
        /// when there is no closing step ahead, so the customer cannot re-enter the same step.</summary>
        public void Finish()
        {
            _index = _steps.Count;
            _insertCursor = -1;
        }

        /// <summary>Repositions to the first <see cref="IClosingStep"/> ahead of the current step.
        /// Skipped steps were never entered, so the caller does not Exit them. Returns false when no
        /// closing step lies ahead (the caller then handles the degenerate finish).</summary>
        public bool SkipToClosing()
        {
            for (var i = _index + 1; i < _steps.Count; i++)
            {
                if (_steps[i] is IClosingStep)
                {
                    _index = i;
                    _insertCursor = -1;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Inserts <paramref name="step"/> right after the current step (FIFO across repeated
        /// calls in the same tick). No-op returning false when done or while on a closing step.</summary>
        public bool InsertNext(ICustomerStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (IsDone || Current is IClosingStep) return false;

            var at = _insertCursor >= 0 ? _insertCursor : _index + 1;
            _steps.Insert(at, step);
            _insertCursor = at + 1;
            return true;
        }

        /// <summary>Inserts <paramref name="step"/> just before the first closing step ahead (or at the
        /// end if none). FIFO across repeated calls. No-op returning false when done or on a closing step.</summary>
        public bool InsertBeforeClosing(ICustomerStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (IsDone || Current is IClosingStep) return false;

            // Re-find each call: inserting before the (shifted) closing step keeps repeated calls FIFO.
            var at = _steps.Count;
            for (var i = _index + 1; i < _steps.Count; i++)
            {
                if (_steps[i] is IClosingStep)
                {
                    at = i;
                    break;
                }
            }

            _steps.Insert(at, step);
            return true;
        }
    }
}
