# Customer Step Pipeline Refactor

> Status: draft / design analysis.  
> Date: 2026-06-20.  
> Scope: documentation only. No runtime refactor is planned in this document.

## Context

Current customer simulation is based on a prebuilt linear list of `ICustomerStep`.
The list is created before the sales day starts by an `ICustomerSpawner`, then `Customer`
ticks the list by index.

Current important files:

- `Assets/Game/Features/BookSell/Services/ICustomerSpawner.cs`
- `Assets/Game/Features/BookSell/Services/DefaultCustomerSpawner.cs`
- `Assets/Game/Features/BookSell/Services/FifteenCustomersSinglePassiveAttemptSpawner.cs`
- `Assets/Game/Features/BookSell/Services/OneToThreePassiveAttemptsCustomerSpawner.cs`
- `Assets/Game/Features/BookSell/Domain/Customer.cs`
- `Assets/Game/Features/BookSell/Domain/ICustomerStep.cs`
- `Assets/Game/Features/BookSell/Domain/Steps/IClosingStep.cs`

Current default shape:

```text
ApproachStep
purchase middle: PassivePurchaseStep / ActiveRequestStep / future steps
CompletePurchaseStep
LeaveStep
```

Current runtime scenario spawners are useful for testing:

- `FifteenCustomersSinglePassiveAttemptSpawner` - 15 customers, exactly 1 passive attempt each, no active requests. Used for the empty-shelf / zero-selected-books scenario.
- `OneToThreePassiveAttemptsCustomerSpawner` - `BaseCustomers` customers, 1..3 passive attempts each, no active requests. Used for passive-only runtime testing with selected books.

The problem is not that static plans are wrong. Static plans are useful for FTUE, story customers, and runtime test scenarios. The problem is that all customer composition currently lives in spawners, so every new behavior increases duplication and makes day/customer-specific behavior harder to express.

## Planned Growth

The pipeline needs to support steps that are not known as simple fixed lists today:

1. Probability-based comments after passive purchases.
   Example: a customer passively buys a book, then may show a HUD comment about that book.

2. Decor-dependent actions.
   Example: if the player owns a coffee machine, a customer may spend time in a `DrinkCoffeeStep` and show "Drinking coffee" in the HUD.

3. Quest and story interactions.
   Example: a character arrives and starts a quest. This can hold `IInteractionLock` similarly to the active recommendation minigame.

4. Dynamic middle generation.
   `ApproachStep` and closing steps are mandatory, but the middle can be generated procedurally, scripted, or changed in reaction to runtime facts.

5. Predefined sequences.
   FTUE, unique characters, and quest chains still need explicit authored sequences.

## Current Pain Points

### 1. Spawners duplicate plan skeleton code

`DefaultCustomerSpawner`, `FifteenCustomersSinglePassiveAttemptSpawner`, and
`OneToThreePassiveAttemptsCustomerSpawner` all manually create:

```text
ApproachStep(...)
middle steps
CompletePurchaseStep()
LeaveStep(...)
```

They also duplicate `RandomInRange`, approach duration, and leave duration helpers.

### 2. Spawner names are scenario names, not reusable behavior pieces

The two runtime spawners are good as smoke-test tools, but they show the direction:
we will keep adding "one more spawner" for each runtime scenario unless plan generation is decomposed.

### 3. Static plans cannot easily react to runtime facts

The current list can route aborts to `IClosingStep`, but it cannot naturally say:

```text
Passive sale happened -> maybe insert CommentStep next
Decor exists -> maybe insert DrinkCoffeeStep after browse
Quest eligible -> insert QuestStep before closing
```

That logic can be forced into existing steps, but then `PassivePurchaseStep` becomes responsible for comments, decor, quests, and future unrelated behavior. That would make the step too broad.

### 4. Interactive steps need a shared pattern

`ActiveRequestStep` already holds `IInteractionLock`. Future quest/dialogue steps will likely need the same "try acquire lock, run while held, wait for external resolution, release lock" pattern.

## Goals

- Keep customer brain pure domain C# and EditMode-testable.
- Preserve static scripted sequences for FTUE, story characters, and runtime test scenarios.
- Reduce duplication in spawners.
- Make the mandatory skeleton centralized: approach, middle, closing tail.
- Support runtime step insertion without making individual steps know about unrelated systems.
- Keep `IClosingStep` semantics: passive failure skips remaining purchase steps but still runs completion and leave.
- Keep active recommendations, passive sales, decor actions, comments, and quests as composable behaviors.

