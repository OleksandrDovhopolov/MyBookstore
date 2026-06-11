# Sales Feature Implementation Plan

## Summary

Implement the first clickable version of the core hypothesis: the player sees a customer request, chooses a book from the daily shelf, receives scoring, sees the reason behind the result, earns rewards, and reaches a day result after a fixed request queue.

First-pass scope:

- Build `BookSell` domain logic.
- Add a simple uGUI/debug gameplay screen.
- Do not build production UI/navigation yet.
- Do not depend on the future `Preparation` feature.
- Do not implement background sales in this slice.

Chosen decisions:

- Implementation level: vertical slice.
- Setup source until `Preparation` exists: fallback provider.
- Stock rule: one selected book equals one copy for the day; after sale, the book becomes `sold_out`.

## Key Changes

Add the sales domain model in `Book.Sell`:

- `SalesSessionSetup`: `Day`, `LocationId`, `BookIds`, `DecorIds`.
- `SalesSessionState`: current request, sold books, accumulated results.
- `SalesDayResult`: customers, sales, gold, excellent/normal/failed counts, sold books, recommendation results.
- `RecommendationResult`, `RecommendationReason`, `ScoreBreakdown`, `RecommendationTier`.

Add sales services:

- `ISalesSetupProvider`: temporary fallback setup using the first available location, first `N` books from config, and no decor.
- `ISalesSessionService`: start day, expose current request, choose book, advance request queue, complete day.
- `IRecommendationScoringService`: calculate score and reason using genre, tags, mood, price, and location demand.
- `ISalesSaveService` or an internal adapter over `ISaveService` using module key `book_sell.sales`.

Expand config models:

- `BookConfig`: add `Tags: string[]`, `Mood: string[]`; keep `Genre`, `BasePrice`, `RarityWeight`.
- `RequestConfig`: replace the old quest-like shape with sales-request fields: `Text`, `DesiredGenres`, `DesiredTags`, `DesiredMood`, `MaxPrice`, `Difficulty`, `BaseRewardGold`.
- `LocationConfig`: add `DemandGenres: string[]`, `DemandTags: string[]`.
- Update `Assets/Configs/*.json` and editor item templates for the new shapes.
- Remove real protected IP from sample books, including `Dune` and `The Hobbit`; use fictional books instead.

Update assembly and DI:

- Add `Configs`, `Save`, `UniTask`, and UI references to `Book.Sell.asmdef` as needed.
- Register setup/session/scoring/save services in `BookSellVContainerBindings.RegisterBookSell()`.

Add a simple gameplay screen:

- Show selected location as text/background placeholder.
- Show current customer request.
- Show available daily shelf books.
- Disable sold-out books after a successful sale.
- Show score tier and reason after recommendation.
- Show counters for request progress, gold, and recommendation quality.
- Show a final summary panel with `SalesDayResult` after the queue ends.
- Use a fixed request count of `5`, or fewer if fewer request configs exist.

## Behavior Details

Scoring rules:

- Genre match: `+3`.
- Each tag match: `+2`.
- Each mood match: `+1`.
- Price match: `+1` when `BookConfig.BasePrice <= RequestConfig.MaxPrice`.
- If `MaxPrice <= 0`, skip price scoring.
- Location demand genre/tag match: `+1` maximum once.

Recommendation tiers:

- `0-2`: `Failed`.
- `3-5`: `Normal`.
- `6+`: `Excellent`.

Rewards:

- `Failed`: no sale, `0` gold.
- `Normal`: sale, `book.BasePrice`.
- `Excellent`: sale, `book.BasePrice + request.BaseRewardGold`.

Save behavior:

- Save current sales state after each recommendation.
- Restore request index, sold books, and accumulated rewards after restart.
- Never grant the same recommendation reward twice.

End-of-day behavior:

- Complete when the fixed request queue is consumed.
- Complete early if all books are sold out.
- Produce `SalesDayResult`; the future `Results` feature will consume this payload.

## Test Plan

Scoring EditMode tests:

- Exact genre/tag/mood/price/location match returns `Excellent` and a complete reason.
- Wrong book returns `Failed`.
- `MaxPrice <= 0` skips price scoring.
- Location bonus is capped at `+1`.

Sales session EditMode tests:

- Fallback setup uses the first location and first `N` books.
- Sold book cannot be selected again.
- Reward is applied once per request.
- Session completes after the request queue ends.
- Session completes when all books are sold out.

Save tests with a fake `ISaveService`:

- State persists after recommendation.
- Restore resumes on the same request index.
- Restore does not duplicate rewards.

Manual Unity check:

- Open gameplay scene.
- Click through up to 5 requests.
- Verify request text, available books, score tier, reason, sold-out state, and final summary.
- Verify no real protected book titles appear in UI or sample config content.

## Assumptions

- `Sales` implementation lives under `Assets/Game/Features/BookSell`.
- The first implementation does not wait for `Preparation`; fallback setup is explicitly temporary.
- `Inventory` remains stubbed for this slice; book ownership is simulated from config.
- `Decor` is ignored in scoring for this slice, but `SalesSessionSetup.DecorIds` is preserved for later integration.
- Production navigation/window system is out of scope.
- A simple uGUI/debug screen is acceptable for validating the core hypothesis.

## Status — vertical slice implemented (2026-06-11)

Decisions locked in (with the user):

