using Game.Conditions.API;
using Game.DayCycle.Day;

namespace Game.DayCycle.Conditions
{
    /// <summary>
    /// Leaf condition: current 1-based game day is at least the configured minimum.
    /// </summary>
    public sealed class DayAtLeastCondition : ICondition
    {
        private readonly IDayProgressService _dayProgress;
        private readonly int _min;

        public DayAtLeastCondition(IDayProgressService dayProgress, int min)
        {
            _dayProgress = dayProgress;
            _min = min < 1 ? 1 : min;
        }

        public ConditionResult Evaluate()
        {
            var currentDay = _dayProgress?.Current?.CurrentDay ?? 1;
            return ConditionResult.Leaf(currentDay, _min, $"dayAtLeast.{_min}");
        }
    }
}
