using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Inventory.Conditions;
using Game.Inventory.Services;
using Game.Rewards.API;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Inventory.Tests.Editor
{
    public sealed class ItemReferenceValidatorTests
    {
        private const string Canister = "fuel_canister";
        private const string Permit = "port_trade_permit";

        [Test]
        public void NoIssues_WhenEveryReferenceResolves()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = Canister })
                .Set(GrantingQuest("q_eddi", Canister, InventoryCategories.Consumable))
                .Set(new LocationConfig
                {
                    Id = "loc_port",
                    UnlockCost = new[] { Cost(Canister, 2) }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            CollectionAssert.IsEmpty(report.Errors);
            CollectionAssert.IsEmpty(report.Warnings);
        }

        [Test]
        public void Error_WhenUnlockCostReferencesUnknownItem()
        {
            var configs = new FakeConfigsService()
                .Set(new LocationConfig
                {
                    Id = "loc_port",
                    UnlockCost = new[] { Cost("typo_permit", 1) }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Errors.Any(e => e.Contains("typo_permit") && e.Contains("loc_port")),
                $"Expected an unknown-item error for 'typo_permit'. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Error_WhenHaveItemInsideCompositeReferencesUnknownItem()
        {
            var configs = new FakeConfigsService()
                .Set(new LocationConfig
                {
                    Id = "loc_village",
                    // Nested two levels deep to prove the walk is structure-agnostic.
                    Unlock = new JObject
                    {
                        ["all"] = new JArray
                        {
                            new JObject { ["type"] = "soldTotal", ["min"] = 200 },
                            new JObject
                            {
                                ["not"] = new JObject
                                {
                                    ["type"] = HaveItemConditionFactory.TypeId,
                                    ["itemId"] = "ghost_item",
                                    ["min"] = 1
                                }
                            }
                        }
                    }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains("ghost_item")),
                $"Expected an unknown-item error for 'ghost_item'. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Error_WhenHaveItemHasNoItemId()
        {
            var configs = new FakeConfigsService()
                .Set(new LocationConfig
                {
                    Id = "loc_port",
                    Unlock = new JObject { ["type"] = HaveItemConditionFactory.TypeId, ["min"] = 1 }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains("no 'itemId'")),
                $"Expected a missing-itemId error. Got: {report.FormatErrors()}");
        }

        [Test]
        public void FindsHaveItemInsideQuestTaskConditions()
        {
            var configs = new FakeConfigsService()
                .Set(new QuestConfig
                {
                    Id = "q_captain",
                    Tasks = new[]
                    {
                        new QuestTaskConfig
                        {
                            Id = 1,
                            CompletionConditions = new JObject
                            {
                                ["type"] = HaveItemConditionFactory.TypeId,
                                ["itemId"] = "postcard",
                                ["min"] = 10
                            }
                        }
                    }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains("postcard") && e.Contains("q_captain")),
                $"Expected the task condition to be scanned. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Error_WhenGrantDeclaresWrongCategory()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = Canister })
                // Consumable granted as a quest item — it would land in the wrong inventory bucket.
                .Set(GrantingQuest("q_eddi", Canister, InventoryCategories.QuestItem));

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains(Canister) && e.Contains("wrong inventory bucket")),
                $"Expected a category-mismatch error. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Error_WhenUnlockCostAmountIsNotPositive()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = Canister })
                .Set(GrantingQuest("q_eddi", Canister, InventoryCategories.Consumable))
                .Set(new LocationConfig
                {
                    Id = "loc_port",
                    UnlockCost = new[] { Cost(Canister, 0) }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains("expected a positive number")),
                $"Expected an amount error. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Error_WhenIdIsDeclaredInTwoCatalogs()
        {
            var configs = new FakeConfigsService()
                .Set(new QuestItemConfig { Id = "shared_id" })
                .Set(new ConsumableConfig { Id = "shared_id" });

            var report = new ItemReferenceValidator(configs).Validate();

            Assert.IsTrue(report.Errors.Any(e => e.Contains("declared twice")),
                $"Expected a duplicate-id error. Got: {report.FormatErrors()}");
        }

        [Test]
        public void Warning_WhenQuestItemIsNeverGranted()
        {
            var configs = new FakeConfigsService()
                .Set(new QuestItemConfig { Id = Permit })
                .Set(new LocationConfig
                {
                    Id = "loc_port",
                    UnlockCost = new[] { Cost(Permit, 1) }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            // Required by the location but handed out by nobody — dead content, not a hard error.
            CollectionAssert.IsEmpty(report.Errors);
            Assert.IsTrue(report.Warnings.Any(w => w.Contains(Permit) && w.Contains("unreachable")),
                $"Expected an unreachable warning for '{Permit}'. Got: {string.Join("; ", report.Warnings)}");
        }

        [Test]
        public void ShopLotCountsAsAGrant()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = Canister })
                .Set(new ShopConfig
                {
                    Id = "newspaper_consumable_fuel_canister",
                    RewardItems = new[]
                    {
                        new RewardItemData
                        {
                            Id = Canister,
                            Category = InventoryCategories.Consumable,
                            Amount = 1,
                            Kind = RewardKind.InventoryItem
                        }
                    }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            CollectionAssert.IsEmpty(report.Errors);
            CollectionAssert.IsEmpty(report.Warnings);
        }

        [Test]
        public void EconomyDayRewardCountsAsAGrant()
        {
            var configs = new FakeConfigsService()
                .Set(new ConsumableConfig { Id = "postcard" })
                .Set(new EconomyConfig
                {
                    Id = EconomyConfig.SingletonId,
                    DayCompletionRewards = new[]
                    {
                        new RewardItemData
                        {
                            Id = "postcard",
                            Category = InventoryCategories.Consumable,
                            Amount = 1,
                            Kind = RewardKind.InventoryItem
                        }
                    }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            CollectionAssert.IsEmpty(report.Errors);
            CollectionAssert.IsEmpty(report.Warnings);
        }

        [Test]
        public void NoReachabilityWarning_ForBooksAndDecor()
        {
            // Books are seeded at runtime and decor reachability is DecorConfigValidator's job.
            var configs = new FakeConfigsService()
                .Set(new BookConfig { Id = "book_1" })
                .Set(new DecorConfig { Id = "lavender" });

            var report = new ItemReferenceValidator(configs).Validate();

            CollectionAssert.IsEmpty(report.Errors);
            CollectionAssert.IsEmpty(report.Warnings);
        }

        [Test]
        public void ResourceRewardsAreIgnored()
        {
            var configs = new FakeConfigsService()
                .Set(new QuestConfig
                {
                    Id = "q_gold",
                    Rewards = new[]
                    {
                        new QuestRewardConfig { Kind = nameof(RewardKind.Resource), Id = "gold", Amount = 100 }
                    }
                });

            var report = new ItemReferenceValidator(configs).Validate();

            CollectionAssert.IsEmpty(report.Errors);
        }

        // ----- helpers -----

        private static LocationUnlockCostConfig Cost(string itemId, int amount)
            => new() { ItemId = itemId, Amount = amount };

        private static QuestConfig GrantingQuest(string questId, string itemId, string category)
            => new()
            {
                Id = questId,
                Rewards = new[]
                {
                    new QuestRewardConfig
                    {
                        Kind = nameof(RewardKind.InventoryItem),
                        Id = itemId,
                        Category = category,
                        Amount = 1
                    }
                }
            };

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
