# Система инвентаря — полная архитектура

---

## Структура системы

Инвентарь делится на два принципиально разных типа хранимых объектов:

| Тип | Что это | Сервис | Хранилище |
|---|---|---|---|
| **StorageItems** | Ресурсы, шарды, фрагменты карт, еда и прочие стакуемые предметы | `IResourcesService` | `ResourcesEntry` |
| **Gear** | Снаряжение (оружие, броня) — уникальные предметы с уровнем и статами | `IGearsService` | `GearsEntry` |

---

## Типы предметов — `ItemType`

`Assets/Shared/CoreApi.Pure/Items/ItemType.cs`

```
Unknown = 0
Weapon = 1            — снаряжение (оружие)
Armor = 2             — снаряжение (броня)
Hero = 3
Resource = 4          — базовый ресурс (монеты, энергия, токены)
TreasureMapPiece = 5  — фрагмент карты сокровищ
Building = 6
GearSummon = 7        — призыв снаряжения
QuestItem = 8
MaxLimiter = 9        — хранит максимальное значение ресурса
ReplenishLimiter = 10 — ограничение восстановления
AvatarAbility = 11
ReplenishRefill = 12  — быстрое восстановление
LootBox = 13
GearSet = 14
AscensionSpirit = 15  — дух вознесения
DecorStage = 16
AvatarPicture = 17
AvatarFrame = 18
Recipe = 19
ChatEmoji = 20
ShardsResource = 21   — шарды для ресурсных предметов
ShardsHero = 22       — шарды для героев
```

Расширения:
- `IsGear()` → `Weapon` или `Armor`
- `IsShard()` → `ShardsHero` или `ShardsResource`
- `IsStackable()` → Resource, TreasureMapPiece, AscensionSpirit, QuestItem, GearSummon, ShardsHero, ShardsResource и лимитеры

---

## StorageItems — стакуемые предметы

### `IResourcesService`
`Assets/Shared/CoreApi.Pure/Items/IResourcesService.cs`

Управляет всеми стакуемыми предметами.

**Основные операции:**

| Метод | Что делает |
|---|---|
| `GetAmount(id)` | Количество предмета (с учётом восстановления и флага бесконечности) |
| `HasEnough(id, amount)` | Проверка наличия нужного количества |
| `GetByPrefix(prefix)` | Все предметы с указанным префиксом ConfigId (например, `"tokens/"`) |
| `Add(id, amount, track)` | Добавить предмет, публикует `ItemDropSignal` |
| `Remove(id, amount, track)` | Убрать предмет |
| `SetAmount(id, value, track)` | Установить точное значение |
| `SubscribeOnChanges(Action<ResourceChangeInfo>)` | Подписка на любое изменение любого ресурса |
| `SetIsUnlimited(id, true)` | Сделать ресурс бесконечным (не тратится) |

**Восстанавливаемые ресурсы** (например, энергия):

| Метод | Что делает |
|---|---|
| `HasReplenishLimit(id)` | Есть ли у ресурса лимит восстановления |
| `GetReplenishInterval(id)` | Интервал восстановления в секундах |
| `GetReplenishedAmount(id)` | Сколько восстановилось с последнего обновления |
| `GetTimeUntilReplenishmentFilled(id)` | Время до полного восстановления |
| `IsEnabledToRefill(id)` | Можно ли восстановить (текущее < лимита) |
| `Refill(id, track)` | Немедленно восполнить до лимита |

**Структура хранилища:**
```
ResourcesEntry
└── ResourcesEntryData
    └── Dictionary<string, ResourceStorageItem>
        └── ResourceStorageItem
            ├── SecuredInt Amount         — текущее количество
            ├── SecuredInt Max            — максимум (int.MaxValue если нет ограничения)
            ├── bool IsUnlimited          — флаг "бесконечный"
            └── ReplenishData
                ├── SecuredInt Limit      — максимум для восстановления
                └── long LastChangedTime  — время последнего изменения (unix)
```

