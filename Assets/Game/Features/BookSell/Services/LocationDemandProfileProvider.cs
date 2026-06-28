using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default profile policy: sample N distinct genres (N = <see cref="SalesTuning.PassiveRequestGenreCount"/>,
    /// clamped to availability) from the location's demand genres; falls back to the genres present on
    /// the day's shelf if the location lists none. Guarantees ≥1 genre whenever any genre exists.
    /// </summary>
    public sealed class LocationDemandProfileProvider : ICustomerProfileProvider
    {
        private readonly IConfigsService _configs;
        private readonly SalesTuning _tuning;

        public LocationDemandProfileProvider(IConfigsService configs, SalesTuning tuning)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _tuning = tuning;
        }

        public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random)
        {
            var pool = ResolvePool(setup);
            if (pool.Count == 0) return CustomerProfile.Empty;   // degenerate day — resolver tolerates

            var count = _tuning?.PassiveRequestGenreCount ?? 2;
            if (count < 1) count = 1;
            if (count > pool.Count) count = pool.Count;

            return new CustomerProfile(SampleDistinct(pool, count, random));
        }

        private List<string> ResolvePool(SalesSessionSetup setup)
        {
            // TODO Gameplay: this currently treats LocationConfig.DemandGenres like an allowed-genre pool.
            // Tiny Bookshop-style demand should be wider: any stocked genre can sell, while location demand
            // genres get a higher chance/weight instead of excluding all other genres.
            // 1) Location demand genres (one source, not the truth about the customer).
            var location = !string.IsNullOrEmpty(setup?.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;
            var pool = DistinctNonEmpty(location?.DemandGenres);
            if (pool.Count > 0) return pool;

            // 2) Fallback: distinct genres present on the day's shelf.
            if (setup?.ShelfBookIds != null)
            {
                var fromShelf = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in setup.ShelfBookIds)
                {
                    if (string.IsNullOrEmpty(id) || !_configs.TryGet<BookConfig>(id, out var cfg)) continue;
                    var g = cfg?.Genre;
                    if (!string.IsNullOrEmpty(g) && seen.Add(g)) fromShelf.Add(g);
                }
                if (fromShelf.Count > 0) return fromShelf;
            }
            return new List<string>();
        }

        private static List<string> DistinctNonEmpty(IReadOnlyList<string> src)
        {
            var list = new List<string>();
            if (src == null) return list;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < src.Count; i++)
                if (!string.IsNullOrEmpty(src[i]) && seen.Add(src[i])) list.Add(src[i]);
            return list;
        }

        private static List<string> SampleDistinct(List<string> pool, int count, ISalesRandom random)
        {
            // Partial Fisher–Yates over a copy.
            var copy = new List<string>(pool);
            var result = new List<string>(count);
            for (var i = 0; i < count && copy.Count > 0; i++)
            {
                var idx = copy.Count == 1 ? 0 : random.Range(0, copy.Count);
                result.Add(copy[idx]);
                copy[idx] = copy[copy.Count - 1];
                copy.RemoveAt(copy.Count - 1);
            }
            return result;
        }
    }
}
