using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Domain layer over the Conditions engine: owns "is location X open?", the reason/progress for
    /// the map UI, and the purchase action. Unlock conditions come from <c>LocationConfig.Unlock</c>
    /// (parsed once via <c>IConditionParser</c>); the purchase fact is persisted separately.
    /// </summary>
    public interface ILocationUnlockService
    {
        /// <summary>True only when the location has been purchased/opened (persisted).</summary>
        bool IsUnlocked(string locationId);

        /// <summary>Full status: state + cost + condition progress tree. Never null (unknown id → Locked).</summary>
        LocationUnlockStatus GetStatus(string locationId);

        /// <summary>
        /// Attempts to open the location: verifies it is <see cref="LocationUnlockState.Unlockable"/>,
        /// spends <c>UnlockCost</c>, persists the unlock, and raises <see cref="Unlocked"/>.
        /// </summary>
        UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct);

        /// <summary>Fired after a successful purchase. Argument is the location id.</summary>
        event Action<string> Unlocked;

        /// <summary>
        /// Fired when a not-yet-unlocked location's <see cref="LocationUnlockState"/> changes because
        /// its conditions moved (e.g. Locked → Unlockable). For reactive UI; not a purchase.
        /// </summary>
        event Action<string> StatusChanged;
    }
}
