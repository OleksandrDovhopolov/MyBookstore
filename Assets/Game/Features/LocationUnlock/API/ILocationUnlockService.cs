using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Domain layer over the Conditions engine: owns "is location X open?" and the reason/progress for
    /// the location UI. Unlock conditions come from <c>LocationConfig.Unlock</c> (parsed once via
    /// <c>IConditionParser</c>); the opened fact is persisted separately. Unlocking is free and
    /// automatic when conditions are met — <see cref="TryUnlockAsync"/> is just the explicit trigger.
    /// </summary>
    public interface ILocationUnlockService
    {
        /// <summary>True only when the location has been purchased/opened (persisted).</summary>
        bool IsUnlocked(string locationId);

        /// <summary>Full status: state + condition progress tree. Never null (unknown id → Locked).</summary>
        LocationUnlockStatus GetStatus(string locationId);

        /// <summary>
        /// Explicitly opens the location if its conditions are met: persists the opened fact and raises
        /// <see cref="Unlocked"/>. Free — there is no cost. Normally unlocking happens automatically when
        /// conditions are satisfied; this is the manual trigger for the same effect.
        /// </summary>
        UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct);

        /// <summary>Fired after a location is opened. Argument is the location id.</summary>
        event Action<string> Unlocked;

        /// <summary>
        /// Fired for a still-locked location when its underlying condition data moved, so its progress
        /// ("Crime 3/10") may have changed. For reactive UI refresh; locations that cross the threshold
        /// raise <see cref="Unlocked"/> instead.
        /// </summary>
        event Action<string> StatusChanged;
    }
}
