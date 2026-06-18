using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Game.Decor.Services
{
    /// <summary>
    /// Real <see cref="IDecorModifierProvider"/> backed by DecorConfig data. Combines per-decor
    /// genre multipliers multiplicatively, then clamps the final value into [0.1, 3.0]
    /// (soft cap, see docs/INPROGRESS/Decor.md §5.2.5). Returns 1.0 when no decor matches the genre.
    /// </summary>
    public sealed class ConfigBasedDecorModifierProvider : IDecorModifierProvider
    {
        //TODO should be out of code. Need to move so GD can adjust it 
        public const float SoftCapMin = 0.1f;
        public const float SoftCapMax = 3.0f;

        private readonly IConfigsService _configs;

        public ConfigBasedDecorModifierProvider(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds)
        {
            if (string.IsNullOrEmpty(genre) || activeDecorIds == null || activeDecorIds.Count == 0)
                return 1f;

            var result = 1f;
            for (var i = 0; i < activeDecorIds.Count; i++)
            {
                var decorId = activeDecorIds[i];
                if (string.IsNullOrEmpty(decorId)) continue;

                var config = _configs.Get<DecorConfig>(decorId);
                if (config?.GenreMultipliers == null) continue;

                for (var j = 0; j < config.GenreMultipliers.Length; j++)
                {
                    var mod = config.GenreMultipliers[j];
                    if (mod == null || mod.Multiplier <= 0f) continue;
                    if (!string.Equals(mod.Genre, genre, StringComparison.OrdinalIgnoreCase)) continue;
                    result *= mod.Multiplier;
                    break;
                }
            }

            return Mathf.Clamp(result, SoftCapMin, SoftCapMax);
        }
    }
}
