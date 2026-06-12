using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    public sealed class WarmupOperation : LoadingOperationBase
    {
        public WarmupOperation()
            : base(
                id: "warmup",
                description: "Warming up game resources",
                isCritical: false,
                weight: 0.1f,
                displayPriority: 70,
                retryPolicy: new LoadingRetryPolicy(1, TimeSpan.Zero),
                timeout: TimeSpan.FromSeconds(5))
        {
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.2f);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            ReportProgress(1f);
        }
    }
}