## Non-goals

- Do not replace the simulation with a commercial behavior tree package.
- Do not introduce server replay or mid-day save serialization as part of this refactor.
- Do not move everything to configs immediately. Code-first seams are enough for the next phase.
- Do not remove runtime scenario spawners yet. They are useful for manual testing.

## Refactoring Options

### Option A - Keep current spawners and add more concrete spawners

Every scenario remains its own `ICustomerSpawner`.

Example future classes:

```text
CoffeeMachineCustomerSpawner
FtueDayOneCustomerSpawner
QuestIntroCustomerSpawner
PassiveCommentsCustomerSpawner
```

Pros:

- Very simple.
- No new abstractions.
- Good for one-off manual testing.

Cons:

- Duplicates skeleton and random helper code.
- Does not scale as features grow.
- Runtime reactions still have to be embedded into steps or pre-expanded in the list.
- DI becomes a manual toggle between many concrete classes.

Verdict: useful for short-lived smoke-test spawners, not good as the main architecture.

### Option B - Add a shared `CustomerPlanBuilder`

Keep `ICustomerSpawner`, but make spawners thin. A common builder owns the skeleton:

```csharp
public static class CustomerPlanBuilder
{
    public static Customer Build(
        string id,
        IEnumerable<ICustomerStep> middle,
        SalesTuning tuning,
        ISalesRandom random);
}
```

Builder creates:

```text
ApproachStep(random duration)
middle
CompletePurchaseStep
LeaveStep(random duration)
```

Pros:

- Low-risk refactor.
- Removes duplicated skeleton and duration helper code.
- Existing spawners stay understandable.
- Keeps runtime test spawners intact.
- Makes mandatory steps consistent.

Cons:

- Still creates a full static plan before day start.
- Does not solve runtime insertion by itself.

Verdict: recommended first step. It is a cleanup with almost no behavior change.

### Option C - Add customer archetypes for middle generation

Spawners choose customer archetypes; archetypes build only the middle of the plan.

Example interface:

```csharp
public interface ICustomerArchetype
{
    string Id { get; }

    IEnumerable<ICustomerStep> BuildMiddle(
        SalesSessionSetup setup,
        SalesTuning tuning,
        ISalesRandom random);
}
```

Examples:

- `SinglePassiveAttemptArchetype`
- `OneToThreePassiveAttemptsArchetype`
- `ProceduralShopperArchetype`
- `ScriptedSequenceArchetype`
- `QuestCharacterArchetype`

Then spawners become "day composition policies":

```text
For day/scenario, choose N archetypes.
For each archetype, build Customer via CustomerPlanBuilder.
```

Pros:

- Separates "how many customers today" from "what this customer does".
- Supports procedural and scripted customers under one model.
- Runtime smoke scenarios become small archetype selections, not duplicated full spawners.
- Easy stepping stone toward config-driven generation later.

Cons:

- Adds one abstraction layer.
- Still static unless combined with runtime insertion/director.

Verdict: recommended second step after `CustomerPlanBuilder`.

### Option D - Mutable customer plan with runtime insertion

Replace raw internal `List<ICustomerStep>` traversal with a plan object that supports safe insertion:

```csharp
public interface ICustomerPlan
{
    ICustomerStep Current { get; }
    bool IsDone { get; }

    void Advance();
    void InsertNext(ICustomerStep step);
    void InsertBeforeClosing(ICustomerStep step);
    void SkipToClosing();
}
```

Important rules:

- Inserted steps must not appear after the first `IClosingStep` unless explicitly intended.
- `SkipToClosing` remains protected and deterministic.
- There should be an injection budget or guard to prevent infinite self-insertion.

Pros:

- Supports "after passive sale, insert comment next".
- Supports "decor active, insert coffee step".
- Supports quest chains and story beats.
- Moves skip-to-closing logic out of `Customer`.

Cons:

- More complex than a fixed list.
- Needs focused tests for insertion order, closing protection, and abort behavior.
- If mid-day save ever appears, injected plan state becomes part of serialization.

Verdict: useful, but should come after the builder/archetype cleanup unless a runtime-injected step is needed immediately.

### Option E - Customer director / rule engine

Introduce a central domain service that observes customer facts and decides whether to inject steps.

