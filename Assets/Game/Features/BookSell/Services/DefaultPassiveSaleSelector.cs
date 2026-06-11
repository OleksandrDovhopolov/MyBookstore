using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="IPassiveSaleSelector"/>
    public sealed class DefaultPassiveSaleSelector : IPassiveSaleSelector
    {
        public ShelfBook PickPassiveSale(IReadOnlyList<ShelfBook> shelf, LocationConfig location, ISalesRandom random)
        {
            if (shelf == null || shelf.Count == 0 || location == null || random == null)
                return null;

            // Сначала собираем кандидатов: Available + матч по DemandGenres или DemandTags.
            var candidates = new List<ShelfBook>(shelf.Count);
            for (var i = 0; i < shelf.Count; i++)
            {
                var book = shelf[i];
                if (book.State != ShelfBookState.Available) continue;
                if (MatchesLocationDemand(book, location))
                    candidates.Add(book);
            }

            if (candidates.Count == 0) return null;

            var index = random.Range(0, candidates.Count);
            return candidates[index];
        }

        private static bool MatchesLocationDemand(ShelfBook book, LocationConfig location)
        {
            var cfg = book.Config;

            if (location.DemandGenres != null && !string.IsNullOrEmpty(cfg.Genre))
            {
                foreach (var g in location.DemandGenres)
                {
                    if (string.Equals(g, cfg.Genre, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (location.DemandTags != null && cfg.Tags != null)
            {
                foreach (var lt in location.DemandTags)
                {
                    if (string.IsNullOrEmpty(lt)) continue;
                    foreach (var bt in cfg.Tags)
                    {
                        if (string.IsNullOrEmpty(bt)) continue;
                        if (string.Equals(lt, bt, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
