using System;

namespace Game.Quest.API
{
    public static class QuestStateExtensions
    {
        /// <summary>True when the quest has reached an award-eligible end (ReadyToAward or Awarded).</summary>
        public static bool IsCompleted(this QuestState state)
            => state == QuestState.Awarded || state == QuestState.ReadyToAward;

        /// <summary>True when a task can no longer change on its own (Completed or Failed).</summary>
        public static bool IsClosed(this QuestTaskState state)
            => state == QuestTaskState.Completed || state == QuestTaskState.Failed;
    }

    /// <summary>
    /// String &lt;-&gt; <see cref="QuestType"/> mapping for the data-driven <c>QuestConfig.Type</c>.
    /// Config values are lower-case (<c>story</c>/<c>side</c>/<c>tutorial</c>), parsing is
    /// case-insensitive. Mirrors <c>BookGenreExtensions</c> in Configs.
    /// </summary>
    public static class QuestTypeExtensions
    {
        public static string ToConfigValue(this QuestType type) => type switch
        {
            QuestType.Story => "story",
            QuestType.Side => "side",
            QuestType.Tutorial => "tutorial",
            _ => type.ToString().ToLowerInvariant()
        };

        public static bool TryParse(string value, out QuestType type)
        {
            type = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            return Enum.TryParse(value.Trim(), ignoreCase: true, out type)
                   && Enum.IsDefined(typeof(QuestType), type);
        }
    }
}
