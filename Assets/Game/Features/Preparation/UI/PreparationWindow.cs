using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Окно Подготовки (выбор книг на день). Заменяет прежний scene-MonoBehaviour PreparationScreenView.
    /// Открывается через UIManager.ShowAsync (см. GameplaySceneController.StartGameAsync). Вся логика здесь;
    /// view (PreparationWindowView) — только ссылки на UI. Confirm → закрыть окно → выезд в локацию (GameFlow).
    /// </summary>
    [Window("PreparationWindow", WindowType.Page)]
    public sealed class PreparationWindow : WindowController<PreparationWindowView>
    {
        private IPreparationSessionService _session;
        private IGameFlowService _gameFlow;

        private CancellationTokenSource _cts;
        private readonly List<PreparationBookRowView> _rows = new();
        private readonly List<SelectableBookItem> _items = new();
        private bool _randomSelectionRunning;
        private bool _confirmRunning;
        private bool _subscribed;

        public bool IsConfirmed { get; private set; }

        [Inject]
        public void InjectServices(IPreparationSessionService session, IGameFlowService gameFlow)
        {
            _session = session;
            _gameFlow = gameFlow;
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
            UpdateCounter();
            UpdateValidation();
            SetRandomBooksButtonInteractable(_items.Count > 0);
        }

        private void Render(IReadOnlyList<SelectableBookItem> items)
        {
            ClearRows();
            _items.Clear();
            _items.AddRange(items);

            var container = View.BookListContainer;
            var prefab = View.BookRowPrefab;
            if (prefab == null || container == null) return;

            for (var i = 0; i < items.Count; i++)
            {
                var row = Object.Instantiate(prefab, container);
                row.Bind(items[i], OnBookClicked);
                _rows.Add(row);
            }
        }

        private void ClearRows()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Object.Destroy(_rows[i].gameObject);
            }
            _rows.Clear();
        }

        private void OnBookClicked(string bookId) => ToggleAsync(bookId, _cts.Token).Forget();

        private async UniTaskVoid ToggleAsync(string bookId, CancellationToken ct)
        {
            await _session.ToggleBookAsync(bookId, ct);
        }

        private void OnStateChanged(PreparationSessionState state)
        {
            if (state == null) return;
            var selectedSet = new HashSet<string>(state.SelectedBookIds);
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                row.SetSelected(selectedSet.Contains(row.BookId));
            }
            UpdateCounter();
            UpdateValidation();
        }

        private void UpdateCounter()
        {
            if (_session == null) return;

            var count = _session.CurrentState?.SelectedBookIds.Count ?? 0;
            var max = _session.Capacity.DailyBookSlots;
            View.SetSlotCount($"{count}/{max}");

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
            else if (_session.CurrentState != null && _session.CurrentState.SelectedBookIds.Count == 0)
                View.SetValidation("Полка пуста — посетители уйдут без покупок");
            else
                View.SetValidation(string.Empty);

            SetButtonInteractable(validation.IsValid && !_randomSelectionRunning);
        }

        private void OnOpenShopClicked() => ConfirmAsync(_cts.Token).Forget();

        private void OnRandomBooksClicked() => SelectRandomBooksAsync(_cts.Token).Forget();

        private async UniTaskVoid SelectRandomBooksAsync(CancellationToken ct)
        {
            if (_session == null || _items.Count == 0) return;

            _randomSelectionRunning = true;
            SetRandomBooksButtonInteractable(false);
            SetButtonInteractable(false);

            try
            {
                var selectedIds = _session.CurrentState?.SelectedBookIds != null
                    ? new List<string>(_session.CurrentState.SelectedBookIds)
                    : new List<string>();

                for (var i = 0; i < selectedIds.Count; i++)
                    await _session.ToggleBookAsync(selectedIds[i], ct);

                var candidates = new List<string>(_items.Count);
                for (var i = 0; i < _items.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_items[i].BookId))
                        candidates.Add(_items[i].BookId);
                }

                Shuffle(candidates);

                var targetCount = Mathf.Min(_session.Capacity.DailyBookSlots, candidates.Count);
                for (var i = 0; i < targetCount; i++)
                    await _session.ToggleBookAsync(candidates[i], ct);
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                _randomSelectionRunning = false;
                SetRandomBooksButtonInteractable(true);
                UpdateCounter();
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

        private static void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
