using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Rewards.API;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lock = Game.UI.Lock;

namespace Game.Quest.UI.Tests.Editor
{
    public sealed class QuestClaimFlowTests
    {
        [Test]
        public async Task Claim_DuplicateWhileInFlight_DoesNotAwardTwice()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter();
            var flow = new QuestClaimFlow(quests, granter, new FakeUIManager(), () => { });
            quests.HoldAward();

            flow.Claim("q1");
            flow.Claim("q1");

            Assert.IsTrue(flow.SuppressRender);
            Assert.AreEqual(1, quests.TryAwardCalls);

            quests.ReleaseAward(true);
            await UniTask.Yield();

            Assert.AreEqual(1, quests.TryAwardCalls);
            Assert.IsFalse(flow.SuppressRender);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_SuppressRender_IsClearedInFinally()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter();
            var changes = 0;
            var flow = new QuestClaimFlow(quests, granter, new FakeUIManager(), () => changes++);
            quests.HoldAward();

            flow.Claim("q1");

            Assert.IsTrue(flow.SuppressRender);

            quests.ReleaseAward(false);
            await UniTask.Yield();

            Assert.IsFalse(flow.SuppressRender);
            Assert.AreEqual(1, changes);
            Assert.AreEqual(0, granter.TryGrantCalls);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_Exception_ClearsSuppressRender()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter
            {
                ExceptionToThrow = new InvalidOperationException("boom")
            };
            var changes = 0;
            var flow = new QuestClaimFlow(quests, granter, new FakeUIManager(), () => changes++);

            LogAssert.Expect(LogType.Error, "[QuestClaimFlow] Claim failed for quest 'q1': boom");

            flow.Claim("q1");
            await UniTask.Yield();

            Assert.IsFalse(flow.SuppressRender);
            Assert.AreEqual(1, changes);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_AwardReturnsFalse_DoesNotGrantOrOpenRewards()
        {
            var quests = new FakeQuestsService { AwardResult = false };
            var granter = new FakeQuestRewardGranter();
            var ui = new FakeUIManager();
            var flow = new QuestClaimFlow(quests, granter, ui, () => { });

            flow.Claim("q1");
            await UniTask.Yield();

            Assert.AreEqual(1, quests.TryAwardCalls);
            Assert.AreEqual(0, granter.TryGrantCalls);
            Assert.AreEqual(0, ui.ShowCalls);
            Assert.IsFalse(flow.SuppressRender);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_GrantFailure_ClearsSuppressAndCallsChanged()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter
            {
                Result = QuestRewardGrantResult.Fail("grant failed")
            };
            var changes = 0;
            var flow = new QuestClaimFlow(quests, granter, new FakeUIManager(), () => changes++);

            LogAssert.Expect(LogType.Error, "[QuestClaimFlow] Failed to grant reward for quest 'q1': grant failed");

            flow.Claim("q1");
            await UniTask.Yield();

            Assert.IsFalse(flow.SuppressRender);
            Assert.AreEqual(1, changes);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_EmptyGrantedItems_DoesNotOpenRewardsWindow()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter
            {
                Result = QuestRewardGrantResult.Ok(new RewardSpec("empty", Array.Empty<RewardItem>()))
            };
            var ui = new FakeUIManager();
            var flow = new QuestClaimFlow(quests, granter, ui, () => { });

            flow.Claim("q1");
            await UniTask.Yield();

            Assert.AreEqual(0, ui.ShowCalls);
            Assert.IsFalse(flow.SuppressRender);
            flow.Dispose();
        }

        [Test]
        public async Task Claim_GrantedItems_OpensRewardsWindow()
        {
            var quests = new FakeQuestsService();
            var granter = new FakeQuestRewardGranter
            {
                Result = QuestRewardGrantResult.Ok(new RewardSpec(
                    "gold",
                    new[] { RewardItem.Resource("gold", 10) }))
            };
            var ui = new FakeUIManager();
            var flow = new QuestClaimFlow(quests, granter, ui, () => { });

            flow.Claim("q1");
            await UniTask.Yield();

            Assert.AreEqual(1, ui.ShowCalls);
            Assert.AreEqual("RewardsWindow", ui.LastShownType?.Name);
            flow.Dispose();
        }

        private sealed class FakeQuestsService : IQuestsService
        {
            private UniTaskCompletionSource _awardHold;

            public int TryAwardCalls { get; private set; }
            public bool AwardResult { get; set; } = true;

            public event Action<IQuest> QuestStarted { add { } remove { } }
            public event Action<IQuest> QuestCompleted { add { } remove { } }
            public event Action<IQuest> QuestAwarded { add { } remove { } }
            public event Action<IQuest> QuestFailed { add { } remove { } }
            public event Action<IQuestTask> TaskCompleted { add { } remove { } }
            public event Action<IQuestTask> TaskProgressChanged { add { } remove { } }

            public void HoldAward() => _awardHold = new UniTaskCompletionSource();

            public void ReleaseAward(bool result)
            {
                AwardResult = result;
                var hold = _awardHold;
                _awardHold = null;
                hold?.TrySetResult();
            }

            public IQuest TryGetQuest(string questId) => null;
            public QuestConfig GetQuestConfig(string questId) => null;
            public QuestState GetQuestState(string questId) => QuestState.Pending;
            public IReadOnlyList<IQuest> GetAllQuests() => Array.Empty<IQuest>();
            public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
            public IQuestChain GetChain(string chainId) => null;
            public IQuestChain GetChainByQuestId(string questId) => null;

            public async UniTask<bool> TryAwardAsync(string questId, CancellationToken ct)
            {
                TryAwardCalls++;
                if (_awardHold != null)
                    await _awardHold.Task;
                return AwardResult;
            }

            public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
        }

        private sealed class FakeQuestRewardGranter : IQuestRewardGranter
        {
            public int TryGrantCalls { get; private set; }
            public QuestRewardGrantResult Result { get; set; }
                = QuestRewardGrantResult.Ok(new RewardSpec("empty", Array.Empty<RewardItem>()));
            public Exception ExceptionToThrow { get; set; }

            public bool IsGranted(string questId) => false;

            public UniTask<QuestRewardGrantResult> TryGrantAsync(string questId, CancellationToken ct)
            {
                TryGrantCalls++;
                if (ExceptionToThrow != null)
                    throw ExceptionToThrow;
                return UniTask.FromResult(Result);
            }
        }

        private sealed class FakeUIManager : IUIManager
        {
            public int ShowCalls { get; private set; }
            public Type LastShownType { get; private set; }

            public event Action<IWindowController> WindowShown { add { } remove { } }
            public event Action<IWindowController> WindowHidden { add { } remove { } }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
            {
                ShowCalls++;
                LastShownType = typeof(T);
                return UniTask.FromResult<T>(null);
            }

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
                => UniTask.CompletedTask;

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;
            public bool IsWindowShown<T>() where T : class, IWindowController => false;
            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Lock SetManualLock(object owner) => null;
        }
    }
}
