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
        /// <summary>True if this dialogue has already been delivered (shown) to the player.</summary>
        bool IsDelivered(string dialogueId);

        /// <summary>Marks the dialogue delivered and persists. Idempotent - a no-op (no save churn) if already set.</summary>
        UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct);

        /// <summary>Clears a delivered flag and persists. Idempotent - a no-op (no save churn) if absent.</summary>
        UniTask ClearAsync(string dialogueId, CancellationToken ct);
    }
}
