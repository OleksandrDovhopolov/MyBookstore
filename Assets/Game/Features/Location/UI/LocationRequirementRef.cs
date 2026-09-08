namespace Game.Location.UI
{
    public enum LocationRequirementKind
    {
        Item,
        Condition
    }

    public readonly struct LocationRequirementRef
    {
        public LocationRequirementRef(LocationRequirementKind kind, string key)
        {
            Kind = kind;
            Key = key;
        }

        public LocationRequirementKind Kind { get; }
        public string Key { get; }

        public static LocationRequirementRef Item(string itemId)
            => new(LocationRequirementKind.Item, itemId);

        public static LocationRequirementRef Condition(string reasonKey)
            => new(LocationRequirementKind.Condition, reasonKey);
    }
}