### `StorageItemConfig` / `ConfigResourceDbData`
`Assets/Core/Items/StorageItemConfig.cs`  
`Assets/Shared/CoreApi.Pure/StaticData/Data/Configs/ConfigResourceDbData.cs`

Статический конфиг предмета (не изменяется в рантайме):

| Поле | Описание |
|---|---|
| `ConfigId` / `Id` | Уникальный строковый идентификатор |
| `InventoryTag` | Строка-группа: определяет в какой вкладке инвентаря лежит предмет |
| `InventoryOrder` | Порядок внутри группы |
| `Rarity` | Редкость (влияет на сортировку) |
| `Title`, `Description` | Локализованные тексты |
| `Image` | Addressable-ссылка на иконку |
| `HasMax` | Есть ли потолок количества |
| `ItemTags` | Дополнительные флаги (enum flags) |
| `Category` | Категория для группировки |
| `Replenish.Interval` | Секунды между тиками восстановления |
| `Replenish.Amount` | Количество за один тик |
| `Replenish.RefillPrice` | Цена мгновенного восполнения |

---

## Gear — снаряжение

### `IGearsService`
`Assets/Shared/CoreApi.Pure/Gears/IGearsService.cs`

Управляет всеми экземплярами снаряжения игрока.

**Получение:**

| Метод | Что делает |
|---|---|
| `GetGears(GearPurpose)` | Все предметы категории (Weapon / Armor) |
| `GetGears(GearPlace)` | Все предметы конкретного слота (Weapon, Head, Neck…) |
| `GetAllGears()` | Всё снаряжение |
| `GetGear(id)` | Один предмет по уникальному ID экземпляра |
| `GetGearsAmount(configId)` | Количество предметов данного конфига |
| `HasAnyGears()` | Есть ли хоть что-то |

**Модификация:**

| Метод | Что делает |
|---|---|
| `AddGear(configId, level, track)` | Создать новый экземпляр, сгенерировать статы, опубликовать `GearDropSignal` |
| `RemoveGear(gear, track)` | Удалить, опубликовать `GearRemovedSignal` |

**Рекомендации:**

| Метод | Что делает |
|---|---|
| `GetBestUnusedGears(role?)` | Лучшие незанятые предметы для каждого слота |
| `GetBetterGearsForHero(hero)` | Предметы лучше, чем текущее снаряжение героя |
| `GetBestUnusedGearForHero(hero, place)` | Лучший незанятый предмет для конкретного слота героя |

**Структура хранилища:**
```
GearsEntry
└── GearsEntryData
    ├── Dictionary<string, GearStorageItem>   — ключ: уникальный ID экземпляра
    │   └── GearStorageItem
    │       ├── string ConfigId               — ссылка на конфиг
    │       ├── SecuredInt Level
    │       ├── SecuredInt Evolution          — уровень эволюции (улучшение качества)
    │       ├── SecuredInt GearExpAccumulated — накопленный опыт
    │       ├── StatModificationValue MainStat
    │       └── List<StatModificationValue> ExtraStats
    └── HashSet<string> Received              — конфиги, которые игрок уже получал хоть раз
```

### `IPlayerGear`
`Assets/Shared/CoreApi.Pure/Gears/IPlayerGear.cs`

Рантайм-обёртка над `GearStorageItem` + конфигом.

| Свойство | Тип | Описание |
|---|---|---|
| `Model` | `IGearModel` | Все характеристики предмета |
| `Id` | `string` | Уникальный ID экземпляра |
| `Purpose` | `GearPurpose` | `Weapon` или `Armor` |
| `IsWeapon` / `IsArmor` | `bool` | Удобные проверки |
| `IsEquipped` | `bool` | Надет ли на героя |
| `EquippedHero` | `IPlayerHero` | Кто носит, или `null` |
| `LevelMax` | `int` | Максимальный уровень (зависит от редкости) |
| `Power` | `int` | Суммарная мощь (сумма всех статов) |
| `MainModifier` | `StatModification` | Основной стат |
| `AcquiredModifiersCount` | `int` | Количество открытых доп. статов |
| `ExpFromFodder` | `int` | Опыт, если использовать как материал для прокачки |

