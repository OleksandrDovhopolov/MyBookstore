using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Game.Quest.API
{
    /// <summary>
    /// Owns the collection of quests: lookup, lifecycle transitions, chains and change notifications.
    /// Built on top of Game.Conditions (docs/QUESTS.md §11): activation/completion/fail come from the
    /// quest's JObject condition trees. Events are plain C# events, like ILocationUnlockService.
    /// </summary>
    public interface IQuestsService
    {
        /// <summary>The quest instance for <paramref name="questId"/>, or null if unknown.</summary>
        IQuest TryGetQuest(string questId);

        /// <summary>Static config for <paramref name="questId"/>, or null if unknown.</summary>
        QuestConfig GetQuestConfig(string questId);

        /// <summary>Current state; Pending for unknown/never-started quests.</summary>
        QuestState GetQuestState(string questId);

        IEnumerable<IQuest> GetActiveQuests();

        /// <summary>Chain by its <c>QuestConfig.ChainId</c>, or null if none.</summary>
        IQuestChain GetChain(string chainId);

        /// <summary>The chain that contains <paramref name="questId"/>, or null if none.</summary>
        IQuestChain GetChainByQuestId(string questId);

        /// <summary>Pending → Active. Returns true if the transition happened.</summary>
        UniTask<bool> TryActivateAsync(string questId, CancellationToken ct);

        /// <summary>ReadyToAward → Awarded (grants rewards / applies permanent effects). Returns true if it happened.</summary>
        UniTask<bool> TryAwardAsync(string questId, CancellationToken ct);

        /// <summary>Active → Failed. Returns true if it happened.</summary>
        UniTask<bool> TryFailAsync(string questId, CancellationToken ct);

        /// <summary>Quest entered Active.</summary>
        event Action<IQuest> QuestStarted;

        /// <summary>Quest entered ReadyToAward (all tasks completed).</summary>
        event Action<IQuest> QuestCompleted;

        /// <summary>Quest entered Awarded.</summary>
        event Action<IQuest> QuestAwarded;

        /// <summary>Quest entered Failed.</summary>
        event Action<IQuest> QuestFailed;

        /// <summary>A task entered Completed.</summary>
        event Action<IQuestTask> TaskCompleted;

        /// <summary>A task's progress changed (numerator moved) without necessarily completing.</summary>
        event Action<IQuestTask> TaskProgressChanged;
    }
}
