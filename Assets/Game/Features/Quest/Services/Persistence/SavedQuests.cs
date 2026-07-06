using System.Collections.Generic;
using Game.Quest.API;
using Game.SalesStats.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        [JsonConverter(typeof(StringEnumConverter))]
        public QuestState State { get; set; }                       // Active | ReadyToAward

        public List<SavedQuestTask> Tasks { get; set; }
    }

    public sealed class SavedQuestTask
    {
        public int Id { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public QuestTaskState State { get; set; }

        /// <summary>Compact sales baseline for this task. Null when the task has no sales-scoped progress.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public SalesStatsBaselineDto SalesBaseline { get; set; }
    }
}
