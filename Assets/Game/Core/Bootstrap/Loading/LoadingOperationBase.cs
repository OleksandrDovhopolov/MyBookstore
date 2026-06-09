using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Bootstrap.Loading
{
    public abstract class LoadingOperationBase : ILoadingOperation
    {
        protected LoadingOperationBase(
            string id,
            string description,
            bool isCritical,
            float weight,
            int displayPriority,
            LoadingRetryPolicy retryPolicy,
            TimeSpan? timeout = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Operation id is required.", nameof(id)) : id;
            Description = string.IsNullOrWhiteSpace(description) ? id : description;
            IsCritical = isCritical;
            Weight = weight <= 0f ? 1f : weight;
            DisplayPriority = displayPriority;
            RetryPolicy = retryPolicy;
            Timeout = timeout;
            Status = LoadingOperationStatus.NotStarted;
        }

        public string Id { get; }
        public string Description { get; }
        public LoadingOperationStatus Status { get; private set; }
        public float Progress { get; private set; }
        public float Weight { get; }
        public bool IsCritical { get; }
        public int DisplayPriority { get; }
        public LoadingRetryPolicy RetryPolicy { get; }
        public TimeSpan? Timeout { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Status = LoadingOperationStatus.InProgress;
            Progress = 0f;

            try
            {
                await ExecuteInternalAsync(ct);
                Progress = 1f;
                Status = LoadingOperationStatus.Completed;
            }
            catch
            {
                Status = LoadingOperationStatus.Failed;
                throw;
            }
        }

        public virtual void Reset()
        {
            Progress = 0f;
            Status = LoadingOperationStatus.NotStarted;
        }

        protected void ReportProgress(float progress)
        {
            Progress = Math.Clamp(progress, 0f, 1f);
        }

        protected abstract UniTask ExecuteInternalAsync(CancellationToken ct);
    }
}
