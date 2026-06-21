using System.Linq;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor.Services
{
    public sealed class SalesShelfStateServiceTests
    {
        [Test]
        public void FirstLoad_CreatesEmptyState()
        {
            var service = new SalesShelfStateService(new FakeSaveService());

            Assert.IsNotNull(service.CurrentState);
            Assert.IsEmpty(service.ShelfBookIds);
            Assert.IsFalse(service.IsSold("b1"));
        }

        [Test]
        public void SetShelf_PersistsDistinctUnsoldBooks()
        {
            var save = new FakeSaveService();
            var service = new SalesShelfStateService(save);

            service.SetShelfAsync(new[] { "a", "b", "a" }, CancellationToken.None).GetAwaiter().GetResult();

            var state = save.GetModuleAsync<SalesShelfState>(SalesSaveKeys.ShelfState, CancellationToken.None)
                .GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "a", "b" }, state.ShelfBookIds);
        }

        [Test]
        public void MarkSold_RemovesFromShelf_AndAddsToSold()
        {
            var save = new FakeSaveService();
            var service = new SalesShelfStateService(save);
            service.SetShelfAsync(new[] { "a", "b" }, CancellationToken.None).GetAwaiter().GetResult();

            service.MarkSoldAsync("a", CancellationToken.None).GetAwaiter().GetResult();

            var state = save.GetModuleAsync<SalesShelfState>(SalesSaveKeys.ShelfState, CancellationToken.None)
                .GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new[] { "b" }, state.ShelfBookIds);
            CollectionAssert.AreEqual(new[] { "a" }, state.SoldBookIds);
        }

        [Test]
        public void MarkSold_IsIdempotent()
        {
            var save = new FakeSaveService();
            var service = new SalesShelfStateService(save);
            service.SetShelfAsync(new[] { "a", "b" }, CancellationToken.None).GetAwaiter().GetResult();

            service.MarkSoldAsync("a", CancellationToken.None).GetAwaiter().GetResult();
            service.MarkSoldAsync("a", CancellationToken.None).GetAwaiter().GetResult();

            var state = save.GetModuleAsync<SalesShelfState>(SalesSaveKeys.ShelfState, CancellationToken.None)
                .GetAwaiter().GetResult();
            Assert.AreEqual(1, state.SoldBookIds.Count(id => id == "a"));
            CollectionAssert.AreEqual(new[] { "b" }, state.ShelfBookIds);
        }
    }
}
