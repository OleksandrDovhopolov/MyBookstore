using System;
using System.Collections.Generic;
using Game.Configs.Models;

namespace Game.Journal.UI
{
    public sealed class JournalPlacesViewModelBuilder
    {
        public IReadOnlyList<JournalPlaceItemModel> Build(
            IEnumerable<LocationConfig> configs,
            Func<string, bool> isUnlocked)
        {
            var result = new List<JournalPlaceItemModel>();
            if (configs == null) return result;

            foreach (var config in configs)
            {
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                result.Add(new JournalPlaceItemModel(
                    config.Id,
                    string.IsNullOrEmpty(config.DisplayName) ? config.Id : config.DisplayName,
                    isUnlocked?.Invoke(config.Id) ?? true,
                    ResolveDemandGenres(config.DemandGenres)));
            }

            return result;
        }

        private static IReadOnlyList<string> ResolveDemandGenres(IReadOnlyList<string> rawGenres)
        {
            if (rawGenres == null || rawGenres.Count == 0) return Array.Empty<string>();

            var result = new List<string>(rawGenres.Count);
            for (var i = 0; i < rawGenres.Count; i++)
            {
                if (BookGenreExtensions.TryParseGenre(rawGenres[i], out var genre))
                    result.Add(genre.ToConfigValue());
            }

            return result;
        }
    }
}
