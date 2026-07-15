namespace Book.Sell.Services
{
    /// <summary>
    /// Single global lock shared by the active minigame and dialogue. Only one interaction runs at a
    /// time; everything else (other customers, spawning) is paused while it is held. Waiters are served
    /// FIFO — the order in which they first requested the lock, not tick order.
    /// </summary>
    public interface IInteractionLock
    {
        bool IsHeld { get; }

        /// <summary>The token currently holding the lock, or null.</summary>
        object CurrentHolder { get; }

        /// <summary>
        /// Tries to acquire the lock for <paramref name="token"/>. If free, acquires and returns true.
        /// If held by someone else, enqueues the token (idempotent) for FIFO service and returns false.
        /// Re-acquiring while already holding returns true.
        /// </summary>
        bool TryAcquire(object token);

        /// <summary>Releases the lock if <paramref name="token"/> holds it; the next FIFO waiter gets a turn.</summary>
        void Release(object token);

        /// <summary>True if <paramref name="token"/> is first in line (or already holding) and would win the next acquire.</summary>
        bool IsNextOrHolding(object token);
    }
}
