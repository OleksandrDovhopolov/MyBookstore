using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorates the requested-genre passive resolver with authored attempts attached to a spawned customer.
    /// The shared PassivePurchaseStep still owns browse, reserve, commit, and feedback.
    /// </summary>
    public sealed class ScriptedPassivePurchaseResolver : IPassivePurchaseResolver
    {
        private const string LogPrefix = "[Sales.ScriptedPassive]";

        private readonly IPassivePurchaseResolver _inner;

        public ScriptedPassivePurchaseResolver(IPassivePurchaseResolver inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PassiveAttemptResult Resolve(Customer self, CustomerContext ctx, IReadOnlyList<ShelfBook> available)
        {
            if (self == null || !self.TryConsumeNextScriptedPassiveAttempt(out var attempt))
                return _inner.Resolve(self, ctx, available);

            var genre = attempt.Genre;
            if (string.IsNullOrWhiteSpace(genre))
            {
                Debug.LogWarning($"{LogPrefix} character '{self.CharacterId}' has an empty scripted genre; falling back.");
                return _inner.Resolve(self, ctx, available);
            }

            if (!attempt.ForceHit)
                return PassiveAttemptResult.Miss(genre);

            var groups = GenreShelfPicker.GroupAvailableByGenre(available);
            if (!groups.TryGetValue(genre, out var genreBooks) || genreBooks.Count == 0)
            {
                Debug.LogError($"{LogPrefix} character '{self.CharacterId}' forced hit for genre '{genre}', but no stock is available.");
                return PassiveAttemptResult.Miss(genre);
            }

            var book = GenreShelfPicker.WeightedPick(genreBooks, ctx.Random);
            return book != null
                ? PassiveAttemptResult.Hit(genre, book)
                : PassiveAttemptResult.Miss(genre);
        }
    }
}
