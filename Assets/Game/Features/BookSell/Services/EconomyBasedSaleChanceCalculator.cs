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

        // TEST ONLY: flat 50% sale gate per genre to exercise the passive purchase flow at runtime.
        // Flip to false (or delete this field + the block in Compute) to restore the real
        // economy-based chance. static readonly (not const) so the real formula below stays reachable.
        private static readonly bool UseTestFlatChance = true;
        private const double TestFlatChance = 0.5d;

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

            // TEST ONLY — see UseTestFlatChance. Forces every genre group to a flat 50% gate.
            if (UseTestFlatChance)
            {
                Debug.Log($"{LogPrefix} genre={genre} copies={count} → chance={TestFlatChance:F3} (TEST flat 50%, economy ignored)");
                return TestFlatChance;
            }

            var economy = ResolveEconomy();
            if (economy == null) return 0d;

            var fCount = Math.Min(economy.BaseSaleChance + economy.PerCopyChance * count, economy.CapChance);
            var locMod = LocationDemandMultiplier(genre, location, economy);
            var decorMod = _decor.GetGenreMultiplier(genre, activeDecorIds);

            var raw = fCount * locMod * decorMod;
            var chance = (double.IsNaN(raw) || double.IsInfinity(raw)) ? 0d : Math.Clamp(raw, 0d, 1d);

            Debug.Log($"{LogPrefix} genre={genre} copies={count} f={fCount:F3}(base {economy.BaseSaleChance:F2}+{economy.PerCopyChance:F2}/copy, cap {economy.CapChance:F2}) locMod={locMod:F2} decorMod={decorMod:F2} → chance={chance:F3}");
            return chance;
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
