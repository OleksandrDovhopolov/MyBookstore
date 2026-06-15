using Game.Inventory.UI;
using Game.Resources.API;
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
}
