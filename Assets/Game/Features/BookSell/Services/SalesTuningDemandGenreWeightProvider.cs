using System;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public sealed class SalesTuningDemandGenreWeightProvider : IDemandGenreWeightProvider
    {
        private readonly SalesTuning _tuning;

        public SalesTuningDemandGenreWeightProvider(SalesTuning tuning)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public double GetWeight(string genre, LocationConfig location)
        {
            if (string.IsNullOrEmpty(genre) || location?.DemandGenres == null)
                return 1d;

            for (var i = 0; i < location.DemandGenres.Length; i++)
            {
                if (string.Equals(location.DemandGenres[i], genre, StringComparison.OrdinalIgnoreCase))
                    return Normalize(_tuning.PassiveDemandGenreWeight);
            }

            return 1d;
        }

        private static double Normalize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 1d)
                return 1d;

            return value;
        }
    }
}
