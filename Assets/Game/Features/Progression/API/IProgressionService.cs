using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Progression.API
{
    /// <summary>
    /// Player progression stats. For now it owns reputation — a non-spendable number that grows
    /// over time and gates content (e.g. future location unlocks). Same sync-read / async-write
    /// model as <see cref="Game.Resources.API.IResourcesService"/>.
    /// </summary>
    public interface IProgressionService
    {
        /// <summary>Always &gt;= 0.</summary>
        int Reputation { get; }

        /// <summary>
        /// Adds <paramref name="delta"/> to reputation. Negative deltas are allowed (e.g. penalty
        /// from too many failed sales) and are clamped so the final value never goes below 0.
        /// <paramref name="reason"/> is audit text logged with the change event.
        /// A zero delta is a no-op (no event, no save).
        /// </summary>
        UniTask AddReputationAsync(int delta, string reason, CancellationToken ct);

        event Action<ProgressionChangeEvent> Changed;
    }
}
