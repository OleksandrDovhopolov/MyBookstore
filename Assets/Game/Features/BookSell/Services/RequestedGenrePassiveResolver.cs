using System;
using System.Collections.Generic;
using Book.Sell.Domain;

namespace Book.Sell.Services
{
    /// <summary>
    /// New passive model: pick one genre from the customer's profile (among those with stock), roll
    /// only that genre's chance gate, and weighted-pick a book on a hit. The chosen genre is reported
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

            // Eligible = requested genres that currently have stock on the shelf.
            var eligible = new List<string>();
            for (var i = 0; i < requested.Count; i++)
                if (!string.IsNullOrEmpty(requested[i]) && groups.ContainsKey(requested[i]))
                    eligible.Add(requested[i]);

            // No requested genre has stock → miss, but still report a requested genre for feedback.
            if (eligible.Count == 0)
                return PassiveAttemptResult.Miss(PickEquiprobable(requested, ctx.Random));

            var genre = PickEquiprobable(eligible, ctx.Random);
            var genreBooks = groups[genre];

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
