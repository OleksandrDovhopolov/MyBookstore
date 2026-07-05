namespace Game.Configs.Models
{
    /// <summary>
    /// One step inside a <see cref="TutorialSequenceConfig"/>. <see cref="Type"/> is a string
    /// discriminator ("showText", "highlightClick", "awaitWindow", "awaitQuest", "awaitPhase",
    /// "awaitLocation") dispatched to a handler in the Game.Tutorial feature. The remaining fields are
    /// optional and only meaningful for some step types.
    /// </summary>
    public sealed class TutorialStepConfig
    {
        public string Id { get; set; }

        public string Type { get; set; }

        /// <summary>Raw display text (localization absent; <see cref="TextKey"/> reserved for later).</summary>
        public string Text { get; set; }

        public string TextKey { get; set; }

        /// <summary>Target id (see TutorialTargetIds) for highlightClick — the rect to cut the hole over.</summary>
        public string Target { get; set; }

        /// <summary>Show the animated pointer over the target (highlightClick).</summary>
        public bool Pointer { get; set; }

        /// <summary>"bottom" | "aboveTarget" | ... — text placement hint.</summary>
        public string Placement { get; set; }

        /// <summary>Padding around the highlighted target rect, in UI units.</summary>
        public int Padding { get; set; }

        /// <summary>Window id (see TutorialWindowIds) for awaitWindow.</summary>
        public string Window { get; set; }

        /// <summary>Quest id for awaitQuest.</summary>
        public string QuestId { get; set; }

        /// <summary>Quest event for awaitQuest ("started" | "taskCompleted" | "completed" | "awarded").</summary>
        public string Event { get; set; }

        /// <summary>Day phase for awaitPhase ("Morning" | "Preparation" | "Sales" | "Results").</summary>
        public string Phase { get; set; }
    }
}
