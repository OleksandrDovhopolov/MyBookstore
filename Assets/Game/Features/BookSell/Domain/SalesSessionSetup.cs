using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// «Что игрок принёс в день»: какая локация и какие книги на полке.
    /// Когда появится фаза Подготовки — придёт оттуда. Сейчас собирается
    /// fallback-провайдером из первой локации + первых N книг.
    /// </summary>
    public sealed class SalesSessionSetup
    {
        public int Day { get; }
        public string LocationId { get; }
        public IReadOnlyList<string> ShelfBookIds { get; }

        /// <summary>Декор сейчас не влияет на scoring (out of scope), но поле сохраняется для будущей интеграции.</summary>
        public IReadOnlyList<string> DecorIds { get; }

        public SalesSessionSetup(int day, string locationId, IReadOnlyList<string> shelfBookIds, IReadOnlyList<string> decorIds = null)
        {
            Day = day;
            LocationId = locationId;
            ShelfBookIds = shelfBookIds ?? System.Array.Empty<string>();
            DecorIds = decorIds ?? System.Array.Empty<string>();
        }
    }
}
