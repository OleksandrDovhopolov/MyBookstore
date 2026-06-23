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
        public const string NonBookRewardId = "non_book_reward";
        private const string NonBookDisplayName = "Reward";

        private static readonly string[] KnownGenreOrder =
        {
            "Classic",
            "Crime",
            "Drama",
            "Fact",
            "Fantasy",
            "Kids",
            "Travel",
        };

        public static List<RewardSpecResource> Build(
            RewardSpec granted,
            IConfigsService configs,
            Func<string, Sprite> resolveIcon)
        {
            var result = new List<RewardSpecResource>();
            if (granted?.Items == null || granted.Items.Count == 0) return result;

            var genreAmounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var genreDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var nonBookAmount = 0;

            for (var i = 0; i < granted.Items.Count; i++)
            {
                var item = granted.Items[i];
                var amount = Mathf.Max(0, item.Amount);
                if (amount <= 0) continue;

                if (IsBookItem(item))
                {
                    if (configs != null
                        && configs.TryGet<BookConfig>(item.Id, out var book)
                        && book != null
                        && !string.IsNullOrEmpty(book.Genre))
                    {
                        AddGenre(genreAmounts, genreDisplayNames, book.Genre, amount);
                        continue;
                    }

                    Debug.LogWarning($"[RewardsWindow] BookConfig '{item.Id}' not found for granted book reward. Using fallback reward icon.");
                }

                nonBookAmount += amount;
            }

            AddKnownGenres(result, genreAmounts, genreDisplayNames, resolveIcon);
            AddUnknownGenres(result, genreAmounts, genreDisplayNames, resolveIcon);

            if (nonBookAmount > 0)
            {
                result.Add(new RewardSpecResource
                {
                    ResourceId = NonBookRewardId,
                    DisplayName = NonBookDisplayName,
                    Kind = RewardKind.Resource,
                    Amount = nonBookAmount,
                    Icon = resolveIcon?.Invoke(NonBookRewardId)
                });
            }

            return result;
        }

        private static bool IsBookItem(RewardItem item) =>
            item.Kind == RewardKind.InventoryItem
            && string.Equals(item.Category, InventoryCategories.Book, StringComparison.OrdinalIgnoreCase);

        private static void AddGenre(
            IDictionary<string, int> amounts,
            IDictionary<string, string> displayNames,
            string genre,
            int amount)
        {
            amounts.TryGetValue(genre, out var current);
            amounts[genre] = current + amount;
            if (!displayNames.ContainsKey(genre))
                displayNames[genre] = genre;
        }

        private static void AddKnownGenres(
            ICollection<RewardSpecResource> result,
            IDictionary<string, int> amounts,
            IReadOnlyDictionary<string, string> displayNames,
            Func<string, Sprite> resolveIcon)
        {
            for (var i = 0; i < KnownGenreOrder.Length; i++)
            {
                var genre = KnownGenreOrder[i];
                if (!amounts.TryGetValue(genre, out var amount) || amount <= 0) continue;

                result.Add(CreateGenreResource(genre, displayNames, amount, resolveIcon));
            }
        }

        private static void AddUnknownGenres(
            ICollection<RewardSpecResource> result,
            IDictionary<string, int> amounts,
            IReadOnlyDictionary<string, string> displayNames,
            Func<string, Sprite> resolveIcon)
        {
            foreach (var pair in amounts)
            {
                if (pair.Value <= 0 || IsKnownGenre(pair.Key)) continue;
                result.Add(CreateGenreResource(pair.Key, displayNames, pair.Value, resolveIcon));
            }
        }

        private static bool IsKnownGenre(string genre)
        {
            for (var i = 0; i < KnownGenreOrder.Length; i++)
                if (string.Equals(KnownGenreOrder[i], genre, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static RewardSpecResource CreateGenreResource(
            string genre,
            IReadOnlyDictionary<string, string> displayNames,
            int amount,
            Func<string, Sprite> resolveIcon)
        {
            displayNames.TryGetValue(genre, out var displayName);
            return new RewardSpecResource
            {
                ResourceId = genre,
                DisplayName = displayName ?? genre,
                Kind = RewardKind.InventoryItem,
                Category = InventoryCategories.Book,
                Amount = amount,
                Icon = resolveIcon?.Invoke(genre)
            };
        }
    }
}
