using System;
using System.Collections.Generic;
using System.Linq;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.Services
{
    public sealed class ConsumableRowSource : IInventoryRowSource
    {
        private const string LogPrefix = "[InventoryRows]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;

        public ConsumableRowSource(IInventoryService inventory, IConfigsService configs)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public int Order => 30;
        public string CategoryId => InventoryCategories.Consumable;

        public IEnumerable<InventoryRowModel> BuildRows()
        {
            var items = _inventory.GetByCategory(CategoryId)
                .OrderBy(it => it.ItemId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!_configs.TryGet<ConsumableConfig>(item.ItemId, out var config) || config == null)
                {
                    Debug.LogWarning($"{LogPrefix} Missing ConsumableConfig for category '{CategoryId}' item '{item.ItemId}'.");
                    continue;
                }

                yield return new InventoryRowModel(
                    config.Id,
                    item.Count,
                    config.Id,
                    InventoryRowStyle.Default,
                    false);
            }
        }
    }
}
