using Game.Rewards.API;

namespace Game.Quest.UI
{
    public sealed class QuestRewardItemModel
    {
        public RewardKind Kind;
        public string Id;
        public string Category;
        public int Amount;
        public string DisplayName;
    }
}
