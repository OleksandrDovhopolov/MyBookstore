using UnityEngine;

namespace Book.Sell.Services
{
    public sealed class UnityRandomSalesRandom : ISalesRandom
    {
        public int Range(int minInclusive, int maxExclusive)
            => Random.Range(minInclusive, maxExclusive);

        public double NextDouble()
            => Random.value;  // [0, 1)
    }
}
