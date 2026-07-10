using System;
using Game.Conditions.API;
using Newtonsoft.Json.Linq;

namespace Game.Decor.Conditions
{
    /// <summary>
    /// Builds <see cref="DecorEquippedCondition"/> from <c>{ "type": "decorEquipped", "decorId": "..." }</c>.
    /// Registered in DI by the Decor binding, so the condition engine discovers it without any engine change.
    /// One decor id per condition — "fireplace OR heater" is an <c>any</c> composite of two such leaves.
    /// </summary>
    public sealed class DecorEquippedConditionFactory : IConditionFactory
    {
        public const string TypeId = "decorEquipped";

        private readonly IDecorPlacementService _decor;

        public DecorEquippedConditionFactory(IDecorPlacementService decor)
            => _decor = decor ?? throw new ArgumentNullException(nameof(decor));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var decorId = node.Value<string>("decorId");
            if (string.IsNullOrEmpty(decorId))
                throw new ArgumentException("missing 'decorId'");

            return new DecorEquippedCondition(_decor, decorId);
        }
    }
}
