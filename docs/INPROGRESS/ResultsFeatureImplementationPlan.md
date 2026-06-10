# Results Feature Implementation Plan

## Summary

Implement the first playable version of the `Results` phase: consume `SalesDayResult`, show a short day summary, apply rewards exactly once, persist completed day progress, and move the player to the next `Morning`.

First-pass scope:

- Build results domain logic.
- Add a simple uGUI/debug results screen.
- Integrate with `Sales`, `Save`, and current day progression.
- Apply gold/reputation/new-book rewards once.
- Do not build a detailed sales journal or shop-after-results flow.

Chosen decisions:

- `SalesDayResult` is the source of truth for numbers shown in results.
- Reward application is idempotent and guarded by completed day id.
- `Next Day` increments day and sets `currentPhase = morning`.
- New book reward is deterministic for MVP, not gacha.

## Key Changes

Add the results domain model:

- `ResultsSessionState`: `Day`, `SalesDayResult`, reward application status, generated summary.
- `ResultsSummary`: sales count, gold earned, recommendation tier counts, best match, day review text, reward info.
- `AppliedDayRewards`: `CompletedDay`, `GoldDelta`, `ReputationDelta`, `NewBookIds`, `AppliedAtUtcMs`.
- `DayProgressState`: current day, current phase, completed days, gold balance, reputation.
- `NextDayResult`: completed day, next day, next phase.

Add results services:

- `IResultsSessionService`: open results, build summary, apply rewards, continue to next day.
- `IResultsRewardService`: convert `SalesDayResult` into gold/reputation/new-book rewards.
- `IResultsSaveService` or internal adapter over `ISaveService` using module keys `results.session` and `day_progress`.
- `IResultsReviewTextProvider`: returns a short review line based on recommendation quality.

Integrate with existing plans:

- Read `SalesDayResult` from `BookSell` save payload or a shared completed-sales payload.
- Use `Morning` day progression contract: after next-day click, set day to `completedDay + 1` and phase to `morning`.
- Keep preparation/sales setup history untouched until later cleanup rules exist.

Add a simple results screen:

- Header: `Day N completed`.
- Show `Sales`, `Gold`, `Excellent`, `Normal`, `Failed`.
- Show best recommendation card: request text, chosen book, score tier, reason.
- Show one review/hook line.
- Show reward line: gold/reputation/new book if any.
- CTA: `Next Day`.

## Behavior Details

Reward calculation:

- `GoldDelta` comes from `SalesDayResult.GoldEarned`; do not recalculate from UI.
- `ReputationDelta = excellentRecommendations`.
- If `failedRecommendations > 2`, apply `-1` reputation; otherwise no penalty.
- Reputation cannot go below `0`.
- First MVP new-book rule: grant one configured fictional book after completing day `1` if it is not already owned; otherwise no new book.

Idempotency:

- Before applying rewards, check whether `CompletedDay` is already in applied/completed rewards.
- If already applied, show existing summary and do not mutate balances again.
- Store `AppliedDayRewards` with completed day id and reward deltas.
- Use `ISaveService.BlockAutosave()` during reward application and next-day transition.

Best match:

- Prefer the highest score recommendation.
- If tied, prefer `Excellent`, then `Normal`, then earliest recommendation.
- If there were no successful recommendations, show the highest attempted match with a neutral review.

Next-day flow:

- `Next Day` is enabled after rewards are applied or verified as already applied.
- On click, set:
  - `currentDay = completedDay + 1`
  - `currentPhase = morning`
- Save immediately with `SaveMode.ForceWithSync` if available in implementation context; otherwise regular save is acceptable for local MVP.

Fallback behavior:

- If no `SalesDayResult` exists, show a blocking error/debug message and do not apply rewards.
- If book config for new reward is missing, skip new book reward and log a warning.
- If save load fails, rely on existing `SaveService` fallback behavior and show empty/default progress.

## Test Plan

Reward service EditMode tests:

- Normal sales add exactly `SalesDayResult.GoldEarned`.
- Reputation increases by excellent recommendation count.
- More than two failed recommendations applies `-1` reputation.
- Reputation never becomes negative.
- Day 1 grants deterministic new book once.

Idempotency tests with fake `ISaveService`:

- Applying the same day twice does not duplicate gold.
- Applying the same day twice does not duplicate reputation.
- Applying the same day twice does not duplicate new books.
- Restart on results shows already-applied rewards without mutation.

Next-day tests:

- `Next Day` increments day by one.
- `Next Day` sets phase to `morning`.
- Next-day transition saves progress.

Sales integration tests:

- Results summary uses `SalesDayResult` numbers without recalculation.
- Best match selects the highest score result.
- Missing sales result blocks reward application.

Manual Unity check:

- Complete a sales session and open results.
- Verify summary values match sales summary.
- Restart on results and verify rewards do not duplicate.
- Click `Next Day` and verify morning opens for the next day.

## Assumptions

- `SalesDayResult` from `BookSell` is available before results opens and remains immutable for the completed day.
- A minimal day progress payload can be introduced if no global day state exists yet.
- Gold/reputation can be stored in the day progress/results payload for this slice until a dedicated `Resources` module is implemented.
- Inventory ownership is still stubbed; new-book reward can be stored as an owned-book id list in the same MVP progress payload.
- Detailed customer journal, store/restock screen, complex economy, and long story scenes are out of scope.
- Existing `docs/INPROGRESS/Итоги.md` remains the GDD/spec source; this file is the implementation plan.
