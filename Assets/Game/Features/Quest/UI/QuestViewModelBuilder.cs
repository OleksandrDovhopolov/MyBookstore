using System;
using System.Collections.Generic;
using Game.Conditions.API;
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
            var (current, goal) = LeafProgress(task);

            return new QuestItemModel
            {
                Id = quest.Id,
                TitleKey = cfg?.TitleKey,
                DescriptionKey = cfg?.DescriptionKey,
                State = quest.State,
                Type = quest.Type,
                NextQuestId = cfg?.NextQuestIds != null && cfg.NextQuestIds.Length > 0 ? cfg.NextQuestIds[0] : null,
                PrimaryTaskKey = task?.Config?.DescriptionKey,
                ProgressCurrent = current,
                ProgressGoal = goal,
                IsComplete = quest.State == QuestState.ReadyToAward || quest.State == QuestState.Awarded
            };
        }

        // The task's Progress is the completion-condition result, usually wrapped in an "all"/"any" composite
        // (e.g. {"all":[{visitLocation, min:3}]}). A composite reports met/total children (0/1), so unwrap
        // single-child composites down to the leaf to show the real "current/required" (e.g. visits 1/3).
        private static (int current, int goal) LeafProgress(IQuestTask task)
        {
            if (task == null) return (0, 1);

            var r = task.Progress;
            while (r.Children != null && r.Children.Count == 1)
                r = r.Children[0];

            return ((int)Math.Max(0, r.Current), (int)Math.Max(1, r.Target));
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
