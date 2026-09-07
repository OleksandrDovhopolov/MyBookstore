using System;
using System.Collections.Generic;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Localization;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>Add/remove cheats for every config-backed inventory item catalog.</summary>
    public sealed class InventoryItemCheatModule : ICheatsModule
    {
        private const string LogTag = "[InventoryItemCheat]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly CancellationToken _ct;

        public InventoryItemCheatModule(
            IInventoryService inventory,
            IConfigsService configs,
            CancellationToken ct)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            AddCatalog(cheatsContainer, InventoryCategories.Decor, "Inventory / Decor", GetDecors());
            AddCatalog(cheatsContainer, InventoryCategories.Consumable, "Inventory / Consumables", GetConsumables());
            AddCatalog(cheatsContainer, InventoryCategories.QuestItem, "Inventory / Quest Items", GetQuestItems());
        }

        private void AddCatalog(
            ICheatsContainer cheatsContainer,
            string categoryId,
            string group,
            IReadOnlyList<CheatInventoryItem> items)
        {
            // "Grant everything" shortcut first, so it stays on top of the per-item buttons.
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick($"+ALL ({items.Count})", () => AddAllAsync(categoryId, items).Forget())
                    .WithGroup(group));

            for (var i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                if (string.IsNullOrEmpty(entry.Id)) continue;

                var id = entry.Id;
                var displayName = string.IsNullOrEmpty(entry.DisplayName) ? id : entry.DisplayName;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"+1 {displayName}", () => AddAsync(id, categoryId).Forget())
                        .WithGroup(group));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"-1 {displayName}", () => RemoveAsync(id).Forget())
                        .WithGroup(group));
            }
        }

        private IReadOnlyList<CheatInventoryItem> GetBooks()
        {
            var configs = _configs.GetAll<BookConfig>();
            if (configs == null) return Array.Empty<CheatInventoryItem>();

            var items = new List<CheatInventoryItem>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                items.Add(new CheatInventoryItem(cfg.Id, ResolveDisplayName(cfg.TitleKey, cfg.Id)));
            }
            return items;
        }

        private IReadOnlyList<CheatInventoryItem> GetDecors()
        {
            var configs = _configs.GetAll<DecorConfig>();
            if (configs == null) return Array.Empty<CheatInventoryItem>();

            var items = new List<CheatInventoryItem>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                items.Add(new CheatInventoryItem(cfg.Id, ResolveDisplayName(cfg.DisplayNameKey, cfg.Id)));
            }
            return items;
        }

        private IReadOnlyList<CheatInventoryItem> GetConsumables()
        {
            var configs = _configs.GetAll<ConsumableConfig>();
            if (configs == null) return Array.Empty<CheatInventoryItem>();

            var items = new List<CheatInventoryItem>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                items.Add(new CheatInventoryItem(cfg.Id, ResolveDisplayName(cfg.DisplayNameKey, cfg.Id)));
            }
            return items;
        }

        private IReadOnlyList<CheatInventoryItem> GetQuestItems()
        {
            var configs = _configs.GetAll<QuestItemConfig>();
            if (configs == null) return Array.Empty<CheatInventoryItem>();

            var items = new List<CheatInventoryItem>(configs.Count);
            for (var i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                items.Add(new CheatInventoryItem(cfg.Id, ResolveDisplayName(cfg.DisplayNameKey, cfg.Id)));
            }
            return items;
        }

        private static string ResolveDisplayName(string key, string fallback)
            => string.IsNullOrEmpty(key) ? fallback : LocalizationLocator.GetOrKey(key);

        private async UniTaskVoid AddAsync(string itemId, string categoryId)
        {
            await _inventory.AddAsync(itemId, categoryId, 1, _ct);
            Debug.Log($"{LogTag} Added 1 '{itemId}' ({categoryId}).");
        }

        /// <summary>
        /// Grants the whole catalog in one shot. Already-owned entries need no filtering here: for a Unique
        /// category <c>InventoryService.ApplyAdd</c> is idempotent (an existing id yields no change), and
        /// <see cref="IInventoryService.AddBatchAsync"/> persists once for the whole batch instead of per item.
        /// </summary>
        private async UniTaskVoid AddAllAsync(string categoryId, IReadOnlyList<CheatInventoryItem> items)
        {
            var batch = new List<InventoryItem>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var id = items[i].Id;
                if (string.IsNullOrEmpty(id)) continue;
                batch.Add(new InventoryItem(id, categoryId, 1));
            }

            if (batch.Count == 0)
            {
                Debug.Log($"{LogTag} Nothing to grant for '{categoryId}' — catalog is empty.");
                return;
            }

            var before = _inventory.GetByCategory(categoryId).Count;
            await _inventory.AddBatchAsync(batch, _ct);
            var granted = _inventory.GetByCategory(categoryId).Count - before;

            Debug.Log($"{LogTag} Granted all '{categoryId}': +{granted} new, " +
                      $"{batch.Count - granted} already owned (skipped).");
        }

        private async UniTaskVoid RemoveAsync(string itemId)
        {
            if (_inventory.GetCount(itemId) <= 0)
            {
                Debug.Log($"{LogTag} '{itemId}' not in inventory - nothing to remove.");
                return;
            }

            var removed = await _inventory.RemoveAsync(itemId, 1, _ct);
            if (removed)
                Debug.Log($"{LogTag} Removed 1 '{itemId}'.");
            else
                Debug.LogWarning($"{LogTag} Remove '{itemId}' failed.");
        }

        private readonly struct CheatInventoryItem
        {
            public readonly string Id;
            public readonly string DisplayName;

            public CheatInventoryItem(string id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }
        }
    }
}
