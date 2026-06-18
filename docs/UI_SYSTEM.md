# UI System

In-project UI window framework under `Assets/Game/Core/UI/`. Authoritative reference for the current state. Future scope is tracked separately in [improvements/UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md).

---

## 1. Quick map

| Layer | Where | Purpose |
|---|---|---|
| Public API | `Game.UI.IUIManager` | `ShowAsync<T>` / `HideAsync` / `HideTopAsync` / `IsWindowShown<T>` / `SetManualLock` |
| Controller base | `WindowController<TView>` | Lifecycle, `[Inject] IUIManager`, `protected CloseAsync()` |
| View base | `WindowView : MonoBehaviour` | Holds Canvas + CanvasGroup + WindowAnimation refs |
| Args | `WindowArgs` (+ feature-specific subclasses) | Fluent layer override + parent + primary key |
| Layer enums | `WindowType` (Page / Popup / Widget), `WindowLayer` (Main / Additional / System / Develop) | Stack semantics × sorting bucket |
| Loading | `AddressablesWindowFactory` → `Infrastructure.ProdAddressablesWrapper` | Async load + cached refs + `Release` |
| Sorting | `UISortingController` | Per-layer `sortingOrder` ranges (no Unity SortingLayers) |
| Caching | `UIStorage` | Keyed by `(Type, primaryKey)` |
| Stack | `UIStack` | 4 per-layer stacks + focus order |
| Animation | `WindowAnimation` / `FadeAnimation` | UniTask + `CanvasGroup.alpha` lerp |
| Blocker | `Lock` / `LockMonitor` | Toggles `UICanvasRoot.Blocker` during animations and manual locks |
| Filter | `IUiWindowFilter` / `UiFilter` | Plug-in policy gate for `ShowAsync` (e.g. future tutorial) |
| Wait helpers | `WaitForCloseAsync()` / `WaitForResultAsync<TResult>()` | Extensions on `IWindowController` |

---

## 2. Assembly

`Assets/Game/Core/UI/Game.Core.UI.asmdef`:

| Reference | Why |
|---|---|
| `VContainer` | DI for IUIManager + `[Inject]` into controllers |
| `UniTask` | Async lifecycle, animations |
| `Unity.Addressables` + `Unity.ResourceManager` | Prefab loading via `ProdAddressablesWrapper` |
| `Unity.TextMeshPro` | TMP labels in window views |
| `Infrastructure` | `ProdAddressablesWrapper.LoadAsync / Release` |

Root namespace: `Game.UI`. Feature windows live under `Game.UI.Common` (`SettingsWindow`, `ConfirmDialog`).

---

## 3. Window lifecycle (with `keepInCache`)

### First show

```
factory.CreateAsync<T>            (Addressables.LoadAsync → Instantiate)
_resolver.Inject(controller)      [Inject] IUIManager etc.
storage.Add(controller, key)
controller.ApplyArguments(args)
view.CanvasGroup.alpha = 0
view.gameObject.SetActive(true)   ← must be active before Apply (Canvas quirk)
sorting.Apply                     (sortingOrder = base + step)
stack.Push
locks.Acquire                     (Blocker on)
   OnInit                         ← only the first time
   OnShowStart
   await PlayInAsync              ← FadeAnimation
   OnShowComplete
locks.release                     (Blocker off if no other locks)
```

### Hide → Show again (`keepInCache: true`)

```
HideAsync(forceClose: false)
   close children (Additional with ParentWindow == this) recursively
   OnHideStart(true)
   await PlayOutAsync
   view.gameObject.SetActive(false)
   OnHideComplete(true)
   Closed event fired
stack.Remove
sorting.Release

[Settings has keepInCache=true → kept in UIStorage, prefab NOT released]

Show again:
   storage.TryGet returns cached
   view re-parented (SetParent(root, false))
   alpha=0, SetActive(true), sorting.Apply, stack.Push, locks.Acquire
   UpdateWindow                   ← instead of OnInit
   OnShowStart
   ...
```

### Hide → Show again (`keepInCache: false`, default)

Same Hide flow, **then**:
```
storage.Remove(controller)
factory.Destroy(controller)
   Object.Destroy(view.gameObject)
   ProdAddressablesWrapper.Release(prefabAddress)
   controller.Dispose()
```

Next `ShowAsync<T>` starts from scratch.

### Hooks (`WindowController<TView>`)

| Hook | Called when |
|---|---|
| `OnInit` | Once per controller instance, after Configure + Inject |
| `OnShowStart` | Every Show, before animation. `Arguments` is current |
| `OnShowComplete` | Every Show, after animation |
| `UpdateWindow` | Every Show **after the first** (when cached) |
| `OnHideStart(bool isClosed)` | Every Hide, before animation |
| `OnHideComplete(bool isClosed)` | Every Hide, after animation |
| `OnDispose` | Once at controller end-of-life (only for non-cached or full teardown) |

