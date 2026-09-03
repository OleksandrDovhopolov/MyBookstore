using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.Location.UI;
using Game.LocationUnlock.API;
using Game.Preparation.Services;
using Game.Preparation.UI;
using Game.Tutorial.API;
using Game.UI;
using Game.UI.ContentWidget;
using MessagePipe;
using SpriteService;
using UIShared;
using UnityEngine;
using VContainer;

namespace GameplayUI
{
    [Window("GameplaySceneController", WindowType.HUD)]
    public class GameplaySceneController : WindowController<GameplaySceneView>, IDataReadyWindow, IHudWindowLauncher
    {
        private const string TutorialClickGenreStepId = "click_genre_panel";
        private const string TutorialFinalTextStepId = "text_4";

        private IDayProgressService _dayProgress;
        private IMorningSessionService _session;
        private IPreparationSessionService _preparationSession;
        private ISaleChancePreviewService _saleChancePreview;
        private ILocationUnlockService _locationUnlock;
        private IConfigsService _configs;
        private IUiSpriteProvider _uiSprites;
        private IGameFlowService _gameFlow;

        // True once the window has loaded all the data it needs to display (currently the genre sprites).
        public bool IsDataReady { get; private set; }

        private IDisposable _genreBookCountsSubscription;
        private IDisposable _locationGoldEarnedSubscription;
        private IDisposable _buttonsInteractableSubscription;
        private IDisposable _tutorialStepSubscription;

        // Anything that currently wants the HUD panels hidden: either an open window (removed when it
        // closes) or a PanelHideLease taken by a multi-window flow. Panels come back only when empty.
        private readonly HashSet<object> _panelHideOwners = new();

        private ISubscriber<GameplayGenreBookCountsChanged> _genreBookCountsSubscriber;
        private ISubscriber<GameplayLocationGoldEarnedChanged> _locationGoldEarnedSubscriber;
        private IPublisher<GameplayGenreBookCountsRequested> _genreBookCountsRequestPublisher;
        private ISubscriber<GameplaySceneButtonsInteractableChanged> _buttonsInteractableSubscriber;
        private ISubscriber<TutorialStepChanged> _tutorialStepSubscriber;
        private bool _suppressSaleChanceWidgetAutoClose;

        [Inject]
        public void Construct(
            IUiSpriteProvider uiSprites,
            IDayProgressService dayProgress,
            IMorningSessionService morningSessionService,
            ISubscriber<GameplaySceneButtonsInteractableChanged> buttonsInteractableSubscriber,
            IPreparationSessionService preparationSession = null,
            ISaleChancePreviewService saleChancePreview = null,
            ILocationUnlockService locationUnlock = null,
            IConfigsService configs = null,
            IGameFlowService gameFlow = null,
            ISubscriber<GameplayGenreBookCountsChanged> genreBookCountsSubscriber = null,
            ISubscriber<GameplayLocationGoldEarnedChanged> locationGoldEarnedSubscriber = null,
            IPublisher<GameplayGenreBookCountsRequested> genreBookCountsRequestPublisher = null,
            ISubscriber<TutorialStepChanged> tutorialStepSubscriber = null)
        {
            _uiSprites = uiSprites;
            _dayProgress = dayProgress;
            _session = morningSessionService;
            _preparationSession = preparationSession;
            _saleChancePreview = saleChancePreview;
            _locationUnlock = locationUnlock;
            _configs = configs;
            _gameFlow = gameFlow;
            _genreBookCountsSubscriber = genreBookCountsSubscriber;
            _locationGoldEarnedSubscriber = locationGoldEarnedSubscriber;
            _buttonsInteractableSubscriber = buttonsInteractableSubscriber;
            _genreBookCountsRequestPublisher = genreBookCountsRequestPublisher;
            _tutorialStepSubscriber = tutorialStepSubscriber;
        }

