using System;
using System.Collections.Generic;
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
        public void IsEligible_WhenDayOneCompletedMorningAndResultsClosed()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            var sequence = new TutorialHub(dayProgress, new FakeUIManager());

            Assert.IsTrue(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsPhaseOrResultsWindowVisible()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Results;
            var ui = new FakeUIManager();
            var sequence = new TutorialHub(dayProgress, ui);

            Assert.IsFalse(sequence.IsEligible());

            dayProgress.Current.CurrentPhase = DayPhase.Morning;
            ui.ResultsShown = true;
            Assert.IsFalse(sequence.IsEligible());

            ui.ResultsShown = false;
            ui.ResultsSpawned = true;
            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void GetSteps_ContainsSingleHubLogStep()
        {
            var sequence = new TutorialHub(new FakeDayProgress(), new FakeUIManager());

            var steps = sequence.GetSteps();

            Assert.AreEqual("tutorial_hub", sequence.Id);
            Assert.AreEqual(TutorialContext.Hub, sequence.Context);
            Assert.AreEqual(TutorialTrigger.HubReady, sequence.Trigger);
            Assert.AreEqual(1, steps.Count);
            Assert.IsInstanceOf<TutorialLogStep>(steps[0]);
            Assert.AreEqual("hub_log", steps[0].Id);
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
            public event Action<IWindowController> WindowShown;
            public bool ResultsShown { get; set; }
            public bool ResultsSpawned { get; set; }

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

            public bool IsWindowSpawned<T>() where T : class, IWindowController
                => typeof(T) == typeof(ResultsWindow) && ResultsSpawned;

            public Game.UI.Lock SetManualLock(object owner) => new LockMonitor().Acquire(owner);
        }
    }
}
