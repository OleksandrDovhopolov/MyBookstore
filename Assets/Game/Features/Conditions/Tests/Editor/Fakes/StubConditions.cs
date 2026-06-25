using Game.Conditions.API;
using Newtonsoft.Json.Linq;

namespace Game.Conditions.Tests.Editor.Fakes
{
    /// <summary>Numeric leaf with a fixed current/target — handy for composing composites in tests.</summary>
    public sealed class StubCondition : ICondition
    {
        private readonly long _current;
        private readonly long _target;
        private readonly string _reasonKey;

        public StubCondition(long current, long target, string reasonKey = "stub")
        {
            _current = current;
            _target = target;
            _reasonKey = reasonKey;
        }

        public ConditionResult Evaluate() => ConditionResult.Leaf(_current, _target, _reasonKey);
    }

    /// <summary>
    /// Test factory for type "threshold": <c>{ "type": "threshold", "current": N, "min": M }</c>.
    /// Lets parser tests exercise leaf dispatch without depending on a real data feature.
    /// </summary>
    public sealed class ThresholdConditionFactory : IConditionFactory
    {
        public const string TypeId = "threshold";
        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var current = node.Value<long?>("current") ?? 0;
            var min = node.Value<long?>("min") ?? 0;
            return new StubCondition(current, min, "threshold");
        }
    }
}
