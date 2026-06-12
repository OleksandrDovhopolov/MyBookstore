using System;
using System.Collections.Generic;
using Book.Sell.Services;
using Game.Configs.Models;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>
    /// Returns a constant chance, or a chance computed from (genre, count) when a delegate is supplied.
    /// Use 1.0 for "always hit" and 0.0 for "always miss" in step/controller tests that care about the
    /// surrounding flow, not the formula itself.
    /// </summary>
    public sealed class FakeBaseSaleChanceCalculator : IBaseSaleChanceCalculator
    {
        private readonly Func<string, int, double> _func;

        public FakeBaseSaleChanceCalculator(double constant)
            => _func = (_, __) => constant;

        public FakeBaseSaleChanceCalculator(Func<string, int, double> func)
            => _func = func ?? throw new ArgumentNullException(nameof(func));

        public double Compute(string genre, int count, LocationConfig location, IReadOnlyList<string> activeDecorIds)
            => _func(genre, count);
    }
}