Possible interface:

```csharp
public interface ICustomerDirector
{
    void OnPassiveSale(Customer customer, PassiveSaleEvent sale, CustomerContext ctx);
    void OnPassiveFailure(Customer customer, CustomerContext ctx);
    void OnActiveResolved(Customer customer, RecommendationResult result, CustomerContext ctx);
}
```

Example rules:

```text
OnPassiveSale:
  if ctx.Random.NextDouble() < commentChance:
      customer.Plan.InsertNext(new CommentStep(bookId))

OnStepBoundary or OnPassiveSale:
  if coffeeMachineActive and ctx.Random.NextDouble() < coffeeChance:
      customer.Plan.InsertNext(new DrinkCoffeeStep())
```

Pros:

- Keeps `PassivePurchaseStep` focused on passive purchase.
- New feature rules live in one place.
- Easy to test with fake random and fake plans.
- Avoids scattering decor/comment/quest logic through unrelated steps.

Cons:

- Needs careful event timing. If `OnPassiveSale` fires inside `PassivePurchaseStep.Tick`, insertion must happen before `Customer.Advance`.
- Needs access to the customer plan, not only the customer state.
- Can become a "god object" if rules are not split by feature.

Verdict: recommended for runtime-injected behavior, but design it as a coordinator of small feature rules, not one giant class.

Timing decision:

- `CustomerDirector` should receive domain facts synchronously through the same path as `ISalesDaySink`, before `Customer.Advance`.
- It should not depend on `ISalesDayController` C# events as the primary mechanism. Controller events are useful for UI and logs, but they add an unnecessary hop for plan mutation.
- Example: `PassivePurchaseStep` emits `OnPassiveSale` before returning `StepStatus.Completed`. A director handling that fact can call `InsertNext(new CommentStep(...))`, and the inserted step becomes the next step when `Customer` advances in the same tick.

Boundary rule:

- `Spawner`/`Archetype` decide what is known at customer creation time.
- `CustomerDirector` decides only from facts that happen during the visit.
- A quest customer known before spawn belongs in `QuestCharacterArchetype` or `ScriptedSequenceArchetype`, not in `CustomerDirector`.
- A quest step unlocked by an in-visit outcome can be injected by `CustomerDirector`.

Determinism rule:

- Director rules must use `ctx.Random` / `ISalesRandom` for all probability rolls.
- They must not use `UnityEngine.Random`, otherwise EditMode tests and seeded replay of a sales day become unreliable.
- Director lifetime should be one instance per sales day, similar to `CustomerContext`/`ISalesDaySink`, registered through DI when introduced.

### Option F - Data-driven scripts/configs

Move archetypes and scripted sequences to configs or ScriptableObjects.

Example authored middle:

```text
PassiveAttempt
Comment("intro_line_01")
Quest("quest_first_regular")
PassiveAttempt
```

Pros:

- Great for FTUE, story customers, quest chains, and balancing.
- Designers can author day/customer behavior without C# changes.

Cons:

- Requires step factory, validation, config schema, editor workflow.
- Too early if the step vocabulary is still moving.

Verdict: good later phase. First create code seams that can be backed by data later.

### Option G - Full behavior tree / utility AI

Replace linear plan with a behavior tree or utility decision system.

Pros:

- Powerful for complex AI.
- Can express selectors, decorators, and priorities.

Cons:

- Overkill for current needs.
- Harder to preserve exact FTUE/story sequences.
- More difficult to test deterministically.
- Does not remove the need for shelf reservation, lock arbitration, and domain pause.

Verdict: not recommended now. Reconsider only if linear plans plus runtime insertion become unmanageable.

## Recommended Direction

Use a hybrid model:

```text
Spawner = chooses customers/archetypes for the day
Archetype = builds the middle of a customer's initial plan
CustomerPlanBuilder = adds mandatory skeleton and closing tail
CustomerPlan = owns traversal, insertion, and skip-to-closing
CustomerDirector = injects optional runtime steps based on facts
```

This preserves both needs:

- Static authored sequences are still possible.
- Runtime features can insert steps when facts happen.

Recommended target flow:

```text
SalesDayController.StartDayAsync
  -> ICustomerSpawner.BuildCustomers
      -> choose archetypes / scripted customers
      -> CustomerPlanBuilder.Build(...)
          -> ApproachStep
          -> archetype middle
          -> CompletePurchaseStep
          -> LeaveStep

During Tick
  -> Customer ticks current step
  -> step emits domain facts through sink
  -> CustomerDirector handles facts synchronously and may insert next step
  -> Customer advances
```

