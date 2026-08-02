using System;
using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;
using Newtonsoft.Json.Linq;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Builds <see cref="ActivePickGenreCondition"/> from
    /// <c>{ "type": "activePickGenre", "genre": "Fact", "min": 5 }</c>.
    /// </summary>
    public sealed class ActivePickGenreConditionFactory : IConditionFactory, ISalesStatsBaselinePlanContributor
    {
        public const string TypeId = "activePickGenre";

        private readonly ISalesStatsReader _reader;

        public ActivePickGenreConditionFactory(ISalesStatsReader reader)
            => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var genre = ReadGenre(node);
            var min = node.Value<int?>("min") ?? 0;
            return new ActivePickGenreCondition(_reader, genre, min);
        }

        public void Contribute(JObject node, SalesStatsBaselineCapturePlan plan)
            => plan?.AddExcellentPickGenre(ReadGenre(node));

        private static BookGenre ReadGenre(JObject node)
        {
            var genreValue = node.Value<string>("genre");
            if (!BookGenreExtensions.TryParseGenre(genreValue, out var genre))
                throw new ArgumentException($"unknown genre '{genreValue}'");
            return genre;
        }
    }
}
