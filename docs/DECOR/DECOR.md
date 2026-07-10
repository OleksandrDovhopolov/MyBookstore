# Decor System — Design Doc

**Status:** In progress (Phase 0 planning)
**Related:** [ADR-0004](../adr/0004-stock-model-hybrid-sale-chance.md), [Inventory](../INVENTORY.md), [REFERENCE_COZY_BOOKSHOP_DAY](../archive/REFERENCE_COZY_BOOKSHOP_DAY.md), [Подготовка](Подготовка.md)
**Module:** new `Game.Decor` + `Game.Decor.API`

---

## 1. Назначение

Декор — основной **progression hook** магазина книг. Заимствуется из Tiny Bookshop: игрок ставит предметы в магазин, которые **меняют вероятности пассивных продаж** по жанрам. Это даёт цикл «оптимизируй полку и декор → больше продаж → больше денег на декор».

В референсе декор делает три вещи (см. [REFERENCE_COZY_BOOKSHOP_DAY.md](../archive/REFERENCE_COZY_BOOKSHOP_DAY.md) lines 93–108):
1. **Меняет шансы продаж** по жанрам/настроениям (houseplant → cozy/nature; globe → adventure/travel)
2. **Меняет поведение покупателей** (coffee pot продлевает время покупателя → больше пассивных проверок)
3. **Создаёт атмосферу** (отображается в newspaper review: «Today the shop smelled of rain and coffee»)

**Phase 0 берёт только (1)** — модификатор шансов. Поведенческие эффекты (2) и атмосфера (3) — будущие фазы.

---

## 1.5 Decor Gameplay Loop

**Phase 0 целевой loop (minimal viable):**
```
   ┌──► завершил день ──► получил gold ──► купил/получил декор (newspaper / Phase 1+ shop)
   │                                                    │
   │                                                    ▼
   └─────── следующий день ◄──── поставил в слот (DecorPlacementScreenView)
```

Игрок не **меняет** декор ежедневно — он **накапливает коллекцию**. Это согласуется с TB-стилем: «set it and forget it» до момента когда появятся новые слоты / стили / лучшие декорации. Loop работает за счёт **роста** числа декораций и слотов, а не их перестановки.

**Phase 2+ целевой loop (TB-style swap-под-ассортимент):**
```
1. Игрок получает новый набор книг (preparation)
2. Анализирует жанры в ассортименте
3. Свапает декор под доминирующие жанры
4. Запускает день
5. Получает продажи
6. На gold покупает новый декор / стили
7. Повторяет цикл
```

Этот loop требует:
- UI который показывает «сколько каких жанров на полке сейчас»
- Быстрый swap-режим (drag-drop, не текстовое меню)
- Достаточный пул декораций чтобы swap имел смысл (~15-20 как минимум)

Phase 0 не покрывает (3) и (6) полностью — игрок может, но необязан. Сценарий «поставил один раз и забыл» — **валидный** для MVP.

**Главный риск MVP:** если новых декораций добавляется мало и слотов всего 8, игрок реально оптимизирует один раз и не возвращается к системе. Митигируется через:
1. Newspaper offers (Phase 0 — один раз; Phase 3 — регулярные офферы каждые 2-3 дня)
2. Расширение слотов через прогрессию (Phase 2 — `LocationConfig` second location с 12-14 слотами)
3. Style set bonuses (Phase 3 — игрок хочет собрать 3 maritime для бонуса)

---

## 2. Phase 0 — что в скоупе

### Сделано / в реализации
- **Реальный `IDecorModifierProvider`** — заменит текущий `NoopDecorModifierProvider`
- **DecorConfig** — JSON через `IConfigsService` (как BookConfig)
- **8 слотов на лавке игрока** — data-driven через `BookShopConfig.DecorSlots` (слоты «едут» с лавкой между локациями; локация — это *где* лавка стоит сегодня, не *что* у неё)
- **Save module `decor.placement`** — персистентное «какой декор где стоит»
- **Инвентарь декораций** — через существующий `IInventoryService` (категория `decor` уже зарегистрирована)
- **UI заглушка** — `DecorPlacementScreenView` (debug uGUI, кнопки/текст; реальный визуал — Phase 2+)
- **Newspaper delivery** — кнопки «Receive free decor» + «Buy paid decor» в `ResultsScreenView` после Day 1
- **3-5 sample-декораций** в `decors.json`: Vintage Globe, Coffee Pot, Houseplant, Old Lamp, Maritime Painting

### Откладывается
- **Behavioral effects** (coffee pot extends customer stay, lamp boosts mood, etc.)
- **Стили / paintable / electric / activatable / distracting / daily upkeep** — поля в DecorConfig зарезервированы как data-only, **никакой логики**
- **Реальный визуал магазина** (тележка / лавка с точками привязки) — stub UI на текстах
- **Покупка у вендоров на локациях, временные продавцы, офферы**
- **Hard currency** — только soft gold
- **Style-based set bonuses** (maritime set, classic set)
- **Reputation gating / unlock-by-progress**

---

## 3. Концепции

### 3.1 Decor

