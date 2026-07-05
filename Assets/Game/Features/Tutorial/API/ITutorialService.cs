using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Tutorial.API
{
    /// <summary>
    /// Owns the forced-step tutorial sequences (Layer 2). One exclusive sequence runs at a time; progress
    /// is broadcast via MessagePipe signals (TutorialSequenceStarted/StepChanged/SequenceCompleted).
    /// Completion is one-way per save. Layer 1 (tutorial-type quests) lives in the quest system, not here.
    /// </summary>
    public interface ITutorialService
    {
        bool IsRunning { get; }

        /// <summary>Id of the currently running sequence, or null.</summary>
        string ActiveSequenceId { get; }

        IReadOnlyCollection<string> CompletedSequenceIds { get; }

        /// <summary>True once <paramref name="sequenceId"/> has been completed (backs the "tutorialCompleted" condition).</summary>
        bool IsSequenceCompleted(string sequenceId);

        /// <summary>
        /// Starts a sequence if eligible. <paramref name="force"/> bypasses the completed/condition checks
        /// (debug). Returns true if a run was started.
        /// </summary>
        UniTask<bool> TryStartAsync(string sequenceId, bool force, CancellationToken ct);

        /// <summary>Aborts the active sequence without marking it complete (debug).</summary>
        UniTask SkipActiveAsync(CancellationToken ct);

        /// <summary>Clears completion + saved progress for a sequence so it can replay (debug).</summary>
        UniTask ResetAsync(string sequenceId, CancellationToken ct);
    }
}
