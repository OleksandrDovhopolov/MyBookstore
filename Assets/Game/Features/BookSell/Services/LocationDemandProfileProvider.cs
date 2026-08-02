using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default profile policy: sample N distinct genres from the whole book catalog. Each slot consumes
    /// one share roll and one bucket index roll; shelf stock affects the outcome later, not the request.
    /// </summary>
    public sealed class LocationDemandProfileProvider : ICustomerProfileProvider
    {
        private const string LogTag = "[Sales.Demand]";

        private readonly IConfigsService _configs;
        private readonly SalesTuning _tuning;

        public LocationDemandProfileProvider(
            IConfigsService configs,
            SalesTuning tuning)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _tuning = tuning;
        }

        public CustomerProfile Create(SalesSessionSetup setup, ISalesRandom random)
        {
            var catalogGenres = ResolveCatalogGenres();
            if (catalogGenres.Count == 0) return CustomerProfile.Empty;   // degenerate catalog — resolver tolerates

            var count = _tuning?.PassiveRequestGenreCount ?? 2;
            if (count < 1) count = 1;
            if (count > catalogGenres.Count) count = catalogGenres.Count;

            var location = ResolveLocation(setup);
            var profile = SampleDistinctByDemandShare(catalogGenres, count, location, random);
            if (!Application.isBatchMode)
            {
                Debug.Log($"{LogTag} location={location?.Id ?? setup?.LocationId ?? "<none>"} " +
                          $"share={ResolveDemandShare(location):0.###} profile=[{string.Join(",", profile.Genres)}] " +
                          $"demandSlots={profile.DemandSlots}/{profile.Genres.Count}");
            }

            return new CustomerProfile(profile.Genres);
        }

        private List<string> ResolveCatalogGenres()
        {
            var catalog = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cfg in _configs.GetAll<BookConfig>())
            {
                var genre = cfg?.PrimaryGenre;
                if (!string.IsNullOrEmpty(genre) && seen.Add(genre)) catalog.Add(genre);
            }

            return catalog;
        }

        private LocationConfig ResolveLocation(SalesSessionSetup setup)
            => !string.IsNullOrEmpty(setup?.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;

        private DemandProfileSample SampleDistinctByDemandShare(
            List<string> catalogGenres,
            int count,
            LocationConfig location,
            ISalesRandom random)
        {
            var result = new List<string>(count);
            var demand = new List<string>();
            var other = new List<string>();
            SplitDemandBuckets(catalogGenres, location, demand, other);

            var share = ResolveDemandShare(location);
            var demandSlots = 0;

            for (var i = 0; i < count && (demand.Count > 0 || other.Count > 0); i++)
            {
                var wantsDemand = random.NextDouble() < share;
                var bucket = wantsDemand ? demand : other;
                var isDemandBucket = wantsDemand;
                if (bucket.Count == 0)
                {
                    bucket = wantsDemand ? other : demand;
                    isDemandBucket = !wantsDemand;
                }

                var idx = random.Range(0, bucket.Count);
                result.Add(bucket[idx]);
                if (isDemandBucket) demandSlots++;
                bucket[idx] = bucket[bucket.Count - 1];
                bucket.RemoveAt(bucket.Count - 1);
            }

            return new DemandProfileSample(result, demandSlots);
        }

        private static void SplitDemandBuckets(
            IReadOnlyList<string> catalogGenres,
            LocationConfig location,
            List<string> demand,
            List<string> other)
        {
            var demandSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var authored = location?.DemandGenres;
            if (authored != null)
            {
                for (var i = 0; i < authored.Length; i++)
                    if (!string.IsNullOrEmpty(authored[i]))
                        demandSet.Add(authored[i]);
            }

            for (var i = 0; i < catalogGenres.Count; i++)
            {
                var genre = catalogGenres[i];
                if (demandSet.Contains(genre))
                    demand.Add(genre);
                else
                    other.Add(genre);
            }
        }

        private double ResolveDemandShare(LocationConfig location)
        {
            var authoredCount = location?.DemandGenres?.Length ?? 0;
            var threshold = _tuning?.NarrowDemandGenreThreshold ?? 3;
            var share = authoredCount >= threshold
                ? _tuning?.PassiveDemandRequestShare ?? 0.70d
                : _tuning?.PassiveDemandRequestShareNarrow ?? 0.50d;

            if (double.IsNaN(share) || double.IsInfinity(share)) return 0d;
            if (share < 0d) return 0d;
            if (share > 1d) return 1d;
            return share;
        }

        private readonly struct DemandProfileSample
        {
            public DemandProfileSample(List<string> genres, int demandSlots)
            {
                Genres = genres;
                DemandSlots = demandSlots;
            }

            public List<string> Genres { get; }
            public int DemandSlots { get; }
        }
    }
}
