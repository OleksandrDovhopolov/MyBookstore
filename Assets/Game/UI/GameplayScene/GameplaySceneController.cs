using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.Location.UI;
using Game.LocationUnlock.API;
using Game.Newspaper.UI;
using Game.Preparation.Services;
using Game.Preparation.UI;
using Game.Resources.API;
using Game.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

[Window("GameplaySceneController", WindowType.HUD)]
public class GameplaySceneController : WindowController<GameplaySceneView>, IDataReadyWindow
{
    private IResourcesService _resources;
    private IDayProgressService _dayProgress;
    private IMorningSessionService _session;
    private IPreparationSessionService _preparationSession;
    private ILocationUnlockService _locationUnlock;
    private IConfigsService _configs;
    private IUiSpriteProvider _uiSprites;

    // True once the window has loaded all the data it needs to display (currently the genre sprites).
    public bool IsDataReady { get; private set; }

    private IDisposable _salesGoldSubscription;
    private IDisposable _genreBookCountsSubscription;
    private IDisposable _buttonsInteractableSubscription;
    
    private ISubscriber<GameplaySalesGoldChanged> _salesGoldSubscriber;
    private ISubscriber<GameplayGenreBookCountsChanged> _genreBookCountsSubscriber;
    private IPublisher<GameplayGenreBookCountsRequested> _genreBookCountsRequestPublisher;
    private ISubscriber<GameplaySceneButtonsInteractableChanged> _buttonsInteractableSubscriber;

    [Inject]
    public void Construct(
        IResourcesService resources,
        IUiSpriteProvider uiSprites,
        IDayProgressService dayProgress,
        IMorningSessionService morningSessionService,
        ISubscriber<GameplaySceneButtonsInteractableChanged> buttonsInteractableSubscriber,
        IPreparationSessionService preparationSession = null,
        ILocationUnlockService locationUnlock = null,
        IConfigsService configs = null,
        ISubscriber<GameplayGenreBookCountsChanged> genreBookCountsSubscriber = null,
        ISubscriber<GameplaySalesGoldChanged> salesGoldSubscriber = null,
        IPublisher<GameplayGenreBookCountsRequested> genreBookCountsRequestPublisher = null)
    {
        _resources = resources;
        _uiSprites = uiSprites;
        _dayProgress = dayProgress;
        _session = morningSessionService;
        _preparationSession = preparationSession;
        _locationUnlock = locationUnlock;
        _configs = configs;
        _salesGoldSubscriber = salesGoldSubscriber;
        _genreBookCountsSubscriber = genreBookCountsSubscriber;
        _buttonsInteractableSubscriber = buttonsInteractableSubscriber;
        _genreBookCountsRequestPublisher = genreBookCountsRequestPublisher;
    }

    protected override void OnInit()
    {
        if (View.StartDayButton != null)
            View.StartDayButton.onClick.AddListener(OnStartGameClicked);

        _buttonsInteractableSubscription = _buttonsInteractableSubscriber.Subscribe(
            e => SetSceneButtonsInteractable(e.Interactable));

        _genreBookCountsSubscription = _genreBookCountsSubscriber?.Subscribe(
            e => View.SetGenreBookCounts(e.Counts, e.PurchasedCounts, e.ShowPurchasedCounts));

        _salesGoldSubscription = _salesGoldSubscriber?.Subscribe(OnSalesGoldChanged);

        if (_dayProgress != null)
            _dayProgress.PhaseChanged += OnDayPhaseChanged;
    }

    protected override void OnShowStart()
    {
        if (_resources == null)
        {
            Debug.LogWarning("[GameplaySceneController] dependencies missing — not registered in DI?");
            return;
        }

        _resources.Changed += OnResourceChanged;

        View.SetGoldAmount(_resources.GetAmount(ResourceIds.Gold));
        View.SetSalesGoldVisible(false);

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

    private void OnResourceChanged(ResourceChangeEvent _) => Refresh();

    private void Refresh()
    {
        View.SetGoldAmount(_resources.GetAmount(ResourceIds.Gold));
    }

    protected override void OnHideStart(bool isClosed)
    {
        base.OnHideStart(isClosed);
        if (_resources != null) _resources.Changed -= OnResourceChanged;
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

        if (_dayProgress != null)
            _dayProgress.PhaseChanged -= OnDayPhaseChanged;
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
    }

    private void OnStartGameClicked() => StartGameAsync().Forget();
    
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
                  $"modifiers=[{string.Join(",", result.ActiveModifierIds)}], " +
                  $"locations=[{string.Join(",", result.TargetLocationIds)}].");

        return true;
    }
}
