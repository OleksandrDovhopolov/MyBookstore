using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Remote;
using Infrastructure;
using Save;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    /// <summary>
    /// MonoBehaviour-точка входа boot-сцены. Заменяет прежний <c>LoadingOrchestratorEntryPoint</c>:
    /// зависимости приходят через [Inject] из VContainer (root prefab = <c>GlobalLifetimeScope</c>),
    /// <see cref="LoadingScreenView"/> — обычный SerializeField на сцене.
    ///
    /// Установка:
    /// 1. Положить этот компонент на GameObject в <c>Bootstrap.unity</c>.
    /// 2. Рядом с ним — обычный VContainer'овский <see cref="VContainer.Unity.LifetimeScope"/>,
    ///    у которого в <c>Auto Inject Game Objects</c> указан этот же GameObject.
    /// 3. <c>Loading Screen View</c> назначить через инспектор (компонент на BootstrapCanvas).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private const string LogPrefix = "[Bootstrap]";
        private const string DefaultErrorMessage = "Check your internet connection and try again.";

        [Header("Pacing")]
        [Tooltip("Минимальное время на экране загрузки. UX-задержка, чтобы лоадер не моргал на быстрых стартах.")]
        [SerializeField] private float _minimumLoadingSeconds = 0f;

        [Tooltip("Общий таймаут загрузки. 0 — без таймаута (не рекомендуется).")]
        [SerializeField] private float _globalLoadingTimeoutSeconds = 60f;

        [Header("Routing")]
        [Tooltip("Имя сцены, в которую переходит лоадер после успешного завершения. Должна быть в Build Settings.")]
        [SerializeField] private string _mainSceneName = "GameplayScene";

        [Header("UI")]
        [SerializeField] private LoadingScreenView _loadingScreenView;

        private LoadingOrchestrator _orchestrator;
        private IAddressablesCatalogService _catalog;
        private IRemoteConfigService _remoteConfig;
        private IConfigsService _configs;
        private ISaveService _save;
        private ISceneTransitionService _sceneTransition;

        // НЕ GetCancellationTokenOnDestroy(): boot-GameObject уничтожается во время
        // SceneManager.LoadSceneAsync(Single), и финал SceneTransitionOperation вылетал бы
        // OperationCanceledException, который оркестратор репортит как «фейл сцены».
        // Application.exitCancellationToken отменяется только при выходе из приложения
        // (и при Stop в Editor) — это ровно то, что нужно бутстрапу, переживающему swap сцены.
        private CancellationToken _appExitCt;

        [Inject]
        public void Construct(
            LoadingOrchestrator orchestrator,
            IAddressablesCatalogService catalog,
            IRemoteConfigService remoteConfig,
            IConfigsService configs,
            ISaveService save,
            ISceneTransitionService sceneTransition)
        {
            _orchestrator = orchestrator;
            _catalog = catalog;
            _remoteConfig = remoteConfig;
            _configs = configs;
            _save = save;
            _sceneTransition = sceneTransition;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            _appExitCt = Application.exitCancellationToken;
            RunBootstrapAsync(_appExitCt).Forget();
        }

        private async UniTask RunBootstrapAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_loadingScreenView == null)
            {
                Debug.LogError($"{LogPrefix} LoadingScreenView не назначен в инспекторе.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_mainSceneName))
            {
                Debug.LogError($"{LogPrefix} MainSceneName пуст. Загрузка не сможет завершиться.");
                return;
            }

            _loadingScreenView.SetVisible(true);
            _loadingScreenView.SetErrorVisible(false);

            _orchestrator.SetPhases(BuildPhases());
            _orchestrator.ProgressChanged += OnProgressChanged;
            _orchestrator.ActiveDescriptionChanged += OnActiveDescriptionChanged;

            var startRealtime = Time.realtimeSinceStartupAsDouble;
            var startPhaseIndex = 0;
            var globalTimeout = _globalLoadingTimeoutSeconds > 0f
                ? TimeSpan.FromSeconds(_globalLoadingTimeoutSeconds)
                : (TimeSpan?)null;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var result = await _orchestrator.RunAsync(startPhaseIndex, globalTimeout, ct);
                    if (result.IsSuccess)
                    {
                        Debug.Log($"{LogPrefix} Loading complete.");
                        break;
                    }

                    var f = result.Failure;
                    Debug.LogError(
                        $"{LogPrefix} Failed at phase={f?.PhaseId} op={f?.OperationId} " +
                        $"timedOut={f?.TimedOut} attempt={f?.Attempt}: {f?.Exception?.Message}");

                    _loadingScreenView.SetError(DefaultErrorMessage);
                    _loadingScreenView.SetErrorVisible(true);
                    await _loadingScreenView.WaitForRetryClickAsync(ct);
                    _loadingScreenView.SetErrorVisible(false);

                    startPhaseIndex = result.FailedPhaseIndex ?? 0;
                    _orchestrator.ResetFromPhase(startPhaseIndex);
                }

                // UX: не уходить со сплеша быстрее, чем за _minimumLoadingSeconds.
                var minSeconds = Mathf.Max(0f, _minimumLoadingSeconds);
                var elapsed = Time.realtimeSinceStartupAsDouble - startRealtime;
                var delaySeconds = minSeconds - elapsed;
                if (delaySeconds > 0d)
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                // shutdown — ok
            }
            finally
            {
                _orchestrator.ProgressChanged -= OnProgressChanged;
                _orchestrator.ActiveDescriptionChanged -= OnActiveDescriptionChanged;
            }
        }

        private IReadOnlyList<LoadingPhase> BuildPhases()
        {
            var skipHeavy = DebugStartFlags.SkipFullLoading;
            if (skipHeavy)
                Debug.LogWarning($"{LogPrefix} SkipFullLoading=true: Addressables update и RemoteConfig init будут пропущены.");

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
                    new SceneTransitionOperation(_sceneTransition, _mainSceneName)
                })
            });

            return new[] { phaseTechnical, phaseData, phaseFinal };
        }

        private void OnProgressChanged(float value)
        {
            // View умирает с boot-сценой во время SceneTransitionOperation. Unity-style null-check.
            if (_loadingScreenView != null)
                _loadingScreenView.SetProgress(value);
        }

        private void OnActiveDescriptionChanged(string description)
        {
            if (_loadingScreenView != null)
                _loadingScreenView.SetStatus(description);
        }
    }
}
