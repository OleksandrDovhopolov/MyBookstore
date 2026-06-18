using Cysharp.Threading.Tasks;
using Game.Resources.API;
using Game.Shop.UI;
using Game.UI;
using UnityEngine;
using VContainer;

[Window("GameplaySceneController", WindowType.HUD)]
public class GameplaySceneController: WindowController<GameplaySceneView>
{
    private IResourcesService _resources;
    //private IProgressionService _progression;

    [Inject]
    public void Construct(IResourcesService resources)
    {
        _resources = resources;
    }

    protected override void OnInit()
    {
        if (View.OpenShopButton != null)
            View.OpenShopButton.onClick.AddListener(OnOpenShopClicked);
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
        if (View != null && View.OpenShopButton != null)
            View.OpenShopButton.onClick.RemoveListener(OnOpenShopClicked);
    }

    private void OnOpenShopClicked() => OpenShopAsync().Forget();

    private async UniTaskVoid OpenShopAsync()
    {
        if (UIManager == null) return;
        await UIManager.ShowAsync<ClassicShopWindow>();
    }
}
