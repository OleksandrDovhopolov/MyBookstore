using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.DayCycle.Conditions;
using Game.DayCycle.Day;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor
{
    public sealed class DayAtLeastConditionTests
    {
        private static IConditionParser Parser(FakeDayProgress dayProgress)
            => new ConditionParser(new ConditionFactoryRegistry(new IConditionFactory[]
            {
                new DayAtLeastConditionFactory(dayProgress)
            }));

        private static JObject Node(int? min = null)
        {
            var node = new JObject { ["type"] = DayAtLeastConditionFactory.TypeId };
            if (min.HasValue) node["min"] = min.Value;
            return node;
        }

        [Test]
        public void NotMet_WhenCurrentDayIsLessThanMin()
        {
            var dayProgress = new FakeDayProgress(1);
            var result = Parser(dayProgress).Parse(Node(2)).Evaluate();

            Assert.IsFalse(result.IsMet);
            Assert.AreEqual(1, result.Current);
            Assert.AreEqual(2, result.Target);
            Assert.AreEqual("dayAtLeast.2", result.ReasonKey);
        }

        [Test]
        public void Met_WhenCurrentDayEqualsMin()
        {
            var dayProgress = new FakeDayProgress(2);
            Assert.IsTrue(Parser(dayProgress).Parse(Node(2)).Evaluate().IsMet);
        }

        [Test]
        public void Met_WhenCurrentDayIsGreaterThanMin()
        {
            var dayProgress = new FakeDayProgress(3);
            Assert.IsTrue(Parser(dayProgress).Parse(Node(2)).Evaluate().IsMet);
        }

        [Test]
        public void MissingMin_DefaultsToOne()
        {
            var dayProgress = new FakeDayProgress(1);
            var result = Parser(dayProgress).Parse(Node()).Evaluate();

            Assert.IsTrue(result.IsMet);
            Assert.AreEqual(1, result.Target);
            Assert.AreEqual("dayAtLeast.1", result.ReasonKey);
        }

        private sealed class FakeDayProgress : IDayProgressService
        {
            public FakeDayProgress(int currentDay) => Current.CurrentDay = currentDay;

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
