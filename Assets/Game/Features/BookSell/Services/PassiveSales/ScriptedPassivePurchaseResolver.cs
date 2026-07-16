using System;
using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Decorates the requested-genre passive resolver with authored per-character attempts.
    /// The shared PassivePurchaseStep still owns browse, reserve, commit, and feedback.
    /// </summary>
    public sealed class ScriptedPassivePurchaseResolver : IPassivePurchaseResolver
    {
        private const string LogPrefix = "[Sales.ScriptedPassive]";

        private readonly IConfigsService _configs;
        private readonly IPassivePurchaseResolver _inner;
        private readonly Dictionary<Customer, int> _attemptIndexByCustomer = new();

        public ScriptedPassivePurchaseResolver(IConfigsService configs, IPassivePurchaseResolver inner)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PassiveAttemptResult Resolve(Customer self, CustomerContext ctx, IReadOnlyList<ShelfBook> available)
        {
            if (!TryGetNextAttempt(self, out var attempt))
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
                Debug.LogWarning($"{LogPrefix} character '{self.CharacterId}' forced hit for genre '{genre}', but no stock is available.");
                return PassiveAttemptResult.Miss(genre);
            }

            var book = GenreShelfPicker.WeightedPick(genreBooks, ctx.Random);
            return book != null
                ? PassiveAttemptResult.Hit(genre, book)
                : PassiveAttemptResult.Miss(genre);
        }

        private bool TryGetNextAttempt(Customer self, out ScriptedPassivePurchaseConfig attempt)
        {
            attempt = null;
            if (self == null || string.IsNullOrEmpty(self.CharacterId))
                return false;

            if (!_configs.TryGet<CharacterConfig>(self.CharacterId, out var character))
                return false;

            var script = character.ScriptedPassivePurchases;
            if (script == null || script.Length == 0)
                return false;

            _attemptIndexByCustomer.TryGetValue(self, out var index);
            if (index >= script.Length)
                return false;

            attempt = script[index];
            _attemptIndexByCustomer[self] = index + 1;
            return attempt != null;
        }
    }
}
