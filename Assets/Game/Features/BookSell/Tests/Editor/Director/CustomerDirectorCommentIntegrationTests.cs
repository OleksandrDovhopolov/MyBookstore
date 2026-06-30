using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services.Director;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Director
{
    public sealed class CustomerDirectorCommentIntegrationTests
    {
        [Test]
        public void PassiveSale_InsertsCommentAfterSaleFeedback_ThenContinuesToClosing()
        {
            var customer = new Customer(
                "c1",
                new ICustomerStep[]
                {
                    new PassivePurchaseStep(),
                    new CompletePurchaseStep(),
                    new LeaveStep(0f)
                });
            var sink = new RecordingSink();
            var tuning = SalesTestKit.FastTuning();
            tuning.PassiveCommitDelay = 0f;
            tuning.PassiveSaleFeedbackDuration = 1f;
            tuning.PassiveSaleCommentChance = 1f;
            tuning.CommentDuration = 0f;
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("book_1", "Fantasy", price: 10)),
                SalesTestKit.Location(demandGenres: new[] { "Fantasy" }),
                sink,
                random: new FakeSalesRandom(),
                tuning: tuning);
            var director = new CustomerDirector(new IPassiveSaleRule[] { new PassiveSaleCommentRule() });

            customer.Tick(ctx, 0f); // browse + reserve
            customer.Tick(ctx, 0f); // commit sale, still enters sale feedback

            Assert.AreEqual(1, sink.PassiveSales.Count);
            director.OnPassiveSale(customer, sink.PassiveSales[0].evt, ctx);
            Assert.IsInstanceOf<PassivePurchaseStep>(customer.CurrentStep, "Passive sale feedback still holds current step.");

            customer.Tick(ctx, 1f); // finish sale feedback and advance
            Assert.IsInstanceOf<CommentStep>(customer.CurrentStep);

            customer.Tick(ctx, 0f); // enter and complete comment
            Assert.AreEqual(1, sink.Comments.Count);
            Assert.AreEqual(1, customer.PurchasedBookCount);
            Assert.IsInstanceOf<CompletePurchaseStep>(customer.CurrentStep);
        }

        [Test]
        public void ChanceZero_KeepsOriginalPlan()
        {
            var customer = new Customer(
                "c1",
                new ICustomerStep[]
                {
                    new PassivePurchaseStep(),
                    new CompletePurchaseStep(),
                    new LeaveStep(0f)
                });
            var sink = new RecordingSink();
            var tuning = SalesTestKit.FastTuning();
            tuning.PassiveSaleCommentChance = 0f;
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("book_1", "Fantasy", price: 10)),
                SalesTestKit.Location(demandGenres: new[] { "Fantasy" }),
                sink,
                random: new FakeSalesRandom(),
                tuning: tuning);
            var director = new CustomerDirector(new IPassiveSaleRule[] { new PassiveSaleCommentRule() });

            customer.Tick(ctx, 0f);
            customer.Tick(ctx, 0f);
            director.OnPassiveSale(customer, sink.PassiveSales[0].evt, ctx);

            Assert.IsEmpty(sink.Comments);
            Assert.IsInstanceOf<CompletePurchaseStep>(customer.CurrentStep);
        }
    }
}
