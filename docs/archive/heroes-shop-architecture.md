# Shop / Trade System Architecture (Heroes project)

> Источник: codebase `C:\Projects\heroes`. Документ создан для планирования архитектуры покупок в другом проекте.
> Клановый магазин намеренно исключён.

---

## 1. Обзор

Система продаж называется **Trade**. Единая точка входа — `TradePurchasesService`. Все магазины используют одинаковый флоу покупки (`BuyTradeLotCommand → FinalizeBuyingLotCommand`), различается только тип валюты и структура данных конкретного магазина.

Ключевые понятия:
- **TradeLot** — один товар (лот) в любом магазине.
- **TradeTab** — вкладка магазина, содержащая набор лотов.
- **ExchangeSlot** — слот в системе наград (`ExchangeService`), привязанный к лоту и хранящий цену + содержимое лутбокса.
- **PriceData** — цена лота: либо ресурс (soft), либо in-app продукт (hard/real).

---

## 2. Типы магазинов (`TradeShopType`)

| Тип | Описание | Структура | Валюта |
|-----|----------|-----------|--------|
| `Bank` | Премиум-магазин | Tabs → Packs → Lots | Soft + Hard |
| `MerchantShop` | Обычный магазин | Tabs → Packs → Lots | Soft |
| `Sale` | Лимитированные предложения | Плоский список лотов | Soft + Hard |
| `BattlePass` | Батл-пасс | Плоский список лотов | Soft + Hard |
| `Rift` | Покупка ключей для боевого режима | Плоский список лотов | Hard |
| `ZeroEnergy` | Предложение при нулевой энергии | Плоский список (max 3 слота) | Soft + Hard + Free |

---

## 3. Типы валют

### `CurrencyType` (enum)
```
Resource  — soft-валюта (управляется ResourcesService)
Inapp     — hard/real (Unity IAP, реальные деньги)
```

### `PriceData` (класс)
```csharp
public class PriceData : IPriceData {
    public CurrencyType Type { get; }  // Resource | Inapp
    public string Item { get; }        // ID ресурса или IAP product id
    public int Value { get; }          // количество

    public bool IsInApp     => Type == CurrencyType.Inapp;
    public bool IsResource  => Type == CurrencyType.Resource;
    public IPriceData CloneWithValue(int value);
}
```

### `ITradeLot` — свойства цены
```csharp
IPriceData Price  // берётся из привязанного ExchangeSlot
bool IsInApp      // Price?.IsInApp ?? false
bool IsFree       // ExchangeSlot.Prices.IsEmpty()
```

### Стандартные ID ресурсов (`ItemsConstants`)
```
resources/gold     — золото (soft)
resources/energy   — энергия (soft)
resources/cent     — центы (soft)
resources/real     — реальные деньги (используется как маркер)
```

---

## 4. Модель данных

### `TradeLotDbData` (статические данные лота, из БД)
```csharp
string ConfigId                        // уникальный ID
LocalizationKeyDbData Title
LocalizationKeyDbData Description
string Icon
string ExchangeSlotConfigId            // ссылка на конфиг слота наград (цена + лутбокс)
List<IEventDbData> ActivationEvents   // события для активации лота
List<IEventDbData> VisibilityEvents   // события для видимости
List<IEventDbData> UnlockPurchaseEvents // события разблокировки покупки
TradeLotLimitDbData Limit              // лимит покупок
long ActivationDelay                   // задержка активации (секунды)
TradeLotViewType ViewType
TradeLotViewColor ViewColor
int DiscountPercentView                // визуальная скидка (только отображение)
PriceDbData DiscountedFullPriceView   // зачёркнутая цена (только отображение)
TradeLotLabelType LabelType
TradeLotAudioType AudioType
```

### `TradeLotLimitDbData` (конфиг лимита)
```csharp
int Value               // сколько раз можно купить за период
TradeLimitMode Mode     // Disposable (разово) | Refreshable (с обновлением)
int Period              // период обновления в секундах
int RefreshOffsetSeconds // смещение старта периода
```

