using System;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.LocationEntry.API;
using Game.Resources.API;
using UnityEngine;

namespace Game.LocationEntry.Services
{
    /// <summary>
    /// Default <see cref="ILocationEntryCostCalculator"/>. Pure read: per-visit fee =
    /// <c>LocationConfig.EntryCost</c> plus the signed <c>DecorConfig.VisitCostDelta</c> of every active
    /// decor (<see cref="IDecorPlacementService.GetActiveDecorIds"/>), clamped to ≥ 0. Currency comes from
    /// <c>LocationConfig.EntryCurrencyId</c> (defaults to gold). See docs/SAVE_DAY_FLOW.md.
    /// </summary>
    public sealed class LocationEntryCostCalculator : ILocationEntryCostCalculator
    {
        private readonly IConfigsService _configs;
        private readonly IDecorPlacementService _decor;

        public LocationEntryCostCalculator(IConfigsService configs, IDecorPlacementService decor)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _decor = decor;   // optional: no decor feature wired → no deltas
        }

        public LocationEntryCost Calculate(string locationId)
        {
            var config = string.IsNullOrEmpty(locationId) ? null : _configs.Get<LocationConfig>(locationId);
            if (config == null)
                return new LocationEntryCost(ResourceIds.Gold, 0, 0, 0);

            var baseCost = config?.EntryCost ?? 0;
            var currency = string.IsNullOrEmpty(config?.EntryCurrencyId) ? ResourceIds.Gold : config.EntryCurrencyId;
            var decorDelta = SumActiveDecorDelta();

            var total = Mathf.Max(0, baseCost + decorDelta);
            return new LocationEntryCost(currency, total, baseCost, decorDelta);
        }

        private int SumActiveDecorDelta()
        {
            var active = _decor?.GetActiveDecorIds();
            if (active == null) return 0;

            var sum = 0;
            for (var i = 0; i < active.Count; i++)
            {
                var decorConfig = _configs.Get<DecorConfig>(active[i]);
                if (decorConfig != null) sum += decorConfig.VisitCostDelta;
            }
            return sum;
        }
    }
}
