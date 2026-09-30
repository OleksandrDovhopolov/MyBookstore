using System.Collections.Generic;

namespace Game.Attention
{
    /// <summary>
    /// Persisted attention state (save module <see cref="AttentionSaveKeys.State"/>): per key, the
    /// set of ids the player has already seen. Unseen is derived as "current ids minus this set",
    /// so nothing here needs to be reconciled when content changes.
    /// </summary>
    public sealed class SavedAttention
    {
        public Dictionary<string, HashSet<string>> SeenIdsByKey { get; set; } = new();
    }
}
