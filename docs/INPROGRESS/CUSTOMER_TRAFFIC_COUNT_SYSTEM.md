# Customer Traffic Count System

> Status: implemented baseline / follow-up backlog.
> Date: 2026-07-10.
> Last updated: 2026-07-14.
> Scope: calculates how many **regular** customers should visit during a sales day. Quest/story customers are
> scheduled by composition decorators on top of the regular count.

## Summary

Customer traffic is no longer owned by individual scenario spawners. The implemented flow is:

```text
SalesDayController.StartDayAsync
  -> ICustomerSpawner.BuildCustomers(setup, tuning, random)
      -> QuestSchedulingCustomerSpawner
          -> RegularCustomerSpawner
              -> ICustomerTrafficResolver.Resolve(setup, tuning)
              -> build N regular customers
          -> prepend quest dialogue customers
```

The resolver owns **how many regular customers** should be built. The spawner owns **what those customers do**.

Implemented files:

- Runtime contract: `ICustomerTrafficResolver`, `CustomerTrafficResult`
- API for contributors:
  - `Book.Sell.API.ICustomerTrafficContributor`
  - `CustomerTrafficContext`
  - `CustomerTrafficAccumulator`
  - `CustomerTrafficContribution`
- Resolver:
  - `Assets/Game/Features/BookSell/Services/Traffic/CustomerTrafficResolver.cs`
- Settings:
  - `Assets/Game/Features/BookSell/Services/Traffic/SalesTrafficConfig.cs`
  - `Assets/Game/Features/BookSell/Domain/SalesTrafficSettings.cs`
- Contributors:
  - `LocationTrafficContributor`
  - `Game.Decor.Services.DecorTrafficContributor`
- Production base spawner:
  - `RegularCustomerSpawner`
- Quest/story composition decorator:
  - `QuestSchedulingCustomerSpawner`
- Boot validation:
  - `CustomerTrafficConfigValidator`

## Current Data Model

### Global Traffic Settings

`SalesTrafficConfig` is a designer-editable `ScriptableObject` assigned through the location installer. It builds
pure-domain `SalesTrafficSettings`.

It stores only global knobs:

- `DefaultCustomerCount`
- `MinCustomerCount`
- `MaxCustomerCount`
- `TrafficRounding`

It does **not** store per-day overrides.

### Per-Day Baseline

Per-day traffic lives in `Assets/Configs/days.json` via `DayConfig`:

```json
{
  "id": "day_001",
  "dayIndex": 1,
  "customerCount": 3,
  "applyModifiers": false
}
```

Fields:

- `customerCount`: regular customer baseline for this day. `null` means use
  `SalesTrafficSettings.DefaultCustomerCount`.
- `applyModifiers`: `null`/missing means `true`. `false` means hard override.

Accepted rule:

- `applyModifiers = false` means exact regular count.
- Exact hard override skips traffic modifiers, min/max clamp, and active-request floor.
- If exact count is below active request count, the system warns; it does not silently raise the day.

### Location And Decor Modifiers

Implemented modifier fields:

- `LocationConfig.CustomerTrafficPercentDelta`
- `DecorConfig.CustomerTrafficPercentDelta`

Both are additive percent deltas:

```text
+0.20 = +20% regular customers
-0.05 = -5% regular customers
0     = neutral
```

`DecorTrafficContributor` sums all placed decor percent deltas into one contribution.

## Calculation Rules

Resolver flow:

1. Find `DayConfig` by `SalesSessionSetup.Day`.
2. Resolve baseline:
   - `DayConfig.CustomerCount` if present;
   - otherwise `SalesTrafficSettings.DefaultCustomerCount`.
3. Resolve modifier mode:
   - `DayConfig.ApplyModifiers == false` => hard override.
   - missing/null/true => modifiers enabled.
4. On hard override:
   - return exact baseline;
   - no contributors;
   - no min/max clamp.
5. On normal day:
   - build `CustomerTrafficContext(day, locationId, decorIds)`;
   - run all configured contributors;
   - sum percent deltas;
   - round;
   - clamp.

Current implemented formula is percent-only:

```text
percentDelta = sum(contribution.PercentDelta)
raw          = baseline * (1 + percentDelta)
rounded      = round(raw, SalesTrafficSettings.Rounding)
final        = clamp(rounded, minCustomerCount, maxCustomerCount)
```

The earlier proposed `FlatDelta`, `Multiplier`, contributor-specific clamps, and absolute overrides are not part
of the current implementation. Add them only when content needs them.

## Spawner Integration

`RegularCustomerSpawner` is the production base spawner.

For non-hard days:

```text
count = max(trafficResult.FinalCount, activeRequestCount)
```

This preserves the active-request floor: every enabled valid condition request should get one regular customer.
The first `N` regular customers receive `Passive -> Active -> Passive` plans, where `N = activeRequestCount`.
Remaining regular customers use passive-attempt plans.

For hard-override days:

```text
count = trafficResult.FinalCount
```

The active-request floor is skipped. If active requests exceed `count`, `RegularCustomerSpawner` logs a warning.

This is intentional: a scripted day such as day 1 can remain exactly `3` regular customers.

## Quest And Story Customers

Special customers are a composition layer, not part of regular traffic.

Current implementation:

- `RegularCustomerSpawner` builds regular customers.
- `QuestSchedulingCustomerSpawner` wraps it and prepends one quest dialogue customer per active quest with an
  undelivered `DialogueId`.

