# Customer Traffic Count System

> Status: proposed / not implemented.
> Date: 2026-07-10.
> Scope: calculates how many regular customers should visit during a sales day. Special/story customers are noted as a future composition layer, not part of this iteration.

## Context

Current customer simulation builds the whole customer list before the sales day starts:

- `SalesDayController.StartDayAsync` calls `ICustomerSpawner.BuildCustomers(setup, tuning, random)`.
- `SalesSessionSetup` already carries the main inputs needed for traffic decisions: `Day`, `LocationId`, `ShelfBookIds`, and `DecorIds`.
- `DefaultCustomerSpawner` currently owns customer count directly with `count = max(requestCount, tuning.BaseCustomers)`.
- Runtime scenario spawners also own their own counts, often as constants, because they are used as smoke-test tools.
- `CustomerPlanBuilder` centralizes the mandatory plan skeleton, while spawners remain responsible for day composition.
- `QuestSchedulingCustomerSpawner` is already a decorator over a base spawner and prepends quest characters. This is useful precedent for future special-character scheduling.

The current count logic is too local to each spawner. The number of visitors should become a separate policy so it can evolve without duplicating logic across spawners.

## Goal

Create one system responsible for calculating the number of regular customers for a day.

The system must support:

- predefined customer counts for specific days;
- a hard final override for scripted days, for example day 1 has exactly 3 customers;
- optional modifiers from location, decor, weather, calendar, events, global effects, and future systems;
- deterministic, EditMode-testable calculation;
- extension without changing the core resolver for every new source of traffic influence.

## Non-goals

- Do not decide exact customer archetypes in this system.
- Do not schedule quest/story/FTUE characters here.
- Do not replace `ICustomerSpawner` with config-driven scripting in this iteration.
- Do not introduce weather, calendar, or global-effect systems as part of this first implementation.
- Do not move runtime smoke-test spawners out of production assemblies in this iteration.

## Key Decision

Add a separate customer traffic resolver, conceptually:

```csharp
public interface ICustomerTrafficResolver
{
    int ResolveCustomerCount(SalesSessionSetup setup, SalesTuning tuning);
}
```

The active production spawner asks this resolver for the number of regular customers, then builds that many regular customer plans.

The resolver owns "how many customers". The spawner owns "what kind of regular customer plans are built".

## Baseline Config

Introduce a dedicated traffic config, separate from `SalesTuning`.

`SalesTuning` is currently timing and pacing data: approach duration, browse duration, spawn interval, max concurrent customers, and similar knobs. Day-by-day traffic is content/balance data and should not live there long-term.

Conceptual config:

```json
{
  "defaultCustomerCount": 10,
  "minCustomerCount": 0,
  "maxCustomerCount": 50,
  "dayOverrides": [
    { "day": 1, "customerCount": 3, "applyModifiers": false },
    { "day": 2, "customerCount": 5, "applyModifiers": true }
  ]
}
```

Field meaning:

- `defaultCustomerCount`: baseline when the day has no explicit row.
- `minCustomerCount`: final lower clamp.
- `maxCustomerCount`: final upper clamp.
- `dayOverrides[].day`: day number.
- `dayOverrides[].customerCount`: baseline or final count for that day.
- `dayOverrides[].applyModifiers`: if `false`, the value is a final hard override.

Confirmed rule:

- Day 1 can be configured as `customerCount = 3`, `applyModifiers = false`.
- That means exactly 3 regular customers, with no decor, weather, location, or event modifiers applied.

## Calculation Flow

1. Resolve the day baseline.
   - If the day has an override, use that count.
   - Otherwise use `defaultCustomerCount`.

2. Check whether modifiers are enabled.
   - If the matching day override has `applyModifiers = false`, return the clamped override directly.
   - If no override exists, modifiers are enabled by default.
   - If an override exists with `applyModifiers = true`, use the override as baseline and continue.

3. Build a context from the current setup.
   - day number;
   - location id and optional `LocationConfig`;
   - active decor ids;
   - future weather id/state;
   - future calendar/day-of-week/event/global-effect facts.

