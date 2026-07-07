using System;

namespace Book.Sell.API
{
    /// <summary>
    /// Ready domain payload for a customer dialogue. Carries only the content key: line lookup is left to
    /// the content/presentation layer (resolved from <c>dialogues.json</c> by <see cref="DialogueId"/>),
    /// keeping the domain UI-agnostic and localization-friendly. Fail-fast on a missing id — for a dialogue
    /// an empty key is almost certainly broken content, and we want to catch it where the step is built
    /// (quest/archetype), not at presentation time.
    /// </summary>
    public sealed class DialoguePayload
    {
        public DialoguePayload(string dialogueId)
        {
            if (string.IsNullOrWhiteSpace(dialogueId))
                throw new ArgumentException("DialogueId must be a non-empty content key.", nameof(dialogueId));

            DialogueId = dialogueId;
        }

        public string DialogueId { get; }
    }
}
