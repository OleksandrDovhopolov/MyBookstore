# UI System — Future Phases

Tracks the deferred scope of the UI System work. The framework + pilot windows shipped in Phase 0+1 are documented at [../UI_SYSTEM.md](../UI_SYSTEM.md). Everything below is a follow-up — one consolidated Notion task references this file.

---

## Phase 2 — Migration and core game windows

### 2.1 ShopWindow

- New `WindowType.Page` window, default layer `Main`, `keepInCache: true`.
- Real shop content (book catalog, prices, purchase flow) gated on a `ShopService` + `ShopConfig` — those are separate features. UI side is the rendering shell + buy flow.
- Wire from `PreparationScreenView`'s currently-stub "Open Shop" button. When real shop is ready, replace the stub click with `ShowAsync<ShopWindow>(...)`.

### 2.2 NoInternetWindow

- `WindowType.Page`, `WindowArgs().AsSystem()` (System layer — must overlay everything).
- Bind to existing `ConnectionService` at `Assets/Game/Infrastructure/Http/Backend/ConnectionService.cs` — it already monitors `Application.internetReachability`. Add a service-level bridge that calls `IUIManager.ShowAsync<NoInternetWindow>().AsSystem()` on offline + a `ConnectionService.HandleNoInternet` callback for auto-hide on recovery.
- Validates the System layer in a real flow (Phase 0 smoke validated it standalone via SmokeSystemDialog).

### 2.3 HUD migration: GameHudView → HudRoot

Current state: `Assets/Game/Features/Resources/UI/GameHudView.cs` is a scene-placed MonoBehaviour in GameplayScene with `[Inject] IResourcesService, IProgressionService`, listening to balance changes.

Two options:

| Option | Effort | Risk |
|---|---|---|
| Convert to `WindowController<GameHudView>` with `WindowType.Widget` (HudRoot) and `ShowAsync<GameHud>()` on gameplay scene enter | Medium | Touches GameplayLifetimeScope wiring |
| Leave HUD scene-placed but move the prefab under `UICanvasRoot.HudRoot` at runtime via a tiny adapter | Low | Couples UISystem to feature wiring |

Recommendation: **Option 1**. Validates the Widget + HudRoot path under real flow.

### 2.4 Migration of existing scene-placed views

The following views in `Assets/Game/Features/` are currently scene-placed MonoBehaviour with VContainer injection, no `WindowController` abstraction:

| View | Feature | Notes |
|---|---|---|
| `MorningScreenView` | DayCycle/Morning | Day briefing — single-shot per day. Likely `Page, Main, keepInCache: false` |
| `PreparationScreenView` | Preparation | Book selection. `Page, Main, keepInCache: false` |
| `SalesScreenView` | BookSell | Real-time minigame. `Page, Main, keepInCache: false` |
| `ResultsScreenView` | DayCycle/Results | Day recap. `Page, Main, keepInCache: false` |
| `InventoryScreenView` | Inventory | Debug browser today. Production version → `Page, Main, keepInCache: true` |

Recommended order (smallest first to reduce blast radius): Morning → Preparation → Results → Sales → Inventory.

For each migration:

1. Rename the view to `<Feature>WindowView` (still extends MonoBehaviour, but now also extends `WindowView`).
2. Move existing `[Inject] Construct(...)` into a new `<Feature>Window : WindowController<...>`.
3. Replace scene placement with prefab + Addressables address.
4. Replace direct `SetActive` calls (e.g. `MorningScreenView` activates `PreparationScreenView`) with `_uiManager.ShowAsync<NextWindow>()`.
5. Remove the corresponding `RegisterComponentInHierarchy<>` call from `GameplayLifetimeScope`.

### 2.5 `TransitionAnimationService`

Dim/fade transition layer separate from `ISceneTransitionService` (which handles scene-load mechanics only).

- New `ITransitionAnimationService` with `PlayInAsync(CT)` + `PlayOutAsync(CT)` (dim fade in / out).
- Implemented as a MonoBehaviour on `UIManagerCanvas` (sibling of `UICanvasRoot`), exposed via DI as a separate service (not on `IUIManager`).
- Used between scene loads: `await transition.PlayInAsync(); await sceneLoader.Load(); await transition.PlayOutAsync();`.

---

## Phase 3 — Quality / polish (each is independent, triggered by need)

### 3.1 Props-as-ScriptableObject

If a single window type ends up with ≥ 5 content variations (likely candidates: `ConfirmDialog`, future `OfferPopup`), introduce SO-based props:

```csharp
[CreateAssetMenu]
public sealed class ConfirmDialogProps : WindowProps<ConfirmDialogArgs> { ... }
```

Designer creates `ConfirmSellRareBook.asset`, `ConfirmExitWithoutSave.asset`, etc. — code calls `ShowAsync<ConfirmDialog>(myConfirmProps)`. Editor-time `[Show window]` button on the SO for rapid iteration.

