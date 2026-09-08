using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Localization;

namespace Game.Bootstrap.Loading
{
    public sealed class LocalizationWarmupOperation : LoadingOperationBase
    {
        private readonly ILocalizationService _localization;

        public LocalizationWarmupOperation(ILocalizationService localization)
            : base(
                id: "localization_warmup",
                description: "Loading localization",
                isCritical: true,
                weight: 0.1f,
                displayPriority: 91,
                retryPolicy: new LoadingRetryPolicy(2, System.TimeSpan.FromSeconds(0.5)),
                timeout: System.TimeSpan.FromSeconds(10))
        {
            _localization = localization;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _localization.WarmupAsync(ct);
            ReportProgress(1f);
        }
    }
}
