using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs;
using Game.DayCycle.Results.UI;
using Game.Configs.Models;
using Game.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Book.Sell.UI
{
    public sealed class SalesScreenView : MonoBehaviour
    {
        private bool _dayRunning;
        private readonly CancellationTokenSource _cts = new();

        private IUIManager _uiManager;
        private IConfigsService _configs;
        private IGameFlowService _gameFlow;
        private ISalesDayController _controller;
        private ICurrentDayProvider _dayProvider;
        private ISalesShelfStateService _shelfState;
        private IRecommendationMinigamePresenter _minigamePresenter;
        
        private IDisposable _genreBookCountsRequestSubscription;
        
        private IPublisher<GameplaySalesGoldChanged> _salesGoldPublisher;
        private IPublisher<GameplayGenreBookCountsChanged> _genreBookCountsPublisher;
        private IPublisher<GameplaySceneButtonsInteractableChanged> _gameplayButtonsPublisher;
        
        [Inject]
        public void Construct(
            ISalesDayController controller,
            ICurrentDayProvider dayProvider = null,
            IUIManager uiManager = null,
            IGameFlowService gameFlow = null,
            IRecommendationMinigamePresenter minigamePresenter = null,
            ISalesShelfStateService shelfState = null,
            IConfigsService configs = null,
            IPublisher<GameplaySceneButtonsInteractableChanged> gameplayButtonsPublisher = null,
            IPublisher<GameplayGenreBookCountsChanged> genreBookCountsPublisher = null,
            IPublisher<GameplaySalesGoldChanged> salesGoldPublisher = null,
            ISubscriber<GameplayGenreBookCountsRequested> genreBookCountsRequestSubscriber = null)
        {
            _controller = controller;
            _dayProvider = dayProvider;
            _uiManager = uiManager;
            _gameFlow = gameFlow;
            _minigamePresenter = minigamePresenter;
            _shelfState = shelfState;
            _configs = configs;
            _gameplayButtonsPublisher = gameplayButtonsPublisher;
            _genreBookCountsPublisher = genreBookCountsPublisher;
            _salesGoldPublisher = salesGoldPublisher;
            _genreBookCountsRequestSubscription = genreBookCountsRequestSubscriber?.Subscribe(_ => PublishGenreBookCounts());
        }

        private void Start()
        {
            if (_controller == null)
            {
                Debug.LogWarning("[SalesScreenView] ISalesDayController was not injected — the screen is inactive.");
                return;
            }

            // On restart, the player may have already completed today's sales but not yet pressed
            // Next Day. In that case, skip Sales entirely and hand straight to Results.
            if (_dayProvider is { IsCurrentDayCompleted: true })
            {
                ShowResultsWindowAsync().Forget();
                return;
            }

            _controller.DayReadyToClose += OnDayReadyToClose;
            _controller.DayCompleted += OnDayCompleted;
            _controller.ShelfChanged += OnShelfChanged;

            StartDayFlowAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            // The day is paused while the recommendation minigame window is open (owned by the presenter).
            if (!_dayRunning || _controller == null || (_minigamePresenter?.IsWindowOpen ?? false)) return;
            _controller.Tick(Time.deltaTime);
            RefreshHeader();
        }

        private async UniTaskVoid StartDayFlowAsync(CancellationToken ct)
        {
            // Day comes from DayCycle.DayProgressService via the ICurrentDayProvider adapter.
            // When the adapter is not registered (e.g. early prototype scenes), fall back to day 1.
            var day = _dayProvider?.CurrentDay ?? 1;
            PublishSalesGold(0, true);
            await _controller.StartDayAsync(day, ct);
            RefreshHeader();
            PublishGenreBookCounts();
            _dayRunning = !_controller.IsDayCompleted;
            SetGameplaySceneButtonsInteractable(!_dayRunning);
        }

        // ---------- controller events ----------


        private void OnShelfChanged()
        {
            PublishGenreBookCounts();
        }

        private void PublishGenreBookCounts()
        {
            _genreBookCountsPublisher?.Publish(new GameplayGenreBookCountsChanged(BuildGenreBookCounts()));
        }

        private void OnDayReadyToClose()
        {
            _dayRunning = false;
            RefreshHeader();
            _controller?.ConcludeDay();
        }

        private void OnDayCompleted(SalesDayResult result)
        {
            _dayRunning = false;
            SetGameplaySceneButtonsInteractable(true);

            Debug.Log($"[SalesScreenView] DayCompleted: day={result.Day}, customers={result.CustomersServed}, " +
                      $"sales={result.SalesCount}, gold={result.GoldEarned}, " +
                      $"excellent={result.ExcellentCount}, normal={result.NormalCount}, " +
                      $"failed={result.FailedCount}, skipped={result.SkippedCount}");

            if (_gameFlow != null)
            {
                ReturnToHubAndShowResultsAsync(_gameFlow, _uiManager).Forget();
            }
            else
            {
                ShowResultsWindowAsync().Forget();
            }
        }

        private static async UniTaskVoid ReturnToHubAndShowResultsAsync(IGameFlowService gameFlow, IUIManager ui)
        {
            await gameFlow.ReturnToHubAsync(CancellationToken.None);
            if (ui != null)
                await ui.ShowAsync<ResultsWindow>(ct: CancellationToken.None);
            else
                Debug.LogError("[SalesScreenView] IUIManager was not injected - cannot open ResultsWindow.");
        }

        private async UniTaskVoid ShowResultsWindowAsync()
        {
            if (_uiManager == null)
            {
                Debug.LogError("[SalesScreenView] IUIManager was not injected - cannot open ResultsWindow.");
                return;
            }

            var window = await _uiManager.ShowAsync<ResultsWindow>(ct: _cts.Token);
            if (window != null)
                gameObject.SetActive(false);
        }
        
        private void RefreshHeader()
        {
            var result = _controller.AccumulatedResult;
            PublishSalesGold(result.GoldEarned, true);
        }

        private void SetGameplaySceneButtonsInteractable(bool interactable)
        {
            _gameplayButtonsPublisher?.Publish(new GameplaySceneButtonsInteractableChanged(interactable));
        }

        private void PublishSalesGold(int goldEarned, bool visible)
        {
            _salesGoldPublisher?.Publish(new GameplaySalesGoldChanged(goldEarned, visible));
        }

        private Dictionary<string, int> BuildGenreBookCounts()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var books = _controller?.Shelf?.Books;
            if (books == null || books.Count == 0)
                return BuildPersistentGenreBookCounts();

            for (var i = 0; i < books.Count; i++)
            {
                var book = books[i];
                if (book == null || book.State != ShelfBookState.Available) continue;

                var genre = book.Config?.Genre;
                if (string.IsNullOrEmpty(genre)) continue;

                counts.TryGetValue(genre, out var count);
                counts[genre] = count + 1;
            }

            return BookGenreCounts.Normalize(counts);
        }

        private Dictionary<string, int> BuildPersistentGenreBookCounts()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var bookIds = _shelfState?.ShelfBookIds;
            if (bookIds == null || _configs == null) return counts;

            for (var i = 0; i < bookIds.Count; i++)
            {
                var bookId = bookIds[i];
                if (string.IsNullOrEmpty(bookId) || _shelfState.IsSold(bookId)) continue;
                if (!_configs.TryGet<BookConfig>(bookId, out var book) || book == null) continue;

                var genre = book.Genre;
                if (string.IsNullOrEmpty(genre)) continue;

                counts.TryGetValue(genre, out var count);
                counts[genre] = count + 1;
            }

            return BookGenreCounts.Normalize(counts);
        }

        private void OnDestroy()
        {
            SetGameplaySceneButtonsInteractable(true);
            PublishSalesGold(0, false);
            _genreBookCountsRequestSubscription?.Dispose();
            _genreBookCountsRequestSubscription = null;

            if (_controller != null)
            {
                _controller.DayReadyToClose -= OnDayReadyToClose;
                _controller.DayCompleted -= OnDayCompleted;
                _controller.ShelfChanged -= OnShelfChanged;
            }

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
