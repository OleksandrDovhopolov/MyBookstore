# Customer Traffic Count System

> Status: proposed / not implemented.
> Date: 2026-07-10.
> Scope: calculates how many regular customers should visit during a sales day. Special/story customers are noted as a future composition layer, not part of this iteration.

## Context

Current customer simulation builds the whole customer list before the sales day starts:

- `SalesDayController.StartDayAsync` calls `ICustomerSpawner.BuildCustomers(setup, tuning, random)`.
- `SalesSessionSetup` already carries the main inputs needed for traffic decisions: `Day`, `LocationId`, `ShelfBookIds`, and `DecorIds`.
- `DefaultCustomerSpawner` illustrates the current count logic: `count = max(requestCount, tuning.BaseCustomers)`. This floor over the number of active `RequestConfig`s matters and must be preserved (see Integration With Spawners).
- **Every current `ICustomerSpawner` implementation is a test / smoke tool.** `DefaultCustomerSpawner`, `TenCustomers*`, `Fifteen*`, `ActiveRequestsOnly*`, `OneToThreePassive*` all own their counts as constants for manual scenario testing. There is **no dedicated production base spawner yet**.
- The only spawner wired into production DI is `QuestSchedulingCustomerSpawner`, a decorator currently wrapping the `TenCustomersThreeActiveAfterPassiveSpawner` test spawner (`BookSellVContainerBindings`). This decorator is the piece carried forward to the next iteration; the base it wraps is expected to be replaced by a real production spawner.
- `CustomerPlanBuilder` centralizes the mandatory plan skeleton, while spawners remain responsible for day composition.
- `QuestSchedulingCustomerSpawner` prepending quest characters is useful precedent for future special-character scheduling.

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