### `TradeLotBuyingData` (контекст покупки — кеш из лота на момент нажатия)
```csharp
ITradeLot Lot
int ExchangeSlotId
int LootBoxId
bool IsInApp
bool IsFree
IPriceData Price
IReadOnlyList<RewardInfoDbData> Rewards
bool SkipAskUser                          // пропустить диалог подтверждения
Func<UniTask> OnBeforeCongratulationAsync // хук перед показом наград
Func<IReadOnlyList<RewardInfoDbData>, UniTask> CustomCongratulationAsync
```

> **Важно**: после нажатия "купить" данные кешируются в `TradeLotBuyingData`. Живой `TradeLot` в процессе покупки не читается — защита от изменения состояния во время транзакции.

### Структура магазинов с вкладками (Bank, MerchantShop)
```
BankShopDbData / MerchantShopDbData
├─ Tabs: List<TradeTabDbData>
│    └─ Packs: List<TradeTabPackDbData>
│         └─ Lots: List<string>          ← ID лотов
├─ Lots: Dict<string, TradeLotDbData>
├─ LotsGroups: Dict<string, TradeLotsGroupDbData>  (только Bank)
└─ Filters: Dict<string, TradeLotFilterDbData>     (только Bank)
```

### Структура магазинов без вкладок (Sale, BattlePass, Rift, ZeroEnergy)
```
RiftShopDbData
└─ Lots: Dict<string, TradeLotDbData>   ← просто словарь лотов
```

---

## 5. Жизненный цикл лота (`TradeLot`)

### Состояния (`TradeLotState`)
```
Pending     — создан, ещё не активирован (ожидает событий или таймера)
Active      — доступен для покупки
WaitRefresh — лимит исчерпан, ожидает обновления периода
Completed   — навсегда выкуплен (Disposable) или деактивирован
```

### Видимость vs Активность
```csharp
bool IsActive  => State == Active
bool IsVisible => (IsActive || IsWaitRefresh || HasActivationTimer) && CanBeVisible()
// Лот может быть Active, но невидимым (VisibilityEvents не выполнены)
// Лот может быть видимым в WaitRefresh — показывается таймер обновления
```

### Активация лота
Три механизма (приоритет по порядку):
1. **ActivationEvents** — подписывается на игровые события; активируется когда событие наступает.
2. **ActivationDelay** — таймер задержки от момента создания; лот становится активным по истечении.
3. **Прямая активация** — вызов `TryActivateByParent()` из `TradeService.Init()` (для Rift, ZeroEnergy).

### Разблокировка покупки
Отдельно от активации: `UnlockPurchaseEvents` — лот активен и виден, но кнопка "купить" заблокирована, пока не выполнено событие.

### Привязка наград (`ExchangeSlot`)
- При переходе в `Active`/`WaitRefresh` → `TryBindExchangeSlot()`.
- `ExchangeService.Bind(configId, featureId)` — создаёт экземпляр слота, возвращает `slotId`.
- Слот хранит: текущую цену (`Prices`), лутбоксы с наградами, счётчик покупок.
- При переходе в `Completed` → `ExchangeService.Unbind(slotId)`.

### После покупки (`OnBought`)
```csharp
void OnBought(long timestamp) {
    // 1. Добавить timestamp в историю покупок
    // 2. Записать FirstLimitedPurchaseTimeStamp если первая ограниченная покупка
    // 3. CleanUpRewards + TryBindRewards (сдвиг на следующий лутбокс)
    // 4. Fire TradeLotBuySignal
    // 5. Если лимит исчерпан → WaitRefresh (Refreshable) или Completed (Disposable)
}
```

---

## 6. Сервисный слой

### `TradeService` (оркестратор)
- Создаёт все лоты и вкладки при `Init()`.
- Магазины с вкладками (Bank, MerchantShop) → `CreateTabs()`.
- Магазины без вкладок (Sale, Rift, ZeroEnergy, BattlePass) → `CreateLots()`.
- Rift и ZeroEnergy активируются сразу в `Init()` через `TryActivateLots()`.
- Предоставляет: `GetLot(id)`, `GetVisibleTabs(shopType)`, `FinalizeBuyingLotCommand(data)`.

### `TradePurchasesService` (флоу покупки)
Точка входа для UI: `BuyLot(data)` / `BuyLotAsync(data)`.

