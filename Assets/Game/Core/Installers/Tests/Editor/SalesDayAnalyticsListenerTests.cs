using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Analytics;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.Bootstrap.Analytics;
using NUnit.Framework;

namespace Game.Bootstrap.Tests.Editor
{
    public sealed class SalesDayAnalyticsListenerTests
    {
        [Test]
        public void DayCompleted_ReportsPassiveSalesCount()
        {
            var sales = new FakeSalesDayController();
            var analytics = new RecordingAnalyticsService();
            var listener = new SalesDayAnalyticsListener(sales, analytics);
            listener.Start();

            var result = new SalesDayResult { Day = 1, LocationId = "loc_park" };
            result.PassiveSales.Add(new PassiveSaleEvent("book_1", 12));
            result.PassiveSales.Add(new PassiveSaleEvent("book_2", 8));

            sales.RaiseDayCompleted(result);

            var evt = analytics.Events.Single(e => e.Name == AnalyticsEventNames.DayCompleted);
            Assert.That(evt.Parameters[AnalyticsParameterNames.PassiveSalesCount], Is.EqualTo(2));
            listener.Dispose();
        }

        [Test]
        public void PassiveMisses_ResetOnNextDayStarted()
        {
            var sales = new FakeSalesDayController();
            var analytics = new RecordingAnalyticsService();
            var listener = new SalesDayAnalyticsListener(sales, analytics);
            listener.Start();

            sales.RaiseDayStarted(1, "loc_park");
            sales.RaiseCustomerPassivePurchaseFailed("Fantasy");
            sales.RaiseDayCompleted(new SalesDayResult { Day = 1, LocationId = "loc_park" });
            sales.RaiseDayStarted(2, "loc_port");
            sales.RaiseDayCompleted(new SalesDayResult { Day = 2, LocationId = "loc_port" });

            var dayCompleted = analytics.Events
                .Where(e => e.Name == AnalyticsEventNames.DayCompleted)
                .ToList();

            Assert.That(dayCompleted[0].Parameters[AnalyticsParameterNames.PassiveMisses], Is.EqualTo(1));
            Assert.That(dayCompleted[1].Parameters[AnalyticsParameterNames.PassiveMisses], Is.EqualTo(0));
            listener.Dispose();
        }

        [Test]
        public void SkippedRecommendation_EmitsNumericTierAndOmitsBookId()
        {
            var sales = new FakeSalesDayController();
            var analytics = new RecordingAnalyticsService();
            var listener = new SalesDayAnalyticsListener(sales, analytics);
            listener.Start();

            sales.RaiseRecommendationResolved(RecommendationResult.Skipped("req_1"));

            var evt = analytics.Events.Single(e => e.Name == AnalyticsEventNames.ActiveSaleCompleted);
            Assert.That(evt.Parameters[AnalyticsParameterNames.RequestId], Is.EqualTo("req_1"));
            Assert.That(evt.Parameters[AnalyticsParameterNames.Tier], Is.EqualTo((int)RecommendationTier.Skipped));
            Assert.That(evt.Parameters.ContainsKey(AnalyticsParameterNames.BookId), Is.False);
            listener.Dispose();
        }

        private sealed class FakeSalesDayController : ISalesDayController
        {
            public int Day => 0;
            public string LocationId => null;
            public SalesShelf Shelf { get; } = new();
            public SalesDayResult AccumulatedResult { get; } = new();
            public ActiveRequestRuntime CurrentRequest => null;
            public SalesDayPhase Phase => SalesDayPhase.Running;
            public bool IsDayCompleted => false;

            public event Action DayReadyToClose { add { } remove { } }
            public event Action<ActiveRequestRuntime> ActiveRequestStarted { add { } remove { } }
            public event Action<int, string> DayStarted;
            public event Action<Customer, DialoguePayload> DialogueStarted { add { } remove { } }
            public event Action<RecommendationResult> RecommendationResolved;
            public event Action<PassiveSaleEvent> PassiveSaleHappened { add { } remove { } }
            public event Action<Customer, RecommendationResult> CustomerRecommendationResolved { add { } remove { } }
            public event Action<Customer, PassiveSaleEvent> CustomerPassiveSaleHappened { add { } remove { } }
            public event Action<Customer, CustomerCommentPayload> CustomerCommented { add { } remove { } }
            public event Action<Customer, string> CustomerPassivePurchaseFailed;
            public event Action<Customer, int> CustomerPurchaseCompleted { add { } remove { } }
            public event Action<Customer> CustomerThoughtBubbleHidden { add { } remove { } }
            public event Action<SalesDayResult> DayCompleted;
            public event Action<Customer> CustomerPhaseChanged { add { } remove { } }
            public event Action<Customer, string> BookReserved { add { } remove { } }
            public event Action<Customer, string> BookReleased { add { } remove { } }
            public event Action ShelfChanged { add { } remove { } }

            public UniTask StartDayAsync(int day, CancellationToken ct) => UniTask.CompletedTask;
            public void Tick(float dt) { }
            public void RecommendBook(string bookId) { }
            public void SkipCurrentRequest() { }
            public void CompleteDialogue() { }
            public void ConcludeDay() { }
            public void ForceCompleteDay(bool zeroOut) { }

            public void RaiseDayStarted(int day, string locationId) => DayStarted?.Invoke(day, locationId);
            public void RaiseRecommendationResolved(RecommendationResult result) => RecommendationResolved?.Invoke(result);
            public void RaiseCustomerPassivePurchaseFailed(string genre) => CustomerPassivePurchaseFailed?.Invoke(null, genre);
            public void RaiseDayCompleted(SalesDayResult result) => DayCompleted?.Invoke(result);
        }
    }
}
