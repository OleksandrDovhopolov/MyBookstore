using System.Collections.Generic;
using Game.Quest.API;

namespace Game.Quest.UI
{
    public sealed class QuestItemModel
    {
        public string Id;
        public string CharacterId;
        public string CharacterPortraitKey;
        public string TitleKey;
        public string DescriptionKey;
        public QuestState State;
        public QuestType Type;
        public string NextQuestId;
        public IReadOnlyList<QuestTaskItemModel> Tasks;
        public IReadOnlyList<QuestRewardItemModel> Rewards;
        public bool IsComplete;
        public bool CanClaim;
        public bool IsRewardClaimed;
        public bool IsFailed;
    }
}