**Проверки перед запуском (`IsReadyToStartBuyingProcess`):**
```
1. lot != null
2. lot.IsActive
3. !lot.IsLimitExceeded
4. Если IsInApp:
   - IapService.Status == Ready  →  иначе ShowBankTransactionNoResponseWindow + retry callback
   - !IsProductInProcess(productId) || TryRemoveRottenPurchase(productId)  →  иначе ShowPurchaseInProcessWindow
```

**Параллельные покупки:** каждый лот независим (`HashSet<ITradeLot> _activeBuyingLots`). Один и тот же лот нельзя купить параллельно, разные — можно.

### `ResourcesService`
```csharp
bool HasEnough(IPriceData price)
bool HasEnough(string resourceId, int amount)
int  GetAmount(string resourceId)
void ChangeAmount(string resourceId, int delta, TrackData trackData)
```

---

## 7. Флоу покупки — детально

```
UI вызывает TradePurchasesService.BuyLotAsync(TradeLotBuyingData)
    │
    ├─ IsReadyToStartBuyingProcess? (6 проверок) → если нет, выходим
    │
    └─ BuyTradeLotCommand.ExecInternalAsync()
          │
          ├─ UIService.BlockOverFocusedWindow()       ← блок UI
          ├─ PurchaseTracker.OnLotPurchaseStarted()   ← аналитика: старт
          │
          └─ CheckIfCanBuyLotAsync()
                ├─ data/lot/lootBox не null
                ├─ LootBoxesService.Exists(lootBoxId)
                ├─ lot.IsUnlocked
                ├─ (не Free, не InApp) → ResourcesService.HasEnough(Price)
                └─ (не Free, не InApp, не MerchantShop) → AskUserToBuyAsync()
                      └─ показывает TradePurchaseConfirmWindow, ждёт ответа
```

**После гейта — два пути:**

#### Путь A: Soft-валюта / Free (`IsInApp == false`)
```
BuyNotInAppLot()
    ├─ Формирует TradeLotFinalizeBuyingData
    │     (FeatureId, ExchangeSlotId, LotId, Timestamp, TrackData, LootBoxId)
    └─ FinalizeBuyingLotCommand.ExecInternal()
          │
          ├─ ExchangeService.Exists(slotId)?
          │     YES → TryPurchase()
          │           └─ ExchangeService.Purchase(slot, trackData)  ← списание валюты + фиксация покупки
          │     NO  → TryPurchase_Deprecated()
          │           └─ LootBoxesService.Unlock(lootBoxId)         ← устаревший путь
          │
          ├─ NeedSendMail()?
          │     Условие: прошло > FeatureMailsConfig.TradeLotDelaySeconds с момента нажатия
          │     И: не является PiggyBank-лотом
          │
          │     YES → TryExtractBattlePassExperience() + MailsService.CreateMailFromExistedLootBoxes()
          │            (награды приходят в почту, а не сразу)
          │     NO  → TakeRewardsFromExchangeSlot() / TakeRewardsFromLootBox()
          │
          ├─ UpdateLot() → lot.OnBought(timestamp)
          └─ PurchaseTracker.OnLotPurchaseFinished()

Результат → ShowRewardsWindowAsync() (если !MailWasSent и есть не-silent награды)
```

#### Путь B: Hard-валюта / IAP (`IsInApp == true`)
```
BuyIapProductAsync()
    ├─ UIService.ShowLoading()
    ├─ Формирует IapPurchaseInfo(trackData)
    │     .WithCreationTime(timestamp)
    │     .WithLootBoxId(lootBoxId)
    │     .SetMock(isEditor)
    │     если Sale → .AddTags(IsSale)
    │
    └─ BuyIapProductCommand.ExecuteAsync()
          ├─ IapPurchasingProvider.BuyProduct()    ← диалог платформы (Google/Apple)
          ├─ IapPurchaseVerifier.Verify()          ← проверка чека
          ├─ PurchaseInitiateRequestCommand        ← сервер: начало валидации
          ├─ PurchaseStatusRequestCommand          ← сервер: статус
          └─ IapPurchaseDelivery                  ← выдача наград

Результат (cmd.DeliveryResult):
    TimeoutDeliveryResult        → ShowPurchaseInProcessWindow (покупка висит)
    FailedDeliveryResult(Cancel) → ShowPurchaseCancelWindow
    FailedDeliveryResult(Error)  → ShowBankTransactionErrorWindow + retry callback
    RewardsTakenDeliveryResult   → ShowRewardsWindowAsync()
```

