using System.Collections.Generic;

namespace Game.DayCycle.Day
{
    /// <summary>
    /// Общий прогресс дня для всего core loop. Единственный источник истины по
    /// «какой сейчас день и фаза» — его читают/пишут все 4 фазы (Утро, Подготовка,
    /// Продажа, Итоги). Сохраняется как Save-модуль <see cref="DayProgressService.ModuleKey"/>.
    /// POCO: сериализуется Newtonsoft через ISaveService.UpdateModuleAsync.
    /// </summary>
    public sealed class DayProgressState
    {
        /// <summary>Текущий игровой день, 1-based.</summary>
        public int CurrentDay { get; set; } = 1;

        public DayPhase CurrentPhase { get; set; } = DayPhase.Morning;

        public int Gold { get; set; }
        public int Reputation { get; set; }

        /// <summary>
        /// Завершённые дни — для идемпотентного начисления наград в Итогах
        /// (не начислять дважды при перезапуске на экране итогов).
        /// </summary>
        public List<int> CompletedDays { get; set; } = new();

        /// <summary>
        /// Книги во владении игрока (id из books.json). Пока заглушка для прототипа,
        /// позже мигрирует в модуль Inventory.
        /// </summary>
        public List<string> OwnedBookIds { get; set; } = new();
    }
}
