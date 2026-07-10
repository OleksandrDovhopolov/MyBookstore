# FTUE — First-Time Experience

Compact design reference for the player's first launch and first scripted day.
Covers both what ships in the current build and the larger first-day vision
that is still in backlog.

> Related: `docs/CORE_LOOP.md` (the day cycle this funnels into).

---

## What FTUE is

The First-Time Experience is the bridge between "fresh install" and "the player
understands the loop." Two responsibilities:

1. **Seed a playable starting state** — gold, books, day 1 setup — so the loop
   has something to chew on from the first click.
2. **Teach the loop on day 1** — make the scripted first day land the key
   mechanics (passive sales, sale chance, recommendation feedback, restock,
   decor) through a curated sequence rather than a procedural day.

Responsibility 1 ships today. Responsibility 2 is partial: the loop is playable
end-to-end but the day 1 narrative scripting is in backlog.

---

## Currently implemented

### Bootstrap phase

`FtueBootstrapOperation` is its own phase in the boot loading pipeline
(`phase_ftue`), placed between `phase_data_load` and `phase_finalization`. It
runs **after** save is loaded and **before** GameplayScene opens.

```
phase_technical_init → phase_data_load → phase_ftue → phase_finalization
```

### What `IFtueBootstrapper.RunAsync` does

1. **Idempotency check.** Reads the `ftue.applied` save module; if `Applied =
   true`, logs and returns. The operation never re-runs against the same save.
2. **Legacy-save guard.** If `day_progress.CurrentDay > 1` or `CompletedDays`
   is non-empty, the player has progressed already — mark applied and exit
   without touching balances or inventory.
3. **Clean first launch.** Seed `gold = 60` (via `IResourcesService`) and add
   27 starter books to inventory under the `book` category. Then write the
   `ftue.applied` marker.

### Starter book preset

Genre order is fixed so the seeded id sequence is stable across runs (good for
deterministic tests). Inside each genre, books are picked by `RarityWeight`
descending — common, "starter-friendly" titles come first; rare ones are saved
for organic discovery later.

| Genre   | Starter count | Total in catalog |
|---------|--------------:|-----------------:|
| Fantasy |             5 |               12 |
| Crime   |             5 |               10 |
| Drama   |             6 |                9 |
| Classic |             3 |                9 |
| Fact    |             3 |                7 |
| Travel  |             3 |                7 |
| Kids    |             2 |                6 |
| **Total** |       **27** |           **60** |

### Constants and config seam

`StartingGold` and the per-genre preset map live as constants in
`FtueBootstrapper` for the MVP. Migration to a dedicated `ftue.json` (or into
`economy.json`) is tracked in `docs/CORE_LOOP.md` "known limitations" and in
Notion — same shape as the `DailyBookSlots` migration.

### Save state (FTUE-owned modules)

FTUE owns three save keys (`FtueSaveKeys`). This is the source of truth for what
each means; `docs/SAVE_DAY_FLOW.md` only lists them in its modules table and
defers here.

| Key | Meaning |
|---|---|
| `ftue.applied` | Starter gold/books were seeded. Set once, never re-runs (see idempotency above). |
| `ftue.welcome_completed` | The player finished the **welcome letter** window only. It does **not** mean the first-location tutorial is done. |
| `ftue.first_location_tutorial` | Persisted state of the scripted first-location tutorial (`Status: NotStarted \| InProgress \| Completed`, plus `Day`/`LocationId`/`CurrentStepId`/`CompletedStepIds`). Backs resume/replay of the authored day so it is not skipped after a quit. Scripting itself is still backlog — see below. |

---

## Future — scripted day 1 vision

The scripted day 1 is the on-ramp for the loop: an authored sequence of
customers and tutorial beats that teaches passive sales, sale chance, dialogue,
and the between-days hub through play, not text.

### Day 1 starting state