#### Диалог подтверждения — когда ПРОПУСКАЕТСЯ
```csharp
var isReadyToBuy = IsFree
    || IsInApp              // платформа сама покажет диалог
    || Shop == TradeShopType.MerchantShop  // в мерчанте нет подтверждения
    || await AskUserToBuyAsync();
```

---

## 8. ExchangeService — система наград

`ExchangeSlot` — центральная единица: хранит конфигурацию вознаграждения (набор лутбоксов с разными вероятностями/содержимым) и состояние (сколько уже куплено).

```csharp
IExchangeSlotInstance slot = ExchangeService.GetSlotInstanceOrDefault(slotId);
IEnumerable<RewardInfoDbData> rewards = ExchangeService.GetRewards(slot);   // текущие награды
bool hasPurchased = ExchangeService.Purchase(slot, trackData);               // зафиксировать покупку
ExchangeSlotTakeResult result = ExchangeService.TakeRewards(takeData);       // выдать награды игроку
```

Цена лота: `slot.Prices` — список `IPriceData`. Обычно одна цена, но архитектура допускает несколько (используется в Rift для multi-resource gate).

---

## 9. Специфика отдельных магазинов

### Sale (распродажи)
- Плоский список лотов, без вкладок.
- Лоты активируются через `ActivationEvents` (события игрового календаря/ивентов).
- Поддерживает `DiscountPercentView` и `DiscountedFullPriceView` — визуальная зачёркнутая цена (только отображение, не влияет на реальную транзакцию).
- При IAP-покупке добавляется тег `IsSale` в `IapPurchaseInfo`.

### BattlePass
- Плоский список лотов.
- `FinalizeBuyingLotCommand` содержит специальную ветку: `TryExtractBattlePassExperience()` — перед отправкой в почту вычленяет XP-ресурс из лутбокса и выдаёт его сразу (не ждёт почты).

### Rift (минимально)
**Что это:** боевой режим с боссами, для входа нужен ключ-ресурс.

**Роль Rift-магазина:** продаёт пачки ключей за гемы (hard-валюта). Ключи — это обычный ресурс (`RiftDbData.ConsumingResource`), который тратится при каждом входе в бой (`ConsumeResourcePerBattle`).

**Особенности:**
- Нет вкладок, нет событийной активации — лоты активируются сразу в `TradeService.Init()`.
- UI (`RiftKeyPurchaseWindow`) показывает до N карточек (`RiftKeyLot`), каждая имеет два состояния:
  - `Buy state` — ключей недостаточно: стандартная кнопка покупки за гемы.
  - `Use state` — ключи есть: кнопка "Использовать" → `RiftService.TryExchangeKeyKit()` (тратит ключи, открывает бой).
- Есть кнопка "Перейти в Банк" (для докупки гемов).
- Данные: `RiftShopDbData { Dict<string, TradeLotDbData> Lots }`.

### ZeroEnergy (минимально)
**Что это:** popup, который появляется когда у игрока заканчивается энергия во время прохождения локации.

**Особенности:**
- Показывает до 3 слотов с разными предложениями (бесплатный, дешёвый, дорогой), набор определяется `TradeLotZeroEnergyFlag` на каждом лоте.
- Слоты и флаги определяются в `ZeroEnergyDbData { TradeLotDbData TradeLot; TradeLotZeroEnergyFlag Flag; bool IsZeroEnergyUnique; }`.
- Подписывается на `TradeLotBuySignal` — если куплена энергия, скрывает описание и закрывает окно при наличии Bank.
- Учитывает туториал: до завершения тутора показывает `FirstSight` слоты, после — `SecondSight`.
- Кнопка "Банк" → `WindowsService.ShowBank(ItemsConstants.EnergyId)`.
- Вся логика отбора лотов на стороне UI (`ZeroEnergyWindow.Setup()` + `ZeroEnergyService.GetFlag(lot)`).

---

## 10. Сигналы / Events

