using System.Collections.Generic;

namespace Book.Sell.API
{
    /// <summary>
    /// Per-genre multiplier applied to the passive sale chance, sourced from active decor.
    /// Neutral value is 1.0 (no effect). Real implementation lives in Game.Decor — this contract
    /// stays in BookSell.API so that decor can plug in without depending on BookSell impl assembly.
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
