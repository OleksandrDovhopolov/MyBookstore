using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using Game.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Окно Подготовки. Гибридная модель: игрок задаёт КВОТЫ по жанрам (−/+), сервис разворачивает их
    /// в конкретные книги. Открывается через UIManager.ShowAsync (см. GameplaySceneController.StartGameAsync).
    /// Confirm → закрыть окно → выезд в локацию (GameFlow). См. docs/GameFlowLoop.md.
    /// </summary>
    [Window("PreparationWindow", WindowType.Page)]
    public sealed class PreparationWindow : WindowController<PreparationWindowView>
    {
        private IPreparationSessionService _session;
        private IGameFlowService _gameFlow;
        private IPublisher<GameplayGenreBookCountsChanged> _genreCountsPublisher;

        private CancellationTokenSource _cts;
        private readonly Dictionary<string, PreparationGenreRowView> _rows = new();
        private bool _randomRunning;
        private bool _confirmRunning;
        private bool _subscribed;

        public bool IsConfirmed { get; private set; }

        [Inject]
        public void InjectServices(
            IPreparationSessionService session,
            IGameFlowService gameFlow,
            IPublisher<GameplayGenreBookCountsChanged> genreCountsPublisher = null)
        {
            _session = session;
            _gameFlow = gameFlow;
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
            var items = await _session.StartOrResumeAsync(ct);
            Render(items);
            OnStateChanged(_session.CurrentState);
            SetRandomBooksButtonInteractable(_rows.Count > 0);
        }

        private void Render(IReadOnlyList<GenreSelectionItem> items)
        {
            ClearRows();

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

            UpdateCounter();
            UpdateValidation();
            PublishGenreCounts(state);
        }

        // Прокидываем выбранные кол-ва по жанрам в HUD (GameplaySceneView._genreBookCountItems)
        // через тот же сигнал, что использует Sales. GameplaySceneController подписан и обновит счётчики.
        private void PublishGenreCounts(PreparationSessionState state)
        {
            if (_genreCountsPublisher == null || state?.GenreQuantities == null) return;

            var counts = new Dictionary<string, int>(state.GenreQuantities.Count);
            foreach (var kv in state.GenreQuantities)
                counts[kv.Key] = kv.Value;

            _genreCountsPublisher.Publish(new GameplayGenreBookCountsChanged(counts));
        }

        private void UpdateCounter()
        {
            if (_session == null) return;

            View.SetSlotCount($"{_session.TotalSelected}/{_session.Capacity.DailyBookSlots}");

            if (_session.CurrentState != null)
            {
                View.SetDay($"День {_session.CurrentState.Day}");
                View.SetLocation(_session.CurrentState.LocationId);
            }
        }

        private void UpdateValidation()
        {
            if (_session == null) return;

            var validation = _session.Validate();
            if (!validation.IsValid)
                View.SetValidation(string.Join("\n", validation.Errors));
            else if (_session.TotalSelected == 0)
                View.SetValidation("Полка пуста — посетители уйдут без покупок");
            else
                View.SetValidation(string.Empty);

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
