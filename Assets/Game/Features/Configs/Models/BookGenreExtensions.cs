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
    }
}
