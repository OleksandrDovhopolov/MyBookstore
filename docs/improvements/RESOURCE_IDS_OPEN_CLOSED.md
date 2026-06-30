# Resource ids — Open/Closed improvement

**Status:** Deferred. Implement **after** the second resource (likely `gems`) is
introduced, or earlier if metadata (icons, display names, hard-currency flag)
becomes load-bearing for UI.
**Selected approach:** **Variant D — hybrid registry + data-driven config.**
**Tracking:** open a Notion ticket when scheduling.

---

## Context

`Game.Resources.API` ships with:

```csharp
public static class ResourceIds
{
    public const string Gold = "gold";
    public const string Gems = "gems";
}
```

Adding a new resource today requires **editing this file** — `Game.Resources.API`
is "open for modification" every time a feature introduces a currency / token /
event ticket. That violates Open/Closed.

Right now there is exactly one resource in use (`gold`); `Gems` is a forward-compat
placeholder that nothing reads or writes yet. The class is small and stable, so
the OCP smell is dormant. It will get loud as soon as:

- A second currency lands (UI needs icon + display name → metadata pressure).
- An event/season system ships its own tokens (designers want to add them via
  data, not C#).
- A feature wants to enumerate "all currencies" for a HUD / wallet screen.

---

## Variants considered

### A. Feature-owned constants

Each feature owns its own `*ResourceIds` class:

```csharp
// Game.Sales
public static class SalesResourceIds { public const string Gold = "gold"; }

// Game.Shop
public static class ShopResourceIds { public const string PremiumTicket = "premium_ticket"; }
```

`Game.Resources.API` loses `ResourceIds` entirely.

- ✅ OCP satisfied; new feature = new class.
- ⚠️ No central catalog; "which resources exist?" is a grep job.
- ⚠️ Risk of duplicate ids across features.
- ⚠️ No place to attach metadata (icon, hard-currency flag, max cap).

### B. `IResourceRegistry` (mirror of `IItemCategoryRegistry`)

Programmatic registry filled at startup:

```csharp
public sealed class ResourceDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool IsHardCurrency { get; }
}

public interface IResourceRegistry
{
    void Register(ResourceDefinition def);
    ResourceDefinition Get(string id);
    IReadOnlyList<ResourceDefinition> GetAll();
}
```

Each feature calls `Register(new ResourceDefinition(...))` in its `VContainerBindings`.

- ✅ OCP satisfied via DI.
- ✅ Central catalog (`GetAll()`) — wallet HUD can enumerate.
- ✅ Carries metadata.
- ✅ Mirrors the existing Inventory pattern (`IItemCategoryRegistry`).
- ⚠️ Metadata lives in C# code — designers cannot rebalance without a build.

### C. `ResourceConfig` via the existing Configs system

```csharp
[ConfigFile("resources")]
public sealed class ResourceConfig : IConfig
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Icon { get; set; }       // addressable
    public bool IsHardCurrency { get; set; }
    public int MaxCap { get; set; }        // 0 = unlimited
}
```

`Assets/Configs/resources.json` lists every resource. New resource → new JSON
entry. Optional Firebase RemoteConfig overrides (already supported by `Configs`)
let prod tune metadata without a client update.

- ✅ Designers add/balance resources without code changes.
- ✅ Rich metadata.
- ✅ Mirrors the existing `BookConfig` / `LocationConfig` pattern.
- ⚠️ Code still references concrete ids (e.g. `FtueBootstrapper` writes "gold").
  Those references stay; only the **catalog** is data-driven, not the call sites.

### D. Hybrid: registry + config (recommended)

`ResourceConfig` is the source of truth for **metadata**. `IResourceRegistry` is
the runtime catalog — it consumes `IConfigsService.GetAll<ResourceConfig>()` on
startup and exposes lookup + enumeration. Features that need to refer to a
specific id keep a small local constant (Variant A style) so the id is
self-documenting and refactor-safe.

```
resources.json  →  IConfigsService  →  ResourceConfig[]
                                              ↓ consumed at startup
                                       IResourceRegistry  ←  one source of truth at runtime
                                              ↑ used by HUD / Wallet / Shop UI

Game.Ftue.Services.FtueResourceIds  →  local const "gold"  →  IResourcesService.AddAsync(...)
```

- ✅ OCP across the board.
- ✅ Designer-editable metadata (JSON + RC override).
- ✅ Discoverable catalog at runtime.
- ✅ Type-safety where it matters (call sites use local consts that have to be
  changed in lockstep with the JSON entry — a deliberate friction).
- ⚠️ Largest investment of the four.

---

## Why D is the right destination

1. The codebase already has two analogous patterns: a **registry** (Inventory) and
   a **data-driven config** (`BookConfig`, `LocationConfig`, `EconomyConfig`).
   D reuses both — no new mental model.
2. UI for currencies inevitably needs icons + display strings. Metadata has to
   live somewhere data-driven; configs are the right place.
3. Call sites that mean a *specific* resource (FTUE writes `gold`, the daily quest
   pays `gold`, a shop sells `gems`) will always need a stable handle. Local
   feature constants keep refactors safe and surface unused references via the
   compiler when an id is renamed in JSON.
4. Server validation for hard-currency lands cleanly: `ResourceConfig.IsHardCurrency`
   reads → `HttpResourcesRepository` validates differently per flag. No special
   subsystem.

---

## Why we are deferring

- Today the project has **one** resource (`gold`). The single-line `public const
  string Gold = "gold"` does no harm.
- Building D before the second resource is **premature** — we would design the
  registry contract against one example, with no real pressure to make it
  general.
- The current `ResourceIds` class is so small that flipping it to D when the
  second resource lands costs ~30 minutes of plumbing. The change is not load-bearing.

A practical heuristic: implement D the same iteration the second currency
(or first hard-currency, or first season token) is introduced. By then, the
metadata pressure is real and the API surface can be designed against two
examples instead of one.

---

## Implementation outline for the future iteration

1. **Move metadata into config.**
   - `Assets/Game/Features/Configs/Models/ResourceConfig.cs` with
     `[ConfigFile("resources")]` and `IConfig`.
   - `Assets/Configs/resources.json` — initial entries for `gold` and `gems`.
2. **Add the registry.**
   - `Game.Resources.API/IResourceRegistry.cs` (interface).
   - `Game.Resources/Services/ResourceRegistry.cs` (implementation;
     `IStartable` reads from `IConfigsService.GetAll<ResourceConfig>()` once at
     startup).
   - DI registration in `ResourcesVContainerBindings`.
3. **Drop the central constants.**
   - Delete `Game.Resources.API/Constants/ResourceIds.cs`.
   - Move the `"gold"` constant into `Game.Ftue.Services.FtueResourceIds` (or
     wherever the *first* writer of gold lives — likely Ftue + Sales rewards).
   - Move `"gems"` into the feature that introduces it (Shop, IAP, etc.).
4. **Wire HUD / Wallet to the registry.**
   - `GameHudView` no longer references `ResourceIds.Gold` directly; it calls
     `_registry.Get("gold")` for display metadata and `_resources.GetAmount("gold")`
     for the value. Or, for the all-currency variant, enumerate
     `_registry.GetAll()` and render every resource with `_resources.GetAmount(def.Id)`.
5. **Tests update.**
   - `ResourcesServiceTests` keeps using literal strings — it tests the service,
     not the catalog.
   - Add `ResourceRegistryTests`: enumerates all configs and resolves them.

## Verification

- All existing EditMode tests still pass.
- HUD shows gold using the metadata pulled from `resources.json` (icon + display
  name, if those fields are wired into the view).
- Designer changes `resources.json` (e.g. renames `gold` → `coins` *with* a
  matching code-side constant rename) and the build still runs.
- Adding a third resource (`event_token`) is a JSON entry + one constant in the
  feature that uses it — no edits to `Game.Resources` or `Game.Resources.API`.

---

## Notes

- **Do not delete `ResourceIds.Gold` until D is implemented.** Leaving it
  alone is the cheapest path; touching it now would force a refactor before we
  are ready.
- This document supersedes any inline TODOs that may live around `ResourceIds.cs`
  — point them here instead of duplicating analysis.
- Related: `docs/TODO.md` → **INF-9** (`SaveHookBootstrapper` cleanup — same
  iteration candidate for the boot-side wiring). The two are independent; either
  can ship first.
