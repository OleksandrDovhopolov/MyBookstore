using System;
using System.Collections.Generic;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
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
                items.Add(new CheatInventoryItem(cfg.Id, cfg.Title));
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
                items.Add(new CheatInventoryItem(cfg.Id, cfg.DisplayName));
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
                items.Add(new CheatInventoryItem(cfg.Id, cfg.DisplayName));
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
                items.Add(new CheatInventoryItem(cfg.Id, cfg.DisplayName));
            }
            return items;
        }

        private async UniTaskVoid AddAsync(string itemId, string categoryId)
        {
            await _inventory.AddAsync(itemId, categoryId, 1, _ct);
            Debug.Log($"{LogTag} Added 1 '{itemId}' ({categoryId}).");
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