`Closed` event fires after `OnHideComplete(isClosed: true)` and is what `WaitForCloseAsync` listens to.

---

## 4. WindowAttribute

```csharp
[Window(prefabAddress: "UI/Common/SettingsWindow",
        type: WindowType.Page,
        keepInCache: true,
        priority: 0)]
public sealed class SettingsWindow : WindowController<SettingsWindowView> { ... }
```

| Field | Meaning |
|---|---|
| `prefabAddress` | Addressables address. Must match the address in `Window → Asset Management → Addressables → Groups` exactly. |
| `type` | `Page` / `Popup` / `Widget`. Determines default layer and parent root |
| `keepInCache` | `true` → instance stays in `UIStorage` after Hide, Addressables handle retained, `OnInit` runs only once. See §3 |
| `priority` | Reserved for ordered Show queue (not currently used) |

Default layer mapping when `args.LayerOverride` is `null`:

| WindowType | Default WindowLayer | Parent root |
|---|---|---|
| Page | Main | `WindowsRoot` |
| Popup | Additional | `WindowsRoot` |
| Widget | Main | `HudRoot` |

---

## 5. WindowArgs and fluent API

```csharp
public class WindowArgs
{
    public WindowLayer? LayerOverride { get; }
    public IWindowController ParentWindow { get; }
    public int PrimaryKey { get; set; }

    public WindowArgs AsMain();        // overrides layer
    public WindowArgs AsAdditional();
    public WindowArgs AsSystem();
    public WindowArgs AsDevelop();
    public WindowArgs WithParent(IWindowController parent);
    public virtual int GetPrimaryKey();
}
```

Subclass for runtime-configured windows. Example:

```csharp
public sealed class ConfirmDialogArgs : WindowArgs
{
    public string Title { get; }
    public string Body { get; }
    public string ConfirmLabel { get; }
    public string CancelLabel { get; }

    public ConfirmDialogArgs(string title, string body,
                              string confirmLabel = "OK",
                              string cancelLabel = "Cancel")
    {
        Title = title;
        Body = body;
        ConfirmLabel = confirmLabel;
        CancelLabel = cancelLabel;
        AsAdditional();
    }
}
```

**Parent-child:** when a child is opened with `WithParent(parentController)`, closing the parent auto-closes the child via `UIManager.HideInternalAsync`’s recursive children sweep (`_storage.All.Where(c => c.Arguments?.ParentWindow == controller)`).

**Primary key:** allows multiple instances of the same controller type to live in cache at once. Default `0`. Override `GetPrimaryKey()` in `WindowArgs` to make instance identity meaningful.

---

## 6. Result-returning windows

For "open dialog, await user choice" flow:

```csharp
public interface IResultWindow<out TResult>
{
    TResult Result { get; }
}
```

Controller implements it:

```csharp
public sealed class ConfirmDialog : WindowController<ConfirmDialogView>,
                                    IResultWindow<ConfirmDialogResult>
{
    public ConfirmDialogResult Result { get; private set; }

    private void OnConfirmClicked()
    {
        Result = ConfirmDialogResult.Confirmed;
        CloseAsync().Forget();
    }
}
```

Usage:

```csharp
var args = new ConfirmDialogArgs("Sell book?", "30% below average price.")
    .WithParent(this);

var dialog = await UIManager.ShowAsync<ConfirmDialog>(args);
var result = await dialog.WaitForResultAsync<ConfirmDialogResult>();
if (result == ConfirmDialogResult.Confirmed) { ... }
```

Plain "wait for close" without typed result: `await controller.WaitForCloseAsync(ct);`.

---

## 7. Sorting model

**NOT** Unity SortingLayers. Reason: nested canvases under a Screen Space Overlay parent silently drop `Canvas.sortingLayerName` / `sortingLayerID` changes. We use **sortingOrder ranges**:

| WindowLayer | Base | Range |
|---|---|---|
| Main | 1000 | 1000–1999 |
| Additional | 2000 | 2000–2999 |
| System | 3000 | 3000–3999 |
| Develop | 4000 | 4000–4999 |

Step inside a layer: `10`. Up to 100 simultaneously visible windows per layer before collision.

`UISortingController.Apply` bumps the layer's top counter by `10`, sets `canvas.overrideSorting = true`, sets `canvas.sortingOrder`. `Release` rolls the counter back if the closed window was the top in its layer (no fragmentation in the common case).

Custom Unity SortingLayers (`UI_Main` etc.) added during Phase 0 in Project Settings are **unused** and can be removed manually if desired (`Edit → Project Settings → Tags and Layers → Sorting Layers`).

---

## 8. Adding a new window — recipe

