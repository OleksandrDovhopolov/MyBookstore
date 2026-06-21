using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    // Заглушка: переход без анимации. Заменяется реальным fade/cover-сервисом в отдельной задаче.
    public sealed class NoOpTransitionAnimationService : ITransitionAnimationService
    {
        public UniTask PlayCoverAsync(CancellationToken ct) => UniTask.CompletedTask;

        public UniTask PlayRevealAsync(CancellationToken ct) => UniTask.CompletedTask;
    }
}
