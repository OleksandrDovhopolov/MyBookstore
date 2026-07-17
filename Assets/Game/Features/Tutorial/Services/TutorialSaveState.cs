using System.Collections.Generic;

namespace Game.Tutorial.Services
{
    /// <summary>
    /// Persisted tutorial progress (module <see cref="TutorialSaveKeys.State"/>). One-way completion:
    /// sequence ids only ever get added to <see cref="CompletedSequenceIds"/>.
    /// </summary>
    public sealed class TutorialSaveState
    {
        /// <summary>Sequences finished on this save; never re-run.</summary>
        public List<string> CompletedSequenceIds { get; set; } = new();

        /// <summary>Sequence currently mid-run (for resume after relaunch), or null.</summary>
        public string ActiveSequenceId { get; set; }

        /// <summary>Index of the current step; kept as a compatibility fallback when <see cref="NextStepId"/> is absent.</summary>
        public int NextStepIndex { get; set; }

        /// <summary>Id of the current step; preferred resume anchor for FromStep sequences.</summary>
        public string NextStepId { get; set; }

        public string UpdatedAtUtcIso { get; set; }
    }
}
