using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Game.DayCycle.Day;
using Game.DayCycle.Results.Domain;
using Game.DayCycle.Results.Services;
using Game.DayCycle.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor.Results
{
    public sealed class ResultsSessionServiceTests
    {
        private sealed class Harness
        {
            public FakeSaveService Save { get; }
            public FakeDayProgressService DayProgress { get; } = new();
            public FakeSceneTransitionService SceneTransition { get; } = new();
            public ResultsSessionService Sut { get; }

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

                Sut = new ResultsSessionService(
                    Save, DayProgress,
                    new DefaultResultsRewardService(),
                    new DefaultResultsReviewTextProvider(),
                    SceneTransition);

                Sut.SummaryReady += s => Summaries.Add(s);
                Sut.NoResultAvailable += () => NoResultEmits++;
            }

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
            var h = new Harness();   // no preload
            h.Run();

            Assert.AreEqual(1, h.NoResultEmits);
            Assert.AreEqual(0, h.Summaries.Count);
            Assert.AreEqual(0, h.DayProgress.State.Gold);
        }

        [Test]
        public void Apply_Once_MutatesBalances_AndPersistsAppliedRecord()
        {
            var h = new Harness(Sales(gold: 150, exc: 3));
            h.Run();

            Assert.AreEqual(1, h.Summaries.Count);
            Assert.AreEqual(150, h.DayProgress.State.Gold);
            Assert.AreEqual(3, h.DayProgress.State.Reputation);

            // Idempotency record persisted.
            Assert.IsTrue(h.Save.Store.ContainsKey(ResultsSessionService.AppliedRewardsModuleKey));
        }

        [Test]
        public void Apply_Twice_DoesNotDoubleRewards()
        {
            var h = new Harness(Sales(gold: 100, exc: 2));
            h.Run();
            h.Run();

            Assert.AreEqual(2, h.Summaries.Count, "Both runs emit a summary…");
            Assert.AreEqual(100, h.DayProgress.State.Gold, "…but balances mutate only once.");
            Assert.AreEqual(2, h.DayProgress.State.Reputation);
            Assert.IsTrue(h.Summaries[1].AlreadyApplied);
            Assert.IsFalse(h.Summaries[0].AlreadyApplied);
        }

        [Test]
        public void Restart_NewServiceOverSameSaveStore_DoesNotDouble()
        {
            // First session: applies day 1.
            var first = new Harness(Sales(gold: 100, exc: 1));
            first.Run();
            Assert.AreEqual(100, first.DayProgress.State.Gold);

            // Restart: brand-new service + day-progress, but Save store carries over the applied record
            // *and* the last day result. Pre-seed the second day-progress with the persisted balances.
            var second = new Harness(sharedStore: first.Save.Store);
            second.DayProgress.State.Gold = first.DayProgress.State.Gold;
            second.DayProgress.State.Reputation = first.DayProgress.State.Reputation;
            second.Run();

            Assert.AreEqual(100, second.DayProgress.State.Gold,
                "Restart on Results must not re-apply rewards.");
            Assert.IsTrue(second.Summaries[0].AlreadyApplied);
        }

        [Test]
        public void AdvanceToNextDay_DelegatesToDayProgress_AndTriggersTransition()
        {
            var h = new Harness(Sales());
            h.Run();
            h.Advance();

            Assert.AreEqual(1, h.DayProgress.AdvanceCallCount);
            Assert.AreEqual(2, h.DayProgress.State.CurrentDay);
            Assert.AreEqual(DayPhase.Morning, h.DayProgress.State.CurrentPhase);
            Assert.AreEqual(1, h.SceneTransition.TransitionCount);
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
            Assert.AreEqual(5, s.SalesCount);
            Assert.AreEqual(4, s.ExcellentCount);
            Assert.AreEqual(1, s.FailedCount);
        }
    }
}
