using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Game.Decor.Services
{
    public sealed class DecorTotalEffectsProvider : IDecorTotalEffectsProvider
    {
        private readonly IConfigsService _configs;
        private readonly IDecorModifierProvider _modifiers;

        public DecorTotalEffectsProvider(IConfigsService configs, IDecorModifierProvider modifiers)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        }

        public IReadOnlyList<DecorTotalEffect> GetTotalEffects(IReadOnlyList<string> activeDecorIds)
        {
            if (activeDecorIds == null || activeDecorIds.Count == 0)
                return Array.Empty<DecorTotalEffect>();

            var result = new List<DecorTotalEffect>();
            foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
            {
                var genreKey = genre.ToConfigValue();
                var multiplier = _modifiers.GetGenreMultiplier(genreKey, activeDecorIds);
                if (Mathf.Approximately(multiplier, 1f)) continue;

                result.Add(new DecorTotalEffect(
                    DecorEffectKind.GenreSaleChance,
                    genreKey,
                    (multiplier - 1f) * 100f));
            }

            var traffic = ResolveTrafficPercent(activeDecorIds);
            if (!Mathf.Approximately(traffic, 0f))
                result.Add(new DecorTotalEffect(DecorEffectKind.CustomerTraffic, null, traffic * 100f));

            return result;
        }

        private float ResolveTrafficPercent(IReadOnlyList<string> activeDecorIds)
        {
            var total = 0f;
            for (var i = 0; i < activeDecorIds.Count; i++)
            {
                var decorId = activeDecorIds[i];
                if (string.IsNullOrEmpty(decorId)) continue;
                if (!_configs.TryGet<DecorConfig>(decorId, out var config)) continue;
                total += config.CustomerTrafficPercentDelta;
            }

            return total;
        }
    }
}
