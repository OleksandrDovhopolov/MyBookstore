using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Services.Director;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Director
{
    public sealed class PassiveSaleCommentRuleTests
    {
        [Test]
        public void ChanceZero_DoesNotInsertOrConsumeRandom()
        {
            var random = new FakeSalesRandom().EnqueueDouble(0.0, 0.42);
            var customer = CustomerWithCurrentPassive();
            var ctx = Context(random, new SalesTuning { PassiveSaleCommentChance = 0f, CommentDuration = 0f });
            var rule = new PassiveSaleCommentRule();

            rule.OnPassiveSale(customer, Sale(), ctx);

            Assert.IsInstanceOf<PassivePurchaseStep>(customer.CurrentStep);
            Assert.AreEqual(0.0, random.NextDouble(), "Chance 0 must preserve the old random stream.");
        }

        [Test]
        public void ChanceOne_InsertsCommentWithoutRandomRoll()
        {
            var random = new FakeSalesRandom().EnqueueDouble(0.42);
            var customer = CustomerWithCurrentPassive();
            var ctx = Context(random, new SalesTuning { PassiveSaleCommentChance = 1f, CommentDuration = 0f });
            var rule = new PassiveSaleCommentRule();

            rule.OnPassiveSale(customer, Sale(), ctx);
            customer.ForceCompleteCurrentStep(ctx);

            var step = customer.CurrentStep as CommentStep;
            Assert.NotNull(step);
            Assert.AreEqual("book_1", step.Payload.BookId);
            Assert.AreEqual("Fantasy", step.Payload.Genre);
            Assert.AreEqual(0.42, random.NextDouble(), "Chance 1 should also avoid an unnecessary random roll.");
        }

        [Test]
        public void RollMiss_DoesNotInsert()
        {
            var customer = CustomerWithCurrentPassive();
            var ctx = Context(
                new FakeSalesRandom().EnqueueDouble(0.99),
                new SalesTuning { PassiveSaleCommentChance = 0.5f, CommentDuration = 0f });
            var rule = new PassiveSaleCommentRule();

            rule.OnPassiveSale(customer, Sale(), ctx);
            customer.ForceCompleteCurrentStep(ctx);

            Assert.IsInstanceOf<CompletePurchaseStep>(customer.CurrentStep);
        }

        private static Customer CustomerWithCurrentPassive()
            => new(
                "c1",
                new ICustomerStep[]
                {
                    new PassivePurchaseStep(),
                    new CompletePurchaseStep(),
                    new LeaveStep(0f)
                });

        private static CustomerContext Context(ISalesRandom random, SalesTuning tuning)
            => SalesTestKit.Context(
                SalesTestKit.Shelf(SalesTestKit.Book("book_1", "Fantasy")),
                SalesTestKit.Location(demandGenres: new[] { "Fantasy" }),
                new RecordingSink(),
                random: random,
                tuning: tuning);

        private static PassiveSaleEvent Sale()
            => new("book_1", 10, new List<string> { "Fantasy" });
    }
}
