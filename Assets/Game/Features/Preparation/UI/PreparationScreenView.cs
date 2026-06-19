using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.Preparation.Domain;
using Game.Preparation.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Preparation.UI
{
    /// <summary>
    /// Debug-экран Подготовки для вертикального среза core loop (uGUI). Не продакшен-окно:
    /// финальный UI приедет с модулем UI System. Логика — в IPreparationSessionService;
    /// view показывает список книг, счётчик и кнопку «Открыть лавку».
    /// Размещается inactive в GameplayScene; активируется из MorningScreenView после смены фазы.
    /// </summary>
    public sealed class PreparationScreenView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _dayLabel;
        [SerializeField] private TMP_Text _locationLabel;
        [SerializeField] private TMP_Text _slotCountLabel;
        [SerializeField] private TMP_Text _validationLabel;

        [Header("List")]
        [SerializeField] private Transform _bookListContainer;
        [SerializeField] private PreparationBookRowView _bookRowPrefab;

        [Header("Actions")]
        [SerializeField] private Button _openShopButton;
        [SerializeField] private Button _randomBooksButton;

        [Header("Next screen")]
        [SerializeField] private GameObject _salesScreenRoot;

        private IPreparationSessionService _session;
        private IDayProgressService _dayProgress;
        private readonly CancellationTokenSource _cts = new();
        private readonly List<PreparationBookRowView> _rows = new();
        private readonly List<SelectableBookItem> _items = new();
        private bool _started;
        private bool _randomSelectionRunning;

        [Inject]
        public void Construct(IPreparationSessionService session, IDayProgressService dayProgress)
        {
            _session = session;
            _dayProgress = dayProgress;
        }

        private void Awake()
        {
            if (_openShopButton != null)
                _openShopButton.onClick.AddListener(OnOpenShopClicked);

            if (_randomBooksButton != null)
                _randomBooksButton.onClick.AddListener(OnRandomBooksClicked);
        }

        private void Start()
        {
            // Гард на рестарт сцены не на нашей фазе: если CurrentPhase ≠ Preparation
            // и сцена всё-таки оставила нас активными, прячемся. Полноценный PhaseRouter — отдельная задача.
            if (_dayProgress != null && _dayProgress.Current != null
                && _dayProgress.Current.CurrentPhase != DayPhase.Preparation)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_session == null)
            {
                Debug.LogWarning("[PreparationScreenView] IPreparationSessionService не внедрён — экран не зарегистрирован в DI?");
                return;
            }

            _session.StateChanged += OnStateChanged;
            _started = true;
            RefreshAsync(_cts.Token).Forget();
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
            if (_bookRowPrefab == null || _bookListContainer == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var row = Instantiate(_bookRowPrefab, _bookListContainer);
                row.Bind(items[i], OnBookClicked);
                _rows.Add(row);
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null) Destroy(_rows[i].gameObject);
            }
            _rows.Clear();
        }

        private void OnBookClicked(string bookId)
        {
            ToggleAsync(bookId, _cts.Token).Forget();
        }

        private async UniTaskVoid ToggleAsync(string bookId, CancellationToken ct)
        {
            await _session.ToggleBookAsync(bookId, ct);
        }

        private void OnStateChanged(PreparationSessionState state)
        {
            if (state == null) return;
            var selectedSet = new HashSet<string>(state.SelectedBookIds);
            for (int i = 0; i < _rows.Count; i++)
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
            if (_slotCountLabel != null)
            {
                var count = _session.CurrentState?.SelectedBookIds.Count ?? 0;
                var max = _session.Capacity.DailyBookSlots;
                _slotCountLabel.text = $"{count}/{max}";
            }
            if (_dayLabel != null && _session.CurrentState != null)
                _dayLabel.text = $"День {_session.CurrentState.Day}";
            if (_locationLabel != null && _session.CurrentState != null)
                _locationLabel.text = _session.CurrentState.LocationId;
        }

        private void UpdateValidation()
        {
            if (_session == null) return;
            var validation = _session.Validate();
            if (_validationLabel != null)
                _validationLabel.text = validation.IsValid ? string.Empty : string.Join("\n", validation.Errors);
            SetButtonInteractable(validation.IsValid && !_randomSelectionRunning);
        }

        private void OnOpenShopClicked()
        {
            ConfirmAsync(_cts.Token).Forget();
        }

        private void OnRandomBooksClicked()
        {
            SelectRandomBooksAsync(_cts.Token).Forget();
        }

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

                for (int i = 0; i < selectedIds.Count; i++)
                {
                    await _session.ToggleBookAsync(selectedIds[i], ct);
                }

                var candidates = new List<string>(_items.Count);
                for (int i = 0; i < _items.Count; i++)
                {
                    if (!string.IsNullOrEmpty(_items[i].BookId))
                        candidates.Add(_items[i].BookId);
                }

                Shuffle(candidates);

                var targetCount = Mathf.Min(_session.Capacity.DailyBookSlots, candidates.Count);
                for (int i = 0; i < targetCount; i++)
                {
                    await _session.ToggleBookAsync(candidates[i], ct);
                }
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
            SetButtonInteractable(false);
            var ok = await _session.ConfirmAsync(ct);
            if (!ok)
            {
                UpdateValidation();
                return;
            }

            if (_salesScreenRoot != null) _salesScreenRoot.SetActive(true);
            gameObject.SetActive(false);
        }

        private void SetButtonInteractable(bool value)
        {
            if (_openShopButton != null)
                _openShopButton.interactable = value;
        }

        private void SetRandomBooksButtonInteractable(bool value)
        {
            if (_randomBooksButton != null)
                _randomBooksButton.interactable = value;
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private void OnDestroy()
        {
            if (_openShopButton != null)
                _openShopButton.onClick.RemoveListener(OnOpenShopClicked);

            if (_randomBooksButton != null)
                _randomBooksButton.onClick.RemoveListener(OnRandomBooksClicked);

            if (_started && _session != null)
                _session.StateChanged -= OnStateChanged;

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
