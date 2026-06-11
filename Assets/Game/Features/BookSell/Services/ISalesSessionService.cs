using System;
using System.Threading;
using Book.Sell.Domain;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Оркестратор дня продажи. View подписывается на 4 события и держит локальный снимок состояния.
    ///
    /// Event flow по тику взаимодействия:
    ///   RecommendBook(id) / SkipCurrentRequest()
    ///     → RecommendationResolved(result)
    ///     → PassiveSaleHappened × 0..2
    ///     → if queue empty → DayCompleted(result)
    ///          else        → ActiveRequestStarted(nextRequest)
    /// </summary>
    public interface ISalesSessionService
    {
        SalesSessionState State { get; }
        SalesDayResult AccumulatedResult { get; }

        /// <summary>Текущий активный запрос или null до StartDay / после завершения.</summary>
        RequestConfig CurrentRequest { get; }

        event Action<RequestConfig> ActiveRequestStarted;
        event Action<RecommendationResult> RecommendationResolved;
        event Action<PassiveSaleEvent> PassiveSaleHappened;
        event Action<SalesDayResult> DayCompleted;

        /// <summary>Собирает setup, генерит очередь активных запросов, эмитит первый ActiveRequestStarted.</summary>
        UniTask StartDayAsync(int day, CancellationToken ct);

        /// <summary>Игрок выбрал книгу с полки и нажал «Подтвердить».</summary>
        void RecommendBook(string bookId);

        /// <summary>Игрок нажал «Ничего не предложить» — Skipped tier, без штрафа.</summary>
        void SkipCurrentRequest();
    }
}
