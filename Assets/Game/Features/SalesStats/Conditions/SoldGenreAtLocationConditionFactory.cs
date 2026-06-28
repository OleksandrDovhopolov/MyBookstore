using System;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;
using Newtonsoft.Json.Linq;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Builds <see cref="SoldGenreAtLocationCondition"/> from
    /// <c>{ "type": "soldGenreAtLocation", "genre": "Fantasy", "locationId": "far_beach", "min": 15 }</c>.
    /// Registered in DI by the SalesStats binding, so the engine discovers it without any engine change.
    /// </summary>
    public sealed class SoldGenreAtLocationConditionFactory : IConditionFactory
    {
        public const string TypeId = "soldGenreAtLocation";

        private readonly ISalesStatsReader _reader;

        public SoldGenreAtLocationConditionFactory(ISalesStatsReader reader)
            => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var genreValue = node.Value<string>("genre");
            if (!BookGenreExtensions.TryParseGenre(genreValue, out var genre))
                throw new ArgumentException($"unknown genre '{genreValue}'");

            var locationId = node.Value<string>("locationId");
            if (string.IsNullOrEmpty(locationId))
                throw new ArgumentException("missing 'locationId'");

            var min = node.Value<int?>("min") ?? 0;
            return new SoldGenreAtLocationCondition(_reader, genre, locationId, min);
        }
    }
}
