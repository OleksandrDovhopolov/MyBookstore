using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Inventory.API;
using Save;
using UnityEngine;

namespace Book.Sell.Services
{
    //TODO does it became god object ?
    /// <inheritdoc cref="ISalesDayController"/>
    public sealed class SalesDayController : ISalesDayController, ISalesDaySink
    {
        private const string LogPrefix = "[Sales.Day]";

        private readonly IConfigsService _configs;
        private readonly ISalesSetupProvider _setupProvider;
        private readonly IRecommendationScoringService _scoring;
        private readonly IPassiveSaleSelector _passiveSelector;
        private readonly ISalesRandom _random;
        private readonly ICustomerSpawner _spawner;
        private readonly IInteractionLock _lock;
        private readonly SalesTuning _tuning;
        private readonly ISaveService _save;
        private readonly ISalesShelfBuilder _shelfBuilder;
        private readonly ISoldBookCommitter _soldBookCommitter;
        private readonly ISalesGoldCollector _salesGoldCollector;

        private SalesShelf _shelf = new();
        private SalesDayResult _result = new();
        private LocationConfig _location;
        private CustomerContext _ctx;
        private List<Customer> _customers = new();

        private Customer _activeCustomer;
        private RequestConfig _activeRequest;

        private float _spawnTimer;
        private int _nextToSpawn;
        private SalesDayPhase _phase = SalesDayPhase.Running;
        private bool _spawningStopped;

        public SalesDayController(
            IConfigsService configs,
            ISalesSetupProvider setupProvider,
            IRecommendationScoringService scoring,
            IPassiveSaleSelector passiveSelector,
            ISalesRandom random,
            ICustomerSpawner spawner,
            IInteractionLock interactionLock,
            SalesTuning tuning,
            ISalesShelfBuilder shelfBuilder = null,
            ISoldBookCommitter soldBookCommitter = null,
            ISalesGoldCollector salesGoldCollector = null,
            IInventoryService inventory = null,
            ISaveService save = null,
            ISalesShelfStateService shelfState = null)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _setupProvider = setupProvider ?? throw new ArgumentNullException(nameof(setupProvider));
            _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            _passiveSelector = passiveSelector ?? throw new ArgumentNullException(nameof(passiveSelector));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _lock = interactionLock ?? throw new ArgumentNullException(nameof(interactionLock));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _shelfBuilder = shelfBuilder ?? new SalesShelfBuilder(_configs);
            _soldBookCommitter = soldBookCommitter ?? CreateLegacySoldBookCommitter(inventory, shelfState);
            _salesGoldCollector = salesGoldCollector ?? new NoOpSalesGoldCollector();
            _save = save;   // optional in tests; in prod injected via DI
        }

        public int Day { get; private set; }
        public string LocationId { get; private set; }
        public SalesShelf Shelf => _shelf;
        public SalesDayResult AccumulatedResult => _result;
        public RequestConfig CurrentRequest => _activeRequest;
        public SalesDayPhase Phase => _phase;
        public bool IsDayCompleted => _phase == SalesDayPhase.Completed;

        public event Action<RequestConfig> ActiveRequestStarted;
        public event Action<RecommendationResult> RecommendationResolved;
        public event Action<PassiveSaleEvent> PassiveSaleHappened;
        public event Action<Customer, RecommendationResult> CustomerRecommendationResolved;
        public event Action<Customer, PassiveSaleEvent> CustomerPassiveSaleHappened;
        public event Action<Customer> CustomerPassivePurchaseFailed;
        public event Action<Customer, int> CustomerPurchaseCompleted;
        public event Action<Customer> CustomerThoughtBubbleHidden;
        public event Action DayReadyToClose;
        public event Action<SalesDayResult> DayCompleted;
        public event Action<Customer> CustomerPhaseChanged;
        public event Action<Customer, string> BookReserved;
        public event Action<Customer, string> BookReleased;
        public event Action ShelfChanged;

