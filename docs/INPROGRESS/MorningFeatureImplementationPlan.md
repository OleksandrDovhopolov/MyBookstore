# Morning Feature Implementation Plan

## Summary

Implement the first playable version of the `Morning` phase: the player starts a day, sees a short day summary with weather/event/hint, gets one clear demand modifier, and continues into `Preparation` with a stable morning context.

First-pass scope:

- Build morning domain logic.
- Add a simple uGUI/debug morning screen.
- Integrate with `Configs` and `Save`.
- Produce active modifier ids for `Preparation`.
- Keep narrative/dialogue short and static.

Chosen decisions:

- Morning is a lightweight screen, not a full scene simulation.
- One active day summary is resolved per day and restored after restart.
- MVP can use a dedicated `DayConfig` or expand `EventConfig`; prefer `DayConfig` if implementation wants clean separation from timed/live events.
- If no configured day exists, use a stable fallback summary.

## Key Changes

Add the morning domain model:

- `MorningDayContext`: `Day`, `DayId`, `Title`, `WeatherId`, `EventId`, `SummaryText`, `HintText`, `ActiveModifierIds`, `TargetLocationIds`.
- `MorningSessionState`: current day, resolved day context, read/continue timestamps.
- `MorningModifier`: id, display text, demand genres/tags, target locations.
- `MorningContinueResult`: payload passed to preparation.

Add morning services:

- `IMorningSessionService`: start/resume morning, resolve day context, mark continue, produce preparation context.
- `IMorningContextResolver`: chooses the configured day context from save day index.
- `IMorningSaveService` or internal adapter over `ISaveService` using module key `morning.session`.
- `IMorningAnalyticsService` can be a thin stub/wrapper until analytics logging is implemented.

Add or expand configs:

- Preferred: add `DayConfig` mapped to `days.json` with `DayIndex`, `Title`, `WeatherId`, `EventId`, `SummaryText`, `HintText`, `DemandGenres`, `DemandTags`, `TargetLocationIds`.
- Alternative: expand `EventConfig` with the same display and demand fields if keeping only four config files is required.
- Extend `LocationConfig` with `DemandGenres` and `DemandTags`, shared with `Preparation` and `Sales`.
- Add sample day content for at least day 1 and day 2.

Update assembly and DI:

- Register morning services in a feature binding near gameplay/core loop modules.
- The service must depend only on `IConfigsService` and `ISaveService` for the first pass.
- If a new assembly is created, reference `Configs`, `Save`, `UniTask`, and UI packages as needed.

Add a simple morning screen:

- Show `Day N`.
- Show title/summary.
- Show weather text/icon placeholder.
- Show one demand modifier card.
- Show one short hint or narrative line.
- CTA: `To Preparation`.
- On continue, save `currentPhase = preparation` and pass morning context to preparation.

## Behavior Details

Day context resolution:

- Read current day from save; if absent, default to day `1`.
- Find matching `DayConfig.DayIndex`.
- If no matching config exists, use the last configured day or a deterministic fallback.
- Resolve `ActiveModifierIds` from the selected day context.
- Persist the resolved context so restart on morning shows the same event/weather/hint.

Weather and event:

- `WeatherId` is a string in MVP; no weather simulation is needed.
- `EventId` is optional and can point to a configured event.
- Morning does not apply rewards directly.
- Morning only produces modifiers that later phases can read.

Continue flow:

- Morning starts with `currentPhase = morning`.
- When the player taps continue, write `currentPhase = preparation`.
- Store the morning context for preparation under a stable payload.
- `Preparation` should read `ActiveModifierIds`, `DemandGenres`, `DemandTags`, and `TargetLocationIds` if available.

Fallback behavior:

- If config loading fails or there are no day configs, show a safe fallback:
  - title: `First Morning`
  - weather: `clear`
  - summary: `A quiet morning for the book cart.`
  - hint: `Choose a few books and open the shop.`
  - no active demand modifiers.
- Log a warning when fallback is used.

## Test Plan

Morning resolver EditMode tests:

- Day 1 resolves the matching configured day.
- Missing day config uses deterministic fallback.
- Weather/event/modifier fields are preserved in `MorningDayContext`.
- Target location ids are passed through unchanged.

Save tests with fake `ISaveService`:

