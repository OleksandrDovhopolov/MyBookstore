using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Leaf condition: "at least <c>min</c> books of <c>genre</c> sold within a single game day". Reads the
    /// best single-day count from the read-only <see cref="ISalesStatsReader"/> seam. ReasonKey is
    /// "soldGenreInSingleDay.{Genre}".
    /// </summary>
    public sealed class SoldGenreInSingleDayCondition : ICondition
    {
        private readonly ISalesStatsReader _reader;
        private readonly BookGenre _genre;
        private readonly int _min;

        public SoldGenreInSingleDayCondition(ISalesStatsReader reader, BookGenre genre, int min)
        {
            _reader = reader;
            _genre = genre;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.GetMaxSoldInSingleDay(_genre), _min,
                $"soldGenreInSingleDay.{_genre}");
    }
}
