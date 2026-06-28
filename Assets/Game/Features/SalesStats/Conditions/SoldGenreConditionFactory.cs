using System;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;
using Newtonsoft.Json.Linq;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Builds <see cref="SoldGenreCondition"/> from <c>{ "type": "soldGenre", "genre": "Crime", "min": 30 }</c>.
    /// Registered in DI by the SalesStats binding, so the engine discovers it without any engine change.
    /// </summary>
    public sealed class SoldGenreConditionFactory : IConditionFactory
    {
        public const string TypeId = "soldGenre";

        private readonly ISalesStatsReader _reader;

        public SoldGenreConditionFactory(ISalesStatsReader reader)
            => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var genreValue = node.Value<string>("genre");
            if (!BookGenreExtensions.TryParseGenre(genreValue, out var genre))
                throw new ArgumentException($"unknown genre '{genreValue}'");

            var min = node.Value<int?>("min") ?? 0;
            return new SoldGenreCondition(_reader, genre, min);
        }
    }
}