### `IGearModel`
`Assets/Shared/CoreApi.Pure/Gears/IGearModel.cs`

| Поле | Описание |
|---|---|
| `ConfigId` | Ссылка на конфиг |
| `Place` | Слот: `Weapon`, `Head`, `Neck`, `Body`, `Hands`, `Legs` |
| `Rarity` | `Star_1`…`Star_5` |
| `Role` | Класс героя (подходит или нет) |
| `Level` | Текущий уровень |
| `Evolution` | Уровень эволюции |
| `GearExpAccumulated` | Накопленный опыт до следующего уровня |
| `MainStat` | Основной стат + значение |
| `ExtraStats` | Список доп. статов (открываются по мере прокачки) |
| `Power` | Суммарная боевая мощь (fixed-point) |

### Слоты и категории

```
GearPlace:  Weapon(0), Head(1), Neck(2), Body(3), Hands(4), Legs(5)
GearPurpose: Weapon(0) — только слот Weapon
             Armor(1)  — слоты Head, Neck, Body, Hands, Legs

GearRarity: Star_1(1) … Star_5(5)
```

---

## Надевание снаряжения — `IHeroGearService`
`Assets/Shared/CoreApi.Pure/Gears/IHeroGearService.cs`

| Метод | Что делает |
|---|---|
| `Equip(hero, gear)` | Надеть снаряжение на героя |
| `Unequip(hero, gear)` | Снять конкретный предмет |
| `Unequip(hero)` | Снять всё снаряжение с героя |

**Логика `Equip`:**
1. Получить текущий предмет в том же слоте у героя
2. Если `gear` уже надет на этого героя — выход
3. Снять текущий предмет (если есть)
4. Если `gear` надет на **другого** героя — обменять: второму надеть снятый предмет первого
5. Добавить `gear.Id` в `hero.StorageItem.Gears`
6. Вызвать `hero.RefreshModel()` — пересчитать мощь
7. Отправить аналитику `TrackEquipment`

При снятии публикует **`GearRemovedFromHeroSignal(hero, gear)`**.

Связь героя со снаряжением хранится в `HeroStorageItem.Gears` — `HashSet<string>` с ID экземпляров снаряжения.

---

## Прокачка снаряжения — `IGearImproveService`
`Assets/Shared/CoreApi.Pure/Gears/...` (реализация `GearImproveService`)

| Метод | Что делает |
|---|---|
| `CanUpgrade(gear)` | Можно ли прокачать (есть материалы и не макс уровень) |
| `GetAvailableFodderFor(gear)` | Список доступных материалов (другое снаряжение + GearExp ресурс) |
| `GetBestFodderForTargetLevel(gear, level)` | Оптимальный набор материалов для достижения уровня |
| `LevelUp(setup)` | Выполнить прокачку: потратить материалы, поднять уровень, опубликовать `GearExpChanged` |
| `CanLevelUp(setup)` | Хватает ли материалов для набора |

Таблица прокачки (`GearLevelingDbData`) хранит для каждой редкости:
- Стоимость по уровням (опыт, золото)
- Максимальный уровень
- Опыт, который даёт предмет при использовании как материала
- Стоимость эволюции

---

## Шарды — `IShardsService`
`Assets/Shared/CoreApi.Pure/Items/IShardsService.cs`

Шарды — особый вид ресурса, N штук которого собираются в целый предмет (героя или ресурс).

**Конфиг шарда (`ShardsConfigDbData`):**

