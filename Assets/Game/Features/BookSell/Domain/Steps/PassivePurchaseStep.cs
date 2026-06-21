using System;
using System.Collections.Generic;
using Book.Sell.API;
using UnityEngine;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// One passive purchase attempt: the customer browses for a while, targets a demand-matching
    /// book (reserve-on-target), then commits the sale after a short delay. A miss (nothing matches)
    /// holds failed-purchase feedback, then closes the customer's shopping cycle.
    /// </summary>
    public sealed class PassivePurchaseStep : ICustomerStep
    {
        private const string LogPrefix = "[Sales.Passive]";

        private enum Sub { Browse, Commit, FailedFeedback, SaleFeedback }

        private Sub _sub;
        private float _t;
        private string _targetId;
        private IReadOnlyList<string> _matchedGenres = Array.Empty<string>();
        private IReadOnlyList<string> _matchedTags = Array.Empty<string>();

        public void Enter(Customer self, CustomerContext ctx)
        {
            _sub = Sub.Browse;
            _t = 0f;
            _targetId = null;
            self.SetPhase(CustomerPhase.Browsing, ctx, forceNotify: true);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _t += dt;

            if (_sub == Sub.Browse)
            {
                if (_t < ctx.Tuning.BrowseDuration) return StepStatus.Running;

                var available = ctx.Shelf.AvailableForSelection();
                Debug.Log($"{LogPrefix} customer={self.Id} browsing → {available.Count} book(s) available, rolling the gate");

                var candidate = ctx.PassiveSelector.PickPassiveSale(
                    available, ctx.Location, ctx.ActiveDecorIds, ctx.Random);

                // Miss: nothing on the shelf matches → the visit's shopping cycle ends, customer leaves.
                if (candidate == null)
                {
                    Debug.Log($"{LogPrefix} customer={self.Id} passive attempt MISSED → leaving");
                    return BeginFailedFeedback(self, ctx);
                }

                // Reserve-on-target. If the reservation race is lost, the cycle ends, customer leaves.
                if (!ctx.Shelf.Reserve(candidate.Book.BookId))
                {
                    Debug.Log($"{LogPrefix} customer={self.Id} lost the reserve race for book={candidate.Book.BookId} → leaving");
                    return BeginFailedFeedback(self, ctx);
                }

                _targetId = candidate.Book.BookId;
                _matchedGenres = candidate.MatchedGenres;
                _matchedTags = candidate.MatchedTags;
                _sub = Sub.Commit;
                _t = 0f;
                ctx.Sink?.OnBookReserved(self, _targetId);
                return StepStatus.Running;
            }

            if (_sub == Sub.FailedFeedback)
                return _t >= ctx.Tuning.PassiveFailureFeedbackDuration
                    ? StepStatus.CompletedAndLeave
                    : StepStatus.Running;

            // Sub.SaleFeedback: hold "bought book" so the HUD shows it before the next attempt's "Choosing".
            if (_sub == Sub.SaleFeedback)
                return _t >= ctx.Tuning.PassiveSaleFeedbackDuration
                    ? StepStatus.Completed
                    : StepStatus.Running;

            // Sub.Commit
            if (_t < ctx.Tuning.PassiveCommitDelay) return StepStatus.Running;

            var book = ctx.Shelf.Find(_targetId);
            var gold = book != null ? book.Config.BasePrice : 0;
            ctx.Shelf.CommitSale(_targetId);

            var saleEvent = new PassiveSaleEvent(_targetId, gold, _matchedGenres, _matchedTags);
            ctx.Sink?.OnPassiveSale(self, saleEvent);
            self.RegisterPurchasedBook();
            Debug.Log($"{LogPrefix} customer={self.Id} BOUGHT book={_targetId} gold={gold} (books bought so far: {self.PurchasedBookCount})");

            return BeginSaleFeedback(ctx);
        }

        private StepStatus BeginSaleFeedback(CustomerContext ctx)
        {
            if (ctx.Tuning.PassiveSaleFeedbackDuration <= 0f)
                return StepStatus.Completed;

            _sub = Sub.SaleFeedback;
            _t = 0f;
            return StepStatus.Running;
        }

        private StepStatus BeginFailedFeedback(Customer self, CustomerContext ctx)
        {
            ctx.Sink?.OnPassivePurchaseFailed(self);

            if (ctx.Tuning.PassiveFailureFeedbackDuration <= 0f)
                return StepStatus.CompletedAndLeave;

            _sub = Sub.FailedFeedback;
            _t = 0f;
            return StepStatus.Running;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
            // Safety net: if the step is aborted while a reservation is still held (not yet committed),
            // release it so the book returns to the shelf.
            if (_targetId != null && ctx.Shelf.IsReserved(_targetId))
            {
                ctx.Shelf.ReleaseReserve(_targetId);
                ctx.Sink?.OnBookReleased(self, _targetId);
                ctx.Sink?.OnPassivePurchaseFailed(self);
            }
        }
    }
}
