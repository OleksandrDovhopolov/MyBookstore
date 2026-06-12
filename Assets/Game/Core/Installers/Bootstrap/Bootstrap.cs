using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Remote;
using Game.Ftue.Services;
using Infrastructure;
using Save;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    /// <summary>
    /// MonoBehaviour entry point of the boot scene. Replaces the previous <c>LoadingOrchestratorEntryPoint</c>:
    /// dependencies come through [Inject] from VContainer (root prefab = <c>GlobalLifetimeScope</c>),
    /// <see cref="LoadingScreenView"/> is a regular SerializeField on the scene.
    ///
    /// Setup:
    /// 1. Put this component on a GameObject in <c>Bootstrap.unity</c>.
    /// 2. Next to it, a regular VContainer <see cref="VContainer.Unity.LifetimeScope"/>
    ///    whose <c>Auto Inject Game Objects</c> list includes this GameObject.
    /// 3. Assign <c>Loading Screen View</c> through the inspector (the component on BootstrapCanvas).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        private const string LogPrefix = "[Bootstrap]";
        private const string DefaultErrorMessage = "Check your internet connection and try again.";

        [Header("Pacing")]
        [Tooltip("Minimum time on the loading screen. UX delay so the loader does not flash on fast starts.")]
        [SerializeField] private float _minimumLoadingSeconds = 0f;

        [Tooltip("Global loading timeout. 0 disables the timeout (not recommended).")]
        [SerializeField] private float _globalLoadingTimeoutSeconds = 60f;

        [Header("Routing")]
        [Tooltip("Name of the scene the loader transitions to on success. Must be in Build Settings.")]
        [SerializeField] private string _mainSceneName = "GameplayScene";

        [Header("UI")]
        [SerializeField] private LoadingScreenView _loadingScreenView;

        private LoadingOrchestrator _orchestrator;
        private IAddressablesCatalogService _catalog;
        private IRemoteConfigService _remoteConfig;
        private IConfigsService _configs;
        private ISaveService _save;
        private ISceneTransitionService _sceneTransition;
        private IFtueBootstrapper _ftue;

        // NOT GetCancellationTokenOnDestroy(): the boot GameObject is destroyed during
        // SceneManager.LoadSceneAsync(Single), and the final SceneTransitionOperation would surface
        // OperationCanceledException, which the orchestrator reports as "scene failure".
        // Application.exitCancellationToken is cancelled only on application exit
        // (and on Stop in the Editor) — exactly what a bootstrap that outlives the scene swap needs.
        private CancellationToken _appExitCt;

        [Inject]
        public void Construct(
            LoadingOrchestrator orchestrator,
            IAddressablesCatalogService catalog,
            IRemoteConfigService remoteConfig,
            IConfigsService configs,
            ISaveService save,
            ISceneTransitionService sceneTransition,
            IFtueBootstrapper ftue)
        {
            _orchestrator = orchestrator;
            _catalog = catalog;
            _remoteConfig = remoteConfig;
            _configs = configs;
            _save = save;
            _sceneTransition = sceneTransition;
            _ftue = ftue;
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
                Debug.LogError($"{LogPrefix} LoadingScreenView is not assigned in the inspector.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_mainSceneName))
            {
                Debug.LogError($"{LogPrefix} MainSceneName is empty. Loading cannot finish.");
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

                // UX: do not leave the splash sooner than _minimumLoadingSeconds.
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
                Debug.LogWarning($"{LogPrefix} SkipFullLoading=true: Addressables update and RemoteConfig init will be skipped.");

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

            // FTUE phase — between data_load and finalization: ISaveService is already loaded (data_load),
            // and ISceneTransitionService has not swapped the scene yet (finalization). The FTUE service
            // writes the starting day_progress into the save, and DayProgressService.LoadAsync in GameplayScene
            // picks it up.
            var phaseFtue = new LoadingPhase("phase_ftue", new[]
            {
                new LoadingGroup("phase_ftue_seq", LoadingGroupExecutionMode.Sequential, new ILoadingOperation[]
                {
                    new FtueBootstrapOperation(_ftue)
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

            return new[] { phaseTechnical, phaseData, phaseFtue, phaseFinal };
        }

        private void OnProgressChanged(float value)
        {
            // The view dies with the boot scene during SceneTransitionOperation. Unity-style null check.
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