| Поле | Описание |
|---|---|
| `OriginalItem` | ConfigId того, что собирается (`hero/artemis`, `resources/gold` и т.д.) |
| `FullShardsCount` | Сколько шардов нужно для одной сборки |
| `ShardResource` | `_generated/shards/{OriginalItem}` — ID ресурса шарда в `IResourcesService` |
| `IsHero` | `OriginalItem.StartsWith("hero/")` |
| `IsResourcesItem` | `OriginalItem.StartsWith("resources/")` |

**Операции:**

| Метод | Что делает |
|---|---|
| `HasEnoughShardsToBuild(itemId)` | Есть ли `>= FullShardsCount` шардов |
| `HasEnoughShardsToBuild()` | Хоть один шард можно собрать |
| `BuildShards(shardConfig)` | Собрать: потратить шарды, выдать награду |

**Результат сборки (`BuildingShardsResult`):**
- `Rewards` — список полученных предметов
- `UsedShardsCount` — сколько потратили
- `IsLimitReached` — достигнут лимит героев
- `IsSuccess`

После успешной сборки:
- Если герой и первый раз — открывается `NewHeroWindow`, затем `RewardsWindow`
- Если герой повторно или ресурс — сразу `RewardsWindow`

---

## Карты сокровищ — `ITreasureMapsService`
`Assets/Shared/CoreApi.Pure/TreasureMaps/ITreasureMapsService.cs`

**Конфиг карты (`TreasureMapDbData`):**

| Поле | Описание |
|---|---|
| `MapId` | Уникальный ID карты |
| `PieceId` | ConfigId ресурса-фрагмента |
| `PiecesCount` | Сколько всего фрагментов |
| `ExpeditionId` | К какой экспедиции относится |
| `MiniGamePrefab` / `FullMapPrefab` | Addressable-ссылки на мини-игру и полную карту |

**Операции:**

| Метод | Что делает |
|---|---|
| `PiecesTotalCount(mapId)` | Сколько всего фрагментов у карты |
| `PiecesFoundCount(mapId)` | Сколько нашёл игрок |
| `AreAllPiecesFound(mapId)` | Все найдены |
| `AreAllPiecesPlaced(mapId)` | Все размещены на картке |
| `PlacedPiecesIndexes(mapId)` | Индексы размещённых фрагментов |
| `SetPieceIsPlaced(mapId, index)` | Отметить фрагмент как размещённый |
| `IsTreasureFounded(mapId)` | Сокровище уже открыто |
| `CleanUpMaps(expeditionId)` | Удалить фрагменты завершённой экспедиции |

**Структура сохранения:**
```
TreasureMapsEntry
└── Dictionary<string, SavedTreasureMap>  — ключ: MapId
    └── SavedTreasureMap
        ├── bool IsTreasureFounded
        └── HashSet<int> PlacedPiecesIndexes
```

---

## Видимость предметов

### `IOverrideItemsService`
`Assets/Shared/CoreApi.Pure/Items/IOverrideItemsService.cs`

`CheckItemIsAvailable(itemId)` — проверяет условие `ItemAvailableForPlayerCondition` из конфига. Позволяет скрыть предмет из инвентаря по внешним условиям (фичи, прогресс, события). Результат кэшируется.

Если предмет недоступен — может быть заменён на `DEFAULT_OVERRIDE` или на конкретный override из `OverrideItemsDbData`.

### `ItemsVisibilityHelper`
`Assets/Game/UI/ItemsVisibilityHelper.cs`

Дополнительная UI-фильтрация поверх `IOverrideItemsService`:
- `ClanExp` — видим только если клан не на максимальном уровне
- Токены боссов (`tokens/` префикс) — видимы только если фича боссов включена
- Всё остальное — видимо по умолчанию

---

## Уведомления о новых предметах — `IReceivedItemsNotificationService`
`Assets/Shared/CoreApi.Pure/Storages/IReceivedItemsNotificationService.cs`