1. **Pick a folder** under `Assets/Game/Core/UI/Common/<FeatureName>/` (or per-feature module — to be decided in Phase 2).
2. **`MyWindowView.cs`** — extends `WindowView`. SerializeField TMP labels, Buttons, etc.
3. **`MyWindowArgs.cs`** (optional) — extends `WindowArgs` if runtime content needs to be passed. Default layer is set via `AsXxx()` in the ctor.
4. **`MyWindow.cs`** — extends `WindowController<MyWindowView>`. Has `[Window("address", type, keepInCache: ...)]`. Subscribe to buttons in `OnInit`, refresh data in `OnShowStart` (and `UpdateWindow` for cached windows).
5. **Prefab** — `MyWindow.prefab` with: `RectTransform` (stretch full), `Canvas` + `CanvasGroup`, `MyWindowView`, `FadeAnimation`. On `WindowView` inspector, set `Animation` to the `FadeAnimation` on the same GameObject. **Do NOT pre-set `Override Sorting`** — `UISortingController` does it at runtime.
6. **Addressables** — `Window → Asset Management → Addressables → Groups`. Drag prefab into the `UI` group. Set the address to match the `[Window(...)]` string exactly.
7. **Show it:** `var window = await uiManager.ShowAsync<MyWindow>(args);`

No VContainer registration needed for the controller — it's `new T()`'d by the factory then `_resolver.Inject(controller)` runs `[Inject]` methods.

---

## 9. VContainer wiring

```csharp
// Assets/Game/Core/Installers/Features/UiSystemVContainerBindings.cs
public static void RegisterUiSystem(this IContainerBuilder builder, UICanvasRoot uiCanvasRootPrefab)
{
    builder.RegisterComponentInNewPrefab(uiCanvasRootPrefab, Lifetime.Singleton)
        .UnderTransform((Transform)null)            // spawn at scene root (then DDOL in UICanvasRoot.Awake)
        .As<IUICanvasRoot>();

    builder.Register<IWindowFactory, AddressablesWindowFactory>(Lifetime.Singleton);
    builder.Register<IUIStorage, UIStorage>(Lifetime.Singleton);
    builder.Register<IUISortingController, UISortingController>(Lifetime.Singleton);
    builder.Register<IUIStack, UIStack>(Lifetime.Singleton);
    builder.Register<IUiFilter, UiFilter>(Lifetime.Singleton);
    builder.Register<LockMonitor>(Lifetime.Singleton);

    builder.Register<UIManager>(Lifetime.Singleton)
        .As<IUIManager>()
        .AsSelf();

    // Force eager instantiation; otherwise nothing resolves IUIManager at boot
    // and the UIManagerCanvas prefab never spawns.
    builder.RegisterBuildCallback(resolver => resolver.Resolve<IUIManager>());
}
```

`BootstrapInstaller` carries the `UICanvasRoot` prefab via `[SerializeField] private UICanvasRoot _uiCanvasRootPrefab;` and calls `builder.RegisterUiSystem(_uiCanvasRootPrefab)`.

### Sibling MonoBehaviour injection

`RegisterComponentInNewPrefab` only injects the registered component. If you add another MonoBehaviour to the `UIManagerCanvas` root (e.g. `UiPilotDebugPanel`), its `[Inject]` methods will NOT run automatically.

`UICanvasRoot` works around this: in `Start()` (after the full container resolve chain has completed) it walks `GetComponents<MonoBehaviour>()` and calls `_resolver.Inject(mb)` on each sibling. Deferring to `Start` avoids the circular Resolve that would happen if we did it inside `[Inject]` itself.

---

## 10. Scene structure

```
DontDestroyOnLoad
├── GlobalLifetimeScope (Clone)
└── UIManagerCanvas (Clone)
    ├── HudRoot                  ← persistent widgets (Phase 2+)
    ├── WindowsRoot              ← Pages, Popups, System dialogs
    └── Blocker                  ← raycast-blocking Image, off by default
```

`UICanvasRoot.Awake` calls `DontDestroyOnLoad(gameObject)` itself (VContainer's `RegisterComponentInNewPrefab` doesn't have a `DontDestroyOnLoad()` chain method).

Per-window Canvases nest under `WindowsRoot` or `HudRoot` at runtime; their RectTransform should be stretch-full on the prefab so they fill the parent root.

---

## 11. Locks / Blocker

`LockMonitor.Acquire(owner)` returns an `IDisposable` `Lock`. While at least one Lock is alive, `UICanvasRoot.Blocker` is active (intercepts UI raycasts).

Used internally for the duration of every Show / Hide animation. Available externally:

```csharp
using var loadingLock = uiManager.SetManualLock("loading-shop");
await shopDataService.LoadAsync();
// auto-released on scope exit
```

---

## 12. Filtering / gating

