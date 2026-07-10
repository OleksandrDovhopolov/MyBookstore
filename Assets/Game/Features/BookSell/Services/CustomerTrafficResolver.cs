using System;
using System.Collections.Generic;
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
    /// clamps. Percent-only this iteration: <c>raw = baseline * (1 + Σ PercentDelta)</c>. Deterministic;
    /// works with an empty contributor list. See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class CustomerTrafficResolver : ICustomerTrafficResolver
    {
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

            // Hard override: exact count — no modifiers, no clamp, and the spawner skips the request floor.
            if (!applyModifiers)
                return new CustomerTrafficResult(baseline, baseline, isHardOverride: true, breakdown: null);

            var context = new CustomerTrafficContext(setup.Day, setup.LocationId, setup.DecorIds);
            var accumulator = new CustomerTrafficAccumulator();
            for (var i = 0; i < _contributors.Count; i++)
                _contributors[i].Contribute(context, accumulator);

            var raw = baseline * (1f + accumulator.TotalPercentDelta());
            var final = Mathf.Clamp(Round(raw), _settings.MinCustomerCount, _settings.MaxCustomerCount);
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
    }
}
