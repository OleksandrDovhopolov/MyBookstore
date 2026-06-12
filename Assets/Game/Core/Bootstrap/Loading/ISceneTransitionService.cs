using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    // Generic-сервис переходов между сценами. Не привязан к bootstrap-флоу —
    // может вызываться из любого места (gameplay → preparation, preparation → gameplay и т.п.).
    public interface ISceneTransitionService
    {
        UniTask TransitionToAsync(string sceneName, IProgress<float> progress, CancellationToken ct);
    }
}
