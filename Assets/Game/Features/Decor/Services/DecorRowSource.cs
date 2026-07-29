using System;
using System.Collections.Generic;
using System.Linq;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Decor.Services
{
    public sealed class DecorRowSource : IInventoryRowSource
    {
        private const string LogPrefix = "[InventoryRows]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;
        private readonly IDecorPlacementService _placement;

        public DecorRowSource(
            IInventoryService inventory,
            IConfigsService configs,
            IDecorPlacementService placement)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _placement = placement;
        }

        public int Order => 10;

        public IEnumerable<InventoryRowModel> BuildRows()
        {
            var items = _inventory.GetByCategory(InventoryCategories.Decor)
                .OrderBy(it => it.ItemId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!_configs.TryGet<DecorConfig>(item.ItemId, out var config) || config == null)
                {
                    Debug.LogWarning($"{LogPrefix} Missing DecorConfig for category '{InventoryCategories.Decor}' item '{item.ItemId}'.");
                    continue;
                }

                yield return new InventoryRowModel(
                    config.Id,
                    0,
                    item.ItemId,
                    InventoryRowStyle.Decor,
                    IsPlaced(item.ItemId));
            }
        }

        private bool IsPlaced(string decorId)
        {
            if (_placement == null || string.IsNullOrEmpty(decorId)) return false;

            var placements = _placement.GetAllPlacements();
            for (var i = 0; i < placements.Count; i++)
            {
                var entry = placements[i];
                if (entry != null && string.Equals(entry.DecorId, decorId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
