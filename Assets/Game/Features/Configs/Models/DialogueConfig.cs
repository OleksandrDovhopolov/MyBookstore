namespace Game.Configs.Models
{
    /// <summary>
    /// Data-driven dialogue definition. File: dialogues.json (JSON array). A shallow node graph — used by
    /// the dialogue engine (presentation, GAME-6 §Этап 5), not by the customer-sim domain. The domain step
    /// carries only the root <c>DialogueId</c>; the engine resolves the graph here.
    ///
    /// Pure DTO (no traversal/lookup) — same style as <see cref="QuestConfig"/>. Entry node = <c>Nodes[0]</c>.
    /// Content invariants (non-empty Nodes, unique NodeId, resolvable Next, 1..3 Options) are validated by
    /// the engine/validator, not enforced here.
    /// </summary>
    [ConfigFile("dialogues")]
    public sealed class DialogueConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Dialogue nodes. The first element is the entry node.</summary>
        public DialogueNodeConfig[] Nodes { get; set; }
    }

    public sealed class DialogueNodeConfig
    {
        public string NodeId { get; set; }

        /// <summary>Raw lines shown for this node (MVP: raw text; localization keys later, INF-4).</summary>
        public string[] Lines { get; set; }

        /// <summary>Answer options. Empty = terminal node (the conversation ends here).</summary>
        public DialogueOptionConfig[] Options { get; set; }
    }

    public sealed class DialogueOptionConfig
    {
        /// <summary>Raw button text for this option (MVP: raw; localization key later).</summary>
        public string Text { get; set; }

        /// <summary>Target <see cref="DialogueNodeConfig.NodeId"/>, or <c>"end"</c>/empty = terminal. An
        /// unresolvable non-"end" target is a content error (engine/validator warns) — NOT treated as terminal.</summary>
        public string Next { get; set; }
    }
}
