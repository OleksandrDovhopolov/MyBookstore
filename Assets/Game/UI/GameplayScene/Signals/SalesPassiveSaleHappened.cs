namespace Game.UI
{
    public readonly struct SalesPassiveSaleHappened
    {
        public string CustomerId { get; }
        public string CharacterId { get; }
        public string Genre { get; }
        public string BookId { get; }

        public SalesPassiveSaleHappened(string customerId, string characterId, string genre, string bookId)
        {
            CustomerId = customerId;
            CharacterId = characterId;
            Genre = genre;
            BookId = bookId;
        }
    }
}
