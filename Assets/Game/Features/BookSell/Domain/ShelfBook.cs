using Game.Configs.Models;

namespace Book.Sell.Domain
{
    /// <summary>
    /// Книга на дневной полке. Снимок <see cref="BookConfig"/> + состояние (Available/SoldOut).
    /// Один экземпляр на книгу в рамках MVP: после продажи (активной или пассивной) — SoldOut до конца дня.
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
