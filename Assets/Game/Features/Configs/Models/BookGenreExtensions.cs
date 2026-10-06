using System;

namespace Game.Configs.Models
{
    public static class BookGenreExtensions
    {
        public static bool TryParseGenre(string value, out BookGenre genre)
        {
            genre = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Enum.TryParse(value, ignoreCase: true, out genre)
                && Enum.IsDefined(typeof(BookGenre), genre);
        }

        public static BookGenre ParseGenre(string value)
        {
            if (TryParseGenre(value, out var genre))
                return genre;

            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown book genre.");
        }

        public static string ToConfigValue(this BookGenre genre) => genre.ToString();

        /// <summary>
        /// Localization key for the inventory hint of a book genre row. Genre rows are aggregates
        /// built in code (see BookGenreRowSource), so there is no config entry to author the key on.
        /// Lowercase to match the dotted-lowercase convention; lookups are case-sensitive.
        /// </summary>
        public static string ToDescriptionLocalizationKey(this BookGenre genre)
            => $"book_genre.{genre.ToConfigValue().ToLowerInvariant()}.desc";
    }
}