**Объект данных** в `decors.json` через `IConfigsService`. Несёт:
- Геймплейный эффект (Phase 0: только `GenreMultipliers`)
- Ограничения размещения (position-type + size)
- Метаданные (название, иконка, стоимость покупки)
- **Reserved fields** — paintable/electric/style/activatable/distracting/upkeep (см. §6)

### 3.2 Slot

**Точка размещения на лавке**. Описана в `BookShopConfig.DecorSlots`:
- `Id` — стабильный идентификатор слота (например `cart_table_1`)
- `PositionType` — `Standing` / `Hanging` / `Wall`
- `MaxSize` — максимально допустимый размер декора (`Small`/`Medium`/`Large`)

Декор может встать в слот только если:
1. `decor.PositionType == slot.PositionType`
2. `decor.Size <= slot.MaxSize`

Phase 0: единственная лавка `main_bookshop` даёт **8 слотов**:
- 3 × `Standing` (`Medium` max) — столы, стенды
- 3 × `Hanging` (`Small` max) — подвесные предметы
- 2 × `Wall` (`Large` max) — настенные

### 3.3 Placement

**Состояние** «какой декор в каком слоте сейчас». Хранится в save module `decor.placement` как массив пар `(SlotId, DecorId)`.

Когда `DecorPlacementService.Place(decorId, slotId)`:
1. Проверка: декор есть в инвентаре, слот существует и подходит, в слоте нет другого декора (либо force replace)
2. Запись в state, persist
3. Событие `PlacementChanged`

Когда `Unplace(slotId)`:
1. Удалить пару из state, persist
2. Событие `PlacementChanged`

### 3.4 Active decor ids

«Активным» считается декор, **который сейчас стоит в слоте**. Список ids доступен через `IDecorPlacementService.GetActiveDecorIds()`. Этот список:
- Передаётся в `SalesSessionSetup.DecorIds` (через `PreparationSalesSetupProvider`)
- Читается `EconomyBasedSaleChanceCalculator` через `IDecorModifierProvider`

---

## 4. Модель данных

### 4.1 DecorConfig

```
[ConfigFile("decors")]
public sealed class DecorConfig : IConfig {
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string IconAddress { get; set; }       // Addressables ключ (опционально)

    // Placement constraints
    public DecorPositionType PositionType { get; set; }
    public DecorSize Size { get; set; }

    // Economy (Phase 0: используется для newspaper-покупки)
    public int BasePrice { get; set; }            // gold
    public DecorRarity Rarity { get; set; }       // common / uncommon / rare / epic
    public int VisitCostDelta { get; set; }       // знаковый сдвиг entry fee локации (gold), пока активен декор

    // Gameplay effect (Phase 0: только это поле имеет рантайм-эффект)
    public DecorGenreModifier[] GenreMultipliers { get; set; }

    // Reserved (Phase 0: data-only, см. §6)
    public string[] Styles { get; set; }          // classic / maritime / cozy / industrial / ...
    public string[] AtmosphereTags { get; set; }  // coffee / rain / wood / sea / ...
    public bool Paintable { get; set; }
    public bool Electric { get; set; }
    public bool Activatable { get; set; }
    public bool Distracting { get; set; }
    public int DailyUpkeepCost { get; set; }
}

public enum DecorRarity { Common, Uncommon, Rare, Epic }

public sealed class DecorGenreModifier {
    public string Genre { get; set; }             // "classic" / "crime" / ...
    public float Multiplier { get; set; }         // 1.5 = +50%, 0.7 = -30%
}

public enum DecorPositionType { Standing, Hanging, Wall }
public enum DecorSize { Small, Medium, Large }
```

### 4.2 BookShopConfig (новый конфиг)

Слоты декора живут на отдельной сущности — **лавке игрока**, а не на локации. Локация отвечает за спрос и условия разблокировки; лавка — за свой инвентарь слотов. Это позволяет одной и той же лавке (`main_bookshop`) перемещаться между разными локациями и сохранять расстановку декора.

```
[ConfigFile("bookshops")]
public sealed class BookShopConfig : IConfig {
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public DecorSlot[] DecorSlots { get; set; }
}

public sealed class DecorSlot {
    public string Id { get; set; }
    public DecorPositionType PositionType { get; set; }
    public DecorSize MaxSize { get; set; }
}
```

`LocationConfig` остаётся, но **без** `DecorSlots` — это поле было концептуально неверно прописано. См. §10 mini-decision про hardcoded `main_bookshop` в Phase 0.

### 4.3 Placement save model

Файл: `Assets/Game/Features/Decor/API/DecorPlacementState.cs`
```
public sealed class DecorPlacementState {
    public int Schema { get; set; } = 1;
    public List<DecorPlacementEntry> Placements { get; set; } = new();
    public bool FirstDayRewardClaimed { get; set; }    // см. §7.2
    public bool FirstDayPurchaseAvailable { get; set; } // взаимоисключимо: одноразовая покупка
}

public sealed class DecorPlacementEntry {
    public string SlotId { get; set; }
    public string DecorId { get; set; }
}
```

