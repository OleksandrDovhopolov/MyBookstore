using Game.Conditions.API;
using Game.Configs.Models;
using Game.SalesStats.API;

namespace Game.SalesStats.Conditions
{
    /// <summary>
    /// Leaf condition: "at least <c>min</c> books of <c>genre</c> sold at <c>locationId</c>". Reads the
    /// read-only <see cref="ISalesStatsReader"/> seam. ReasonKey is "soldGenreAtLocation.{Location}.{Genre}".
    /// </summary>
    public sealed class SoldGenreAtLocationCondition : ICondition
    {
        private readonly ISalesStatsReader _reader;
        private readonly BookGenre _genre;
        private readonly string _locationId;
        private readonly int _min;

        public SoldGenreAtLocationCondition(ISalesStatsReader reader, BookGenre genre, string locationId, int min)
        {
            _reader = reader;
            _genre = genre;
            _locationId = locationId;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.GetSold(_genre, _locationId), _min,
                $"soldGenreAtLocation.{_locationId}.{_genre}");
    }
}
