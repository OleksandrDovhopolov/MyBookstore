using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DayCycle.Day;
using Game.Tutorial.API;
using Game.Tutorial.Content;
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
            var sequence = new TutorialHub(dayProgress);

            Assert.IsTrue(sequence.IsEligible());
        }

        [Test]
        public void IsEligible_FalseWhileResultsPhase()
        {
            var dayProgress = new FakeDayProgress();
            dayProgress.Current.CompletedDays.Add(1);
            dayProgress.Current.CurrentPhase = DayPhase.Results;
            var sequence = new TutorialHub(dayProgress);

            Assert.IsFalse(sequence.IsEligible());
        }

        [Test]
        public void GetSteps_ContainsSingleHubLogStep()
        {
            var sequence = new TutorialHub(new FakeDayProgress());

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
    }
}
