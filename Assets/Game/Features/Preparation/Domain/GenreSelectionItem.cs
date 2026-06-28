namespace Game.Preparation.Domain
{
    /// <summary>
    /// DTO для UI: одна строка выбора по жанру. Игрок задаёт Quantity в диапазоне [0, Available].
    /// Available — сколько непроданных книг этого жанра есть у игрока (потолок ползунка).
    /// </summary>
    public sealed class GenreSelectionItem
    {
        public string Genre { get; }
        public int Available { get; }
        public int Quantity { get; set; }

        public GenreSelectionItem(string genre, int available, int quantity)
        {
            Genre = genre;
            Available = available;
            Quantity = quantity;
        }
    }
}
