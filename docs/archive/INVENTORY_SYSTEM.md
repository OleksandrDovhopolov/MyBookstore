# Inventory System

**Source project:** `C:\Projects\Research`
**Feature path:** `Assets\Game\Features\Inventory\Runtime\`

---

## Overview

Server-authoritative inventory system. The client can only **remove** items — add operations are performed exclusively by the server. Items are grouped by categories and support pluggable use-handlers (e.g. card packs). State is persisted via SaveService and kept in memory via an ECS-inspired world.

---

## Architecture Layers

```
API (Interfaces + Models)
  └─ Implementation
        ├─ Core (ECS-style in-memory world)
        ├─ Services (InventoryModuleService, ServerApi, Storage)
        ├─ UI (Presenter, Controller, Views, Widgets)
        └─ Categories / ItemUseHandlers
```

---

## Key Interfaces

| Interface | Responsibility |
|---|---|
| `IInventoryService` | Add / Remove items, change events |
| `IInventoryReadService` | Query items by owner + category |
| `IInventoryItemUseService` | Consume item (validate → remove → handler) |
| `IInventorySnapshotService` | Apply server snapshot to local world |
| `IInventoryServerApi` | HTTP: remove / remove-batch |
| `IInventoryStorage` | Load / Save persistent state |
| `IItemCategoryRegistry` | Register / lookup item categories |
| `IInventoryItemUseHandler` | Handle specific item consumption |
| `IInventoryUseHandlerStorage` | Register / remove use handlers |

### IInventoryService

```csharp
public interface IInventoryService
{
    Observable<InventoryChangedEvent> OnInventoryChanged { get; }
    UniTask AddItemAsync(InventoryItemDelta itemDelta, CancellationToken ct = default);
    UniTask RemoveItemAsync(InventoryItemDelta itemDelta, CancellationToken ct = default);
    UniTask<InventoryBatchRemoveResult> RemoveItemsAsync(
        IReadOnlyList<InventoryItemDelta> itemDeltas,
        CancellationToken ct = default);
}
```

> `AddItemAsync` throws `NotSupportedException` — items are added only via server snapshots.

---

## Data Models

### InventoryItemView *(read-only)*
```csharp
public readonly struct InventoryItemView
{
    public string OwnerId    { get; }
    public string ItemId     { get; }
    public int    StackCount { get; }
    public string CategoryId { get; }
}
```

### InventoryItemDelta *(mutation)*
```csharp
public readonly struct InventoryItemDelta
{
    public string OwnerId    { get; }
    public string ItemId     { get; }
    public int    Amount     { get; }
    public string CategoryId { get; }
}
```

### InventoryChangedEvent
```csharp
public readonly struct InventoryChangedEvent
{
    public string OwnerId { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<InventoryItemView>> ItemsByCategory { get; }
    public DateTime ChangedAtUtc { get; }
}
```

### InventoryBatchRemoveResult
```csharp
public readonly struct InventoryBatchRemoveResult
{
    public int RequestedStacks { get; }
    public int RemovedStacks   { get; }
    public IReadOnlyList<InventoryItemDelta> FailedItems { get; }
}
```

---

## Core Implementation

### InventoryModuleService

**File:** `Runtime\Implementation\Services\InventoryModuleService.cs`

Implements: `IInventoryService`, `IInventoryReadService`, `IInventoryItemUseService`,
`IInventoryUseHandlerStorage`, `IInventorySnapshotService`, `IDisposable`

Key behaviour:
- Semaphore-based per-owner thread-safe initialization
- Events published via R3 `Observable`
- Category ID mismatch detection on snapshot apply
- `RemoveReason = "inventory_consume"`, `RemoveBatchReason = "inventory_remove_batch"`

### ECS Core (Internal)

| Class | Role |
|---|---|
| `InventoryWorld` | In-memory entity store (owner / item / stack / category) |
| `InventoryStackKey` | Composite key `(OwnerId, ItemId, CategoryId)` |
| `InventoryQuerySystem` | Query wrapper over `InventoryWorld` |
| `AddItemSystem` | Thin wrapper: `InventoryWorld.AddOrStack()` |
| `RemoveItemSystem` | Thin wrapper: `InventoryWorld.Remove()` |
| `ItemDataComponent` | `{ ItemId, CategoryId }` |
| `OwnerComponent` | `{ OwnerId }` |
| `StackComponent` | `{ Count }` |

---

## Server Contracts

### HTTP Endpoints
| Operation | Path |
|---|---|
| Single remove | `POST inventory/remove` |
| Batch remove | `POST inventory/remove-batch` |

### Request / Response
```csharp
// Single
RemoveInventoryItemCommand  { PlayerId, ItemId, Amount, Reason }

// Batch
RemoveInventoryBatchCommand { PlayerId, Items: [{ItemId, Amount}], Reason }

// Response
InventoryOperationResponse  { Success, ErrorCode, ErrorMessage, PlayerState }
PlayerStateSnapshotResponseDto { Resources, InventoryItems: [{ItemId, Amount}] }
```

---

## Persistence (Save Module)

**Module key:** `"inventory"` inside `GameSaveData`

```csharp
InventoryModuleSaveData
  └─ Owners: List<InventoryOwnerSaveData>
        └─ Items: List<InventoryItemSaveData>
              { OwnerId, ItemId, StackCount, CategoryId }
```

---

## Categories

| ID | Class | Metadata type |
|---|---|---|
| `"regular"` | `SimpleItemCategory` | `ResourceWidgetMetadata` (view-only) |
| `"card_pack"` | `CardsItemCategory` | `ActionWidgetMetadata` (button) |

Built-in IDs:
```csharp
InventoryBuiltInCategoryIds.Regular   = "regular"
InventoryBuiltInCategoryIds.CardPack  = "card_pack"
```

---

## Item Use Flow

```
User clicks item
  → InventoryWindowController.OnInventoryButtonClickedHandler()
  → IInventoryItemUseService.ConsumeItemAsync(delta)
      → find IInventoryItemUseHandler by CategoryId
      → validate stock
      → RemoveItemAsync() (HTTP → server snapshot applied)
      → handler.UseAsync()   // e.g. open card pack
```

### CardPackInventoryUseHandler

```csharp
public bool CanHandle(InventoryItemDelta item)
    => item.CategoryId == CardCollectionGeneralConfig.CardPack

public UniTask UseAsync(InventoryItemDelta item, string ownerId, CancellationToken ct)
    => _openPackFlowService.OpenPackById(item.ItemId, ct)
```

---

## UI Stack

```
InventoryWindowController   — tab switching, filtering, item click handling
  └─ InventoryWindowView    — pool-based view recycling, Addressable sprite loading
        └─ InventoryView    — single item cell (stack count, click event)

InventoryTabsPresenter      — initializes per-category ViewModels, subscribes to changes
  └─ InventoryCategoryTabViewModel  — ReactiveProperty<IReadOnlyList<InventoryItemUiModel>>
```

### InventoryItemUiModel
```csharp
public readonly struct InventoryItemUiModel
{
    public string       ItemId     { get; }
    public string       Title      { get; }
    public int          StackCount { get; }
    public ItemCategory Category   { get; }
    public string       Subtitle   { get; }
    public Sprite       Icon       { get; }  // resolved from RewardSpec
}
```

Content widgets:
- `InventoryWidgetView` — `ActionWidgetMetadata` (button, e.g. open pack)
- `InventoryResourceWidgetView` — `ResourceWidgetMetadata` (display-only)

---

## Integrations

| System | Integration point |
|---|---|
| **Rewards** | `InventoryRewardHandler` handles `RewardKind.InventoryItem` |
| **Player state snapshot** | `InventoryPlayerStateSnapshotHandler` applies server state |
| **Card collection** | `CardCollectionInventoryIntegration` attaches category + use handler |

---

## DI Registration

```csharp
// InventoryVContainerBindings.cs
builder.Register<IInventoryServerApi, InventoryServerApi>(Singleton);

builder.Register<IItemCategoryRegistry>(_ => {
    var r = new ItemCategoryRegistry();
    r.Register(new SimpleItemCategory());
    return r;
}, Singleton);

builder.Register<InventoryModuleService>(Singleton)
    .As<IInventoryService>()
    .As<IInventoryReadService>()
    .As<IInventoryItemUseService>()
    .As<IInventorySnapshotService>()
    .As<IInventoryUseHandlerStorage>()
    .As<IDisposable>();
```

---

## Assembly Definitions

| Assembly | Namespace | Key deps |
|---|---|---|
| `Inventory.API` | `Inventory.API` | R3, UniTask |
| `Inventory.Implementation` | `Inventory.Implementation` | Inventory.API, VContainer, UIShared, Infrastructure, Rewards |
| `Inventory.Tests.Editor` | — | Inventory.API, Inventory.Implementation |

---

## File Index

```
Assets\Game\Features\Inventory\Runtime\
├─ API\
│   ├─ IInventoryService.cs
│   ├─ IInventoryReadService.cs
│   ├─ IInventoryStorage.cs
│   ├─ IInventoryItemUseService.cs
│   ├─ IInventorySnapshotService.cs
│   ├─ IInventoryServerApi.cs
│   ├─ InventoryItemView.cs
│   ├─ InventoryItemDelta.cs
│   ├─ InventoryChangedEvent.cs
│   ├─ InventoryBatchRemoveResult.cs
│   ├─ InventoryItemCategory.cs
│   ├─ InventoryServerContracts.cs
│   ├─ Categories\IItemCategoryRegistry.cs
│   └─ ItemUse\
│       ├─ IInventoryItemUseHandler.cs
│       └─ IInventoryUseHandlerStorage.cs
└─ Implementation\
    ├─ InventoryVContainerBindings.cs
    ├─ ItemCategoryRegistry.cs
    ├─ Categories\SimpleItemCategory.cs
    ├─ Core\
    │   ├─ InventoryWorld.cs
    │   ├─ InventoryQuerySystem.cs
    │   ├─ InventoryComponents.cs
    │   ├─ InventoryStackKey.cs
    │   ├─ AddItemSystem.cs
    │   └─ RemoveItemSystem.cs
    ├─ Services\
    │   ├─ InventoryModuleService.cs
    │   ├─ InventoryServerApi.cs
    │   └─ InMemoryInventoryStorage.cs
    └─ UI\
        ├─ InventoryItemUiModel.cs
        ├─ InventoryCategorizedItemUiModel.cs
        ├─ InventoryCategoryTabViewModel.cs
        ├─ InventoryTabsPresenter.cs
        ├─ WIndow\
        │   ├─ InventoryWindowController.cs
        │   ├─ InventoryWindowView.cs
        │   └─ InventoryView.cs
        ├─ ContentWidget\
        │   ├─ InventoryWidgetView.cs
        │   ├─ InventoryResourceWidgetView.cs
        │   ├─ InventoryWidgetData.cs
        │   └─ InventoryResourceWidgetData.cs
        └─ Debug\CheatInventoryButton.cs

Assets\Game\Features\Rewards\
    ├─ InventoryRewardHandler.cs
    └─ InventoryPlayerStateSnapshotHandler.cs

Assets\Game\Features\CardCollection\...\
    ├─ CardPackInventoryUseHandler.cs
    └─ CardCollectionInventoryIntegration.cs

Assets\Game\Core\Models\GameSaveData.cs
```
