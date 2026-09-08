using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Game.Localization;
using Game.Quest.API;
using Game.Rewards.API;

namespace Game.Quest.UI
{
    public sealed class QuestViewModelBuilder
    {
        private static readonly IReadOnlyList<QuestTaskItemModel> NoTasks = Array.Empty<QuestTaskItemModel>();
        private static readonly IReadOnlyList<QuestRewardItemModel> NoRewards = Array.Empty<QuestRewardItemModel>();

        private readonly IConfigsService _configs;

        public QuestViewModelBuilder(IConfigsService configs = null)
        {
            _configs = configs;
        }

        public IReadOnlyList<QuestItemModel> Build(IEnumerable<IQuest> quests)
        {
            var models = new List<QuestItemModel>();
            if (quests == null) return models;

            foreach (var quest in quests)
            {
                if (quest == null || quest.State == QuestState.Pending) continue;
                models.Add(BuildOne(quest));
            }

            return models;
        }

        private QuestItemModel BuildOne(IQuest quest)
        {
            var cfg = quest.Config;
            var characterId = cfg?.CharacterId;

            return new QuestItemModel
            {
                Id = quest.Id,
                CharacterId = characterId,
                CharacterPortraitKey = ResolvePortraitKey(characterId),
                TitleKey = cfg?.TitleKey,
                DescriptionKey = cfg?.DescriptionKey,
                State = quest.State,
                Type = quest.Type,
                NextQuestId = cfg?.NextQuestIds != null && cfg.NextQuestIds.Length > 0 ? cfg.NextQuestIds[0] : null,
                Tasks = BuildTasks(quest),
                Rewards = BuildRewards(cfg),
                IsComplete = quest.State.IsCompleted(),
                CanClaim = quest.State == QuestState.ReadyToAward,
                IsRewardClaimed = quest.State == QuestState.Awarded,
                IsFailed = quest.State == QuestState.Failed
            };
        }

        private IReadOnlyList<QuestTaskItemModel> BuildTasks(IQuest quest)
        {
            var tasks = quest.Tasks;
            if (tasks == null || tasks.Count == 0) return NoTasks;

            var result = new List<QuestTaskItemModel>(tasks.Count);
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                if (task == null) continue;

                var (current, goal) = LeafProgress(task);
                var isDone = task.State == QuestTaskState.Completed || quest.State.IsCompleted();
                if (isDone) current = goal;

                result.Add(new QuestTaskItemModel
                {
                    TaskId = task.Id,
                    DescriptionKey = task.Config?.DescriptionKey,
                    Current = current,
                    Goal = goal,
                    IsDone = isDone,
                    Fill01 = goal <= 0 ? 0f : Math.Min(1f, Math.Max(0f, current / (float)goal))
                });
            }

            return result;
        }

        private IReadOnlyList<QuestRewardItemModel> BuildRewards(QuestConfig config)
        {
            var rewards = config?.Rewards;
            if (rewards == null || rewards.Length == 0) return NoRewards;

            var result = new List<QuestRewardItemModel>(rewards.Length);
            for (var i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.Id) || reward.Amount <= 0) continue;

                result.Add(new QuestRewardItemModel
                {
                    Kind = ParseRewardKind(reward.Kind),
                    Id = reward.Id,
                    Category = reward.Category,
                    Amount = reward.Amount,
                    DisplayName = ResolveRewardDisplayName(reward)
                });
            }

            return result;
        }

        private string ResolvePortraitKey(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || _configs == null)
                return null;

            return _configs.TryGet<CharacterConfig>(characterId, out var character) && character != null
                ? character.PortraitKey
                : null;
        }

        private string ResolveRewardDisplayName(QuestRewardConfig reward)
        {
            if (reward == null || string.IsNullOrEmpty(reward.Id) || _configs == null)
                return reward?.Id;

            if (string.Equals(reward.Category, InventoryCategories.Consumable, StringComparison.OrdinalIgnoreCase)
                && _configs.TryGet<ConsumableConfig>(reward.Id, out var consumable)
                && consumable != null)
                return LocalizationLocator.GetOrKey(consumable.DisplayNameKey);

            if (string.Equals(reward.Category, InventoryCategories.QuestItem, StringComparison.OrdinalIgnoreCase)
                && _configs.TryGet<QuestItemConfig>(reward.Id, out var questItem)
                && questItem != null)
                return LocalizationLocator.GetOrKey(questItem.DisplayNameKey);

            if (string.Equals(reward.Category, InventoryCategories.Decor, StringComparison.OrdinalIgnoreCase)
                && _configs.TryGet<DecorConfig>(reward.Id, out var decor)
                && decor != null)
                return LocalizationLocator.GetOrKey(decor.DisplayNameKey);

            return reward.Id;
        }

        private static RewardKind ParseRewardKind(string kind)
            => Enum.TryParse<RewardKind>(kind, ignoreCase: true, out var parsed) ? parsed : default;

        private static (int current, int goal) LeafProgress(IQuestTask task)
        {
            if (task == null) return (0, 1);

            var r = task.Progress;
            while (r.Children != null && r.Children.Count == 1)
                r = r.Children[0];

            return (ClampCount(r.Current, min: 0), ClampCount(r.Target, min: 1));
        }

        private static int ClampCount(long value, int min)
        {
            if (value < min) return min;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
