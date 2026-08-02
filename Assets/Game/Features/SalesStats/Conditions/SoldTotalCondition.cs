using Game.Conditions.API;
using Game.SalesStats.API;

namespace Game.SalesStats.Conditions
{
    /// <summary>Leaf condition: "at least min books sold across all genres".</summary>
    public sealed class SoldTotalCondition : ICondition
    {
        private readonly ISalesStatsReader _reader;
        private readonly int _min;

        public SoldTotalCondition(ISalesStatsReader reader, int min)
        {
            _reader = reader;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_reader.TotalSold, _min, SoldTotalConditionFactory.TypeId);
    }
}
