# Save and Day Flow

Current reference for how persisted save data interacts with the day loop,
especially when the player exits during `LocationScene`.

Related:

- [ADR-0001](adr/0001-save-data-modular-payload.md) - save data model.
- [ADR-0003](adr/0003-customer-simulation.md) - sales simulation and current no-runtime-save decision.
- [GameFlowLoop.md](GameFlowLoop.md) - hub/location scene flow.
- [FTUE.md](FTUE.md) - first-entry flow and starter seeding.

---

## Save Model

The save file is a modular payload:

```text
SaveData
  Meta
  Modules: Dictionary<string, ModulePayload>
```

Each feature owns its module key, DTO, and schema version. The Save assembly
does not know feature data types. A feature reads with `GetModuleAsync<T>()`
and writes with `UpdateModuleAsync(moduleKey, dto, schemaVersion, ct)`.

`UpdateModuleAsync` updates the in-memory module table, marks the save dirty,
and schedules a debounced save. The debounce delay is currently 600 ms. A
`ForceWithSync` mode exists, but the current gameplay flow does not appear to
force-save on application pause or quit.

Important consequence: a feature write is immediately visible to other systems
in memory, but a hard app kill shortly after the write can still lose the most
recent debounced disk/server flush.

---

## Main Modules

| Module key | Owner | What it stores |
|---|---|---|
| `ftue.applied` | FTUE | Whether starter gold/books were seeded. |
| `resources` | Resources | Resource amounts, currently including `gold`. |
| `inventory` | Inventory | Owned unique items and stack items, including books. |
| `day_progress` | DayCycle | Current day, current phase, completed days. |
| `preparation.session` | Preparation | Selected location, selected shelf book ids, genre quantities, confirm flag. |
| `book_sell.shelf_state` | BookSell | Current shelf book ids and sold book ids. |
| `book_sell.last_day_result` | BookSell | Completed sales-day summary, written only at day completion. |
| `sales_stats` | SalesStats | Aggregated sold counts by genre. |
| `progression` | Progression | Player progression values, such as reputation. |
| `location_unlock` | LocationUnlock | Location unlock state. |
| `shop` | Shop | Shop state and purchase limits. |

The list is decentralized by design. New persisted features should add their
own keys and keep the DTO/version local to the owning feature or its API
assembly.

---

## Current Day Flow

### Boot

1. Save loads in the loading pipeline.
2. Save hooks hydrate in-memory services such as resources, inventory, stats,
   progression, and decor.
3. FTUE bootstrap runs after save load. It seeds starter gold/books only once
   and writes `ftue.applied`.
4. `GameplayScene` opens as the hub.

### Hub / Morning

`GameplaySceneController` starts or resumes the morning context. If the current
day is not completed, `MorningSessionService.StartOrResumeAsync` sets
`day_progress.CurrentPhase` back to `Morning`.

This means that an incomplete `Sales` phase does not currently restore the
player into `LocationScene` on the next boot.

### Preparation

When the player continues from the hub:

1. `MorningSessionService.ContinueToPreparationAsync` sets phase to
   `Preparation`.
2. `PreparationSessionService` builds or resumes `preparation.session`.
3. Confirm writes selected shelf books into `book_sell.shelf_state`, persists
   `preparation.session`, and sets `day_progress.CurrentPhase = Sales`.
4. `IGameFlowService.EnterLocationAsync` loads `LocationScene` additively over
   the hub.

### Location / Sales

`SalesScreenView` starts `SalesDayController` for the current day. The
controller builds a runtime-only sales day from the setup provider:

- confirmed `preparation.session`, when present;
- otherwise a fallback shelf from configs.

The current runtime sales simulation is not serialized. Customers, active
steps, locks, request progress, timers, and dialogs exist only in memory.

However, sale consequences are persisted during the day:

- active and passive sales add gold through `IResourcesService`, writing the
  `resources` module;