Save module key: `"decor.placement"`. Подключается через `ISaveHook.AfterLoadAsync` (паттерн как у Inventory / Resources).

**Orphan cleanup (обязательно в Phase 0):** в `AfterLoadAsync` сервис проходит по `Placements` и проверяет, что для каждого `DecorId` существует `DecorConfig` через `IConfigsService.IsExists`. Если декор удалён из контента (или переименован) → запись удаляется из state + persist + warning в логи. Это защита от поломки save при изменении конфигов.

---

## 4.5 Decor Economy Targets

Балансные цели для авторинга контента в Phase 0. Числа подтверждены геймдизайнером:

| Метрика | Значение | Комментарий |
|---|---|---|
| Day 1 income (без декора) | 60-90 gold | FtueBootstrapper стартовый seed = 60 gold |
| Cheap decor | 30-50 gold | Houseplant 30, Old Lamp 40, Vintage Globe 50 |
| Mid decor | 80-150 gold | Maritime Painting 80, будущий контент 100-150 |
| Rare decor | 200-400 gold | **Не появляется в Phase 0**, зарезервировано для Phase 1+ |
| Payback time | 3-5 дней | Декор окупается за неделю game-time |
| Day 1 newspaper paid offer | 50 gold | Coffee Pot — стартовая цена «достижимо но ощутимо» |

**Бюджет gold для игрока в Phase 0:**
- День 1: получает ~60-90 gold от продаж
- День 1 newspaper: бесплатный Vintage Globe + опция Coffee Pot за 50 gold
- День 2-3: накапливает gold, появляются возможности покупки (в Phase 0 — нет, в Phase 1+ — shop window)

**Soft cap бонусов** (см. §5.2.5) задаёт верхнюю границу окупаемости: 4 adventure-декорации не дадут больше чем 1 хорошая на adventure (clamp ×3.0), что предотвращает «один genre монобилд».

---

### 4.4 Inventory integration

Декор в инвентаре — **существующая** категория `InventoryCategories.Decor` (Unique). API:

```
_inventory.AddAsync(decorId, InventoryCategories.Decor, amount: 1, ct)  // купил / получил
_inventory.GetByCategory(InventoryCategories.Decor)                     // список владений
_inventory.Has(decorId)                                                 // проверка перед Place
_inventory.RemoveAsync(decorId, 1, ct)                                  // продажа декора (Phase 1+)
```

`NoopDecorUseHandler` заменяется (или дополняется) на `DecorActivationUseHandler`, который при `Use(decorId)`:
- Если есть свободный совместимый слот → `IDecorPlacementService.Place(decorId, firstCompatibleSlot)`
- Иначе → noop + событие «нужна placement-сцена»

В Phase 0 — оба пути будут актуальны: use-handler как fallback, основной флоу через `DecorPlacementScreenView`.

---

## 5. Сервисы и интеграция

### 5.1 IDecorPlacementService (новый, в Game.Decor.API)

```
public interface IDecorPlacementService {
    IReadOnlyList<DecorPlacementEntry> GetAllPlacements();
    string GetDecorInSlot(string slotId);                              // null если пусто
    IReadOnlyList<string> GetActiveDecorIds();                          // unique decor ids в слотах

    UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct);
    UniTask UnplaceAsync(string slotId, CancellationToken ct);
    UniTask ClearAllAsync(CancellationToken ct);

    event Action PlacementChanged;
}

public enum DecorPlacementResult {
    Success,
    DecorNotInInventory,
    SlotNotFound,
    PositionTypeMismatch,
    SizeMismatch,
    SlotOccupied
}
```

**Реализация `DecorPlacementService`** держит:
- Reference на `IInventoryService` (проверка ownership)
- Reference на `IConfigsService` (DecorConfig + LocationConfig для слотов)
- Reference на `ISaveService` (persist `decor.placement` модуль)
- Reference на `ICurrentLocationProvider` (стартово — захардкожено `loc_downtown`, см. §10 mini-decision)

Self-registers как `ISaveHook` для AfterLoadAsync.

### 5.2 ConfigBasedDecorModifierProvider (заменит NoopDecorModifierProvider)

```
public sealed class ConfigBasedDecorModifierProvider : IDecorModifierProvider {
    public ConfigBasedDecorModifierProvider(IConfigsService configs) { ... }

    public float GetGenreMultiplier(string genre, IReadOnlyList<string> activeDecorIds) {
        float result = 1f;
        foreach (var id in activeDecorIds) {
            var config = _configs.Get<DecorConfig>(id);
            if (config?.GenreMultipliers == null) continue;
            foreach (var mod in config.GenreMultipliers) {
                if (string.Equals(mod.Genre, genre, StringComparison.OrdinalIgnoreCase)) {
                    result *= mod.Multiplier;     // multiplicative composition
                    break;
                }
            }
        }
        return result;
    }
}
```

**Multiplicative composition rationale:** два декора по +50% дают ×2.25 (не +100%), отрицательные перемножаются с положительными корректно. Согласуется с TB-стилем и формулой ADR-0004.

