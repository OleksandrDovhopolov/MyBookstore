namespace Game.Rewards.Services
{
    /// <summary>
    /// Randomness port for Rewards. Lets <see cref="BookBoxRewardExpander"/> stay deterministic in
    /// tests without referencing BookSell's <c>ISalesRandom</c> implementation. Bootstrap wires an
    /// adapter that delegates to the existing project-wide RNG so the whole game shares one source.
    /// </summary>
    public interface IRewardRandom
    {
        /// <summary>Returns an int in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }
}
