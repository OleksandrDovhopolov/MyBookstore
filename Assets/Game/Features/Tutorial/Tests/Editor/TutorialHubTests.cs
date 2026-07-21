using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.DayCycle.Results.UI;
using Game.Tutorial.API;
using Game.Tutorial.Content;
using Game.UI;
using NUnit.Framework;

namespace Game.Tutorial.Tests.Editor
{
    public sealed class TutorialHubTests
    {
        [Test]
        public void IsEligible_WhenDayOneCompletedAndMorning()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsTrue(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhenDayOneNotCompleted()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsPhase()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Results;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsWindowShown()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager { ResultsShown = true });

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void GetSteps_ContainsHubDialogueJournalClickAndJournalWait()
        {
            var sequence = new TutorialHub(new FakeDayProgress(), new FakeUIManager());

            var steps = sequence.GetSteps();

            Assert.AreEqual("tutorial_hub", sequence.Id);
            Assert.AreEqual(TutorialContext.Hub, sequence.Context);
            Assert.AreEqual(TutorialTrigger.HubReady, sequence.Trigger);
            Assert.AreEqual(3, steps.Count);
            Assert.IsInstanceOf<TutorialDialogueStep>(steps[0]);
            Assert.AreEqual("hub_dialogue", steps[0].Id);
            Assert.IsInstanceOf<TutorialHighlightClickStep>(steps[1]);
            Assert.AreEqual("click_journal_button", steps[1].Id);
            Assert.IsInstanceOf<TutorialAwaitWindowStep>(steps[2]);
            Assert.AreEqual("wait_journal_window", steps[2].Id);
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public event Action<DayProgressState> PhaseChanged;
            public DayProgressState Current { get; } = new();
            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);
            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask AdvanceToNextDayAsync(CancellationToken ct) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        private sealed class FakeUIManager : IUIManager
        {
            private readonly LockMonitor _locks = new();

            public event Action<IWindowController> WindowShown;
            public event Action<IWindowController> WindowHidden;
            public bool ResultsShown { get; set; }

            public UniTask<T> ShowAsync<T>(WindowArgs args = null, CancellationToken ct = default)
                where T : class, IWindowController, new()
                => UniTask.FromResult<T>(null);

            public UniTask HideAsync<T>(bool forceClose = false, CancellationToken ct = default)
                where T : class, IWindowController
                => UniTask.CompletedTask;

            public UniTask HideAsync(IWindowController controller, bool forceClose = false, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public UniTask HideTopAsync(WindowLayer? layer = null, CancellationToken ct = default)
                => UniTask.CompletedTask;

            public IWindowController GetTopWindow(WindowLayer? layer = null) => null;

            public bool IsWindowShown<T>() where T : class, IWindowController
                => typeof(T) == typeof(ResultsWindow) && ResultsShown;

            public bool IsWindowSpawned<T>() where T : class, IWindowController => false;
            public Game.UI.Lock SetManualLock(object owner) => _locks.Acquire(owner);
        }
    }
}
