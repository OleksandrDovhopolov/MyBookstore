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
                Book("classic_1", "Classic"),
                Book("classic_2", "Classic"),
                Book("crime_1", "Crime"),
                Book("kids_1", "Kids"));
            var spec = new RewardSpec("book_box", new[]
            {
                RewardItem.InventoryItem("classic_1", InventoryCategories.Book, 1),
                RewardItem.InventoryItem("classic_2", InventoryCategories.Book, 2),
                RewardItem.InventoryItem("crime_1", InventoryCategories.Book, 2),
                RewardItem.InventoryItem("kids_1", InventoryCategories.Book, 0),
            });

            var rewards = RewardsWindowRewardBuilder.Build(spec, configs, resolveIcon: null);

            Assert.AreEqual(2, rewards.Count);
            Assert.AreEqual("Classic", rewards[0].ResourceId);
            Assert.AreEqual(3, rewards[0].Amount);
            Assert.AreEqual("Crime", rewards[1].ResourceId);
            Assert.AreEqual(2, rewards[1].Amount);
            Assert.IsFalse(rewards.Any(r => string.Equals(r.ResourceId, "Kids", StringComparison.OrdinalIgnoreCase)));
        }

        [Test]
        public void Build_NonBookRewards_CreatesSingleFallbackEntry()
        {
            var spec = new RewardSpec("decor_reward", new[]
            {
                RewardItem.InventoryItem("decor_clock", InventoryCategories.Decor, 1),
                RewardItem.Resource("gold", 100),
            });

            var rewards = RewardsWindowRewardBuilder.Build(spec, new FakeConfigsService(), resolveIcon: null);

            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(RewardsWindowRewardBuilder.NonBookRewardId, rewards[0].ResourceId);
            Assert.AreEqual(101, rewards[0].Amount);
        }

        [Test]
        public void Build_MissingBookConfig_UsesFallbackEntry()
        {
            var spec = new RewardSpec("book_box", new[]
            {
                RewardItem.InventoryItem("missing_book", InventoryCategories.Book, 2),
            });

            LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[RewardsWindow] BookConfig 'missing_book' not found for granted book reward. Using fallback reward icon.");

            var rewards = RewardsWindowRewardBuilder.Build(spec, new FakeConfigsService(), resolveIcon: null);

            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(RewardsWindowRewardBuilder.NonBookRewardId, rewards[0].ResourceId);
            Assert.AreEqual(2, rewards[0].Amount);
        }

        private static BookConfig Book(string id, string genre) =>
            new BookConfig { Id = id, Genre = genre };

        private sealed class FakeConfigsService : IConfigsService
        {
            private readonly Dictionary<string, BookConfig> _books;

            public FakeConfigsService(params BookConfig[] books)
            {
                _books = (books ?? Array.Empty<BookConfig>())
                    .Where(b => b != null && !string.IsNullOrEmpty(b.Id))
                    .ToDictionary(b => b.Id, StringComparer.Ordinal);
            }

            public UniTask WarmupAsync(CancellationToken ct) => UniTask.CompletedTask;

            public IReadOnlyList<T> GetAll<T>() where T : class, IConfig
            {
                if (typeof(T) == typeof(BookConfig))
                    return _books.Values.Cast<T>().ToList();
                return Array.Empty<T>();
            }

            public T Get<T>(string id) where T : class, IConfig
            {
                TryGet<T>(id, out var config);
                return config;
            }

            public bool TryGet<T>(string id, out T config) where T : class, IConfig
            {
                if (typeof(T) == typeof(BookConfig)
                    && !string.IsNullOrEmpty(id)
                    && _books.TryGetValue(id, out var book))
                {
                    config = book as T;
                    return true;
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
