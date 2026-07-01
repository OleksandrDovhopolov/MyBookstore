using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
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
    private ITransitionAnimationService _transition;

    private CancellationToken _destroyToken;

    [Inject]
    public void Install(UIManager uiManager, ISaveService save, ITransitionAnimationService transition)
    {
        _uiManager = uiManager;
        _save = save;
        _transition = transition;
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

            // Wait until the hub window has loaded everything it needs to display (genre sprites, day
            // context, ...) before revealing the screen.
            await UniTask.WaitUntil(() => hud.IsShown && hud.IsDataReady, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            var firstEntry = await IsFirstEntryAsync(ct);

            // On first entry keep the hub invisible behind the non-full-screen welcome letter; the reveal
            // then shows the scene background + letter without flashing the HUD.
            if (firstEntry)
                hud.SetHudVisible(false);

            await _transition.PlayRevealAsync(ct);   // remove the transition cover

            if (firstEntry)
            {
                await ShowWelcomeAndWaitAsync(ct);
                hud.SetHudVisible(true);
            }
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
