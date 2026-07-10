using System;
using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.SalesStats.API
{
    /// <summary>Describes which sales counters a quest task needs in its compact baseline.</summary>
    public sealed class SalesStatsBaselineCapturePlan
    {
        private readonly HashSet<string> _genres = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _locationGenres = new(StringComparer.Ordinal);
        private readonly HashSet<string> _singleDayGenres = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Genres => _genres;
        public IReadOnlyDictionary<string, HashSet<string>> LocationGenres => _locationGenres;
        public IReadOnlyCollection<string> SingleDayGenres => _singleDayGenres;
        public int ActivationDay { get; set; }
        public bool RequiresCurrentDay => _singleDayGenres.Count > 0;
        public bool IsEmpty => _genres.Count == 0 && _locationGenres.Count == 0 && _singleDayGenres.Count == 0;

        public void AddGenre(BookGenre genre) => _genres.Add(genre.ToConfigValue());

        public void AddLocationGenre(string locationId, BookGenre genre)
        {
            if (string.IsNullOrEmpty(locationId)) return;
            if (!_locationGenres.TryGetValue(locationId, out var genres))
            {
                genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _locationGenres[locationId] = genres;
            }

            genres.Add(genre.ToConfigValue());
        }

        public void AddSingleDayGenre(BookGenre genre) => _singleDayGenres.Add(genre.ToConfigValue());
    }
}
