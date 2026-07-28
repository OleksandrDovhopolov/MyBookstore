using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>
    /// Add/Remove buttons for every <see cref="QuestItemConfig"/> in quest_items.json. Mirrors
    /// <see cref="DecorationCheatModule"/>: writes straight to <see cref="IInventoryService"/> and skips
    /// the quest that would normally grant the item, so the inventory row can be checked without playing
    /// the quest through.
    /// </summary>
    public class QuestItemCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Quest Items";
        private const string LogTag = "[QuestItemCheat]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly CancellationToken _ct;

        public QuestItemCheatModule(
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
            foreach (var cfg in _configs.GetAll<QuestItemConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                var id = cfg.Id;
                var displayName = string.IsNullOrEmpty(cfg.DisplayName) ? id : cfg.DisplayName;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Add {displayName}", () => AddQuestItemAsync(id).Forget())
                        .WithGroup(CardsGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Remove {displayName}", () => RemoveQuestItemAsync(id).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private async UniTaskVoid AddQuestItemAsync(string itemId)
        {
            // quest_item is a Unique category: a repeat add is a no-op, not a duplicate.
            await _inventory.AddAsync(itemId, InventoryCategories.QuestItem, 1, _ct);
            Debug.Log($"{LogTag} Added '{itemId}' to inventory (quest bypassed).");
        }

        private async UniTaskVoid RemoveQuestItemAsync(string itemId)
        {
            if (!_inventory.Has(itemId))
            {
                Debug.Log($"{LogTag} '{itemId}' not in inventory — nothing to remove.");
                return;
            }

            await _inventory.RemoveAsync(itemId, 1, _ct);
            Debug.Log($"{LogTag} Removed '{itemId}' from inventory.");
        }
    }
}
