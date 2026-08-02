using System;
using Newtonsoft.Json;

namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг книги. Цена больше не хранится в контенте: все книги продаются за <see cref="FixedPriceGold"/>.
    /// Файл: books_converted.json (JSON-массив). Legacy books.json remains in Assets/Configs but is not
    /// used by the typed runtime mapping.
    /// </summary>
    [ConfigFile("books_converted")]
    public sealed class BookConfig : IConfig
    {
        public const int FixedPriceGold = 10;
        private const string FemaleAuthorQuality = "Female Author";

        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }

        /// <summary>Жанры книги. Текущие legacy-системы используют первый жанр как основной.</summary>
        public string[] Genres { get; set; }

        public float RarityWeight { get; set; } = 0.5f;
        public int Published { get; set; }
        public int Pages { get; set; }
        [JsonProperty("fakeOrReal")]
        public string FakeOrReal { get; set; }

        /// <summary>Уникальные качества книги. Временно содержит legacy-значения из старого поля tags.</summary>
        public string[] Qualities { get; set; }

        public string PrimaryGenre => Genres != null && Genres.Length > 0 ? Genres[0] : null;

        //TODO do not calculate every request
        public bool IsFemaleAuthor
        {
            get
            {
                if (Qualities == null) return false;
                for (var i = 0; i < Qualities.Length; i++)
                {
                    if (string.Equals(Qualities[i], FemaleAuthorQuality, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }
    }
}
