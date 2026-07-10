using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// Data-driven tutorial sequence (Layer 2 forced-step engine). File: tutorials.json (JSON array).
    /// Mirrors <see cref="QuestConfig"/>: Configs stays feature-agnostic, so <see cref="Trigger"/> /
    /// <see cref="Context"/> / <see cref="ResumePolicy"/> and each step's type are plain strings (no
    /// Game.Tutorial enums here), and <see cref="ActivationConditions"/> is a raw <see cref="JObject"/>
    /// tree parsed later by the Conditions engine.
    /// </summary>
    [ConfigFile("tutorials")]
    public sealed class TutorialSequenceConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Lower runs first when several sequences are eligible at once.</summary>
        public int Priority { get; set; }

        /// <summary>"hub" | "location" | "any" — where the sequence is allowed to run.</summary>
        public string Context { get; set; }

        /// <summary>
        /// "hubReady" | "locationLoaded" | "phaseChanged" | "questStarted" | "questCompleted" —
        /// the event that makes the sequence eligible for the activation scan.
        /// </summary>
        public string Trigger { get; set; }

        /// <summary>Optional trigger parameter (e.g. questId for questStarted, phase for phaseChanged).</summary>
        public string TriggerParam { get; set; }

        /// <summary>"restart" (default) | "fromStep" — how a saved active sequence resumes after relaunch.</summary>
        public string ResumePolicy { get; set; }

        /// <summary>Condition tree evaluated at trigger time; null/empty = always eligible.</summary>
        public JObject ActivationConditions { get; set; }

        public TutorialStepConfig[] Steps { get; set; }
    }
}
