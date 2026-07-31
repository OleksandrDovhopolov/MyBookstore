using System;
using System.Threading;
using cheatModule;
using Cysharp.Threading.Tasks;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Cheat
{
    /// <summary>One-click cheats for story-gating items that are awkward to obtain during smoke tests.</summary>
    public sealed class StoryItemCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Story Items";
        private const string LogTag = "[StoryItemCheat]";

        private const string MapItemId = "map";
        private const string PostcardItemId = "postcard";

        private readonly IInventoryService _inventory;
        private readonly CancellationToken _ct;

        public StoryItemCheatModule(IInventoryService inventory, CancellationToken ct)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _ct = ct;
        }

        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("+1 Map", () => AddAsync(MapItemId, InventoryCategories.QuestItem, 1).Forget())
                    .WithGroup(CardsGroup));

            cheatsContainer.AddItem<CheatButtonItem>(item =>
                item.OnClick("+1 Postcard", () => AddAsync(PostcardItemId, InventoryCategories.Consumable, 1).Forget())
                    .WithGroup(CardsGroup));
        }

        private async UniTaskVoid AddAsync(string itemId, string categoryId, int amount)
        {
            await _inventory.AddAsync(itemId, categoryId, amount, _ct);
            Debug.Log($"{LogTag} Added {amount} '{itemId}'.");
        }
    }
}
