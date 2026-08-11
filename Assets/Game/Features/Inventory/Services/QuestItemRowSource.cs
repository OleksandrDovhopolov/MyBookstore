using System;
using System.Collections.Generic;
using System.Linq;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using UnityEngine;

namespace Game.Inventory.Services
{
    public sealed class QuestItemRowSource : IInventoryRowSource
    {
        private const string LogPrefix = "[InventoryRows]";

        private readonly IInventoryService _inventory;
        private readonly IConfigsService _configs;

        public QuestItemRowSource(IInventoryService inventory, IConfigsService configs)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public int Order => 20;
        public string CategoryId => InventoryCategories.QuestItem;

        public IEnumerable<InventoryRowModel> BuildRows()
        {
            var items = _inventory.GetByCategory(CategoryId)
                .OrderBy(it => it.ItemId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!_configs.TryGet<QuestItemConfig>(item.ItemId, out var config) || config == null)
                {
                    Debug.LogWarning($"{LogPrefix} Missing QuestItemConfig for category '{CategoryId}' item '{item.ItemId}'.");
                    continue;
                }

                yield return new InventoryRowModel(
                    config.Id,
                    0,
                    config.Id,
                    InventoryRowStyle.QuestItem,
                    false);
            }
        }
    }
}
