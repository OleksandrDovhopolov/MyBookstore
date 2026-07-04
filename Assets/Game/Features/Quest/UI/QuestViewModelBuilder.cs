using System.Collections.Generic;
using Game.Quest.API;

namespace Game.Quest.UI
{
    /// <summary>
    /// Maps quest instances to <see cref="QuestItemModel"/>s for the window. Pure and EditMode-testable —
    /// keeps <see cref="QuestWindow"/> thin. Mirrors <c>JournalCharactersViewModelBuilder</c>.
    /// </summary>
    public sealed class QuestViewModelBuilder
    {
        public IReadOnlyList<QuestItemModel> Build(IEnumerable<IQuest> quests)
        {
            var models = new List<QuestItemModel>();
            if (quests == null) return models;

            foreach (var quest in quests)
                if (quest != null) models.Add(BuildOne(quest));

            return models;
        }

        private static QuestItemModel BuildOne(IQuest quest)
        {
            var cfg = quest.Config;
            var task = PrimaryTask(quest);

            return new QuestItemModel
            {
                TitleKey = cfg?.TitleKey,
                DescriptionKey = cfg?.DescriptionKey,
                State = quest.State,
                Type = quest.Type,
                PrimaryTaskKey = task?.Config?.DescriptionKey,
                ProgressCurrent = task?.GetProgress() ?? 0,
                ProgressGoal = task != null ? task.GetGoal() : 1,
                IsComplete = quest.State == QuestState.ReadyToAward || quest.State == QuestState.Awarded
            };
        }

        // First not-yet-completed task (what the player is working on), else the last task.
        private static IQuestTask PrimaryTask(IQuest quest)
        {
            var tasks = quest.Tasks;
            if (tasks == null || tasks.Count == 0) return null;

            for (var i = 0; i < tasks.Count; i++)
                if (tasks[i] != null && tasks[i].State != QuestTaskState.Completed)
                    return tasks[i];

            return tasks[tasks.Count - 1];
        }
    }
}
