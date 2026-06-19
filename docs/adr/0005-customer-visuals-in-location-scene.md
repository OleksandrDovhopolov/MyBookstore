# ADR-0005: Customer visuals live in LocationScene world space

- **Status:** Proposed
- **Date:** 2026-06-18
- **Deciders:** project owner
- **Related:** [ADR-0003](0003-customer-simulation.md), [ADR-0004](0004-stock-model-hybrid-sale-chance.md), `docs/INPROGRESS/SCENE_ARCHITECTURE.md`, `docs/INPROGRESS/LOCATION_BUILDING.md`, `docs/INPROGRESS/WorldHud-Phase-0-Editor-Setup.md`, `Assets/Game/Features/BookSell/UI/SalesScreenView.cs`

## Resolved Questions

- Multiple customers may be visible at the same time.
- Approach points use lane offsets from the start, not one shared point.
- A customer who opens an active request stays near the stall for the whole active minigame.
- Entry side and exit side are fully random and independent.
- Off-screen entry/exit points are primarily location-authored anchors. Fallback camera-derived points are allowed only for prototypes.

## Context

Sales already runs as a real-time customer simulation:

- `SalesDayController` owns the domain tick, customer plans, passive/active purchase steps, reservations, lock, and day completion.
- `SalesScreenView` pumps `ISalesDayController.Tick(Time.deltaTime)`, renders the shelf/debug HUD, opens the active request panel, and forwards `RecommendBook` / `SkipCurrentRequest`.
- `CustomerVisualRegistry` already listens to `CustomerPhaseChanged` and spawns a `CustomerVisual` placeholder prefab, but the current placement is hardcoded as a horizontal row.
- `CustomerBubbleBinder` already maps customer phases to world-space thought bubbles via World HUD.

The next small visual improvement is:

1. Customer appears outside the visible area, initially as a simple square sprite.
2. Customer moves to a point near the bookshop/stall.
3. Existing active/passive purchase pipeline continues to resolve the sale.
4. Customer leaves the location after purchase flow completes.
5. Customers may enter from left or right and may leave to either side.

The architectural question is whether this visual flow belongs to UI / `SalesScreenView` or to screen/world presentation for the sales location.

The reference game, Tiny Bookshop, frames the bookshop as a scenic location where the player stocks books/items, sets up shop in different places, and watches customers come in while recommendations happen. The Steam page describes scenic locations, customer flow, decor/items affecting customers, and recommending the right book to the right person: https://store.steampowered.com/app/2133760/Tiny_Bookshop/

## Decision

Customer movement and customer sprites are implemented in **LocationScene world space**, not inside `SalesScreenView` UI.

`SalesScreenView` remains a gameplay HUD/debug screen for the sales phase:

- starts and ticks the sales day;
- renders shelf availability and feedback log;
- opens the active request panel;
- forwards player input to `ISalesDayController`;
- hands off to Results on day completion.

It must not own customer coordinates, spawn sides, path movement, sorting layers, or sprite lifecycle beyond subscribing to controller events if it needs UI feedback.

The customer visual layer is a presentation adapter over the domain:

- Domain customer state remains pure C# and coordinate-free.
- `CustomerPhaseChanged` remains the primary signal for phase-driven visuals.
- `CustomerVisualRegistry` evolves from "spawn in a row" to "spawn/move/despawn in location space".
- `CustomerVisual` owns only visual state: transform, square sprite, bubble anchor, simple motion/idle.
- Location-specific anchors come from the scene or a location context, not from the domain.

## Location Contract

For this phase, each sales location provides these visual anchors:

- `EntryLeft`: off-screen or just outside camera view on the left.
- `EntryRight`: off-screen or just outside camera view on the right.
- `ShopApproach`: point near the stall where a customer waits/browses.
- `LaneOffsets`: small local offsets around `ShopApproach` so multiple visible customers do not stack on one pixel.
- `ExitLeft`: off-screen left.
- `ExitRight`: off-screen right.

Long term these anchors belong to `LocationController` / `ILocationContext` from `LOCATION_BUILDING.md`. For the immediate phase, serialized fields on the visual registry/config are acceptable if that keeps the slice small.

Entry/exit anchors should be authored per location instead of always calculated from camera bounds. This keeps the system flexible:

- Promenade-like locations can let customers walk far along the street before disappearing.
- Tight locations can hide customers just outside the frame or behind foreground masking.
- Future wide/pannable locations can place exits beyond the current camera view without changing sales code.

If a prototype scene has no anchors yet, the registry may derive fallback points from the current camera bounds with a fixed margin, but that is a temporary editor convenience, not the production contract.

## Visual State Mapping

| Domain phase | Visual behavior |
|---|---|
| `Spawned` / first `CustomerPhaseChanged` | Instantiate square sprite at `EntryLeft` or `EntryRight`, outside visible area. |
| `Approaching` | Move toward `ShopApproach` or an offset lane near it. |
| `Browsing` | Idle at the approach point. Thought bubble can show thinking state. |
| `AwaitingHelp` / `InMinigame` | Stay near the stall for the entire active minigame. Active request panel remains driven by `SalesScreenView`. |
| `Leaving` | Choose `ExitLeft` or `ExitRight` randomly and independently from entry side, then move off-screen. |
| `Done` | Despawn after the leaving motion or after a short fallback delay. |

