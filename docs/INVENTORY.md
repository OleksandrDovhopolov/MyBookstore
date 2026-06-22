# Inventory System

Local-first player inventory feature. Owns books, decor, puzzle pieces — and any new
category added at startup. Designed to be replaced or augmented by a server backend
without changing consumers.

> Source code: `Assets/Game/Features/Inventory/`
> Save module: `inventory` (schema 1)
> Code language: English (per `docs/LANGUAGE_POLICY.md`)

---

## At a glance

```
┌─────────────────────────────────────────────────────────────────────┐
│                       Game.Inventory.API                            │
│  IInventoryService  IInventoryRepository  IItemCategoryRegistry     │
│  IInventoryItemUseHandler  IInventoryUseRouter                      │
│  DTOs: InventoryItem, InventoryChangeEvent, InventoryUseResult,     │
│        ItemCategory, ItemStackingMode, InventoryStateDto            │
│  Constants: InventoryCategories, InventorySaveKeys                  │
└─────────────────────────────────────────────────────────────────────┘
                                  ▲
                                  │ implemented by
                                  │
┌─────────────────────────────────────────────────────────────────────┐
│                          Game.Inventory                             │
│  InventoryService            ←─── ISaveHook (self-registers)        │
│  SaveBackedInventoryRepository  → ISaveService                      │
│  ItemCategoryRegistry                                               │
│  InventoryUseRouter                                                 │
│  UseHandlers/  NoopDecorUseHandler · PuzzleAssembleUseHandler       │
│  UI/           InventoryScreenView · InventoryItemRowView (debug)   │
└─────────────────────────────────────────────────────────────────────┘
```

Two assembly definitions:
- **`Game.Inventory.API`** — no Unity engine deps beyond UniTask. Consumers (Preparation,
  Ftue, Decor, Puzzle, future Quests) reference only this assembly.
- **`Game.Inventory`** — implementation, VContainer bindings live in `Game.Bootstrap`
  (see `InventoryVContainerBindings`).

---

## Domain model

### Two-bucket storage

The service stores items in two parallel collections inside `InventoryService`:

| Bucket  | C# type                                  | Used when category is …  | Example items                            |
|---------|------------------------------------------|--------------------------|------------------------------------------|
| Uniques | `Dictionary<string, string>` (id → cat)  | `ItemStackingMode.Unique`| Books, decor, recipes, postcards         |
| Stacks  | `Dictionary<string, (string, int)>`      | `ItemStackingMode.Stack` | Puzzle pieces, resources, gifts, seeds   |

The chosen bucket is decided by the **category**, not the item id. A category is
registered once at startup with a stacking mode:

```csharp
registry.Register(new ItemCategory("book",         ItemStackingMode.Unique, "Books"));
registry.Register(new ItemCategory("decor",        ItemStackingMode.Unique, "Decor"));
registry.Register(new ItemCategory("puzzle_piece", ItemStackingMode.Stack,  "Puzzle Pieces"));
```

Built-in ids live in `InventoryCategories.{Book, Decor, PuzzlePiece}`. To add a new
category — register it in `InventoryVContainerBindings.RegisterInventory()` and
optionally extend `InventoryCategories` for type-safe access.

### Why a registry, not an enum

Registry-driven categories let downstream features add their own categories without
patching the inventory feature itself. A future decor feature can register `decor` (it
already does in MVP); a future kitchen feature can register `recipe`. The inventory
service does not know about specific categories — only their stacking mode.

### InventoryItem (the read DTO)

```csharp
public sealed class InventoryItem
{
    public string ItemId { get; }
    public string CategoryId { get; }
    public int Count { get; }   // 1 for Unique entries; N for Stack
}
```

Returned by every read method on `IInventoryService` and passed into use handlers.
Treat it as immutable.

---

## Public API (`IInventoryService`)

### Sync read

```csharp
IReadOnlyList<InventoryItem> GetAll();
IReadOnlyList<InventoryItem> GetByCategory(string categoryId);
bool Has(string itemId);
int  GetCount(string itemId);   // 1 for unique, N for stack, 0 if absent
```

