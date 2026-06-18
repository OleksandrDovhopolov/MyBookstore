using UnityEngine;

namespace Book.Sell.UI.Customer
{
    // Payload for a state transition. Nulls are allowed — the view only uses fields relevant
    // to the target state.
    public sealed class CustomerThoughtPayload
    {
        public Sprite BookSprite { get; }
        public Sprite RejectedBookSprite { get; }
        public Sprite ReplacementBookSprite { get; }
        public string CommentText { get; }

        public CustomerThoughtPayload(
            Sprite bookSprite = null,
            Sprite rejectedBookSprite = null,
            Sprite replacementBookSprite = null,
            string commentText = null)
        {
            BookSprite = bookSprite;
            RejectedBookSprite = rejectedBookSprite;
            ReplacementBookSprite = replacementBookSprite;
            CommentText = commentText;
        }

        public static readonly CustomerThoughtPayload Empty = new();
    }
}
