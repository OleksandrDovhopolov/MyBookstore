using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Book.Sell.Services;
using Book.Sell.Tests.Editor.Fakes;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.DayCycle.Day;
using Game.Inventory.API;
using Game.Preparation.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Book.Sell.Tests.Editor
{
    public sealed class SalesDayControllerTests
    {
        // ----- builders -----

        private static Customer Passive(string id)
            => new(id, new ICustomerStep[] { new ApproachStep(), new PassivePurchaseStep(), new LeaveStep() });

        private static Customer Active(string id, RequestConfig req)
            => new(id, new ICustomerStep[] { new ApproachStep(), new ActiveRequestStep(req), new LeaveStep() });

        // Active request with the full closing tail, so CompletePurchaseStep actually runs after the
        // recommendation resolves (needed to assert the visit-completion event).
        private static Customer ActiveWithCompletion(string id, RequestConfig req)
            => new(id, new ICustomerStep[]
            {
                new ApproachStep(), new ActiveRequestStep(req), new CompletePurchaseStep(), new LeaveStep()
            });

        private static SalesDayController Build(
            BookConfig[] books, RequestConfig[] requests, LocationConfig location, IReadOnlyList<Customer> customers,
            SalesTuning tuning = null,
            ISalesShelfStateService shelfState = null,
            RecordingInventoryService inventory = null,
            ISoldBookCommitter soldBookCommitter = null)
        {
            var configs = new FakeConfigsService();
            configs.SetAll(books);
            configs.SetAll(requests);
            configs.SetAll(new[] { location });

            inventory ??= RecordingInventoryService.WithBooks(books);
            soldBookCommitter ??= new SoldBookCommitter(inventory, shelfState);

            return new SalesDayController(
                configs,
                new DefaultSalesSetupProvider(configs),
                new RecommendationScoringService(),
                SalesTestKit.AlwaysHitPassiveSelector(),
                new FakeSalesRandom(),
                new StubCustomerSpawner(customers),
                new InteractionLock(),
                tuning ?? SalesTestKit.FastTuning(),
                soldBookCommitter: soldBookCommitter);
        }

        private static void StartDay(SalesDayController c)
            => c.StartDayAsync(1, CancellationToken.None).GetAwaiter().GetResult();

        // Drives the day until it stops being Running (i.e. reaches ReadyToClose). The day no longer
        // auto-completes; tests assert at ReadyToClose, then call ConcludeDay() when they need the
        // published result.
        private static void Run(SalesDayController c, int maxTicks = 200)
        {
            for (var i = 0; i < maxTicks && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private static void DriveUntilActive(SalesDayController c, int maxTicks = 50)
        {
            for (var i = 0; i < maxTicks && c.CurrentRequest == null && c.Phase == SalesDayPhase.Running; i++) c.Tick(0.1f);
        }

        private sealed class RecordingInventoryService : IInventoryService
        {
            private readonly List<InventoryItem> _items = new();

            public List<(string ItemId, int Amount)> RemoveCalls { get; } = new();
            public List<string> OperationLog { get; }

            public event System.Action<InventoryChangeEvent> Changed;

            public RecordingInventoryService(List<string> operationLog = null)
            {
                OperationLog = operationLog ?? new List<string>();
            }

            public static RecordingInventoryService WithBooks(IEnumerable<BookConfig> books)
            {
                var inventory = new RecordingInventoryService();
                if (books == null) return inventory;

                foreach (var book in books)
                {
                    if (book == null || string.IsNullOrEmpty(book.Id)) continue;
                    inventory.Seed(book.Id, InventoryCategories.Book);
                }
                return inventory;
            }

            public RecordingInventoryService Seed(string itemId, string categoryId, int count = 1)
            {
                if (!_items.Any(i => i.ItemId == itemId))
                    _items.Add(new InventoryItem(itemId, categoryId, count));
                return this;
            }

            public IReadOnlyList<InventoryItem> GetAll() => _items.ToList();

            public IReadOnlyList<InventoryItem> GetByCategory(string categoryId)
                => _items.Where(i => i.CategoryId == categoryId).ToList();

            public bool Has(string itemId) => GetCount(itemId) > 0;

            public int GetCount(string itemId)
                => _items.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0;

            public UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct)
            {
                Seed(itemId, categoryId, amount);
                Changed?.Invoke(new InventoryChangeEvent(categoryId, itemId, InventoryChangeKind.Added, amount));
                return UniTask.CompletedTask;
            }

            public UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct)
            {
                foreach (var item in items)
                    Seed(item.ItemId, item.CategoryId, item.Count);
                return UniTask.CompletedTask;
            }

            public UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct)
            {
                RemoveCalls.Add((itemId, amount));
                OperationLog.Add($"inventory:{itemId}");

                var existing = _items.FirstOrDefault(i => i.ItemId == itemId);
                if (existing == null || existing.Count < amount)
                    return UniTask.FromResult(false);

                _items.Remove(existing);
                var newCount = existing.Count - amount;
                if (newCount > 0)
                    _items.Add(new InventoryItem(itemId, existing.CategoryId, newCount));

                Changed?.Invoke(new InventoryChangeEvent(existing.CategoryId, itemId, InventoryChangeKind.Removed, newCount));
                return UniTask.FromResult(true);
            }
        }

        private sealed class OrderedShelfStateService : ISalesShelfStateService
        {
            private readonly List<string> _operationLog;

            public OrderedShelfStateService(List<string> operationLog)
            {
                _operationLog = operationLog;
            }

            public List<string> Sold { get; } = new();
            public IReadOnlyList<string> ShelfBookIds => Array.Empty<string>();
            public SalesShelfState CurrentState { get; } = new();
            public bool IsSold(string bookId) => Sold.Contains(bookId);
            public UniTask SetShelfAsync(IReadOnlyList<string> bookIds, CancellationToken ct) => UniTask.CompletedTask;

            public UniTask MarkSoldAsync(string bookId, CancellationToken ct)
            {
                _operationLog.Add($"shelf:{bookId}");
                if (!string.IsNullOrEmpty(bookId) && !Sold.Contains(bookId))
                    Sold.Add(bookId);
                return UniTask.CompletedTask;
            }
        }

        private sealed class RecordingSoldBookCommitter : ISoldBookCommitter
        {
            public bool ResetCalled { get; private set; }
            public bool CommitCalled { get; private set; }
            public bool FlushCalled { get; private set; }

            public void Reset()
            {
                ResetCalled = true;
            }

            public void CommitSoldBook(string bookId, string source)
            {
                CommitCalled = true;
            }

            public UniTask FlushAsync(CancellationToken ct)
            {
                FlushCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private sealed class StaticPreparationInventoryProvider : IPreparationInventoryProvider
        {
            private readonly IReadOnlyList<BookConfig> _ownedBooks;

            public StaticPreparationInventoryProvider(IReadOnlyList<BookConfig> ownedBooks)
                => _ownedBooks = ownedBooks;

            public IReadOnlyList<BookConfig> GetOwnedBooks() => _ownedBooks;
        }

        private sealed class FakeDayProgressService : IDayProgressService
        {
            public event System.Action<DayProgressState> PhaseChanged;

            public DayProgressState Current { get; } = new();

            public UniTask<DayProgressState> LoadAsync(CancellationToken ct) => UniTask.FromResult(Current);

            public UniTask SetPhaseAsync(DayPhase phase, CancellationToken ct)
            {
                Current.CurrentPhase = phase;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask MarkCurrentDayCompletedAsync(CancellationToken ct)
            {
                Current.CompletedDays.Add(Current.CurrentDay);
                Current.CurrentPhase = DayPhase.Results;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask AdvanceToNextDayAsync(CancellationToken ct)
            {
                Current.CurrentDay++;
                Current.CurrentPhase = DayPhase.Morning;
                PhaseChanged?.Invoke(Current);
                return UniTask.CompletedTask;
            }

            public UniTask SaveAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        // ----- tests -----

        [Test]
        public void SoldBookCommitter_EmptyBookId_DoesNothing()
        {
            var shelfState = new RecordingShelfStateService();
            var inventory = new RecordingInventoryService().Seed("b1", InventoryCategories.Book);
            var committer = new SoldBookCommitter(inventory, shelfState);

            committer.CommitSoldBook(null, "test");
            committer.CommitSoldBook(string.Empty, "test");
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.IsEmpty(shelfState.Sold);
            CollectionAssert.IsEmpty(inventory.RemoveCalls);
        }

        [Test]
        public void SoldBookCommitter_MarksShelfBeforeRemovingInventory()
        {
            var operationLog = new List<string>();
            var shelfState = new OrderedShelfStateService(operationLog);
            var inventory = new RecordingInventoryService(operationLog).Seed("b1", InventoryCategories.Book);
            var committer = new SoldBookCommitter(inventory, shelfState);

            committer.CommitSoldBook("b1", "test");
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "shelf:b1", "inventory:b1" }, operationLog);
        }

        [Test]
        public void SoldBookCommitter_MissingInventoryService_LogsError()
        {
            var committer = new SoldBookCommitter(null, new RecordingShelfStateService());

            LogAssert.Expect(LogType.Error,
                "[Sales.Day] cannot remove sold book 'b1' from inventory (test): IInventoryService is not available.");

            committer.CommitSoldBook("b1", "test");
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void SoldBookCommitter_RemoveReturnsFalse_LogsError()
        {
            var inventory = new RecordingInventoryService();
            var committer = new SoldBookCommitter(inventory, new RecordingShelfStateService());

            LogAssert.Expect(LogType.Error, "[Sales.Day] sold book 'b1' was not present in inventory during test sale.");

            committer.CommitSoldBook("b1", "test");
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void SoldBookCommitter_Flush_CanBeCalledRepeatedly()
        {
            var inventory = new RecordingInventoryService().Seed("b1", InventoryCategories.Book);
            var committer = new SoldBookCommitter(inventory, new RecordingShelfStateService());

            committer.CommitSoldBook("b1", "test");
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            committer.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, inventory.RemoveCalls.Count);
        }

        [Test]
        public void DayCompleted_FlushesSoldBookCommitsBeforePublishing()
        {
            var committer = new RecordingSoldBookCommitter();
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") },
                soldBookCommitter: committer);

            var completedAfterFlush = false;
            c.DayCompleted += _ => completedAfterFlush = committer.FlushCalled;

            StartDay(c);
            Run(c);
            c.ConcludeDay();

            Assert.IsTrue(committer.ResetCalled);
            Assert.IsTrue(committer.CommitCalled);
            Assert.IsTrue(completedAfterFlush);
        }

        [Test]
        public void StartDay_NoCustomers_BecomesReadyToClose_ThenConcludes()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1") }, new RequestConfig[0],
                SalesTestKit.Location(), new List<Customer>());

            var readyToClose = false;
            var completed = false;
            c.DayReadyToClose += () => readyToClose = true;
            c.DayCompleted += _ => completed = true;

            StartDay(c);
            Run(c);

            // No customers → day is immediately closable, but does NOT auto-complete.
            Assert.IsTrue(readyToClose);
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsFalse(completed, "Day waits for the player to close the shop.");

            c.ConcludeDay();

            Assert.IsTrue(completed);
            Assert.IsTrue(c.IsDayCompleted);
        }

        [Test]
        public void SinglePassiveCustomer_BuysOneBook_ThenLeaves()
        {
            // Two books so the day ends via "all customers done" (one book remains),
            // not via "all sold out" — that lets the customer actually reach Done (CustomersServed).
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, passive, "One PassivePurchaseStep → one book bought.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(80, c.AccumulatedResult.GoldEarned);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed);
        }

        [Test]
        public void StartDay_RaisesShelfChanged_AfterShelfIsBuilt()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") },
                new RequestConfig[0],
                SalesTestKit.Location(),
                new List<Customer>());

            var changes = 0;
            c.ShelfChanged += () => changes++;

            StartDay(c);

            Assert.AreEqual(1, changes);
            Assert.AreEqual(2, c.Shelf.Books.Count);
        }

        [Test]
        public void RecommendBook_SuccessfulSale_RaisesShelfChanged()
        {
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            StartDay(c);
            DriveUntilActive(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            c.RecommendBook("b1");

            Assert.AreEqual(1, changes);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
        }

        [Test]
        public void ActiveSale_CountsAsPurchasedBook_CompletionFiresWithCountOne()
        {
            // Two books so the day ends via "all customers done" (b2 remains), letting the customer
            // reach CompletePurchase/Done after the active sale of b1.
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { ActiveWithCompletion("c1", reqA) });

            (Customer customer, int count)? completion = null;
            c.CustomerPurchaseCompleted += (cust, n) => completion = (cust, n);

            StartDay(c);
            DriveUntilActive(c);
            c.RecommendBook("b1");
            Run(c);

            Assert.IsTrue(completion.HasValue, "An active sale must trigger the visit-completion bubble.");
            Assert.AreEqual(1, completion.Value.count, "The active sale counts as one purchased book.");
        }

        [Test]
        public void SkippedActiveRequest_BuysNothing_NoCompletion()
        {
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { ActiveWithCompletion("c1", reqA) });

            var completionFired = false;
            c.CustomerPurchaseCompleted += (_, _) => completionFired = true;

            StartDay(c);
            DriveUntilActive(c);
            c.SkipCurrentRequest();
            Run(c);

            Assert.IsFalse(completionFired, "0 books bought → CompletePurchase is skipped, no completion bubble.");
        }

        [Test]
        public void RecommendBook_SuccessfulSale_MarksBookSoldInPersistentShelf()
        {
            var shelfState = new RecordingShelfStateService();
            var inventory = new RecordingInventoryService().Seed("b1", InventoryCategories.Book);
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) },
                shelfState: shelfState,
                inventory: inventory);

            StartDay(c);
            DriveUntilActive(c);

            c.RecommendBook("b1");

            CollectionAssert.Contains(shelfState.Sold, "b1");
            Assert.AreEqual(1, inventory.RemoveCalls.Count);
            Assert.AreEqual("b1", inventory.RemoveCalls[0].ItemId);
            Assert.AreEqual(1, inventory.RemoveCalls[0].Amount);
            Assert.IsFalse(inventory.Has("b1"));
        }

        [Test]
        public void SkipCurrentRequest_DoesNotRaiseShelfChanged()
        {
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            StartDay(c);
            DriveUntilActive(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            c.SkipCurrentRequest();

            Assert.AreEqual(0, changes);
            Assert.AreEqual(ShelfBookState.Available, c.Shelf.Find("b1").State);
        }

        [Test]
        public void SkipCurrentRequest_DoesNotMarkBookSoldInPersistentShelf()
        {
            var shelfState = new RecordingShelfStateService();
            var inventory = new RecordingInventoryService().Seed("b1", InventoryCategories.Book);
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) },
                shelfState: shelfState,
                inventory: inventory);

            StartDay(c);
            DriveUntilActive(c);

            c.SkipCurrentRequest();

            CollectionAssert.IsEmpty(shelfState.Sold);
            CollectionAssert.IsEmpty(inventory.RemoveCalls);
            Assert.IsTrue(inventory.Has("b1"));
        }

        [Test]
        public void PassiveSale_RaisesShelfChanged()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);

            var changes = 0;
            c.ShelfChanged += () => changes++;

            Run(c);

            Assert.AreEqual(1, changes);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
        }

        [Test]
        public void PassiveSale_MarksBookSoldInPersistentShelf()
        {
            var shelfState = new RecordingShelfStateService();
            var inventory = new RecordingInventoryService().Seed("b1", InventoryCategories.Book);
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") },
                shelfState: shelfState,
                inventory: inventory);

            StartDay(c);
            Run(c);

            CollectionAssert.Contains(shelfState.Sold, "b1");
            Assert.AreEqual(1, inventory.RemoveCalls.Count);
            Assert.AreEqual("b1", inventory.RemoveCalls[0].ItemId);
            Assert.AreEqual(1, inventory.RemoveCalls[0].Amount);
            Assert.IsFalse(inventory.Has("b1"));
        }

        [Test]
        public void RecommendBook_SoldBookMissingFromInventory_LogsError()
        {
            var shelfState = new RecordingShelfStateService();
            var inventory = new RecordingInventoryService();
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) },
                shelfState: shelfState,
                inventory: inventory);

            StartDay(c);
            DriveUntilActive(c);

            LogAssert.Expect(LogType.Error, "[Sales.Day] sold book 'b1' was not present in inventory during active sale.");

            c.RecommendBook("b1");

            CollectionAssert.Contains(shelfState.Sold, "b1");
            Assert.AreEqual(1, inventory.RemoveCalls.Count);
            Assert.AreEqual("b1", inventory.RemoveCalls[0].ItemId);
            Assert.AreEqual(1, inventory.RemoveCalls[0].Amount);
        }

        [Test]
        public void PreparationInventoryProvider_EmptyInventory_ReturnsEmptyList()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") });
            var provider = new DayProgressInventoryProvider(new RecordingInventoryService(), configs);

            LogAssert.Expect(LogType.Warning, "[Preparation.Inventory] inventory book category is empty - no owned books available.");

            var owned = provider.GetOwnedBooks();

            CollectionAssert.IsEmpty(owned);
        }

        [Test]
        public void PreparationInventoryProvider_ReturnsOnlyBooksOwnedInInventory()
        {
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { SalesTestKit.Book("b1"), SalesTestKit.Book("b2") });
            var inventory = new RecordingInventoryService().Seed("b2", InventoryCategories.Book);
            var provider = new DayProgressInventoryProvider(inventory, configs);

            var owned = provider.GetOwnedBooks();

            CollectionAssert.AreEqual(new[] { "b2" }, owned.Select(b => b.Id).ToArray());
        }

        [Test]
        public void PreparationSession_IncludesCatalogGenresWithZeroOwnedBooks()
        {
            var sciFi = SalesTestKit.Book("b1", genre: "sci-fi");
            var romance = SalesTestKit.Book("b2", genre: "romance");
            var configs = new FakeConfigsService();
            configs.SetAll(new[] { sciFi, romance });

            var service = new PreparationSessionService(
                new FakeSaveService(),
                new FakeDayProgressService(),
                new StaticPreparationInventoryProvider(new[] { sciFi }),
                new RecordingShelfStateService(),
                configs);

            var items = service.StartOrResumeAsync(CancellationToken.None).GetAwaiter().GetResult();

            var byGenre = items.ToDictionary(item => item.Genre, StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(1, byGenre["sci-fi"].Available);
            Assert.AreEqual(0, byGenre["romance"].Available);
            Assert.AreEqual(0, byGenre["romance"].Quantity);
        }

        [Test]
        public void PreparationSession_RandomizeAfterSales_FillsFromFullInventory()
        {
            var books = Enumerable.Range(1, 12)
                .Select(i => SalesTestKit.Book($"b{i}", genre: "sci-fi"))
                .ToArray();
            var configs = new FakeConfigsService();
            configs.SetAll(books);

            var shelfState = new RecordingShelfStateService();
            shelfState.SetShelfAsync(new[] { "b1", "b2", "b3", "b4" }, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            var service = new PreparationSessionService(
                new FakeSaveService(),
                new FakeDayProgressService(),
                new StaticPreparationInventoryProvider(books),
                shelfState,
                configs);

            service.StartOrResumeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(4, service.TotalSelected, "Fresh day keeps unsold books from the previous shelf.");

            service.RandomizeAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(12, service.TotalSelected, "Random must refill to DailyBookSlots from all owned inventory.");
            CollectionAssert.AreEquivalent(books.Select(b => b.Id), service.CurrentState.SelectedBookIds);
        }

        [Test]
        public void PassiveMiss_RaisesCustomerPassivePurchaseFailed()
        {
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            var failures = new List<string>();
            c.CustomerPassivePurchaseFailed += customer => failures.Add(customer.Id);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { "c1" }, failures);
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void PassiveFailure_AbandonsRemainingPassiveSteps_AndLeaves()
        {
            // Empty shelf → every passive attempt misses. The first miss must end the visit, so the
            // second PassivePurchaseStep never runs (one failure, not two).
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var failures = 0;
            c.CustomerPassivePurchaseFailed += _ => failures++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(1, failures, "First passive miss aborts the plan → the second passive never runs.");
            Assert.AreEqual(0, c.AccumulatedResult.SalesCount);
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "The aborting customer still leaves (served).");
        }

        [Test]
        public void PassiveFailure_BeforeActiveRequest_SkipsTheMinigame()
        {
            // Plan: Approach → Passive(miss) → Active → Leave. The passive miss ends the visit before
            // the active step is reached, so the minigame never opens.
            var req = SalesTestKit.Request("reqA");
            var c = Build(
                new BookConfig[0],
                new[] { req },
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new ActiveRequestStep(req), new LeaveStep()
                    })
                });

            var activeStarted = 0;
            c.ActiveRequestStarted += _ => activeStarted++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsNull(c.CurrentRequest, "No active minigame should ever open.");
            Assert.AreEqual(0, activeStarted, "Passive failure aborts before the active step is entered.");
        }

        [Test]
        public void PassiveSuccess_DoesNotAbort_ContinuesToNextPassive()
        {
            // Guard against over-aborting: a successful passive returns plain Completed, so a second
            // passive step still runs. Two books so the day ends via "all customers done".
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(), new LeaveStep()
                    })
                });

            var passive = 0;
            c.PassiveSaleHappened += _ => passive++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, passive, "Both passive steps succeed; success does not end the cycle.");
            Assert.AreEqual(2, c.AccumulatedResult.SalesCount);
        }

        [Test]
        public void CompletePurchase_HappyPath_FiresWithPassiveCount()
        {
            // Three books so two passive sales leave one on the shelf — the day ends via "all customers
            // done" (not "all sold out"), letting the customer reach CompletePurchase with count 2.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b2", genre: "sci-fi", price: 80),
                    SalesTestKit.Book("b3", genre: "sci-fi", price: 80)
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { 2 }, completions, "Completion fires once with the passive count.");
        }

        [Test]
        public void CompletePurchase_AbortWithZeroSales_DoesNotFire()
        {
            // Empty shelf → first passive misses → abort → CompletePurchase skipped (count 0).
            var c = Build(
                new BookConfig[0],
                new RequestConfig[0],
                SalesTestKit.Location(),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = 0;
            c.CustomerPurchaseCompleted += (_, _) => completions++;

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(0, completions, "No books bought → completion skipped.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Customer still leaves (served).");
        }

        [Test]
        public void ActiveRequest_OnlyOneMinigame_PausesOthers_ThenSequencesFifo()
        {
            var reqA = SalesTestKit.Request("reqA");
            var reqB = SalesTestKit.Request("reqB");
            var c = Build(
                new[] { SalesTestKit.Book("b1") },
                new[] { reqA, reqB },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA), Active("c2", reqB) });

            var started = new List<string>();
            c.ActiveRequestStarted += r => started.Add(r.Id);

            StartDay(c);
            DriveUntilActive(c);

            Assert.AreEqual(1, started.Count, "Only one minigame opens.");
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            // Pause: while the lock is held, ticking makes no progress and no second minigame opens.
            for (var i = 0; i < 5; i++) c.Tick(0.1f);
            Assert.AreEqual(1, started.Count, "Second customer stays paused while the first is in the minigame.");

            c.SkipCurrentRequest();
            DriveUntilActive(c);

            Assert.AreEqual(2, started.Count);
            Assert.AreEqual("reqB", started[1], "FIFO: second request opens after the first resolves.");

            c.SkipCurrentRequest();
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            CollectionAssert.AreEqual(new[] { "reqA", "reqB" }, started);
            Assert.AreEqual(2, c.AccumulatedResult.SkippedCount);
        }

        [Test]
        public void ReserveContention_TwoPassive_PickDifferentBooks()
        {
            // With the probabilistic selector always firing, each passive customer reserves an
            // available book; the reservation hides it from the second customer's pick.
            var c = Build(
                new[]
                {
                    SalesTestKit.Book("b1", genre: "sci-fi"),
                    SalesTestKit.Book("b2", genre: "romance", tags: new[] { "summer" })
                },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }, demandTags: new[] { "space" }),
                new List<Customer> { Passive("c1"), Passive("c2") });

            var soldIds = new List<string>();
            c.PassiveSaleHappened += e => soldIds.Add(e.BookId);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.AreEqual(2, soldIds.Count, "Both customers buy.");
            CollectionAssert.AreEquivalent(new[] { "b1", "b2" }, soldIds, "No double-reservation: customers pick distinct books.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
        }

        [Test]
        public void SoldOut_StopsSpawning_FinishesInFlight_ThenReadyToClose()
        {
            // Single book, three customers, spawned one-at-a-time (large SpawnInterval). c1 spawns and
            // buys the only book; once sold out, spawning stops so c2/c3 never appear. c1 must still run
            // its closing steps (LeaveStep → Done) before the day is closable — AllSoldOut no longer ends
            // the day instantly.
            var tuning = SalesTestKit.FastTuning();
            tuning.SpawnInterval = 100f;   // only the first customer spawns within the test's tick budget

            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1"), Passive("c2"), Passive("c3") },
                tuning);

            StartDay(c);
            Run(c);

            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase, "Day waits for the in-flight buyer to finish, then is closable.");
            Assert.AreEqual(1, c.AccumulatedResult.SalesCount, "Only one book to sell.");
            Assert.IsTrue(c.Shelf.AllSoldOut());
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed,
                "Only c1 was served; spawning stopped on sold-out so c2/c3 never appeared.");
        }

        [Test]
        public void LastBookBought_RunsClosingSteps_ThenReadyToClose()
        {
            // Repro of the freeze bug: 1 book, 1 customer with the full closing tail. Buying the last
            // book must NOT end the day before CompletePurchase + Leave run.
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 70) },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer>
                {
                    new("c1", new ICustomerStep[]
                    {
                        new ApproachStep(), new PassivePurchaseStep(),
                        new CompletePurchaseStep(), new LeaveStep()
                    })
                });

            var completions = new List<int>();
            c.CustomerPurchaseCompleted += (_, count) => completions.Add(count);
            SalesDayResult published = null;
            c.DayCompleted += r => published = r;

            StartDay(c);
            Run(c);

            // Day did NOT auto-complete; the customer finished its plan.
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
            Assert.IsTrue(c.Shelf.AllSoldOut());
            CollectionAssert.AreEqual(new[] { 1 }, completions, "CompletePurchase ran for the one bought book.");
            Assert.AreEqual(1, c.AccumulatedResult.CustomersServed, "Buyer reached Done, not frozen.");
            Assert.IsNull(published, "Results not published until the player closes the shop.");

            c.ConcludeDay();

            Assert.IsTrue(c.IsDayCompleted);
            Assert.IsNotNull(published);
            Assert.AreEqual(1, published.SalesCount);
            Assert.AreEqual(1, published.CustomersServed);
        }

        [Test]
        public void RecommendBook_ActiveMinigame_ScoresSellsAndCompletes()
        {
            var reqA = SalesTestKit.Request("reqA");
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi", price: 80) },
                new[] { reqA },
                SalesTestKit.Location(),
                new List<Customer> { Active("c1", reqA) });

            RecommendationResult resolved = null;
            c.RecommendationResolved += r => resolved = r;

            StartDay(c);
            DriveUntilActive(c);
            Assert.AreEqual("reqA", c.CurrentRequest.Id);

            c.RecommendBook("b1");

            Assert.IsNotNull(resolved);
            Assert.AreEqual(RecommendationTier.Excellent, resolved.Tier);
            Assert.AreEqual("b1", resolved.BookId);
            Assert.AreEqual(ShelfBookState.SoldOut, c.Shelf.Find("b1").State);
            Assert.AreEqual(80 + 25, c.AccumulatedResult.GoldEarned);

            Run(c);
            Assert.AreEqual(SalesDayPhase.ReadyToClose, c.Phase);
        }

        [Test]
        public void RecommendBook_WithNoActiveMinigame_IsIgnored()
        {
            var c = Build(
                new[] { SalesTestKit.Book("b1", genre: "sci-fi") },
                new RequestConfig[0],
                SalesTestKit.Location(demandGenres: new[] { "sci-fi" }),
                new List<Customer> { Passive("c1") });

            StartDay(c);
            // No active minigame is open yet → recommend is a no-op (no exception, no sale via this path).
            c.RecommendBook("b1");

            Assert.AreEqual(0, c.AccumulatedResult.ManualRequests);
        }
    }
}
