using System;
using System.Threading;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using Game.WorldHud;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.UI
{
    /// <summary>
    /// Owns the lifecycle of <see cref="RecommendationMinigameWindow"/>: listens for active requests on
    /// <see cref="ISalesDayController"/>, opens the window (handing it the gameplay-scoped controller via
    /// <see cref="RecommendationMinigameArgs"/>, since the bootstrap-scoped window factory cannot inject it),
    /// and exposes <see cref="IsWindowOpen"/> so <see cref="SalesScreenView"/> can pause the day while it is up.
    /// While the window is open it also suppresses every world HUD (thought bubbles) so the focused minigame
    /// is clean, restoring them on close. Extracted from SalesScreenView so the screen only pumps the sim
    /// and renders the header/log.
    /// </summary>
    public sealed class RecommendationMinigamePresenter : IRecommendationMinigamePresenter, IStartable, IDisposable
    {
        private readonly ISalesDayController _controller;
        private readonly IUIManager _uiManager;
        private readonly IWorldHudManager _worldHud;
        private readonly CancellationTokenSource _cts = new();

        private IDisposable _worldHudSuppression;

        public bool IsWindowOpen { get; private set; }

        public RecommendationMinigamePresenter(
            ISalesDayController controller,
            IUIManager uiManager = null,
            IWorldHudManager worldHud = null)
        {
            _controller = controller;
            _uiManager = uiManager;
            _worldHud = worldHud;
        }

        public void Start()
        {
            if (_controller != null)
                _controller.ActiveRequestStarted += OnActiveRequestStarted;
        }

        public void Dispose()
        {
            if (_controller != null)
                _controller.ActiveRequestStarted -= OnActiveRequestStarted;

            // Never leave the global HUD manager stuck suppressed if we unload while the window is open.
            ReleaseWorldHud();

            _cts.Cancel();
            _cts.Dispose();
        }

        private void OnActiveRequestStarted(RequestConfig req)
        {
            // Pause the day synchronously (so the very next Update skips Tick) and hide all world HUDs,
            // then open the window.
            IsWindowOpen = true;
            SuppressWorldHud();
            OpenWindowAsync().Forget();
        }

        private async UniTaskVoid OpenWindowAsync()
        {
            if (_uiManager == null)
            {
                Debug.LogWarning("[RecommendationMinigamePresenter] IUIManager was not injected — cannot open the recommendation minigame window.");
                EndMinigame();   // don't leave the day paused / HUDs hidden with no UI to resolve the request
                return;
            }

            try
            {
                var window = await _uiManager.ShowAsync<RecommendationMinigameWindow>(
                    new RecommendationMinigameArgs(_controller), _cts.Token);

                if (window != null)
                    window.Closed += OnWindowClosed;
                else
                    EndMinigame();
            }
            catch (OperationCanceledException)
            {
                EndMinigame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecommendationMinigamePresenter] Failed to open the recommendation minigame window: {ex}");
                EndMinigame();
            }
        }

        private void OnWindowClosed(IWindowController _)
        {
            // Window is disposed after close; resume the day and bring the HUDs back.
            EndMinigame();
        }

        // Resume the day and restore world HUDs. Safe to call multiple times.
        private void EndMinigame()
        {
            IsWindowOpen = false;
            ReleaseWorldHud();
        }

        private void SuppressWorldHud() => _worldHudSuppression ??= _worldHud?.SuppressAll();

        private void ReleaseWorldHud()
        {
            _worldHudSuppression?.Dispose();
            _worldHudSuppression = null;
        }
    }
}
