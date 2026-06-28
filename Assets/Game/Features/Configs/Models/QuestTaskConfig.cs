using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// One task inside a <see cref="QuestConfig"/>. Conditions are raw <see cref="JObject"/> trees parsed
    /// later by the Conditions engine.
    /// </summary>
    public sealed class QuestTaskConfig
    {
        public int Id { get; set; }

        public string DescriptionKey { get; set; }

        /// <summary>Condition tree that completes the task (Active → Completed).</summary>
        public JObject CompletionConditions { get; set; }

        /// <summary>Optional condition tree that activates the task within an active quest. Null = active with the quest.</summary>
        public JObject ActivationConditions { get; set; }

        /// <summary>If true, the task may roll back from Completed when its condition stops holding (e.g. "keep decor equipped N days").</summary>
        public bool CanBeReset { get; set; }
    }
}
