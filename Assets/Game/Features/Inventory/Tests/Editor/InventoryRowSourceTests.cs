using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Inventory.Services;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Inventory.Tests.Editor
{
    public sealed class InventoryRowSourceTests
    {
        [Test]
        public void BookGenreRowSource_ReturnsAllGenres_AndSumsOwnedBooks()
        {
            var inventory = new FakeInventoryService()
                .Seed("fantasy_1", InventoryCategories.Book, 1)
                .Seed("fantasy_2", InventoryCategories.Book, 3)
                .Seed("crime_1", InventoryCategories.Book, 2);
            var configs = new FakeConfigsService()
                .Set(new BookConfig { Id = "fantasy_1", Genres = new[] { BookGenre.Fantasy.ToConfigValue() } })
                .Set(new BookConfig { Id = "fantasy_2", Genres = new[] { BookGenre.Fantasy.ToConfigValue() } })
                .Set(new BookConfig { Id = "crime_1", Genres = new[] { BookGenre.Crime.ToConfigValue() } });

            var rows = new BookGenreRowSource(inventory, configs).BuildRows().ToList();

            Assert.AreEqual(Enum.GetValues(typeof(BookGenre)).Length, rows.Count);
            Assert.AreEqual(BookGenre.Classic.ToConfigValue(), rows[0].SpriteId);
            Assert.AreEqual(0, rows[0].Count);
            Assert.AreEqual(2, rows.Single(r => r.SpriteId == BookGenre.Crime.ToConfigValue()).Count);
            Assert.AreEqual(4, rows.Single(r => r.SpriteId == BookGenre.Fantasy.ToConfigValue()).Count);
            Assert.IsTrue(rows.All(r => r.Style == InventoryRowStyle.Default));
            Assert.IsTrue(rows.All(r => r.ItemId == r.SpriteId));
        }

        [Test]
        public void QuestItemRowSource_BuildsQuestItemRows()
        {
            var inventory = new FakeInventoryService()
                .Seed("milly_letter", InventoryCategories.QuestItem);
            var configs = new FakeConfigsService()
                .Set(new QuestItemConfig { Id = "milly_letter" });

            var rows = new QuestItemRowSource(inventory, configs).BuildRows().ToList();

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("milly_letter", rows[0].SpriteId);
            Assert.AreEqual(0, rows[0].Count);
            Assert.AreEqual("milly_letter", rows[0].ItemId);
            Assert.AreEqual(InventoryRowStyle.QuestItem, rows[0].Style);
        }

        [Test]
        public void ConsumableRowSource_BuildsDefaultRowsWithCount()
        {
            var inventory = new FakeInventoryService()
                .Seed("fuel_canister", InventoryCategories.Consumable, 15);
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = "fuel_canister" });

            var rows = new ConsumableRowSource(inventory, configs).BuildRows().ToList();

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("fuel_canister", rows[0].SpriteId);
            Assert.AreEqual(15, rows[0].Count);
            Assert.AreEqual("fuel_canister", rows[0].ItemId);
            Assert.AreEqual(InventoryRowStyle.Default, rows[0].Style);
        }

        [Test]
        public void ConsumableRowSource_MissingConfig_SkipsRowAndLogsWarning()
        {
            var inventory = new FakeInventoryService()
                .Seed("ghost_canister", InventoryCategories.Consumable, 1);
            var configs = new FakeConfigsService();

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("Missing ConsumableConfig.*ghost_canister"));

            var rows = new ConsumableRowSource(inventory, configs).BuildRows().ToList();

            Assert.AreEqual(0, rows.Count);
        }

        [Test]
        public void RowSources_HaveExpectedOrder()
        {
            var inventory = new FakeInventoryService();
            var configs = new FakeConfigsService();

            var sources = new IInventoryRowSource[]
            {
                new ConsumableRowSource(inventory, configs),
                new QuestItemRowSource(inventory, configs),
                new BookGenreRowSource(inventory, configs)
            };

            var ordered = sources.OrderBy(s => s.Order).Select(s => s.Order).ToArray();

            CollectionAssert.AreEqual(new[] { 0, 20, 30 }, ordered);
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly List<InventoryItem> _items = new();

#pragma warning disable CS0067
            public event Action<InventoryChangeEvent> Changed;
#pragma warning restore CS0067

            public FakeInventoryService Seed(string itemId, string categoryId, int count = 1)
            {
                _items.Add(new InventoryItem(itemId, categoryId, count));
                return this;
            }

            public IReadOnlyList<InventoryItem> GetAll() => _items;

            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
                => _items.Where(i => string.Equals(i.CategoryId, categoryId, StringComparison.Ordinal)).ToList();

            public bool Has(string itemId) => GetCount(itemId) > 0;

            public int GetCount(string itemId)
                => _items.FirstOrDefault(i => string.Equals(i.ItemId, itemId, StringComparison.Ordinal))?.Count ?? 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<Type, List<IConfig>> _byType = new();

            public FakeConfigsService Set<T>(T config) where T : class, IConfig
            {
                if (!_byType.TryGetValue(typeof(T), out var list))
                {
                    list = new List<IConfig>();
                    _byType[typeof(T)] = list;
                }

                list.Add(config);
                return this;
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public T Get<T>(string id) where T : class, IConfig
                => GetAll<T>().FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                config = Get<T>(id);
                return config != null;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig => UniTask.FromResult(Get<T>(id));

            public bool IsExists<T>(string id) where T : class, IConfig => Get<T>(id) != null;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
                => _byType.TryGetValue(typeof(T), out var list)
                    ? list.Cast<T>().ToList()
                    : Array.Empty<T>();
        }
    }
}