- sold books are removed from `inventory`;
- sold books are removed from `book_sell.shelf_state.ShelfBookIds` and added to
  `SoldBookIds`;
- sales stats are recorded in memory and flushed through the save hook on the
  next save cycle.

At actual day completion, `SalesDayController` writes
`book_sell.last_day_result` before emitting `DayCompleted`.

### Results / Next Day

After `DayCompleted`, `SalesScreenView` returns to the hub and opens
`ResultsWindow`.

`ResultsSummarySessionService.LoadAndApplyAsync` reads
`book_sell.last_day_result`, builds the summary, and marks the current day
completed in `day_progress`.

`AdvanceToNextDayAsync` then increments `CurrentDay`, resets the phase to
`Morning`, and keeps only the normal persistent modules. There is no separate
archive of each runtime sales day.

---

## Exit During LocationScene

If the player exits while the sales day is still running:

1. The runtime day is lost. Customers, current request/dialogue, lock state,
   and timers are not restored.
2. Any sale side effects that already reached save services may persist:
   `resources`, `inventory`, `book_sell.shelf_state`, and possibly
   `sales_stats`.
3. `book_sell.last_day_result` is not written unless the day completed.
4. `day_progress` may still say `Sales`, but the next hub startup calls
   `MorningSessionService.StartOrResumeAsync`, which moves an incomplete
   current day back to `Morning`.
5. The player therefore returns to the hub, not to the middle of the location.

This is close to the ADR-0003 MVP decision that "the day is recreated on
restart", but the current implementation already persists some economic
side effects during the day. That creates a partial-progress behavior rather
than a pure restart.

---

## FTUE Impact

`ftue.welcome_completed` should only mean that the player finished the welcome
letter window. It should not mean that the first location tutorial is complete.

The first location tutorial needs its own persisted state if it must not be
skipped after a quit. A future module could track one current tutorial/day run,
for example:

```text
ftue.first_location_tutorial
  Status: NotStarted | InProgress | Completed
  Day
  LocationId
  CurrentStepId
  CompletedStepIds
```

If the tutorial includes authored customer/dialogue content, it should either
restore from this state or intentionally restart the tutorial from a safe
checkpoint. The current save/day flow does not provide that guarantee.

---

## Chosen Direction

Use a transactional sales day with defer-commit.

During `LocationScene`, sales effects are provisional. The day can update
runtime UI and in-memory day result, but it must not write day-scoped economic
effects into persistent modules until the day is completed.

High-level rule:

```text
Start sales day
  -> build runtime day from preparation/session setup
  -> collect provisional sold books, gold, stats, and result in memory
  -> if player exits before completion: discard runtime state
  -> if day completes: atomically commit all day effects
```

This matches the current ADR-0003 behavior that runtime sales state is not
restored after restart, but fixes the current mismatch where gold, inventory,
shelf state, and stats can leak into the save during an unfinished day.

The final commit should apply together:

- add earned gold to `resources`;
- remove sold books from `inventory`;
- update `book_sell.shelf_state`;
- flush sales stats;
- write `book_sell.last_day_result`;
- let Results mark `day_progress` as completed.

Implementation note: moving writes into `PublishCompletionAsync` is not enough
by itself. The commit path should block autosave while multiple modules are
updated, then force-save after the full commit succeeds. Otherwise a crash
between module writes can still leave partial persistent state.

For player-facing behavior, an unfinished day simply did not happen. On the
next launch the player returns to the hub and can start that day again from the
prepared setup.

### Why This Is Preferred

- It removes the current partial-progress leak instead of adding a complex
  restore system around it.
- It avoids serializing customers, step indices, injected director steps,
  interaction locks, timers, and unfinished dialogue state while the step
  pipeline is still changing.
- It keeps the sales phase easy to reason about: Preparation commit is the
  input, Results commit is the output, and Sales itself is provisional.
- UI can still show live provisional income through the existing sales-gold HUD
  without touching the persistent wallet.