Отслеживает непросмотренные предметы. Подписывается на:
- `ItemDropSignal` — новый StorageItem
- `GearDropSignal` — новое снаряжение
- `HeroAddedSignal` — новый герой

При получении сигнала добавляет ID в `Dictionary<ItemType, HashSet<string>>` и `Dictionary<string, HashSet<string>>` (по `InventoryTag`). Публикует `ReceivedItemsChangedSignal`.

| Метод | Что делает |
|---|---|
| `HasNewItems()` | Есть ли хоть один новый предмет |
| `HasNewItems(ItemType)` | Есть ли новые предметы данного типа |
| `GetNewItemsByInventoryTag(tag)` | Список новых предметов в конкретной группе |
| `IsNew(itemId)` | Конкретный предмет новый? |
| `MarkAsNotNew(itemId)` | Пометить как просмотренный, публикует `ReceivedItemsChangedSignal` |

**Особый случай — фрагменты карт:** помечаются как просмотренные только если это первый найденный фрагмент карты. Остальные фрагменты той же карты остаются «новыми» до открытия окна инвентаря.

---

## Сигнальный поток

### Добавление предмета

```
Источник (лут, магазин, миссия)
  → ItemsService.Add(id, amount)
      → IRewardsRouter.Handle()
          ┌─ StorageItem → ResourcesService.ChangeAmount()
          │     → ItemDropSignal(id, amount, isNew)
          └─ Gear      → GearsService.AddGear()
                → GearDropSignal(configId, uniqueId)
  → ReceivedItemsNotificationService
      [подписан на ItemDropSignal / GearDropSignal]
      → добавляет в _newItemsByType, _newItemsByTag
      → ReceivedItemsChangedSignal
  → UI: обновляет бейджи "новый" на вкладках инвентаря
```

### Просмотр предмета в инвентаре

```
Клик по ячейке сетки
  → InventoryGridController.OnCellViewTouched(index)
  → InventoryWindowItemInfo.SetSelectedItemInfo(info)
      → info.MarkAsSeen()
          → ReceivedItemsNotificationService.MarkAsNotNew(id)
              → ReceivedItemsChangedSignal
      → WindowItemSelectedSignal<InventoryWindowItemInfo>
  → InventoryWindow.OnInventoryItemSelectedSignal()
  → RedrawSelectedItemInfoPanel() — показать нужную правую панель
```

### Надевание снаряжения

```
GearStatsPanel: нажата кнопка «Надеть» / «Заменить»
  → InventoryWindow.OnWearGear(gear) / OnGearReplaceClick(gear)
  → ShowSubWindowAsync(WearGearWindowProps.Create(gear))
  → WearGearWindow: игрок выбирает героя
  → HeroGearService.Equip(hero, gear)
      → Снять текущий предмет в слоте (если есть) → GearRemovedFromHeroSignal
      → Обмен если gear надет на другого героя → GearRemovedFromHeroSignal
      → hero.StorageItem.Gears обновлён
      → hero.RefreshModel() — пересчёт мощи
  → InventoryWindow.OnRemoveGearFromHeroSignal() → RedrawSelectedItemInfoPanel()
  → InventoryWindow.RefreshView() → RefreshGrid() + JumpToSelectedCell()
```

### Прокачка снаряжения

```
GearStatsPanel: нажата кнопка «Улучшить»
  → InventoryWindow.OnGearLevelUpClick(gear)
  → ShowSubWindowAsync(GearImproveWindowProps.Create())
  → GearImproveWindow: игрок выбирает материалы и подтверждает
  → IGearImproveService.LevelUp(setup)
      → ResourcesService.Remove(GearExpConfigId, amount)
      → GearsService.RemoveGear() для каждого материала-геара
      → PlayerGear.SetLevel(targetLevel, expLeft)
          → GearExpChanged(gear)
  → InventoryWindow.OnGearExpChangedSignal() → InitItems()
```

### Сборка шардов

