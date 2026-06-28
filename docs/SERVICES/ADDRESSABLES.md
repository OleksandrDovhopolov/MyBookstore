# Addressables System

Описание того, как в проекте используется Unity Addressables через обёртку `ProdAddressablesWrapper`.

---

## Архитектура

Весь доступ к Addressables идёт через единую статическую точку входа — `ProdAddressablesWrapper` (`Assets/Game/Core/Infrastructure/Scripts/ProdAddressablesWrapper.cs`). Прямые вызовы `Addressables.LoadAssetAsync` в продакшн-коде отсутствуют.

```
Потребители (UI, Features, TileEditor)
        ↓
ProdAddressablesWrapper   ← единственный слой работы с Addressables API
        ↓
UnityEngine.Addressables
```

---

## ProdAddressablesWrapper

Статический класс в namespace `Infrastructure`. Реализует:

- **Ref-counted кэш хэндлов** — повторный запрос того же адреса возвращает уже открытый `AsyncOperationHandle`, увеличивая счётчик ссылок. Релиз вызывается только при обнулении счётчика.
- **Маппинг instance-id → ключ кэша** — позволяет вызывать `Release(UnityObject)` без знания адреса.
- **Thread-safety** — все операции с кэшом защищены `lock(Lock)`.

### Публичный API

| Метод | Описание |
|---|---|
| `LoadSync<T>(address)` | Синхронная загрузка через `WaitForCompletion()`. Используется в Editor/TileEditor. |
| `LoadAsync<T>(address, ct)` | Асинхронная загрузка с поддержкой отмены (`CancellationToken`). Основной метод. |
| `LoadGroupAsync<T>(addresses, ct, maxConcurrency)` | Параллельная загрузка нескольких адресов с лимитом параллелизма (по умолчанию 8). |
| `DownloadDependenciesByLabelAsync(label, ct)` | Скачивает бандлы по Addressables-лейблу на диск (без загрузки в RAM). |
| `ResolveAddressesByLabelAsync<T>(label, ct)` | Получает список адресов ассетов типа `T` по лейблу через `LoadResourceLocationsAsync`. |
| `WarmupGroupByLabelAsync<T>(label, ct, takeCount, maxConcurrency)` | Резолвит адреса по лейблу, берёт первые `takeCount` и загружает их в RAM. Возвращает загруженные адреса для последующего релиза. |
| `Release(object)` | Релиз по объекту Unity (ищет ключ через instance-id). |
| `Release(address)` | Релиз всех хэндлов с данным адресом. |
| `ReleaseGroup(addresses)` | Релиз списка адресов (используется совместно с `WarmupGroupByLabelAsync`). |
| `ReleaseAll()` | Полный сброс — освобождает все хэндлы и очищает кэш. |

### Жизненный цикл хэндла

```
LoadAsync/LoadSync
  → AcquireHandle: если кэш hit — refCount++, иначе Addressables.LoadAssetAsync + refCount=1
  → CompleteLoad: записывает instanceId→key в InstanceIdToKey

Release
  → refCount--
  → если refCount == 0: удаляет из кэша, вызывает Addressables.Release(handle)
```

---

## Потребители

### BaseAddressablesProvider (CardCollection)

`Assets/Game/Features/CardCollection/CardsCollectionImpl/Scripts/Utils/BaseAddressablesProvider.cs`

Абстрактный базовый класс для провайдеров статических данных. Загружает ассет по адресу, вызывает `ParseAsset()` для парсинга, кэширует результат. При смене адреса — автоматически освобождает предыдущий.

```csharp
// Паттерн использования
var data = await provider.LoadAsync(address, ct);
// ...
provider.ClearCache(); // вызывает ProdAddressablesWrapper.Release
```

### WindowFactoryDI (UI)

`Assets/Game/UI/UIShared/Scripts/WindowFactoryDI/WindowFactoryDI.cs`

Фабрика окон через DI. Загружает prefab окна по адресу из `[Window]`-атрибута, затем инстанциирует его через VContainer. Хэндл **не освобождается** явно — предполагается, что окно живёт долго.

### AddressablesHudPrefabLoader (UI)

`Assets/Game/UI/UIShared/Scripts/Hud/Runtime/AddressablesHudPrefabLoader.cs`

Тонкая обёртка `IHudPrefabLoader` — загружает и освобождает prefabs для HUD-виджетов.

### RuntimeLocationObjectsFactory (Location)

`Assets/Game/Features/Location/Runtime/RuntimeLocationObjectsFactory.cs`

Фабрика объектов локации. Загружает prefab асинхронно по `objectId`, хранит ссылки в списке, освобождает все при `Dispose()`.

### TileEditorObjectsFactory (TileEditor)

`Assets/TileEditor/Runtime/Scripts/Init/Factory/TileEditorObjectsFactory.cs`

Editor-версия фабрики объектов. Использует **синхронную** загрузку (`LoadSync`) — допустимо только в контексте редактора, не в рантайме.

