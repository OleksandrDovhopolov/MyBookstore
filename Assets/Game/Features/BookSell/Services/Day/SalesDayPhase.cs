namespace Book.Sell.Services
{
    /// <summary>
    /// Lifecycle of a sales day. The day no longer auto-completes: it reaches <see cref="ReadyToClose"/>
    /// once every spawned customer has finished its plan (so closing steps run), then the player taps
    /// "close shop" to move it to <see cref="Completed"/> (results).
    /// </summary>
    public enum SalesDayPhase
    {
        /// <summary>Customers spawn and tick.</summary>
        Running,

        /// <summary>No more new customers and all spawned ones are Done — waiting for the player to close.</summary>
        ReadyToClose,

        /// <summary>Player closed the shop (or debug force) — result published.</summary>
        Completed
    }
}
