using System.Collections.Generic;
using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Изменяющееся состояние дня продажи. View рендерит из снимка после события.
    /// Не сериализуется (Save out of scope для текущей итерации).
    /// </summary>
    public sealed class SalesSessionState
    {
        public int Day { get; set; }
        public string LocationId { get; set; }

        public List<ShelfBook> Shelf { get; } = new();
        public List<RequestConfig> ActiveQueue { get; } = new();

        /// <summary>Индекс текущего активного запроса внутри <see cref="ActiveQueue"/>. -1 пока день не стартанул.</summary>
        public int CurrentRequestIndex { get; set; } = -1;

        public bool DayCompleted { get; set; }
    }
}
