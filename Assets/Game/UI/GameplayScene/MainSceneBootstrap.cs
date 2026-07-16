using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.Ftue;
using Game.Ftue.Domain;
using Game.Ftue.Services;
using Game.LocationUnlock.API;
using Game.Preparation.Services;
using Game.UI;
using MessagePipe;
using Save;
using UnityEngine;
using VContainer;

namespace GameplayUI
{
    public class MainSceneBootstrap : MonoBehaviour
    {
        private UIManager _uiManager;
        private ISaveService _save;
        private ITransitionAnimationService _transition;
        private IPublisher<GameplayHubReady> _hubReadyPublisher;
        private WelcomeWindowStartupSettings _welcomeWindowStartupSettings;

        private FirstDayEntrySettings _firstDayEntrySettings;
        private IDayProgressService _dayProgress;
        private FirstDayEntryFlow _firstDayEntryFlow;

        private CancellationToken _destroyToken;

        [Inject]
        public void Install(
            UIManager uiManager,
            ISaveService save,
            ITransitionAnimationService transition,
            IPublisher<GameplayHubReady> hubReadyPublisher,
            WelcomeWindowStartupSettings welcomeWindowStartupSettings,
            FirstDayEntrySettings firstDayEntrySettings = null,
            IDayProgressService dayProgress = null,
            IMorningSessionService morningSession = null,
            IPreparationSessionService preparationSession = null,
            IGameFlowService gameFlow = null,
            IConfigsService configs = null,
            ILocationUnlockService locationUnlock = null)
        {
            _uiManager = uiManager;
            _save = save;
            _transition = transition;
            _hubReadyPublisher = hubReadyPublisher;
            _welcomeWindowStartupSettings = welcomeWindowStartupSettings;
            _firstDayEntrySettings = firstDayEntrySettings;
            _dayProgress = dayProgress;

            if (morningSession != null && preparationSession != null && gameFlow != null && configs != null)
            {
                _firstDayEntryFlow = new FirstDayEntryFlow(
                    morningSession, preparationSession, gameFlow, configs, locationUnlock);
            }
        }

        private void Awake()
        {
            _destroyToken = this.GetCancellationTokenOnDestroy();
        }

        private void Start()
        {
            LoadGameplayAsync(_destroyToken).Forget();
        }

        private async UniTaskVoid LoadGameplayAsync(CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var hud = await _uiManager.ShowAsync<GameplaySceneController>(ct: ct);

                await UniTask.WaitUntil(() => hud.IsShown && hud.IsDataReady, cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                var firstEntry = await IsFirstEntryAsync(ct);
                var showWelcomeWindow = firstEntry && (_welcomeWindowStartupSettings?.StartWelcomeWindow ?? true);
                var directEntry = await ShouldEnterFirstDayLocationAsync(ct);

                var deferHubReveal = directEntry && !showWelcomeWindow;

                if (showWelcomeWindow || deferHubReveal)
                    hud.SetHudVisible(false);

                if (!deferHubReveal)
                    await _transition.PlayRevealAsync(ct);

                if (showWelcomeWindow)
                {
                    await ShowWelcomeAndWaitAsync(ct);
                    hud.SetHudVisible(true);
                }

                if (directEntry)
                {
                    if (await _firstDayEntryFlow.EnterAsync(ct))
                        return;

                    hud.SetHudVisible(true);
                    if (deferHubReveal)
                        await _transition.PlayRevealAsync(ct);
                }

                _hubReadyPublisher?.Publish(new GameplayHubReady(0));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask<bool> IsFirstEntryAsync(CancellationToken ct)
        {
            var welcome = await _save.GetModuleAsync<WelcomeCompletedState>(FtueSaveKeys.WelcomeCompleted, ct);
            return welcome == null || !welcome.Completed;
        }

        // Day-1 "drop straight into the location" gate. True only on a fresh, not-yet-started day 1 when the
        // product switch is set to Location and the orchestrator is available. Any miss → normal hub flow.
        private async UniTask<bool> ShouldEnterFirstDayLocationAsync(CancellationToken ct)
        {
            if (_firstDayEntrySettings?.Mode != FirstDayEntryMode.Location) return false;
            if (_firstDayEntryFlow == null || _dayProgress == null) return false;

            var state = await _dayProgress.LoadAsync(ct);
            return state != null
                   && state.CurrentDay == 1
                   && state.CurrentPhase == DayPhase.Morning
                   && !(state.CompletedDays?.Contains(state.CurrentDay) ?? false);
        }

        private async UniTask ShowWelcomeAndWaitAsync(CancellationToken ct)
        {
            var welcome = await _uiManager.ShowAsync<WelcomeWindowController>(ct: ct);
            if (welcome == null) return;

            await UniTask.WaitUntil(() => !welcome.IsShown, cancellationToken: ct);
        }
    }
}
