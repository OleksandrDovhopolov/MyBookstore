namespace Book.Sell.Domain
{
    /// <summary>
    /// A background ("passive") sale: a book left the shelf on its own because it matched
    /// the location's demand. The UI typically renders this as a short toast / log line.
    /// </summary>
    public sealed class PassiveSaleEvent
    {
        public string BookId { get; }
        public int GoldEarned { get; }

        public PassiveSaleEvent(string bookId, int goldEarned)
        {
            BookId = bookId;
            GoldEarned = goldEarned;
        }
    }
}
