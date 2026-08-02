using System;
using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// New passive model: pick one genre from the customer's full profile, roll only that genre's chance
    /// gate when it has stock, and weighted-pick a book on a hit. The chosen genre is reported
    /// on both hit and miss (so the bubble can show its sprite either way).
    /// </summary>
    public sealed class RequestedGenrePassiveResolver : IPassivePurchaseResolver
    {
        private readonly IBaseSaleChanceCalculator _calculator;

        public RequestedGenrePassiveResolver(IBaseSaleChanceCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public PassiveAttemptResult Resolve(Customer self, CustomerContext ctx, IReadOnlyList<ShelfBook> available)
        {
            var requested = self?.Profile?.DesiredGenres;
            if (requested == null || requested.Count == 0)
                return PassiveAttemptResult.Miss(null);   // provider guarantees >=1; defensive only

            var groups = GenreShelfPicker.GroupAvailableByGenre(available);

            var genre = PickEquiprobable(requested, ctx.Random);
            if (string.IsNullOrEmpty(genre) || !groups.TryGetValue(genre, out var genreBooks))
                return PassiveAttemptResult.Miss(genre);

            var chance = _calculator.Compute(genre, genreBooks.Count, ctx.Location, ctx.ActiveDecorIds);
            if (chance <= 0d)
                return PassiveAttemptResult.Miss(genre);

            var roll = ctx.Random.NextDouble();
            if (roll >= chance)
                return PassiveAttemptResult.Miss(genre);

            var book = GenreShelfPicker.WeightedPick(genreBooks, ctx.Random);
            return book != null ? PassiveAttemptResult.Hit(genre, book) : PassiveAttemptResult.Miss(genre);
        }

        private static string PickEquiprobable(IReadOnlyList<string> items, ISalesRandom random)
            => items.Count == 1 ? items[0] : items[random.Range(0, items.Count)];
    }
}
