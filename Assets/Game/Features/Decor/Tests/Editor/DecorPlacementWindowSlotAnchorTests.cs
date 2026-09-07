using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Decor.UI;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Decor.Tests.Editor
{
    public sealed class DecorPlacementWindowSlotAnchorTests
    {
        private const string PrefabPath = "Assets/Game/Features/Decor/UI/Prefab/DecorPlacementWindow.prefab";
        private const string BookshopsPath = "Assets/Configs/bookshops.json";

        [Test]
        public void PrefabSlotAnchors_MatchMainBookshopConfig()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab, $"Decor placement prefab not found at {PrefabPath}.");

            var view = prefab.GetComponentInChildren<DecorPlacementWindowView>(true);
            Assert.NotNull(view, $"Decor placement prefab has no {nameof(DecorPlacementWindowView)}.");

            var configSlotIds = LoadMainBookshopSlotIds();
            AssertAnchorsMatchConfig(
                "serialized SlotAnchors",
                view.SlotAnchors ?? Array.Empty<DecorSlotAnchorView>(),
                configSlotIds);
            AssertAnchorsMatchConfig(
                "hierarchy DecorSlotAnchorView components",
                view.GetComponentsInChildren<DecorSlotAnchorView>(true),
                configSlotIds);
        }

        private static HashSet<string> LoadMainBookshopSlotIds()
        {
            var shops = JsonConvert.DeserializeObject<BookShopConfig[]>(File.ReadAllText(BookshopsPath));
            var shop = shops?.FirstOrDefault(s => s?.Id == DecorPlacementService.HardcodedBookShopId);
            Assert.NotNull(shop, $"BookShop '{DecorPlacementService.HardcodedBookShopId}' not found in {BookshopsPath}.");
            Assert.NotNull(shop?.DecorSlots, $"BookShop '{DecorPlacementService.HardcodedBookShopId}' has no decor slots.");

            return new HashSet<string>(
                shop.DecorSlots
                    .Where(s => s != null)
                    .Select(s => s.Id),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void AssertAnchorsMatchConfig(
            string source,
            IReadOnlyList<DecorSlotAnchorView> anchors,
            HashSet<string> configSlotIds)
        {
            Assert.NotNull(anchors, $"{source} is null.");

            var anchorSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                Assert.NotNull(anchor, $"{source}[{i}] is null.");
                Assert.IsFalse(string.IsNullOrEmpty(anchor.SlotId), $"{source}[{i}] has empty SlotId.");
                Assert.IsTrue(anchorSlotIds.Add(anchor.SlotId), $"{source} has duplicate SlotId '{anchor.SlotId}'.");
                Assert.IsTrue(configSlotIds.Contains(anchor.SlotId), $"{source} contains unknown SlotId '{anchor.SlotId}'.");
            }

            foreach (var slotId in configSlotIds)
                Assert.IsTrue(anchorSlotIds.Contains(slotId), $"{source} is missing anchor for slot '{slotId}'.");
        }
    }
}
