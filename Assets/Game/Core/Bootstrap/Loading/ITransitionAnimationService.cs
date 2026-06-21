using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    // Хук анимации перехода между сценами (cover «закрыть экран» / reveal «открыть экран»).
    // Вызывается GameFlowService вокруг load/unload сцен.
    //
    // Текущая реализация — NoOpTransitionAnimationService (без анимации). Реальный fade/cover
    // делается своим UI-кодом (НЕ DOTween — проект отказался от DOTween) в отдельной задаче;
    // такой impl живёт в Game.UI и реализует этот интерфейс. Research-референс cover/reveal —
    // docs/INPROGRESS/TRANSITION_ANIMATION_SERVICE.md.
    public interface ITransitionAnimationService
    {
        // Закрывает экран перед сменой сцен.
        UniTask PlayCoverAsync(CancellationToken ct);

        // Открывает экран после того, как целевая сцена готова.
        UniTask PlayRevealAsync(CancellationToken ct);
    }
}
