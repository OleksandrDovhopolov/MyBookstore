using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Preparation.Domain;
using Save;
using UnityEngine;

namespace Game.Preparation.Services
{
    /// <inheritdoc cref="IPreparationSessionService"/>
    public sealed class PreparationSessionService : IPreparationSessionService
    {
        private const string LogPrefix = "[Preparation.Session]";

        // MVP: capacity захардкожена временно. По спеке (docs/INPROGRESS/Подготовка.md, раздел "Лимиты")
        // dailyBookSlots — это параметр состояния игрока (CurrentBookCapacity), который растёт через улучшения
        // лавки (диапазон 12–20). Миграция в EconomyConfig / DayProgressState — отдельная задача.
        // Зафиксировано в docs/INPROGRESS/CORE_LOOP_STATUS.md → "Известные ограничения".
        private const int DefaultMinDailyBooks = 1;
        private const int DefaultDailyBookSlots = 12;
        private const string DefaultLocationId = "loc_downtown";

        private readonly ISaveService _save;
        private readonly IDayProgressService _dayProgress;
        private readonly IPreparationInventoryProvider _inventory;

        private PreparationSessionState _state;

        public PreparationSessionService(
            ISaveService save,
            IDayProgressService dayProgress,
            IPreparationInventoryProvider inventory)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            Capacity = new PreparationCapacity(DefaultMinDailyBooks, DefaultDailyBookSlots);
        }

        public PreparationCapacity Capacity { get; }

        public PreparationSessionState CurrentState => _state;

        public event Action<PreparationSessionState> StateChanged;

        public async UniTask<IReadOnlyList<SelectableBookItem>> StartOrResumeAsync(CancellationToken ct)
        {
            var saved = await _save.GetModuleAsync<PreparationSessionState>(PreparationSaveKeys.Session, ct);
            var currentDay = _dayProgress.Current.CurrentDay;

            var needsFresh = saved == null || saved.Day != currentDay || saved.Confirmed;
            if (needsFresh)
            {
                _state = new PreparationSessionState
                {
                    Day = currentDay,
                    LocationId = DefaultLocationId,
                    SelectedBookIds = new List<string>(),
                    SelectedDecorIds = new List<string>(),
                    Confirmed = false
                };
                await _save.UpdateModuleAsync(PreparationSaveKeys.Session, _state, PreparationSaveKeys.SessionSchemaVersion, ct);
            }
            else
            {
                _state = saved;
            }

            if (_dayProgress.Current.CurrentPhase != DayPhase.Preparation)
                await _dayProgress.SetPhaseAsync(DayPhase.Preparation, ct);

            return BuildSelectableItems(_inventory.GetOwnedBooks(), _state.SelectedBookIds);
        }

        public async UniTask ToggleBookAsync(string bookId, CancellationToken ct)
        {
            if (_state == null)
            {
                Debug.LogWarning($"{LogPrefix} ToggleBookAsync вызван до StartOrResumeAsync.");
                return;
            }

            if (string.IsNullOrEmpty(bookId)) return;

            if (_state.SelectedBookIds.Remove(bookId))
            {
                // Был выбран — сняли.
            }
            else
            {
                if (_state.SelectedBookIds.Count >= Capacity.DailyBookSlots)
                {
                    Debug.LogWarning($"{LogPrefix} достигнут лимит {Capacity.DailyBookSlots} книг — '{bookId}' не добавлен.");
                    return;
                }
                _state.SelectedBookIds.Add(bookId);
            }

            await _save.UpdateModuleAsync(PreparationSaveKeys.Session, _state, PreparationSaveKeys.SessionSchemaVersion, ct);
            StateChanged?.Invoke(_state);
        }

        public PreparationValidationResult Validate()
        {
            if (_state == null)
                return PreparationValidationResult.Fail("Session not started");

            if (_state.SelectedBookIds.Count < Capacity.MinDailyBooks)
                return PreparationValidationResult.Fail($"Выберите хотя бы {Capacity.MinDailyBooks} книгу");

            return PreparationValidationResult.Ok();
        }

        public async UniTask<bool> ConfirmAsync(CancellationToken ct)
        {
            var validation = Validate();
            if (!validation.IsValid)
            {
                Debug.LogWarning($"{LogPrefix} Confirm отклонён: {string.Join("; ", validation.Errors)}");
                return false;
            }

            _state.Confirmed = true;
            await _save.UpdateModuleAsync(PreparationSaveKeys.Session, _state, PreparationSaveKeys.SessionSchemaVersion, ct);
            await _dayProgress.SetPhaseAsync(DayPhase.Sales, ct);

            Debug.Log($"{LogPrefix} day={_state.Day} location={_state.LocationId} shelf={_state.SelectedBookIds.Count} → Sales.");
            return true;
        }

        private static IReadOnlyList<SelectableBookItem> BuildSelectableItems(IReadOnlyList<BookConfig> books, IReadOnlyList<string> selectedIds)
        {
            var selectedSet = new HashSet<string>(selectedIds);
            var items = new List<SelectableBookItem>(books.Count);
            foreach (var book in books)
            {
                items.Add(new SelectableBookItem(
                    book.Id,
                    book.Title,
                    book.Author,
                    book.Genre,
                    book.BasePrice,
                    selectedSet.Contains(book.Id)));
            }
            return items;
        }
    }
}
