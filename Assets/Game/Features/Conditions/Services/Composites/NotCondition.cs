using Game.Conditions.API;

namespace Game.Conditions.Services
{
    /// <summary>Met when its single child is NOT met.</summary>
    public sealed class NotCondition : ICondition
    {
        public const string ReasonKey = "not";

        private readonly ICondition _inner;

        public NotCondition(ICondition inner) => _inner = inner;

        public ConditionResult Evaluate()
        {
            var inner = _inner.Evaluate();
            return new ConditionResult(!inner.IsMet, inner.IsMet ? 0 : 1, 1, ReasonKey,
                new[] { inner });
        }
    }
}
