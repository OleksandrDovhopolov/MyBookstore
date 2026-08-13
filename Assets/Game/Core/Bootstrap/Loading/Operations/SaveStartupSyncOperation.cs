using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save.Sync;

namespace Game.Bootstrap.Loading
{
    public sealed class SaveStartupSyncOperation : LoadingOperationBase
    {
        private readonly SaveSyncBootstrap _sync;

        public SaveStartupSyncOperation(SaveSyncBootstrap sync)
            : base(
                id: "save_startup_sync",
                description: "Syncing save data",
                isCritical: true,
                weight: 0.15f,
                displayPriority: 89,
                retryPolicy: new LoadingRetryPolicy(1, TimeSpan.Zero),
                timeout: TimeSpan.FromSeconds(10))
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _sync.SyncOnStartupAsync(ct);
            ReportProgress(1f);
        }
    }
}
