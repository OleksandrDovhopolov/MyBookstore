using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Remote;
using Infrastructure;
using Save;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap.Loading
{
    // Заменяет три отдельных IAsyncStartable (AddressablesWarmup / ConfigsWarmup / BookDuneProbe).
    // Строит фазы загрузки, запускает оркестратор, ведёт LoadingScreenView, разруливает retry.
    public sealed class LoadingOrchestratorEntryPoint : IAsyncStartable
    {
        private const string LogPrefix = "[LoadingOrchestrator]";
        private const string ErrorMessage = "Check your internet connection and try again.";
        private static readonly TimeSpan GlobalTimeout = TimeSpan.FromSeconds(60);

        private readonly LoadingOrchestrator _orchestrator;
        private readonly LoadingScreenView _view;
        private readonly IAddressablesCatalogService _catalog;
        private readonly IRemoteConfigService _remoteConfig;
        private readonly IConfigsService _configs;
        private readonly ISaveService _save;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly LoadingSettings _settings;

        public LoadingOrchestratorEntryPoint(
            LoadingOrchestrator orchestrator,
            LoadingScreenView view,
            IAddressablesCatalogService catalog,
            IRemoteConfigService remoteConfig,
            IConfigsService configs,
            ISaveService save,
            ISceneTransitionService sceneTransition,
            LoadingSettings settings)
        {
            _orchestrator = orchestrator;
            _view = view;
            _catalog = catalog;
            _remoteConfig = remoteConfig;
            _configs = configs;
            _save = save;
            _sceneTransition = sceneTransition;
            _settings = settings;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                _view.SetVisible(true);
                _view.SetErrorVisible(false);

                _orchestrator.SetPhases(BuildPhases());
                _orchestrator.ProgressChanged += OnProgressChanged;
                _orchestrator.ActiveDescriptionChanged += OnActiveDescriptionChanged;

                try
                {
                    await RunWithRetryAsync(cancellation);
                }
                finally
                {
                    _orchestrator.ProgressChanged -= OnProgressChanged;
                    _orchestrator.ActiveDescriptionChanged -= OnActiveDescriptionChanged;
                }
            }
            catch (OperationCanceledException)
            {
                // shutdown — ignore
            }
        }

        private async UniTask RunWithRetryAsync(CancellationToken ct)
        {
            var startPhaseIndex = 0;

            while (!ct.IsCancellationRequested)
            {
                var result = await _orchestrator.RunAsync(startPhaseIndex, GlobalTimeout, ct);
                if (result.IsSuccess)
                {
                    Debug.Log($"{LogPrefix} Loading complete.");
                    // К моменту сюда мы уже в gameplay-сцене. LoadingScreenView жил в boot-сцене
                    // и уничтожен SceneManager.LoadSceneAsync(Single). SetVisible не вызываем.
                    return;
                }

                var f = result.Failure;
                Debug.LogError(
                    $"{LogPrefix} Failed at phase={f?.PhaseId} op={f?.OperationId} " +
                    $"timedOut={f?.TimedOut} attempt={f?.Attempt}: {f?.Exception?.Message}");

                _view.SetError(ErrorMessage);
                _view.SetErrorVisible(true);
                await _view.WaitForRetryClickAsync(ct);
                _view.SetErrorVisible(false);

                startPhaseIndex = result.FailedPhaseIndex ?? 0;
                _orchestrator.ResetFromPhase(startPhaseIndex);
            }
        }

        private IReadOnlyList<LoadingPhase> BuildPhases()
        {
            var skipHeavy = DebugStartFlags.SkipFullLoading;
            if (skipHeavy)
            {
                Debug.LogWarning(
                    $"{LogPrefix} SkipFullLoading=true: Addressables update и RemoteConfig init будут пропущены.");
            }

            var technicalOps = skipHeavy
                ? new ILoadingOperation[] { new WarmupOperation() }
                : new ILoadingOperation[]
                {
                    new AddressablesUpdateOperation(_catalog),
                    new RemoteConfigInitOperation(_remoteConfig)
                };

            var phaseTechnical = new LoadingPhase("phase_technical_init", new[]
            {
                new LoadingGroup("phase_technical_seq", LoadingGroupExecutionMode.Sequential, technicalOps)
            });

            var phaseData = new LoadingPhase("phase_data_load", new[]
            {
                new LoadingGroup("phase_data_parallel", LoadingGroupExecutionMode.Parallel, new ILoadingOperation[]
                {
                    new ConfigsWarmupOperation(_configs),
                    new SaveDataLoadOperation(_save)
                })
            });

            var phaseFinal = new LoadingPhase("phase_finalization", new[]
            {
                new LoadingGroup("phase_finalization_seq", LoadingGroupExecutionMode.Sequential, new ILoadingOperation[]
                {
                    new WarmupOperation(),
                    new SceneTransitionOperation(_sceneTransition, _settings.GameplaySceneName)
                })
            });

            return new[] { phaseTechnical, phaseData, phaseFinal };
        }

        private void OnProgressChanged(float value)
        {
            // View может быть уже уничтожен после SceneTransitionOperation
            // (boot-сцена выгружена). Unity перегружает == чтобы вернуть true для
            // destroyed MonoBehaviour — проверка работает.
            if (_view != null)
            {
                _view.SetProgress(value);
            }
        }

        private void OnActiveDescriptionChanged(string description)
        {
            if (_view != null)
            {
                _view.SetStatus(description);
            }
        }
    }
}
