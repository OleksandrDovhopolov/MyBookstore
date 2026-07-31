using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Book.Sell.API
{
    /// <summary>
    /// Fire-once memory for scripted dialogues (GAME-6): records which dialogue ids the player has already
    /// been shown, so a quest character carrying an intro dialogue is spawned only once while its quest stays active.
    /// </summary>
    public interface IDeliveredDialoguesService
    {
        /// <summary>Raised when the visible delivered-dialogue set changes.</summary>
        event Action Changed;

        /// <summary>True if this dialogue has already been delivered (shown) to the player.</summary>
        bool IsDelivered(string dialogueId);

        /// <summary>Marks the dialogue delivered and persists immediately. Used for non-day-scoped dialogues.</summary>
        UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct);

        /// <summary>Marks the dialogue delivered in memory only. Flushed by the sales-day commit.</summary>
        UniTask MarkDeliveredDeferredAsync(string dialogueId, CancellationToken ct);

        /// <summary>Flushes all deferred dialogue ids into the committed save-backed set.</summary>
        UniTask CommitAsync(CancellationToken ct);

        /// <summary>Discards deferred dialogue ids that belong to an unfinished sales run.</summary>
        void DiscardDeferred();
    }
}
