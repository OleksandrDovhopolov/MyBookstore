using Game.Conditions.API;
using Game.Inventory.API;

namespace Game.Inventory.Conditions
{
    /// <summary>
    /// Leaf condition: "player owns at least <c>min</c> of <c>itemId</c>". Reads the read seam
    /// <see cref="IInventoryService.GetCount"/>. ReasonKey is "haveItem.{itemId}".
    /// </summary>
    public sealed class HaveItemCondition : ICondition
    {
        private readonly IInventoryService _inventory;
        private readonly string _itemId;
        private readonly int _min;

        public HaveItemCondition(IInventoryService inventory, string itemId, int min)
        {
            _inventory = inventory;
            _itemId = itemId;
            _min = min;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Leaf(_inventory.GetCount(_itemId), _min, $"haveItem.{_itemId}");
    }
}