        public UniTask StartDayAsync(int day, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var setup = _setupProvider.BuildForDay(day);
            Day = setup.Day;
            LocationId = setup.LocationId;

            _location = !string.IsNullOrEmpty(setup.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;

            _shelf = _shelfBuilder.Build(setup.ShelfBookIds);

            _result = new SalesDayResult { Day = setup.Day };
            _ctx = new CustomerContext(_shelf, _lock, _random, _passiveSelector, _location, setup.DecorIds, this, _tuning);

            _customers = new List<Customer>(_spawner.BuildCustomers(setup, _tuning, _random));
            _soldBookCommitter.Reset();
            _salesGoldCollector.Reset();
            _nextToSpawn = 0;
            _spawnTimer = _tuning.SpawnInterval;   // spawn the first customer on the first tick
            _activeCustomer = null;
            _activeRequest = null;
            _phase = SalesDayPhase.Running;
            _spawningStopped = false;

            if (_customers.Count == 0)
            {
                // No customers: the first Tick's UpdateDayPhase moves the day straight to ReadyToClose
                // (allSpawned & spawnedDone are vacuously true), so the player can still close the shop.
                Debug.LogWarning($"{LogPrefix} No customers for day {setup.Day} — day is immediately closable.");
            }

            ShelfChanged?.Invoke();
            return UniTask.CompletedTask;
        }

        public void Tick(float dt)
        {
            if (_phase != SalesDayPhase.Running) return;
            if (_lock.IsHeld) return;   // domain pause: an active minigame / dialogue is open

            SpawnDue(dt);

            // Tick activated, not-done customers. Stop the moment someone opens a minigame (freeze the rest).
            for (var i = 0; i < _nextToSpawn; i++)
            {
                var customer = _customers[i];
                if (customer.IsDone) continue;
                customer.Tick(_ctx, dt);
                if (_lock.IsHeld) break;
            }

            UpdateDayPhase();
        }

        public void RecommendBook(string bookId)
        {
            if (_activeCustomer == null)
            {
                Debug.LogWarning($"{LogPrefix} RecommendBook with no active minigame — ignored.");
                return;
            }
            if (string.IsNullOrEmpty(bookId))
            {
                Debug.LogWarning($"{LogPrefix} RecommendBook(null) — ignored.");
                return;
            }

            var shelfBook = _shelf.Find(bookId);
            if (shelfBook == null || shelfBook.State != ShelfBookState.Available || _shelf.IsReserved(bookId))
            {
                Debug.LogWarning($"{LogPrefix} book '{bookId}' is not available for selection — ignored.");
                return;
            }

            var request = _activeRequest;
            var result = _scoring.Score(shelfBook.Config, request, _location);

            if (result.Tier == RecommendationTier.Normal || result.Tier == RecommendationTier.Excellent)
            {
                _shelf.CommitSale(bookId);

                //TODO active should be const in config class
                _soldBookCommitter.CommitSoldBook(bookId, "active");
                _salesGoldCollector.CollectSaleGold(Day, bookId, result.GoldEarned, "active");
                _result.SoldBookIds.Add(bookId);
                _result.SalesCount++;
                _activeCustomer.RegisterPurchasedBook();
                ShelfChanged?.Invoke();
                Debug.Log($"{LogPrefix} active sale: book={bookId}, tier={result.Tier}, " +
                          $"gold={result.GoldEarned}, request={request.Id}");
            }

            _result.GoldEarned += result.GoldEarned;
            _result.ManualRequests++;
            CountTier(result.Tier);
            _result.Recommendations.Add(result);

            CustomerRecommendationResolved?.Invoke(_activeCustomer, result);
            RecommendationResolved?.Invoke(result);
            ResolveActive();
        }

        public void ForceCompleteDay(bool zeroOut)
        {
            if (_phase == SalesDayPhase.Completed) return;

            // Drop in-progress minigame state so the published result is consistent.
            // The IInteractionLock may still be held by the active step — that's fine: the next
            // Tick short-circuits on the phase before reaching the lock check, and the View
            // stops pumping Update once _dayRunning flips to false in OnDayCompleted.
            _activeCustomer = null;
            _activeRequest = null;

            if (zeroOut)
            {
                _result = new SalesDayResult { Day = Day };
            }

            // Reuse the organic completion path: same save + event ordering as ConcludeDay.
            _phase = SalesDayPhase.Completed;
            PublishCompletionAsync().Forget();
        }

        public void ConcludeDay()
        {
            if (_phase != SalesDayPhase.ReadyToClose) return;
            _phase = SalesDayPhase.Completed;
            PublishCompletionAsync().Forget();
        }

        public void SkipCurrentRequest()
        {
            if (_activeCustomer == null)
            {
                Debug.LogWarning($"{LogPrefix} Skip with no active minigame — ignored.");
                return;
            }

            var request = _activeRequest;
            var result = RecommendationResult.Skipped(request.Id);

            _result.ManualRequests++;
            CountTier(RecommendationTier.Skipped);
            _result.Recommendations.Add(result);

            CustomerRecommendationResolved?.Invoke(_activeCustomer, result);
            RecommendationResolved?.Invoke(result);
            ResolveActive();
        }

        // ----- ISalesDaySink (facts reported by steps) -----

        void ISalesDaySink.OnPhaseChanged(Customer customer, CustomerPhase phase)
        {
            if (phase == CustomerPhase.Done)
                _result.CustomersServed++;

            switch (phase)
            {
                case CustomerPhase.Approaching:
                    Debug.Log($"{LogPrefix} customer arrived: {customer.Id}");
                    break;
                case CustomerPhase.AwaitingHelp:
                    Debug.Log($"{LogPrefix} customer wants help: {customer.Id}");
                    break;
                case CustomerPhase.Leaving:
                    Debug.Log($"{LogPrefix} customer leaving: {customer.Id}");
                    break;
                case CustomerPhase.Done:
                    Debug.Log($"{LogPrefix} customer done: {customer.Id}");
                    break;
            }

            CustomerPhaseChanged?.Invoke(customer);
        }

        void ISalesDaySink.OnBookReserved(Customer customer, string bookId)
        {
            Debug.Log($"{LogPrefix} reserved (targeting): book={bookId}, customer={customer.Id}");
            BookReserved?.Invoke(customer, bookId);
        }

        void ISalesDaySink.OnBookReleased(Customer customer, string bookId)
        {
            Debug.Log($"{LogPrefix} reservation released (no sale): book={bookId}, customer={customer.Id}");
            BookReleased?.Invoke(customer, bookId);
        }

        void ISalesDaySink.OnPassivePurchaseFailed(Customer customer)
        {
            Debug.Log($"{LogPrefix} passive purchase failed: customer={customer.Id}");
            CustomerPassivePurchaseFailed?.Invoke(customer);
        }

        void ISalesDaySink.OnPurchaseCompleted(Customer customer, int purchasedBookCount)
        {
            Debug.Log($"{LogPrefix} purchase completed: customer={customer.Id}, books={purchasedBookCount}");
            CustomerPurchaseCompleted?.Invoke(customer, purchasedBookCount);
        }

        void ISalesDaySink.OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent)
        {
            //TODO passive should be const in config class
            _soldBookCommitter.CommitSoldBook(saleEvent.BookId, "passive");
            _salesGoldCollector.CollectSaleGold(Day, saleEvent.BookId, saleEvent.GoldEarned, "passive");
            _result.GoldEarned += saleEvent.GoldEarned;
            _result.SalesCount++;
            _result.SoldBookIds.Add(saleEvent.BookId);
            _result.PassiveSales.Add(saleEvent);
            ShelfChanged?.Invoke();

            // PassiveSaleHappened fires before the log so subscribers see the event first.
            CustomerPassiveSaleHappened?.Invoke(customer, saleEvent);
            PassiveSaleHappened?.Invoke(saleEvent);

            Debug.Log($"{LogPrefix} passive sale: book={saleEvent.BookId}, gold={saleEvent.GoldEarned}, " +
                      $"location={LocationId}");
        }

