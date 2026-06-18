using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.UI;
using UnityEngine;

namespace Game.Cheat
{
    public class DecorationCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Decoration";
        private const string LogTag = "[DecorationCheat]";

        private readonly UIManager _uiManager;
        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly CancellationToken _ct;

        public DecorationCheatModule(
            UIManager uiManager,
            IInventoryService inventory,
            IConfigsService configs,
            CancellationToken ct)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            var decors = _configs.GetAll<DecorConfig>();
            foreach (var cfg in decors)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                var id = cfg.Id;
                var displayName = string.IsNullOrEmpty(cfg.DisplayName) ? id : cfg.DisplayName;

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Add {displayName}", () => AddDecorAsync(id).Forget())
                        .WithGroup(CardsGroup));

                cheatsContainer.AddItem<CheatButtonItem>(item =>
                    item.OnClick($"Remove {displayName}", () => RemoveDecorAsync(id).Forget())
                        .WithGroup(CardsGroup));
            }
        }

        private async UniTaskVoid AddDecorAsync(string decorId)
        {
            await _inventory.AddAsync(decorId, InventoryCategories.Decor, 1, _ct);
            Debug.Log($"{LogTag} Added '{decorId}' to inventory (price ignored).");
        }

        private async UniTaskVoid RemoveDecorAsync(string decorId)
        {
            if (!_inventory.Has(decorId))
            {
                Debug.Log($"{LogTag} '{decorId}' not in inventory — nothing to remove.");
                return;
            }
            await _inventory.RemoveAsync(decorId, 1, _ct);
            Debug.Log($"{LogTag} Removed '{decorId}' from inventory.");
        }
    }
}