        protected override void OnInit()
        {
            if (View.MenuButtons != null)
            {
                View.MenuButtons.StartDayClicked += OnStartGameClicked;
                View.MenuButtons.Bind(this);
            }

            View.GenreItemClicked += OnGenreItemClicked;

            _buttonsInteractableSubscription =
                _buttonsInteractableSubscriber.Subscribe(e => SetSceneButtonsInteractable(e.Interactable));

            _genreBookCountsSubscription = _genreBookCountsSubscriber?.Subscribe(e =>
                View.SetGenreBookCounts(e.Counts, e.PurchasedCounts, e.ShowPurchasedCounts));

            _locationGoldEarnedSubscription = _locationGoldEarnedSubscriber?.Subscribe(e =>
                View.SetLocationEarnedGold(e.Amount));

            _tutorialStepSubscription = _tutorialStepSubscriber?.Subscribe(OnTutorialStepChanged);

            if (_dayProgress != null)
                _dayProgress.PhaseChanged += OnDayPhaseChanged;

            if (_gameFlow != null)
                _gameFlow.LocationLoadedChanged += OnLocationLoadedChanged;
        }

        protected override void OnShowStart()
        {
            // The genre panel is shown only inside the location; sync it instantly to the current state so a
            // hub boot starts hidden and a resume in-location starts shown (no animation flash).
            View.SetPanelShown(
                AnimatedShowHidePanel.PanelId.GenreBookCounts,
                _gameFlow?.IsLocationLoaded == true,
                instant: true);
            View.SetGoldCounterMode(_gameFlow?.IsLocationLoaded == true);

            LoadGenreSpritesAsync(View.destroyCancellationToken).Forget();
            RefreshDayAndGenreCountsAsync().Forget();
        }