### Implementation Targets

- `SalesGoldCollector`: collect earned gold in memory; apply to
  `IResourcesService` only during final commit.
- `SoldBookCommitter`: collect sold book ids in memory; remove from
  `IInventoryService` only during final commit.
- `SalesShelfStateService`: avoid writing sold shelf state during the running
  day, or separate runtime shelf state from persisted shelf state.
- `SalesStatsService`: avoid flushing day sales through autosave before final
  commit. `RecordSold` currently sets `_dirty` and calls `_save.MarkDirty()`,
  so it must NOT be called per sale during a transactional day — accumulate the
  sold ids / delta in the day buffer and update stats only at final commit.
- Final commit: make the full day application idempotent (see Idempotency below).

### Entry Fee (Sunk Visit Cost)

Entering a location costs gold per visit. The amount is per-location and can be
raised (or lowered) by active decor. This is a **per-visit fee**, distinct from
`LocationConfig.UnlockCost`, which is the one-time unlock price owned by the
LocationUnlock feature.

The entry fee is the save-scum lever. The chosen rule:

> The entry fee is a **committed visit-attempt cost** — it is NOT part of the
> sales transaction and does NOT roll back on exit. Only the day's **sales
> effects** roll back.

So the accurate model is *"the Sales effects did not happen; the entry attempt
did happen"* — not *"the whole day did not happen"*. Use that wording in design
docs.

Why sunk and not refunded:

- Refunding the fee on exit makes re-entry free → infinite save-scum (enter,
  see bad RNG, quit, re-enter at no net cost).
- A sunk fee makes every fresh attempt cost gold, so quitting is always at least
  as bad as finishing the day.
- Related lever: persisting a per-day seed would make a re-entered day identical
  (save-scum pointless) and could allow a softer fee. With no seed today, the
  fee is the practical deterrent. (Open decision — see below.)

New data + seam:

- `LocationConfig.EntryCost` (gold; separate from `UnlockCost`).
- `DecorConfig.EntryCostDelta` — **neutral, signed** contribution (allow negative
  so decor can also discount; do not frame decor purely as a penalty). Do not
  conflate with a future `DailyUpkeepCost` mechanic.
- `ILocationEntryCostCalculator` (mirror of `IDecorModifierProvider`):
  `cost = EntryCost(location) + Σ EntryCostDelta(activeDecor)`, clamped ≥ 0.
  Active decor from `IDecorPlacementService.GetActiveDecorIds()`.
- Preparation UI shows the cost breakdown (base + decor) and disables Confirm
  when the player cannot afford it.

### Confirm / Entry Order (do gold check BEFORE confirm)

`PreparationWindow.ConfirmAsync` today calls `_session.ConfirmAsync` first
(which writes `preparation.session`, `shelf_state`, and phase `Sales`) and only
then `EnterLocationAsync`. Charging/validating the fee after confirm risks the
state *"preparation confirmed but entry blocked"*. Correct order:

```text
1. calculate entry cost (location + active decor)
2. Has(gold, cost)?  -> if not, do NOT confirm; show shortfall, keep window open
3. _session.ConfirmAsync   (commit shelf selection + phase Sales)
4. RemoveAsync(gold, cost)  (commit the sunk visit fee)
5. EnterLocationAsync
```

Failure path: if `EnterLocationAsync` fails, the recovery must restore **both**
the gold (refund the fee) **and** the Preparation UX — not just gold. Note that
`preparation.session.Confirmed == true` will make `StartOrResume` build a fresh
state, so `ReopenAfterTransitionFailureAsync` must account for that, not assume a
clean reopen.

### Commit Ownership and Boundaries

- `Preparation confirm` = input committed (shelf selection).
- `Enter location` = ante committed (entry fee, sunk). **Refunded only on a technical
  entry failure** (`EnterLocationAsync` threw → the visit never started). **Never rolled
  back on a normal exit/quit during the day.**
