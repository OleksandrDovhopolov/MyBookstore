using System.Collections.Generic;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Computes the gate chance for a passive sale of a given genre on the current shelf.
    /// Output is in [0, 1] and feeds the per-genre stage-1 roll in <see cref="IPassiveSaleSelector"/>.
    /// Returns 0 when <paramref name="count"/> is 0.
    /// </summary>
    public interface IBaseSaleChanceCalculator
    {
        double Compute(string genre, int count, LocationConfig location, IReadOnlyList<string> activeDecorIds);
    }
}
