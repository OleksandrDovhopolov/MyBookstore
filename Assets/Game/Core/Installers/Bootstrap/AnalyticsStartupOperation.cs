using System;
using System.Threading;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using UnityEngine;
using PlayerIdentityProvider = Save.Identity.IPlayerIdentityProvider;

namespace Game.Bootstrap
{
    public sealed class AnalyticsStartupOperation : LoadingOperationBase
    {
        private const string LogPrefix = "[AnalyticsStartup]";

        private readonly IAnalyticsService _analytics;
        private readonly PlayerIdentityProvider _identity;

        public AnalyticsStartupOperation(IAnalyticsService analytics, PlayerIdentityProvider identity)
            : base(
                id: "analytics_init",
                description: "Starting analytics",
                isCritical: false,
                weight: 0.1f,
                displayPriority: 86,
                retryPolicy: new LoadingRetryPolicy(0, TimeSpan.Zero),
                timeout: TimeSpan.FromSeconds(10))
        {
            _analytics = analytics;
            _identity = identity;
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            try
            {
                ReportProgress(0.1f);
                await EnsureFirebaseDependenciesAsync(ct);

                ReportProgress(0.5f);
                var playerId = _identity?.GetPlayerId();
                if (!string.IsNullOrWhiteSpace(playerId))
                {
                    _analytics?.SetUserId(playerId);
                }

                ReportProgress(0.7f);
                _analytics?.Initialize();

                ReportProgress(0.9f);
                if (_analytics?.IsInitialized == true)
                {
                    _analytics.TrackEvent(new AnalyticsEvent(AnalyticsEventNames.SessionStarted));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogError($"{LogPrefix} Failed: {exception}");
            }
            finally
            {
                ReportProgress(1f);
            }
        }

        private static async UniTask EnsureFirebaseDependenciesAsync(CancellationToken ct)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var loader = new RemoteConfigLoader();
            await loader.EnsureDependenciesAsync(ct);
#else
            await UniTask.CompletedTask;
#endif
        }
    }
}
