using System;
using Game.Configs.Models;

namespace Dialogue
{
    /// <summary>Outcome of <see cref="DialogueEngine.Choose"/>.</summary>
    public enum ChooseResult
    {
        /// <summary>Moved to the option's target node; keep rendering.</summary>
        Advanced,

        /// <summary>The option ends the conversation (<c>next</c> == "end"/empty/null).</summary>
        Ended,

        /// <summary>The option points at a node that does not exist (content error). The engine does NOT
        /// move; the view logs an error and treats it as an end so the UI never gets stuck.</summary>
        UnknownTarget
    }

    /// <summary>
    /// Pure, view-agnostic runner over a resolved <see cref="DialogueConfig"/> graph (GAME-6 §Этап 5, A1).
    /// No Unity types — not even <c>Debug.*</c>: it returns a status and lets the window/presenter decide
    /// what to log. Both the 2.1 window and the future 2.2 world-HUD drive it; swapping presentation replaces
    /// only the view.
    ///
    /// Holds the current node (start = <c>Nodes[0]</c>). A terminal node (empty <see cref="Options"/>) has no
    /// choice — the conversation ends there. Unknown non-"end" targets are surfaced as
    /// <see cref="ChooseResult.UnknownTarget"/> (not silently treated as terminal).
    /// </summary>
    public sealed class DialogueEngine
    {
        private const string EndTarget = "end";

        private readonly DialogueConfig _config;
        private DialogueNodeConfig _current;

        public DialogueEngine(DialogueConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.Nodes == null || config.Nodes.Length == 0)
                throw new ArgumentException("DialogueConfig must have at least one node (the entry node).", nameof(config));

            _current = config.Nodes[0];
        }

        /// <summary>The node currently shown: its <see cref="DialogueNodeConfig.Lines"/> + answer options.</summary>
        public DialogueNodeConfig Current => _current;

        /// <summary>True when the current node has no options — the conversation ends here (offer a single
        /// "Continue" button in the view; there is nothing more to choose).</summary>
        public bool IsTerminal => _current.Options == null || _current.Options.Length == 0;

        /// <summary>
        /// Picks option <paramref name="optionIndex"/> on the current node.
        /// <list type="bullet">
        /// <item><c>next</c> == "end"/empty/null → <see cref="ChooseResult.Ended"/> (no move).</item>
        /// <item>existing node id → moves there, <see cref="ChooseResult.Advanced"/>.</item>
        /// <item>unknown node id (or out-of-range index) → <see cref="ChooseResult.UnknownTarget"/> (no move).</item>
        /// </list>
        /// </summary>
        public ChooseResult Choose(int optionIndex)
        {
            var options = _current.Options;
            if (options == null || optionIndex < 0 || optionIndex >= options.Length)
                return ChooseResult.UnknownTarget;

            var next = options[optionIndex].Next;
            if (string.IsNullOrWhiteSpace(next) || string.Equals(next, EndTarget, StringComparison.OrdinalIgnoreCase))
                return ChooseResult.Ended;

            var target = FindNode(next);
            if (target == null)
                return ChooseResult.UnknownTarget;

            _current = target;
            return ChooseResult.Advanced;
        }

        private DialogueNodeConfig FindNode(string nodeId)
        {
            foreach (var node in _config.Nodes)
            {
                if (node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
                    return node;
            }
            return null;
        }
    }
}
