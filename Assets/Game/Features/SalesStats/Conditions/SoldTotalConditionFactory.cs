using System;
using Game.Conditions.API;
using Game.SalesStats.API;
using Newtonsoft.Json.Linq;

namespace Game.SalesStats.Conditions
{
    /// <summary>Builds <see cref="SoldTotalCondition"/> from { "type": "soldTotal", "min": 200 }.</summary>
    public sealed class SoldTotalConditionFactory : IConditionFactory
    {
        public const string TypeId = "soldTotal";

        private readonly ISalesStatsReader _reader;

        public SoldTotalConditionFactory(ISalesStatsReader reader)
            => _reader = reader ?? throw new ArgumentNullException(nameof(reader));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var min = node.Value<int?>("min") ?? 0;
            return new SoldTotalCondition(_reader, min);
        }
    }
}
