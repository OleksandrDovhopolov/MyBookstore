using System.Collections.Generic;
using Game.Conditions.API;

namespace Game.Conditions.Services
{
    /// <summary>Met when every child is met. Progress = met-children / total-children.</summary>
    public sealed class AllOfCondition : ICondition
    {
        public const string ReasonKey = "all";

        private readonly IReadOnlyList<ICondition> _children;

        public AllOfCondition(IReadOnlyList<ICondition> children)
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

            // Vacuously true when empty (an empty AllOf imposes no requirement).
            var isMet = met == _children.Count;
            return new ConditionResult(isMet, met, _children.Count, ReasonKey, results);
        }
    }
}
