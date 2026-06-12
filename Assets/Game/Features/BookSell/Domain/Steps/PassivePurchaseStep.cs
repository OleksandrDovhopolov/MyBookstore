using System;
using System.Collections.Generic;
using Book.Sell.API;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// One passive purchase attempt: the customer browses for a while, targets a demand-matching
    /// book (reserve-on-target), then commits the sale after a short delay. A miss (nothing matches)
    /// completes the step without a sale — the customer just moves on to the next plan step (skip book).
    /// </summary>
    public sealed class PassivePurchaseStep : ICustomerStep
    {
        private enum Sub { Browse, Commit }

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
            self.SetPhase(CustomerPhase.Browsing, ctx);
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            _t += dt;

            if (_sub == Sub.Browse)
            {
                if (_t < ctx.Tuning.BrowseDuration) return StepStatus.Running;

                var candidate = ctx.PassiveSelector.PickPassiveSale(
                    ctx.Shelf.AvailableForSelection(), ctx.Location, ctx.Random);

                // Miss: nothing on the shelf matches the location demand → skip this book, continue plan.
                if (candidate == null) return StepStatus.Completed;

                // Reserve-on-target. If the reservation race is lost, treat as a miss.
                if (!ctx.Shelf.Reserve(candidate.Book.BookId)) return StepStatus.Completed;

                _targetId = candidate.Book.BookId;
                _matchedGenres = candidate.MatchedGenres;
                _matchedTags = candidate.MatchedTags;
                _sub = Sub.Commit;
                _t = 0f;
                ctx.Sink?.OnBookReserved(self, _targetId);
                return StepStatus.Running;
            }

            // Sub.Commit
            if (_t < ctx.Tuning.PassiveCommitDelay) return StepStatus.Running;

            var book = ctx.Shelf.Find(_targetId);
            var gold = book != null ? book.Config.BasePrice : 0;
            ctx.Shelf.CommitSale(_targetId);

            var saleEvent = new PassiveSaleEvent(_targetId, gold, _matchedGenres, _matchedTags);
            ctx.Sink?.OnPassiveSale(self, saleEvent);
            return StepStatus.Completed;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
            // Safety net: if the step is aborted while a reservation is still held (not yet committed),
            // release it so the book returns to the shelf.
            if (_targetId != null && ctx.Shelf.IsReserved(_targetId))
            {
                ctx.Shelf.ReleaseReserve(_targetId);
                ctx.Sink?.OnBookReleased(self, _targetId);
            }
        }
    }
}
