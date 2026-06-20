using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class PassivePurchaseStepTests
    {
        private static SalesTuning Tuning() => new()
        {
            ApproachDuration = 0f,
            BrowseDuration = 1f,
            PassiveCommitDelay = 1f,
            PassiveFailureFeedbackDuration = 1f,
            PassiveSaleFeedbackDuration = 1f,
            SpawnInterval = 0f
        };

        [Test]
        public void ChanceHit_ReservesCommits_ThenHoldsSaleFeedback()
        {
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi", price: 80));
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                sink,
                tuning: Tuning(),
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var step = new PassivePurchaseStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);

            // Browse phase done → target + reserve, still running.
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 1f));
            Assert.IsTrue(shelf.IsReserved("b1"), "Book is reserved during the commit window.");
            Assert.IsEmpty(shelf.AvailableForSelection(), "Reserved book is hidden from selection.");
            Assert.IsEmpty(sink.PassiveSales, "No sale until the commit delay passes.");

            // Commit window done → sale fires, but the step holds sale feedback (still running).
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 1f),
                "Bought-book feedback is held so the HUD shows it before the next attempt.");
            Assert.AreEqual(1, sink.PassiveSales.Count);
            Assert.AreEqual("b1", sink.PassiveSales[0].evt.BookId);
            Assert.AreEqual(80, sink.PassiveSales[0].evt.GoldEarned);
            Assert.AreEqual(ShelfBookState.SoldOut, shelf.Find("b1").State);
            Assert.IsFalse(shelf.IsReserved("b1"));

            // Sale-feedback hold elapses → step completes.
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f));
            Assert.AreEqual(1, sink.PassiveSales.Count, "Sale fires exactly once.");
            Assert.AreEqual(1, self.PassivePurchaseCount);
        }

        [Test]
        public void ChanceMiss_HoldsFailedFeedback_ThenCompletesAsMiss()
        {
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                sink,
                tuning: Tuning(),
                passiveSelector: SalesTestKit.AlwaysMissPassiveSelector());
            var step = new PassivePurchaseStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 1f),
                "Miss shows Failed first instead of immediately closing the visit.");
            Assert.IsEmpty(sink.PassiveSales);
            Assert.AreEqual(1, sink.PassiveFailures.Count);
            Assert.AreSame(self, sink.PassiveFailures[0]);
            Assert.AreEqual(ShelfBookState.Available, shelf.Find("b1").State);

            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 0.5f));
            Assert.AreEqual(1, sink.PassiveFailures.Count, "Failed feedback is emitted once.");

            Assert.AreEqual(StepStatus.CompletedAndLeave, step.Tick(self, ctx, 0.5f),
                "After the Failed hold, the shopping cycle ends and closing steps can run.");
            Assert.AreEqual(1, sink.PassiveFailures.Count, "Completing the hold does not emit another failure.");
        }

        [Test]
        public void ReserveRaceMiss_HoldsFailedFeedback_ThenCompletesAsMiss()
        {
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi"));
            shelf.Reserve("b1");
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                sink,
                tuning: Tuning(),
                passiveSelector: new FixedCandidateSelector(shelf.Find("b1")));
            var step = new PassivePurchaseStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);

            Assert.AreEqual(StepStatus.Running, step.Tick(self, ctx, 1f));
            Assert.AreEqual(1, sink.PassiveFailures.Count);
            Assert.IsTrue(shelf.IsReserved("b1"), "The step did not own the reservation, so it must not release it.");

            Assert.AreEqual(StepStatus.CompletedAndLeave, step.Tick(self, ctx, 1f));
            Assert.AreEqual(1, sink.PassiveFailures.Count);
        }

        [Test]
        public void SaleEvent_ReportsGenre_TagsAreEmpty()
        {
            var sink = new RecordingSink();
            var shelf = SalesTestKit.Shelf(SalesTestKit.Book("b1", genre: "sci-fi", tags: new[] { "space", "survival" }));
            var ctx = SalesTestKit.Context(
                shelf,
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }, demandTags: new[] { "space" }),
                sink,
                tuning: Tuning(),
                passiveSelector: SalesTestKit.AlwaysHitPassiveSelector());
            var step = new PassivePurchaseStep();
            var self = new Customer("c1", new[] { step });

            step.Enter(self, ctx);
            step.Tick(self, ctx, 1f);
            step.Tick(self, ctx, 1f);

            var evt = sink.PassiveSales[0].evt;
            CollectionAssert.Contains(evt.MatchedGenres.ToArray(), "sci-fi");
            Assert.IsEmpty(evt.MatchedTags, "Passive sales no longer report tag matches (ADR-0004).");
        }

        private sealed class FixedCandidateSelector : IPassiveSaleSelector
        {
            private readonly ShelfBook _book;

            public FixedCandidateSelector(ShelfBook book)
            {
                _book = book;
            }

            public PassiveSaleCandidate PickPassiveSale(
                System.Collections.Generic.IReadOnlyList<ShelfBook> shelf,
                LocationConfig location,
                System.Collections.Generic.IReadOnlyList<string> activeDecorIds,
                ISalesRandom random)
                => new(_book, new[] { _book.Config.Genre }, System.Array.Empty<string>());
        }
    }
}
