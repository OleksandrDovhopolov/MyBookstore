using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.Services
{
    public sealed class ActiveRequestGenreCoverageValidator : IAsyncStartable
    {
        private const string LogTag = "[ActiveRequestGenres]";

        private static bool _validatedThisProcess;

        private readonly IConfigsService _configs;
        private readonly IActiveRequestGenreResolver _genreResolver;

        public ActiveRequestGenreCoverageValidator(IConfigsService configs)
            : this(configs, new ConditionActiveRequestGenreResolver())
        {
        }

        public ActiveRequestGenreCoverageValidator(
            IConfigsService configs,
            IActiveRequestGenreResolver genreResolver)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _genreResolver = genreResolver ?? throw new ArgumentNullException(nameof(genreResolver));
        }

        /// <summary>Test hook: clears the process latch so a test can run the validator again.</summary>
        public static void ResetValidationLatch() => _validatedThisProcess = false;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (_validatedThisProcess) return;
            _validatedThisProcess = true;

            await _configs.WarmupAsync(cancellation);

            var report = Validate();
            for (var i = 0; i < report.Errors.Count; i++)
                Debug.LogError($"{LogTag} config scan: {report.Errors[i]}");
            for (var i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning($"{LogTag} config scan: {report.Warnings[i]}");
        }

        public ActiveRequestGenreCoverageReport Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var coveredGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var request in _configs.GetAll<RequestDefinitionConfig>())
            {
                if (request == null || !request.Enabled) continue;

                var id = string.IsNullOrWhiteSpace(request.Id) ? "<missing-id>" : request.Id;
                var genres = _genreResolver.Resolve(request);
                if (genres == null || genres.Count == 0)
                {
                    warnings.Add($"request '{id}' has no resolved genres; it can match every customer profile.");
                    continue;
                }

                for (var i = 0; i < genres.Count; i++)
                    coveredGenres.Add(genres[i]);

                var metadataGenre = request.Genre;
                if (!string.IsNullOrWhiteSpace(metadataGenre) && !ContainsGenre(genres, metadataGenre))
                {
                    warnings.Add($"request '{id}' metadata genre '{metadataGenre}' is not included in resolved genres [{string.Join(",", genres)}].");
                }
            }

            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                var name = genre.ToString();
                if (!coveredGenres.Contains(name))
                    errors.Add($"genre '{name}' has no enabled active request; customers with this DesiredGenres entry will fall back.");
            }

            return new ActiveRequestGenreCoverageReport(errors, warnings);
        }

        private static bool ContainsGenre(IReadOnlyList<string> genres, string expected)
        {
            for (var i = 0; i < genres.Count; i++)
                if (string.Equals(genres[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
