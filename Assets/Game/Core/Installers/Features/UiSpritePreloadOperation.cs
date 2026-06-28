using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Newspaper.UI;

namespace Game.Bootstrap
{
    /// <summary>
    /// ILoadingOperation wrapper around <see cref="IUiSpriteProvider"/>. Preloads newspaper/rewards
    /// UI sprites during bootstrap so the first window open is instant. Non-critical: a failed preload
    /// just leaves icons null (windows tolerate null sprites) instead of blocking the game start.
    /// Mirrors <see cref="FtueBootstrapOperation"/>.
    /// </summary>
    public sealed class UiSpritePreloadOperation : LoadingOperationBase
    {
        private readonly IUiSpriteProvider _provider;

        public UiSpritePreloadOperation(IUiSpriteProvider provider)
            : base(
                id: "ui_sprite_preload",
                description: "Preloading UI sprites",
                isCritical: false,
                weight: 0.1f,
                displayPriority: 70,
                retryPolicy: new LoadingRetryPolicy(1, TimeSpan.FromSeconds(0.3)),
                timeout: TimeSpan.FromSeconds(10))
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);
            await _provider.PreloadAsync(ct);
            ReportProgress(1f);
        }
    }
}
