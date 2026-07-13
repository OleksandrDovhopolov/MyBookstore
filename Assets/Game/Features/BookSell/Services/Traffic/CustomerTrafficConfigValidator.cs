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
    /// Boot-time sanity check for customer-traffic config: a hard-override day (<c>applyModifiers = false</c>)
    /// declares an EXACT regular count, so the request-count floor is skipped for it. If that exact count is
    /// below the number of active requests, some scripted active requests would go unserved — warn the
    /// designer instead of silently raising the day. See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficConfigValidator : IAsyncStartable
    {
        private const string LogTag = "[TrafficValidator]";

        private readonly IConfigsService _configs;
        private readonly IActiveRequestRuntimeProvider _activeRequests;

        public CustomerTrafficConfigValidator(IConfigsService configs)
            : this(configs, new ConfigActiveRequestRuntimeProvider(configs, new BookConditionRequestEvaluator()))
        {
        }

        public CustomerTrafficConfigValidator(
            IConfigsService configs,
            IActiveRequestRuntimeProvider activeRequests)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _activeRequests = activeRequests ?? throw new ArgumentNullException(nameof(activeRequests));
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            await _configs.WarmupAsync(cancellation);

            foreach (var warning in Validate())
                Debug.LogWarning($"{LogTag} {warning}");
        }

        /// <summary>Pure validation used by tests: returns one warning per under-supplied hard-override day.</summary>
        public IReadOnlyList<string> Validate()
        {
            var warnings = new List<string>();
            var requestCount = _activeRequests.GetRequests().Count;

            foreach (var day in _configs.GetAll<DayConfig>())
            {
                if (day.ApplyModifiers != false) continue;       // only hard-override days
                if (!day.CustomerCount.HasValue) continue;

                if (day.CustomerCount.Value < requestCount)
                {
                    warnings.Add(
                        $"Day '{day.Id}' (index {day.DayIndex}) is a hard override with customerCount=" +
                        $"{day.CustomerCount.Value}, below the {requestCount} active request(s). " +
                        "The floor is skipped for hard-override days, so those requests would go unserved.");
                }
            }

            return warnings;
        }
    }
}