Add a separate customer traffic resolver. It returns a **structured result**, not a bare `int`, so the same call feeds the spawner, debug logs, and a future Preparation forecast UI (see Open Questions #3, #4):

```csharp
public interface ICustomerTrafficResolver
{
    CustomerTrafficResult Resolve(SalesSessionSetup setup, SalesTuning tuning);
}

public sealed class CustomerTrafficResult
{
    public int FinalCount { get; }          // clamped count the spawner uses
    public int Baseline { get; }            // day baseline before modifiers
    public IReadOnlyList<CustomerTrafficContribution> Breakdown { get; } // for logs / forecast UI
}
```

The active production spawner asks this resolver for the result, reads `FinalCount`, then builds that many regular customer plans.

The resolver owns "how many customers". The spawner owns "what kind of regular customer plans are built".

## Baseline Config

Introduce a dedicated traffic config, separate from `SalesTuning`.

`SalesTuning` is currently timing and pacing data: approach duration, browse duration, spawn interval, max concurrent customers, and similar knobs. Day-by-day traffic is content/balance data and should not live there long-term. The existing `BaseCustomers` field on `SalesTuning` is expected to be removed once the resolver owns the baseline — traffic count should have a single source of truth.

Follow the existing config pattern in this project: a designer-editable `ScriptableObject` that builds a plain domain object, exactly like `SalesTuningConfig.BuildTuning()` is registered via `RegisterInstance`. So introduce `SalesTrafficConfig : ScriptableObject` with a `BuildTrafficConfig()` method — **not** a raw JSON file. The shape below is the built domain object / authoring layout, shown as JSON only for readability:

```jsonc
// SalesTrafficConfig (ScriptableObject) — conceptual shape
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

`dayOverrides` is an authoring list; the resolver should index it into a `Dictionary<int, DayOverride>` once at build time so day lookup is O(1) rather than a linear scan.

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
- A hard override (`applyModifiers = false`) is truly hard: it bypasses **both** modifiers **and** the final min/max clamp, so day 1 is exactly 3 even if `minCustomerCount` is higher. Value validity (`customerCount >= 0`) is checked when the config is loaded, not silently clamped at resolve time.
- Caveat: the request-count floor (see Integration With Spawners) can still raise a hard-override day above its stated number. Config validation should warn if a hard override is below the number of active `RequestConfig`s for that day so designers notice the conflict rather than getting a silently larger day.

## Calculation Flow

1. Resolve the day baseline.
   - If the day has an override, use that count.
   - Otherwise use `defaultCustomerCount`.

2. Check whether modifiers are enabled.
   - If the matching day override has `applyModifiers = false`, return the override directly — no modifiers, no min/max clamp (hard override). Validity was enforced at config load.
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

Conceptual formula. Contributions carry both additive percents (`PercentDelta`) and explicit multipliers (`Multiplier`); the two combine explicitly so behavior does not depend on which field a contributor happened to pick:

```text
flatDelta        = Σ FlatDelta
percentMultiplier = (1 + Σ PercentDelta) * Π Multiplier
raw              = (baseline + flatDelta) * percentMultiplier
final            = clamp(round(raw), minCustomerCount, maxCustomerCount)
```

This multiplicative combination mirrors the existing decor precedent, where `ConfigBasedDecorModifierProvider` multiplies per-decor genre multipliers and then clamps into a soft cap. Percent deltas are summed (two "-5%" sources give -10%, not compounded), while explicit multipliers multiply.

Example:

```text
baseline = 10
location = +20%  (PercentDelta +0.20)
rain     = -5%   (PercentDelta -0.05)
decor    = +1 flat (FlatDelta +1)

percentMultiplier = 1 + (0.20 - 0.05) = 1.15
raw = (10 + 1) * 1.15 = 12.65
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

There is no production base spawner today — every `ICustomerSpawner` is a test tool, and only `QuestSchedulingCustomerSpawner` (the decorator) is carried into the next iteration. So integration means **creating a real production base spawner** (or promoting one existing test spawner to that role) that consumes the resolver, and keeping the quest decorator wrapping it:

```text
result   = trafficResolver.Resolve(setup, tuning)
count    = max(result.FinalCount, activeRequestCount)   // request-count floor, see below
// build `count` regular customer plans
```

The resolver should not build customers.

**Request-count floor.** The current logic is `count = max(requestCount, tuning.BaseCustomers)`. `BaseCustomers` goes away, but the `max(…, requestCount)` floor stays: every active `RequestConfig` scheduled for the day must get a customer, so the day can never have fewer visitors than there are scripted active requests. Apply this floor in the spawner, on top of `FinalCount`, because the spawner is what reads `RequestConfig`s (`_configs.GetAll<RequestConfig>()`) — the resolver stays free of that dependency and stays deterministic from config/modifiers. The floor's interaction with hard-override days is called out under Baseline Config.

Runtime scenario spawners can keep hardcoded counts for manual testing. They are intentionally scenario tools and do not need to use the traffic resolver unless the scenario is meant to test real production traffic.

## Integration With Quest And Story Customers

Special customers are a separate layer.

Future rule:

```text
regularCount = trafficResolver.Resolve(...).FinalCount
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

## Logging And Log Analysis Contract

The traffic system must be easy to reconstruct from a log file. A developer should be able to answer:

```text
Why did sales day N get X regular customers?
```

Use one stable log tag for all customer-traffic calculation messages:

```text
[Sales.Traffic]
```

Until the project has a production logging wrapper for gameplay services, `Debug.Log` / `Debug.LogWarning` is acceptable. When `docs/SERVICES/LOGGING_SYSTEM.md` is implemented for this project, the same events should move to a typed/channel logger without changing the event names or payload fields.

### Required Events

Log exactly one summary line per traffic resolution:

```text
[Sales.Traffic] resolved day=1 location=park baselineSource=dayOverride baseline=3 applyModifiers=false hardOverride=true raw=3 rounded=3 min=0 max=50 final=3 contributors=0
```

For modifier-enabled days, include contribution summary:

```text
[Sales.Traffic] resolved day=5 location=promenade baselineSource=default baseline=10 applyModifiers=true hardOverride=false percentDelta=0.15 multiplier=1.00 flatDelta=0 raw=11.50 rounded=12 min=1 max=50 final=12 contributors=2
```

Log one optional breakdown line per non-neutral contributor, only when it actually contributes:

```text
[Sales.Traffic] contribution day=5 source=location id=promenade percentDelta=0.20 multiplier=1.00 flatDelta=0 reason=location.promenade
[Sales.Traffic] contribution day=5 source=decor id=rain_sign percentDelta=-0.05 multiplier=1.00 flatDelta=0 reason=decor.rain_sign
```

Log warning lines for invalid or surprising config:

```text
[Sales.Traffic] warning day=1 hard override customerCount=-1 is invalid
[Sales.Traffic] warning day=1 hard override regularCount=3 conflicts with requestFloor=5
[Sales.Traffic] warning duplicate day override day=2 first=5 second=7 using=second
```

If the production spawner applies a request-count floor after the resolver, log that separately because it is not part of the resolver result:

```text
[Sales.Traffic] spawnerFloor day=4 resolvedRegular=3 requestCount=5 finalRegular=5 applied=true
```

If hard overrides are defined as "exactly N regular customers", the recommended rule is that the request-count floor does **not** mutate the value. In that case, log only a warning when the configured hard override conflicts with available active requests.

### Required Fields

The summary event must contain:

- `day`: sales day number from `SalesSessionSetup.Day`.
- `location`: `SalesSessionSetup.LocationId`, or empty/null marker.
- `baselineSource`: `dayOverride`, `default`, or future source id.
- `baseline`: baseline count before modifiers.
- `applyModifiers`: whether contributors were allowed to run.
- `hardOverride`: true when the baseline is final and bypasses modifiers/clamp.
- `percentDelta`: summed additive percentage delta.
- `multiplier`: product of explicit multipliers.
- `flatDelta`: summed flat count delta.
- `raw`: raw calculated value before rounding.
- `rounded`: value after rounding before clamp.
- `min` / `max`: active clamp values.
- `final`: final regular customer count returned by the resolver.
- `contributors`: number of non-neutral contributions included in the calculation.

Each contribution event must contain:

- `day`;
- `source`: stable contributor type id, for example `location`, `decor`, `weather`, `event`;
- `id`: source object id, for example location id or decor id;
- `percentDelta`;
- `multiplier`;
- `flatDelta`;
- `reason`: stable reason key suitable for tests and future forecast UI.

### Contributor Logging Rules

Contributors should not independently log normal "no effect" cases. They should return neutral/no contribution and let the resolver produce the one summary event.

Contributors may log warnings for bad local data, for example:

- decor id from setup has no `DecorConfig`;
- location id has no `LocationConfig`;
- percent value is NaN or outside a validated range.

Normal contribution breakdown should be emitted by the resolver from `CustomerTrafficResult.Breakdown`, not scattered across each contributor. This keeps the log order stable and makes log parsing easier.

### Log Levels

- `Information`: one resolve summary per sales day start.
- `Debug`: per-contributor breakdown, if logs become too noisy.
- `Warning`: invalid config, duplicate day rows, missing referenced config, hard override conflicts.
- `Error`: only for states where the resolver cannot produce a valid count and must fall back.

### Parser-Friendly Format

Prefer stable key-value fields over prose:

```text
key=value key=value key=value
```

Avoid localized text in machine-parsed parts of the message. Human-readable text can be appended after the key-value section if needed.

Recommended event names:

- `resolved`
- `contribution`
- `spawnerFloor`
- `warning`

This makes simple log filtering possible:

```text
[Sales.Traffic] resolved
[Sales.Traffic] contribution
[Sales.Traffic] spawnerFloor
```

### Example Full Trace

```text
[Sales.Traffic] contribution day=2 source=location id=park percentDelta=0.10 multiplier=1.00 flatDelta=0 reason=location.park
[Sales.Traffic] contribution day=2 source=decor id=poster_a percentDelta=0.05 multiplier=1.00 flatDelta=0 reason=decor.poster_a
[Sales.Traffic] resolved day=2 location=park baselineSource=dayOverride baseline=5 applyModifiers=true hardOverride=false percentDelta=0.15 multiplier=1.00 flatDelta=0 raw=5.75 rounded=6 min=1 max=50 final=6 contributors=2
```

Hard override example:

```text
[Sales.Traffic] resolved day=1 location=park baselineSource=dayOverride baseline=3 applyModifiers=false hardOverride=true raw=3 rounded=3 min=1 max=50 final=3 contributors=0
```

The hard override log intentionally shows clamp settings but does not apply them.

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

1. Add `SalesTrafficConfig : ScriptableObject` with `BuildTrafficConfig()` (mirrors `SalesTuningConfig`), carrying:
   - default count;
   - min/max;
   - per-day overrides;
   - `applyModifiers`.
   Enforce `customerCount >= 0` and warn on hard-override-below-request-count at load.

2. Add `ICustomerTrafficResolver` returning `CustomerTrafficResult` (FinalCount + Baseline + Breakdown).

3. Add simple resolver with no external contributors yet (must handle an empty contributor list).

4. Register the config (via `RegisterInstance` from the SO, like `SalesTuning`) and resolver in `BookSellVContainerBindings`.

5. Create a real production base spawner (or promote a test spawner) that reads `result.FinalCount`, applies the `max(…, requestCount)` floor, and builds that many regular customers. Keep `QuestSchedulingCustomerSpawner` wrapping it.

6. Add tests:
   - day 1 hard override returns exactly 3;
   - day 1 ignores a fake modifier when `applyModifiers = false`;
   - hard override bypasses the min/max clamp;
   - resolver emits a summary log containing baseline, modifier totals, raw, rounded, clamp, final, and contributor count;
   - resolver emits stable breakdown entries for non-neutral contributors;
   - unknown day uses default count;
   - day override with `applyModifiers = true` applies modifiers;
   - final value clamps to min/max (non-hard days);
   - request-count floor raises the count when there are more active requests than the resolved value;
   - resolver works with zero contributors.

7. Add first optional contributor:
   - location or decor, whichever has stable config data first.

## Open Questions

1. Should `minCustomerCount` be 0 or 1 in production config?

2. Resolved in this revision: config is a `SalesTrafficConfig` ScriptableObject (matching `SalesTuningConfig`), not a JSON file. Still open: whether it stays standalone or folds into a broader future `DayBalanceConfig`.

3. Should special/story customers count toward the displayed "visitors today" number in UI? (`CustomerTrafficResult.FinalCount` gives the regular count to build that display on.)

4. Should traffic modifiers be visible to the player as forecast text during Preparation? (`CustomerTrafficResult.Breakdown` already carries per-contributor reasons for this, if enabled.)

5. Should future random variance be allowed, or should traffic always be deterministic from day setup?

## Recommendation

Implement this as a traffic resolver plus contributor pipeline.

Use day override with `applyModifiers = false` for day 1:

```text
day 1 -> exactly 3 regular customers
```

Keep special-character scheduling in spawner decorators or a future composition scheduler. Do not mix "how many regular visitors" with "which story characters arrive" in the same service.