### FishingHudFacade / FishingLureSelectionHudFacade (Fishing)

`Assets/Game/Features/Fishing/Runtime/FishingHudFacade.cs`  
`Assets/Game/Features/Fishing/Runtime/FishingLureSelectionHudFacade.cs`

Оба фасада реализуют **prewarm-паттерн**:

1. При `Start()` — запускают `RunPrewarmAsync` через `SemaphoreSlim(1,1)` (только одна загрузка параллельно).
2. Загружают все спрайты лур по адресам из конфига и складывают в `_spritesByAddress`.
3. При `Dispose()` — вызывают `ReleaseGroup(_warmedAddresses)`.
4. Если `TryShowAsync` вызван до завершения prewarma — показывают info-сообщение и возвращают `false`.

### NewFishView (Fishing)

`Assets/Game/Features/Fishing/Runtime/Presentation/NewFishView.cs`

Загружает спрайт рыбы прямо из View по адресу `FishId`. Хэндл не освобождается явно — View живёт в пределах сцены.

### EventSpriteManager (CardCollection)

`Assets/Game/Features/CardCollection/CardsCollectionImpl/Scripts/Services/EventSpriteManager.cs`

Менеджер спрайтов событий. Поддерживает два режима загрузки:
- `BindSpriteAsync` — загружает спрайт напрямую по адресу.
- `BindSpriteFromAtlasAsync` — загружает `SpriteAtlas` и достаёт спрайт по имени (`atlas.GetSprite(name)`).

Хранит состояние per-event (кэш спрайтов, in-flight задачи, биндинги). При завершении события — с задержкой 1 с вызывает `ReleaseEvent`, который освобождает все адреса события.

### EventAssetWarmupService (EventOrchestration)

`Assets/Game/Features/EventOrchestration/Module/Core/EventAssetWarmupService.cs`

Сервис предзагрузки ассетов для запланированных событий. Работает на `Tick()`:
- За **7 сек** до старта события: `DownloadDependenciesByLabelAsync(eventId)` — скачивает бандлы на диск.
- За **3 сек** до старта: `WarmupGroupByLabelAsync<SpriteAtlas>(eventId, ct, MaxWarmupAtlasesCount=1)` — загружает атлас в RAM.
- При завершении события (`OnEventCompleted`): `ReleaseGroup(warmedAddresses)`.

### CardCollectionSession (CardCollection)

`Assets/Game/Features/CardCollection/CardsCollectionImpl/Scripts/Integration/CardCollectionSession.cs`

При старте сессии:
1. `DownloadDependenciesByLabelAsync("Spring_Collection")` — скачивает бандлы.
2. `WarmupGroupByLabelAsync<Sprite>("Spring_Collection", ct, 30)` — загружает 30 спрайтов.
3. При `Dispose()` — `ReleaseGroup(_sessionWarmedAddresses)`.

> **TODO в коде:** лейбл `"Spring_Collection"` захардкожен как debug-заглушка вместо динамического `scheduleItem.Id`. Комментарий: *"LIVEOPS bug. if there is no group in addressables - error. hotfix required"*.

---

## Паттерны использования

### Prewarm + Release group

```csharp
// Загрузить
var addresses = await ProdAddressablesWrapper.WarmupGroupByLabelAsync<Sprite>(label, ct, count);

// Использовать через обычный LoadAsync (ref-count увеличится)
// ...

// Освободить при dispose
ProdAddressablesWrapper.ReleaseGroup(addresses);
```

### Загрузка prefab с ручным Release

```csharp
var prefab = await ProdAddressablesWrapper.LoadAsync<GameObject>(key, ct);
_prefabRefs.Add(prefab);
// ...
// В Dispose:
foreach (var p in _prefabRefs) ProdAddressablesWrapper.Release(p);
```

### Загрузка спрайта из атласа

```csharp
var atlas = await ProdAddressablesWrapper.LoadAsync<SpriteAtlas>(atlasAddress, ct);
var sprite = atlas.GetSprite(spriteName);
// Освобождать нужно atlasAddress, не sprite
ProdAddressablesWrapper.Release(atlasAddress);
```

---

## Важные замечания

- **Ref-count**: один `Release` снимает одну ссылку. Если один адрес загружен несколько раз — нужно столько же релизов.
- **`LoadSync` только в Editor**: метод вызывает `WaitForCompletion()`, что блокирует главный поток. В `TileEditorObjectsFactory` это приемлемо; в рантайм-фичах использовать запрещено.
- **Лейблы = event ID**: события в `EventAssetWarmupService` используют `scheduleItem.Id` как Addressables-лейбл для группировки ассетов события.
- **Кэш не шарится между типами**: ключ кэша — пара `(address, Type)`, поэтому `LoadAsync<Sprite>` и `LoadAsync<SpriteAtlas>` по одному адресу — разные записи кэша.
