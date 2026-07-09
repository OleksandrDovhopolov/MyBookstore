using System.Threading;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Services
{
    /// <summary>
    /// Fire-once memory for scripted dialogues (GAME-6): records which dialogue ids the player has already
    /// been shown, so a quest character carrying an intro dialogue is spawned only once even while its quest
    /// stays Active. Keyed by <c>dialogueId</c> (dialogue content), not by quest — the norm is a 1:1
    /// dialogue↔quest link. Save-backed so it survives across days/sessions.
    /// </summary>
    public interface IDeliveredDialoguesService
    {
        /// <summary>True if this dialogue has already been delivered (shown) to the player.</summary>
        bool IsDelivered(string dialogueId);

        /// <summary>Marks the dialogue delivered and persists. Idempotent — a no-op (no save churn) if already set.</summary>
        UniTask MarkDeliveredAsync(string dialogueId, CancellationToken ct);
    }
}
