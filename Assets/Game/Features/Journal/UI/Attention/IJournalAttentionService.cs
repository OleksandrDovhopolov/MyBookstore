using System;

namespace Game.Journal.UI
{
    public interface IJournalAttentionService
    {
        bool HasAnyUnseen { get; }
        bool HasUnseen(JournalAttentionCategory category);
        void MarkSeen(JournalAttentionCategory category);
        event Action Changed;
    }
}
