using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;

namespace Game.Ftue.Services
{
    /// <summary>
    /// ILoadingOperation wrapper around <see cref="IFtueBootstrapper"/>.
    /// Mirrors <c>SaveDataLoadOperation</c>/<c>ConfigsWarmupOperation</c>: lets the FTUE step
    /// participate in the Bootstrap loading pipeline as its own phase between data_load and finalization.
    /// </summary>
    public sealed class FtueBootstrapOperation : LoadingOperationBase
    {
        private readonly IFtueBootstrapper _bootstrapper;

        public FtueBootstrapOperation(IFtueBootstrapper bootstrapper)
            : base(
                id: "ftue_bootstrap",
                description: "Preparing starter inventory",
                isCritical: true,
                weight: 0.1f,
                displayPriority: 80,
                retryPolicy: new LoadingRetryPolicy(2, TimeSpan.FromSeconds(0.3)),
                timeout: TimeSpan.FromSeconds(10))
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _bootstrapper.RunAsync(ct);
            ReportProgress(1f);
        }
    }
}
