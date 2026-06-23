using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Morning;
using Game.Preparation.Services;
using Game.Preparation.UI;
using Game.Resources.API;
using Game.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

[Window("GameplaySceneController", WindowType.HUD)]
public class GameplaySceneController: WindowController<GameplaySceneView>
{
    private IResourcesService _resources;
    private IMorningSessionService _session;
    private IPreparationSessionService _preparationSession;
    private ISubscriber<GameplaySceneButtonsInteractableChanged> _buttonsInteractableSubscriber;
    private ISubscriber<GameplayGenreBookCountsChanged> _genreBookCountsSubscriber;
    private IPublisher<GameplayGenreBookCountsRequested> _genreBookCountsRequestPublisher;
    private IDisposable _buttonsInteractableSubscription;
    private IDisposable _genreBookCountsSubscription;

    [Inject]
    public void Construct(
        IResourcesService resources,
        IMorningSessionService morningSessionService,
        ISubscriber<GameplaySceneButtonsInteractableChanged> buttonsInteractableSubscriber,
        IPreparationSessionService preparationSession = null,
        ISubscriber<GameplayGenreBookCountsChanged> genreBookCountsSubscriber = null,
        IPublisher<GameplayGenreBookCountsRequested> genreBookCountsRequestPublisher = null)
    {
        _resources = resources;
        _session = morningSessionService;
        _preparationSession = preparationSession;
        _buttonsInteractableSubscriber = buttonsInteractableSubscriber;
        _genreBookCountsSubscriber = genreBookCountsSubscriber;
        _genreBookCountsRequestPublisher = genreBookCountsRequestPublisher;
    }

    protected override void OnInit()
    {
        if (View.StartDayButton != null)
            View.StartDayButton.onClick.AddListener(OnStartGameClicked);

        _buttonsInteractableSubscription = _buttonsInteractableSubscriber.Subscribe(
            e => SetSceneButtonsInteractable(e.Interactable));

        _genreBookCountsSubscription = _genreBookCountsSubscriber?.Subscribe(
            e => View.SetGenreBookCounts(e.Counts));
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

        RefreshDayAndGenreCountsAsync().Forget();
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
    //private void OnProgressionChanged(ProgressionChangeEvent _) => Refresh();

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

        if (View != null && View.StartDayButton != null)
            View.StartDayButton.onClick.RemoveAllListeners();
    }

    private void SetSceneButtonsInteractable(bool interactable)
    {
        View?.SetSceneButtonsInteractable(interactable);
    }
    private void OnStartGameClicked() => StartGameAsync().Forget();
    
    private async UniTaskVoid StartGameAsync()
    {
        View.SetStartButtonActive(false);

        try
        {
            var continued = await StartPreparationAsync(View.destroyCancellationToken);
            if (!continued)
            {
                View.SetStartButtonActive(true);
                return;
            }
            
            var window = await UIManager.ShowAsync<PreparationWindow>(ct: View.destroyCancellationToken);
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
            Debug.LogError($"[GameplaySceneController] Failed to open PreparationWindow: {e}");
            View.SetStartButtonActive(true);
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

        Debug.Log($"[GameplaySceneController] -> Preparation. Day {result.Day}, " +
                  $"modifiers=[{string.Join(",", result.ActiveModifierIds)}], " +
                  $"locations=[{string.Join(",", result.TargetLocationIds)}].");

        return true;
    }
}