The movement is cosmetic. It does not advance or block the domain simulation. If visual movement is still in progress when the domain phase changes, the visual may interrupt the current move and start the next one.

Multiple customers may be visible at once. Visual lane assignment is presentation-only and does not imply shop capacity or queueing rules in the domain.

## Active And Passive Purchase Flow

Active and passive purchases stay in the existing pipeline:

- Passive sales are still resolved by `PassivePurchaseStep` and `IPassiveSaleSelector`.
- Active recommendations are still resolved by `ActiveRequestStep`, `IInteractionLock`, `RecommendBook`, and `SkipCurrentRequest`.
- The square customer does not implement purchase logic.

For Phase 0/1 visuals, it is enough to show arrival, wait/browse, active-request bubble state, and leaving.

Richer feedback needs event correlation that is currently missing:

- `PassiveSaleEvent` does not expose `CustomerId`.
- `ActiveRequestStarted` exposes only `RequestConfig`, not the `Customer`.
- `RecommendationResolved` exposes only `RecommendationResult`, not the `Customer`.

If bubbles need to show "this exact customer bought/rejected this exact book", add customer-aware events or event args to `ISalesDayController` instead of guessing in UI.

"Customer-aware events" means that presentation events carry the customer that caused the event, for example:

```csharp
event Action<Customer, RequestConfig> ActiveRequestStarted;
event Action<Customer, RecommendationResult> RecommendationResolved;
event Action<Customer, PassiveSaleEvent> PassiveSaleHappened;
```

The current events are enough for global UI like the request panel and feedback log. They are not enough for per-customer bubbles, because the view cannot reliably know which visual should show "bought", "rejected", or "happy" feedback after a sale resolves. For this movement slice, defer changing the event contract unless richer bubbles/comments are included in the same task.

## Consequences

### Positive

- Matches the planned scene architecture: Sales happens in a location, not in a pure UI screen.
- Keeps `SalesScreenView` focused and removable later if the HUD is redesigned.
- Reuses current domain events and World HUD without rewriting sales logic.
- Lets future art, sorting layers, decor slots, and location-specific paths grow naturally from `LOCATION_BUILDING.md`.
- Preserves testability: no Unity coordinates leak into `Book.Sell.Domain`.

### Negative

- Requires a small scene contract before final location prefabs exist.
- Current `CustomerVisualRegistry` needs to stop using hardcoded row positions.
- Exact sale/bubble feedback is limited until controller events carry customer correlation.
- Visual motion can temporarily diverge from domain timing unless we explicitly keep it cosmetic and interruptible.

## Rejected Alternatives

### Put customers inside `SalesScreenView`

Rejected. It would make character movement a UI concern, duplicate the future location scene, and make later migration to world-space customers expensive. `SalesScreenView` should remain the sales HUD/minigame surface.

### Add coordinates to domain customer steps

Rejected. ADR-0003 deliberately keeps the customer brain coordinate-free and testable. "Approach" in the domain means elapsed step time and phase, not a `Vector3`.

### Wait for final location art before adding movement

Rejected for this phase. A square sprite with scene anchors is enough to validate the customer flow and smoke-test the architecture before final art.

## Implementation Notes

Suggested small slice:

1. Add a `CustomerRouteAnchors` / config DTO with entry, approach, and exit transforms.
2. Extend `CustomerVisualRegistryConfig` to accept those anchors.
3. Replace `_spawnedCount * SpawnSpacingX` with random side selection and anchor-based spawn position.
4. Add simple movement to `CustomerVisual`, for example `MoveToAsync(Vector3 target, float duration, CancellationToken ct)` or an Update-driven visual state.
5. On `Approaching`, move to approach anchor.
6. On `Leaving`, pick left/right exit and move off-screen.
7. On `Done`, despawn after movement finishes or after a short fallback timeout.
8. Keep `SalesScreenView` unchanged except for optional debug labels/log lines.

Minimum verification:

- A customer appears outside the camera view.
- The customer moves toward the stall.
- Multiple customers can occupy distinct visual lanes near the stall.
- Passive sale log / active request panel still works.
- After `Leaving` / `Done`, the customer exits and is destroyed.
- Entry side and exit side are independently randomized.

## Open Questions

1. Exact lane policy: round-robin, random free lane, or deterministic by customer index?
2. Do we add customer-aware controller events now, or defer until richer per-customer bubbles/comments are implemented?

## When To Revisit

Revisit this ADR if:

- customer movement starts affecting gameplay timing;
- locations gain complex paths, obstacles, or crowding;
- `SalesScreenView` is replaced by a diegetic HUD;
- the customer visual layer needs exact per-customer passive/active result feedback.
