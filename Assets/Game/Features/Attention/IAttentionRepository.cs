using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Attention
{
    public interface IAttentionRepository
    {
        UniTask<SavedAttention> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(SavedAttention state, CancellationToken ct);
    }
}
