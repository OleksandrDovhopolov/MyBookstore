using System.Collections.Generic;

namespace Book.Sell.Services
{
    /// <summary>
    /// Stub implementation: returns neutral multiplier (1.0) regardless of inputs.
    /// Replaced once a real decor feature exists.
    /// </summary>
    public sealed class NoopDecorModifierProvider : IDecorModifierProvider
    {
        public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds) => 1f;
    }
}
