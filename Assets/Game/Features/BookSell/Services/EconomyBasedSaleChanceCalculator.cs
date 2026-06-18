using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Default calculator. Formula:
    /// <c>clamp(min(baseChance + perCopy * count, capChance) * locMod * decorMod, 0, 1)</c>,
    /// where <c>locMod = LocationDemandMultiplier</c> iff genre is listed in
    /// <see cref="LocationConfig.DemandGenres"/>, else 1.0; <c>decorMod</c> comes from
    /// <see cref="IDecorModifierProvider"/>.
    /// </summary>
    public sealed class EconomyBasedSaleChanceCalculator : IBaseSaleChanceCalculator
    {
        private const string LogPrefix = "[Sales.Chance]";

        private readonly IConfigsService _configs;
        private readonly IDecorModifierProvider _decor;

        private EconomyConfig _economy;
        private bool _economyMissingLogged;

        public EconomyBasedSaleChanceCalculator(IConfigsService configs, IDecorModifierProvider decor)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _decor = decor ?? throw new ArgumentNullException(nameof(decor));
        }

        public double Compute(string genre, int count, LocationConfig location, IReadOnlyList<string> activeDecorIds)
        {
            if (count <= 0 || string.IsNullOrEmpty(genre)) return 0d;

            var economy = ResolveEconomy();
            if (economy == null) return 0d;

            var fCount = Math.Min(economy.BaseSaleChance + economy.PerCopyChance * count, economy.CapChance);
            var locMod = LocationDemandMultiplier(genre, location, economy);
            var decorMod = _decor.GetGenreMultiplier(genre, activeDecorIds);

            var raw = fCount * locMod * decorMod;
            if (double.IsNaN(raw) || double.IsInfinity(raw)) return 0d;
            return Math.Clamp(raw, 0d, 1d);
        }

        private EconomyConfig ResolveEconomy()
        {
            if (_economy != null) return _economy;

            _economy = _configs.Get<EconomyConfig>(EconomyConfig.SingletonId);
            if (_economy == null && !_economyMissingLogged)
            {
                Debug.LogError($"{LogPrefix} EconomyConfig '{EconomyConfig.SingletonId}' is missing — passive sale chance falls back to 0.");
                _economyMissingLogged = true;
            }
            return _economy;
        }

        private static double LocationDemandMultiplier(string genre, LocationConfig location, EconomyConfig economy)
        {
            if (location?.DemandGenres == null || location.DemandGenres.Length == 0) return 1d;
            for (var i = 0; i < location.DemandGenres.Length; i++)
            {
                if (string.Equals(location.DemandGenres[i], genre, StringComparison.OrdinalIgnoreCase))
                    return economy.LocationDemandMultiplier;
            }
            return 1d;
        }
    }
}
