using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.LocationUnlock.API
{
    /// <summary>Persistence seam for the set of unlocked (purchased) location ids. Mirrors the other repositories.</summary>
    public interface ILocationUnlockRepository
    {
        UniTask<LocationUnlockStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(LocationUnlockStateDto state, CancellationToken ct);
    }
}
