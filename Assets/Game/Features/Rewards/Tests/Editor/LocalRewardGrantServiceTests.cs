using System.Collections.Generic;
using System.Threading;
using Game.Rewards.API;
using Game.Rewards.Services;
using Game.Rewards.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Rewards.Tests.Editor
{
    public sealed class LocalRewardGrantServiceTests
    {
        private const string SourceShopFoo = "shop:foo";

        private static (LocalRewardGrantService svc, FakeResourcesService resources, FakeInventoryService inventory)
            Build(params IRewardSpecExpander[] expanders)
        {
            var resources = new FakeResourcesService();
            var inventory = new FakeInventoryService();
            var svc = new LocalRewardGrantService(resources, inventory, expanders);
            return (svc, resources, inventory);
        }

        [Test]
        public void Grant_Resource_AddsToResources()
        {
            var (svc, resources, inventory) = Build();
            var spec = new RewardSpec("test_gold", new[] { RewardItem.Resource("gold", 10) });

            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, resources.GetAmount("gold"));
            Assert.AreEqual(1, resources.AddCalls.Count);
            Assert.AreEqual(("gold", 10, SourceShopFoo), resources.AddCalls[0]);
            Assert.AreEqual(0, inventory.AddCalls.Count);
        }

        [Test]
        public void Grant_InventoryItem_AddsToInventory()
        {
            var (svc, resources, inventory) = Build();
            var spec = new RewardSpec("test_book",
                new[] { RewardItem.InventoryItem("book_001", "book", 1) });

            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, inventory.GetCount("book_001"));
            Assert.AreEqual(("book_001", "book", 1), inventory.AddCalls[0]);
            Assert.AreEqual(0, resources.AddCalls.Count);
        }

        [Test]
        public void Grant_MultipleItems_AppliesAll()
        {
            var (svc, resources, inventory) = Build();
            var spec = new RewardSpec("test_mixed", new[]
            {
                RewardItem.Resource("gold", 25),
                RewardItem.InventoryItem("book_001", "book", 1),
                RewardItem.InventoryItem("decor_vintage_globe", "decor", 1)
            });

            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(25, resources.GetAmount("gold"));
            Assert.AreEqual(1, inventory.GetCount("book_001"));
            Assert.AreEqual(1, inventory.GetCount("decor_vintage_globe"));
        }

        [Test]
        public void Grant_WithoutExpander_GrantedEqualsRequested()
        {
            var (svc, _, _) = Build();
            var spec = new RewardSpec("test_id", new[] { RewardItem.Resource("gold", 5) });

            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreSame(spec, result.Granted);
        }

        [Test]
        public void Grant_WithMatchingExpander_AppliesExpandedSpec()
        {
            var expanded = new RewardSpec("expanded_box", new[]
            {
                RewardItem.InventoryItem("book_001", "book", 1),
                RewardItem.InventoryItem("book_002", "book", 1)
            });
            var expander = new StubRewardSpecExpander(
                s => s.Id == "book_box",
                _ => expanded);
            var (svc, _, inventory) = Build(expander);

            var requested = new RewardSpec("book_box", new[] { RewardItem.InventoryItem("placeholder", "book", 1) });
            var result = svc.GrantAsync(requested, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreSame(expanded, result.Granted);
            Assert.AreEqual(1, expander.ExpandCalls);
            Assert.AreEqual(1, inventory.GetCount("book_001"));
            Assert.AreEqual(1, inventory.GetCount("book_002"));
            Assert.AreEqual(0, inventory.GetCount("placeholder"));
        }

        [Test]
        public void Grant_NonMatchingExpander_LeavesSpecUnchanged()
        {
            var expander = new StubRewardSpecExpander(s => false, s => s);
            var (svc, _, inventory) = Build(expander);

            var spec = new RewardSpec("plain", new[] { RewardItem.InventoryItem("book_001", "book", 1) });
            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreSame(spec, result.Granted);
            Assert.AreEqual(0, expander.ExpandCalls);
            Assert.AreEqual(1, inventory.GetCount("book_001"));
        }

        [Test]
        public void Grant_ZeroAmount_SkipsItem()
        {
            var (svc, resources, inventory) = Build();
            var spec = new RewardSpec("test_zero", new[]
            {
                RewardItem.Resource("gold", 0),
                RewardItem.InventoryItem("book_001", "book", 0)
            });

            var result = svc.GrantAsync(spec, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, resources.AddCalls.Count);
            Assert.AreEqual(0, inventory.AddCalls.Count);
        }

        [Test]
        public void Grant_NullSpec_ReturnsFail()
        {
            var (svc, _, _) = Build();

            var result = svc.GrantAsync(null, SourceShopFoo, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.FailureReason);
        }
    }
}