Sync because consumers like `DayProgressInventoryProvider` are called from synchronous
flow (Preparation screen rendering). Safe to call after `AfterLoadAsync` runs during
save load — the in-memory cache is populated then.

### Async write

```csharp
UniTask AddAsync(string itemId, string categoryId, int amount, CancellationToken ct);
UniTask AddBatchAsync(IEnumerable<InventoryItem> items, CancellationToken ct);
UniTask<bool> RemoveAsync(string itemId, int amount, CancellationToken ct);
```

Semantics:

- **`AddAsync` on Unique categories** is idempotent. Adding an already-owned id is a
  no-op — no repository write, no `Changed` event.
- **`AddAsync` on Stack categories** increments the count. `amount > 1` adds multiple at
  once.
- **`AddBatchAsync`** is the bulk path. All items are merged into the in-memory state,
  then the repository is written exactly once. Use this from seeders (FTUE) and from any
  reward flow that grants several items together.
- **`RemoveAsync` returns `false`** when the inventory does not hold that many — and
  nothing is mutated. No partial removals.
- Unknown `categoryId` → an error is logged and the call is a no-op.

### Events

```csharp
event Action<InventoryChangeEvent> Changed;
```

Fires once per successful mutation. Subscribers (UI, analytics, mission triggers) can
react in fan-out style. Batch operations fire one event per affected item.

```csharp
public sealed class InventoryChangeEvent
{
    public string CategoryId { get; }
    public string ItemId { get; }
    public InventoryChangeKind Kind { get; }   // Added | Removed | Updated
    public int NewCount { get; }               // 0 means the entry was deleted
}
```

---

## Persistence

### Save module

`InventorySaveKeys.State` = `"inventory"`, schema version 1. The persisted POCO is
`InventoryStateDto` (lives in the API assembly so future repositories can target it):

```csharp
public sealed class InventoryStateDto
{
    public List<UniqueEntry> Uniques { get; set; } = new();
    public List<StackEntry> Stacks  { get; set; } = new();

    public sealed class UniqueEntry { public string ItemId; public string CategoryId; }
    public sealed class StackEntry  { public string ItemId; public string CategoryId; public int Count; }
}
```

### Load flow (one-time, at app start)

```
SaveService.LoadAsync
   └─ for each ISaveHook: AfterLoadAsync
        └─ InventoryService.AfterLoadAsync
             ├─ repo.LoadAsync(ct)              // SaveBackedInventoryRepository → ISaveService
             ├─ rebuild _uniques + _stacks
             └─ _loaded = true
```

`InventoryService` self-registers as `ISaveHook` in its constructor. To ensure the
service is *constructed* before `LoadAsync` (so that registration happens in time),
`Bootstrap.cs` injects `IInventoryService` as one of its dependencies — VContainer
builds the singleton when Bootstrap is constructed, well before
`SaveDataLoadOperation` runs.

### Write flow

```
InventoryService.AddAsync / RemoveAsync / AddBatchAsync
   ├─ mutate in-memory dictionaries
   ├─ await repo.SaveAsync(dto, ct)            // SaveService.UpdateModuleAsync(...) under the hood
   └─ raise Changed
```

The repository is awaited before `Changed` fires. Subscribers can assume that, by the
time they see the event, the change is committed (modulo SaveService's debounced flush
to disk — which is a SaveService concern, not an inventory one).

---

## Server readiness

The interface seam is `IInventoryRepository`:

```csharp
public interface IInventoryRepository
{
    UniTask<InventoryStateDto> LoadAsync(CancellationToken ct);
    UniTask SaveAsync(InventoryStateDto state, CancellationToken ct);
}
```

MVP implementation:

- **`SaveBackedInventoryRepository`** — reads and writes the `inventory` save module
  through `ISaveService`. Fully local. This is what ships now.

Future implementations (not blocking MVP):

- **`HttpInventoryRepository`** — server-authoritative reads/writes. On a write the
  service awaits the HTTP round-trip, so failures naturally propagate to the caller
  (e.g. a UI "use" button stays disabled until the server confirms).