- `Sales` = provisional, runtime buffer only.
- `Results` = output committed (sales effects applied).
- `Exit mid-day` = discard the buffer; entry fee stays spent; replay the day.

Keep `CompletedDays` ownership with **Results**, not the sales commit.
`ResultsSummarySessionService.LoadAndApplyAsync` marks the day completed today
([ResultsSummarySessionService.cs:64](Assets/Game/Features/DayCycle/Results/Services/ResultsSummarySessionService.cs)).
The sales commit should atomically apply sales effects + `last_day_result`;
Results then idempotently marks the day completed. Moving `CompletedDays` into
the sales commit changes that contract (Sales would drive DayCycle phase) and
must be a separate, explicit decision with updated Results tests.

### Dedicated Commit Service (do not bloat `SalesDayController`)

`SalesDayController` is already near a god object. Introduce
`ISalesDayCommitService.CommitAsync(SalesDayCommit commit, ct)`. The controller
only assembles the result and calls commit; the service does the work: take a
`BlockAutosave` lease, apply resources / inventory / shelf / stats /
`last_day_result`, then one forced save.

Also rename the buffering seams so intent is clear after defer-commit: today
`FlushAsync` means "await launched write-through tasks"
([SalesGoldCollector.cs:36](Assets/Game/Features/BookSell/Services/SalesGoldCollector.cs),
[SoldBookCommitter.cs:42](Assets/Game/Features/BookSell/Services/SoldBookCommitter.cs)).
After defer-commit it means "apply the accumulated effects". Prefer
`Collect...` + `ApplyAsync`, or fold both into one `SalesDayEffectsBuffer`.

### Idempotency

`CompletedDays` alone is not enough. If the commit fails after
`resources.AddAsync` but before `last_day_result`, a retry could double-grant
gold. Mitigations:

- Apply all effects inside one `BlockAutosave` lease followed by a single forced
  save, so the persistence window is one file write (minimizes the partial-state
  gap).
- Add a stronger guard: a commit id in `last_day_result` (or a
  `book_sell.applied_day_commits` marker) so a re-run of the same day's commit is
  a no-op. Document the residual crash-mid-commit risk explicitly.

### Open Decisions

1. ✅ ~~Entry fee on exit — sunk vs refunded~~ — **Resolved:** sunk on normal exit/quit; the
   only refund is on a technical `EnterLocationAsync` failure (the visit did not start). One
   contract, no save-scum loophole.
2. Persist a per-day seed? If yes, re-entry is deterministic (save-scum moot) and
   the fee can be softer; if no, the fee is the main deterrent.
3. Tutorial day is excluded from RNG rollback — it keeps its own checkpoint state
   (`ftue.first_location_tutorial`), authored content must not be re-rolled.

---

## Considered Alternative: Current Day Resume

The first option considered was saving one generated day at a time and resuming
it after relaunch:

```text
day_run.current
  Day
  LocationId
  Seed
  GeneratedCustomers[]
  RuntimeProgress
  Status: InProgress | ReadyToClose | Completed
```

This would allow the game to resume the same generated day after relaunch. The
hard part is not storing the generated customer plan; it is deciding what to do
with partially completed interactions, reserved books, provisional gold,
dialogue choices, and tutorial steps.

For quest dialogues with choices, the safer rule is to persist completed
dialogue decisions immediately, and on quit during an unfinished dialogue resume
from the last stable dialogue node or restart that dialogue before applying any
choice effects.

---

## Development

`CUSTOMER_STEP_PIPELINE_REFACTOR.md` describes the likely direction for future
customer composition:

```text
Spawner -> Archetype -> CustomerPlanBuilder -> CustomerPlan -> CustomerDirector
```

Responsibilities:

- `ICustomerSpawner` chooses which customers appear in the day.
- `ICustomerArchetype` builds the initial middle steps for a customer.
- `CustomerPlanBuilder` adds the mandatory skeleton:
  `Approach -> middle -> CompletePurchase -> Leave`.
