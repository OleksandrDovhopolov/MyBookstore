using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Progression.API
{
    /// <summary>
    /// Persistence seam under <see cref="IProgressionService"/>. MVP is local; a future server
    /// implementation swaps in via DI.
    /// </summary>
    public interface IProgressionRepository
    {
        UniTask<ProgressionStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(ProgressionStateDto state, CancellationToken ct);
    }
}
