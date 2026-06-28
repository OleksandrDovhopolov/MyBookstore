using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Leaf condition: "at least <c>min</c> books of <c>genre</c> sold". Reads the read-only
    /// <see cref="ISalesStatsReader"/> seam — never the event bus, never the writable service.
    /// ReasonKey is "soldGenre.{Genre}" for UI localization.
    /// </summary>
    public sealed class SoldGenreCondition : ICondition
    {
        private readonly ISalesStatsReader _reader;
        private readonly BookGenre _genre;
        private readonly int _min;

        public SoldGenreCondition(ISalesStatsReader reader, BookGenre genre, int min)
        {
            _reader = reader;
            _genre = genre;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.GetSold(_genre), _min, $"soldGenre.{_genre}");
    }
}
