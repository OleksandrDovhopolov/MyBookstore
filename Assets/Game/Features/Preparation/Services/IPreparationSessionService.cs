using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Preparation.Domain;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Управляет фазой «Подготовка». Гибридная модель: игрок задаёт КВОТЫ по жанрам, сервис
    /// авто-разворачивает их в конкретные SelectedBookIds (непроданные с прошлой полки → добор случайно).
    /// Downstream (Sales/shelf/scoring) читает SelectedBookIds. См. docs/GameFlowLoop.md.
    /// </summary>
    public interface IPreparationSessionService
    {
        PreparationCapacity Capacity { get; }
        PreparationSessionState CurrentState { get; }

        /// <summary>Срабатывает после изменения квот — view перерисовывает строки/счётчик/кнопку.</summary>
        event Action<PreparationSessionState> StateChanged;

        /// <summary>Суммарно выбрано книг (= сумма квот = SelectedBookIds.Count).</summary>
        int TotalSelected { get; }

        /// <summary>
        /// Поднимает state из Save или создаёт новый, гарантирует фазу Preparation,
        /// возвращает по одному GenreSelectionItem на доступный жанр (с текущей квотой).
        /// </summary>
        UniTask<IReadOnlyList<GenreSelectionItem>> StartOrResumeAsync(CancellationToken ct);

        /// <summary>Задаёт квоту жанра (клампится в [0, available] и под общий лимит DailyBookSlots).</summary>
        UniTask SetGenreQuantityAsync(string genre, int quantity, CancellationToken ct);

        /// <summary>Случайно заполняет квоты до лимита (кнопка Random).</summary>
        UniTask RandomizeAsync(CancellationToken ct);

        PreparationValidationResult Validate();

        /// <summary>
        /// Подтверждает выбор и переключает фазу на Sales. Возвращает false, если Validate() не прошёл.
        /// </summary>
        UniTask<bool> ConfirmAsync(CancellationToken ct);
    }
}
