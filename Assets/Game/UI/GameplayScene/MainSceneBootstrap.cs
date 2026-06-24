using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI;
using UnityEngine;
using VContainer;

public class MainSceneBootstrap : MonoBehaviour
{
    private UIManager _uiManager;

    private CancellationToken _destroyToken;

    [Inject]
    public void Install(UIManager uiManager)
    {
        _uiManager = uiManager;
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

            var controller = await _uiManager.ShowAsync<GameplaySceneController>(ct: ct);
            await UniTask.WaitUntil(() => controller.IsShown && controller.SpritesLoaded, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
