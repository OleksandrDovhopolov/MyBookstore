namespace Game.Newspaper.UI
{
    public sealed class NewspaperOffer
    {
        public string LotId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string PriceText { get; }
        public bool IsAvailable { get; }
        public string StateText { get; }

        public NewspaperOffer(
            string lotId,
            string displayName,
            string description,
            string priceText,
            bool isAvailable,
            string stateText)
        {
            LotId = lotId;
            DisplayName = displayName;
            Description = description;
            PriceText = priceText;
            IsAvailable = isAvailable;
            StateText = stateText;
        }
    }
}
