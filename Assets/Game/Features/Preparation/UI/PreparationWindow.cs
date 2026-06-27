using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Configs.Models;
using Game.Newspaper.UI;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using Game.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Preparation.UI
{
    [Window("PreparationWindow", WindowType.Page)]
    public sealed class PreparationWindow : WindowController<PreparationWindowView>
    {
        private IPreparationSessionService _session;
        private IGameFlowService _gameFlow;
        private IUiSpriteProvider _uiSprites;
        private IPublisher<GameplayGenreBookCountsChanged> _genreCountsPublisher;

        private CancellationTokenSource _cts;
        private readonly Dictionary<string, PreparationGenreRowView> _rows = new();
        private IReadOnlyList<GenreSelectionItem> _items;
        private bool _randomRunning;
        private bool _confirmRunning;
        private bool _subscribed;

        public bool IsConfirmed { get; private set; }

        [Inject]
        public void InjectServices(
            IPreparationSessionService session,
            IGameFlowService gameFlow,
            IUiSpriteProvider uiSprites,
            IPublisher<GameplayGenreBookCountsChanged> genreCountsPublisher = null)
        {
            _session = session;
            _gameFlow = gameFlow;
            _uiSprites = uiSprites;
            _genreCountsPublisher = genreCountsPublisher;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.OpenShopButton != null)
                View.OpenShopButton.onClick.AddListener(OnOpenShopClicked);

            if (View.RandomBooksButton != null)
                View.RandomBooksButton.onClick.AddListener(OnRandomBooksClicked);
        }

        protected override void OnShowStart()
        {
            if (_session == null)
            {
                Debug.LogWarning("[PreparationWindow] IPreparationSessionService not injected.");
                return;
            }

            IsConfirmed = false;
            _confirmRunning = false;
            Subscribe();
            RefreshAsync(_cts.Token).Forget();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            Unsubscribe();
        }

        protected override void OnDispose()
        {
            Unsubscribe();

            if (View != null)
            {
                if (View.OpenShopButton != null)
                    View.OpenShopButton.onClick.RemoveListener(OnOpenShopClicked);
                if (View.RandomBooksButton != null)
                    View.RandomBooksButton.onClick.RemoveListener(OnRandomBooksClicked);
            }

            ClearRows();

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void Subscribe()
        {
            if (_subscribed || _session == null) return;
            _session.StateChanged += OnStateChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _session == null) return;
            _session.StateChanged -= OnStateChanged;
            _subscribed = false;
        }

        private async UniTaskVoid RefreshAsync(CancellationToken ct)
        {
            SetButtonInteractable(false);
            SetRandomBooksButtonInteractable(false);
            var locationId = (Arguments as PreparationWindowArgs)?.LocationId;
            var items = await _session.StartOrResumeAsync(ct, locationId);
            Render(items);
            OnStateChanged(_session.CurrentState);
            SetRandomBooksButtonInteractable(_rows.Count > 0);
            LoadGenreIconsAsync(ct).Forget();
        }

        // Грузим иконки жанров по id (= имя жанра) через общий кэширующий провайдер и раздаём строкам.
        private async UniTaskVoid LoadGenreIconsAsync(CancellationToken ct)
        {
            if (_uiSprites == null) return;

            // Снимок: _rows может быть пересоздан/очищен (ClearRows) пока мы ждём загрузку.
            var rows = _rows.Values.ToList();
            try
            {
                foreach (var row in rows)
                {
                    if (row == null) continue;

                    var sprite = await _uiSprites.GetSpriteAsync(row.Genre, ct);
                    if (ct.IsCancellationRequested) return;
                    if (row != null) row.SetIcon(sprite);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        private void Render(IReadOnlyList<GenreSelectionItem> items)
        {
            ClearRows();
            _items = items;

            var container = View.GenreListContainer;
            var prefab = View.GenreRowPrefab;
            if (prefab == null || container == null) return;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var row = Object.Instantiate(prefab, container);
                row.Bind(item, OnSetGenreQuantity);
                _rows[item.Genre] = row;
            }
        }

        private void ClearRows()
        {
            foreach (var row in _rows.Values)
                if (row != null) Object.Destroy(row.gameObject);
            _rows.Clear();
        }

        private void OnSetGenreQuantity(string genre, int quantity)
            => SetGenreQuantityAsync(genre, quantity, _cts.Token).Forget();

        private async UniTaskVoid SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct)
        {
            await _session.SetGenreQuantityAsync(genre, quantity, ct);
        }

        private void OnStateChanged(PreparationSessionState state)
        {
            if (state == null) return;

            var canAddMore = _session.TotalSelected < _session.Capacity.DailyBookSlots;
            foreach (var pair in _rows)
            {
                if (pair.Value == null) continue;
                state.GenreQuantities.TryGetValue(pair.Key, out var qty);
                pair.Value.SetState(qty, canAddMore);
            }

            UpdateShelfPreview(state);
            UpdateCounter();
            UpdateValidation();
            PublishGenreCounts(state);
        }

        private void UpdateShelfPreview(PreparationSessionState state)
        {
            if (_items == null) return;

            // Keep the ordered items in sync with the authoritative quotas, then render the bar from them.
            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null) continue;
                state.GenreQuantities.TryGetValue(item.Genre, out var qty);
                item.Quantity = qty;
            }

            View.RenderShelfPreview(_items, OnShelfSegmentClicked);
        }

        private void OnShelfSegmentClicked(string genre)
        {
            var state = _session?.CurrentState;
            if (state == null) return;

            state.GenreQuantities.TryGetValue(genre, out var qty);
            if (qty <= 0) return;

            OnSetGenreQuantity(genre, qty - 1);
        }

        // Прокидываем выбранные кол-ва по жанрам в HUD через тот же сигнал, что использует Sales.
        private void PublishGenreCounts(PreparationSessionState state)
        {
            if (_genreCountsPublisher == null || state?.GenreQuantities == null) return;

            _genreCountsPublisher.Publish(
                new GameplayGenreBookCountsChanged(BookGenreCounts.Normalize(state.GenreQuantities)));
        }

        private void UpdateCounter()
        {
            if (_session == null) return;

            View.SetSlotCount($"{_session.TotalSelected}/{_session.Capacity.DailyBookSlots}");

            // Prefer the human-readable name passed via args; fall back to the session's location id.
            var locationText = (Arguments as PreparationWindowArgs)?.DisplayName
                               ?? _session.CurrentState?.LocationId;
            if (!string.IsNullOrEmpty(locationText))
                View.SetLocation(locationText);
        }

        protected override void UpdateWindow() => UpdateCounter();

        private void UpdateValidation()
        {
            if (_session == null) return;

            var validation = _session.Validate();
            if (!validation.IsValid)
            {
                Debug.LogWarning(string.Join("\n", validation.Errors));
            }
            else if (_session.TotalSelected == 0)
            {
                Debug.LogWarning($"Shelf is empty - clients will leave");
            }

            SetButtonInteractable(validation.IsValid && !_randomRunning);
        }

        private void OnOpenShopClicked() => ConfirmAsync(_cts.Token).Forget();

        private void OnRandomBooksClicked() => RandomizeAsync(_cts.Token).Forget();

        private async UniTaskVoid RandomizeAsync(CancellationToken ct)
        {
            if (_session == null || _rows.Count == 0 || _randomRunning) return;

            _randomRunning = true;
            SetRandomBooksButtonInteractable(false);
            SetButtonInteractable(false);
            try
            {
                await _session.RandomizeAsync(ct); // StateChanged обновит строки/счётчик
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                _randomRunning = false;
                SetRandomBooksButtonInteractable(true);
                UpdateValidation();
            }
        }

        private async UniTaskVoid ConfirmAsync(CancellationToken ct)
        {
            if (_confirmRunning) return;

            _confirmRunning = true;
            var closed = false;

            SetButtonInteractable(false);
            try
            {
                if (_gameFlow == null)
                {
                    Debug.LogWarning("[PreparationWindow] IGameFlowService not injected — cannot enter location.");
                    return;
                }

                var ok = await _session.ConfirmAsync(ct);
                if (!ok)
                {
                    UpdateValidation();
                    return;
                }

                // Окно уничтожится при закрытии — захватываем GameFlow в локаль до await.
                var gameFlow = _gameFlow;
                IsConfirmed = true;

                await CloseAsync(CancellationToken.None);
                closed = true;

                try
                {
                    await gameFlow.EnterLocationAsync(CancellationToken.None);
                }
                catch (System.OperationCanceledException)
                {
                    // Application shutdown / editor stop.
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PreparationWindow] EnterLocationAsync failed after confirm: {e}");
                    await ReopenAfterTransitionFailureAsync();
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (!closed)
                {
                    IsConfirmed = false;
                    _confirmRunning = false;
                    UpdateValidation();
                }
            }
        }

        private async UniTask ReopenAfterTransitionFailureAsync()
        {
            if (UIManager == null) return;

            try
            {
                await UIManager.ShowAsync<PreparationWindow>(ct: CancellationToken.None);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PreparationWindow] Failed to reopen after transition failure: {e}");
            }
        }

        private void SetButtonInteractable(bool value)
        {
            if (View.OpenShopButton != null)
                View.OpenShopButton.interactable = value;
        }

        private void SetRandomBooksButtonInteractable(bool value)
        {
            if (View.RandomBooksButton != null)
                View.RandomBooksButton.interactable = value;
        }
    }
}
