using System.Collections.Generic;

namespace Book.Sell.Services
{
    /// <summary>
    /// Per-genre multiplier applied to the passive sale chance, sourced from active decor.
    /// Neutral value is 1.0 (no effect). Real decor lands in a follow-up feature; the stub
    /// returns 1.0 unconditionally so callers and the formula are wired ahead of time.
    /// </summary>
    public interface IDecorModifierProvider
    {
        /// <summary>
        /// Returns the multiplier for <paramref name="genre"/> given the player's currently
        /// active decor (ids carried by <c>SalesSessionSetup.DecorIds</c>).
        /// </summary>
        float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds);
    }
}