```
ItemShardsStatsPanel / HeroShardsStatsPanel: нажата кнопка «Собрать»
  → BuildShardsOperation.Execute()
  → IShardsService.BuildShards(shardsConfig)
      → ResourcesService.Remove(ShardResource, FullShardsCount * count)
      → ItemsService.Add(OriginalItem, count)      — выдать героя/ресурс
  → Если герой и первый раз → NewHeroWindow → RewardsWindow
  → Если ресурс или повтор → RewardsWindow
  → UpdateData() — обновить панель шардов
```

---

## Слой инвентарного окна

### Открытие окна — `InventoryWindowProps`
`Assets/Game/UI/Windows/Props/InventoryWindowProps.cs`

Единственная точка входа для открытия инвентаря:

```csharp
InventoryWindowProps.Create(
    inventoryTab: InventoryTab.Weapons,  // стартовая вкладка
    entryPoint:   "hero_screen",         // для аналитики
    itemToShow:   "weapon/sword_01",     // предмет для авто-выбора (опционально)
    onClose:      () => { ... }          // колбэк при закрытии (опционально)
)
```

При наличии `itemToShow` — окно само определяет нужную вкладку через `ItemsService.GetItemType()` и `StorageItemConfig.InventoryTag`.

### Вкладки — `InventoryTab` + `InventoryTabInfo`

```
InventoryTab enum:
  Weapons(0)   IsGear = true   → GearPurpose.Weapon
  Armors(1)    IsGear = true   → GearPurpose.Armor
  Materials(2) StorageItems с тегами вкладки
  Food(3)      StorageItems с тегами вкладки
  Rest(4)      StorageItems с тегами вкладки
  Shards(5)    StorageItems с тегами вкладки
```

`InventoryTabInfo` (MonoBehaviour на каждой вкладке):
- Видимость: все `_features` должны быть доступны через `IFeaturesService`
- Доступность для взаимодействия: дополнительно для Armors — `HudService.IsGearEnabled` (недоступно в середине туториала)
- Недоступная вкладка показывает лейбл «Скоро»

### Загрузка предметов в `InventoryWindow`

**Gear-вкладки (Weapons / Armors):**
```
GearsService.GetGears(GearPurpose)
  → список IPlayerGear
  → InventoryWindowItemInfo.Get(gear) для каждого
  → GearFilterAndSortingController.GetFiltered()  — применить фильтр + сортировку
  → добавить пустые слоты до кратного числа строк
  → InventoryGridController.SetData(items)
```

**StorageItem-вкладки:**
```
ResourcesService.GetByPrefix(prefix) для каждого prefix из InventoryTabInfo._itemsPrefixes
  → фильтр: amount > 0 && ItemsVisibilityHelper.IsVisible(id)
  → фильтр: StorageItemConfig.InventoryTag входит в список тегов вкладки
  → InventoryWindowItemInfo.Get(id)
  → AbstractWindowItemInfo.OrderStorageItems(items, tags)
  → добавить пустые слоты
  → InventoryGridController.SetData(items)
```

**Реакция на изменение `IResourcesService`** (пока окно видимо):
- Если количество предмета стало `> 0` и он не был в списке → `InitItems()` (перестроить список)
- Если количество стало `0` и предмет выбран → `InitItems()` (убрать и выбрать первый)
- Если количество изменилось, но предмет уже в списке → `RefreshItems()` (только перерисовать)

### Правые панели статистики

| Панель | Условие показа | Действия |
|---|---|---|
| `GearStatsPanel` | `ItemType.Weapon` или `Armor` | Надеть / Улучшить / Заменить |
| `StorageItemStatsPanel` | Всё остальное | Использовать (если есть операция) |
| `ItemShardsStatsPanel` | `ItemType.ShardsResource` | Показать прогресс + кнопка сборки |
| `HeroShardsStatsPanel` | `ItemType.ShardsHero` | Как `ItemShardsStatsPanel` + фракция + звёзды |
| `TreasureMapInfoPanel` | `ItemType.TreasureMapPiece` | Перейти к экспедиции / Собрать карту |

