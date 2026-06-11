using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// A book sitting on the daily shelf. Snapshot of <see cref="BookConfig"/> + a runtime state
    /// flag (Available / SoldOut). One copy per book for the MVP: after a sale (active or passive)
    /// the book is SoldOut for the rest of the day.
    /// </summary>
    public sealed class ShelfBook
    {
        public string BookId { get; }
        public BookConfig Config { get; }
        public ShelfBookState State { get; set; }

        public ShelfBook(BookConfig config)
        {
            BookId = config.Id;
            Config = config;
            State = ShelfBookState.Available;
        }
    }
}
