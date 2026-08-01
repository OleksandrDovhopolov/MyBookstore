using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Ftue.Domain;
using Game.Ftue.Services;
using Game.Inventory.API;
using Game.Resources.API;
using NUnit.Framework;
using Save;

namespace Game.Ftue.Tests.Editor
{
    public sealed class FtueBootstrapperTests
    {
        [Test]
        public void RunAsync_CleanFirstLaunch_GrantsStarterGoldBooksAndMarker()
        {
            var save = new FakeSaveService();
            var configs = new FakeConfigsService(BuildCatalog());
            var inventory = new FakeInventoryService();
            var resources = new FakeResourcesService();
            var bootstrapper = new FtueBootstrapper(save, configs, inventory, resources);

            bootstrapper.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(60, resources.GetAmount(ResourceIds.Gold));
            var books = inventory.GetByCategory(InventoryCategories.Book);
            Assert.AreEqual(54, books.Count);
            Assert.AreEqual(54, books.Select(i => i.ItemId).Distinct(StringComparer.Ordinal).Count());
            Assert.IsTrue(books.All(i => i.Count == 1));

            var marker = save.GetModuleAsync<FtueAppliedState>(FtueSaveKeys.Applied, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert.IsNotNull(marker);
            Assert.IsTrue(marker.Applied);
            Assert.IsFalse(string.IsNullOrWhiteSpace(marker.AppliedAtUtcIso));
        }

        private static IReadOnlyList<BookConfig> BuildCatalog()
        {
            var result = new List<BookConfig>();
            AddBooks(result, "Fantasy", 10);
            AddBooks(result, "Crime", 10);
            AddBooks(result, "Drama", 12);
            AddBooks(result, "Classic", 6);
            AddBooks(result, "Fact", 6);
            AddBooks(result, "Travel", 6);
            AddBooks(result, "Kids", 4);
            return result;
        }

        private static void AddBooks(List<BookConfig> result, string genre, int count)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(new BookConfig
                {
                    Id = $"{genre.ToLowerInvariant()}_{i:D2}",
                    Genres = new[] { genre }
                });
            }
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _modules = new(StringComparer.Ordinal);

            public UniTask<T> GetModuleAsync<T>(string moduleKey, CancellationToken ct) where T : class
                => UniTask.FromResult(_modules.TryGetValue(moduleKey, out var value) ? value as T : null);

            public UniTask UpdateModuleAsync<T>(string moduleKey, T value, int schemaVersion, CancellationToken ct)
            {
                _modules[moduleKey] = value;
                return UniTask.CompletedTask;
            }

            public UniTask LoadAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct, SaveMode mode = SaveMode.Regular) => UniTask.CompletedTask;
            public void MarkDirty() { }
            public void RegisterHook(ISaveHook hook) { }
            public IDisposable BlockAutosave() => new NoopLease();
            public void Dispose() { }

            private sealed class NoopLease : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly IReadOnlyList<BookConfig> _books;

            public FakeConfigsService(IReadOnlyList<BookConfig> books) => _books = books;

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
                => typeof(T) == typeof(BookConfig) ? _books.Cast<T>().ToList() : Array.Empty<T>();
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly List<InventoryItem> _items = new();

            public event Action<InventoryChangeEvent> Changed;

            public IReadOnlyList<InventoryItem> GetAll() => _items;

            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
                => _items.Where(i => string.Equals(i.CategoryId, categoryId, StringComparison.Ordinal)).ToList();

            public bool Has(string itemId) => GetCount(itemId) > 0;

            public int GetCount(string itemId)
                => _items.FirstOrDefault(i => string.Equals(i.ItemId, itemId, StringComparison.Ordinal))?.Count ?? 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
            {
                _items.Add(new InventoryItem(itemId, categoryId, amount));
                Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, amount));
                return UniTask.CompletedTask;
            }

            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
            {
                _items.AddRange(items);
                return UniTask.CompletedTask;
            }

            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct) => UniTask.FromResult(false);
        }

        private sealed class FakeResourcesService : IResourcesService
        {
            private readonly Dictionary<string, int> _amounts = new(StringComparer.Ordinal);

            public event Action<ResourceChangeEvent> Changed;

            public IReadOnlyDictionary<string, int> GetAll() => _amounts;
            public int GetAmount(string resourceId) => _amounts.TryGetValue(resourceId, out var amount) ? amount : 0;
            public bool Has(string resourceId, int amount) => GetAmount(resourceId) >= amount;

            public UniTask AddAsync(string resourceId, int amount, string reason, CancellationToken ct)
            {
                var oldAmount = GetAmount(resourceId);
                var newAmount = oldAmount + amount;
                _amounts[resourceId] = newAmount;
                Changed?.Invoke(new ResourceChangeEvent(resourceId, oldAmount, newAmount, amount, reason));
                return UniTask.CompletedTask;
            }

            public UniTask<bool> RemoveAsync(string resourceId, int amount, string reason, CancellationToken ct)
                => UniTask.FromResult(false);
        }
    }
}
