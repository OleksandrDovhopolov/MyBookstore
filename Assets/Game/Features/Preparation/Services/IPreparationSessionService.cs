using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Preparation.Domain;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Управляет состоянием фазы «Подготовка»: загрузка/создание сессии, тоггл книг,
    /// валидация выбора и подтверждение перехода в Sales. Аналог
    /// <c>IMorningSessionService</c> / <c>IResultsSessionService</c>.
    /// </summary>
    public interface IPreparationSessionService
    {
        PreparationCapacity Capacity { get; }
        PreparationSessionState CurrentState { get; }

        /// <summary>Срабатывает после ToggleBookAsync — view перерисовывает счётчик и кнопку.</summary>
        event Action<PreparationSessionState> StateChanged;

        /// <summary>
        /// Поднимает state из Save или создаёт новый, гарантирует фазу Preparation,
        /// возвращает список «доступных» книг с пометкой IsSelected.
        /// </summary>
        UniTask<IReadOnlyList<SelectableBookItem>> StartOrResumeAsync(CancellationToken ct);

        UniTask ToggleBookAsync(string bookId, CancellationToken ct);

        PreparationValidationResult Validate();

        /// <summary>
        /// Подтверждает выбор и переключает фазу на Sales. Возвращает false,
        /// если Validate() не прошёл — экран остаётся открытым.
        /// </summary>
        UniTask<bool> ConfirmAsync(CancellationToken ct);
    }
}
