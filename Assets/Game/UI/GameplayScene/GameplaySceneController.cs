using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.Decor.UI;
using Game.Location.UI;
using Game.LocationUnlock.API;
using Game.Newspaper.UI;
using Game.Preparation.Services;
using Game.Preparation.UI;
using Game.UI;
using Game.UI.ContentWidget;
using MessagePipe;
using UIShared;
using UnityEngine;
using VContainer;

[Window("GameplaySceneController", WindowType.HUD)]
public class GameplaySceneController : WindowController<GameplaySceneView>, IDataReadyWindow
{
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

    private IDisposable _salesGoldSubscription;
    private IDisposable _genreBookCountsSubscription;
    private IDisposable _buttonsInteractableSubscription;

    private readonly HashSet<IWindowController> _panelHideOwners = new();
    
    private ISubscriber<GameplaySalesGoldChanged> _salesGoldSubscriber;
    private ISubscriber<GameplayGenreBookCountsChanged> _genreBookCountsSubscriber;
    private IPublisher<GameplayGenreBookCountsRequested> _genreBookCountsRequestPublisher;
    private ISubscriber<GameplaySceneButtonsInteractableChanged> _buttonsInteractableSubscriber;

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
        ISubscriber<GameplaySalesGoldChanged> salesGoldSubscriber = null,
        IPublisher<GameplayGenreBookCountsRequested> genreBookCountsRequestPublisher = null)
    {
        _uiSprites = uiSprites;
        _dayProgress = dayProgress;
        _session = morningSessionService;
        _preparationSession = preparationSession;
        _saleChancePreview = saleChancePreview;
        _locationUnlock = locationUnlock;
        _configs = configs;
        _gameFlow = gameFlow;
        _salesGoldSubscriber = salesGoldSubscriber;
        _genreBookCountsSubscriber = genreBookCountsSubscriber;
        _buttonsInteractableSubscriber = buttonsInteractableSubscriber;
        _genreBookCountsRequestPublisher = genreBookCountsRequestPublisher;
    }

    protected override void OnInit()
    {
        if (View.StartDayButton != null)
            View.StartDayButton.onClick.AddListener(OnStartGameClicked);

        if (View.DecorButton != null)
            View.DecorButton.onClick.AddListener(OnDecorButtonClicked);

        View.GenreItemClicked += OnGenreItemClicked;

        _buttonsInteractableSubscription = _buttonsInteractableSubscriber.Subscribe(
            e => SetSceneButtonsInteractable(e.Interactable));

        _genreBookCountsSubscription = _genreBookCountsSubscriber?.Subscribe(
            e => View.SetGenreBookCounts(e.Counts, e.PurchasedCounts, e.ShowPurchasedCounts));

        _salesGoldSubscription = _salesGoldSubscriber?.Subscribe(OnSalesGoldChanged);

        if (_dayProgress != null)
            _dayProgress.PhaseChanged += OnDayPhaseChanged;

        if (_gameFlow != null)
            _gameFlow.LocationLoadedChanged += OnLocationLoadedChanged;
    }

    protected override void OnShowStart()
    {
        View.SetSalesGoldVisible(false);

        // The genre panel is shown only inside the location; sync it instantly to the current state so a
        // hub boot starts hidden and a resume in-location starts shown (no animation flash).
        View.SetPanelShown(
            AnimatedShowHidePanel.PanelId.GenreBookCounts,
            _gameFlow?.IsLocationLoaded == true,
            instant: true);

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

        _salesGoldSubscription?.Dispose();
        _salesGoldSubscription = null;

        if (View != null && View.StartDayButton != null)
            View.StartDayButton.onClick.RemoveAllListeners();

        if (View != null && View.DecorButton != null)
            View.DecorButton.onClick.RemoveListener(OnDecorButtonClicked);

        if (View != null)
            View.GenreItemClicked -= OnGenreItemClicked;

        foreach (var owner in _panelHideOwners)
            owner.Closed -= OnPanelHidingWindowClosed;
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

    private void OnSalesGoldChanged(GameplaySalesGoldChanged e)
    {
        if (View == null) return;

        View.SetSalesGoldAmount(e.GoldEarned);
        View.SetSalesGoldVisible(e.Visible);
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
            var args = new ContentWidgetArgs(data, anchor, this);
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
    
    private async UniTaskVoid StartGameAsync()
    {
        View.SetStartButtonActive(false);

        try
        {
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
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameplaySceneController] Failed to start the day: {e}");
            View.SetStartButtonActive(true);
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

    private void OnDecorButtonClicked() => ShowWindowWithPanelsHiddenAsync<DecorPlacementWindow>().Forget();

    private async UniTaskVoid ShowWindowWithPanelsHiddenAsync<TWindow>(WindowArgs args = null)
        where TWindow : class, IWindowController, new()
    {
        try
        {
            await View.HideAnimatedPanelsAsync();

            var window = await UIManager.ShowAsync<TWindow>(args, View.destroyCancellationToken);
            if (window == null)
            {
                ShowPanelsIfNoOwnersLeft();
                return;
            }

            _panelHideOwners.Add(window);
            window.Closed += OnPanelHidingWindowClosed;
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

    private void OnPanelHidingWindowClosed(IWindowController controller)
    {
        controller.Closed -= OnPanelHidingWindowClosed;
        _panelHideOwners.Remove(controller);
        ShowPanelsIfNoOwnersLeft();
    }

    private void ShowPanelsIfNoOwnersLeft()
    {
        // Re-show is not gating anything, so fire-and-forget the animation.
        if (_panelHideOwners.Count == 0)
            View.ShowAnimatedPanelsAsync().Forget();
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

        Debug.Log($"[GameplaySceneController] -> Preparation. Day {result.Day}, " +
                  $"locations=[{string.Join(",", result.TargetLocationIds)}].");

        return true;
    }
}
