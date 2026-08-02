using Game.Conditions.API;
using Newtonsoft.Json.Linq;

namespace Game.Conditions.Services
{
    /// <summary>
    /// Explicit never-met condition for content that must be activated by code instead of reevaluation.
    /// </summary>
    public sealed class ManualConditionFactory : IConditionFactory
    {
        public const string TypeId = "manual";

        public string Type => TypeId;

        public ICondition Create(JObject node) => new NeverMetCondition(TypeId);
    }
}
