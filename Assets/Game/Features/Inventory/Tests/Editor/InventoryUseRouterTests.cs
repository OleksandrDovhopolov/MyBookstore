using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.Inventory.Services;
using Game.Inventory.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Inventory.Tests.Editor
{
    public sealed class InventoryUseRouterTests
    {
        private const string DecorCategory = "decor";
        private const string BookCategory = "book";

        private sealed class StubHandler : IInventoryItemUseHandler
        {
            public StubHandler(string category, InventoryUseResult result) { SupportedCategoryId = category; _result = result; }
            public string SupportedCategoryId { get; }
            private readonly InventoryUseResult _result;
            public UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct) => UniTask.FromResult(_result);
        }

        private static InventoryService BuildInventory()
        {
            var save = new FakeSaveService();
            var repo = new FakeInventoryRepository();
            var registry = new ItemCategoryRegistry();
            registry.Register(new ItemCategory(BookCategory, ItemStackingMode.Unique, "Books"));
            registry.Register(new ItemCategory(DecorCategory, ItemStackingMode.Unique, "Decor"));
            var svc = new InventoryService(save, repo, registry);
            svc.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            return svc;
        }

        [Test]
        public void HandlerConsumesItem_RemoveCalled()
        {
            var inv = BuildInventory();
            inv.AddAsync("decor_plant", DecorCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            var handler = new StubHandler(DecorCategory, InventoryUseResult.Ok(consume: true, message: "used"));
            var router = new InventoryUseRouter(inv, new List<IInventoryItemUseHandler> { handler });

            var result = router.UseAsync("decor_plant", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ConsumeAfterUse);
            Assert.IsFalse(inv.Has("decor_plant"));
        }

        [Test]
        public void HandlerKeepsItem_NotRemoved()
        {
            var inv = BuildInventory();
            inv.AddAsync("decor_plant", DecorCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            var handler = new StubHandler(DecorCategory, InventoryUseResult.Ok(consume: false));
            var router = new InventoryUseRouter(inv, new List<IInventoryItemUseHandler> { handler });

            var result = router.UseAsync("decor_plant", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.ConsumeAfterUse);
            Assert.IsTrue(inv.Has("decor_plant"));
        }

        [Test]
        public void ItemMissing_ReturnsNotOwned()
        {
            var inv = BuildInventory();
            var router = new InventoryUseRouter(inv, new List<IInventoryItemUseHandler>());

            var result = router.UseAsync("nope", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual("not owned", result.Message);
        }

        [Test]
        public void NoHandlerForCategory_ReturnsFail()
        {
            var inv = BuildInventory();
            inv.AddAsync("b1", BookCategory, 1, CancellationToken.None).GetAwaiter().GetResult();

            var router = new InventoryUseRouter(inv, new List<IInventoryItemUseHandler>());

            var result = router.UseAsync("b1", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            StringAssert.Contains("no handler", result.Message);
            Assert.IsTrue(inv.Has("b1"));
        }
    }
}
