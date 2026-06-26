using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Game.DayCycle.Day;
using Game.DayCycle.Results.Domain;
using Game.DayCycle.Results.Services;
using Game.DayCycle.Tests.Editor.Fakes;
using Game.Resources.API;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor.Results
{
    public sealed class ResultsSessionServiceTests
    {
        private sealed class Harness
        {
            public FakeSaveService Save { get; }
            public FakeDayProgressService DayProgress { get; } = new();
            public FakeResourcesService Resources { get; } = new();
            public FakeProgressionService Progression { get; } = new();
            public ResultsSummarySessionService Sut { get; }

            public List<ResultsSummary> Summaries { get; } = new();
            public int NoResultEmits { get; private set; }

            public Harness(SalesDayResult preloadedResult = null,
                Dictionary<string, string> sharedStore = null)
            {
                Save = sharedStore != null ? new FakeSaveService(sharedStore) : new FakeSaveService();
                if (preloadedResult != null)
                {
                    Save.UpdateModuleAsync(SalesSaveKeys.LastDayResult, preloadedResult,
                        SalesSaveKeys.LastDayResultSchemaVersion, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }

                Sut = new ResultsSummarySessionService(
                    Save,
                    DayProgress,
                    new ResultsSummaryBuilder(new DefaultResultsReviewTextProvider()));

                Sut.SummaryReady += s => Summaries.Add(s);
                Sut.NoResultAvailable += () => NoResultEmits++;
            }

            public int Gold => Resources.GetAmount(ResourceIds.Gold);
            public int Reputation => Progression.Reputation;

            public void Run() => Sut.LoadAndApplyAsync(CancellationToken.None).GetAwaiter().GetResult();
            public void Advance() => Sut.AdvanceToNextDayAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private static SalesDayResult Sales(int day = 1, int gold = 100, int exc = 2, int fail = 0)
            => new()
            {
                Day = day,
                GoldEarned = gold,
                ExcellentCount = exc,
                FailedCount = fail,
                SalesCount = exc + fail
            };

        [Test]
        public void NoSalesResult_EmitsNoResultAvailable_NoMutation()
        {
            var h = new Harness();
            h.Run();

            Assert.AreEqual(1, h.NoResultEmits);
            Assert.AreEqual(0, h.Summaries.Count);
            Assert.AreEqual(0, h.Gold);
            Assert.AreEqual(0, h.Reputation);
        }

        [Test]
        public void Load_Once_BuildsSummary_WithoutMutatingBalances()
        {
            var h = new Harness(Sales(gold: 150, exc: 3));
            h.Run();

            Assert.AreEqual(1, h.Summaries.Count);
            Assert.AreEqual(0, h.Gold);
            Assert.AreEqual(0, h.Reputation);
            Assert.IsFalse(h.Save.Store.ContainsKey("results.applied_rewards"));
            Assert.AreEqual(1, h.DayProgress.MarkCompletedCallCount);
            Assert.AreEqual(DayPhase.Results, h.DayProgress.State.CurrentPhase);
            CollectionAssert.Contains(h.DayProgress.State.CompletedDays, 1);
        }

        [Test]
        public void Load_Twice_UsesCachedSummary_DoesNotMutateBalances()
        {
            var h = new Harness(Sales(gold: 100, exc: 2));
            h.Run();
            h.Run();

            Assert.AreEqual(2, h.Summaries.Count, "Both runs emit a summary.");
            Assert.AreEqual(0, h.Gold);
            Assert.AreEqual(0, h.Reputation);
            Assert.AreSame(h.Summaries[0], h.Summaries[1], "Second run re-emits the cached summary.");
            Assert.IsFalse(h.Summaries[0].AlreadyApplied);
            Assert.IsFalse(h.Summaries[1].AlreadyApplied);
        }

        [Test]
        public void Restart_NewServiceOverSameSaveStore_DoesNotUseAppliedRewards()
        {
            var first = new Harness(Sales(gold: 100, exc: 1));
            first.Run();

            var second = new Harness(sharedStore: first.Save.Store);
            second.Run();

            Assert.AreEqual(0, second.Gold);
            Assert.AreEqual(0, second.Reputation);
            Assert.IsFalse(second.Save.Store.ContainsKey("results.applied_rewards"));
            Assert.IsFalse(second.Summaries[0].AlreadyApplied);
        }

        [Test]
        public void AdvanceToNextDay_DelegatesToDayProgress()
        {
            var h = new Harness(Sales());
            h.Run();
            h.Advance();

            Assert.AreEqual(1, h.DayProgress.AdvanceCallCount);
            Assert.AreEqual(2, h.DayProgress.State.CurrentDay);
            Assert.AreEqual(DayPhase.Morning, h.DayProgress.State.CurrentPhase);
        }

        [Test]
        public void AdvanceToNextDay_ForcesSaveAfterProgressAdvance()
        {
            var h = new Harness(Sales());
            h.Run();
            h.Advance();

            Assert.AreEqual(1, h.Save.ForceWithSyncSaveCallCount);
        }

        [Test]
        public void AdvanceToNextDay_Twice_ForSameCompletedDay_IsNoOpSecondTime()
        {
            var h = new Harness(Sales());
            h.Run();
            h.Advance();
            h.Advance();

            Assert.AreEqual(1, h.DayProgress.AdvanceCallCount);
            Assert.AreEqual(2, h.DayProgress.State.CurrentDay);
        }

        [Test]
        public void Summary_NumbersComeStraightFromSales()
        {
            var sales = Sales(gold: 77, exc: 4, fail: 1);
            sales.SalesCount = 5;
            sales.NormalCount = 0;
            sales.SkippedCount = 0;

            var h = new Harness(sales);
            h.Run();

            var s = h.Summaries[0];
            Assert.AreEqual(77, s.GoldEarned, "View receives gold-earned verbatim from sales.");
            Assert.AreEqual(0, s.GoldDelta);
            Assert.AreEqual(0, s.ReputationDelta);
            Assert.AreEqual(5, s.SalesCount);
            Assert.AreEqual(4, s.ExcellentCount);
            Assert.AreEqual(1, s.FailedCount);
        }
    }
}
