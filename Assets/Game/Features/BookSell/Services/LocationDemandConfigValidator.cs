using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.Services
{
    /// <summary>
    /// Sanity check for LocationConfig.DemandGenres. Passive demand profiles sample from the book
    /// catalog, while the UI renders only parsed BookGenre values, so authored demand must satisfy both.
    /// </summary>
    public sealed class LocationDemandConfigValidator : IAsyncStartable
    {
        private const string LogTag = "[DemandValidator]";

        private static bool _validatedThisProcess;

        private readonly IConfigsService _configs;
        private readonly SalesTuning _tuning;

        public LocationDemandConfigValidator(IConfigsService configs)
            : this(configs, new SalesTuning())
        {
        }

        public LocationDemandConfigValidator(IConfigsService configs, SalesTuning tuning)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _tuning = tuning ?? new SalesTuning();
        }

        /// <summary>Test hook: clears the process latch so a test can run the validator again.</summary>
        public static void ResetValidationLatch() => _validatedThisProcess = false;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (_validatedThisProcess) return;
            _validatedThisProcess = true;

            await _configs.WarmupAsync(cancellation);

            foreach (var warning in Validate())
                Debug.LogWarning($"{LogTag} config scan: {warning}");
        }

        /// <summary>Pure validation used by tests: one warning per location demand issue.</summary>
        public IReadOnlyList<string> Validate()
        {
            var warnings = new List<string>();
            var catalogGenres = ResolveCatalogGenres();

            foreach (var location in _configs.GetAll<LocationConfig>())
            {
                if (location == null) continue;

                var locationId = string.IsNullOrEmpty(location.Id) ? "<missing-id>" : location.Id;
                var demandGenres = location.DemandGenres;
                if (demandGenres == null || demandGenres.Length == 0)
                {
                    warnings.Add($"location '{locationId}' has no demandGenres; passive demand share falls back to non-demand catalog genres.");
                    continue;
                }

                if (demandGenres.Length < 2)
                    warnings.Add($"location '{locationId}' has fewer than 2 demandGenres; passive demand share can drift with distinct profile sampling.");

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < demandGenres.Length; i++)
                {
                    var genre = demandGenres[i];
                    if (string.IsNullOrWhiteSpace(genre))
                    {
                        warnings.Add($"location '{locationId}' has an empty demand genre at index {i}.");
                        continue;
                    }

                    if (!seen.Add(genre))
                        warnings.Add($"location '{locationId}' repeats demand genre '{genre}'.");

                    if (!BookGenreExtensions.TryParseGenre(genre, out _))
                        warnings.Add($"location '{locationId}' demand genre '{genre}' is not a valid BookGenre and will not render in LocationWindow.");

                    if (!catalogGenres.Contains(genre))
                        warnings.Add($"location '{locationId}' demand genre '{genre}' has no BookConfig.PrimaryGenre in the catalog.");
                }

                ValidateBuckets(locationId, seen, catalogGenres, warnings);
            }

            return warnings;
        }

        private void ValidateBuckets(
            string locationId,
            HashSet<string> demandGenres,
            HashSet<string> catalogGenres,
            List<string> warnings)
        {
            var requestedCount = _tuning.PassiveRequestGenreCount;
            if (requestedCount < 1) requestedCount = 1;
            if (requestedCount > catalogGenres.Count) requestedCount = catalogGenres.Count;
            if (requestedCount <= 0) return;

            var demandCount = 0;
            foreach (var genre in catalogGenres)
                if (demandGenres.Contains(genre))
                    demandCount++;

            var otherCount = catalogGenres.Count - demandCount;
            if (demandCount < requestedCount)
            {
                warnings.Add($"location '{locationId}' has only {demandCount} catalog-backed demand genres, " +
                             $"but passiveRequestGenreCount is {requestedCount}; demand share can drift through bucket fallback.");
            }

            if (otherCount < requestedCount)
            {
                warnings.Add($"location '{locationId}' has only {otherCount} non-demand catalog genres, " +
                             $"but passiveRequestGenreCount is {requestedCount}; non-demand share can drift through bucket fallback.");
            }
        }

        private HashSet<string> ResolveCatalogGenres()
        {
            var catalogGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var book in _configs.GetAll<BookConfig>())
            {
                var genre = book?.PrimaryGenre;
                if (!string.IsNullOrEmpty(genre)) catalogGenres.Add(genre);
            }

            return catalogGenres;
        }
    }
}
