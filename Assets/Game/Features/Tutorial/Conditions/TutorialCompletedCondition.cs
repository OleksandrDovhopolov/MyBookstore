using System;
using Game.Conditions.API;
using Game.Tutorial.API;

namespace Game.Tutorial.Conditions
{
    /// <summary>
    /// Leaf condition: "tutorial sequence <c>sequenceId</c> has been completed". Reads
    /// <see cref="ITutorialService"/> lazily (resolved via the injected factory) so building the condition
    /// factory collection never forces the tutorial service — avoids a DI cycle through IConditionParser.
    /// </summary>
    public sealed class TutorialCompletedCondition : ICondition
    {
        private readonly Func<ITutorialService> _tutorial;
        private readonly string _sequenceId;

        public TutorialCompletedCondition(Func<ITutorialService> tutorial, string sequenceId)
        {
            _tutorial = tutorial;
            _sequenceId = sequenceId;
        }

        public ConditionResult Evaluate()
        {
            var completed = _tutorial()?.IsSequenceCompleted(_sequenceId) ?? false;
            return ConditionResult.Boolean(completed, $"tutorialCompleted.{_sequenceId}");
        }
    }
}