        private async UniTaskVoid LoadGenreSpritesAsync(CancellationToken ct)
        {
            try
            {
                var sprites = new Dictionary<BookGenre, Sprite>();
                foreach (BookGenre genre in Enum.GetValues(typeof(BookGenre)))
                {
                    var sprite = await _uiSprites.GetSpriteAsync(genre.ToString(), ct);
                    if (ct.IsCancellationRequested) return;
                    if (sprite != null) sprites[genre] = sprite;
                }

                View.SetGenreSprites(sprites);
                IsDataReady = true;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask RefreshDayAndGenreCountsAsync()
        {
            try
            {
                var ct = View.destroyCancellationToken;
                var context = await _session.StartOrResumeAsync(ct);
                View.SetDayText($"Day {context.Day}");

                if (_preparationSession != null)
                {
                    var counts = await _preparationSession.GetGenreQuantitiesPreviewAsync(ct);
                    View.SetGenreBookCounts(counts);
                }

                _genreBookCountsRequestPublisher?.Publish(new GameplayGenreBookCountsRequested());
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
        }

        protected override void OnDispose()
        {
            _buttonsInteractableSubscription?.Dispose();
            _buttonsInteractableSubscription = null;

            _genreBookCountsSubscription?.Dispose();
            _genreBookCountsSubscription = null;

            _locationGoldEarnedSubscription?.Dispose();
            _locationGoldEarnedSubscription = null;

            _tutorialStepSubscription?.Dispose();
            _tutorialStepSubscription = null;

            if (View?.MenuButtons != null)
            {
                View.MenuButtons.StartDayClicked -= OnStartGameClicked;
                View.MenuButtons.Unbind();
            }
            
            if (View != null)
                View.GenreItemClicked -= OnGenreItemClicked;

            foreach (var owner in _panelHideOwners)
            {
                if (owner is IWindowController window)
                    window.Closed -= OnPanelHidingWindowClosed;
            }

            _panelHideOwners.Clear();

            if (_dayProgress != null)
                _dayProgress.PhaseChanged -= OnDayPhaseChanged;

            if (_gameFlow != null)
                _gameFlow.LocationLoadedChanged -= OnLocationLoadedChanged;
        }

        private void SetSceneButtonsInteractable(bool interactable)
        {
            View?.SetSceneButtonsInteractable(interactable);
        }

        private void OnDayPhaseChanged(DayProgressState state)
        {
            if (state?.CurrentPhase == DayPhase.Morning)
                RefreshDayAndGenreCountsAsync().Forget();

            if (state?.CurrentPhase == DayPhase.Results)
                HideContentWidgetOnDayEnd();
        }

        // The genre panel tracks the location boundary (fired before each reveal), so it never flashes in the hub.
        private void OnLocationLoadedChanged(bool loaded)
        {
            View?.SetPanelShown(AnimatedShowHidePanel.PanelId.GenreBookCounts, loaded);
            View?.SetGoldCounterMode(loaded);
        }

        private void OnStartGameClicked() => StartGameAsync().Forget();

        private void OnGenreItemClicked(BookGenre genre, Sprite sprite, RectTransform anchor)
        {
            if (anchor == null) return;

            ShowSaleChanceWidgetAsync(genre, sprite, anchor).Forget();
        }

        private async UniTaskVoid ShowSaleChanceWidgetAsync(BookGenre genre, Sprite sprite, RectTransform anchor)
        {
            try
            {
                if (anchor == null || View == null) return;
                if (_saleChancePreview == null)
                {
                    Debug.LogWarning("[GameplaySceneController] ISaleChancePreviewService is not injected.");
                    return;
                }

                var ct = View.destroyCancellationToken;
                var percent = await _saleChancePreview.GetPercentAsync(genre, ct);
                if (View == null || anchor == null || ct.IsCancellationRequested) return;

                var data = new SaleChanceWidgetData(genre, percent, sprite);
                var args = new ContentWidgetArgs(
                    data,
                    anchor,
                    this,
                    autoCloseEnabled: !_suppressSaleChanceWidgetAutoClose);
                await UIManager.ShowAsync<ContentWidgetController>(args, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplaySceneController] Failed to show sale chance widget: {e}");
            }
        }

        private void HideContentWidgetOnDayEnd()
        {
            if (UIManager == null || !UIManager.IsWindowShown<ContentWidgetController>()) return;

            UIManager.HideAsync<ContentWidgetController>(forceClose: true, ct: CancellationToken.None).Forget();
        }

        private void OnTutorialStepChanged(TutorialStepChanged step)
        {
            _suppressSaleChanceWidgetAutoClose =
                step.SequenceId == TutorialSequenceIds.DayOne
                && (step.StepId == TutorialClickGenreStepId
                    || step.StepId == TutorialFinalTextStepId);
        }

        private async UniTaskVoid StartGameAsync()
        {
            View.SetStartButtonActive(false);

            IDisposable panelsLease = null;
            try
            {
                // Held for the whole flow: the Location window closes before the Preparation window
                // opens, so per-window ownership alone would drop to zero in between and flash the
                // panels back in. Released in finally, after Preparation has taken over as owner.
                panelsLease = await HideHudPanelsAsync();

                var locationId = await PickLocationAsync(View.destroyCancellationToken);
                if (string.IsNullOrEmpty(locationId))
                {
                    View.SetStartButtonActive(true);
                    return;
                }

                var continued = await StartPreparationAsync(View.destroyCancellationToken);
                if (!continued)
                {
                    View.SetStartButtonActive(true);
                    return;
                }

                var displayName = ResolveLocationDisplayName(locationId);
                var window = await UIManager.ShowAsync<PreparationWindow>(
                    new PreparationWindowArgs(locationId, displayName),
                    View.destroyCancellationToken);
                if (window == null)
                {
                    View.SetStartButtonActive(true);
                    return;
                }

                window.Closed += OnPreparationWindowClosed;
                TrackPanelHideOwner(window);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplaySceneController] Failed to start the day: {e}");
                View.SetStartButtonActive(true);
            }
            finally
            {
                panelsLease?.Dispose();
            }
        }

        // Opens the Location Window and waits for the player to pick an unlocked location (Start).
        // Returns null if the window was closed without a choice.
        private async UniTask<string> PickLocationAsync(CancellationToken ct)
        {
            try
            {
                var window = await UIManager.ShowAsync<LocationWindow>(new LocationWindowArgs(), ct);
                if (window == null) return null;
                return await window.WaitForResultAsync<string>(ct);
            }
            catch (InvalidOperationException ex)
            {
                var fallback = PickFirstUnlockedLocationId();
                if (!string.IsNullOrEmpty(fallback))
                {
                    Debug.LogWarning($"[GameplaySceneController] LocationWindow unavailable; " +
                                     $"falling back to '{fallback}'. {ex.Message}");
                    return fallback;
                }

                Debug.LogError($"[GameplaySceneController] LocationWindow unavailable and no unlocked " +
                               $"fallback location was found. {ex}");
                return null;
            }
        }

        private string ResolveLocationDisplayName(string locationId)
        {
            if (_configs != null && _configs.TryGet<LocationConfig>(locationId, out var config)
                                 && !string.IsNullOrEmpty(config.DisplayName))
                return config.DisplayName;
            return locationId;
        }

        private string PickFirstUnlockedLocationId()
        {
            if (_configs == null) return null;

            foreach (var config in _configs.GetAll<LocationConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                if (_locationUnlock == null || _locationUnlock.IsUnlocked(config.Id))
                    return config.Id;
            }

            return null;
        }

        public async UniTask OpenAsync<TWindow>(WindowArgs args = null)
            where TWindow : class, IWindowController, new()
        {
            // A widget (or the HUD itself) opens on top of the HUD without replacing its context, so it
            // leaves the panels up — and must not become a hide owner either: a still-open widget would
            // otherwise keep the panels down after every real owner has released them.
            if (KeepsHudPanelsVisible<TWindow>())
            {
                await ShowKeepingPanelsAsync<TWindow>(args);
                return;
            }

            try
            {
                await View.HideAnimatedPanelsAsync();

                var window = await UIManager.ShowAsync<TWindow>(args, View.destroyCancellationToken);
                if (window == null)
                {
                    ShowPanelsIfNoOwnersLeft();
                    return;
                }

                TrackPanelHideOwner(window);
            }
            catch (OperationCanceledException)
            {
                ShowPanelsIfNoOwnersLeft();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplaySceneController] Failed to open {typeof(TWindow).Name}: {e}");
                ShowPanelsIfNoOwnersLeft();
            }
        }

        private async UniTask ShowKeepingPanelsAsync<TWindow>(WindowArgs args)
            where TWindow : class, IWindowController, new()
        {
            try
            {
                await UIManager.ShowAsync<TWindow>(args, View.destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplaySceneController] Failed to open {typeof(TWindow).Name}: {e}");
            }
        }

        // Mirrors the [Window] lookup UIManager.ShowAsync does. A type without the attribute throws
        // there anyway, so it just falls through to the regular panel-hiding path.
        private static bool KeepsHudPanelsVisible<TWindow>()
            => typeof(TWindow).GetCustomAttribute<WindowAttribute>()?.Type
                is WindowType.Widget or WindowType.HUD;

        // Hides the HUD panels until the returned lease is disposed. Used by flows that open more than
        // one window in sequence, where per-window ownership would leave a gap between them.
        private async UniTask<IDisposable> HideHudPanelsAsync()
        {
            var lease = new PanelHideLease(this);
            _panelHideOwners.Add(lease);

            await View.HideAnimatedPanelsAsync();
            return lease;
        }

        // Add returns false for a window that is already an owner, so a repeated open never
        // subscribes OnPanelHidingWindowClosed twice.
        private void TrackPanelHideOwner(IWindowController window)
        {
            if (window == null) return;

            if (_panelHideOwners.Add(window))
                window.Closed += OnPanelHidingWindowClosed;
        }

        private void OnPanelHidingWindowClosed(IWindowController controller)
        {
            controller.Closed -= OnPanelHidingWindowClosed;
            _panelHideOwners.Remove(controller);
            ShowPanelsIfNoOwnersLeft();
        }

        private void ShowPanelsIfNoOwnersLeft()
        {
            // Can run after the HUD was torn down (cancelled flow, disposed lease) — nothing to animate then.
            if (View == null) return;

            // Re-show is not gating anything, so fire-and-forget the animation.
            if (_panelHideOwners.Count == 0)
                View.ShowAnimatedPanelsAsync().Forget();
        }

        private sealed class PanelHideLease : IDisposable
        {
            private GameplaySceneController _owner;

            public PanelHideLease(GameplaySceneController owner) => _owner = owner;

            public void Dispose()
            {
                if (_owner == null) return;

                var owner = _owner;
                _owner = null;
                owner._panelHideOwners.Remove(this);
                owner.ShowPanelsIfNoOwnersLeft();
            }
        }

        private void OnPreparationWindowClosed(IWindowController controller)
        {
            if (controller is not PreparationWindow window) return;

            window.Closed -= OnPreparationWindowClosed;
            if (!window.IsConfirmed)
                View.SetStartButtonActive(true);
        }

        private async UniTask<bool> StartPreparationAsync(CancellationToken ct)
        {
            if (_session == null)
            {
                Debug.LogWarning("[GameplaySceneController] Cannot continue: IMorningSessionService is not injected.");
                return false;
            }

            var result = await _session.ContinueToPreparationAsync(ct);
            if (result == null)
                return false;

            Debug.Log($"[GameplaySceneController] -> Preparation. Day {result.Day}.");

            return true;
        }
    }
}
