using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Decor;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using Game.Inventory.API;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Decor.Tests.Editor
{
    public sealed class DecorRowSourceTests
    {
        [Test]
        public void DecorRowSource_BuildsDecorRows_AndHighlightsPlacedDecor()
        {
            var inventory = new FakeInventoryService();
            inventory.Seed("globe", InventoryCategories.Decor);
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { new DecorConfig { Id = "globe" } });
            var placement = new FakeDecorPlacementService()
                .Place("slot_1", "globe");

            var rows = new DecorRowSource(inventory, configs, placement).BuildRows().ToList();

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("globe", rows[0].SpriteId);
            Assert.AreEqual(0, rows[0].Count);
            Assert.AreEqual("globe", rows[0].ItemId);
            Assert.AreEqual(InventoryRowStyle.Decor, rows[0].Style);
            Assert.AreEqual(InventoryCategories.Decor, new DecorRowSource(inventory, configs, placement).CategoryId);
            Assert.IsTrue(rows[0].IsHighlighted);
        }

        [Test]
        public void DecorRowSource_MissingConfig_SkipsRowAndLogsWarning()
        {
            var inventory = new FakeInventoryService();
            inventory.Seed("missing_decor", InventoryCategories.Decor);
            var configs = new FakeConfigsService();

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Missing DecorConfig.*missing_decor"));

            var rows = new DecorRowSource(inventory, configs, new FakeDecorPlacementService()).BuildRows().ToList();

            Assert.AreEqual(0, rows.Count);
        }

        [Test]
        public void DecorRowSource_HasExpectedOrder()
        {
            var source = new DecorRowSource(new FakeInventoryService(), new FakeConfigsService(), new FakeDecorPlacementService());

            Assert.AreEqual(10, source.Order);
        }

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            private readonly List<DecorPlacementEntry> _placements = new();

#pragma warning disable CS0067
            public event Action PlacementChanged;
            public event Action<DecorPlacementChange> PlacementActionPerformed;
#pragma warning restore CS0067

            public FakeDecorPlacementService Place(string slotId, string decorId)
            {
                _placements.Add(new DecorPlacementEntry { SlotId = slotId, DecorId = decorId });
                return this;
            }

            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => _placements;

            public string GetDecorInSlot(string slotId)
                => _placements.FirstOrDefault(p => string.Equals(p.SlotId, slotId, StringComparison.Ordinal))?.DecorId;

            public IReadOnlyList<string> GetActiveDecorIds()
                => _placements.Select(p => p.DecorId).ToList();

            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct) => default;

            public UniTask<DecorPlacementResult> ReplaceAsync(string decorId, string slotId, CancellationToken ct) => default;

            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
