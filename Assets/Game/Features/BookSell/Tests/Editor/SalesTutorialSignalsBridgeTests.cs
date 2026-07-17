using System;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.UI;
using MessagePipe;
using NUnit.Framework;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesTutorialSignalsBridgeTests
    {
        [Test]
        public void CustomerPhaseChanged_PublishesPrimitiveSignal()
        {
            var h = new Harness();
            h.Bridge.Start();

            h.Sales.RaiseCustomerPhaseChanged(new Customer(
                "customer_1",
                Array.Empty<ICustomerStep>(),
                characterId: "eddi"));

            Assert.AreEqual("customer_1", h.PhasePublisher.Last.CustomerId);
            Assert.AreEqual("eddi", h.PhasePublisher.Last.CharacterId);
            Assert.AreEqual("Spawned", h.PhasePublisher.Last.Phase);
        }

        [Test]
        public void PassiveSale_PublishesFirstMatchedGenreAndBookId()
        {
            var h = new Harness();
            h.Bridge.Start();
            var sale = new PassiveSaleEvent("book_fact", 10, new[] { "Fact", "Travel" });

            h.Sales.RaiseCustomerPassiveSaleHappened(new Customer(
                "customer_1",
                Array.Empty<ICustomerStep>(),
                characterId: "eddi"), sale);

            Assert.AreEqual("customer_1", h.SalePublisher.Last.CustomerId);
            Assert.AreEqual("eddi", h.SalePublisher.Last.CharacterId);
            Assert.AreEqual("Fact", h.SalePublisher.Last.Genre);
            Assert.AreEqual("book_fact", h.SalePublisher.Last.BookId);
        }

        [Test]
        public void PassiveSale_PrefersResolvedGenre()
        {
            var h = new Harness();
            h.Bridge.Start();
            var sale = new PassiveSaleEvent(
                "book_fact",
                10,
                new[] { "Legacy" },
                resolvedGenre: "Fact");

            h.Sales.RaiseCustomerPassiveSaleHappened(new Customer(
                "customer_1",
                Array.Empty<ICustomerStep>(),
                characterId: "eddi"), sale);

            Assert.AreEqual("Fact", h.SalePublisher.Last.Genre);
        }

        [Test]
        public void PassivePurchaseFailed_PublishesFailedGenre()
        {
            var h = new Harness();
            h.Bridge.Start();

            h.Sales.RaiseCustomerPassivePurchaseFailed(new Customer(
                "customer_1",
                Array.Empty<ICustomerStep>(),
                characterId: "eddi"), "Travel");

            Assert.AreEqual("customer_1", h.FailPublisher.Last.CustomerId);
            Assert.AreEqual("eddi", h.FailPublisher.Last.CharacterId);
            Assert.AreEqual("Travel", h.FailPublisher.Last.Genre);
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var h = new Harness();
            h.Bridge.Start();
            h.Bridge.Dispose();

            h.Sales.RaiseCustomerPassivePurchaseFailed(new Customer(
                "customer_1",
                Array.Empty<ICustomerStep>(),
                characterId: "eddi"), "Travel");

            Assert.AreEqual(0, h.FailPublisher.PublishCount);
        }

        private sealed class Harness
        {
            public FakeSalesDayController Sales { get; } = new();
            public RecordingPublisher<SalesCustomerPhaseChanged> PhasePublisher { get; } = new();
            public RecordingPublisher<SalesPassiveSaleHappened> SalePublisher { get; } = new();
            public RecordingPublisher<SalesPassivePurchaseFailed> FailPublisher { get; } = new();
            public SalesTutorialSignalsBridge Bridge { get; }

            public Harness()
            {
                Bridge = new SalesTutorialSignalsBridge(Sales, PhasePublisher, SalePublisher, FailPublisher);
            }
        }

        private sealed class RecordingPublisher<T> : IPublisher<T>
        {
            public T Last { get; private set; }
            public int PublishCount { get; private set; }

            public void Publish(T message)
            {
                Last = message;
                PublishCount++;
            }
        }

        private sealed class FakeSalesDayController : ISalesDayController
        {
            public int Day => 1;
            public string LocationId => "loc_downtown";
            public SalesShelf Shelf => null;
            public SalesDayResult AccumulatedResult { get; } = new();
            public ActiveRequestRuntime CurrentRequest => null;
            public SalesDayPhase Phase => SalesDayPhase.Running;
            public bool IsDayCompleted => false;

            public event Action DayReadyToClose;
            public event Action<ActiveRequestRuntime> ActiveRequestStarted;
            public event Action<Customer, DialoguePayload> DialogueStarted;
            public event Action<RecommendationResult> RecommendationResolved;
            public event Action<PassiveSaleEvent> PassiveSaleHappened;
            public event Action<Customer, RecommendationResult> CustomerRecommendationResolved;
            public event Action<Customer, PassiveSaleEvent> CustomerPassiveSaleHappened;
            public event Action<Customer, CustomerCommentPayload> CustomerCommented;
            public event Action<Customer, string> CustomerPassivePurchaseFailed;
            public event Action<Customer, int> CustomerPurchaseCompleted;
            public event Action<Customer> CustomerThoughtBubbleHidden;
            public event Action<SalesDayResult> DayCompleted;
            public event Action<Customer> CustomerPhaseChanged;
            public event Action<Customer, string> BookReserved;
            public event Action<Customer, string> BookReleased;
            public event Action ShelfChanged;

            public UniTask StartDayAsync(int day, System.Threading.CancellationToken ct) => UniTask.CompletedTask;
            public void Tick(float dt) { }
            public void RecommendBook(string bookId) { }
            public void SkipCurrentRequest() { }
            public void CompleteDialogue() { }
            public void ConcludeDay() { }
            public void ForceCompleteDay(bool zeroOut) { }

            public void RaiseCustomerPhaseChanged(Customer customer) => CustomerPhaseChanged?.Invoke(customer);
            public void RaiseCustomerPassiveSaleHappened(Customer customer, PassiveSaleEvent sale)
                => CustomerPassiveSaleHappened?.Invoke(customer, sale);
            public void RaiseCustomerPassivePurchaseFailed(Customer customer, string genre)
                => CustomerPassivePurchaseFailed?.Invoke(customer, genre);
        }
    }
}
