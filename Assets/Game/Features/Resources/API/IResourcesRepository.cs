using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Resources.API
{
    /// <summary>
    /// Persistence seam under <see cref="IResourcesService"/>. The MVP implementation talks to
    /// <c>ISaveService</c>; a future HTTP-backed implementation can be swapped in without changes
    /// to the service or its consumers.
    /// </summary>
    public interface IResourcesRepository
    {
        UniTask<ResourcesStateDto> LoadAsync(CancellationToken ct);
        UniTask SaveAsync(ResourcesStateDto state, CancellationToken ct);
    }
}
