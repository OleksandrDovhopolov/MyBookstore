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
    /// Sanity check for the days.json traffic table: a day must not ask for more active requests than it
    /// has customers, or the surplus silently goes unserved (the spawner caps it). Scans the WHOLE day
    /// table, not the current day — warnings name the offending day id.
    ///
    /// Cadence: registered as an entry point in the LOCATION scope, so <see cref="StartAsync"/> runs on
    /// every LocationScene load (once per day entry), not once at boot. It is a static-config check, so a
    /// process-wide latch keeps it to a single run — otherwise the same warnings repeat every day.
    /// </summary>
    public sealed class CustomerTrafficConfigValidator : IAsyncStartable
    {
        private const string LogTag = "[TrafficValidator]";

        // Static config never changes within a process; the validator rides a per-location-load entry
        // point, so latch it to keep the warnings to one run.
        private static bool _validatedThisProcess;

        private readonly IConfigsService _configs;
        private readonly SalesTrafficSettings _settings;

        public CustomerTrafficConfigValidator(IConfigsService configs)
            : this(configs, new SalesTrafficSettings())
        {
        }

        public CustomerTrafficConfigValidator(IConfigsService configs, SalesTrafficSettings settings)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

        /// <summary>Pure validation used by tests: one warning per day that over-asks for active requests.</summary>
        public IReadOnlyList<string> Validate()
        {
            var warnings = new List<string>();

            foreach (var day in _configs.GetAll<DayConfig>())
            {
                if (day == null) continue;

                var customers = day.CustomerCount ?? _settings.DefaultCustomerCount;
                var requests = day.ActiveRequestCount ?? _settings.DefaultActiveRequestCount;

                if (requests > customers)
                {
                    warnings.Add(
                        $"day '{day.Id}' (index {day.DayIndex}) asks for activeRequestCount={requests} " +
                        $"but only has customerCount={customers}. The spawner caps requests at the customer " +
                        "count, so the surplus would go unserved.");
                }
            }

            return warnings;
        }
    }
}