The key architectural split:

- Spawn-time composition is owned by `ICustomerSpawner` and `ICustomerArchetype`.
- Runtime reaction is owned by `CustomerDirector`.
- If a step is known before the customer starts the visit, put it in the initial archetype/script.
- If a step depends on an in-visit outcome, inject it through the director.

## How Future Features Fit

### Passive purchase comment

Preferred:

```text
PassivePurchaseStep succeeds
OnPassiveSale fact emitted
CustomerDirector rolls comment chance
CommentStep inserted next
Customer advances into CommentStep
```

Avoid making comments part of `PassivePurchaseStep`. It would couple purchase economy to presentation/story behavior.

`CommentStep` needs a payload gap to be solved before implementation:

- Preferred: director resolves the needed book/comment metadata and creates `CommentStep` with a ready-to-display payload.
- Alternative: add a read-only config/lookup dependency to `CustomerContext`, but this makes every step able to reach configs.
- Recommendation: keep `CommentStep` simple. It should display a prepared text/payload and hold for a duration, not resolve configs itself.

HUD ordering must be designed together with `Fail` and `CompletePurchase` bubbles:

- If passive failure happens after at least one passive sale, `CompletePurchaseStep` should be allowed to replace the fail bubble with the completion bubble.
- If passive failure happens with zero passive sales, the fail bubble can remain through the walk-away.
- A comment should not be injected after a passive failure abort, because comments are middle steps and `SkipToClosing` should skip the middle.

### Coffee machine / decor action

Preferred:

```text
CustomerDirector sees ctx.ActiveDecorIds
Rule rolls chance or checks customer archetype
DrinkCoffeeStep inserted before closing or after a purchase
HUD shows "Drinking coffee"
Step holds for configured duration
```

Decor can also affect passive sale chance through existing passive selector/calculator paths. That is separate from decor action steps.

### Quest customer

Two valid forms:

1. Static scripted archetype:

```text
Approach -> QuestStep -> CompletePurchase -> Leave
```

2. Runtime injection:

```text
Passive sale happened / active resolved / decor interaction happened / other in-visit condition met
CustomerDirector inserts QuestStep
```

If quest eligibility is known before the visit, it belongs to spawn-time composition:

```text
QuestCharacterArchetype -> Approach -> QuestStep -> CompletePurchase -> Leave
```

`QuestStep` should probably share a lock-holding pattern with `ActiveRequestStep`.

Possible later abstraction:

```csharp
public abstract class LockHoldingStep : ICustomerStep
{
    // TryAcquire, Running while held, Release on Exit.
}
```

This abstraction should only cover acquire/hold/release mechanics. Resolution stays feature-specific:

- active recommendation resolves through `RecommendBook` / `SkipCurrentRequest`;
- quest/dialogue interactions will need their own resolution channel.

Do not add this abstraction until the second lock-holding step actually exists.

### FTUE and story customers

Use `ScriptedSequenceArchetype` or direct scripted customer construction.

Static example:

```text
Approach
CommentStep("ftue_welcome")
PassivePurchaseStep
QuestStep("first_recommendation")
CompletePurchaseStep
Leave
```

Important: scripted sequences can still receive runtime injections if desired, but they should be able to opt out. FTUE often needs fewer random interruptions.

## Suggested Migration Plan

### Phase 0 - Document and stabilize current behavior

No architecture change.

- Keep current spawners.
- Keep runtime scenario spawners.
- Add/maintain tests around:
  - passive failure routes to closing steps,
  - complete purchase runs after prior passive sales,
  - active requests still hold the lock.

### Phase 1 - Extract `CustomerPlanBuilder`

Behavior-preserving cleanup.

- Move approach/leave duration helper logic into one builder/helper.
- Move mandatory skeleton creation into the builder.
- Rewrite current spawners to provide only middle steps. If later generation needs more context, move that responsibility to archetypes in Phase 2 rather than mixing builder signatures.

Expected result:

- Less duplication.
- Safer future changes to mandatory skeleton.
- Runtime scenario spawners remain easy to read.

### Phase 2 - Introduce code-first archetypes

