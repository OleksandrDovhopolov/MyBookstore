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
    // Строит фазы загрузки, запускает оркестратор. Retry — пока вручную через перезапуск
    // (LoadingScreenView подключим следующим шагом после сборки префаба).
    public sealed class LoadingOrchestratorEntryPoint : IAsyncStartable
    {
        private const string LogPrefix = "[LoadingOrchestrator]";

        private readonly LoadingOrchestrator _orchestrator;
        private readonly IAddressablesCatalogService _catalog;
        private readonly IRemoteConfigService _remoteConfig;
        private readonly IConfigsService _configs;
        private readonly ISaveService _save;

        public LoadingOrchestratorEntryPoint(
            LoadingOrchestrator orchestrator,
            IAddressablesCatalogService catalog,
            IRemoteConfigService remoteConfig,
            IConfigsService configs,
            ISaveService save)
        {
            _orchestrator = orchestrator;
            _catalog = catalog;
            _remoteConfig = remoteConfig;
            _configs = configs;
            _save = save;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                _orchestrator.SetPhases(BuildPhases());
                _orchestrator.ProgressChanged += OnProgressChanged;
                _orchestrator.ActiveDescriptionChanged += OnActiveDescriptionChanged;

                try
                {
                    var result = await _orchestrator.RunAsync(0, TimeSpan.FromSeconds(60), cancellation);
                    if (result.IsSuccess)
                    {
                        Debug.Log($"{LogPrefix} Loading complete.");
                    }
                    else
                    {
                        var f = result.Failure;
                        Debug.LogError(
                            $"{LogPrefix} Loading failed at phase={f?.PhaseId} op={f?.OperationId} " +
                            $"timedOut={f?.TimedOut} attempt={f?.Attempt}: {f?.Exception?.Message}");
                    }
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

        private IReadOnlyList<LoadingPhase> BuildPhases()
        {
            var phaseTechnical = new LoadingPhase("phase_technical_init", new[]
            {
                new LoadingGroup("phase_technical_seq", LoadingGroupExecutionMode.Sequential, new ILoadingOperation[]
                {
                    new AddressablesUpdateOperation(_catalog),
                    new RemoteConfigInitOperation(_remoteConfig)
                })
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
                    new WarmupOperation()
                })
            });

            return new[] { phaseTechnical, phaseData, phaseFinal };
        }

        private static void OnProgressChanged(float value)
        {
            Debug.Log($"{LogPrefix} progress={value:P0}");
        }

        private static void OnActiveDescriptionChanged(string description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                Debug.Log($"{LogPrefix} status='{description}'");
            }
        }
    }
}
