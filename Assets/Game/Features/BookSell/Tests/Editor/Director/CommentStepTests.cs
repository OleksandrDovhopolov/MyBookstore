using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Director
{
    public sealed class CommentStepTests
    {
        [Test]
        public void Enter_EmitsCommentPayload_AndCompletesAfterDuration()
        {
            var payload = new CustomerCommentPayload("book_1", "Fantasy");
            var customer = new Customer("c1", new ICustomerStep[] { new CommentStep(payload) });
            var sink = new RecordingSink();
            var ctx = SalesTestKit.Context(
                SalesTestKit.Shelf(),
                SalesTestKit.Location(),
                sink,
                tuning: new SalesTuning { CommentDuration = 0.5f });

            customer.Tick(ctx, 0.25f);

            Assert.AreEqual(1, sink.Comments.Count);
            Assert.AreSame(payload, sink.Comments[0].payload);
            Assert.IsFalse(customer.IsDone);

            customer.Tick(ctx, 0.25f);

            Assert.IsTrue(customer.IsDone);
        }
    }
}
