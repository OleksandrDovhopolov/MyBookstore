using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Inventory.Conditions;
using Game.Rewards.API;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer.Unity;

namespace Game.Inventory.Services
{
    /// <summary>
    /// Validates inventory-item references across configs at boot, mirroring <c>DecorConfigValidator</c>.
    /// Item ids are plain strings shared by four config files, so a typo in any of them fails silently at
    /// runtime; this catches it up front instead.
    /// <para>
    /// Catalog = quest_items.json + consumables.json + decors.json + books_converted.json. References are collected
    /// from quest rewards, shop lots, economy day-completion rewards, <see cref="LocationConfig.UnlockCost"/>, and every
    /// <c>haveItem</c> node inside a condition tree. Awaits <see cref="IConfigsService.WarmupAsync"/> first
    /// so configs are loaded regardless of entry-point registration order. In Editor errors throw to block
    /// Play mode; in runtime builds they are logged and the broken reference stays broken downstream.
    /// </para>
    /// </summary>
    public sealed class ItemReferenceValidator : IAsyncStartable
    {
        private const string LogTag = "[ItemValidator]";
        private const string CatalogFiles =
            "quest_items.json, consumables.json, decors.json or books_converted.json";

        private readonly IConfigsService _configs;

        public ItemReferenceValidator(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await _configs.WarmupAsync(cancellation);

            var report = Validate();

            for (var i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning($"{LogTag} {report.Warnings[i]}");

            if (!report.HasErrors) return;

            for (var i = 0; i < report.Errors.Count; i++)
                Debug.LogError($"{LogTag} {report.Errors[i]}");

#if UNITY_EDITOR
            throw new InvalidOperationException(
                $"{LogTag} {report.Errors.Count} item config error(s). See console.\n{report.FormatErrors()}");
#endif
        }

        public ItemValidationReport Validate()
        {
            var report = new ItemValidationReport();
            var catalog = BuildCatalog(report);
            var references = CollectReferences(report);
            ValidateReferences(report, catalog, references);
            ValidateReachability(report, catalog, references);
            return report;
        }

        // ----- catalog -----

        /// <summary>itemId → category, built from every config file that declares an inventory item.</summary>
        private Dictionary<string, string> BuildCatalog(ItemValidationReport report)
        {
            var catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddCatalogEntries<QuestItemConfig>(catalog, report, InventoryCategories.QuestItem, "quest_items.json");
            AddCatalogEntries<ConsumableConfig>(catalog, report, InventoryCategories.Consumable, "consumables.json");
            AddCatalogEntries<DecorConfig>(catalog, report, InventoryCategories.Decor, "decors.json");
            AddCatalogEntries<BookConfig>(catalog, report, InventoryCategories.Book, "books_converted.json");
            return catalog;
        }

        private void AddCatalogEntries<T>(
            Dictionary<string, string> catalog,
            ItemValidationReport report,
            string category,
            string fileName)
            where T : class, IConfig
        {
            var all = _configs.GetAll<T>();
            if (all == null) return;

            for (var i = 0; i < all.Count; i++)
            {
                var config = all[i];
                if (config == null) continue;

                var id = config.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.Errors.Add($"{fileName}: entry at index {i} has an empty id.");
                    continue;
                }

                if (catalog.TryGetValue(id, out var existingCategory))
                {
                    report.Errors.Add(
                        $"Item id '{id}' is declared twice — as category '{existingCategory}' and again in " +
                        $"{fileName} as '{category}'. Ids must be unique across item catalogs.");
                    continue;
                }

                catalog.Add(id, category);
            }
        }

        // ----- references -----

        private List<ItemReference> CollectReferences(ItemValidationReport report)
        {
            var references = new List<ItemReference>();
            CollectQuestReferences(references, report);
            CollectShopReferences(references);
            CollectEconomyReferences(references);
            CollectLocationReferences(references, report);
            return references;
        }

        private void CollectQuestReferences(List<ItemReference> references, ItemValidationReport report)
        {
            var quests = _configs.GetAll<QuestConfig>();
            if (quests == null) return;

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null) continue;