- **`WriteThroughInventoryRepository`** — local cache + background server sync, modeled
  on `HttpSaveStorage`'s write-through pattern.

Swapping the implementation only requires changing one DI line in
`InventoryVContainerBindings`. Consumers do not change.

---

## Use handlers

Each category can opt into an "use this item" action through a handler plugin:

```csharp
public interface IInventoryItemUseHandler
{
    string SupportedCategoryId { get; }
    UniTask<InventoryUseResult> UseAsync(InventoryItem item, CancellationToken ct);
}
```

Routing happens through `IInventoryUseRouter.UseAsync(itemId, ct)`:

1. Find the item in the inventory. Missing → `Success=false, message="not owned"`.
2. Find a handler whose `SupportedCategoryId` matches the item's category. Missing →
   `Success=false, message="no handler for category '…'"`.
3. Call `handler.UseAsync(item, ct)`.
4. If `result.Success && result.ConsumeAfterUse` → automatically call
   `InventoryService.RemoveAsync(itemId, 1, ct)`.
5. Return the result.

### Handler authoring tips

- Keep handlers small. They are not the place for complex feature logic — invoke a
  domain service inside `UseAsync` if needed.
- Return `InventoryUseResult.Ok(consume: true)` for consumables (e.g. a single-use gift),
  `Ok(consume: false)` for actions that keep the item (activating decor, opening a
  postcard you can re-read).
- Use `InventoryUseResult.Fail("…")` to bail out without consuming.

### Built-in stub handlers

| Handler                        | Category       | Behaviour                                                                                                     |
|--------------------------------|----------------|---------------------------------------------------------------------------------------------------------------|
| `NoopDecorUseHandler`          | `decor`        | Logs `[Decor] activate stub: {itemId}`, keeps the item. Real decor activation lands with the decor feature.   |
| `PuzzleAssembleUseHandler`     | `puzzle_piece` | Counts player's puzzle pieces; if `≥ FullPiecesStub` logs "assembled (stub)", otherwise logs "{n}/{required}". |

Replace these as the real features ship.

---

## Integration with other features

### FTUE (first-time user experience)

On a clean first launch, `FtueBootstrapper` seeds the starter set by calling
`IInventoryService.AddBatchAsync(...)` with one `InventoryItem` per starter book
(category `book`). It does this after writing `day_progress` (gold) and before writing
the `ftue.applied` marker, so a crash mid-bootstrap leaves a recoverable state.

### Preparation

`DayProgressInventoryProvider` reads `IInventoryService.GetByCategory("book")`, maps ids
to `BookConfig` via `IConfigsService.TryGet<BookConfig>`, and returns only those books.
If the category is empty, it returns an empty list and logs a warning. There is no
catalog fallback here: inventory is the ownership source of truth.

### Sales

Sales does not read inventory directly to build the day setup. It receives
`SalesSessionSetup` from `PreparationSalesSetupProvider`, which is itself populated from
the player's Preparation choices.

On a successful sale, Sales consumes the sold book by calling
`IInventoryService.RemoveAsync(bookId, 1, ct)`. `SalesShelfState` remains a current
shelf/session state for UI and day flow; it is not the source of truth for whether the
player owns a book.

### Debug UI

`InventoryScreenView` is a lightweight tabbed window that lists items by category and
exposes a Use button when a handler exists. It is opt-in: present in the scene as an
inactive GameObject; toggle from the Inspector during dev. Production UI lands with the
UI System module.

---

## Adding a new category

1. **Pick a stacking mode.** Unique for "you either own it or you don't" (decor,
   recipes, dyes). Stack for "N copies aggregate to one row" (puzzle pieces,
   consumables, seeds).
2. **Register at startup** in `InventoryVContainerBindings.RegisterInventory()`:
   ```csharp
   registry.Register(new ItemCategory("postcard", ItemStackingMode.Unique, "Postcards"));
   ```
3. **Optional:** add a constant to `InventoryCategories` for type-safe access from
   consumers.