```
TradeLotBuySignal              — лот куплен { Lot, Rewards }
TradeLotChangeStateSignal      — изменилось состояние { Lot, PrevState, NewState }
TradeLotMarkAsViewedSignal     — лот просмотрен
TradeLotUnlockedChangedSignal  — изменилась доступность покупки
TradeLotVisibilityChangedSignal — изменилась видимость
TradeTabRefreshSignal          — вкладка обновлена
TradeTabChangeStateSignal      — состояние вкладки изменено
```

---

## 11. Аналитика

`PurchaseTracker` — центральный трекер, вызывается в `BuyTradeLotCommand`.

| Момент | Метод | Analytics Event |
|--------|-------|-----------------|
| Открытие окна | `OnOpenTradeWindow` | `STORE_WINDOW_OPENED` |
| Нажатие "купить" | `OnLotPurchaseStarted` | — |
| Покупка завершена | `OnLotPurchaseFinished` | `STORE_PURCHASE_MADE` |
| Покупка отменена/провалена | `OnLotPurchaseFailed` | `STORE_PURCHASE_CANCELED` |
| Sale-покупка | — | `OFFER_PURCHASE_MADE` |

Payload аналитики включает: `store_type`, `lot_id`, `tab_id`, `price (spent_items)`, `rewards (received_items)`, `limit_total`, `limit_left`, `displayed_discount`, `purchase_lifetime`, `entry_point`.

---

## 12. Ключевые интерфейсы для портирования

| Интерфейс | Файл | Роль |
|-----------|------|------|
| `ITradeService` | `Core/Trade/TradeService.cs` | Оркестратор: создаёт и хранит все лоты/вкладки |
| `ITradePurchasesService` | `Core/Trade/TradePurchasesService.cs` | Точка входа для покупки |
| `ITradeLot` | `Core/Trade/TradeLot.cs` | Модель лота (состояние, цена, награды, лимиты) |
| `ITradeCommandsFactory` | `Game/Trade/TradeCommandsFactory.cs` | Фабрика команд покупки |
| `IPriceData` | `Shared/CoreApi.Pure/...` | Цена (тип + ID ресурса + количество) |
| `IResourcesService` | `Shared/CoreLogic/Items/ResourcesService.cs` | Кошелёк soft-валют |
| `IIapService` | `Core/Iap/IapService.cs` | Обёртка Unity IAP |
| `IExchangeService` | `Core/...` | Система наград: bind/purchase/take |
| `IFinalizeTradeLotPurchaseCommand` | `FinalizeBuyingLotCommand.cs` | Выдача наград после покупки |

---

## 13. Что важно при портировании

1. **`TradeLotBuyingData` — кеш-снимок.** Нельзя читать живой `TradeLot` в процессе покупки. Это защита от race condition: пока идёт async-покупка, состояние лота может обновиться.

2. **MerchantShop не спрашивает подтверждение.** Это hardcode в `CheckIfCanBuyLotAsync`. При портировании решить: каждый магазин сам задаёт это поведение, или централизованный флаг.

3. **Временной лимит → почта.** Если между нажатием "купить" и финализацией прошло слишком много времени, награды не выдаются сразу — уходят в почту. Порог: `FeatureMailsConfig.TradeLotDelaySeconds`. При портировании может упроститься до "всегда выдавать сразу".

4. **ExchangeService = центр правды о ценах и наградах.** Сама `TradeLotDbData` не хранит цену и содержимое награды — только `ExchangeSlotConfigId`. Реальная цена и лутбокс живут в `ExchangeSlot`. Это сделано для поддержки ротации лутбоксов и динамических наград.

5. **Deprecated: LootBoxesService.** В коде есть старый путь через `LootBoxesService` (с проверкой `ExchangeService.Exists`). Это легаси, в новом проекте использовать только `ExchangeService`.

6. **Три независимых типа событий на лоте:**
   - `ActivationEvents` — когда лот становится активным.
   - `VisibilityEvents` — когда лот становится видимым (отдельно от активности!).
   - `UnlockPurchaseEvents` — когда разблокируется кнопка покупки (лот виден, но недоступен).

7. **Лимиты.** `Disposable` — куплено N раз всего, больше никогда. `Refreshable` — куплено N раз за период, затем обновляется. Период отсчитывается с момента первой покупки, не с midnight.