                var origin = $"Quest '{quest.Id}'";

                if (quest.Rewards != null)
                {
                    for (var j = 0; j < quest.Rewards.Length; j++)
                    {
                        var reward = quest.Rewards[j];
                        if (reward == null) continue;

                        // Resource rewards carry a currency id, not an item id — not our business.
                        if (!string.Equals(reward.Kind, nameof(RewardKind.InventoryItem),
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        references.Add(ItemReference.Grant(reward.Id, reward.Category, $"{origin} reward"));
                    }
                }

                CollectHaveItemReferences(references, report, quest.ActivationConditions,
                    $"{origin} activation conditions");
                CollectHaveItemReferences(references, report, quest.FailConditions,
                    $"{origin} fail conditions");

                if (quest.Tasks == null) continue;

                for (var j = 0; j < quest.Tasks.Length; j++)
                {
                    var task = quest.Tasks[j];
                    if (task == null) continue;

                    CollectHaveItemReferences(references, report, task.CompletionConditions,
                        $"{origin} task {task.Id} completion conditions");
                    CollectHaveItemReferences(references, report, task.ActivationConditions,
                        $"{origin} task {task.Id} activation conditions");
                }
            }
        }

        private void CollectShopReferences(List<ItemReference> references)
        {
            var lots = _configs.GetAll<ShopConfig>();
            if (lots == null) return;

            for (var i = 0; i < lots.Count; i++)
            {
                var lot = lots[i];
                // Book-box lots leave RewardItems empty on purpose — the expander fills them at runtime.
                if (lot?.RewardItems == null) continue;

                for (var j = 0; j < lot.RewardItems.Length; j++)
                {
                    var item = lot.RewardItems[j];
                    if (item == null || item.Kind != RewardKind.InventoryItem) continue;

                    references.Add(ItemReference.Grant(item.Id, item.Category, $"Shop lot '{lot.Id}'"));
                }
            }
        }

        private void CollectEconomyReferences(List<ItemReference> references)
        {
            var rewards = _configs.Get<EconomyConfig>(EconomyConfig.SingletonId)?.DayCompletionRewards;
            if (rewards == null) return;

            for (var i = 0; i < rewards.Length; i++)
            {
                var item = rewards[i];
                if (item == null || item.Kind != RewardKind.InventoryItem) continue;

                references.Add(ItemReference.Grant(
                    item.Id,
                    item.Category,
                    "Economy day-completion reward"));
            }
        }

        private void CollectLocationReferences(List<ItemReference> references, ItemValidationReport report)
        {
            var locations = _configs.GetAll<LocationConfig>();
            if (locations == null) return;

            for (var i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null) continue;

                var origin = $"Location '{location.Id}'";

                if (location.UnlockCost != null)
                {
                    for (var j = 0; j < location.UnlockCost.Length; j++)
                    {
                        var cost = location.UnlockCost[j];
                        if (cost == null)
                        {
                            report.Errors.Add($"{origin} unlockCost entry #{j} is null.");
                            continue;
                        }

                        if (cost.Amount <= 0)
                        {
                            report.Errors.Add(
                                $"{origin} unlockCost for '{cost.ItemId}' has amount {cost.Amount}; " +
                                "expected a positive number.");
                        }

                        references.Add(ItemReference.Require(cost.ItemId, $"{origin} unlock cost"));
                    }
                }

                CollectHaveItemReferences(references, report, location.Unlock, $"{origin} unlock conditions");
            }
        }

        /// <summary>
        /// Walks a raw condition tree and picks up every <c>haveItem</c> node. Deliberately structure-agnostic
        /// (recurses through any object/array) so it keeps working if the composite syntax ever changes.
        /// </summary>
        private static void CollectHaveItemReferences(
            List<ItemReference> references,
            ItemValidationReport report,
            JToken node,
            string origin)
        {
            switch (node)
            {
                case JObject obj:
                {
                    var type = obj.Value<string>("type");
                    if (string.Equals(type, HaveItemConditionFactory.TypeId, StringComparison.OrdinalIgnoreCase))
                    {
                        var itemId = obj.Value<string>("itemId");
                        if (string.IsNullOrWhiteSpace(itemId))
                            report.Errors.Add($"{origin}: a 'haveItem' condition has no 'itemId'.");
                        else
                            references.Add(ItemReference.Require(itemId, $"{origin} (haveItem)"));
                    }

                    foreach (var property in obj.Properties())
                        CollectHaveItemReferences(references, report, property.Value, origin);
                    break;
                }
                case JArray array:
                {
                    foreach (var child in array)
                        CollectHaveItemReferences(references, report, child, origin);
                    break;
                }
            }
        }

