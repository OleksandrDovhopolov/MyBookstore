using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.LocationUnlock.API
{
    /// <summary>
    /// Domain layer over the Conditions engine: owns opened location ids, condition progress, and
    /// optional item costs for manual unlocks.
    /// </summary>
    public interface ILocationUnlockService
    {
        /// <summary>True only when the location has been opened and persisted.</summary>
        bool IsUnlocked(string locationId);

        /// <summary>Full status: state + condition progress tree. Never null (unknown id returns Locked).</summary>
        LocationUnlockStatus GetStatus(string locationId);

        /// <summary>Item cost progress for a manual unlock. Empty when the location has no unlock cost.</summary>
        IReadOnlyList<LocationUnlockCostProgress> GetCost(string locationId);

        /// <summary>
        /// Explicitly opens the location if its conditions and item cost are met: consumes cost items,
        /// persists the opened fact, and raises <see cref="Unlocked"/>.
        /// </summary>
        UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct);

        /// <summary>Fired after a location is opened. Argument is the location id.</summary>
        event Action<string> Unlocked;

        /// <summary>
        /// Fired for a still-locked location when its underlying condition data moved, so its progress
        /// may have changed. Locations that cross the threshold raise <see cref="Unlocked"/> instead.
        /// </summary>
        event Action<string> StatusChanged;
    }
}
