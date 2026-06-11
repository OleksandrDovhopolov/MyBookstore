using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.Domain;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Game.Configs.Models;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesSessionServiceTests
    {
        // --- arrange helpers ---

        private static BookConfig Sci(string id, int price = 80) => new()
        {
            Id = id, Genre = "sci-fi", BasePrice = price,
            Tags = new[] { "space", "survival" },
            Mood = new[] { "smart" }
        };

        private static BookConfig Off(string id) => new()
        {
            Id = id, Genre = "romance", BasePrice = 40,
            Tags = new[] { "summer" },
            Mood = new[] { "warm" }
        };

        private static RequestConfig SciReq(string id) => new()
        {
            Id = id, Text = $"запрос {id}",
            DesiredGenres = new[] { "sci-fi" },
            DesiredTags = new[] { "space", "survival" },
            DesiredMood = new[] { "smart" },
            MaxPrice = 100,
            BaseRewardGold = 25
        };

        private static LocationConfig Uni() => new()
        {
            Id = "loc_uni", DemandGenres = new[] { "sci-fi" }, DemandTags = new[] { "space" }
        };

        private sealed class Harness
        {
            public FakeConfigsService Configs { get; } = new();
            public FakeSalesRandom Random { get; } = new();
            public SalesSessionService Service { get; }

            public List<RequestConfig> ActiveStarted { get; } = new();
            public List<RecommendationResult> Resolved { get; } = new();
            public List<PassiveSaleEvent> Passive { get; } = new();
            public List<SalesDayResult> DayCompleted { get; } = new();

            public Harness(IReadOnlyList<BookConfig> books, IReadOnlyList<RequestConfig> requests, LocationConfig location)
            {
                Configs.SetAll(books);
                Configs.SetAll(requests);
                Configs.SetAll(new[] { location });

                Service = new SalesSessionService(
                    Configs,
                    new DefaultSalesSetupProvider(Configs),
                    new RecommendationScoringService(),
                    new DefaultPassiveSaleSelector(),
                    Random);

                Service.ActiveRequestStarted += r => ActiveStarted.Add(r);
                Service.RecommendationResolved += r => Resolved.Add(r);
                Service.PassiveSaleHappened += e => Passive.Add(e);
                Service.DayCompleted += r => DayCompleted.Add(r);
            }

            public void StartDay(int day = 1)
                => Service.StartDayAsync(day, CancellationToken.None).GetAwaiter().GetResult();
        }

        // --- tests ---

        [Test]
        public void StartDay_EmitsFirstActiveRequest_AndPopulatesShelf()
        {
            var h = new Harness(
                books: new[] { Sci("b1"), Sci("b2"), Sci("b3") },
                requests: new[] { SciReq("r1"), SciReq("r2") },
                location: Uni());

            // никаких пассивных — день не начинался
            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();

            Assert.AreEqual(1, h.ActiveStarted.Count);
            Assert.AreEqual("r1", h.ActiveStarted[0].Id);
            Assert.AreEqual(3, h.Service.State.Shelf.Count);
            Assert.AreEqual(0, h.DayCompleted.Count);
        }

        [Test]
        public void RecommendBook_MatchingBook_EmitsRecommendation_And_AdvancesQueue()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // оба тика — без пассивных
            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(1, h.Resolved.Count);
            Assert.AreEqual(RecommendationTier.Excellent, h.Resolved[0].Tier);
            Assert.AreEqual(ShelfBookState.SoldOut, h.Service.State.Shelf[0].State);
            Assert.Greater(h.Service.AccumulatedResult.GoldEarned, 0);
            // advance to next request
            Assert.AreEqual(2, h.ActiveStarted.Count);
            Assert.AreEqual("r2", h.ActiveStarted[1].Id);
        }

        [Test]
        public void RecommendBook_AlreadySoldOut_IsNoOp()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");
            var resolvedBefore = h.Resolved.Count;

            // повторная попытка — игнор
            h.Service.RecommendBook("b1");

            Assert.AreEqual(resolvedBefore, h.Resolved.Count, "Sold-out книга не должна резолвиться повторно.");
        }

        [Test]
        public void SkipCurrentRequest_EmitsSkippedTier_NoGoldNoSale()
        {
            var h = new Harness(
                new[] { Sci("b1") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(1, h.Resolved.Count);
            Assert.AreEqual(RecommendationTier.Skipped, h.Resolved[0].Tier);
            Assert.IsNull(h.Resolved[0].BookId);
            Assert.AreEqual(0, h.Service.AccumulatedResult.GoldEarned);
            Assert.AreEqual(1, h.Service.AccumulatedResult.SkippedCount);
            Assert.AreEqual(ShelfBookState.Available, h.Service.State.Shelf[0].State, "Skip не трогает полку.");
        }

        [Test]
        public void PassiveSales_BothThresholdsTriggered_AndShelfHasMatch_FiresTwo()
        {
            // 3 матчевых книги, 3 запроса (хватит на серию)
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2"), Sci("b3") },
                new[] { SciReq("r1"), SciReq("r2"), SciReq("r3") },
                Uni());

            // На первом тике: NextDouble #1=0.5 < 0.6 (attempt #1 ок), NextDouble #2=0.3 < 0.4 (attempt #2 ок)
            // Range вернёт первый кандидат и так
            h.Random.EnqueueDouble(0.5, 0.3, /* следующие тики не нужны */ 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1"); // b1 sold_out по активной + 2 passive fire

            Assert.AreEqual(2, h.Passive.Count, "При двух матчевых порогах и наличии кандидатов — ровно 2 пассивные.");
        }

        [Test]
        public void PassiveSales_FirstThresholdFails_FiresZero()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // NextDouble #1 = 0.7 >= 0.6 → попытка #1 даже не запускается
            h.Random.EnqueueDouble(0.7, 0.7);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(0, h.Passive.Count);
        }

        [Test]
        public void PassiveSales_FirstSucceeds_SecondThresholdFails_FiresOne()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // attempt #1: 0.3 < 0.6 → запускаем, успех; attempt #2: 0.7 >= 0.4 → не запускаем
            h.Random.EnqueueDouble(0.3, 0.7, 0.99, 0.99);

            h.StartDay();
            h.Service.RecommendBook("b1");

            Assert.AreEqual(1, h.Passive.Count);
        }

        [Test]
        public void PassiveSales_NoMatchingBooksOnShelf_FiresZero_EvenIfThresholdsPass()
        {
            // На полке только romance — Uni demand'у не матчит
            var h = new Harness(
                new[] { Off("o1"), Off("o2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // На активный запрос нечего предложить (romance vs sci-fi) — Skip, и оба порога «успешны»
            h.Random.EnqueueDouble(0.0, 0.0, 0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(0, h.Passive.Count, "Без матча на полке пассивная не срабатывает даже при удачных порогах.");
        }

        [Test]
        public void DayCompleted_FiresOnce_AfterLastActiveResolved()
        {
            // 2 запроса, по skip каждому — день завершён
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            h.Random.EnqueueDouble(0.99, 0.99, 0.99, 0.99);

            h.StartDay();
            h.Service.SkipCurrentRequest();
            h.Service.SkipCurrentRequest();

            Assert.AreEqual(1, h.DayCompleted.Count);
            Assert.IsTrue(h.Service.State.DayCompleted);
            // После DayCompleted ActiveRequestStarted больше не эмитится сверх 2 запросов
            Assert.AreEqual(2, h.ActiveStarted.Count);
        }

        [Test]
        public void ActiveQueue_LimitedTo_DefaultSize()
        {
            // 10 запросов в конфиге, но в день берётся ровно DefaultActiveQueueSize (5)
            var requests = Enumerable.Range(1, 10).Select(i => SciReq($"r{i}")).ToArray();
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2"), Sci("b3"), Sci("b4"), Sci("b5") },
                requests,
                Uni());

            h.Random.EnqueueDouble(Enumerable.Repeat(0.99, 50).ToArray());

            h.StartDay();
            for (var i = 0; i < SalesSessionService.DefaultActiveQueueSize; i++)
                h.Service.SkipCurrentRequest();

            Assert.AreEqual(SalesSessionService.DefaultActiveQueueSize, h.ActiveStarted.Count);
            Assert.AreEqual(1, h.DayCompleted.Count);
        }

        [Test]
        public void StartDay_NoBooks_NoRequests_CompletesImmediately()
        {
            var h = new Harness(
                books: new BookConfig[0],
                requests: new RequestConfig[0],
                location: Uni());

            h.StartDay();

            Assert.AreEqual(0, h.ActiveStarted.Count);
            Assert.AreEqual(1, h.DayCompleted.Count, "Пустая очередь → день сразу завершён.");
        }

        [Test]
        public void EventOrder_ActiveResolved_Before_PassiveFired_Before_NextRequest()
        {
            var h = new Harness(
                new[] { Sci("b1"), Sci("b2") },
                new[] { SciReq("r1"), SciReq("r2") },
                Uni());

            // Одна пассивная после первого активного
            h.Random.EnqueueDouble(0.3, 0.7, 0.99, 0.99);

            // лента событий
            var log = new List<string>();
            h.Service.RecommendationResolved += _ => log.Add("resolved");
            h.Service.PassiveSaleHappened += _ => log.Add("passive");
            h.Service.ActiveRequestStarted += _ => log.Add("started");

            h.StartDay();              // started r1
            h.Service.RecommendBook("b1"); // resolved → passive → started r2

            // первая запись — started (r1) случилась в StartDay
            CollectionAssert.AreEqual(new[] { "started", "resolved", "passive", "started" }, log);
        }
    }
}