4. Run all registered traffic contributors.

5. Combine contributions.

6. Round and clamp the final result.

Conceptual formula:

```text
raw = (baseline + flatDelta) * percentMultiplier
final = clamp(round(raw), minCustomerCount, maxCustomerCount)
```

Example:

```text
baseline = 10
location = +20%
rain = -5%
decor = +1 flat

raw = (10 + 1) * 1.20 * 0.95 = 12.54
final = 13
```

Rounding policy should be explicit in config or code. Recommended default: round to nearest integer, away from zero only if needed for designer clarity. Avoid hidden floor behavior because small positive modifiers would feel broken.

## Contributor Pipeline

Use a contributor/modifier pipeline, not classic Chain of Responsibility.

Conceptual contract:

```csharp
public interface ICustomerTrafficContributor
{
    void Contribute(CustomerTrafficContext context, CustomerTrafficAccumulator accumulator);
}
```

Alternative immutable shape:

```csharp
public interface ICustomerTrafficContributor
{
    CustomerTrafficContribution Evaluate(CustomerTrafficContext context);
}
```

Recommended direction: return structured contributions or write into an accumulator, rather than returning a final number from each handler.

Useful contribution fields:

- `FlatDelta`: additive change, for example `+2 visitors`.
- `PercentDelta`: additive percent, for example `-0.05` for rainy weather.
- `Multiplier`: multiplicative modifier, for example `1.10`.
- `MinClamp` / `MaxClamp`: optional contributor-specific bounds.
- `Reason`: debug string for logs/tests.

The resolver can expose debug output later:

```text
day baseline 10
location: promenade +20%
weather: rain -5%
decor: signboard +1
final: 13
```

## Pattern Review

### Chain of Responsibility

Classic Chain of Responsibility is not the best fit because traffic calculation needs most handlers to participate. Decor, location, weather, and event modifiers should all apply together.

The useful part of Chain of Responsibility is the ordered pipeline, but not the early-exit behavior.

Use:

- ordered list of contributors;
- all applicable contributors run;
- no contributor owns the whole final count unless it explicitly emits a special override type.

Avoid:

- "first handler that knows the answer wins";
- hidden order-dependent mutation of the final count.

### IteratorDecorator

Decorator fits customer-list composition better than numeric traffic calculation.

Existing example:

- `QuestSchedulingCustomerSpawner` wraps a base `ICustomerSpawner`;
- it prepends quest customers before regular customers;
- the base spawner stays responsible for normal day composition.

Recommended use:

- keep decorator-style spawners for special/story/quest customers;
- keep traffic count calculation inside a resolver used by the regular base spawner.

Do not model every traffic modifier as nested decorators around `ICustomerSpawner`. That would make it hard to inspect why a day had 17 customers.

## Ownership Boundaries

Recommended ownership:

- `Book.Sell.API`
  - public contracts that other features may implement, if needed.
  - Example: `ICustomerTrafficContributor`.

- `Book.Sell`
  - resolver implementation;
  - default day-table contributor;
  - location contributor if it only reads `LocationConfig`;
  - base spawner integration.

- `Game.Decor`
  - decor-specific contributor implementation, if decor needs its own config fields.
  - This matches the existing pattern where decor implements `IDecorModifierProvider` from `Book.Sell.API`.

- Future `Weather`, `Calendar`, `Events`, or global effect features
  - implement contributors through the API contract;
  - register through DI into `IReadOnlyList<ICustomerTrafficContributor>`.

This keeps `Book.Sell` from depending directly on future feature implementations.

## Data Model Notes

Decor currently has `GenreMultipliers` for passive sale chance. Traffic modifiers should not be forced into `GenreMultipliers`.

Future decor traffic data could be either:

1. Directly added to `DecorConfig`, for example:

```csharp
public float CustomerTrafficPercentDelta { get; set; }
public int CustomerTrafficFlatDelta { get; set; }
```

2. Moved into a more generic effect block later:

```json
{
  "effects": [
    { "type": "customerTraffic.percentDelta", "value": 0.10 },
    { "type": "customerTraffic.flatDelta", "value": 1 }
  ]
}
```

