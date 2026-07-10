using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LocationVisits.API;

namespace Game.LocationVisits.Services
{
    /// <summary>Persistence seam for <see cref="LocationVisitService"/> (mirrors the SalesStats repo).</summary>
    public interface ILocationVisitsRepository
    {
        UniTask<LocationVisitsStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(LocationVisitsStateDto state, CancellationToken ct);
    }
}
