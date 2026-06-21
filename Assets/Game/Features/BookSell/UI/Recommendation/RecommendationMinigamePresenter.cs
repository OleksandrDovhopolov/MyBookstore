using System;
using System.Threading;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.UI;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.UI
{
    /// <summary>
    /// Owns the lifecycle of <see cref="RecommendationMinigameWindow"/>: listens for active requests on
    /// <see cref="ISalesDayController"/>, opens the window (handing it the gameplay-scoped controller via
    /// <see cref="RecommendationMinigameArgs"/>, since the bootstrap-scoped window factory cannot inject it),
    /// and exposes <see cref="IsWindowOpen"/> so <see cref="SalesScreenView"/> can pause the day while it is up.
    /// Extracted from SalesScreenView so the screen only pumps the sim and renders the header/log.
    /// </summary>
    public sealed class RecommendationMinigamePresenter : IRecommendationMinigamePresenter, IStartable, IDisposable
    {
        private readonly ISalesDayController _controller;
        private readonly IUIManager _uiManager;
        private readonly CancellationTokenSource _cts = new();

        public bool IsWindowOpen { get; private set; }

        public RecommendationMinigamePresenter(ISalesDayController controller, IUIManager uiManager = null)
        {
            _controller = controller;
            _uiManager = uiManager;
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

            _cts.Cancel();
            _cts.Dispose();
        }

        private void OnActiveRequestStarted(RequestConfig req)
        {
            // Pause the day synchronously (so the very next Update skips Tick), then open the window.
            IsWindowOpen = true;
            OpenWindowAsync().Forget();
        }

        private async UniTaskVoid OpenWindowAsync()
        {
            if (_uiManager == null)
            {
                Debug.LogWarning("[RecommendationMinigamePresenter] IUIManager was not injected — cannot open the recommendation minigame window.");
                IsWindowOpen = false;   // don't leave the day paused with no UI to resolve the request
                return;
            }

            try
            {
                var window = await _uiManager.ShowAsync<RecommendationMinigameWindow>(
                    new RecommendationMinigameArgs(_controller), _cts.Token);

                if (window != null)
                    window.Closed += OnWindowClosed;
                else
                    IsWindowOpen = false;
            }
            catch (OperationCanceledException)
            {
                IsWindowOpen = false;
            }
        }

        private void OnWindowClosed(IWindowController _)
        {
            // Window is disposed after close; resume pumping the day.
            IsWindowOpen = false;
        }
    }
}
