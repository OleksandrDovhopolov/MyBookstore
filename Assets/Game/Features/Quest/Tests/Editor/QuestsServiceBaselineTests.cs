using System.Collections.Generic;
using System.Threading;
using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Configs.Models;
using Game.Quest.API;
using Game.Quest.Services;
using Game.Quest.Tests.Editor.Fakes;
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

            public QuestsService NewQuests()
            {
                var parser = new ConditionParser(new ConditionFactoryRegistry(Factories));
                return new QuestsService(Save, Configs, parser, repository: Repo, sales: Sales,
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

            return new Harness { Sales = sales, Factories = factories, Configs = configs, Save = save, Repo = new FakeQuestsRepository() };
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

        private sealed class FlagFactory : IConditionFactory
        {
            private readonly ICondition _condition;
            public FlagFactory(ICondition condition) => _condition = condition;
            public string Type => "flag";
            public ICondition Create(JObject node) => _condition;
        }
    }
}
