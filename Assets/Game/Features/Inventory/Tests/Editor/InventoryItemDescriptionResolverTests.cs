using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Inventory.UI;
using NUnit.Framework;

namespace Game.Inventory.Tests.Editor
{
    public sealed class InventoryItemDescriptionResolverTests
    {
        [Test]
        public void Consumable_ReturnsConfigDescriptionKey()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig
                {
                    Id = "fuel_canister",
                    DescriptionKey = "consumable.fuel_canister.desc"
                });

            var key = InventoryItemDescriptionResolver.ResolveDescriptionKey(
                configs, "fuel_canister", InventoryRowStyle.Default);

            Assert.AreEqual("consumable.fuel_canister.desc", key);
        }

        [Test]
        public void QuestItem_ReturnsConfigDescriptionKey()
        {
            var configs = new FakeConfigsService()
                .Set(new QuestItemConfig { Id = "map", DescriptionKey = "quest_item.map.desc" });

            var key = InventoryItemDescriptionResolver.ResolveDescriptionKey(
                configs, "map", InventoryRowStyle.QuestItem);

            Assert.AreEqual("quest_item.map.desc", key);
        }

        [Test]
        public void BookGenreRow_ReturnsGenreKey_IgnoringCase()
        {
            var configs = new FakeConfigsService();

            Assert.AreEqual(
                "book_genre.classic.desc",
                InventoryItemDescriptionResolver.ResolveDescriptionKey(
                    configs, BookGenre.Classic.ToConfigValue(), InventoryRowStyle.Default));
            Assert.AreEqual(
                "book_genre.classic.desc",
                InventoryItemDescriptionResolver.ResolveDescriptionKey(
                    configs, "classic", InventoryRowStyle.Default));
        }

        [Test]
        public void EveryGenre_ResolvesToItsOwnKey()
        {
            var configs = new FakeConfigsService();
            var keys = Enum.GetValues(typeof(BookGenre))
                .Cast<BookGenre>()
                .Select(g => InventoryItemDescriptionResolver.ResolveDescriptionKey(
                    configs, g.ToConfigValue(), InventoryRowStyle.Default))
                .ToList();

            Assert.IsTrue(keys.All(k => !string.IsNullOrEmpty(k)));
            Assert.AreEqual(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        }

        [Test]
        public void UnknownItem_ReturnsNull()
        {
            var key = InventoryItemDescriptionResolver.ResolveDescriptionKey(
                new FakeConfigsService(), "no_such_item", InventoryRowStyle.Default);

            Assert.IsNull(key);
        }

        [Test]
        public void ConfigWithoutDescriptionKey_ReturnsNull()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = "postcard", DescriptionKey = "   " });

            var key = InventoryItemDescriptionResolver.ResolveDescriptionKey(
                configs, "postcard", InventoryRowStyle.Default);

            Assert.IsNull(key);
        }

        [Test]
        public void MissingServiceOrId_ReturnsNull()
        {
            Assert.IsNull(InventoryItemDescriptionResolver.ResolveDescriptionKey(
                null, "fuel_canister", InventoryRowStyle.Default));
            Assert.IsNull(InventoryItemDescriptionResolver.ResolveDescriptionKey(
                new FakeConfigsService(), null, InventoryRowStyle.Default));
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
