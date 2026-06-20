using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Game.Shop.UI;
using Game.UI;
using MessagePipe;
using System;
using Game.DayCycle.Morning.UI;
using UnityEngine;
using VContainer;

[Window("GameplaySceneController", WindowType.HUD)]
public class GameplaySceneController: WindowController<GameplaySceneView>
{
    private IResourcesService _resources;
    private ISubscriber<GameplaySceneButtonsInteractableChanged> _buttonsInteractableSubscriber;
    private ISubscriber<GameplayGenreBookCountsChanged> _genreBookCountsSubscriber;
    private IPublisher<GameplayGenreBookCountsRequested> _genreBookCountsRequestPublisher;
    private IDisposable _buttonsInteractableSubscription;
    private IDisposable _genreBookCountsSubscription;
    //private IProgressionService _progression;

    [Inject]
    public void Construct(
        IResourcesService resources,
        ISubscriber<GameplaySceneButtonsInteractableChanged> buttonsInteractableSubscriber,
        ISubscriber<GameplayGenreBookCountsChanged> genreBookCountsSubscriber = null,
        IPublisher<GameplayGenreBookCountsRequested> genreBookCountsRequestPublisher = null)
    {
        _resources = resources;
        _buttonsInteractableSubscriber = buttonsInteractableSubscriber;
        _genreBookCountsSubscriber = genreBookCountsSubscriber;
        _genreBookCountsRequestPublisher = genreBookCountsRequestPublisher;
    }

    protected override void OnInit()
    {
        if (View.OpenShopButton != null)
            View.OpenShopButton.onClick.AddListener(OnOpenShopClicked);

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
        //_progression.Changed += OnProgressionChanged;

        var goldAmount = _resources.GetAmount(ResourceIds.Gold);
        Debug.LogWarning($"[GameplaySceneController] goldAmount {goldAmount}");
        View.SetGoldAmount(goldAmount);
        View.SetGenreBookCounts(null);
        _genreBookCountsRequestPublisher?.Publish(new GameplayGenreBookCountsRequested());
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

        if (View != null && View.OpenShopButton != null)
            View.OpenShopButton.onClick.RemoveListener(OnOpenShopClicked);

        if (View != null && View.StartDayButton != null)
            View.StartDayButton.onClick.RemoveListener(OnStartGameClicked);
    }

    public void SetSceneButtonsInteractable(bool interactable)
    {
        View?.SetSceneButtonsInteractable(interactable);
    }

    private void OnOpenShopClicked() => OpenShopAsync().Forget();
    private void OnStartGameClicked() => StartGameAsync().Forget();

    private async UniTaskVoid StartGameAsync()
    {
        // TODO: Replace this temporary scene lookup with a proper phase router.
        var morningScreen = View.MorningScreenRoot != null
            ? View.MorningScreenRoot.GetComponent<MorningScreenView>()
            : null;

        if (morningScreen == null)
        {
            Debug.LogWarning("[GameplaySceneController] MorningScreenView not found, cannot start day.");
            return;
        }

        var preparationScreenRoot = View.PreparationScreenRoot;
        if (preparationScreenRoot == null)
        {
            Debug.LogWarning("[GameplaySceneController] PreparationScreenView not found, cannot start day.");
            return;
        }

        View.SetStartButtonActive(false);
        var continued = await morningScreen.ContinueToPreparationAsync(View.destroyCancellationToken);
        if (!continued)
        {
            View.SetStartButtonActive(true);
            return;
        }

        preparationScreenRoot.SetActive(true);
        View.MorningScreenRoot.SetActive(false);
    }

    private async UniTaskVoid OpenShopAsync()
    {
        if (UIManager == null) return;
        await UIManager.ShowAsync<ClassicShopWindow>();
    }
}