### 5.2.5 Bonus stacking rules (soft cap)

Без ограничения 4 adventure-декорации перемножаются как `1.5 × 1.4 × 1.3 × 1.5 = ×4.1` — это даёт «один genre монобилд» который убивает балансные намерения. **Жёсткое правило для Phase 0:**

```
per-genre финальный мультипликатор клампится в [0.1, 3.0]
```

Реализация в `ConfigBasedDecorModifierProvider.GetGenreMultiplier`:
```csharp
result *= mod.Multiplier;
// ... после прохода всех декораций ...
return Mathf.Clamp(result, 0.1f, 3.0f);
```

Что это значит для игрока:
- **Максимум +200%** к шансу одного жанра — нет смысла собирать > 3 хороших декораций на один жанр (diminishing return)
- **Минимум −90%** — декор не убивает жанр насмерть, какие-то продажи всё равно проходят
- Сохраняется ощущение «каждая дополнительная декорация что-то даёт» при умеренной комбинации

**Композиция с `EconomyConfig.CapChance`:** soft cap применяется на уровне жанрового мультипликатора **до** финального clamp в `EconomyBasedSaleChanceCalculator` (`Math.Clamp(raw, 0, 1)`). Цепочка:
```
fCount × locMod × decorMod (soft-capped) → clamp [0,1] → roll
```

**Diminishing returns** (не Phase 0): если cap окажется слишком грубым, в Phase 1 заменим на `result = 1 + sum(mod-1) × diminishing_factor(N)` с убывающим коэффициентом. Пока — простой clamp.

**Тесты:** `ConfigBasedDecorModifierProviderTests` обязан покрывать:
- 1 декор → значение без клампа
- 4 декора на один жанр с произведением > 3.0 → клампится в 3.0
- 1 декор с множителем 0.05 → клампится в 0.1
- 2 декора (positive × negative) на разных жанрах → не интерферируют

### 5.3 SalesSessionSetup.DecorIds — новый источник

Сейчас `PreparationSalesSetupProvider` берёт DecorIds из `PreparationSessionState.SelectedDecorIds` (всегда пустой массив).

Замена:
```
// Old:
var decor = state.SelectedDecorIds?.ToArray() ?? Array.Empty<string>();

// New (Phase 0):
var decor = _decorPlacementService.GetActiveDecorIds().ToArray();
```

Через DI получаем `IDecorPlacementService` (новая зависимость в Preparation feature). `PreparationSessionState.SelectedDecorIds` остаётся в save схеме на случай если позже захотим per-day toggle, но **не используется** в Phase 0.

### 5.4 Newspaper / Results screen

После Day 1 (`day_progress.completedDays == 1` или флаг `FirstDayPurchaseAvailable` в save):
- `ResultsScreenView` показывает блок «Newspaper offers»:
  - Кнопка **«Receive free: <Vintage Globe>»** (один раз, флаг `FirstDayRewardClaimed`)
  - Кнопка **«Buy: <Coffee Pot> — 50 gold»** (один раз, флаг `FirstDayPurchaseAvailable`)
- Клик → `_decorRewardService.ClaimFreeDecorAsync(...)` или `_decorRewardService.BuyOfferedDecorAsync(...)`
  - Внутри: `_inventory.AddAsync(decorId, "decor", 1)` + (для платного) `_resources.RemoveAsync(Gold, 50, "decor_newspaper")`
  - Set флаг в `decor.placement` save → кнопка пропадает

Phase 0: реализация в обычных uGUI-кнопках `ResultsScreenView` (debug-стиль). Phase 2 — миграция в UISystem.

### 5.5 Связь с инвентарём (use handler)

`Assets/Game/Features/Inventory/UseHandlers/NoopDecorUseHandler.cs` — заменить на `DecorActivationUseHandler`:
- При `Use(decorId)`: если есть свободный compatible слот → автоплейс. Если все компатибл слоты заняты → noop + лог «open placement screen».
- `ConsumeAfterUse = false` (декор остаётся в инвентаре после use)

Регистрация в `Game.Decor` bindings, заменяет `NoopDecorUseHandler` в `InventoryUseRouter`.

---

## 6. Reserved data fields (Phase 0: data-only)

Все следующие поля **есть в `DecorConfig`** и в `decors.json`, но **не имеют рантайм-эффекта** в Phase 0. Цель — зафиксировать данные в контенте, чтобы потом не миграть.

