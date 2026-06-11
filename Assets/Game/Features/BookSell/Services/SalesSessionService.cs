using System;
using System.Collections.Generic;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <inheritdoc cref="ISalesSessionService"/>
    public sealed class SalesSessionService : ISalesSessionService
    {
        private const string LogPrefix = "[Sales.Session]";
        public const int DefaultActiveQueueSize = 5;

        // Тригерные пороги для пассивных продаж. Внутри 0..1: чем меньше — тем чаще пассивная.
        public const double PassiveAttempt1Threshold = 0.6;   // 60% шанс попробовать первую
        public const double PassiveAttempt2Threshold = 0.4;   // 40% шанс попробовать вторую (только если первая успешна)

        private readonly IConfigsService _configs;
        private readonly ISalesSetupProvider _setupProvider;
        private readonly IRecommendationScoringService _scoring;
        private readonly IPassiveSaleSelector _passiveSelector;
        private readonly ISalesRandom _random;

        private SalesSessionState _state = new();
        private SalesDayResult _result = new();
        private LocationConfig _locationConfig;

        public SalesSessionService(
            IConfigsService configs,
            ISalesSetupProvider setupProvider,
            IRecommendationScoringService scoring,
            IPassiveSaleSelector passiveSelector,
            ISalesRandom random)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _setupProvider = setupProvider ?? throw new ArgumentNullException(nameof(setupProvider));
            _scoring = scoring ?? throw new ArgumentNullException(nameof(scoring));
            _passiveSelector = passiveSelector ?? throw new ArgumentNullException(nameof(passiveSelector));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public SalesSessionState State => _state;
        public SalesDayResult AccumulatedResult => _result;
        public RequestConfig CurrentRequest =>
            _state.CurrentRequestIndex >= 0 && _state.CurrentRequestIndex < _state.ActiveQueue.Count
                ? _state.ActiveQueue[_state.CurrentRequestIndex]
                : null;

        public event Action<RequestConfig> ActiveRequestStarted;
        public event Action<RecommendationResult> RecommendationResolved;
        public event Action<PassiveSaleEvent> PassiveSaleHappened;
        public event Action<SalesDayResult> DayCompleted;

        public UniTask StartDayAsync(int day, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            ResetState(day);

            var setup = _setupProvider.BuildForDay(day);
            _state.Day = setup.Day;
            _state.LocationId = setup.LocationId;
            _result.Day = setup.Day;

            _locationConfig = !string.IsNullOrEmpty(setup.LocationId)
                ? _configs.Get<LocationConfig>(setup.LocationId)
                : null;

            BuildShelf(setup.ShelfBookIds);
            BuildActiveQueue(DefaultActiveQueueSize);

            if (_state.ActiveQueue.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} Очередь активных запросов пуста — день сразу завершён.");
                _state.DayCompleted = true;
                DayCompleted?.Invoke(_result);
                return UniTask.CompletedTask;
            }

            _state.CurrentRequestIndex = 0;
            ActiveRequestStarted?.Invoke(_state.ActiveQueue[0]);
            return UniTask.CompletedTask;
        }

        public void RecommendBook(string bookId)
        {
            if (!EnsureActiveRequest(out var request)) return;
            if (string.IsNullOrEmpty(bookId))
            {
                Debug.LogWarning($"{LogPrefix} RecommendBook(null) — игнорируем.");
                return;
            }

            var shelfBook = FindShelfBook(bookId);
            if (shelfBook == null)
            {
                Debug.LogWarning($"{LogPrefix} bookId='{bookId}' нет на полке — игнорируем.");
                return;
            }
            if (shelfBook.State == ShelfBookState.SoldOut)
            {
                Debug.LogWarning($"{LogPrefix} bookId='{bookId}' уже sold_out — игнорируем.");
                return;
            }

            var result = _scoring.Score(shelfBook.Config, request, _locationConfig);

            // Failed — тоже даёт обратную связь клиенту, но книга НЕ продаётся.
            // Normal/Excellent — продажа.
            if (result.Tier == RecommendationTier.Normal || result.Tier == RecommendationTier.Excellent)
            {
                shelfBook.State = ShelfBookState.SoldOut;
                _result.SoldBookIds.Add(shelfBook.BookId);
                _result.SalesCount++;
            }

            _result.GoldEarned += result.GoldEarned;
            _result.ManualRequests++;
            _result.CustomersServed++;
            CountTier(result.Tier);
            _result.Recommendations.Add(result);

            RecommendationResolved?.Invoke(result);

            TryFirePassiveSales();
            AdvanceQueueOrComplete();
        }

        public void SkipCurrentRequest()
        {
            if (!EnsureActiveRequest(out var request)) return;

            var result = RecommendationResult.Skipped(request.Id);
            _result.ManualRequests++;
            _result.CustomersServed++;
            CountTier(RecommendationTier.Skipped);
            _result.Recommendations.Add(result);

            RecommendationResolved?.Invoke(result);

            TryFirePassiveSales();
            AdvanceQueueOrComplete();
        }

        // ----- internals -----

        private void ResetState(int day)
        {
            _state = new SalesSessionState { Day = day };
            _result = new SalesDayResult { Day = day };
            _locationConfig = null;
        }

        private void BuildShelf(IReadOnlyList<string> ids)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var book = _configs.Get<BookConfig>(ids[i]);
                if (book == null)
                {
                    Debug.LogWarning($"{LogPrefix} BookConfig '{ids[i]}' не найден — пропускаю.");
                    continue;
                }
                _state.Shelf.Add(new ShelfBook(book));
            }
        }

        private void BuildActiveQueue(int targetSize)
        {
            var allRequests = _configs.GetAll<RequestConfig>();
            // Берём первые targetSize в порядке конфига (детерминированно).
            // Когда появится Утро/Подготовка — селекция может стать сложнее.
            for (var i = 0; i < allRequests.Count && _state.ActiveQueue.Count < targetSize; i++)
            {
                _state.ActiveQueue.Add(allRequests[i]);
            }
        }

        private bool EnsureActiveRequest(out RequestConfig request)
        {
            request = CurrentRequest;
            if (request == null)
            {
                Debug.LogWarning($"{LogPrefix} Нет активного запроса. StartDay не вызван или день уже завершён.");
                return false;
            }
            return true;
        }

        private ShelfBook FindShelfBook(string bookId)
        {
            for (var i = 0; i < _state.Shelf.Count; i++)
            {
                if (_state.Shelf[i].BookId == bookId) return _state.Shelf[i];
            }
            return null;
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

        private void TryFirePassiveSales()
        {
            // Attempt #1: 60% шанс.
            if (_random.NextDouble() >= PassiveAttempt1Threshold) return;
            if (!FireOnePassiveSale()) return;

            // Attempt #2 — только если первая прошла: 40% шанс.
            if (_random.NextDouble() >= PassiveAttempt2Threshold) return;
            FireOnePassiveSale();
        }

        private bool FireOnePassiveSale()
        {
            var book = _passiveSelector.PickPassiveSale(_state.Shelf, _locationConfig, _random);
            if (book == null) return false;

            book.State = ShelfBookState.SoldOut;
            var gold = book.Config.BasePrice;

            _result.GoldEarned += gold;
            _result.SalesCount++;
            _result.CustomersServed++;
            _result.SoldBookIds.Add(book.BookId);

            var evt = new PassiveSaleEvent(book.BookId, gold);
            _result.PassiveSales.Add(evt);

            PassiveSaleHappened?.Invoke(evt);
            return true;
        }

        private void AdvanceQueueOrComplete()
        {
            var nextIndex = _state.CurrentRequestIndex + 1;
            if (nextIndex >= _state.ActiveQueue.Count)
            {
                _state.CurrentRequestIndex = -1;
                _state.DayCompleted = true;
                DayCompleted?.Invoke(_result);
                return;
            }

            _state.CurrentRequestIndex = nextIndex;
            ActiveRequestStarted?.Invoke(_state.ActiveQueue[nextIndex]);
        }
    }
}
