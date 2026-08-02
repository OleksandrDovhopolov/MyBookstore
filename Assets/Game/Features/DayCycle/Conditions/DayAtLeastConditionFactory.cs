using System;
using Game.Conditions.API;
using Game.DayCycle.Day;
using Newtonsoft.Json.Linq;

namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Builds <see cref="DayAtLeastCondition"/> from <c>{ "type": "dayAtLeast", "min": 2 }</c>.
    /// </summary>
    public sealed class DayAtLeastConditionFactory : IConditionFactory
    {
        public const string TypeId = "dayAtLeast";

        private readonly IDayProgressService _dayProgress;

        public DayAtLeastConditionFactory(IDayProgressService dayProgress)
            => _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var min = node.Value<int?>("min") ?? 1;
            return new DayAtLeastCondition(_dayProgress, min);
        }
    }
}
