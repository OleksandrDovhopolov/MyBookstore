using System;
using System.Collections.Generic;
using Game.Rewards.Services;
using Game.Shop.API;
using UnityEngine;
using Random = System.Random;

namespace Game.Shop.Services
{
    public static class BookBoxRotation
    {
        private const int TargetGeneralCount = 2;
        private const int TargetGenreCount = 2;
        private const int MaxOfferedCount = TargetGeneralCount + TargetGenreCount;

        public static IReadOnlyList<ShopLot> Select(IReadOnlyList<ShopLot> bookLots, int day)
        {
            if (bookLots == null || bookLots.Count == 0) return Array.Empty<ShopLot>();

            var general = new List<ShopLot>();
            var genre = new List<ShopLot>();
            for (var i = 0; i < bookLots.Count; i++)
            {
                var lot = bookLots[i];
                if (lot == null) continue;

                if (!BookBoxPoolRules.TryGet(lot.RewardId, out var rule))
                {
                    Debug.LogWarning(
                        $"[Shop] Book lot '{lot.LotId}' rewardId '{lot.RewardId}' has no BookBoxPoolRules rule; " +
                        "it will not be offered by daily rotation.");
                    continue;
                }

                if (rule.Kind == BookBoxKind.Genre)
                    genre.Add(lot);
                else
                    general.Add(lot);
            }

            SortByLotId(general);
            SortByLotId(genre);

            var rng = new Random(day);
            Shuffle(general, rng);
            Shuffle(genre, rng);

            var selectedGeneral = Take(general, 0, TargetGeneralCount);
            var selectedGenre = Take(genre, 0, TargetGenreCount);

            if (selectedGeneral.Count < TargetGeneralCount)
            {
                var needed = TargetGeneralCount - selectedGeneral.Count;
                selectedGenre.AddRange(Take(genre, selectedGenre.Count, needed));
            }

            if (selectedGenre.Count < TargetGenreCount)
            {
                var needed = TargetGenreCount - selectedGenre.Count;
                selectedGeneral.AddRange(Take(general, selectedGeneral.Count, needed));
            }

            var result = new List<ShopLot>(MaxOfferedCount);
            result.AddRange(selectedGeneral);
            result.AddRange(selectedGenre);
            if (result.Count > MaxOfferedCount)
                result.RemoveRange(MaxOfferedCount, result.Count - MaxOfferedCount);

            return result;
        }

        private static void SortByLotId(List<ShopLot> lots) =>
            lots.Sort((a, b) => string.Compare(a?.LotId, b?.LotId, StringComparison.Ordinal));

        private static List<ShopLot> Take(IReadOnlyList<ShopLot> lots, int start, int count)
        {
            var result = new List<ShopLot>(count);
            if (lots == null || count <= 0) return result;

            for (var i = start; i < lots.Count && result.Count < count; i++)
                result.Add(lots[i]);

            return result;
        }

        private static void Shuffle<T>(IList<T> items, Random rng)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