        // ----- checks -----

        private static void ValidateReferences(
            ItemValidationReport report,
            Dictionary<string, string> catalog,
            List<ItemReference> references)
        {
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];

                if (string.IsNullOrWhiteSpace(reference.ItemId))
                {
                    report.Errors.Add($"{reference.Origin} references an item with an empty id.");
                    continue;
                }

                if (!catalog.TryGetValue(reference.ItemId, out var actualCategory))
                {
                    if (reported.Add($"{reference.Origin}|{reference.ItemId}|missing"))
                    {
                        report.Errors.Add(
                            $"{reference.Origin} references item '{reference.ItemId}' which has no entry in " +
                            $"{CatalogFiles}.");
                    }
                    continue;
                }

                // Only grants declare a category; costs and conditions address the item by id alone.
                if (string.IsNullOrEmpty(reference.Category)) continue;

                if (!string.Equals(reference.Category, actualCategory, StringComparison.OrdinalIgnoreCase)
                    && reported.Add($"{reference.Origin}|{reference.ItemId}|category"))
                {
                    report.Errors.Add(
                        $"{reference.Origin} grants item '{reference.ItemId}' as category " +
                        $"'{reference.Category}', but its config declares '{actualCategory}' — the item would " +
                        "land in the wrong inventory bucket.");
                }
            }
        }

        /// <summary>
        /// Quest items and consumables reach the player only through quest rewards, shop lots, and economy
        /// day-completion rewards, so one that is declared but never granted is dead content. Books are seeded/expanded at runtime and decor
        /// reachability is already covered by <c>DecorConfigValidator</c> — both are skipped here.
        /// </summary>
        private static void ValidateReachability(
            ItemValidationReport report,
            Dictionary<string, string> catalog,
            List<ItemReference> references)
        {
            var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < references.Count; i++)
            {
                var reference = references[i];
                if (reference.IsGrant && !string.IsNullOrWhiteSpace(reference.ItemId))
                    granted.Add(reference.ItemId);
            }

            var unreachable = new List<string>();
            foreach (var pair in catalog)
            {
                if (!IsReachabilityTracked(pair.Value)) continue;
                if (granted.Contains(pair.Key)) continue;
                unreachable.Add($"Item '{pair.Key}' ({pair.Value}) is not granted by any quest reward or " +
                                "shop lot or day-completion reward - unreachable by the player.");
            }

            // Dictionary order is unspecified; sort so the console output is stable between runs.
            unreachable.Sort(StringComparer.Ordinal);
            report.Warnings.AddRange(unreachable);
        }

        private static bool IsReachabilityTracked(string category)
            => string.Equals(category, InventoryCategories.QuestItem, StringComparison.OrdinalIgnoreCase)
               || string.Equals(category, InventoryCategories.Consumable, StringComparison.OrdinalIgnoreCase);

        private readonly struct ItemReference
        {
            public readonly string ItemId;

            /// <summary>Category declared at the reference site; null when the site addresses the item by id alone.</summary>
            public readonly string Category;

            public readonly string Origin;

            /// <summary>True when the site hands the item to the player; false when it consumes/reads it.</summary>
            public readonly bool IsGrant;

            private ItemReference(string itemId, string category, string origin, bool isGrant)
            {
                ItemId = itemId;
                Category = category;
                Origin = origin;
                IsGrant = isGrant;
            }

            public static ItemReference Grant(string itemId, string category, string origin)
                => new(itemId, category, origin, isGrant: true);

            public static ItemReference Require(string itemId, string origin)
                => new(itemId, null, origin, isGrant: false);
        }
    }
}