- Starting morning writes `currentPhase = morning`.
- Restart restores the same resolved morning context.
- Continue writes `currentPhase = preparation`.
- Continue produces a payload preparation can consume.

Config tests:

- `DayConfig` entries deserialize from sample `days.json`.
- Empty optional fields do not crash context resolution.
- Duplicate/missing ids are handled by the existing config service behavior and logged.

Manual Unity check:

- Open morning screen.
- Verify day number, summary, weather, modifier, hint, and CTA render.
- Click `To Preparation`.
- Verify preparation receives active modifiers or an empty stable list.
- Restart on morning and confirm the same day context is shown.

## Assumptions

- `Morning` is part of the core loop and can live in a new feature folder such as `Assets/Game/Features/DayCycle` or a dedicated `Morning` folder during implementation.
- Current day/progress save model is not implemented yet, so the first pass may store day state in a new module payload.
- `Preparation` already plans to accept active modifier ids; morning becomes the source of those ids.
- Real weather simulation, calendar rules, dynamic news, and long dialogue are out of scope.
- The first implementation uses placeholder UI and text-only weather.
- Existing `docs/INPROGRESS/Утро.md` remains the GDD/spec source; this file is the implementation plan.

## Status — vertical slice implemented (2026-06-10)

Decisions locked in (with the user):

- Config source for the day: **new `DayConfig` → `days.json`** (not an expanded `EventConfig`).
- Code home: **new `DayCycle` assembly** hosting the shared day-state + the Morning phase. Sales stays in `Book.Sell`.
- UI: **debug uGUI** screen in `GameplayScene` (no UI System dependency yet).
- Notion: 4 core-loop tasks created in «✅ Задачи до релиза (MVP)» linked to the parent «Прототип core loop» via `Depends on` chain (Утро → Подготовка → Продажа → Итоги).

What landed:

- `Game.Configs.Models.DayConfig` (`[ConfigFile("days")]`) + sample `Assets/Configs/days.json` (day 1, day 2).
- `Assets/Game/Features/DayCycle/` assembly:
  - `Day/DayPhase`, `Day/DayProgressState` (shared current day/phase/gold/reputation/completed days/owned books), `Day/IDayProgressService` + `DayProgressService` (Save module `day_progress`, schema v1). This is the shared day-state the whole core loop reads.
  - `Morning/` domain: `MorningDayContext`, `MorningContinueResult`, `IMorningContextResolver` + `MorningContextResolver` (deterministic resolve by `DayIndex` → reuse last day → fallback; modifier ids derived as `weather_*` / `event_*`), `IMorningSessionService` + `MorningSessionService`, `MorningFallback`.
  - `Morning/UI/MorningScreenView` (self-injecting uGUI/TMP debug view).
- DI: `DayCycleVContainerBindings.RegisterDayCycle()` wired into `GameInstaller`; resolves `ISaveService`/`IConfigsService` from the global scope. The view is registered only if present in the scene (project still runs before the UI is wired).
- EditMode tests (`DayCycle.Tests.Editor`): resolver field mapping, modifier derivation, fallback, day-beyond-content reuse, determinism; session start/resume, phase reset, persisted day, continue → preparation persistence across restart.

Determinism note: the morning context is re-resolved from the persisted `CurrentDay` each open, so restart on the morning phase shows the same day/event/weather **without** persisting the context itself — satisfies the acceptance criterion with no extra save module.

Manual scene wiring still required (Unity, by hand — not committable as code):

1. In `GameplayScene`, under the Canvas add a GameObject with `MorningScreenView`.
2. Assign the 6 `TMP_Text` fields (day / title / weather / summary / hint / modifier) and the `Continue` `Button`.
3. Ensure `GameplayLifetimeScope` has `GameInstaller` in its mono-installers (so `RegisterDayCycle` runs and finds the view).

Follow-ups (out of this slice):

- Publish `days.json` to the Config server so non-editor builds resolve it (editor uses `LocalFolderConfigSource`).
- Extend `LocationConfig` with `DemandGenres`/`DemandTags` — belongs to the Preparation task.
- Analytics events (`morning_opened`, `morning_continue_clicked`) — stub until the analytics logging task.
- `Preparation` consumes `MorningContinueResult` as its setup input (replaces its temporary fallback).
