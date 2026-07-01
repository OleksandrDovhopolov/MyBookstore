using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    // Хук анимации перехода между сценами (cover «закрыть экран» / reveal «открыть экран»).
    // Вызывается GameFlowService вокруг load/unload сцен.
    //
    // Runtime registration uses DeferredTransitionAnimationService as a router to the MonoBehaviour
    // assigned on UIManagerCanvas. NoOpTransitionAnimationService remains only as a defensive fallback
    // inside the router when the prefab reference is missing.
    public interface ITransitionAnimationService
    {
        // Закрывает экран перед сменой сцен.
        UniTask PlayCoverAsync(CancellationToken ct);

        // Открывает экран после того, как целевая сцена готова.
        UniTask PlayRevealAsync(CancellationToken ct);
    }
}
