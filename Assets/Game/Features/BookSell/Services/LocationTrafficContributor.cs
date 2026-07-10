using System;
using System.Globalization;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Traffic contributor for the active location: emits <c>LocationConfig.CustomerTrafficPercentDelta</c>
    /// (locations.json). Neutral (0) locations contribute nothing. Lives in Book.Sell because it reads a
    /// LocationConfig via IConfigsService. See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class LocationTrafficContributor : ICustomerTrafficContributor
    {
        private readonly IConfigsService _configs;

        public LocationTrafficContributor(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Contribute(CustomerTrafficContext context, CustomerTrafficAccumulator accumulator)
        {
            if (context == null || accumulator == null) return;
            if (string.IsNullOrEmpty(context.LocationId)) return;
            if (!_configs.TryGet<LocationConfig>(context.LocationId, out var location)) return;

            var delta = location.CustomerTrafficPercentDelta;
            if (delta == 0f) return;

            accumulator.Add(delta, $"location {context.LocationId} {delta.ToString("+0.##;-0.##", CultureInfo.InvariantCulture)}");
        }
    }
}
