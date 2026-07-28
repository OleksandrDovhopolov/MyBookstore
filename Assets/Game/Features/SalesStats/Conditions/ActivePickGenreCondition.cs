using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;

namespace Game.SalesStats.Conditions
{
    /// <summary>Leaf condition: "at least min excellent active recommendations of genre".</summary>
    public sealed class ActivePickGenreCondition : ICondition
    {
        private readonly ISalesStatsReader _reader;
        private readonly BookGenre _genre;
        private readonly int _min;

        public ActivePickGenreCondition(ISalesStatsReader reader, BookGenre genre, int min)
        {
            _reader = reader;
            _genre = genre;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.GetExcellentPicks(_genre), _min, $"activePickGenre.{_genre}");
    }
}
