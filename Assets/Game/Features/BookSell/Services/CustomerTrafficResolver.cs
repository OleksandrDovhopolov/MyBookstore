using System;
using System.Collections.Generic;
using System.Globalization;
using Book.Sell.API;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default <see cref="ICustomerTrafficResolver"/>: resolves the day baseline from <c>DayConfig</c>
    /// (days.json), then either returns it as an exact hard override or runs the contributor pipeline and
    /// clamps. Percent-only this iteration: <c>raw = baseline * (1 + sum PercentDelta)</c>. Deterministic;
    /// works with an empty contributor list. See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficResolver : ICustomerTrafficResolver
    {
        private const string LogTag = "[Sales.Traffic]";

        private readonly SalesTrafficSettings _settings;
        private readonly IConfigsService _configs;
        private readonly IReadOnlyList<ICustomerTrafficContributor> _contributors;

        public CustomerTrafficResolver(
            SalesTrafficSettings settings,
            IConfigsService configs,
            IReadOnlyList<ICustomerTrafficContributor> contributors)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _contributors = contributors ?? Array.Empty<ICustomerTrafficContributor>();
        }

        public CustomerTrafficResult Resolve(SalesSessionSetup setup, SalesTuning tuning)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            var day = FindByDayIndex(setup.Day);
            var baseline = day?.CustomerCount ?? _settings.DefaultCustomerCount;
            var applyModifiers = day?.ApplyModifiers ?? true;
            var baselineSource = day?.CustomerCount.HasValue == true ? "dayOverride" : "default";

            // Hard override: exact count; no modifiers, no clamp, and the spawner skips the request floor.
            if (!applyModifiers)
            {
                LogResolved(
                    setup,
                    baselineSource,
                    baseline,
                    applyModifiers: false,
                    hardOverride: true,
                    percentDelta: 0f,
                    raw: baseline,
                    rounded: baseline,
                    final: baseline,
                    contributors: 0);
                return new CustomerTrafficResult(baseline, baseline, isHardOverride: true, breakdown: null);
            }

            var context = new CustomerTrafficContext(setup.Day, setup.LocationId, setup.DecorIds);
            var accumulator = new CustomerTrafficAccumulator();
            for (var i = 0; i < _contributors.Count; i++)
                _contributors[i].Contribute(context, accumulator);

            var percentDelta = accumulator.TotalPercentDelta();
            var raw = baseline * (1f + percentDelta);
            var rounded = Round(raw);
            var final = Mathf.Clamp(rounded, _settings.MinCustomerCount, _settings.MaxCustomerCount);

            LogContributions(setup.Day, accumulator.Contributions);
            LogResolved(
                setup,
                baselineSource,
                baseline,
                applyModifiers: true,
                hardOverride: false,
                percentDelta: percentDelta,
                raw: raw,
                rounded: rounded,
                final: final,
                contributors: accumulator.Contributions.Count);

            return new CustomerTrafficResult(final, baseline, isHardOverride: false, accumulator.Contributions);
        }

        private DayConfig FindByDayIndex(int dayIndex)
        {
            foreach (var day in _configs.GetAll<DayConfig>())
                if (day.DayIndex == dayIndex)
                    return day;
            return null;
        }

        private int Round(float raw)
        {
            switch (_settings.Rounding)
            {
                case TrafficRounding.Floor: return Mathf.FloorToInt(raw);
                case TrafficRounding.Ceil: return Mathf.CeilToInt(raw);
                default: return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            }
        }

        private void LogContributions(int day, IReadOnlyList<CustomerTrafficContribution> contributions)
        {
            if (contributions == null) return;

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                Debug.Log($"{LogTag} contribution day={day} " +
                          $"source={LogValue(contribution.Source, "unknown")} " +
                          $"id={LogValue(contribution.Id, "unknown")} " +
                          $"percentDelta={Format(contribution.PercentDelta)} " +
                          "multiplier=1.00 flatDelta=0 " +
                          $"reason={LogValue(contribution.Reason, "none")}");
            }
        }

        private void LogResolved(
            SalesSessionSetup setup,
            string baselineSource,
            int baseline,
            bool applyModifiers,
            bool hardOverride,
            float percentDelta,
            float raw,
            int rounded,
            int final,
            int contributors)
        {
            Debug.Log($"{LogTag} resolved day={setup.Day} " +
                      $"location={LogValue(setup.LocationId, "none")} " +
                      $"baselineSource={baselineSource} " +
                      $"baseline={baseline} " +
                      $"applyModifiers={Bool(applyModifiers)} " +
                      $"hardOverride={Bool(hardOverride)} " +
                      $"percentDelta={Format(percentDelta)} " +
                      "multiplier=1.00 flatDelta=0 " +
                      $"raw={Format(raw)} " +
                      $"rounded={rounded} " +
                      $"min={_settings.MinCustomerCount} " +
                      $"max={_settings.MaxCustomerCount} " +
                      $"final={final} " +
                      $"contributors={contributors}");
        }

        private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Bool(bool value) => value ? "true" : "false";

        private static string LogValue(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