        void ISalesDaySink.OnActiveRequestStarted(Customer customer, RequestConfig request)
        {
            _activeCustomer = customer;
            _activeRequest = request;
            ActiveRequestStarted?.Invoke(request);
        }

        void ISalesDaySink.OnHideThoughtBubble(Customer customer)
            => CustomerThoughtBubbleHidden?.Invoke(customer);

        // ----- internals -----

        private void SpawnDue(float dt)
        {
            if (_spawningStopped) return;   // shelf sold out — no new customers (in-flight ones still finish)
            if (_nextToSpawn >= _customers.Count) return;
            _spawnTimer += dt;
            // Cap check inside the loop prevents bursts: one freed slot lets at most one new customer in.
            while (_spawnTimer >= _tuning.SpawnInterval
                   && _nextToSpawn < _customers.Count
                   && !IsConcurrencyCapReached())
            {
                _spawnTimer -= _tuning.SpawnInterval;
                _nextToSpawn++;   // include the next customer in the tick loop
            }
        }

        private bool IsConcurrencyCapReached()
        {
            var cap = _tuning.MaxConcurrentCustomers;
            if (cap <= 0) return false;   // no limit
            return ActiveCustomerCount() >= cap;
        }

        // Customers present on the floor = spawned [0.._nextToSpawn) that are not yet Done.
        private int ActiveCustomerCount()
        {
            var count = 0;
            for (var i = 0; i < _nextToSpawn; i++)
                if (!_customers[i].IsDone) count++;
            return count;
        }