Recommended first step: explicit fields, because they are easy to validate and easy for tests. Move to generic effects only when multiple systems really need shared effect authoring.

## Integration With Spawners

The default production spawner should change from owning the baseline count to consuming the resolver:

```text
count = trafficResolver.ResolveCustomerCount(setup, tuning)
```

Then it builds `count` regular customers.

The resolver should not build customers.

Runtime scenario spawners can keep hardcoded counts for manual testing. They are intentionally scenario tools and do not need to use the traffic resolver unless the scenario is meant to test real production traffic.

## Integration With Quest And Story Customers

Special customers are a separate layer.

Future rule:

```text
regularCount = trafficResolver.ResolveCustomerCount(...)
regularCustomers = baseSpawner.BuildRegularCustomers(regularCount)
specialCustomers = specialSchedulers.BuildSpecialCustomers(...)
finalCustomers = merge/sort/prepend according to scheduling policy
```

For now, `QuestSchedulingCustomerSpawner` can continue to prepend quest characters.

Important distinction:

- If day 1 says exactly 3 customers, that should mean exactly 3 regular customers unless a future FTUE/story policy explicitly defines whether story customers are included in the number.
- Before adding special customers to scripted days, decide whether special customers count inside or outside the regular traffic count.

Recommended default:

- regular traffic count excludes special/story/quest customers;
- scripted FTUE days can use a separate composition spec when exact total headcount matters.

## Edge Cases

### Zero customers

`SalesDayController` already supports zero customers: it logs a warning and the next tick can move the day toward close. The traffic resolver may return zero if config allows it.

Default recommendation:

- production `minCustomerCount = 1` after FTUE, unless zero-customer days are intentional;
- tests may use zero.

### Sold-out shelf

`SalesDayController` already stops spawning new customers when the shelf is sold out, while allowing in-flight customers to finish. Traffic count is only the planned visitor count, not a guarantee that every planned customer appears if the shelf sells out early.

### Randomness

The count resolver should avoid consuming `ISalesRandom` in the first implementation. Count should be deterministic from setup/config/modifiers.

If random traffic variance is added later, it must be explicit and tested because spawners currently care about seeded random stream order.

### Modifiers That Reduce Below Zero

Always clamp final count. Negative customer counts are invalid.

### Modifier Ordering

Avoid order-dependent behavior where possible.

Recommended combination:

- sum all flat deltas;
- combine all percent deltas into one multiplier, or multiply explicit multipliers in a stable order;
- apply clamps at the end.

If a future contributor needs an absolute override, make that a named contribution type and define priority rules.

## Suggested First Iteration

1. Add traffic config with:
   - default count;
   - min/max;
   - per-day overrides;
   - `applyModifiers`.

2. Add `ICustomerTrafficResolver`.

3. Add simple resolver with no external contributors yet.

4. Register resolver in `BookSellVContainerBindings`.

5. Update the production base spawner to use resolver for regular customer count.

6. Add tests:
   - day 1 override returns exactly 3;
   - day 1 ignores a fake modifier when `applyModifiers = false`;
   - unknown day uses default count;
   - day override with `applyModifiers = true` applies modifiers;
   - final value clamps to min/max.

7. Add first optional contributor:
   - location or decor, whichever has stable config data first.

## Open Questions

1. Should `minCustomerCount` be 0 or 1 in production config?

2. Should day overrides live in a new `sales_traffic.json`, or in a broader future `day_balance.json`?

3. Should special/story customers count toward the displayed "visitors today" number in UI?

4. Should traffic modifiers be visible to the player as forecast text during Preparation?

5. Should future random variance be allowed, or should traffic always be deterministic from day setup?

## Recommendation

Implement this as a traffic resolver plus contributor pipeline.

Use day override with `applyModifiers = false` for day 1:

```text
day 1 -> exactly 3 regular customers
```

Keep special-character scheduling in spawner decorators or a future composition scheduler. Do not mix "how many regular visitors" with "which story characters arrive" in the same service.
