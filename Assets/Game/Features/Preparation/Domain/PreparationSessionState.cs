using System.Collections.Generic;

namespace Game.Preparation.Domain
{
    /// <summary>
    /// Сохраняемое состояние фазы «Подготовка»: какой день, какая локация,
    /// какие книги положены на полку и подтверждена ли подготовка.
    /// POCO: сериализуется Newtonsoft через ISaveService.UpdateModuleAsync
    /// под ключом <c>PreparationSaveKeys.Session</c>.
    /// </summary>
    public sealed class PreparationSessionState
    {
        public int Day { get; set; } = 1;

        /// <summary>В MVP захардкожено loc_downtown — выбор локации откроется в следующей итерации.</summary>
        public string LocationId { get; set; } = "loc_downtown";

        /// <summary>
        /// Намерение игрока: сколько книг каждого жанра выставить на полку (genre → count).
        /// Источник правды для UI. Конкретные книги резолвятся из этих квот в SelectedBookIds.
        /// </summary>
        public Dictionary<string, int> GenreQuantities { get; set; } = new();

        /// <summary>
        /// Резолв квот в конкретные id книг (выход). Downstream (Sales/shelf/scoring) читает именно это.
        /// </summary>
        public List<string> SelectedBookIds { get; set; } = new();

        /// <summary>В MVP всегда пусто — декор подключается в задаче baseSaleChance.</summary>
        public List<string> SelectedDecorIds { get; set; } = new();

        /// <summary>true после ConfirmAsync — гард, чтобы StartOrResumeAsync создал новый state на следующий заход.</summary>
        public bool Confirmed { get; set; }
    }
}
