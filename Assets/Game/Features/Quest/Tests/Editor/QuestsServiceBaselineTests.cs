using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Quest.API;
using Game.Quest.Services;
using Game.Quest.Tests.Editor.Fakes;
using Game.SalesStats.API;
using Game.SalesStats.Conditions;
using Game.SalesStats.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Quest.Tests.Editor
{
    /// <summary>
    /// 4b integration on REAL SalesStatsService + QuestsService: sales conditions count "since task start".
    /// </summary>
    public sealed class QuestsServiceBaselineTests
    {
        private const string FantasyBook = "book_fantasy";
        private const string FarBeach = "far_beach";
        private const string Ct = "ct";

        private static JObject Sales(string type, int min, string locationId = null)
        {
            var node = new JObject { ["type"] = type, ["genre"] = BookGenre.Fantasy.ToConfigValue(), ["min"] = min };
            if (locationId != null) node["locationId"] = locationId;
            return node;
        }

        private static QuestConfig QuestCfg(string id, JObject completion)
            => new QuestConfig
            {
                Id = id, Type = "story",
                Tasks = new[] { new QuestTaskConfig { Id = 1, CompletionConditions = completion } }
            };

        private sealed class Harness
        {
            public SalesStatsService Sales;
            public List<IConditionFactory> Factories;
            public FakeConfigsService Configs;
            public FakeSaveService Save;
            public FakeQuestsRepository Repo;
            public FakeDayProgress DayProgress;
            public bool UseDayProgress = true;

            public QuestsService NewQuests()
            {
                var parser = new ConditionParser(new ConditionFactoryRegistry(Factories));
                return new QuestsService(Save, Configs, parser, repository: Repo, sales: Sales,
                    dayProgress: UseDayProgress ? DayProgress : null,
                    allFactories: Factories, salesBaseline: Sales);
            }

            public void Sell(int day, int times)
            {
                for (var i = 0; i < times; i++)
                    Sales.RecordSold(FantasyBook, new Game.SalesStats.API.SaleContext(FarBeach, day));
            }
        }

        private static Harness Build(IConditionFactory extra, params QuestConfig[] quests)
        {
            var save = new FakeSaveService();
            var configs = new FakeConfigsService()
                .Add(new BookConfig { Id = FantasyBook, Genre = BookGenre.Fantasy.ToConfigValue() });
            foreach (var q in quests) configs.Add(q);

            var sales = new SalesStatsService(save, new SaveBackedSalesStatsRepository(save), configs);
            sales.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            var factories = new List<IConditionFactory>
            {
                new SoldGenreConditionFactory(sales),
                new SoldGenreAtLocationConditionFactory(sales),
                new SoldGenreInSingleDayConditionFactory(sales)
            };
            if (extra != null) factories.Add(extra);

            return new Harness
            {
                Sales = sales,
                Factories = factories,
                Configs = configs,
                Save = save,
                Repo = new FakeQuestsRepository(),
                DayProgress = new FakeDayProgress()
            };
        }

        private static QuestState State(QuestsService q, string id) => q.GetQuestState(id);

        [Test]
        public void SalesBeforeActivation_DoNotCount()
        {
            var h = Build(null, QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenre, 3)));
            h.Sell(1, 3); // BEFORE the quest exists/activates → lifetime Fantasy = 3

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult(); // head activates → baseline = 3
            Assert.AreEqual(QuestState.Active, State(quests, "q1"), "pre-existing sales must not satisfy the task");

            h.Sell(1, 3); // AFTER activation → scoped 3 ≥ 3 (RecordSold fires Changed → reevaluate)
            Assert.AreEqual(QuestState.Awarded, State(quests, "q1"));
        }

        [Test]
        public void SingleDay_BigDayBeforeActivation_DoesNotCount()
        {
            var h = Build(null, QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenreInSingleDay, 5)));
            h.Sell(1, 5); // big day BEFORE activation

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(QuestState.Active, State(quests, "q1"));

            h.Sell(2, 5); // a full day AFTER activation
            Assert.AreEqual(QuestState.Awarded, State(quests, "q1"));
        }

        [Test]
        public void MixedTree_SalesScoped_NonSalesNormal()
        {
            var flag = new MutableCondition(false);
            var completion = new JObject
            {
                ["all"] = new JArray { Sales(SalesConditionTypeIds.SoldGenre, 2), new JObject { ["type"] = "flag" } }
            };
            var h = Build(new FlagFactory(flag), QuestCfg("q1", completion));

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult(); // baseline = 0

            h.Sell(1, 2);                       // sales leaf met (scoped 2), flag still false
            Assert.AreEqual(QuestState.Active, State(quests, "q1"));

            flag.Met = true;
            h.Sell(1, 1);                       // any sale fires Changed → reevaluate; soldGenre stays met (3 >= 2)
            Assert.AreEqual(QuestState.Awarded, State(quests, "q1"));
        }

        [Test]
        public void Baseline_SurvivesRestart_NoResetNoDouble()
        {
            var h = Build(null, QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenre, 3)));

            var q1 = h.NewQuests();
            q1.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult(); // baseline = 0
            h.Sell(1, 2);                        // scoped 2/3 → Active
            q1.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult(); // persist Active + baseline(0)
            Assert.AreEqual(QuestState.Active, State(q1, "q1"));

            var q2 = h.NewQuests();              // same sales (lifetime 2) + same repo
            q2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            // baseline restored (0), live=2 → scoped 2 (progress preserved, not reset to 0, not doubled to 4)
            Assert.AreEqual(QuestState.Active, State(q2, "q1"));

            h.Sell(1, 1);                        // scoped 3 → award
            Assert.AreEqual(QuestState.Awarded, State(q2, "q1"));
        }

        [Test]
        public void ActiveSalesQuest_SavesCompactLocationBaselineOnly()
        {
            var h = Build(null,
                QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenreAtLocation, 10, FarBeach)));
            h.Sell(1, 2);
            h.Sales.RecordSold(FantasyBook, new SaleContext("other_location", 1));

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            quests.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var baseline = h.Repo.Stored.Active["q1"].TaskBaseline[1];
            Assert.IsNull(baseline.SoldByGenre);
            Assert.IsNull(baseline.SoldByDayGenre);
            Assert.IsNull(baseline.SoldInSingleDayGenre);
            Assert.IsTrue(baseline.SoldByLocationGenre.ContainsKey(FarBeach));
            Assert.IsFalse(baseline.SoldByLocationGenre.ContainsKey("other_location"));
            Assert.AreEqual(2, baseline.SoldByLocationGenre[FarBeach][BookGenre.Fantasy.ToConfigValue()]);
        }

        [Test]
        public void MixedTree_BuildsBaselinePlanForNestedSalesLeaves()
        {
            var completion = new JObject
            {
                ["all"] = new JArray
                {
                    Sales(SalesConditionTypeIds.SoldGenreAtLocation, 10, FarBeach),
                    new JObject { ["not"] = Sales(SalesConditionTypeIds.SoldGenre, 99) }
                }
            };
            var h = Build(null, QuestCfg("q1", completion));
            h.Sell(1, 3);

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            quests.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var baseline = h.Repo.Stored.Active["q1"].TaskBaseline[1];
            Assert.AreEqual(3, baseline.SoldByGenre[BookGenre.Fantasy.ToConfigValue()]);
            Assert.AreEqual(3, baseline.SoldByLocationGenre[FarBeach][BookGenre.Fantasy.ToConfigValue()]);
        }

        [Test]
        public void SingleDayCompactBaseline_SurvivesRestart()
        {
            var h = Build(null, QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenreInSingleDay, 3)));
            h.Sell(1, 5);
            h.DayProgress.Current.CurrentDay = 2;

            var q1 = h.NewQuests();
            q1.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            q1.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var baseline = h.Repo.Stored.Active["q1"].TaskBaseline[1];
            Assert.IsNull(baseline.SoldByDayGenre);
            Assert.AreEqual(2, baseline.SoldInSingleDayGenre[BookGenre.Fantasy.ToConfigValue()].ActivationDay);
            Assert.AreEqual(0, baseline.SoldInSingleDayGenre[BookGenre.Fantasy.ToConfigValue()].ActivationDayCount);

            var q2 = h.NewQuests();
            q2.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            h.Sell(2, 2);
            Assert.AreEqual(QuestState.Active, State(q2, "q1"));

            h.DayProgress.Current.CurrentDay = 3;
            h.Sell(3, 3);
            Assert.AreEqual(QuestState.Awarded, State(q2, "q1"));
        }

        [Test]
        public void LegacySingleDayBaseline_DoesNotReconstructActivationDay()
        {
            var cfg = QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenreInSingleDay, 1));
            var h = Build(null, cfg);
            h.Sell(1, 5);
            h.Repo.Stored = new Game.Quest.Services.Persistence.SavedQuests
            {
                Active = new Dictionary<string, Game.Quest.Services.Persistence.SavedQuest>
                {
                    ["q1"] = new()
                    {
                        State = QuestState.Active,
                        Tasks = new Dictionary<int, QuestTaskState> { [1] = QuestTaskState.Active },
                        TaskBaseline = new Dictionary<int, SalesStatsBaselineDto>
                        {
                            [1] = new()
                            {
                                SoldByDayGenre = new Dictionary<int, Dictionary<string, int>>
                                {
                                    [1] = new() { [BookGenre.Fantasy.ToConfigValue()] = 5 }
                                }
                            }
                        }
                    }
                }
            };

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(QuestState.Active, State(quests, "q1"));

            h.Sell(2, 1);
            Assert.AreEqual(QuestState.Awarded, State(quests, "q1"));
        }

        [Test]
        public void SingleDayWithoutDayProgress_UsesLegacyBaselineFallback()
        {
            var h = Build(null, QuestCfg("q1", Sales(SalesConditionTypeIds.SoldGenreInSingleDay, 5)));
            h.UseDayProgress = false;
            h.Sell(1, 5);

            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            quests.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var baseline = h.Repo.Stored.Active["q1"].TaskBaseline[1];
            Assert.IsNull(baseline.SoldInSingleDayGenre);
            Assert.IsNotNull(baseline.SoldByDayGenre);
            Assert.AreEqual(5, baseline.SoldByDayGenre[1][BookGenre.Fantasy.ToConfigValue()]);
        }

        [Test]
        public void Suspend_DefersReeval_BaselineIncludesSalesDuringSuspend()
        {
            // Chain: head (flag) → q_sales (soldGenre). The successor activates only when the head completes.
            var flag = new MutableCondition(false);
            var head = QuestCfg("head", new JObject { ["all"] = new JArray { new JObject { ["type"] = "flag" } } });
            head.NextQuestIds = new[] { "q_sales" };
            var qSales = QuestCfg("q_sales", Sales(SalesConditionTypeIds.SoldGenre, 3));

            var h = Build(new FlagFactory(flag), head, qSales);
            var quests = h.NewQuests();
            quests.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(QuestState.Active, State(quests, "head"));
            Assert.AreEqual(QuestState.Pending, State(quests, "q_sales")); // successor waits for its predecessor

            // Mimics a day commit: complete the head AND record the day's sales while re-eval is suspended.
            using (quests.SuspendReevaluation())
            {
                flag.Met = true;   // would complete head → activate q_sales
                h.Sell(1, 3);      // RecordSold fires Changed, but reeval is suspended → nothing reacts yet
                Assert.AreEqual(QuestState.Active, State(quests, "head"), "no reeval while suspended");
                Assert.AreEqual(QuestState.Pending, State(quests, "q_sales"));
            }

            // One reeval on dispose: head completes → q_sales activates → its baseline is captured NOW, so it
            // includes the 3 sales recorded during the suspension → those sales do NOT count toward q_sales.
            Assert.AreEqual(QuestState.Awarded, State(quests, "head"));
            Assert.AreEqual(QuestState.Active, State(quests, "q_sales"));

            h.Sell(2, 3); // only sales AFTER activation count → now it awards
            Assert.AreEqual(QuestState.Awarded, State(quests, "q_sales"));
        }

        private sealed class FlagFactory : IConditionFactory
        {
            private readonly ICondition _condition;
            public FlagFactory(ICondition condition) => _condition = condition;
            public string Type => "flag";
            public ICondition Create(JObject node) => _condition;
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
