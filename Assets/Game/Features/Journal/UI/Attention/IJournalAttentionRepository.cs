using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Journal.UI
{
    public interface IJournalAttentionRepository
    {
        UniTask<SavedJournalAttention> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(SavedJournalAttention state, CancellationToken ct);
    }
}