- Player drops into a single fixed location.
- Stock is **pre-seeded** (the 27-book preset above) so the shelf is alive on
  arrival; no empty shop on day 1.
- A right-hand panel shows **per-genre counts** with icons; hover/tap reveals
  the genre's current sale chance — the same UI surface that will carry the
  player through the rest of the game.
- Gold starts at 60.

### Scripted customer sequence

The first customer is an **important NPC** (the "introducer" — a named local
who later kicks off the first quest arc). They arrive, speak a short dialogue
(with branching), and tell the player they love two specific genres. The
sequence then plays out:

1. **Passive buy #1, success on a liked genre.** Tutorial HUD: *"Nice pick.
   When a customer finds a book they like, they keep browsing."*
2. **Passive buy #2 on the other liked genre, fail.** Tutorial HUD chain
   teaches sale chance:
   - *"You have books in the right genre…"*
   - *"…but the sale depends on Sale Chance."*
   - *"You can check Sale Chance at any time by hovering over the genres."*
3. The introducer leaves with the HUD playing the "purchase wrapped up"
   animation.
4. **Two more customers arrive at different walk speeds** (one slow, one
   running) — teaches per-customer `ApproachDuration` variability without
   words.
5. The next ~5 customers play out a mix of successes, fails, and one
   **dialogue-only NPC** (a journalist asking a few questions). One success
   carries a flavour line from the buyer ("Wow, you really know the ropes")
   and one carries an in-character reference ("I read about *Northern
   Letters*") — showing that the HUD can hold buyer voice, not just scoring.

### Closing ritual

Evening falls visually, a lamp turns on, and three clickable zones appear on
the shopfront — two windows and a door/shutters. Each click closes its zone.
When all are closed the UI fades and the van drives off. This replaces (or
augments) the current automatic "all customers done → next phase" condition
with a deliberate end-of-day ritual.

### Between-days hub — the Newspaper

The morning-after screen for day 1 (and onwards) is a **newspaper spread**
that combines weather, events, restock, and decor purchases. The current
Morning screen evolves into this hub once shop / restock features land.

- **Left page** — flavour article, today/tomorrow weather, this week's events
  (one notable per weekday — e.g., Saturday fleamarket), guest column.
- **Right page** ("Classifieds") — purchasable book lots and decor items,
  priced in gold. Day 1 includes a **free starter box** to teach restock
  without spending. Decor (seasonal items, e.g., a plant or a beverage cooler)
  is also bought here.

The first newspaper opens with a tutorial chain that walks the player through
weather, events, gold, current stock, and finally the Classifieds — pointed
out with text-HUD arrows on the relevant UI rectangles.

### Map and travel cost

After the newspaper, the player sees a **city map**. On day 1 only one
location is unlocked (the same as day 1's). It shows a **visit cost in gold**
(e.g., 6g). Future locations are visible but locked. Spending gold to travel
is the first opportunity-cost decision the player makes. (The entry-fee
mechanics — fields, charge order, sunk-vs-refund — are owned by
`docs/SAVE_DAY_FLOW.md`; this section only describes the day-1 framing.)

### Stocking screen

Tapping the location opens the **stocking screen** — the production-quality
Preparation phase:

- Left: **storage** (the player's full inventory) grouped by genre with
  counts.
- Center: the **daily shelf**, capacity `In Shop X / 40` (the player's
  current `DailyBookSlots`).
- Right: per-genre counters on the shelf.
- Controls: select a genre → fill from storage in genre order → confirm.

Tutorial line: *"These are the books in your storage. Select a genre by
clicking it, to start stocking your shelves."*

### Day 2 onward

After stocking, **Start** opens day 2 at the same location. The scripted
overlay does not return; the player is in the procedural loop from this point
on.

---

## Tutorial system requirements

The scripted day above needs an onboarding engine that does not exist yet.
Minimum surface area:

- A **queue of tutorial steps**, each consisting of: text, optional target
  UI rectangle to highlight, optional arrow/pointer, an advance condition
  (next click on the highlighted element / next game event / any tap).
- **UI gating** — when a tutorial step expects "click X", other interactions
  are blocked or de-emphasised.
- **Replayability** — a debug toggle to replay the scripted day from a fresh
  save.
- **Authoring** — steps are data, not code (JSON or ScriptableObject), so
  designers can iterate without recompiling.

---

## Known gaps versus the current build

| Scripted-day mechanic                         | Current build                                     | Gap                                                  |
|-----------------------------------------------|---------------------------------------------------|------------------------------------------------------|
| Probabilistic passive sale (sale chance)      | ✅ ships in `WeightedPassiveSaleSelector` (ADR-0004) | None — the gate exists; tuning ongoing.            |
| Per-genre count + storage/shop split          | ✅ aggregate over per-title shelf                  | Storage surface in UI is missing (uses raw inventory). |
| Dialogue with branching                       | ❌ no dialogue system                              | Needs `DialogueStep` + an authoring tool.            |
| Buyer voice lines in service HUD              | ❌ events carry no flavour text                    | Add `FlavourLine` field on sale events + content.    |
| Newspaper hub (weather + events + restock)    | ❌ Morning screen only                             | Combine Morning + Shop + Classifieds into one panel. |
| Decor influences sale chance                  | ✅ seam exists (`IDecorModifierProvider`, stubbed) | Real decor catalog + activation flow.                |
| End-of-day click ritual                       | ❌ ends automatically                              | New "evening" state with click zones + transition.   |
| Tutorial / onboarding engine                  | ❌ none                                            | New subsystem (see requirements above).              |
| Variable customer walk speed                  | ⚠️ uniform `ApproachDuration`                      | Per-customer `ApproachDuration` (easy change).       |
| Shelf capacity (`In Shop X/40`)               | ⚠️ `DailyBookSlots = 12` constant                  | Migrate to player state + economy config (tracked).  |
| Location travel cost                          | ✅ shipped (`LocationConfig.EntryCost` + `LocationEntryCostCalculator`, charged in `PreparationWindow.ConfirmAsync`) | Tuning + map UI surfacing. See `docs/SAVE_DAY_FLOW.md`. |
| Map UI with locked locations                  | ⚠️ data model supports list; UI is single-pick    | Map screen with locked entries + unlock conditions.  |
| Shop customisation screen                     | ❌ none                                            | Placeholder; out of scope for first FTUE pass.       |

---

## Out of scope for the first FTUE iteration

- Procedural variation of the scripted day. The day 1 sequence is authored;
  randomisation comes from day 2.
- Long-form narrative arcs. The introducer NPC, the journalist, etc. should
  each be one screen of dialogue; the larger arc lives in the future quest
  system.
- Full economy of restock and decor. The first newspaper includes free
  starter items to teach the mechanics; the real economy lands when the
  shop/restock features ship.
- A second location. Day 1 and day 2 share one location; the map exists but
  serves to set expectations, not to gate gameplay.

---

## How to ship the scripted day, when it lands

1. **Dialogue system** — minimum: linear dialogue with optional one-of-N
   choice, hooked into `SalesDayController` via a new step kind.
2. **Tutorial engine** — see requirements above.
3. **Buyer flavour lines** — extend `PassiveSaleEvent` and the active result
   event with an optional `Message` field; populate from a small content
   table.
4. **Newspaper / Classifieds screen** — net-new feature; subsumes the current
   Morning screen on day ≥ 1.
5. **End-of-day click ritual** — new state in the day FSM ("Evening") that
   runs after all customers have left.
6. **Variable walk speed** — one config field per customer archetype.
7. **Travel cost** — new field on `LocationConfig` consumed by Preparation.
8. **Map screen** — new view; reuse the location list with unlock predicates.

Items 1, 2, 4, 5 are the load-bearing pieces. The rest are content / tuning
once the systems exist.
