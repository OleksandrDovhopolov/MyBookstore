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
