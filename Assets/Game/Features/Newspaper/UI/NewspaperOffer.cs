namespace Game.Newspaper.UI
{
    public sealed class NewspaperOffer
    {
        public string LotId { get; }

        /// <summary>
        /// Addressables id used to load this offer's icon. For decor offers it is the decors.json id
        /// (e.g. <c>vintage_globe</c>); for book offers it is the shared book-box sprite id.
        /// </summary>
        public string IconId { get; }

        public string DisplayName { get; }
        public string Description { get; }
        public string PriceText { get; }
        public bool IsAvailable { get; }
        public string StateText { get; }

        public NewspaperOffer(
            string lotId,
            string iconId,
            string displayName,
            string description,
            string priceText,
            bool isAvailable,
            string stateText)
        {
            LotId = lotId;
            IconId = iconId;
            DisplayName = displayName;
            Description = description;
            PriceText = priceText;
            IsAvailable = isAvailable;
            StateText = stateText;
        }
    }
}
