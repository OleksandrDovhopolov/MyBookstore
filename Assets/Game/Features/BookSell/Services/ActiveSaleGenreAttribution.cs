using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public static class ActiveSaleGenreAttribution
    {
        public static string ResolveSoldGenre(ActiveRequestRuntime request, BookConfig book)
        {
            var requiredGenres = request?.RequiredGenres;
            var bookGenres = book?.Genres;
            if (requiredGenres == null || requiredGenres.Count == 0) return null;
            if (bookGenres == null || bookGenres.Length == 0) return null;

            for (var i = 0; i < requiredGenres.Count; i++)
            {
                var required = requiredGenres[i];
                if (string.IsNullOrWhiteSpace(required)) continue;

                for (var j = 0; j < bookGenres.Length; j++)
                {
                    if (string.Equals(required, bookGenres[j], StringComparison.OrdinalIgnoreCase))
                        return required;
                }
            }

            return null;
        }
    }
}
