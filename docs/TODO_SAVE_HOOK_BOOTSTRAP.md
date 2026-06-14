# TODO: Cleaner `ISaveHook` registration (drop forced-construction injects in `Bootstrap.cs`)

**Status:** Open · raised after Inventory / Resources / Progression iteration
**Owner:** Dev
**Tracking:** add a Notion ticket when scheduled

---

## Problem

`Bootstrap.cs` currently injects three services that it never calls:

```csharp
// Injected to force construction (and therefore ISaveHook self-registration) before
// SaveDataLoadOperation runs LoadAsync. We never invoke methods on these fields directly.
// ReSharper disable NotAccessedField.Local
private IInventoryService _inventory;
private IResourcesService _resources;
private IProgressionService _progression;
// ReSharper restore NotAccessedField.Local
```

The services self-register as `ISaveHook` inside their constructors:

```csharp
public ResourcesService(ISaveService save, IResourcesRepository repository)
{
    ...
    save.RegisterHook(this);  // ← happens only if VContainer actually constructs this object
}
```

VContainer constructs singletons lazily. If nobody resolves them before
`SaveService.LoadAsync` runs in `phase_data_load`, the hooks are not on
`SaveService._hooks` and `AfterLoadAsync` never fires for them. So `Bootstrap.cs`
pulls them in just to *force* the construction.

This works, but it leaks:

- Adding a fourth `ISaveHook`-based service (next time we ship one) requires editing
  `Bootstrap.cs` again. Bootstrap is not the right place for "list of save-aware
  services in the project."
- The fields look like dead code. Future maintainers (or a code-review tool) will
  ask why they exist; the ReSharper suppression hides the fact that this is an
  intentional anti-pattern.
- Coupling: `Bootstrap` now depends on `Game.Inventory.API`, `Game.Resources.API`,
  `Game.Progression.API` purely as a side-channel for DI ordering.
- The pattern is invisible to anyone who has not read this comment. There is no
  compile-time check that a new save-hook service has been wired into the boot.

---

## Options

### A. Make each service an `IStartable` (or `IInitializable`)

VContainer auto-constructs and auto-invokes `IStartable.Start()` /
`IAsyncStartable.StartAsync()` on registered types. Move `RegisterHook(this)` out
of the constructor and into `Start()`. DI calls `Start()` for every registered
startable at scope build time — exactly when we need it.

- ✅ Idiomatic VContainer.
- ✅ `Bootstrap.cs` stops carrying the burden.
- ✅ Adding a new save-aware service: implement `IStartable`, register, done.
- ⚠️ Each service has to expose itself as `IStartable` in DI (extra `.As<IStartable>()`).
- ⚠️ Startables run on the main thread synchronously — fine for `RegisterHook`.

### B. Single `SaveHookBootstrapper : IStartable` that resolves `IEnumerable<ISaveHook>`

Add one Bootstrap-side class. VContainer collects every type registered as
`ISaveHook` into the enumerable and the startable iterates once, calling
`_save.RegisterHook(hook)`. Hook services no longer call `RegisterHook` in their
ctor — the bootstrapper does.

```csharp
public sealed class SaveHookBootstrapper : IStartable
{
    private readonly ISaveService _save;
    private readonly IReadOnlyList<ISaveHook> _hooks;
    public SaveHookBootstrapper(ISaveService save, IReadOnlyList<ISaveHook> hooks) { ... }
    public void Start() { foreach (var h in _hooks) _save.RegisterHook(h); }
}
```

- ✅ One seam, registered once in `BootstrapInstaller`.
- ✅ Adding a new save hook: register the service `.As<ISaveHook>()`. Bootstrap discovers it automatically.
- ✅ Removes the "self-registration in constructor" surprise — hook lifetime is explicit.
- ⚠️ Slightly more indirection than A; the bootstrapper itself has to be an `IStartable` registered as an entry point.

### C. Push the responsibility into `SaveService`

`SaveService` accepts `IEnumerable<ISaveHook>` in its constructor and stores them.
`RegisterHook` could remain for late hooks but base hooks come from DI.

- ✅ Single owner of the hook list.
- ⚠️ Saves feature now depends on the consumer pattern; `IEnumerable<ISaveHook>` resolution timing has to land before `LoadAsync`.
- ⚠️ Slightly leaks the consumer model into the save feature.

---

## Recommendation

**Option B: a dedicated `SaveHookBootstrapper` as `IStartable`.**

It is the smallest change that removes the foot-gun from `Bootstrap.cs` and
makes "I am a save hook" a pure DI registration (`.As<ISaveHook>()`). It also
removes the constructor side-effect (`RegisterHook(this)`) from `InventoryService`,
`ResourcesService`, `ProgressionService` — moving them closer to plain POCOs.

Option A is also reasonable; the trade-off is whether we want `IStartable`
sprinkled across feature implementations (A) or one consolidated entry point (B).

---

## Migration plan (Option B)

1. Add `Assets/Game/Core/Installers/Bootstrap/SaveHookBootstrapper.cs`:
   ```csharp
   public sealed class SaveHookBootstrapper : IStartable
   {
       private readonly ISaveService _save;
       private readonly IReadOnlyList<ISaveHook> _hooks;
       public SaveHookBootstrapper(ISaveService save, IReadOnlyList<ISaveHook> hooks)
       { _save = save; _hooks = hooks; }
       public void Start() { foreach (var h in _hooks) _save.RegisterHook(h); }
   }
   ```
2. In `BootstrapInstaller.InstallBindings`, register the entry point **before** any
   `RegisterSave()`-dependent loading phase:
   ```csharp
   builder.RegisterEntryPoint<SaveHookBootstrapper>(Lifetime.Singleton);
   ```
3. In each feature's `VContainerBindings`, add `.As<ISaveHook>()` to the service
   registration:
   ```csharp
   builder.Register<InventoryService>(Lifetime.Singleton)
       .As<IInventoryService>()
       .As<ISaveHook>();   // ← was registered as a hook via ctor side-effect; now declared in DI
   ```
4. Remove `_save.RegisterHook(this)` from the constructors of:
   - `InventoryService`
   - `ResourcesService`
   - `ProgressionService`
5. Remove the dummy fields and their injections from `Bootstrap.cs`:
   - `_inventory`, `_resources`, `_progression` (plus the `using` directives).
6. Update tests:
   - Constructor-self-registration tests (e.g. `Constructor_SelfRegistersAsSaveHook`)
     become irrelevant — delete or repurpose.
   - Tests calling `service.AfterLoadAsync(...)` manually still work; they bypass
     the bootstrapper entirely.

## Verification

- Cold start: same console as today — `[Inventory] loaded ...`, `[Resources] loaded ...`,
  `[Progression] loaded ...`. If any service skips its log, the bootstrapper
  missed it (registration drift between DI and `.As<ISaveHook>()`).
- Restart: HUD shows the persisted gold/reputation — hooks fired during
  `SaveService.LoadAsync` exactly as before.
- Add a one-off `EnumerableHookCountTest`: integration check that the bootstrapper
  resolves N hooks where N == number of `.As<ISaveHook>()` registrations. Cheap
  guard against silent regressions when a future feature forgets to register.

## Notes

- This refactor is purely a cleanup: behaviour does not change. It moves the
  "list of save-aware services" from `Bootstrap.cs` to DI configuration, where it
  belongs.
- Worth doing **before** any new save-aware feature lands — otherwise it carries
  the smell forward and the Bootstrap dependency list keeps growing.
- Do NOT do this and the "promote SaveService to take `IEnumerable<ISaveHook>`"
  refactor together. Pick one (B is the lighter choice).