`IUiWindowFilter.CanBeShown(Type)` — return `false` to block. `UiFilter` aggregates filters and short-circuits on first `false`. `IUIManager.ShowAsync<T>` checks the filter first and returns `null` if blocked.

Usage (Phase 2+, when tutorial lands): `filter.AddFilter(new TutorialBlocksShopFilter())`.

---

## 13. Debug overlay

`UiPilotDebugPanel` (Editor / DEVELOPMENT_BUILD only) lives on `UIManagerCanvas`. OnGUI exposes:

- **Show Settings** — opens the SettingsWindow (cache validation)
- **Show Confirm (await)** — opens ConfirmDialog, logs the awaited result
- **Hide Top** — closes the focused window

Remove or replace before release — it's behind `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so it compiles out of release builds.

---

## 14. Known limitations / decisions

- **Sorting via Unity SortingLayer doesn't work** for nested canvases under a Screen Space Overlay parent. We use `sortingOrder` ranges. Documented in §7.
- **Settings cached forever during a session** — `keepInCache=true` never auto-evicts. `ClearCache<T>()` API is not exposed yet; add when needed.
- **Sorting order grows monotonically per layer** if the closed window wasn't the top — small "holes" left in the counter. Cosmetic, doesn't affect correctness; up to 100 simultaneous windows per layer.
- **`TransitionAnimationService`** (dim/fade between scenes) is deferred. Scene transitions currently use `ISceneTransitionService` from `Game.Bootstrap.Loading` directly without dim/fade.
- **HudRoot is currently empty** — `GameHudView` and other persistent HUD elements live as scene-placed MonoBehaviour in `Assets/Game/Features/Resources/UI/`. Migration tracked under [improvements/UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md).
- **World-space UI** (over-NPC nameplates, damage numbers, speech bubbles) is **out of scope** for this system. When needed, build a separate `IWorldHudManager` — UI System is for screen-space windows only.

---

## 15. File map

```
Assets/Game/Core/UI/
├── Game.Core.UI.asmdef
├── Animation/
│   ├── WindowAnimation.cs            ← abstract
│   └── FadeAnimation.cs              ← CanvasGroup.alpha lerp, UniTask
├── Args/
│   └── WindowArgs.cs
├── Common/
│   ├── Confirm/
│   │   ├── ConfirmDialog.cs
│   │   ├── ConfirmDialogArgs.cs
│   │   ├── ConfirmDialogResult.cs
│   │   ├── ConfirmDialogView.cs
│   │   └── Prefabs/ConfirmDialog.prefab
│   └── Settings/
│       ├── SettingsWindow.cs
│       ├── SettingsWindowView.cs
│       └── Prefabs/SettingsWindow.prefab
├── Controller/
│   ├── IWindow.cs
│   ├── IWindowController.cs
│   ├── WindowAttribute.cs
│   ├── WindowController.cs
│   ├── WindowLayer.cs
│   ├── WindowType.cs
│   └── WindowView.cs
├── Core/
│   ├── IResultWindow.cs
│   ├── IUICanvasRoot.cs
│   ├── IUIManager.cs
│   ├── IUIStack.cs
│   ├── IUIStorage.cs
│   ├── UICanvasRoot.cs
│   ├── UIManager.cs
│   ├── UIStack.cs
│   └── UIStorage.cs
├── Debug/
│   └── UiPilotDebugPanel.cs          ← #if UNITY_EDITOR || DEVELOPMENT_BUILD
├── Extensions/
│   └── WindowControllerExtensions.cs
├── Factory/
│   ├── AddressablesWindowFactory.cs
│   └── IWindowFactory.cs
├── Filter/
│   ├── IUiWindowFilter.cs
│   └── UiFilter.cs
├── Lock/
│   ├── Lock.cs
│   └── LockMonitor.cs
├── Prefabs/
│   └── UIManagerCanvas.prefab
└── Sorting/
    ├── IUISortingController.cs
    ├── UISortingController.cs
    └── UISortingLayers.cs
```

Wiring:

```
Assets/Game/Core/Installers/Features/UiSystemVContainerBindings.cs
Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.cs   ← +[SerializeField] UICanvasRoot
```

---

## 16. Adding a new SortingLayer-segment-equivalent (future)

If you need a new logical bucket (e.g. `Tutorial` between Additional and System):

1. Add to `WindowLayer` enum.
2. Add `TutorialBase` constant to `UISortingLayers`.
3. Add the base to `BaseOrderFor` and to `UISortingController._topOrderByLayer` dictionary.
4. Pick a base value that doesn't collide with neighbours (e.g. `2500`).
5. Map a default `WindowType` to it in `UIManager.DefaultLayerFor` if any new `WindowType` is also being introduced.
6. Possibly update `UIStack.FocusOrder`.
