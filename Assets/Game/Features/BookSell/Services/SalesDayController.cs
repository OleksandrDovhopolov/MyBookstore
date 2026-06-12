using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Save;
using UnityEngine;

namespace Book.Sell.Services
{
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

        private SalesShelf _shelf = new();
        private SalesDayResult _result = new();
        private LocationConfig _location;
        private CustomerContext _ctx;
        private List<Customer> _customers = new();

        private Customer _activeCustomer;
        private RequestConfig _activeRequest;

        private float _spawnTimer;
        private int _nextToSpawn;
        private bool _completed;

        public SalesDayController(
            IConfigsService configs,
            ISalesSetupProvider setupProvider,
            IRecommendationScoringService scoring,
            IPassiveSaleSelector passiveSelector,
            ISalesRandom random,
            ICustomerSpawner spawner,
            IInteractionLock interactionLock,
            SalesTuning tuning,
            ISaveService save = null)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _setupProvider = setupProvider ?? throw new ArgumentNullException(nameof(setupProvider));
            _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            _passiveSelector = passiveSelector ?? throw new ArgumentNullException(nameof(passiveSelector));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _lock = interactionLock ?? throw new ArgumentNullException(nameof(interactionLock));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _save = save;   // optional in tests; in prod injected via DI
        }

        public int Day { get; private set; }
        public string LocationId { get; private set; }
        public SalesShelf Shelf => _shelf;
        public SalesDayResult AccumulatedResult => _result;
        public RequestConfig CurrentRequest => _activeRequest;
        public bool IsDayCompleted => _completed;

        public event Action<RequestConfig> ActiveRequestStarted;
        public event Action<RecommendationResult> RecommendationResolved;
        public event Action<PassiveSaleEvent> PassiveSaleHappened;
        public event Action<SalesDayResult> DayCompleted;
        public event Action<Customer> CustomerPhaseChanged;

        public UniTask StartDayAsync(int day, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var setup = _setupProvider.BuildForDay(day);
            Day = setup.Day;
            LocationId = setup.LocationId;

            _location = !string.IsNullOrEmpty(setup.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;

            _shelf = new SalesShelf();
            BuildShelf(setup.ShelfBookIds);

            _result = new SalesDayResult { Day = setup.Day };
            _ctx = new CustomerContext(_shelf, _lock, _random, _passiveSelector, _location, setup.DecorIds, this, _tuning);

            _customers = new List<Customer>(_spawner.BuildCustomers(setup, _tuning, _random));
            _nextToSpawn = 0;
            _spawnTimer = _tuning.SpawnInterval;   // spawn the first customer on the first tick
            _activeCustomer = null;
            _activeRequest = null;
            _completed = false;

            if (_customers.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No customers for day {setup.Day} — completing immediately.");
                CompleteDay();
            }

            return UniTask.CompletedTask;
        }

        public void Tick(float dt)
        {
            if (_completed) return;
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

            CheckEndOfDay();
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
                _result.SoldBookIds.Add(bookId);
                _result.SalesCount++;
                Debug.Log($"{LogPrefix} active sale: book={bookId}, tier={result.Tier}, " +
                          $"gold={result.GoldEarned}, request={request.Id}");
            }

            _result.GoldEarned += result.GoldEarned;
            _result.ManualRequests++;
            CountTier(result.Tier);
            _result.Recommendations.Add(result);

            RecommendationResolved?.Invoke(result);
            ResolveActive();
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
            => Debug.Log($"{LogPrefix} reserved (targeting): book={bookId}, customer={customer.Id}");

        void ISalesDaySink.OnBookReleased(Customer customer, string bookId)
            => Debug.Log($"{LogPrefix} reservation released (no sale): book={bookId}, customer={customer.Id}");

        void ISalesDaySink.OnPassiveSale(Customer customer, PassiveSaleEvent saleEvent)
        {
            _result.GoldEarned += saleEvent.GoldEarned;
            _result.SalesCount++;
            _result.SoldBookIds.Add(saleEvent.BookId);
            _result.PassiveSales.Add(saleEvent);

            // PassiveSaleHappened fires before the log so subscribers see the event first.
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

        // ----- internals -----

        private void BuildShelf(IReadOnlyList<string> ids)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var book = _configs.Get<BookConfig>(ids[i]);
                if (book == null)
                {
                    Debug.LogWarning($"{LogPrefix} BookConfig '{ids[i]}' not found — skipping.");
                    continue;
                }
                _shelf.Add(new ShelfBook(book));
            }
        }

        private void SpawnDue(float dt)
        {
            if (_nextToSpawn >= _customers.Count) return;
            _spawnTimer += dt;
            while (_spawnTimer >= _tuning.SpawnInterval && _nextToSpawn < _customers.Count)
            {
                _spawnTimer -= _tuning.SpawnInterval;
                _nextToSpawn++;   // include the next customer in the tick loop
            }
        }

        private void ResolveActive()
        {
            var customer = _activeCustomer;
            _activeCustomer = null;
            _activeRequest = null;

            // Exits the ActiveRequestStep (releasing the lock) and advances the customer's plan.
            customer.ForceCompleteCurrentStep(_ctx);

            CheckEndOfDay();
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

        private void CheckEndOfDay()
        {
            if (_completed) return;

            var allSpawned = _nextToSpawn >= _customers.Count;
            var allDone = true;
            for (var i = 0; i < _customers.Count; i++)
            {
                if (!_customers[i].IsDone) { allDone = false; break; }
            }

            if ((allSpawned && allDone) || _shelf.AllSoldOut())
                CompleteDay();
        }

        private void CompleteDay()
        {
            // Flag the day completed synchronously so further ticks early-return.
            // Actual publish (save write + event) runs as an async chain so the save module is
            // populated BEFORE downstream subscribers (Results) react. Without this ordering the
            // Results view reads an empty module in the same frame and bails with "no result".
            if (_completed) return;
            _completed = true;
            PublishCompletionAsync().Forget();
        }

        private async UniTaskVoid PublishCompletionAsync()
        {
            // Snapshot the result reference so a late reset doesn't race the publish.
            var snapshot = _result;

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
    }
}
