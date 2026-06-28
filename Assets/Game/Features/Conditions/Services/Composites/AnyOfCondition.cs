using System.Collections.Generic;
using Game.Conditions.API;

namespace Game.Conditions.Services
{
    /// <summary>Met when at least one child is met. Progress = met-children, target = 1 (need any).</summary>
    public sealed class AnyOfCondition : ICondition
    {
        public const string ReasonKey = "any";

        private readonly IReadOnlyList<ICondition> _children;

        public AnyOfCondition(IReadOnlyList<ICondition> children)
            => _children = children ?? System.Array.Empty<ICondition>();

        public ConditionResult Evaluate()
        {
            var results = new ConditionResult[_children.Count];
            var met = 0;
            for (var i = 0; i < _children.Count; i++)
            {
                results[i] = _children[i].Evaluate();
                if (results[i].IsMet) met++;
            }

            // Empty AnyOf is never met (there is nothing to satisfy it).
            var target = _children.Count == 0 ? 0 : 1;
            return new ConditionResult(met >= 1, met, target, ReasonKey, results);
        }
    }
}
