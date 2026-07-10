using System.Threading;
using Cysharp.Threading.Tasks;

namespace Infrastructure.ResourceAnimations
{
    public interface IResourceAnimationService
    {
        UniTask PlayAsync(ResourceAnimationRequest request, CancellationToken ct = default);
    }
}
