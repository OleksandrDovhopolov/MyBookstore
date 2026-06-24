using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Rewards.API;
using UnityEngine;

namespace Game.Newspaper.UI
{
    public static class RewardsWindowRewardBuilder
    {
        public static List<RewardSpecResource> Build(RewardSpec granted, IConfigsService configs)
        {
            var result = new List<RewardSpecResource>();
            if (granted?.Items == null || granted.Items.Count == 0) return result;

            var genreAmounts = new Dictionary<BookGenre, int>();
            var decorOrder = new List<string>();
            var decorAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var decorConfigs = new Dictionary<string, DecorConfig>(StringComparer.Ordinal);

            for (var i = 0; i < granted.Items.Count; i++)
            {
                var item = granted.Items[i];
                var amount = Mathf.Max(0, item.Amount);
                if (amount <= 0) continue;

                if (IsCategory(item, InventoryCategories.Book))
                {
                    AccumulateBook(item, amount, configs, genreAmounts);
                    continue;
                }

                if (IsCategory(item, InventoryCategories.Decor))
                {
                    AccumulateDecor(item, amount, configs, decorOrder, decorAmounts, decorConfigs);
                    continue;
                }

                Debug.LogWarning(
                    $"[RewardsWindow] Invalid reward item '{item.Id}' (kind={item.Kind}, category='{item.Category}'). Skipped.");
            }

            AddGenreResources(result, genreAmounts);
            AddDecorResources(result, decorOrder, decorAmounts, decorConfigs);
            return result;
        }

        private static bool IsCategory(RewardItem item, string category) =>
            item.Kind == RewardKind.InventoryItem
            && string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase);

        private static void AccumulateBook(
            RewardItem item,
            int amount,
            IConfigsService configs,
            IDictionary<BookGenre, int> genreAmounts)
        {
            if (configs == null
                || !configs.TryGet<BookConfig>(item.Id, out var book)
                || book == null
                || !BookGenreExtensions.TryParseGenre(book.Genre, out var genre))
            {
                Debug.LogWarning(
                    $"[RewardsWindow] Book reward '{item.Id}' has no valid BookConfig/genre. Skipped.");
                return;
            }

            genreAmounts.TryGetValue(genre, out var current);
            genreAmounts[genre] = current + amount;
        }

        private static void AccumulateDecor(
            RewardItem item,
            int amount,
            IConfigsService configs,
            ICollection<string> decorOrder,
            IDictionary<string, int> decorAmounts,
            IDictionary<string, DecorConfig> decorConfigs)
        {
            if (configs == null
                || !configs.TryGet<DecorConfig>(item.Id, out var decor)
                || decor == null)
            {
                Debug.LogWarning(
                    $"[RewardsWindow] Decor reward '{item.Id}' has no DecorConfig. Skipped.");
                return;
            }

            if (!decorAmounts.ContainsKey(item.Id))
            {
                decorOrder.Add(item.Id);
                decorConfigs[item.Id] = decor;
            }

            decorAmounts.TryGetValue(item.Id, out var current);
            decorAmounts[item.Id] = current + amount;
        }

        private static void AddGenreResources(
            ICollection<RewardSpecResource> result,
            IReadOnlyDictionary<BookGenre, int> genreAmounts)
        {
            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                if (!genreAmounts.TryGetValue(genre, out var amount) || amount <= 0) continue;

                var name = genre.ToString();
                result.Add(new RewardSpecResource
                {
                    ResourceId = name,
                    DisplayName = name,
                    Kind = RewardKind.InventoryItem,
                    Category = InventoryCategories.Book,
                    Amount = amount,
                    Icon = null
                });
            }
        }

        private static void AddDecorResources(
            ICollection<RewardSpecResource> result,
            IReadOnlyList<string> decorOrder,
            IReadOnlyDictionary<string, int> decorAmounts,
            IReadOnlyDictionary<string, DecorConfig> decorConfigs)
        {
            for (var i = 0; i < decorOrder.Count; i++)
            {
                var id = decorOrder[i];
                if (!decorAmounts.TryGetValue(id, out var amount) || amount <= 0) continue;
                if (!decorConfigs.TryGetValue(id, out var decor) || decor == null) continue;

                result.Add(new RewardSpecResource
                {
                    ResourceId = decor.Id,
                    DisplayName = decor.DisplayName,
                    Kind = RewardKind.InventoryItem,
                    Category = InventoryCategories.Decor,
                    Amount = amount,
                    Icon = null
                });
            }
        }
    }
}
