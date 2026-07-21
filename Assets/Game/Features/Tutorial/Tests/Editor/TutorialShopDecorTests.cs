using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using NUnit.Framework;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialShopDecorTests
    {
        [Test]
        public void IsEligible_FalseWithoutDecor()
        {
            var sequence = new TutorialShopDecor(new FakeInventoryService());

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_TrueWhenDecorExists()
        {
            var inventory = new FakeInventoryService();
            inventory.DecorItems.Add(new InventoryItem("globe", InventoryCategories.Decor, 1));
            var sequence = new TutorialShopDecor(inventory);

            Assert.IsTrue(sequence.IsEligible());
        }

        [Test]
        public void GetSteps_ContainsSingleShopDecorLogStep()
        {
            var sequence = new TutorialShopDecor(new FakeInventoryService());

            var steps = sequence.GetSteps();

            Assert.AreEqual(TutorialSequenceIds.ShopDecor, sequence.Id);
            Assert.AreEqual(40, sequence.Priority);
            Assert.AreEqual(TutorialContext.Hub, sequence.Context);
            Assert.AreEqual(TutorialTrigger.HubReady, sequence.Trigger);
            Assert.AreEqual(TutorialResumePolicy.Restart, sequence.ResumePolicy);
            Assert.AreEqual(1, steps.Count);
            Assert.IsInstanceOf<TutorialLogStep>(steps[0]);
            Assert.AreEqual("shop_decor_log", steps[0].Id);
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public List<InventoryItem> DecorItems { get; } = new();

            public IReadOnlyList<InventoryItem> GetAll() => DecorItems;

            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
                => string.Equals(categoryId, InventoryCategories.Decor, StringComparison.Ordinal)
                    ? DecorItems
                    : Array.Empty<InventoryItem>();

            public bool Has(string itemId) => false;
            public int GetCount(string itemId) => 0;
            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);

#pragma warning disable CS0067
            public event Action<InventoryChangeEvent> Changed;
#pragma warning restore CS0067
        }
    }
}
