using System;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;

namespace Game.Decor.Services
{
    /// <summary>
    /// Traffic contributor for placed decor: sums <c>DecorConfig.CustomerTrafficPercentDelta</c> over the
    /// decor ids the player currently has placed (carried in <see cref="CustomerTrafficContext.DecorIds"/>,
    /// sourced from <see cref="IDecorPlacementService.GetActiveDecorIds"/>). Distinct from
    /// <see cref="ConfigBasedDecorModifierProvider"/>, which handles per-genre sale chance.
    /// See docs/INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md.
    /// </summary>
    public sealed class DecorTrafficContributor : ICustomerTrafficContributor
    {
        private readonly IConfigsService _configs;

        public DecorTrafficContributor(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Contribute(CustomerTrafficContext context, CustomerTrafficAccumulator accumulator)
        {
            if (context == null || accumulator == null) return;
            var decorIds = context.DecorIds;
            if (decorIds == null || decorIds.Count == 0) return;

            var total = 0f;
            for (var i = 0; i < decorIds.Count; i++)
            {
                var decorId = decorIds[i];
                if (string.IsNullOrEmpty(decorId)) continue;
                if (!_configs.TryGet<DecorConfig>(decorId, out var config)) continue;
                total += config.CustomerTrafficPercentDelta;
            }

            if (total == 0f) return;
            accumulator.Add(total, "decor", "placed", $"decor.placed.{decorIds.Count}");
        }
    }
}