| Поле | Phase 0 семантика | Будущая семантика |
|---|---|---|
| `Styles` | Массив строк, парсится но игнорируется | **Set bonuses**: `3 maritime → adventure +10%`; `5 maritime → adventure +20%`; `8 maritime → newspaper special review`. Аналогично classic / cozy / industrial. |
| `Rarity` | enum (Common/Uncommon/Rare/Epic), парсится | Drop tables, weighted offers, sorting в UI, reward newspaper-логика. Phase 0: ничего |
| `AtmosphereTags` | Массив строк, парсится | **Newspaper review**: «Today the shop smelled of `rain` and `coffee`» — генерируется из tag-union активных декораций. Phase 0: поле есть, текст не генерится. Также используется для achievement-целей в дальнейшем |
| `Paintable` | bool, чистый flag | Игрок может перекрасить декор (новый use case + UI палитра) |
| `Electric` | bool, чистый flag | Декор «тратит электричество» = daily upkeep + on/off toggle |
| `Activatable` | bool, чистый flag | Декор имеет toggle on/off в Preparation (decor может выключаться чтобы экономить upkeep) |
| `Distracting` | bool, чистый flag | **Semantics TBD** — нужна ссылка на TB. Возможно «отвлекает покупателя → меньше шанс перехода в активную минигру». Фиксируем поле. |
| `DailyUpkeepCost` | int, чистый flag | Списывается gold каждое утро при day rollover. Если не хватает gold → декор автоматически Disable'ится. |

Открытое предложение: если в Phase 1 решим что эти эффекты реальны — все они подключаются через extension hooks (`IDecorEffectApplier` chain), не trough core `DecorPlacementService`. Изоляция сложности.

---

## 7. UI surface (Phase 0)

### 7.1 DecorPlacementScreenView (новая)

`Assets/Game/Features/Decor/UI/DecorPlacementScreenView.cs` — debug uGUI MonoBehaviour. Структура (текст + кнопки):

```
┌──────────────────────────────────────────────────────────────────┐
│  Active Decor Effects                                            │
│  ─────────────────────                                           │
│  Adventure  ×1.9                                                 │
│  History    ×1.2                                                 │
│  Cozy       ×1.3                                                 │
│  Kids       ×0.7    ← RED                                        │
│                                                                  │
│  (cap reached on Adventure: x3.0)  ← если активен soft cap       │
└──────────────────────────────────────────────────────────────────┘

Decor Placement (location: loc_downtown)
=========================================
Slot cart_table_1     [Standing / Medium]  → Empty            [Place ▾]
Slot cart_table_2     [Standing / Medium]  → Vintage Globe    [Unplace]
                                              Effects: Adventure +50%, History +20%
Slot wall_left        [Wall / Large]       → Maritime Painting [Unplace]
                                              Effects: Adventure +40%, Kids −30%  ← RED on negative
...

Inventory (decor):
- Vintage Globe (placed)
  Effects: Adventure +50%, History +20%
  Style: classic, maritime
- Coffee Pot (in inventory, not placed)
  Effects: Cozy +30%
- Old Lamp (in inventory, not placed)
  Effects: Mystery +30%

[Clear all]   [Close]
```

**Active Decor Effects panel (вверху):** агрегированные мультипликаторы по жанрам через `IDecorModifierProvider.GetGenreMultiplier` для каждого жанра в проекте. Только non-1.0 значения. **Soft cap индикатор:** если для какого-то жанра raw произведение > 3.0 — отдельная строка «cap reached» под таблицей, выделена жёлтым.

**Per-decor effects на карточках:** каждая карточка декора (в slot list И в inventory list) показывает строки `Genre ±N%` для каждой записи `GenreMultipliers`. Положительные — зелёным/нейтральным, отрицательные (`multiplier < 1.0`) — **красным**.

**Negative-effect confirm:** при клике `[Place ▾]` → выбор декора с хотя бы одним отрицательным эффектом → перед вызовом `PlaceAsync` показывается **modal confirm** через `ConfirmDialog` UI System:
```
Title: «Place Maritime Painting?»
Body:  «This decor will REDUCE Kids sales by 30%. Continue?»
Buttons: [Place anyway] / [Cancel]
```
Использование уже существующего [`ConfirmDialog`](../UI_SYSTEM.md) (Popup, Additional) — `await uiManager.ShowAsync<ConfirmDialog>(args)` + `WaitForResultAsync<ConfirmDialogResult>()`.

`[Place ▾]` раскрывает inline-меню (debug-стиль, без dropdown) с совместимыми не-размещёнными декорами из инвентаря. Клик → проверка negative effects → confirm если нужен → `PlaceAsync`.

`[Unplace]` → `UnplaceAsync(slotId)` без confirm (откат всегда безопасен).

`[Clear all]` → confirm «Clear all decor placements?» → `ClearAllAsync`.

Активация view: пока нет UISystem-окна → активируется через debug-кнопку в `GameHudView` или прямо в сцене.

### 7.2 ResultsScreenView — расширение

После `_resultsScreenRoot.SetActive(true)` в `SalesScreenView`, `ResultsScreenView.OnEnable` проверяет:
- `_decorRewardService.HasFreeDecorAvailable()` → показать панель free claim
- `_decorRewardService.HasOfferedDecorAvailable()` → показать панель paid buy

Если ничего нет — панели скрыты, обычный recap.

---

## 8. Sample content (Phase 0 — `decors.json`)

