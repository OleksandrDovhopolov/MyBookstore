namespace Book.Sell.Domain
{
    /// <summary>
    /// Фоновая продажа: книга ушла «сама» по матчу со спросом локации. UI рисует short toast.
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