### 3.2 Dimmed overlay under Additional

When a Popup opens over a Main window, dim the Main window for visual hierarchy. Implement in `UIManager` by toggling an Image (`UICanvasRoot.DimmedOverlay`) between Main and Additional canvas Z-orders during the popup's life.

### 3.3 Pre-warm cache for frequently opened windows

`IWindowFactory.PreloadAsync<T>()` — triggers `ProdAddressablesWrapper.LoadAsync` without instantiating. Useful for Shop / Inventory / Map. Hook into Bootstrap or gameplay-scene enter.

### 3.4 MessagePipe UI events

If consumers (analytics, tutorial, audio cue) want to react to window lifecycle without DI'ing UIManager, publish:

```csharp
public readonly struct WindowShownMessage<T> { public T Controller { get; } }
public readonly struct WindowHiddenMessage<T> { ... }
```

UI System depends on MessagePipe already-registered in `BootstrapInstaller`. Publish from `UIManager.ShowAsync` / `HideAsync`.

### 3.5 Develop layer with dev windows

`WindowLayer.Develop` is reserved and gets `UISortingLayers.DevelopBase = 4000`. When dev-UI gets non-trivial (cheat menus, telemetry overlays, config editor in-game), replace `UiPilotDebugPanel`'s OnGUI hack with real `WindowController<...>` windows opened via `.AsDevelop()`.

### 3.6 Cache eviction API

If `keepInCache: true` proves too aggressive (memory growth between scenes), add `IUIManager.ClearCache<T>()` and `IUIManager.ClearAllCache()`. Walks `UIStorage`, calls `_factory.Destroy(controller)` for matching entries.

---

## Phase 4 — Post-MVP scope

### 4.1 CollectionsWindow

Page, Main, `keepInCache: true`. Browse/inspect collections of books.

### 4.2 RestorationWindow

Page, Main. Restoration minigame UI (puzzle-style). Whether `keepInCache` makes sense depends on the gameplay (in-progress state).

### 4.3 Tutorial infrastructure

- `TutorialFilter : IUiWindowFilter` — blocks certain windows during specific tutorial steps.
- `TutorialStepPopup` (Popup, Additional) — tooltip-style positioned over a target world / UI element. Likely needs Props-as-SO (3.1) first so each step is a designer-editable asset.

---

## Cross-cutting deferred items

### Sorting compaction

Current behaviour: closing a non-top window in a layer leaves a "hole" in the sortingOrder counter. Up to 100 simultaneous windows per layer before collision; unlikely to be reached, but if becomes relevant, implement compaction in `UISortingController.Release` (walk `_assigned` for the same layer, re-pack indices).

### Custom Unity SortingLayers cleanup

`UI_Hud`, `UI_Main`, `UI_Additional`, `UI_System`, `UI_Develop` were added in `Project Settings → Tags and Layers → Sorting Layers` during Phase 0 attempt at SortingLayer-based ordering. They are now **unused by code** (replaced with sortingOrder ranges). Safe to remove manually; left in place to avoid breaking any prefab that might reference them.

### MVVM controller variant

Initial reference doc considered `WindowWidget<TProps, TView, TViewModel>` as an MVVM variant. **Not planned** unless multiple windows need shared `INotifyPropertyChanged`-style state. Controller↔View + `OnShowStart`/`UpdateWindow` covers the project's needs to date.

### World-space UI (over-NPC indicators, damage numbers)

**Out of scope** for UISystem. Separate `IWorldHudManager` system when needed — per-object `World Space Canvas` for MyBookstore's small NPC count is the simplest fit. Tracked separately.

---

## Open mini-decisions for Phase 2 kick-off

- **Per-feature asmdefs** vs all windows in `Game.Core.UI`? Currently both Settings and ConfirmDialog live in `Game.Core.UI`. As feature windows grow (Shop, Inventory production UI), move them into per-feature asmdefs (`Game.Shop.UI`, etc.) that reference `Game.Core.UI`.
- **Single-instance enforcement** (`SingleInstanceInQueue` from reference doc)? Defer until first real duplicate-Show issue.
- **Background fade for parent during Popup** — see 3.2. Cheap and improves modal feel; consider doing as part of Phase 2.5 polish pass after migration completes.

---

## Status snapshot at handoff (2026-06-14)

| Phase | Status |
|---|---|
| Phase 0 — framework scaffold | Shipped (`ae36d66`) |
| Phase 1 — pilot windows | Shipped (`ae36d66`) |
| Phase 2 — migration + core game windows | **TBD — this doc + Notion follow-up task** |
| Phase 3 — quality / polish | Deferred, triggered by real need |
| Phase 4 — post-MVP scope | Deferred |
