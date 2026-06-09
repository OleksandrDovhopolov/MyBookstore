using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;

namespace Game.Bootstrap.Loading
{
    public sealed class ConfigsWarmupOperation : LoadingOperationBase
    {
        private readonly IConfigsService _configs;

        public ConfigsWarmupOperation(IConfigsService configs)
            : base(
                id: "configs_warmup",
                description: "Loading game configuration",
                isCritical: true,
                weight: 0.2f,
                displayPriority: 92,
                retryPolicy: new LoadingRetryPolicy(2, TimeSpan.FromSeconds(0.5)),
                timeout: TimeSpan.FromSeconds(15))
        {
            _configs = configs;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _configs.WarmupAsync(ct);
            ReportProgress(1f);
        }
    }
}
