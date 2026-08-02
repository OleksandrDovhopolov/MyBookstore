using System;
using Book.Sell.Services;

namespace Book.Sell.Tests.Editor.Fakes
{
    public sealed class SeededSalesRandom : ISalesRandom
    {
        private readonly Random _random;

        public SeededSalesRandom(int seed)
        {
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
            => minInclusive >= maxExclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
