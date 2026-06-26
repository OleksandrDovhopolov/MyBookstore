using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Ftue;
using Game.Ftue.Domain;
using Game.Ftue.Services;
using Game.UI;
using Save;
using UnityEngine;
using VContainer;

public class MainSceneBootstrap : MonoBehaviour
{
    private UIManager _uiManager;
    private ISaveService _save;

    private CancellationToken _destroyToken;

    [Inject]
    public void Install(UIManager uiManager, ISaveService save)
    {
        _uiManager = uiManager;
        _save = save;
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

            var firstEntry = await IsFirstEntryAsync(ct);

            var hud = await _uiManager.ShowAsync<GameplaySceneController>(ct: ct);

            // Keep the hub mounted (so it preloads) but invisible behind the non-full-screen welcome
            // letter; reveal it once the player presses Start.
            if (firstEntry)
            {
                hud.SetHudVisible(false);
                await ShowWelcomeAndWaitAsync(ct);
            }

            await UniTask.WaitUntil(() => hud.IsShown && hud.SpritesLoaded, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            if (firstEntry)
                hud.SetHudVisible(true);
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

    private async UniTask ShowWelcomeAndWaitAsync(CancellationToken ct)
    {
        var welcome = await _uiManager.ShowAsync<WelcomeWindowController>(ct: ct);
        if (welcome == null) return;

        await UniTask.WaitUntil(() => !welcome.IsShown, cancellationToken: ct);
    }
}
