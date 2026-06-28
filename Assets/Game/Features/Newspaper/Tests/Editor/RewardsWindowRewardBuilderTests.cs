using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Newspaper.UI;
using Game.Rewards.API;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Newspaper.Tests.Editor
{
    public sealed class RewardsWindowRewardBuilderTests
    {
        [Test]
        public void Build_BookRewards_GroupsByGenreAndSkipsZeroAmounts()
        {
            var configs = new FakeConfigsService(
                books: new[]
                {
                    Book("classic_1", "Classic"),
                    Book("classic_2", "Classic"),
                    Book("crime_1", "Crime"),
                    Book("kids_1", "Kids"),
                });
            var spec = new RewardSpec("book_box", new[]
            {
                RewardItem.InventoryItem("classic_1", InventoryCategories.Book, 1),
                RewardItem.InventoryItem("classic_2", InventoryCategories.Book, 2),
                RewardItem.InventoryItem("crime_1", InventoryCategories.Book, 2),
                RewardItem.InventoryItem("kids_1", InventoryCategories.Book, 0),
            });

            var rewards = RewardsWindowRewardBuilder.Build(spec, configs);

            Assert.AreEqual(2, rewards.Count);
            Assert.AreEqual("Classic", rewards[0].ResourceId);
            Assert.AreEqual(3, rewards[0].Amount);
            Assert.AreEqual("Crime", rewards[1].ResourceId);
            Assert.AreEqual(2, rewards[1].Amount);
            Assert.IsFalse(rewards.Any(r => string.Equals(r.ResourceId, "Kids", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void Build_DecorReward_EmitsDecorAndSkipsResource()
        {
            var configs = new FakeConfigsService(
                decors: new[] { Decor("vintage_globe", "Vintage Globe", "Decor/VintageGlobe") });
            var spec = new RewardSpec("decor_reward", new[]
            {
                RewardItem.InventoryItem("vintage_globe", InventoryCategories.Decor, 1),
                RewardItem.Resource("gold", 100),
            });

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[RewardsWindow] Invalid reward item 'gold' (kind=Resource, category=''). Skipped.");

            var rewards = RewardsWindowRewardBuilder.Build(spec, configs);

            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual("Decor/VintageGlobe", rewards[0].ResourceId);
            Assert.AreEqual("Vintage Globe", rewards[0].DisplayName);
            Assert.AreEqual(InventoryCategories.Decor, rewards[0].Category);
            Assert.AreEqual(1, rewards[0].Amount);
        }

        [Test]
        public void Build_MissingBookConfig_WarnsAndSkips()
        {
            var spec = new RewardSpec("book_box", new[]
            {
                RewardItem.InventoryItem("missing_book", InventoryCategories.Book, 2),
            });

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[RewardsWindow] Book reward 'missing_book' has no valid BookConfig/genre. Skipped.");

            var rewards = RewardsWindowRewardBuilder.Build(spec, new FakeConfigsService());

            Assert.AreEqual(0, rewards.Count);
        }

        [Test]
        public void Build_MissingDecorConfig_WarnsAndSkips()
        {
            var spec = new RewardSpec("decor_reward", new[]
            {
                RewardItem.InventoryItem("missing_decor", InventoryCategories.Decor, 1),
            });

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[RewardsWindow] Decor reward 'missing_decor' has no DecorConfig. Skipped.");

            var rewards = RewardsWindowRewardBuilder.Build(spec, new FakeConfigsService());

            Assert.AreEqual(0, rewards.Count);
        }

        private static BookConfig Book(string id, string genre) =>
            new BookConfig { Id = id, Genre = genre };

        private static DecorConfig Decor(string id, string displayName, string iconAddress) =>
            new DecorConfig { Id = id, DisplayName = displayName, IconAddress = iconAddress };

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, BookConfig> _books;
            private readonly Dictionary<string, DecorConfig> _decors;

            public FakeConfigsService(BookConfig[] books = null, DecorConfig[] decors = null)
            {
                _books = (books ?? Array.Empty<BookConfig>())
                    .Where(b => b != null && !string.IsNullOrEmpty(b.Id))
                    .ToDictionary(b => b.Id, StringComparer.Ordinal);
                _decors = (decors ?? Array.Empty<DecorConfig>())
                    .Where(d => d != null && !string.IsNullOrEmpty(d.Id))
                    .ToDictionary(d => d.Id, StringComparer.Ordinal);
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            {
                if (typeof(T) == typeof(BookConfig))
                    return _books.Values.Cast<T>().ToList();
                if (typeof(T) == typeof(DecorConfig))
                    return _decors.Values.Cast<T>().ToList();
                return Array.Empty<T>();
            }

            public T Get<T>(string id) where T : class, IConfig
            {
                TryGet<T>(id, out var config);
                return config;
            }

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (typeof(T) == typeof(BookConfig) && _books.TryGetValue(id, out var book))
                    {
                        config = book as T;
                        return true;
                    }

                    if (typeof(T) == typeof(DecorConfig) && _decors.TryGetValue(id, out var decor))
                    {
                        config = decor as T;
                        return true;
                    }
                }

                config = null;
                return false;
            }

            public UniTask<T> GetAsync<T>(string id) where T : class, IConfig =>
                UniTask.FromResult(Get<T>(id));

            public bool IsExists<T>(string id) where T : class, IConfig =>
                TryGet<T>(id, out _);
        }
    }
}
