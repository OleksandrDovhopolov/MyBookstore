namespace Book.Sell.Services
{
    /// <summary>
    /// Randomness port for the Sales feature. UnityEngine.Random in prod, a deterministic
    /// queue-backed fake in tests. The port keeps tests reproducible and isolates Sales from
    /// the global Random state.
    /// </summary>
    public interface ISalesRandom
    {
        /// <summary>Returns an int in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }
}
