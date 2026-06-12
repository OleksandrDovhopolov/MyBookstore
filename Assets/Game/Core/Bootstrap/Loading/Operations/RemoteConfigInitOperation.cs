using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Remote;

namespace Game.Bootstrap.Loading
{
    public sealed class RemoteConfigInitOperation : LoadingOperationBase
    {
        private readonly IRemoteConfigService _remoteConfig;

        public RemoteConfigInitOperation(IRemoteConfigService remoteConfig)
            : base(
                id: "remote_config_init",
                description: "Loading remote configuration",
                isCritical: false,
                weight: 0.2f,
                displayPriority: 85,
                retryPolicy: new LoadingRetryPolicy(2, TimeSpan.FromSeconds(0.5)),
                timeout: TimeSpan.FromSeconds(10))
        {
            _remoteConfig = remoteConfig;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            if (_remoteConfig != null)
            {
                await _remoteConfig.InitializeAsync(ct);
            }
            ReportProgress(1f);
        }
    }
}
