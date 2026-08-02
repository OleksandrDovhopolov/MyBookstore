namespace Game.Journal.UI
{
    public sealed class JournalObjectItemModel
    {
        public JournalObjectItemModel(string decorId, string displayName)
        {
            DecorId = decorId;
            DisplayName = displayName;
        }

        public string DecorId { get; }
        public string DisplayName { get; }
    }
}
