using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Quest.API;
using UnityEngine;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Waits for a quest event: "started" | "completed" | "awarded" | "taskCompleted" (optionally a specific
    /// TaskId). Every branch does an initial state check FIRST, so a resume/reload where the event already
    /// happened does not hang (the C# event won't fire again).
    /// </summary>
    public sealed class AwaitQuestStepHandler : ITutorialStepHandler
    {
        private const string LogPrefix = "[Tutorial]";

        private readonly IQuestsService _quests;

        public AwaitQuestStepHandler(IQuestsService quests) => _quests = quests;

        public string Type => TutorialStepTypes.AwaitQuest;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            if (_quests == null || string.IsNullOrEmpty(step?.QuestId))
            {
                Debug.LogWarning($"{LogPrefix} awaitQuest: no quest service or questId; auto-advancing.");
                return;
            }

            var questId = step.QuestId;
            var evt = (step.Event ?? "completed").ToLowerInvariant();
            var taskId = step.TaskId;

            if (IsSatisfied(evt, questId, taskId)) return;

            var tcs = new UniTaskCompletionSource();
            // Single delegate instances (NOT method groups) so += and -= reference the same delegate.
            Action<IQuest> onQuest = q =>
            {
                if (q != null && q.Id == questId && IsSatisfied(evt, questId, taskId)) tcs.TrySetResult();
            };
            Action<IQuestTask> onTask = t =>
            {
                if (t != null && t.QuestId == questId && (taskId == 0 || t.Id == taskId)) tcs.TrySetResult();
            };

            Subscribe(evt, onQuest, onTask);
            try
            {
                if (IsSatisfied(evt, questId, taskId)) return;
                await tcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                Unsubscribe(evt, onQuest, onTask);
            }
        }

        private bool IsSatisfied(string evt, string questId, int taskId)
        {
            switch (evt)
            {
                case "started":
                    return _quests.GetQuestState(questId) != QuestState.Pending;
                case "completed":
                    var cs = _quests.GetQuestState(questId);
                    return cs == QuestState.ReadyToAward || cs == QuestState.Awarded;
                case "awarded":
                    return _quests.GetQuestState(questId) == QuestState.Awarded;
                case "taskcompleted":
                    var quest = _quests.TryGetQuest(questId);
                    return quest?.Tasks != null && quest.Tasks.Any(
                        t => t.State == QuestTaskState.Completed && (taskId == 0 || t.Id == taskId));
                default:
                    Debug.LogWarning($"{LogPrefix} awaitQuest: unknown event '{evt}'.");
                    return true; // fail-open — do not soft-lock on a bad authoring value
            }
        }

        private void Subscribe(string evt, Action<IQuest> onQuest, Action<IQuestTask> onTask)
        {
            switch (evt)
            {
                case "started": _quests.QuestStarted += onQuest; break;
                case "completed": _quests.QuestCompleted += onQuest; break;
                case "awarded": _quests.QuestAwarded += onQuest; break;
                case "taskcompleted": _quests.TaskCompleted += onTask; break;
            }
        }

        private void Unsubscribe(string evt, Action<IQuest> onQuest, Action<IQuestTask> onTask)
        {
            switch (evt)
            {
                case "started": _quests.QuestStarted -= onQuest; break;
                case "completed": _quests.QuestCompleted -= onQuest; break;
                case "awarded": _quests.QuestAwarded -= onQuest; break;
                case "taskcompleted": _quests.TaskCompleted -= onTask; break;
            }
        }
    }
}