```
[
  {
    "id": "vintage_globe",
    "displayName": "Vintage Globe",
    "iconAddress": "Decor/VintageGlobe",
    "positionType": "Standing",
    "size": "Small",
    "basePrice": 50,
    "genreMultipliers": [
      { "genre": "adventure", "multiplier": 1.5 },
      { "genre": "history",   "multiplier": 1.2 }
    ],
    "styles": ["classic", "maritime"],
    "paintable": false,
    "electric": false,
    "activatable": false,
    "distracting": false,
    "dailyUpkeepCost": 0
  },
  {
    "id": "coffee_pot",
    "displayName": "Coffee Pot",
    "iconAddress": "Decor/CoffeePot",
    "positionType": "Standing",
    "size": "Small",
    "basePrice": 50,
    "genreMultipliers": [
      { "genre": "cozy", "multiplier": 1.3 }
    ],
    "styles": ["cozy"],
    "paintable": false,
    "electric": true,
    "activatable": false,
    "distracting": false,
    "dailyUpkeepCost": 2
  },
  {
    "id": "houseplant",
    "displayName": "Houseplant",
    "iconAddress": "Decor/Houseplant",
    "positionType": "Standing",
    "size": "Small",
    "basePrice": 30,
    "genreMultipliers": [
      { "genre": "nature", "multiplier": 1.4 },
      { "genre": "cozy",   "multiplier": 1.2 }
    ],
    "styles": ["cozy", "industrial"],
    "paintable": false,
    "electric": false,
    "activatable": false,
    "distracting": false,
    "dailyUpkeepCost": 0
  },
  {
    "id": "old_lamp",
    "displayName": "Old Lamp",
    "iconAddress": "Decor/OldLamp",
    "positionType": "Hanging",
    "size": "Small",
    "basePrice": 40,
    "genreMultipliers": [
      { "genre": "mystery", "multiplier": 1.3 }
    ],
    "styles": ["classic"],
    "paintable": true,
    "electric": true,
    "activatable": false,
    "distracting": false,
    "dailyUpkeepCost": 1
  },
  {
    "id": "maritime_painting",
    "displayName": "Maritime Painting",
    "iconAddress": "Decor/MaritimePainting",
    "positionType": "Wall",
    "size": "Medium",
    "basePrice": 80,
    "genreMultipliers": [
      { "genre": "adventure", "multiplier": 1.4 },
      { "genre": "kids",      "multiplier": 0.7 }
    ],
    "styles": ["maritime"],
    "paintable": false,
    "electric": false,
    "activatable": false,
    "distracting": false,
    "dailyUpkeepCost": 0
  }
]
```

Первый free reward (Day 1): **`vintage_globe`**.
Первый paid offer (Day 1, 50 gold): **`coffee_pot`**.

---

## 9. Sample slots в BookShopConfig

Новый файл `Assets/Configs/bookshops.json`:

```
[
  {
    "id": "main_bookshop",
    "displayName": "Main cart",
    "decorSlots": [
      { "id": "cart_table_1", "positionType": "Standing", "maxSize": "Medium" },
      { "id": "cart_table_2", "positionType": "Standing", "maxSize": "Medium" },
      { "id": "cart_table_3", "positionType": "Standing", "maxSize": "Medium" },
      { "id": "hang_1",       "positionType": "Hanging",  "maxSize": "Small"  },
      { "id": "hang_2",       "positionType": "Hanging",  "maxSize": "Small"  },
      { "id": "hang_3",       "positionType": "Hanging",  "maxSize": "Small"  },
      { "id": "wall_left",    "positionType": "Wall",     "maxSize": "Large"  },
      { "id": "wall_right",   "positionType": "Wall",     "maxSize": "Large"  }
    ]
  }
]
```

---

## 10. Open mini-decisions (резолвим в реализации)

- **`IPlayerBookShopProvider`** — в Phase 0 захардкодить `"main_bookshop"` в `DecorPlacementService.HardcodedBookShopId`. Phase 2+ — нормальный сервис когда появится несколько форматов лавок. `ICurrentLocationProvider` отдельно — для другой задачи (где лавка стоит сегодня).
- **BookShop vs Location split** — слоты декора живут на `BookShopConfig`, не на `LocationConfig` (фикс концептуальной ошибки из ранней Phase 0). Save хранит только `(slotId, decorId)`, без shop/location id — slot id сохранены 1:1 (`cart_table_1` и т.д.) → существующие save'ы не ломаются.
- **Multiplier composition при пересекающихся жанрах разных декораций** — multiplicative (Vintage Globe ×1.5 + другой adventure-декор ×1.3 = ×1.95). Альтернатива — сумма приращений. Выбор: multiplicative, см. §5.2.
- **Use-handler vs placement screen** — оба пути активны. Use-handler автоплейсит в первый совместимый, placement-screen даёт явный выбор. Чё-нибудь приоритетнее? Предложение: placement-screen — основной флоу, use-handler — лог + подсказка.
- **Slot icon / sprite** — Phase 0 нет визуала, только id строкой. Phase 2 — `slot.IconAnchor` (world position на лавке).
- **Stack limit на инвентарь декораций** — текущий Inventory `Unique` категория уже не позволяет дубликаты (HashSet). Хорошо, ничего не делаем.
- **«Очистить все слоты» в DecorPlacementScreenView** — кнопка `[Clear all]` есть в API (`ClearAllAsync`), пока выводим debug-кнопку для тестов.

