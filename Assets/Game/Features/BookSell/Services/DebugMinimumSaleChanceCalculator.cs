using System;
using System.Collections.Generic;
using Book.Sell.API;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Temporary debug calculator: keeps the regular economy/location/decor formula, but floors any
    /// valid passive sale chance at 50% so sales-heavy flows are easier to test.
    /// </summary>
    public sealed class DebugMinimumSaleChanceCalculator : IBaseSaleChanceCalculator
    {
        private const double MinChance = 0.5d;

        private readonly EconomyBasedSaleChanceCalculator _inner;

        public DebugMinimumSaleChanceCalculator(IConfigsService configs, IDecorModifierProvider decor)
        {
            _inner = new EconomyBasedSaleChanceCalculator(configs, decor);
        }

        public double Compute(string genre, int count, LocationConfig location, IReadOnlyList<string> activeDecorIds)
        {
            if (count <= 0 || string.IsNullOrEmpty(genre)) return 0d;

            var chance = _inner.Compute(genre, count, location, activeDecorIds);
            return Math.Clamp(Math.Max(chance, MinChance), 0d, 1d);
        }
    }
}