- `CustomerPlan` owns traversal, insertion, and skip-to-closing behavior.
- `CustomerDirector` observes in-visit facts and injects optional runtime steps.

The director is the proposed place for behavior that is not known when the
customer is spawned:

- after a passive sale, insert `CommentStep`;
- if decor is active, insert `DrinkCoffeeStep`;
- if an in-visit quest condition is met, insert `QuestStep`;
- if an active recommendation resolves in a special way, insert a follow-up
  beat.

Spawn-time and runtime composition should stay separate:

- if a quest/story customer is known before the visit, use
  `QuestCharacterArchetype` or `ScriptedSequenceArchetype`;
- if the step depends on something that happened during the visit, inject it
  through `CustomerDirector`.

The director should receive domain facts synchronously before
`Customer.Advance`, not through `SalesDayController` UI/log events. Example:
`PassivePurchaseStep` emits `OnPassiveSale`, the director calls
`customer.Plan.InsertNext(new CommentStep(...))`, and the inserted comment
becomes the next step when the customer advances.

### Impact on Current Day Resume

If mid-day resume is added later, the generated day should account for this
future pipeline. The save module can either persist the full current plan state,
or persist enough stable inputs to rebuild the day and resume from checkpoints.

Full plan-state approach:

```text
day_run.current
  Day
  LocationId
  Seed
  Customers[]
    Id
    ArchetypeId
    Profile
    PlanSteps[]
    CurrentStepIndex
    InjectedSteps[]
    CompletedFacts[]
  Status
```

Pros: closest to exact resume.

Cons: every new step type needs serializable state; injected director steps
become part of the save schema.

Checkpoint/rebuild approach:

```text
day_run.current
  Day
  LocationId
  Seed
  GeneratedCustomerSpecs[]
  CommittedFacts[]
  CurrentStableCheckpoint
  Status
```

Pros: simpler and more resilient while the step vocabulary is still changing.

Cons: resume is less exact; the game must define where safe checkpoints are.

For tutorial and quest dialogues, prefer stable checkpoints. A dialogue choice
should be committed only at an explicit commit point. If the player exits during
an unfinished dialogue, resume from the last stable dialogue node or restart
that dialogue segment before applying choice effects.

### Snapshot/restore variant

Another possible design is snapshot/restore: treat a started but unfinished day
as temporary, but allow day effects to write during the day and restore a
baseline if the player exits before completion.

High-level rule:

```text
Start day
  -> create day_run.current baseline snapshot
  -> run sales/tutorial
  -> if day completed: commit results and clear day_run.current
  -> if app exits/restarts before completion: restore baseline and restart day
```

The baseline must contain every save-backed value that can be mutated during
the day:

```text
day_run.current
  Day
  LocationId
  Seed
  Baseline
    Resources
    Inventory
    ShelfState
    SalesStats
    PreparationSession
    TutorialState
  Status: InProgress | Completed
```

On restart, if `day_run.current.Status == InProgress`, the game would restore
the baseline modules, discard unfinished runtime state, and return the player to
the beginning of the day/tutorial.

Pros:

- simple player-facing rule: unfinished day did not happen;
- avoids serializing every customer step, injected director step, lock, timer,
  and unfinished dialogue;
- good fit for FTUE/tutorial, where skipping content is worse than replaying it.

Cons:

- requires atomic-ish baseline capture before any sale can mutate resources or
  inventory;
- every day-mutated module must be included, otherwise partial side effects can
  leak through;
- repeated app kills can replay the same day from the start, which may be fine
  for tutorial but needs a product decision for normal locations;
- if purchases/quest rewards become available inside a location, those modules
  must join the baseline or be delayed until day commit.

This is not the preferred implementation right now. Defer-commit is cleaner:
if sale rewards and sold-book removal are buffered inside the day run and
committed only at day completion, there is no unfinished economic state to
restore.