- Add `ICustomerArchetype`.
- Convert:
  - `FifteenCustomersSinglePassiveAttemptSpawner` to use `SinglePassiveAttemptArchetype`.
  - `OneToThreePassiveAttemptsCustomerSpawner` to use `OneToThreePassiveAttemptsArchetype`.
  - `DefaultCustomerSpawner` to use a procedural archetype.
- Keep `ICustomerSpawner` as the day composition policy.

Expected result:

- Spawners choose who appears.
- Archetypes describe what a customer initially does.

### Phase 3 - Introduce `CustomerPlan`

- Hide raw list/index traversal behind a plan object.
- Move `SkipToClosing` out of `Customer`.
- Add safe insertion APIs:
  - `InsertNext`
  - `InsertBeforeClosing`
- Add tests for insertion and closing-tail protection:
  - `InsertNext` puts a runtime step immediately after the current step.
  - `InsertBeforeClosing` never inserts after the first `IClosingStep`.
  - `SkipToClosing` skips injected middle steps as well as initially authored middle steps.
  - injection followed by passive-failure abort routes to `CompletePurchaseStep`/`LeaveStep`, not to an injected comment/decor step.

Expected result:

- Runtime insertion becomes possible without exposing raw list mutation.

### Phase 4 - Add first runtime-injected step

Use a small feature as proof:

- `CommentStep` after passive sale with chance.
- Add `CustomerDirector` or a narrower `PassiveSaleCommentRule`.
- Keep rule code-first.
- Unit-test the rule with fake `ISalesRandom`; do not use `UnityEngine.Random`.
- Unit-test event timing: `OnPassiveSale` -> director inserts `CommentStep` -> `Customer.Advance` enters the comment next.
- Unit-test HUD ordering with completion/failure bubbles before wiring presentation broadly.

Expected result:

- Proves event timing and insertion semantics.
- Avoids designing an abstract system without a concrete use case.

### Phase 5 - Decor and quest steps

- Add decor action step only when a decor item actually needs it.
- Add quest/interaction step when a quest flow exists.
- Extract shared lock-holding helper only after `ActiveRequestStep` has a real sibling.

### Phase 6 - Data-driven authoring

Once step vocabulary stabilizes:

- Add config/SO representation for archetypes.
- Add step factory and validation.
- Move FTUE/story/customer scripts to data where useful.

## Test Coverage Notes

Keep `StubCustomerSpawner` for controller tests that need exact authored plans.

New abstractions should have focused unit tests:

- `CustomerPlanBuilder`
  - builds `Approach -> middle -> CompletePurchase -> Leave`;
  - preserves middle step order;
  - applies approach/leave duration rules consistently.
- `ICustomerArchetype`
  - procedural archetypes generate the expected middle ranges;
  - scripted archetypes preserve exact authored order;
  - runtime scenario archetypes match the current smoke spawners.
- `CustomerPlan`
  - advances like the current raw list;
  - inserts runtime middle steps in deterministic order;
  - protects the closing tail;
  - skips injected middle steps on passive-failure abort.
- `CustomerDirector`
  - handles facts synchronously before `Advance`;
  - uses `ISalesRandom`;
  - respects opt-out flags for FTUE/story customers if those flags are added.
- Lock-holding steps
  - share acquire/hold/release behavior only;
  - keep resolution channels feature-specific.

## Open Questions

1. Should runtime test spawners stay in production assemblies, or move behind debug/dev configuration later?

2. Should FTUE scripted customers opt out of random director injections by default?

3. How should injected steps be limited?
   - Max injected steps per customer?
   - Max consecutive comment/decor steps?
   - Feature-specific cooldowns?

4. Do quest steps need a separate resolution channel from active recommendations?

5. If mid-day save is added later, do we serialize the current plan including injected steps, or restart the sales day?

6. Does `CommentStep` receive fully prepared HUD text/payload, or should `CustomerContext` expose a read-only config lookup? Preliminary recommendation: prepared payload.

## Preliminary Recommendation

Do not jump straight to a mutable runtime brain.

Recommended next concrete work:

1. Extract `CustomerPlanBuilder`.
2. Convert the three real spawners to use it.
3. Introduce code-first archetypes.
4. Only then add mutable plan insertion for the first real dynamic feature, most likely probability-based `CommentStep` after passive sale.

This keeps the system simple now, reduces duplication immediately, and leaves a clean path for comments, decor actions, quests, FTUE, and story customers.
