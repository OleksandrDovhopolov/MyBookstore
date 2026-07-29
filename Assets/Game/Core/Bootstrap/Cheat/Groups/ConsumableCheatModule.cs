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
    /// <summary>Add/Remove inputs for stackable consumables from consumables.json.</summary>
    public sealed class ConsumableCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Consumables";
        private const string LogTag = "[ConsumableCheat]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly CancellationToken _ct;

        public ConsumableCheatModule(
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
            foreach (var cfg in _configs.GetAll<ConsumableConfig>())
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                var id = cfg.Id;
                var displayName = string.IsNullOrEmpty(cfg.DisplayName) ? id : cfg.DisplayName;

                cheatsContainer.AddItem<CheatInputItem>(item =>
                    item.OnInputChange<int>($"Add {displayName}: N", amount => AddConsumableAsync(id, amount).Forget())
                        .WithGroup(CardsGroup));

                cheatsContainer.AddItem<CheatInputItem>(item =>
                    item.OnInputChange<int>($"Remove {displayName}: N", amount => RemoveConsumableAsync(id, amount).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private async UniTaskVoid AddConsumableAsync(string itemId, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"{LogTag} Add '{itemId}' ignored: amount must be > 0.");
                return;
            }

            await _inventory.AddAsync(itemId, InventoryCategories.Consumable, amount, _ct);
            Debug.Log($"{LogTag} Added {amount} '{itemId}'.");
        }

        private async UniTaskVoid RemoveConsumableAsync(string itemId, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"{LogTag} Remove '{itemId}' ignored: amount must be > 0.");
                return;
            }

            var count = _inventory.GetCount(itemId);
            if (count < amount)
            {
                Debug.LogWarning($"{LogTag} Remove '{itemId}' ignored: have {count}, need {amount}.");
                return;
            }

            var removed = await _inventory.RemoveAsync(itemId, amount, _ct);
            if (removed)
                Debug.Log($"{LogTag} Removed {amount} '{itemId}'.");
            else
                Debug.LogWarning($"{LogTag} Remove '{itemId}' failed.");
        }
    }
}
