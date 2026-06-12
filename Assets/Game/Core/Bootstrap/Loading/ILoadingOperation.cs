using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    public interface ILoadingOperation
    {
        string Id { get; }
        string Description { get; }
        LoadingOperationStatus Status { get; }
        float Progress { get; }
        float Weight { get; }
        bool IsCritical { get; }
        int DisplayPriority { get; }
        LoadingRetryPolicy RetryPolicy { get; }
        TimeSpan? Timeout { get; }

        UniTask ExecuteAsync(CancellationToken ct);
        void Reset();
    }
}
