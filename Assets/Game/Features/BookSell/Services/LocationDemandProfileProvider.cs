using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default profile policy: sample N distinct genres (N = <see cref="SalesTuning.PassiveRequestGenreCount"/>,
    /// clamped to availability) from genres present on the day's shelf. Location demand genres stay
    /// sellable-demand hints: they receive extra weight but do not exclude other stocked genres.
    /// </summary>
    public sealed class LocationDemandProfileProvider : ICustomerProfileProvider
    {
        private readonly IConfigsService _configs;
        private readonly SalesTuning _tuning;
        private readonly IDemandGenreWeightProvider _demandWeights;

        public LocationDemandProfileProvider(
            IConfigsService configs,
            SalesTuning tuning,
            IDemandGenreWeightProvider demandWeights)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _tuning = tuning;
            _demandWeights = demandWeights ?? throw new ArgumentNullException(nameof(demandWeights));
        }

        public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random)
        {
            var pool = ResolvePool(setup);
            if (pool.Count == 0) return CustomerProfile.Empty;   // degenerate day — resolver tolerates

            var count = _tuning?.PassiveRequestGenreCount ?? 2;
            if (count < 1) count = 1;
            if (count > pool.Count) count = pool.Count;

            var location = ResolveLocation(setup);
            return new CustomerProfile(SampleDistinctWeighted(pool, count, location, random));
        }

        private List<string> ResolvePool(SalesSessionSetup setup)
        {
            var fromShelf = new List<string>();
            if (setup?.ShelfBookIds != null)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in setup.ShelfBookIds)
                {
                    if (string.IsNullOrEmpty(id) || !_configs.TryGet<BookConfig>(id, out var cfg)) continue;
                    var g = cfg?.PrimaryGenre;
                    if (!string.IsNullOrEmpty(g) && seen.Add(g)) fromShelf.Add(g);
                }
            }
            return fromShelf;
        }

        private LocationConfig ResolveLocation(SalesSessionSetup setup)
            => !string.IsNullOrEmpty(setup?.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;

        private List<string> SampleDistinctWeighted(
            List<string> pool,
            int count,
            LocationConfig location,
            ISalesRandom random)
        {
            var copy = new List<string>(pool);
            var result = new List<string>(count);
            for (var i = 0; i < count && copy.Count > 0; i++)
            {
                var idx = PickWeightedIndex(copy, location, random);
                result.Add(copy[idx]);
                copy[idx] = copy[copy.Count - 1];
                copy.RemoveAt(copy.Count - 1);
            }
            return result;
        }

        private int PickWeightedIndex(IReadOnlyList<string> genres, LocationConfig location, ISalesRandom random)
        {
            if (genres.Count == 1) return 0;

            double total = 0d;
            for (var i = 0; i < genres.Count; i++)
                total += Math.Max(1d, _demandWeights.GetWeight(genres[i], location));

            if (total <= 0d)
                return random.Range(0, genres.Count);

            var roll = random.NextDouble() * total;
            double cumulative = 0d;
            for (var i = 0; i < genres.Count; i++)
            {
                cumulative += Math.Max(1d, _demandWeights.GetWeight(genres[i], location));
                if (roll < cumulative) return i;
            }

            return genres.Count - 1;
        }
    }
}