---

## 11. Где живёт код

### Новый модуль `Game.Decor`

```
Assets/Game/Features/Decor/
├── Game.Decor.asmdef                  ← refs: VContainer, UniTask, Configs, Save, Game.Inventory.API, Game.Resources.API, Book.Sell.API, Game.Decor.API
├── API/
│   ├── Game.Decor.API.asmdef          ← refs: UniTask, Configs (для DecorPositionType/Size enums shared)
│   ├── IDecorPlacementService.cs
│   ├── DecorPlacementResult.cs
│   ├── DecorPlacementEntry.cs
│   ├── DecorPlacementState.cs         (save DTO)
│   ├── DecorPositionType.cs           (enum)
│   ├── DecorSize.cs                   (enum)
│   ├── DecorSaveKeys.cs               (const "decor.placement")
│   └── IDecorRewardService.cs         (newspaper delivery)
├── Services/
│   ├── DecorPlacementService.cs
│   ├── SaveBackedDecorPlacementStorage.cs
│   ├── ConfigBasedDecorModifierProvider.cs   ← заменит NoopDecorModifierProvider
│   ├── DecorActivationUseHandler.cs           ← заменит NoopDecorUseHandler
│   └── DecorRewardService.cs                  (newspaper offers)
├── UI/
│   └── DecorPlacementScreenView.cs            (debug uGUI)
└── (later) Editor/, Tests/Editor/
```

### Изменения в существующих файлах

```
Assets/Game/Features/Configs/Models/DecorConfig.cs                                 ← NEW
Assets/Game/Features/Configs/Models/DecorSlot.cs                                   ← NEW
Assets/Game/Features/Configs/Models/DecorPositionType.cs                            (or in .API)
Assets/Game/Features/Configs/Models/DecorSize.cs                                    (or in .API)
Assets/Game/Features/Configs/Models/BookShopConfig.cs                              ← NEW (owns DecorSlots)
Assets/Game/Features/Configs/Models/LocationConfig.cs                              ← unchanged (DecorSlots not here)

Assets/Game/Features/BookSell/API/IDecorModifierProvider.cs                        ← MOVED here from Services/
Assets/Game/Features/BookSell/Services/NoopDecorModifierProvider.cs                ← DELETED (replaced by ConfigBasedDecorModifierProvider)
Assets/Game/Features/BookSell/Book.Sell.asmdef                                     ← +Game.Decor.API (для использования IDecorPlacementService в PreparationSalesSetupProvider)
Assets/Game/Features/BookSell/Services/PreparationSalesSetupProvider.cs            ← заменить источник DecorIds на IDecorPlacementService

Assets/Game/Features/Inventory/UseHandlers/NoopDecorUseHandler.cs                  ← DELETED (replaced by DecorActivationUseHandler)

Assets/Game/Features/DayCycle/Results/UI/ResultsScreenView.cs                      ← +newspaper offers panel + bindings to IDecorRewardService

Assets/Configs/decors.json                                                          ← NEW (sample 5 items)
Assets/Configs/bookshops.json                                                       ← NEW (single entry main_bookshop, 8 slots)
Assets/Configs/locations.json                                                       ← unchanged (no decorSlots)

Assets/Game/Core/Installers/Features/DecorVContainerBindings.cs                    ← NEW
Assets/Game/Core/Installers/Features/BookSellVContainerBindings.cs                 ← replace NoopDecorModifierProvider registration
Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.cs                        ← +builder.RegisterDecor()
```

---

## 11.5 Config validation (обязательно в Phase 0)

Контент-ошибки — главный источник «тихих багов» в систем data-driven. Защищаемся через **отдельный сервис `DecorConfigValidator`** который запускается на bootstrap (через `IStartable` или `RegisterBuildCallback`) и проверяет:

### DecorConfig

| Правило | Действие при нарушении |
|---|---|
| `Id` непустой и уникальный в `decors.json` | Hard fail (LogError + throw в Editor) |
| `DisplayName` непустой | LogWarning |
| `PositionType` — valid enum | LogError + декор исключается из доступного списка |
| `Size` — valid enum | LogError + декор исключается |
| `BasePrice >= 0` | LogWarning, clamp в 0 |
| Для каждого `GenreMultipliers[i]`: `Multiplier > 0` | LogError + entry игнорируется (мультипликатор < 0 ломает clamp) |
| Для каждого `GenreMultipliers[i]`: `Genre` существует среди BookConfig.Genre (через scan `IConfigsService.GetAll<BookConfig>()`) | LogWarning (decor про несуществующий genre — мёртвый) |
| `Rarity` — valid enum | LogWarning, fallback Common |

### BookShopConfig.DecorSlots

| Правило | Действие |
|---|---|
| `Slot.Id` непустой и уникальный в рамках одной лавки | Hard fail |
| `PositionType` / `MaxSize` — valid enum | Hard fail |

### DecorPlacementState (runtime после load)

Проверка выполняется в `DecorPlacementService.AfterLoadAsync`:

