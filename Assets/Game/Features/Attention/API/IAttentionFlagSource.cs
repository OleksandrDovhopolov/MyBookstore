using System;

namespace Game.Attention.API
{
    /// <summary>
    /// A key whose seen-state is owned by someone else (e.g. Journal memories, persisted in the
    /// <c>characters</c> save module as <c>SavedCharacters.SeenMemoryIds</c>). The attention service
    /// surfaces it alongside id-based sources but never persists it and never marks itself dirty for
    /// it — the owner handles storage.
    /// </summary>
    public interface IAttentionFlagSource
    {
        string Key { get; }

        bool HasUnseen { get; }

        void MarkSeen();

        event Action Changed;
    }
}
