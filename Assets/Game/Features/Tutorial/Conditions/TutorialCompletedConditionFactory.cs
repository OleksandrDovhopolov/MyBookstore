using System;
using Game.Conditions.API;
using Game.Tutorial.API;
using Newtonsoft.Json.Linq;

namespace Game.Tutorial.Conditions
{
    /// <summary>
    /// Builds <see cref="TutorialCompletedCondition"/> from
    /// <c>{ "type": "tutorialCompleted", "sequenceId": "..." }</c>. Registered lazily (holds a
    /// <see cref="Func{ITutorialService}"/>, not the service) — see the WeatherIsConditionFactory pattern in
    /// DayCycleVContainerBindings — so it never forms a DI cycle with IConditionParser.
    /// </summary>
    public sealed class TutorialCompletedConditionFactory : IConditionFactory
    {
        public const string TypeId = "tutorialCompleted";

        private readonly Func<ITutorialService> _tutorial;

        public TutorialCompletedConditionFactory(Func<ITutorialService> tutorial)
            => _tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var sequenceId = node.Value<string>("sequenceId");
            if (string.IsNullOrEmpty(sequenceId))
                throw new ArgumentException("missing 'sequenceId'");

            return new TutorialCompletedCondition(_tutorial, sequenceId);
        }
    }
}
