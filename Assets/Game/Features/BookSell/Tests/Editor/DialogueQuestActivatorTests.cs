using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.Tests.Editor.Fakes;
using Book.Sell.UI;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Quest.API;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class DialogueQuestActivatorTests
    {
        [Test]
        public void DialogueWithoutQuest_DoesNotActivate()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new DialogueConfig
                {
                    Id = "eddy1",
                    Nodes = Array.Empty<DialogueNodeConfig>()
                }
            });
            var quests = new FakeQuestsService(("q_intro_eddi", QuestState.Pending));
            var activator = new DialogueQuestActivator(configs, quests);

            var activated = activator
                .ActivateForDialogueAsync("eddy1", CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(activated);
            Assert.AreEqual(0, quests.TryActivateCalls);
        }

        [Test]
        public void DialogueWithQuest_ActivatesQuestOnce()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[]
            {
                new DialogueConfig
                {
                    Id = "milly1",
                    ActivatesQuestId = "q_intro_milly",
                    Nodes = Array.Empty<DialogueNodeConfig>()
                }
            });
            var quests = new FakeQuestsService(("q_intro_milly", QuestState.Pending));
            var activator = new DialogueQuestActivator(configs, quests);

            var first = activator
                .ActivateForDialogueAsync("milly1", CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            var second = activator
                .ActivateForDialogueAsync("milly1", CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsTrue(first);
            Assert.IsFalse(second);
            Assert.AreEqual(2, quests.TryActivateCalls);
            Assert.AreEqual(QuestState.Active, quests.GetQuestState("q_intro_milly"));
        }

        private sealed class FakeQuestsService : IQuestsService
        {
            private readonly Dictionary<string, QuestState> _states = new(StringComparer.Ordinal);

            public FakeQuestsService(params (string Id, QuestState State)[] quests)
            {
                for (var i = 0; i < quests.Length; i++)
                    _states[quests[i].Id] = quests[i].State;
            }

            public int TryActivateCalls { get; private set; }

            public IQuest TryGetQuest(string questId) => null;
            public QuestConfig GetQuestConfig(string questId) => null;
            public IReadOnlyList<IQuest> GetAllQuests() => Array.Empty<IQuest>();

            public QuestState GetQuestState(string questId)
                => questId != null && _states.TryGetValue(questId, out var state) ? state : QuestState.Pending;

            public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
            public IQuestChain GetChain(string chainId) => null;
            public IQuestChain GetChainByQuestId(string questId) => null;

            public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct)
            {
                TryActivateCalls++;
                if (string.IsNullOrEmpty(questId) || GetQuestState(questId) != QuestState.Pending)
                    return UniTask.FromResult(false);

                _states[questId] = QuestState.Active;
                return UniTask.FromResult(true);
            }

            public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);

            public event Action<IQuest> QuestStarted;
            public event Action<IQuest> QuestCompleted;
            public event Action<IQuest> QuestAwarded;
            public event Action<IQuest> QuestFailed;
            public event Action<IQuestTask> TaskCompleted;
            public event Action<IQuestTask> TaskProgressChanged;
        }
    }
}
