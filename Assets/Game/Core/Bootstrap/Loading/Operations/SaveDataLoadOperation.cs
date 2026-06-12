using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Save;

namespace Game.Bootstrap.Loading
{
    public sealed class SaveDataLoadOperation : LoadingOperationBase
    {
        private readonly ISaveService _saveService;

        public SaveDataLoadOperation(ISaveService saveService)
            : base(
                id: "save_data_load",
                description: "Loading save data",
                isCritical: true,
                weight: 0.3f,
                displayPriority: 90,
                retryPolicy: new LoadingRetryPolicy(2, TimeSpan.FromSeconds(0.3)),
                timeout: TimeSpan.FromSeconds(10))
        {
            _saveService = saveService;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _saveService.LoadAsync(ct);
            ReportProgress(1f);
        }
    }
}
