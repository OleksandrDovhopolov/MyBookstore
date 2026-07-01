using System;

namespace Book.Sell.API
{
    /// <summary>
    /// Ready domain payload for a short customer comment. Text lookup is intentionally left to the view or
    /// a later content layer; the step does not resolve configs.
    /// </summary>
    public sealed class CustomerCommentPayload
    {
        public CustomerCommentPayload(string bookId, string genre = null, string textKey = null)
        {
            BookId = bookId ?? string.Empty;
            Genre = genre ?? string.Empty;
            TextKey = textKey ?? string.Empty;
        }

        public string BookId { get; }
        public string Genre { get; }
        public string TextKey { get; }
    }
}
