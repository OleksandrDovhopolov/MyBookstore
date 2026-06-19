using System.Linq;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Steps
{
    public sealed class PassivePurchaseStepTests
    {
        private static SalesTuning Tuning() => new()
        {
            ApproachDuration = 0f, BrowseDuration = 1f, PassiveCommitDelay = 1f, SpawnInterval = 0f
        };

        [Test]
        public void ChanceHit_ReservesDuringWindow_ThenCommitsSale()
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

            // Commit window done → sale.
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f));
            Assert.AreEqual(1, sink.PassiveSales.Count);
            Assert.AreEqual("b1", sink.PassiveSales[0].evt.BookId);
            Assert.AreEqual(80, sink.PassiveSales[0].evt.GoldEarned);
            Assert.AreEqual(ShelfBookState.SoldOut, shelf.Find("b1").State);
            Assert.IsFalse(shelf.IsReserved("b1"));
        }

        [Test]
        public void ChanceMiss_CompletesAsMiss_NoSale_BookStaysAvailable()
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
            Assert.AreEqual(StepStatus.Completed, step.Tick(self, ctx, 1f), "Miss completes the step.");
            Assert.IsEmpty(sink.PassiveSales);
            Assert.AreEqual(1, sink.PassiveFailures.Count);
            Assert.AreSame(self, sink.PassiveFailures[0]);
            Assert.AreEqual(ShelfBookState.Available, shelf.Find("b1").State);
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
    }
}
