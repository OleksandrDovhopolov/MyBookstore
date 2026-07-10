namespace Game.Quest.API
{
    /// <summary>
    /// Quest category for the first version (docs/QUESTS.md §1). Stored in <c>QuestConfig.Type</c> as a
    /// lower-case string and parsed via <see cref="QuestTypeExtensions"/>.
    /// </summary>
    public enum QuestType
    {
        Story,
        Side,
        Tutorial
    }
}