        private void ResolveActive()
        {
            var customer = _activeCustomer;
            _activeCustomer = null;
            _activeRequest = null;

            // Exits the ActiveRequestStep (releasing the lock) and advances the customer's plan.
            customer.ForceCompleteCurrentStep(_ctx);

            UpdateDayPhase();
        }

        private void CountTier(RecommendationTier tier)
        {
            switch (tier)
            {
                case RecommendationTier.Excellent: _result.ExcellentCount++; break;
                case RecommendationTier.Normal: _result.NormalCount++; break;
                case RecommendationTier.Failed: _result.FailedCount++; break;
                case RecommendationTier.Skipped: _result.SkippedCount++; break;
            }
        }

        private void UpdateDayPhase()
        {
            if (_phase != SalesDayPhase.Running) return;

            // Sold out: no new customers — but the ones already on the floor must finish their plans
            // (CompletePurchase → Leave → Done) before the day is closable. This is the fix for the
            // "buyer of the last book freezes mid-plan" bug: AllSoldOut no longer ends the day.
            if (_shelf.AllSoldOut()) _spawningStopped = true;

            var noMoreCustomers = _nextToSpawn >= _customers.Count || _spawningStopped;
            var spawnedDone = true;
            for (var i = 0; i < _nextToSpawn; i++)
            {
                if (!_customers[i].IsDone) { spawnedDone = false; break; }
            }

            if (noMoreCustomers && spawnedDone)
            {
                // The day's work is done; wait for the player to close the shop (ConcludeDay).
                _phase = SalesDayPhase.ReadyToClose;
                DayReadyToClose?.Invoke();
            }
        }

        private async UniTaskVoid PublishCompletionAsync()
        {
            // Snapshot the result reference so a late reset doesn't race the publish.
            var snapshot = _result;

            await _soldBookCommitter.FlushAsync(CancellationToken.None);
            await _salesGoldCollector.FlushAsync(CancellationToken.None);

            // 1) Persist BEFORE emitting so anyone listening to DayCompleted can immediately read
            //    book_sell.last_day_result. UpdateModuleAsync only returns after the in-memory
            //    module table is updated (it awaits its own semaphore internally).
            if (_save != null)
            {
                try
                {
                    await _save.UpdateModuleAsync(SalesSaveKeys.LastDayResult, snapshot,
                        SalesSaveKeys.LastDayResultSchemaVersion, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LogPrefix} failed to persist SalesDayResult: {ex.Message}");
                }
            }

            // 2) Now it is safe to notify the View; Results will see a populated save module.
            DayCompleted?.Invoke(snapshot);
        }

        private static ISoldBookCommitter CreateLegacySoldBookCommitter(
            IInventoryService inventory,
            ISalesShelfStateService shelfState)
        {
            return inventory != null || shelfState != null
                ? new SoldBookCommitter(inventory, shelfState)
                : new NoOpSoldBookCommitter();
        }
    }
}