### Кэш предметов — `AbstractWindowItemInfo<T>`

Все `InventoryWindowItemInfo` хранятся в статических словарях и переиспользуются:
- `Dictionary<ConfigId, TItemInfo>` — для StorageItems
- `Dictionary<IPlayerGear, TItemInfo>` — для снаряжения

Статически хранится `SelectedItemInfo` — текущий выбранный предмет. Смена выбора:
1. Пометить предыдущий как просмотренный
2. Установить новый `SelectedItemInfo`
3. Опубликовать `WindowItemSelectedSignal`

---

## Сохранение между сессиями

| Что | Где хранится | Как синхронизируется |
|---|---|---|
| Количество ресурсов | `ResourcesEntry → ResourcesEntryData` | `ResourcesService` вызывает `MarkAsChanged()` при каждом изменении |
| Снаряжение (статы, уровень) | `GearsEntry → GearsEntryData` | `GearImproveServiceClientLogic` отправляет `LevelUpGearRequest` на сервер |
| Какой гир надет на героя | `HeroStorageItem.Gears (HashSet<string>)` | Через `HeroesEntry` при каждом `SetChanged()` |
| Прогресс карт сокровищ | `TreasureMapsEntry` | По факту размещения фрагментов |
| Просмотренные предметы | в памяти (не персистится между сессиями) | Загружается из сигналов при старте |

Данные защищены `SecuredInt` (обфускация в памяти против читов). Сериализация через MessagePack.

---

## Фильтрация снаряжения — `GearFilterAndSortingController`
`Assets/Game/UI/Components/Filters/Controllers/GearFilterAndSortingController.cs`

Активен только для Gear-вкладок. Хранит отдельные настройки для `GearPurpose.Weapon` и `GearPurpose.Armor` — при переключении вкладок настройки сохраняются.

`GetFiltered()`:
1. Применить `ComplexFilter<IPlayerGear>` (фильтры по статам, редкости, слоту и т.д.)
2. Применить `ComplexSorting` (сортировка по уровню, редкости, мощи и т.д.)
3. Вернуть отфильтрованный список → в `RefreshGrid()`

---

## Жизненный цикл окна инвентаря

```
InventoryWindowProps.Create(tab, entryPoint, item?, onClose?)
  → UIService.ShowWindow(props)

OnPropsApplied():
  → HideAllStatsPanels()
  → SetupTabsAndGrids()         — собрать вкладки, скрыть недоступные
  → GetTabToOpen(ItemToShow)    — определить стартовую вкладку
  → SelectTab(tab, itemToShow)  — загрузить предметы

BeforeShow():
  → RedrawSelectedItemInfoPanel()
  → Подписка: _gearStatsPanel.OnWearClick/LevelUp/Replace
  → Подписка: ResourcesService.SubscribeOnChanges
  → Подписка: сигналы WindowItemSelected, GearRemovedFromHero,
              ReceivedItemsChanged, GearExpChanged

[Игрок взаимодействует]
  Клик по вкладке → MarkItemsInTabAsSeen() → SelectTab() → InitItems()
  Клик по ячейке → SetSelectedItemInfo() → RedrawSelectedItemInfoPanel()
  Надеть         → ShowSubWindow(WearGearWindowProps) → RefreshView()
  Улучшить       → ShowSubWindow(GearImproveWindowProps) → RedrawPanel()
  Собрать шарды  → BuildShardsOperation → RewardsWindow
  Карта сокровищ → ShowTreasureMapWindow

BeforeHide():
  → Отписка от всех событий
  → _filterAndSortingPanel.Clear()

OnClosed():
  → MarkItemsInTabAsSeen()      — всё открытое помечается просмотренным
  → Props.OnClose?.Invoke()
  → HeroInfoSelectionModel.Reset()
```