- **Both passive and active** sales in the same slice (vs «only manual first» originally).
- **Skip** is its own tier (`RecommendationTier.Skipped`), neutral, 0 gold, отдельный счётчик `SkippedCount` — НЕ `Failed`.
- **Passive sales** не зависят от таймера: после каждой активной резолюции (включая Skip) сервис делает 0–2 попытки. Attempt #1 — 60% шанс; если успешно — attempt #2 — 40% шанс. Подбор — из `Available` книг полки с матчем по `LocationConfig.DemandGenres ∪ DemandTags`.
- **End of day** — после фиксированной очереди (`DefaultActiveQueueSize = 5`).
- **Without Save, without `DayProgressService`** — Sales standalone, in-memory state. Каждый `StartDayAsync` сбрасывает state.
- **UI — на стороне пользователя**. Sales отдаёт всё через 4 события + DTO; никакого Unity-объекта в сборке `Book.Sell`.
- **`ISalesRandom`** — порт над `UnityEngine.Random`, фейк-очередь в тестах для детерминизма.

What landed:

- **Config refactor**:
  - `BookConfig` расширен полями `Tags: string[]`, `Mood: string[]`.
  - `RequestConfig` **полностью заменён** на sales-shape (`Text`, `DesiredGenres/Tags/Mood`, `MaxPrice`, `Difficulty`, `BaseRewardGold`). Старая quest-форма (`bookId`/`rewardSoft`/`timeLimitSeconds`) удалена.
  - `LocationConfig` расширен полями `DemandGenres: string[]`, `DemandTags: string[]`.
- **Sample content** в `Assets/Configs/` — без real IP (никаких Dune/Hobbit):
  - `books.json` — 8 вымышленных книг с заполненными `Tags`/`Mood`.
  - `requests.json` — 5 sales-shape запросов с осмысленными `Text` и разными матчами/«trap» по `MaxPrice`.
  - `locations.json` — 2 локации с `DemandGenres`/`DemandTags`. `loc_downtown` теги согласованы с `DayConfig.day_001` (`study`/`short`/`science`).
- **`Book.Sell` assembly** (refs: UniTask, VContainer, Configs):
  - `Domain/`: `SalesSessionSetup`, `SalesSessionState`, `SalesDayResult`, `RecommendationResult/Reason/Tier`, `ScoreBreakdown`, `ShelfBook(/State)`, `PassiveSaleEvent`.
  - `Services/`: `ISalesRandom`+`UnityRandomSalesRandom`; `ISalesSetupProvider`+`DefaultSalesSetupProvider` (первая локация + первые 8 книг); `IRecommendationScoringService`+`RecommendationScoringService` (чистая логика с case-insensitive матчем); `IPassiveSaleSelector`+`DefaultPassiveSaleSelector`; `ISalesSessionService`+`SalesSessionService` (оркестрация + 4 события).
- **DI** в `BookSellVContainerBindings` — все 5 сервисов как `Singleton`. `GameInstaller.RegisterBookSell()` уже вызывается.
- **EditMode-тесты** (`Book.Sell.Tests.Editor`):
  - `RecommendationScoringServiceTests` — 9 кейсов: exact match→Excellent, wrong→Failed, `MaxPrice<=0` skip price, over-budget no-price-bonus, location cap +1, Normal tier 3-5 gold = BasePrice, null location, matched-tags-in-reason, case-insensitive.
  - `SalesSessionServiceTests` — 11 кейсов: первый ActiveRequestStarted, RecommendBook + advance, sold-out no-op, Skip → Skipped tier no gold no shelf change, оба порога пассивных → 2 fires, первый порог фейлится → 0 fires, второй порог фейлится → 1 fire, нет матча на полке → 0 fires, DayCompleted ровно 1 раз, очередь капится на `DefaultActiveQueueSize`, пустой setup → мгновенный DayCompleted, **event-order**: `started → resolved → passive → started`.

Event flow по тику взаимодействия (фиксирован тестом):

```
RecommendBook(id) / SkipCurrentRequest()
  → RecommendationResolved(result)
  → PassiveSaleHappened × 0..2
  → if queue empty → DayCompleted(result)
       else        → ActiveRequestStarted(nextRequest)
```

Manual scene wiring (пользовательский UI, не коммитится кодом):

1. В `GameplayScene` положить GameObject с пользовательским View-скриптом, у него `[Inject] ISalesSessionService` (resolve через `GameplayLifetimeScope.Auto Inject Game Objects` — туда добавить этот GameObject).
2. View:
   - подписывается на 4 события + рендерит `service.State.Shelf` (фильтр по `Available`);
   - кнопка «Подтвердить» вызывает `service.RecommendBook(selectedBookId)`;
   - кнопка «Ничего не предложить» вызывает `service.SkipCurrentRequest()`;
   - `service.StartDayAsync(day: 1, ct)` в `Start()`.
3. Запуск из `Bootstrap.unity` (уже настроен в прошлой итерации).

Follow-ups (out of this slice):

- Интеграция с `DayCycle.IDayProgressService` (передача `currentDay` и финализация наград в общий day-state).
- Save для in-progress дня (модуль `book_sell.sales`).
- Фаза Results — экран итогов поверх `SalesDayResult`.
- Аналитика (`sales_opened`, `request_shown`, `book_recommended`, `recommendation_reason_shown`, `sales_completed`).
- Подключение `Preparation` как реального источника `SalesSessionSetup` (заменяет `DefaultSalesSetupProvider`).
- Учёт декора в scoring (поле `DecorIds` уже хранится).
