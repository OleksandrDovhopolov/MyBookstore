namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг книги. Расширен под scoring-систему продаж (Tags + Mood).
    /// Файл: books.json (JSON-массив).
    /// </summary>
    [ConfigFile("books")]
    public sealed class BookConfig : IConfig
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }

        /// <summary>Основной жанр книги: sci-fi / mystery / romance / classic / nonfiction / ...</summary>
        public string Genre { get; set; }

        public int BasePrice { get; set; }
        public float RarityWeight { get; set; }
        public int Published { get; set; }
        public int Pages { get; set; }

        /// <summary>Темы/теги книги (survival, space, study, cozy, history, ...). Совпадение с запросом — +2 каждый.</summary>
        public string[] Tags { get; set; }

        /// <summary>Тон/настроение (smart, tense, cozy, romantic, dark, optimistic, ...). Совпадение — +1 каждый.</summary>
        public string[] Mood { get; set; }
    }
}
