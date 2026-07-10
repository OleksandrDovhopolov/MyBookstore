using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Inventory.API;
using Game.Inventory.Conditions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Inventory.Tests.Editor
{
    /// <summary>
    /// "haveItem" condition parsed by the engine → evaluated against a fake inventory seam.
    /// </summary>
    public sealed class HaveItemConditionTests
    {
        private const string Knife = "knife";

        private static IConditionParser Parser(FakeInventoryService inv)
            => new ConditionParser(new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new HaveItemConditionFactory(inv)
            }));

        private static JObject Node(string itemId, int? min = null)
        {
            var node = new JObject { ["type"] = HaveItemConditionFactory.TypeId, ["itemId"] = itemId };
            if (min.HasValue) node["min"] = min.Value;
            return node;
        }

        [Test]
        public void Met_WhenCountReachesMin()
        {
            var inv = new FakeInventoryService();
            var condition = Parser(inv).Parse(Node(Knife, 2));

            Assert.IsFalse(condition.Evaluate().IsMet);

            inv.Counts[Knife] = 2;
            var result = condition.Evaluate();
            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(2, result.Current);
            Assert.AreEqual(2, result.Target);
            Assert.AreEqual($"haveItem.{Knife}", result.ReasonKey);
        }

        [Test]
        public void DefaultMin_IsOne()
        {
            var inv = new FakeInventoryService();
            var condition = Parser(inv).Parse(Node(Knife)); // no min

            Assert.IsFalse(condition.Evaluate().IsMet);
            inv.Counts[Knife] = 1;
            Assert.IsTrue(condition.Evaluate().IsMet);
        }

        [Test]
        public void NonPositiveMin_NormalizedToOne_NotAlwaysMet()
        {
            var inv = new FakeInventoryService();
            var condition = Parser(inv).Parse(Node(Knife, 0));

            // min<=0 must become 1, so an empty inventory is NOT met.
            Assert.IsFalse(condition.Evaluate().IsMet);
            inv.Counts[Knife] = 1;
            Assert.IsTrue(condition.Evaluate().IsMet);
        }

        [Test]
        public void EmptyItemId_FailsClosed()
        {
            var inv = new FakeInventoryService();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("factory 'haveItem' failed"));

            Assert.IsFalse(Parser(inv).Parse(Node("")).Evaluate().IsMet);
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            public readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);

            public bool Has(string itemId) => GetCount(itemId) > 0;
            public int GetCount(string itemId)
                => itemId != null && Counts.TryGetValue(itemId, out var c) ? c : 0;

            public IReadOnlyList<InventoryItem> GetAll() => Array.Empty<InventoryItem>();
            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId) => Array.Empty<InventoryItem>();
            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);

#pragma warning disable CS0067
            public event Action<InventoryChangeEvent> Changed;
#pragma warning restore CS0067
        }
    }
}
