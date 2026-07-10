using Game.Conditions.API;

namespace Game.Decor.Conditions
{
    /// <summary>
    /// Leaf condition: "decor <c>decorId</c> is currently equipped/placed". Reads the read seam
    /// <see cref="IDecorPlacementService.GetActiveDecorIds"/>. ReasonKey is "decorEquipped.{decorId}".
    /// </summary>
    public sealed class DecorEquippedCondition : ICondition
    {
        private readonly IDecorPlacementService _decor;
        private readonly string _decorId;

        public DecorEquippedCondition(IDecorPlacementService decor, string decorId)
        {
            _decor = decor;
            _decorId = decorId;
        }

        public ConditionResult Evaluate()
        {
            var met = false;
            var active = _decor.GetActiveDecorIds();
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i] == _decorId) { met = true; break; }
            }

            return ConditionResult.Boolean(met, $"decorEquipped.{_decorId}");
        }
    }
}
