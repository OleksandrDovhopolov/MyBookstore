using Game.UI.ContentWidget;

namespace Game.Quest.UI
{
    public sealed class QuestRewardWidgetData : ContentWidgetDataBase
    {
        public QuestRewardWidgetData(string rewardId, string description)
        {
            RewardId = rewardId;
            Description = description;
        }

        public string RewardId { get; }
        public string Description { get; }
    }
}
