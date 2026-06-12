namespace Game.Preparation.Domain
{
    /// <summary>
    /// DTO для UI: одна строка списка «доступные книги». IsSelected мутируется при тоггле,
    /// чтобы view мог перерисовать строку без полного refetch'а.
    /// </summary>
    public sealed class SelectableBookItem
    {
        public string BookId { get; }
        public string Title { get; }
        public string Author { get; }
        public string Genre { get; }
        public int BasePrice { get; }
        public bool IsSelected { get; set; }

        public SelectableBookItem(string bookId, string title, string author, string genre, int basePrice, bool isSelected)
        {
            BookId = bookId;
            Title = title;
            Author = author;
            Genre = genre;
            BasePrice = basePrice;
            IsSelected = isSelected;
        }
    }
}
