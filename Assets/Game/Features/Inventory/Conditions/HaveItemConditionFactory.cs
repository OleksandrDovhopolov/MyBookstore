using System;
using Game.Conditions.API;
using Game.Inventory.API;
using Newtonsoft.Json.Linq;

namespace Game.Inventory.Conditions
{
    /// <summary>
    /// Builds <see cref="HaveItemCondition"/> from <c>{ "type": "haveItem", "itemId": "...", "min": 1 }</c>.
    /// Registered in DI by the Inventory binding. <c>min</c> defaults to and is normalized to at least 1,
    /// so the condition can never be accidentally always-met.
    /// </summary>
    public sealed class HaveItemConditionFactory : IConditionFactory
    {
        public const string TypeId = "haveItem";

        private readonly IInventoryService _inventory;

        public HaveItemConditionFactory(IInventoryService inventory)
            => _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var itemId = node.Value<string>("itemId");
            if (string.IsNullOrEmpty(itemId))
                throw new ArgumentException("missing 'itemId'");

            var min = node.Value<int?>("min") ?? 1;
            if (min <= 0) min = 1;

            return new HaveItemCondition(_inventory, itemId, min);
        }
    }
}
