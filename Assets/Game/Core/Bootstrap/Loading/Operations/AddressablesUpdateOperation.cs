using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infrastructure;

namespace Game.Bootstrap.Loading
{
    public sealed class AddressablesUpdateOperation : LoadingOperationBase
    {
        private readonly IAddressablesCatalogService _catalog;

        public AddressablesUpdateOperation(IAddressablesCatalogService catalog)
            : base(
                id: "addressables_update",
                description: "Checking game content updates",
                isCritical: false,
                weight: 0.3f,
                displayPriority: 95,
                retryPolicy: new LoadingRetryPolicy(2, TimeSpan.FromSeconds(1)),
                timeout: TimeSpan.FromSeconds(20))
        {
            _catalog = catalog;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _catalog.InitializeAndUpdateAsync(ct);
            ReportProgress(1f);
        }
    }
}
