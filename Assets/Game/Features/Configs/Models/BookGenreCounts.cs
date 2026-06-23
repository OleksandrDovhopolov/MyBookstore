using System.Collections.Generic;

namespace Game.Configs.Models
{
    public static class BookGenreCounts
    {
        public static Dictionary<string, int> Normalize(IReadOnlyDictionary<string, int> counts)
        {
            var normalized = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

            foreach (BookGenre genre in System.Enum.GetValues(typeof(BookGenre)))
            {
                var genreId = genre.ToConfigValue();
                normalized[genreId] = 0;
            }

            if (counts == null)
                return normalized;

            foreach (var pair in counts)
            {
                if (!BookGenreExtensions.TryParseGenre(pair.Key, out var genre))
                    continue;

                var genreId = genre.ToConfigValue();
                normalized[genreId] += pair.Value;
            }

            return normalized;
        }
    }
}
