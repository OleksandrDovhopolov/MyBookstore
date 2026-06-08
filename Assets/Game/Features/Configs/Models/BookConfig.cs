namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг книги. Поля иллюстративны — финальный набор выравнивается
    /// с Notion-задачей «Data-driven конфиги». Файл: books.json (JSON-массив).
    /// </summary>
    [ConfigFile("books")]
    public sealed class BookConfig : IConfig
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Genre { get; set; }
        public int BasePrice { get; set; }
        public float RarityWeight { get; set; }
    }
}
