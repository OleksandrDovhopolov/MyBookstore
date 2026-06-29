using System.Collections.Generic;
using Game.Quest.API;
using Game.SalesStats.API;

namespace Game.Quest.Services.Persistence
{
    /// <summary>
    /// Persisted quest state (save module <see cref="QuestsSaveKeys.State"/>). Only non-derivable state is
    /// stored: Pending quests are omitted (re-derived from config + conditions each launch). Mirrors the
    /// heroes <c>SavedQuests</c> split: full data for non-terminal quests, id-only for terminals.
    /// </summary>
    public sealed class SavedQuests
    {
        /// <summary>Active + ReadyToAward quests with their per-task states.</summary>
        public Dictionary<string, SavedQuest> Active { get; set; }

        /// <summary>Terminal: awarded quest ids (never re-awarded on load).</summary>
        public List<string> Awarded { get; set; }

        /// <summary>Terminal: failed quest ids.</summary>
        public List<string> Failed { get; set; }
    }

    public sealed class SavedQuest
    {
        public QuestState State { get; set; }                       // Active | ReadyToAward
        public Dictionary<int, QuestTaskState> Tasks { get; set; }  // task id -> state

        /// <summary>4b: per-task sales baseline snapshot (only for active sales tasks). Null when none.</summary>
        public Dictionary<int, SalesStatsStateDto> TaskBaseline { get; set; }
    }
}