| Правило | Действие |
|---|---|
| Каждый `Placement.DecorId` ссылается на существующий `DecorConfig` | Если orphan → удалить запись + LogWarning. Persist обновлённый state |
| Каждый `Placement.SlotId` ссылается на существующий `Slot` в текущем `BookShopConfig` | Если orphan → удалить запись + LogWarning |
| `PositionType` декора совпадает с `PositionType` слота | Несовместимость после миграции контента → удалить + LogWarning |

### DecorConfigValidator API

```
public sealed class DecorConfigValidator : IStartable
{
    public DecorConfigValidator(IConfigsService configs);
    public ValidationReport Validate();   // публичный для тестов
    public void Start();                   // вызывает Validate(), логирует, в Editor может throw
}
public sealed class ValidationReport
{
    public List<string> Errors { get; }
    public List<string> Warnings { get; }
    public bool HasErrors => Errors.Count > 0;
}
```

В Editor (`#if UNITY_EDITOR`): hard-fail на errors блокирует Play mode. В runtime build: только log + continue (декор с ошибками молча исключается).

---

## 12. Phase roadmap

| Phase | Состав |
|---|---|
| **Phase 0** (этот документ) | DecorConfig + slots + placement service + sale chance modifier + inventory route + stub UI + first-day rewards + 5 sample items |
| **Phase 1** | Behavioral effects (coffee pot extends customer browse, lamp boosts mood). Reserved fields → реальная семантика по одному за раз. Orphaned-placement clean-up at day start. Tests for all integration paths. |
| **Phase 2** | Реальный визуал магазина (тележка / лавка) с world-space slot anchors. Декорация ставится визуально, не через текстовое меню. UISystem-окно `DecorPlacementWindow`. |
| **Phase 3** | Style set bonuses (3 maritime → +X%). Paintable swatches. Decor offers через newspaper в обычные дни (не только Day 1). |
| **Phase 4** | Hard currency покупка. Daily upkeep + auto-disable при нехватке gold. Activatable on/off в Preparation. Distracting эффект. |
| **Phase 5+** | Покупка у вендоров локаций. Временные продавцы (event-driven). Reputation gate на дорогие декорации. |

---

## 13. Тесты Phase 0

EditMode-тесты в новой `Game.Decor.Tests.Editor` asmdef:

| Класс | Покрытие |
|---|---|
| `ConfigBasedDecorModifierProviderTests` | Один декор, несколько декоров (multiplicative), без декораций (=1), отсутствующий genre, decor id отсутствует в конфиге, **soft cap clamp в 3.0**, **lower clamp в 0.1**, negative × positive на разных жанрах не интерферируют |
| `DecorPlacementServiceTests` | Place / Unplace / GetActiveDecorIds / ownership-check / slot-not-found / position-mismatch / size-mismatch / slot-occupied / save round-trip / **orphaned placement cleanup при AfterLoadAsync** (decor удалён из конфига → запись удалена) |
| `DecorRewardServiceTests` | First-day free claim once, paid purchase once, недостаточно gold → fail, idempotent flags |
| `DecorConfigValidatorTests` | Все правила из §11.5: empty id, duplicate id, invalid enum, negative multiplier, unknown genre, locked slot uniqueness в LocationConfig. Каждое правило — отдельный case. |

Дополнительно — расширить `EconomyBasedSaleChanceCalculatorTests` кейсом с реальным провайдером + декором (не через `FakeBaseSaleChanceCalculator`, а через локальный fake `IDecorModifierProvider`).

---

## 14. Открытые вопросы (для обсуждения)

1. **`Distracting` effect** — какая фактическая семантика? Может «отвлекает покупателя — он не подходит к прилавку → нет активной мини-игры»? Запросить уточнение у геймдизайнера / референса.
2. ✅ ~~Multipliers > 2.0 разрешены?~~ — **Решено:** soft cap на per-genre мультипликатор в `[0.1, 3.0]` (см. §5.2.5).
3. ✅ ~~«Negative» декор разрешён?~~ — **Решено:** разрешён, но обязан показываться красным + модальный confirm перед Place (см. §7.1).
4. **Если у игрока ВСЕ слоты заняты, но он купил новый декор** — Phase 0: декор просто лежит в инвентаре, явно не activate'ится. Use-handler пишет лог. UI понятно показывает «нет места».
5. ✅ ~~Save migration~~ — **Решено:** schema 1 с самого начала; миграции нет. `AfterLoadAsync` создаёт default state + чистит orphaned placements (см. §4.3 + §11.5).
6. ✅ ~~Orphan cleanup в Phase 0?~~ — **Решено:** обязательно в `AfterLoadAsync` (см. §4.3 + §11.5).
7. **VisualWeight / DecorationScore** — TB-stat «насколько магазин украшен» для будущей атмосферы / newspaper review. Отложено в Phase 1+, если решим что нужно — добавится новым полем без миграции.
8. **MessagePipe-телеметрия** (`DecorPlacedMessage`, `DecorPurchasedMessage`) — Phase 1, когда появятся consumers (аналитика, achievements).
