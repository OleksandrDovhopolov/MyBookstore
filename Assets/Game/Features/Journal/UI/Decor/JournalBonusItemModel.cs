namespace Game.Journal.UI
{
    public sealed class JournalBonusItemModel
    {
        public JournalBonusItemModel(string label, string percentText, bool isPositive, string iconKey)
        {
            Label = label;
            PercentText = percentText;
            IsPositive = isPositive;
            IconKey = iconKey;
        }

        public string Label { get; }
        public string PercentText { get; }
        public bool IsPositive { get; }
        public string IconKey { get; }
    }
}
