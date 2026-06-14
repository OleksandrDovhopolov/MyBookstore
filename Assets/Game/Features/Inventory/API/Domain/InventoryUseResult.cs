namespace Game.Inventory.API
{
    /// <summary>
    /// Return value of <see cref="IInventoryUseRouter.UseAsync"/> and of any concrete
    /// <see cref="IInventoryItemUseHandler"/>. The router consults <see cref="ConsumeAfterUse"/>
    /// to decide whether to remove the item from inventory after a successful use.
    /// </summary>
    public sealed class InventoryUseResult
    {
        public bool Success { get; }
        public bool ConsumeAfterUse { get; }
        public string Message { get; }

        public InventoryUseResult(bool success, bool consumeAfterUse, string message)
        {
            Success = success;
            ConsumeAfterUse = consumeAfterUse;
            Message = message;
        }

        public static InventoryUseResult Ok(bool consume = false, string message = null)
            => new(true, consume, message);

        public static InventoryUseResult Fail(string message)
            => new(false, false, message);
    }
}