4. **Optional:** ship an `IInventoryItemUseHandler` for it if the category has an action.

That is the entire surface. The save format does not need a schema bump — categories
are stored on each entry alongside the id.

---

## Testing

Tests live in `Assets/Game/Features/Inventory/Tests/Editor/` and use the local fakes
`FakeInventoryRepository` and `FakeSaveService`.

| Suite                                | What it covers                                                                                       |
|--------------------------------------|------------------------------------------------------------------------------------------------------|
| `InventoryServiceTests`              | Unique/Stack adds, idempotency, GetByCategory, partial/full removes, batch persistence, roundtrip.  |
| `InventoryUseRouterTests`            | Handler dispatch, ConsumeAfterUse path, missing-item / missing-handler error messages.               |
| `LegacyOwnedBooksMigrationHookTests` | One-time migration of legacy `day_progress.OwnedBookIds` into inventory (no-op after first run).     |

To run them in the Editor: **Window → General → Test Runner → EditMode → Run All** with
`Game.Inventory.Tests.Editor` selected.

---

## Known limitations & deferred work

| Item                                                 | Resolution                                                                                          |
|------------------------------------------------------|-----------------------------------------------------------------------------------------------------|
| Production inventory window                          | Debug UI only; production view lands with the UI System module.                                     |
| Real decor activation (`SalesSessionSetup.DecorIds`) | Stays empty until a real decor feature ships. Inventory holds the owned decor today.                |
| Puzzle config (pieces required, reward)              | Handler uses `FullPiecesStub = 8`. Replace with config when the puzzle feature ships.                |
| "New item" badges                                    | `Changed` event is the seam; the badge service is out of scope for MVP.                              |
| Feature-gated visibility (RPG `IOverrideItemsService`)| Out of scope. All inventory items are visible in MVP.                                                |
| Server-authoritative writes                          | Out of scope. `IInventoryRepository` is the seam; `HttpInventoryRepository` is a follow-up.          |
| Per-item max caps                                    | Out of scope. Add when balancing requires it.                                                       |
| Per-item catalog (`IInventoryItemConfig`)            | Out of scope. Books use `BookConfig`; decor will get `DecorConfig` when decor lands.                 |

---

## File map

```
Assets/Game/Features/Inventory/
├── Game.Inventory.asmdef               # impl (refs UniTask, VContainer, Save, Configs, Newtonsoft, ...)
├── API/
│   ├── Game.Inventory.API.asmdef       # contracts only (UniTask)
│   ├── Constants/  InventoryCategories.cs · InventorySaveKeys.cs
│   ├── Domain/     InventoryItem.cs · InventoryChangeEvent.cs · InventoryUseResult.cs
│   │               ItemCategory.cs · ItemStackingMode.cs · InventoryStateDto.cs
│   ├── IInventoryService.cs · IInventoryRepository.cs
│   ├── IItemCategoryRegistry.cs
│   └── IInventoryItemUseHandler.cs · IInventoryUseRouter.cs
├── Services/
│   ├── InventoryService.cs             # ISaveHook + sync read / async write
│   ├── SaveBackedInventoryRepository.cs
│   ├── ItemCategoryRegistry.cs
│   ├── InventoryUseRouter.cs
│   └── LegacyOwnedBooksMigrationHook.cs   # one-shot migration from day_progress.OwnedBookIds
├── UseHandlers/
│   ├── NoopDecorUseHandler.cs
│   └── PuzzleAssembleUseHandler.cs
├── UI/
│   ├── InventoryScreenView.cs           # debug uGUI, tabbed
│   └── InventoryItemRowView.cs
└── Tests/Editor/
    ├── Game.Inventory.Tests.Editor.asmdef
    ├── Fakes/  FakeInventoryRepository.cs · FakeSaveService.cs
    ├── InventoryServiceTests.cs
    ├── InventoryUseRouterTests.cs
    └── LegacyOwnedBooksMigrationHookTests.cs
```

DI: `Assets/Game/Core/Installers/Features/InventoryVContainerBindings.cs` (registered
in `BootstrapInstaller`, global scope).