Therefore:

```text
regularCount = traffic resolver result (+ active request floor on non-hard days)
questCount   = quest scheduling decorator result
totalShown   = regularCount + questCount
```

Open product question: if UI shows "visitors today", should it show regular-only or total visitors including
quest/story customers? The resolver result is regular-only.

## Contributor Ownership

Current ownership:

- `Book.Sell.API`
  - contributor contract and API-safe context/result primitives;
  - lets other features implement traffic contributors without depending on `Book.Sell` implementation assembly.
- `Book.Sell`
  - resolver;
  - `LocationTrafficContributor`;
  - production base spawner integration.
- `Game.Decor`
  - `DecorTrafficContributor`.

DI note:

`BookSellVContainerBindings` composes contributor list explicitly:

```text
LocationTrafficContributor
DecorTrafficContributor
```

This is because VContainer does not automatically aggregate a single contributor list across parent and child
scopes in the shape needed here. Future contributors can use the same explicit composition until a shared
registration helper is worth introducing.

## Logging Contract

Stable log tag:

```text
[Sales.Traffic]
```

Resolver logs:

- one `resolved` summary line per resolve;
- one `contribution` line per non-neutral contribution.

Spawner logs:

- `requestCap` warning when the day asks for more active requests than it can serve (capped at the
  customer count / pool size).

> **Removed:** the `spawnerFloor` / `requestFloor` lines. The spawner used to floor the customer count at
> "number of enabled requests" (`Math.Max(count, requestCount)`), so a 49-entry `requests.json` forced a
> 49-customer day. Active-request demand now comes from `IActiveRequestCountResolver` (day config), and the
> catalog is only a pool to draw from — it can never raise traffic.

Current parser-friendly examples:

```text
[Sales.Traffic] resolved day=2 location=loc baselineSource=dayOverride baseline=10 applyModifiers=true hardOverride=false percentDelta=0.15 multiplier=1.00 flatDelta=0 raw=11.5 rounded=12 min=0 max=100 final=12 contributors=2
[Sales.Traffic] contribution day=2 source=location id=loc percentDelta=0.2 multiplier=1.00 flatDelta=0 reason=location.loc
[Sales.Requests] resolved day=2 location=loc baselineSource=dayOverride baseline=2 applyModifiers=true hardOverride=false percentDelta=0 raw=2 rounded=2 min=0 max=50 final=2 contributors=0
[Sales.Traffic] requestCap day=1 demand=5 customers=2 pool=5 final=2 — the day asks for more active requests than it can serve.
```

The log still includes `multiplier=1.00 flatDelta=0` for compatibility with the broader planned shape, even
though the current implementation is percent-only.

## Validation

`CustomerTrafficConfigValidator` runs at boot and warns when:

- a hard-override day has `customerCount`;
- `applyModifiers = false`;
- active request count is greater than that exact regular count.

This catches content where a scripted exact day would not have enough regular customers to serve all active
requests. The validator warns; it does not rewrite the count.

## Test Coverage

Covered by EditMode tests:

- hard override returns exact count;
- hard override ignores contributors and clamp;
- modifier day sums percent deltas, not compounded;
- missing day uses default count;
- missing `ApplyModifiers` defaults to modifiers on;
- final count clamps on non-hard days;
- empty contributor list returns baseline;
- non-hard request floor raises count;
- hard override skips request floor;
- first `N` regular customers receive active requests;
- validator warns on hard-override below active request count;
- location contributor reads `LocationConfig.CustomerTrafficPercentDelta`.

Decor traffic contributor has implementation coverage through the feature shape, but should receive focused
tests if traffic balancing starts depending heavily on decor values.

## Edge Cases

### Zero Customers

The resolver can return zero if config allows it.

`SalesDayController` can handle no customers: the day can move toward close. Production config should decide
whether zero-customer days are ever intentional.

### Sold-Out Shelf

Traffic count is planned visitors, not guaranteed visitors. If the shelf sells out, `SalesDayController` stops
spawning new customers while allowing in-flight customers to finish.

### Randomness

The resolver does not consume `ISalesRandom`. Keep it deterministic unless random variance becomes an explicit
balance feature with tests, because seeded random stream order matters elsewhere in the sales simulation.

### Negative Modifiers

Negative percent deltas are allowed, but final count is clamped on non-hard days. If total percent drops below
`-1.0`, raw count becomes negative and then clamps to the configured minimum.

## Backlog / Open Questions

1. **Visitor UI semantics.** Should a "visitors today" UI show regular-only count or total count after quest/story
   customers are prepended?

2. **Preparation forecast.** Should location/decor traffic modifiers be visible to the player before starting
   the day? `CustomerTrafficResult.Breakdown` is ready for this, but no UI currently consumes it.

3. **Random variance.** Should traffic remain deterministic forever, or should later balance add an explicit
   random variance contributor?

4. **More contribution shapes.** Add flat deltas, explicit multipliers, contributor clamps, or absolute overrides
   only when real content needs them.

5. **More contributors.** Weather, calendar, event, global world effects, or quest effects can implement
   `ICustomerTrafficContributor` later.

6. **Validation hardening.** Add editor/config validation for duplicate `DayConfig.DayIndex`, invalid min/max
   settings, suspicious percent values, and missing referenced location/decor configs.

7. **Docs cleanup.** Older docs may still describe `RequestConfig` or test spawners as current. Treat this file
   and the code listed above as the current source of truth for traffic count behavior.
