# Shop Module — Design Doc

**Status:** Draft (Phase 0 — planning)
**Related:**
- [Decor](Decor.md) — основной consumer декор-офферов и текущий референс флоу покупки ([DecorRewardService.cs](../../Assets/Game/Features/Decor/Services/DecorRewardService.cs))
- [Resources](../../Assets/Game/Features/Resources/API/IResourcesService.cs) — кошелёк (`gold` сейчас, `gems` зарезервированы)
- [Inventory](../INVENTORY.md) — куда складываются купленные книги / декор / коробки
- [heroes-shop-architecture.md](heroes-shop-architecture.md) — ориентир по терминологии (TradeLot/TradeTab/ExchangeSlot) для будущих фаз
- [NewspaperWindow.cs](../../Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs) — точка входа первого магазина (Phase 0)

> Цель документа — зафиксировать, **какие магазины планируются**, **что они продают**, **в какой валюте** и **в каком порядке** мы их реализуем. Это не финальная архитектура — детали ExchangeSlot/лимитов/событий разворачиваются в отдельных фазовых док-файлах по мере того, как магазины выходят за рамки хардкода.

---

## 1. Назначение

Магазины — основной sink для `gold` и единственный путь, по которому игрок целенаправленно расширяет свою коллекцию книг, декора и расходников. Pull-каналы (продажи покупателям, события на локации) дают валюту; push-каналы (магазины, офферы) превращают её обратно в gameplay.

**Phase 0 принцип:** всё за `gold`. Никаких hard currency, IAP, real-money — даже там, где в финальной модели они появятся (Банк, BattlePass, premium offers). Это сужает контракт `IShopService` до одной валюты и одного флоу списания через `IResourcesService.RemoveAsync(...)`.

**Post-MVP:** добавляются `gems`, IAP-продукты, бандлы и event-driven офферы. Контракт менять не придётся — `ResourceIds.Gems` уже зарезервирован, а структура «лот = price + payload» переживает обе валюты.

---

## 2. Глоссарий

Чтобы не плодить терминов, тянем минимум из Heroes:

- **ShopLot** — один товар (одна карточка в UI). Имеет `Price` (валюта + сумма) и `Payload` (что выдать).
- **ShopTab** — вкладка магазина (Books / Decor / Boxes …). Магазин без вкладок = одна неявная вкладка.
- **Box (Loot Box)** — особый Payload: «N случайных книг из пула». Конфигурируется отдельным documentом (см. §3.1).
- **Offer** — лот с лимитом покупок и/или временем жизни (один день, до условия и т.п.).
- **Storefront** — конкретный магазин (Newspaper, Classic Shop, Bank, …). Один Storefront = один UI-флоу + свой набор Tab'ов/Lot'ов.

В Phase 0 контракт сводится примерно к:

```csharp
public interface IShopService {
    IReadOnlyList<ShopLot> GetLots(string storefrontId);
    UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct);
}
```

`ShopPurchaseResult` различает `Success` / `NotEnoughGold` / `LimitReached` / `LotInactive`. Этого хватает на все магазины ниже до момента, когда появится IAP.

---

## 3. Магазины — обзор

| # | Storefront | Что продаёт | Структура | Phase 0 валюта | Phase 0 в скоупе |
|---|------------|-------------|-----------|---------------|-----------------|
| 1 | **Newspaper — Books** | Коробки с книгами (Common / Rare / Genre) | Плоский список 3–4 лота | gold | YES (новое — поверх существующего окна) |
| 1.1 | **Newspaper — Decor** | 2 декора в день (1 free + 1 paid) | Плоский список 2 лота | gold | YES (уже работает через `DecorRewardService`) |
| 2 | **Classic Shop (Bookseller)** | Коробки с книгами, декор, отдельные книги (TBD) | Вкладки: Books / Boxes / Decor | gold | Phase 1 |
| 3 | **Bank** | Gold за hard, hard за real, BattlePass | Плоский список / вкладки | (skip) | NO (Post-MVP) |
| 4 | **Location Vendors** | 2–4 декора, специфичных для локации | Плоский список, привязка к локации | gold | Phase 2 |
| 5 | **Offers (Sale)** | Бандлы gold + boxes + decor | Плоский список, timed | gold | Phase 2 |
| 6 | **Crafting Supplies** | Бумага, чернила, корешки (для реставрации) | Вкладка в Classic Shop | gold | Late Post-MVP |
| 7 | **(extras — см. §10)** | Подписка, daily deal, refresh-token, скины UI | — | — | Идеи |

Phase 0 = магазины 1 и 1.1. Всё остальное — заголовки и контракты, реализация позже.

---

## 3.1 Магазин 1 — Newspaper (Books)

**Точка входа:** существующее окно `NewspaperWindow` ([NewspaperWindow.cs](../../Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs)). Сейчас оно показывает только декор-офферы; в Phase 0 поверх добавляются book-box лоты.

### Лоты (планируемые цены — драфт, не финал)

| Лот ID | Название | Содержимое | Цена | Лимит |
|--------|----------|------------|------|-------|
| `newspaper_box_common` | Common Crate | 15 случайных книг любого жанра | ~20 gold | 1 раз/день |
| `newspaper_box_rare` | Rare Crate | 8 случайных rare книг | ~30 gold | 1 раз/день |
| `newspaper_box_dystopic_cyberpunk` | Dystopic Fiction (touch of cyberpunk) | 1 редкая жанровая книга | ~40 gold | 1 раз/день |

> **Открытые вопросы:**
> - Drop-pool коробок: фиксированный для Common (любой жанр) или отдельный пул для каждой коробки? Скорее второе — иначе Rare/Genre неотличимы по результату от Common.
> - Жанровая коробка содержит **одну** книгу или **несколько одного жанра**? У игрока на лавке ~N жанровых слотов, поэтому 1 книги обычно мало для realtime прокачки полки; склонен к 3–5.
> - Ассортимент коробок ротируется ежедневно (как декор), или это статика всю игру? Скорее ротация: жанр Dystopic Fiction = «сегодняшний» жанр, завтра другой.

### Контракт payload (книжная коробка)

Открытие коробки = выдать `N` строк (`book_id`) в инвентарь категории `book`. Минимальный конфиг:

```jsonc
{
  "lot_id": "newspaper_box_common",
  "price": { "currency": "gold", "amount": 20 },
  "limit": { "mode": "daily", "value": 1 },
  "payload": {
    "type": "book_box",
    "pool": "common_pool_id",
    "rolls": 15
  }
}
```

Пул и сам алгоритм роллов живут отдельно (Inventory/BookSell — там уже есть `ISalesRandom`, переиспользуем его абстракцию рандома, чтобы тесты не плодили свои). **Не отдаём логику пулов в `IShopService`** — он только знает, что заплатил `price` и попросил `IBookBoxOpener.OpenAsync(poolId, rolls)`.

### UI

Phase 0: добавить в `NewspaperWindowView` секцию `Book Crates` с 3 карточками (icon / название / цена / кнопка). Карточки скрываются, если лимит на сегодня уже исчерпан. Никакой анимации открытия — просто toast «Получено: X книг», как сейчас работает декор.

---

## 3.2 Магазин 1.1 — Newspaper (Decor)

**Уже работает** через `DecorRewardService` ([DecorRewardService.cs](../../Assets/Game/Features/Decor/Services/DecorRewardService.cs)). 2 лота: 1 бесплатный (`vintage_globe`) + 1 платный (`coffee_pot`, 50 gold). State хранится в `DecorPlacementState.FirstDayRewardClaimed / FirstDayPurchaseDone`.

### Долги, которые подсветил `DecorRewardService`

1. **`ClaimFreeDecorAsync` — это `BuyAsync` с ценой 0.** В коде уже стоит `//TODO`. Когда появится общий `IShopService`, free-decor становится обычным лотом с `price.amount = 0`, а `DecorRewardService` — тонкой обёрткой, которая дёргает `IShopService` (или удаляется совсем).
2. **Ротация офферов.** Сейчас id хардкод, оффер «один раз навсегда» (флаг `FirstDayPurchaseDone`). Phase 3 (по [Decor.md](Decor.md)): офферы обновляются каждые 2–3 дня → state переезжает на «последний день ротации» + «куплено за этот цикл».
3. **Конфиг.** Цена / id декора зашиты константами. Должны жить в `NewspaperConfig` / `ShopConfig` через `IConfigsService` (паттерн уже есть для books и decor).

---

## 3.3 Магазин 2 — Classic Shop (Bookseller)

**Phase 1.** Постоянный магазин букиниста. Не привязан к ежедневному циклу газеты — открывается из главного UI в любой день.

### Структура — вкладки

- **Books** — отдельные книги (1 шт. за лот). Узкий ассортимент, ротируется реже коробок. Цены выше per-book, чем в коробках, но игрок знает, что покупает.
- **Boxes** — те же типы коробок, что в Newspaper, но ассортимент шире и/или без daily-лимита (TBD: либо чуть дороже, либо с лимитом на неделю).
- **Decor** — постоянный каталог декора. Не ежедневный, а «вся коллекция, доступная к этой локации/репутации». Phase 2+: gated по progression.

### Открытые вопросы

- **Отдельные книги — есть ли смысл вообще?** Если коробка Rare даёт 8 книг за 30 gold (~3.75/штука), то отдельная rare книга должна стоить меньше ~10 gold, чтобы конкурировать со «случайным выбором». Иначе игроки никогда не покупают штучно. Возможно, отдельные книги = «выбор без рандома, дороже», и это сознательный trade-off.
- **Граница с Newspaper.** Если Classic Shop продаёт те же коробки — Newspaper становится daily-freemium витриной (узкий выбор, скидка), а Classic — широким каталогом по полной цене.

### Phase 1 валюта

Только `gold`. Post-MVP можно ввести `gems` для премиум-лотов (cosmetic декор, exclusive книги), но это **не** перевод обычного ассортимента на hard currency — это новый ассортимент.

---

## 3.4 Магазин 3 — Bank (Post-MVP)

**Не в Phase 0/1.** Зафиксирован для планирования контракта.

- Продаёт `gems` за реальные деньги.
- Продаёт `gold` за `gems` (вторичный sink для hard).
- Продаёт BattlePass — отдельный лот с длинным reward track.

### Открытый вопрос: Bank vs Classic Shop

Можно сделать Bank отдельным Storefront'ом (как в Heroes), а можно — вкладкой Premium внутри Classic Shop. **Рекомендация:** отдельный Storefront. Тогда:
- IAP-флоу (платформенный диалог, валидация чека) изолирован в одном месте.
- Classic Shop остаётся чистым soft-валютным магазином, его контракт не трогает IAP.
- Аналитика чище: `bank_open` ≠ `classic_shop_open`.

В Phase 0 эта развилка не блокирует — мы просто не создаём Bank Storefront.

---

## 3.5 Магазин 4 — Location Vendors (Phase 2)

NPC-продавцы прямо на локации. Под текущий gameplay лучше всего ложатся как **per-location decor офферы**: 2–4 декора, тематически совпадающих с локацией.

Технически — это `Storefront` с `id = "vendor_<location_id>"`, лоты собираются из `LocationConfig.VendorOffers`. Покупка идёт через тот же `IShopService.BuyAsync`. UI отдельный (прямо в world-сцене, не в окне), но контракт сервиса не меняется.

### Открытые вопросы

- **Ассортимент меняется при переезде локации, или у каждой локации свой «накопленный» state офферов?** Скорее первое: вендор = свойство локации, а не отдельный персистентный магазин.
- **Это «офферы» (lim. by day) или «постоянный ассортимент локации»?** Возможно, и то и то: 1–2 редких оффера на день + базовый каталог локации.

---

## 3.6 Магазин 5 — Offers / Sale (Phase 2)

Бандлы. Один лот = коробка + декор + gold за пакетную цену. Цена бандла должна быть очевидно лучше суммы компонентов, иначе он бесполезен.

**Особенности:**
- Лимит на покупку (один раз / N раз).
- Time-bound: появляется при событии (закончил локацию, поднял репутацию, прошёл день N), живёт до X дней.
- DiscountView — визуальная «зачёркнутая цена», как в Heroes. Только UI, не влияет на транзакцию.

Это первый магазин, где появляется **событийная активация лотов** (`ActivationEvents` из Heroes). До этого момента все лоты — статически активные или активные по daily reset.

---

## 3.7 Магазин 6 — Crafting Supplies (Late Post-MVP)

Расходники для фичи реставрации книг: бумага, чернила, корешки. Реализуются как обычная вкладка в Classic Shop, когда (и если) реставрация выйдет в работу.

Под это нужны:
- Новая категория `InventoryCategories.CraftingMaterial` (или подкатегория) в [InventoryCategories.cs](../../Assets/Game/Features/Inventory/API/Constants/InventoryCategories.cs).
- Цены ниже, чем у книг (это «расходка», игрок покупает регулярно).
- Возможно, stack-режим — но это уже решение Inventory, не Shop.

**Не делаем в Phase 0/1.** Просто фиксируем, что вкладка ляжет естественно.

---

## 4. Жизненный цикл лота — Phase 0

Минимальный набор состояний, без событийной активации:

```
Available  — есть на витрине, можно купить (если хватает gold и не превышен лимит)
SoldOut    — лимит исчерпан, ждём ежедневного reset (00:00 по локали игрока — TBD)
Hidden     — лот не показывается (например, gated по progress; Phase 2+)
```

Daily reset:
- Newspaper-лоты сбрасываются вместе с переходом дня в core loop (`ICurrentDayProvider` уже есть в BookSell).
- Classic Shop — TBD: либо daily, либо weekly, либо не сбрасывается вообще (постоянный ассортимент).

**Не делаем в Phase 0:**
- `ActivationEvents` / `VisibilityEvents` / `UnlockPurchaseEvents` (Heroes-style). Заводим, когда появятся Offers (§3.6).
- Refreshable-лимиты с периодом отсчёта от первой покупки. Daily = достаточно.
- `WaitRefresh` состояние с таймером на UI.

---

## 5. Флоу покупки — Phase 0

Простой soft-only флоу, без диалога подтверждения и без mail-fallback'ов:

```
UI клик "купить"
  ├─ IShopService.BuyAsync(lotId, ct)
  │     ├─ lot.IsAvailable?                       → нет: ShopPurchaseResult.LotInactive
  │     ├─ lot.LimitReached?                      → да:  ShopPurchaseResult.LimitReached
  │     ├─ resources.Has(gold, lot.Price)?        → нет: ShopPurchaseResult.NotEnoughGold
  │     ├─ resources.RemoveAsync(gold, price, "shop:<lotId>", ct)
  │     ├─ payload.GrantAsync(ct)                 ← inventory.Add* / boxOpener.Open / etc.
  │     ├─ lot.IncrementPurchases()               ← обновить лимит, сохранить state
  │     └─ return Success(rewards)
  └─ UI показывает rewards / toast / открытие коробки
```

Эквивалент Heroes-флоу, но без `TradePurchasesService` / `BuyTradeLotCommand` / `FinalizeBuyingLotCommand`. Те команды появятся, когда добавятся IAP и почта. В Phase 0 одна команда, один await.

**Подтверждение покупки.** В Phase 0 подтверждения нет — клик сразу списывает gold. Это согласуется с тем, как уже работает `DecorRewardService.BuyOfferedDecorAsync`. Когда появятся дорогие лоты (>~100 gold) или IAP — добавим `IShopConfirmationPolicy`, который решает, для каких лотов показывать диалог.

---

## 6. Сервисный слой — что нужно добавить

### Новые модули

- `Game.Shop.API` — `IShopService`, `ShopLot`, `ShopPurchaseResult`, `ShopPayload` (книжная коробка / декор / валюта / inventory item).
- `Game.Shop.Services` — `ShopService`, лот-фабрика из конфига, daily-reset hook.
- `Game.Shop.Config` — `ShopConfig` (json через `IConfigsService`).

### Переиспользуем

| Что | Откуда | Зачем |
|-----|--------|-------|
| Списание gold | [IResourcesService.RemoveAsync](../../Assets/Game/Features/Resources/API/IResourcesService.cs) | Транзакция валюты |
| Выдача книг / декора | [IInventoryService.AddAsync](../../Assets/Game/Features/Inventory/API/IInventoryService.cs) | Payload-доставка |
| Рандом для коробок | `ISalesRandom` из BookSell | Один источник rng = детерминированные тесты |
| Конфиги | `IConfigsService` | Лоты грузятся как json, не хардкод |
| Текущий день | `ICurrentDayProvider` (BookSell) | Daily reset лотов |

### Что **не** делаем сразу

- ExchangeService / ExchangeSlot (Heroes). Слишком много инфраструктуры для одной валюты и трёх лотов.
- Отдельный `IShopConfigService`. Лоты живут в общем `ShopConfig`, грузятся как обычный json.
- Аналитика-трекер с STORE_PURCHASE_MADE и payload'ом spent/received items. **Должна** появиться, но в Phase 0 достаточно одного `Debug.Log` (как делает `DecorRewardService` сейчас). Полная аналитика — отдельная задача, не блокирует магазин.

---

## 7. Save / Persistence

Что нужно хранить между сессиями:

- **Daily state магазина** — какие лоты сегодня уже куплены, на какой день setup собран. Хранится в новом save-модуле `shop.daily` (по аналогии с `decor.placement`).
- **Permanent state** — для Disposable-лотов (купил один раз навсегда). В Phase 0 таких нет, но дизайн save-схемы должен это поддерживать (поле `purchased_disposable_lots: [...]`).
- **Migration:** существующий `DecorPlacementState.FirstDayRewardClaimed / FirstDayPurchaseDone` мигрирует в `shop.daily` (или живёт параллельно до полного перехода декор-офферов на `IShopService`).

Save-схема (черновик):
```jsonc
"shop.daily": {
  "day": 5,
  "purchases": {
    "newspaper_box_common": 1,
    "newspaper_box_rare": 0
  }
}
```

---

## 8. Аналитика — что хотим мерить (Post-Phase-0)

Минимальный набор, который пригодится для балансировки экономики:

- `shop_window_opened { storefront_id }`
- `shop_lot_purchased { lot_id, price_amount, payload_summary }`
- `shop_lot_failed { lot_id, reason }` — `NotEnoughGold` критически важен, это сигнал о деф ците валюты в экономике.
- `shop_box_opened { box_id, rolls, rewards: [book_id...] }` — для проверки честности drop-таблиц.

В Phase 0 не реализуем, но контракт `IShopService.BuyAsync` должен возвращать достаточно информации, чтобы трекер потом цеплялся декоратором, а не пересобирался через side-channel.

---

## 9. Phase плана

| Phase | Скоуп | Зависимости |
|-------|-------|-------------|
| **0 (сейчас)** | Newspaper Books (3 коробки) поверх существующего окна. Decor offers переезжают на новый `IShopService` (или остаются как есть, если переход рискует). Один storefront, daily-limit, без аналитики. | Drop-pool spec для коробок ([нужно дописать](#-открытые-вопросы)). |
| **1** | Classic Shop (Books / Boxes / Decor вкладки). Постоянный ассортимент. | Phase 0 контракт `IShopService`. |
| **2** | Location Vendors + Offers/Sale. Событийная активация лотов. | LocationConfig, событийная шина. |
| **Post-MVP** | Bank (gems / IAP / BattlePass). | IAP-инфраструктура (нет в проекте). |
| **Late Post-MVP** | Crafting Supplies. | Фича реставрации. |

---

## 10. Идеи дополнительных магазинов (§7 из запроса)

Подбор под жанр (cozy book-shop sim):

1. **Daily Deal / Spotlight** — один случайный лот в день со скидкой 30–50%. Не отдельный магазин, а топовая карточка в Newspaper или Classic Shop. Дешёвый dopamine hit для ежедневного захода.
2. **Subscription / Reader's Club** — недельная подписка (если/когда появится hard currency): даёт ежедневный бесплатный лот + скидку на коробки. Soft retention механика.
3. **Wishlist / Pre-order** — игрок отмечает книгу/декор, который хочет; магазин показывает её первой при появлении в ротации. Не отдельный магазин, но фича на стыке Shop ↔ Inventory.
4. **Trade-in / Sell-back** — продать ненужные книги обратно за gold (низкий курс). Sink для дубликатов из коробок. **Спорно** — может сломать экономику коробок, если курс плохо посчитан.
5. **Auction / Mystery Box** — один случайный rare-декор в день за высокую цену, без previewа. Геймплейная альтернатива «купить точно то, что хочу».
6. **Book Hunter** — NPC, который раз в N дней приносит конкретную редкую книгу по заказу игрока. Quest-style витрина (1 лот, выдаётся по выполнении условия).
7. **Refresh Token** — потратить gold/gems, чтобы прокрутить ассортимент Newspaper до конца дня. Опасно — обрезает daily-loop, но решает проблему «сегодня нет ничего интересного».
8. **Seasonal Stalls** — временный магазин, привязанный к календарю / событию (Хэллоуин-стенд, рождественский книжный лоток). Reuse Offers-инфраструктуры, без отдельного движка.
9. **UI Cosmetics** — обложки полок, рамки окна газеты, шрифт ценников. Чистый монетизационный канал, нулевое влияние на gameplay. Post-MVP.
10. **Reputation Vendor** — продаёт за gold + «репутацию у жанра», когда репутация появится. Связка двух валют без необходимости вводить hard.

Конкретно для Phase 0 ничего из этого не нужно — но **Daily Deal (1)** ляжет в Newspaper Books легко: помечаем один лот тегом `featured`, UI рисует его крупнее. Это полезно зафиксировать сразу, чтобы тег появился в схеме конфига.

---

## 11. Открытые вопросы

### Решено (см. §12 — Технический дизайн)

- ✅ **Drop-pool коробок** — Phase 0: рандом из общего пула книг (хардкод на клиенте, баланс ронять не пытаемся). Phase 1+: ролит сервер.
- ✅ **Размер жанровой коробки** — Phase 0 черновые значения: common=15, rare=8, genre=1. Финальный баланс в Phase 1.
- ✅ **Ротация ассортимента** — Phase 0 не ротируется. Те же 3–4 коробки навсегда.
- ✅ **Daily reset тайминг** — Phase 0 N/A (нет reset'а вообще).
- ✅ **Подтверждение покупки** — Phase 0 без диалога.
- ✅ **Newspaper storefront структура** — два storefront'а в одном окне: `newspaper.books` + `newspaper.decor`.

### Ещё открыто

- **Цена отдельных книг vs коробок** — есть ли вообще смысл в штучной покупке. Решаем при дизайне Classic Shop (Phase 1).
- **Bank = отдельный Storefront или вкладка Premium в Classic Shop?** Решаем перед Phase 3.
- **Trade-in / sell-back** — рискованно для экономики, обсудить отдельно.
- **`DecorRewardService` после Phase 0 миграции** — удалить или оставить thin facade поверх `IShopService`? Зависит от того, есть ли вызывающие за пределами `NewspaperWindow` (текущий `UiPilotDebugPanel.cs:69` дёргает `ClaimFreeDecorAsync` — миграция дебаг-панели → удаление facade).
- **Какие конкретно книги попадают в Phase 0 пулы коробок** — нужен список `book_id` или фильтр по тегу (`rarity == "common"`).
- **`source`-строка для аналитики / `IRewardGrantService.GrantAsync(..., source)`** — формат: `"shop:<lotId>"` или `"shop:<storefrontId>:<lotId>"`? Определяем при подключении аналитики (Phase 1).

### Phase 0 — известные ограничения и tech debt

Список фиксируется по факту реализации PR1–PR4. Каждый пункт = осознанное Phase 0 решение, **не баг**. Адресуется в Phase 1+, если приоритет позволит.

#### Игровая логика

1. **Book-box не фильтрует по инвентарю.** `BookBoxRewardExpander.BuildPool` ([`BookBoxRewardExpander.cs:78`](../../Assets/Game/Features/Rewards/Services/BookBoxRewardExpander.cs)) тянет из всех `BookConfig`'ов с фильтром rarity/genre, **не учитывает** что у игрока уже есть. Категория `book` — `ItemStackingMode.Unique`, дубликаты silent no-op в `InventoryService.ApplyAdd` ([`InventoryService.cs:208-217`](../../Assets/Game/Features/Inventory/Services/InventoryService.cs)). **Эффект:** при заполнении коллекции крейт становится всё бесполезнее (50/60 в инвентаре → 15 ролов дают ~2-3 новых книги, остальные silent drop, gold потрачен).
   - **Фикс (Phase 1):** inject `IInventoryService` в expander, в `BuildPool` исключать `_inventory.Has(book.Id)`. `Math.Min(rolls, poolSize)` уже корректно clamp'ит при сжатии пула. +2 теста: исключение owned, пустой пул.
   - **UI doliv:** `LastBookRewardLabel` уже показывает фактическое количество через `result.Granted.Items.Count` — игрок поймёт, что коллекция почти полная. Опционально: добавить hint «коллекция почти полная — крейт может дать меньше».
   - **Альтернатива (Phase 2+):** daily rotation пулов делает дубликаты приемлемыми (как gacha).

2. **`shop` save для Unlimited лотов redundant.** Счётчик `Purchases` инкрементится при каждой покупке книжной коробки, но никто его не читает (`IsAvailable` для `Unlimited` returns `true` без проверки). **Эффект:** save bloat (несколько байт), нет функционального вреда. Решение «оставить» обсуждалось — forward-compat для daily-reset / lifetime-N.
   - **Опционально (Phase 1):** в `ShopService.BuyAsync` пропускать `IncrementPurchase` + `_repository.SaveAsync` для `Mode == Unlimited`.

3. **`MaxPurchases=0` на Unlimited лотах** — семантически странное. JSON выглядит как «0 покупок разрешено», но фактически `Mode==Unlimited` обходит проверку.
   - **Фикс (Phase 1):** сделать `MaxPurchases` опциональным (`int?`) или ввести `ShopLotLimit.Unlimited()` factory + кастомный JSON converter, чтобы конфиг писал просто `"limit": "Unlimited"`.

4. **`DecorRewardService` facade хардкодит item id'ы.** `OfferedFreeDecorId => "vintage_globe"`, `OfferedPaidDecorId => "coffee_pot"` ([`DecorRewardService.cs:13-14`](../../Assets/Game/Features/Decor/Services/DecorRewardService.cs)). `OfferedPaidPrice` уже мигрирован на чтение из конфига; id'ы — нет, потому что нет способа определить «первый item в первом rewardItems»-конвенции без догадок.
   - **Фикс (Phase 1):** либо вернуть id из `lot.RewardItems[0].Id` (хрупкая convention), либо удалить эти свойства из API и обновить consumer'ов.

5. **Atomicity gap в BuyAsync (`SHOP.md §12.4`).** Если `RewardGrantService.GrantAsync` падает после `RemoveAsync` — gold списан, награда не выдана. `ShopService` логирует `Debug.LogError` с source для ручного восстановления, но автоматического refund'а нет.
   - **Фикс (Phase 2 — server-authoritative):** атомарная транзакция на сервере + snapshot apply.

#### Инфраструктура

6. **`ShopService.AfterLoadAsync` локально дожидается `_configs.WarmupAsync`.** ([`ShopService.cs:AfterLoadAsync`](../../Assets/Game/Features/Shop/Services/ShopService.cs)) Это патч для race condition: `Bootstrap.cs:201` запускает `SaveDataLoadOperation` и `ConfigsWarmupOperation` в **параллельной** `LoadingGroup`. Если save load заканчивается раньше прогрева конфигов, `ShopService` (как `ISaveHook`) видит пустой `IConfigsService.GetAll<ShopConfig>()` → пустой каталог. Локально ждём `WarmupAsync` (idempotent).
   - **Фикс (Phase 1):** на уровне Bootstrap — либо сделать Configs зависимостью SaveDataLoad, либо вынести в две последовательные phase'ы. Затронет и других consumer'ов конфигов в save-hook'ах (потенциально `DecorPlacementService`).

7. **`DecorRewardService` facade живёт только ради `UiPilotDebugPanel`.** Единственный consumer вне `NewspaperWindow`. После миграции дебаг-панели на `IShopService.BuyAsync(NewspaperShopLotIds.DecorFreeVintageGlobe, ...)` можно удалить facade + интерфейс `IDecorRewardService`.

8. **`IDecorRewardService` остался в `Game.Decor.API`.** API всё ещё экспортирует интерфейс, который существует только для legacy consumer'а. Удаляется вместе с пунктом 7.

#### UX

9. **Inline label вместо popup.** `NewspaperWindow.LastBookRewardLabel` показывает «Получено: 15 книг — book_001, book_004, ...» как простой текст. Нет анимации, нет иконок, нет possibility to dismiss. Это сознательно скромный MVP UX.
   - **Фикс (Phase 1):** `RewardsWindow` popup со списком книг + иконки + кнопка OK. Переиспользуется для Classic Shop и других магазинов.

10. **Нет аналитики.** Контракт `IShopService.LotPurchased` готов к подписке трекером (см. `SHOP.md §8`), но decorator/listener в Phase 0 не реализован.
    - **Фикс (Phase 1):** `ShopAnalyticsListener` подписывается на `LotPurchased` и эмитит события из списка §8.

11. **Нет подтверждения покупки.** Клик «купить» сразу списывает gold. Для дешёвых лотов (20-50 gold) приемлемо; для будущих дорогих лотов (Phase 3 Bank) обязательно.
    - **Фикс (Phase 1):** `IConfirmationPolicy` с порогом по `lot.Price.Amount` или флагом в `ShopConfig`.

12. **Нет daily reset.** Книжные коробки бесконечны, декор-офферы одноразовые. Phase 0 не ротирует.
    - **Фикс (Phase 1):** см. §13 Phase plan — daily reset по `ICurrentDayProvider` (уже есть в BookSell) или server-calendar.

---

## 12. Технический дизайн (Phase 0)

Этот раздел фиксирует контракты и pipeline, по которым реализуется Phase 0. Цель — чтобы реализация шла без новых архитектурных решений «на ходу», и чтобы переход на Phase 1+ (сервер-authoritative) требовал замены реализаций, а не контрактов.

### 12.1. Asmdef-структура

Три новых модуля + использование существующих.

```
Game.Rewards.API     ←─────┐
Game.Rewards               │
                           │
Game.Shop.API     ←──┐     │
Game.Shop  ─────────┼──────┘  (depends on Rewards.API)
Game.Shop.UI ─── ──┘

(внешние зависимости Shop:
   Game.Resources.API, Game.Inventory.API, Game.Configs.API, Game.Save.API)
```

| Asmdef | Содержит | Зависит от |
|--------|----------|------------|
| `Game.Rewards.API` | `IRewardGrantService`, `RewardSpec`, `RewardItem`, `RewardKind`, `RewardGrantResult` | UniTask |
| `Game.Rewards` | `LocalRewardGrantService` (Phase 0). Phase 1+: `ServerRewardGrantService` рядом, выбирается через DI. | `Rewards.API`, `Resources.API`, `Inventory.API` |
| `Game.Shop.API` | `IShopService`, `ShopLot`, `ShopPrice`, `ShopLotLimit`, `ShopPurchaseResult`, `ShopPurchaseStatus` | UniTask |
| `Game.Shop` | `ShopService`, `ShopConfig` (JSON), `BookBoxRewardExpander` (Phase 0 only), save-репозиторий | `Shop.API`, `Rewards.API`, `Resources.API`, `Configs.API`, `Save.API` |
| `Game.Shop.UI` | секции `NewspaperBookCratesSection`, `NewspaperDecorSection`, `ShopLotCardView` | `Shop.API`, `Rewards.API` (для отображения spec'a) |

**Принцип:** Shop **не** ссылается на `Game.Rewards` (impl). Только на `Game.Rewards.API`. Это позволяет менять `LocalRewardGrantService` ↔ `ServerRewardGrantService` без пересборки Shop'а.

**Что не делаем в Phase 0:**
- `IRewardHandler` (handler pattern из Rewards-System.md). В Phase 0 `LocalRewardGrantService` обходится `switch` по `RewardKind` — handler pattern избыточен на двух типах.
- `IRewardIntentService`, `IRewardPlayerStateSyncService`, `IRewardSpecProvider` из Rewards-System.md. Они появляются в Phase 1+, когда добавятся реклама и серверный grant.

---

### 12.2. Контракт `Game.Rewards.API`

```csharp
namespace Game.Rewards.API
{
    public enum RewardKind
    {
        Resource,        // gold, gems
        InventoryItem    // book, decor, puzzle_piece
    }

    public readonly struct RewardItem
    {
        public string Id { get; }            // "gold" | "vintage_globe" | book_id
        public string Category { get; }      // только для InventoryItem; null для Resource
        public int Amount { get; }
        public RewardKind Kind { get; }
    }

    public sealed class RewardSpec
    {
        public string Id { get; }                          // "newspaper_decor_vintage_globe"
        public IReadOnlyList<RewardItem> Items { get; }    // что выдать
    }

    public interface IRewardGrantService
    {
        /// <summary>
        /// Выдаёт награду. <paramref name="source"/> — аудит-строка ("shop:lotId", "ftue").
        /// Результат содержит фактически выданный spec — может отличаться от запрошенного
        /// (например, для book-box: запрос "book_box_common_15", результат — 15 конкретных книг).
        /// </summary>
        UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct);
    }

    public readonly struct RewardGrantResult
    {
        public bool Success { get; }
        public RewardSpec Granted { get; }            // фактически выданное; null при failure
        public string FailureReason { get; }          // null при success
    }
}
```

**Почему `Granted` может отличаться от запрошенного:**
- Phase 0: для book-box запрос `"book_box_common_15"` развёртывается в 15 конкретных книжных `RewardItem` (через `BookBoxRewardExpander`).
- Phase 1+: сервер сам решает, что выдать, и возвращает развёрнутый spec.

UI должен показывать **`Granted`**, не запрос — иначе игрок увидит «куплено: book_box_common_15» вместо реальных книг.

---

### 12.3. Контракт `Game.Shop.API`

```csharp
namespace Game.Shop.API
{
    public sealed class ShopLot
    {
        public string LotId { get; }
        public string StorefrontId { get; }            // "newspaper.books" | "newspaper.decor"
        public ShopPrice Price { get; }
        public string RewardId { get; }                // ссылка на RewardSpec в Rewards
        public ShopLotLimit Limit { get; }
    }

    public readonly struct ShopPrice
    {
        public string Currency { get; }                // "gold" (Phase 0). Phase 3+: "gems", "inapp"
        public int Amount { get; }                     // 0 = free
    }

    public sealed class ShopLotLimit
    {
        public ShopLimitMode Mode { get; }
        public int MaxPurchases { get; }               // для Disposable
    }

    public enum ShopLimitMode
    {
        Unlimited,         // Phase 0 book crates
        Disposable         // Phase 0 decor lots (купил один раз — навсегда)
    }

    public interface IShopService
    {
        IReadOnlyList<ShopLot> GetLots(string storefrontId);
        bool TryGetLot(string lotId, out ShopLot lot);

        int GetPurchaseCount(string lotId);
        bool IsAvailable(string lotId);                // false если LimitReached

        UniTask<ShopPurchaseResult> BuyAsync(string lotId, CancellationToken ct);

        event Action<ShopPurchaseEvent> LotPurchased;
    }

    public readonly struct ShopPurchaseResult
    {
        public ShopPurchaseStatus Status { get; }
        public RewardSpec Granted { get; }              // null если не Success
        public ShopLot Lot { get; }
    }

    public enum ShopPurchaseStatus
    {
        Success,
        NotEnoughCurrency,
        LimitReached,
        LotNotFound,
        InternalError
    }

    public readonly struct ShopPurchaseEvent
    {
        public ShopLot Lot { get; }
        public RewardSpec Granted { get; }
    }
}
```

**Что в Phase 0 не нужно в API:**
- `ActivationEvents` / `VisibilityEvents` / `UnlockPurchaseEvents` — все лоты в Phase 0 «активны и видимы» либо «полностью выкуплены».
- Состояние `WaitRefresh` — нет рефреша.
- `IsLotInactive` причина — все лоты активны или sold-out.

---

### 12.4. Покупочный pipeline (Phase 0)

```
UI клик "купить"
  ├─ IShopService.BuyAsync(lotId, ct)
  │     ├─ TryGetLot(lotId)                                    → LotNotFound
  │     ├─ IsAvailable(lotId)?  (под лимитом)                   → LimitReached
  │     ├─ IResourcesService.Has(price.Currency, price.Amount)  → NotEnoughCurrency
  │     ├─ IResourcesService.RemoveAsync(...)                   ← списание gold
  │     ├─ IRewardGrantService.GrantAsync(
  │     │       rewardSpecRegistry[lot.RewardId],
  │     │       $"shop:{lot.LotId}", ct)
  │     │
  │     │     ├─ Phase 0: LocalRewardGrantService
  │     │     │     ├─ Если spec.Id startsWith "book_box_" → BookBoxRewardExpander
  │     │     │     │     ролит N книг → возвращает развёрнутый RewardSpec
  │     │     │     └─ Для каждого RewardItem:
  │     │     │           Resource      → Resources.AddAsync
  │     │     │           InventoryItem → Inventory.AddAsync(item.Id, item.Category, amount)
  │     │     │
  │     │     └─ Phase 1+: ServerRewardGrantService
  │     │           POST /rewards/grant {reward_id, source, idempotency_key}
  │     │           ← возвращает snapshot, который применяется к локальному state
  │     │
  │     ├─ Increment purchase count → save shop.<lotId>.purchases
  │     ├─ Fire LotPurchased(lot, granted)
  │     └─ return Success(granted, lot)
  └─ UI читает result.Granted и показывает RewardsWindow / inline toast
```

**Атомарность:** Phase 0 не транзакционна. Если `RewardGrantService.GrantAsync` падает между `Resources.Remove` и `Inventory.Add` — gold списан, награда не выдана. Митигация в Phase 0:
- Сначала Remove (валюта), потом Grant. Если Grant падает — пишем error log, **в Phase 0 не откатываем**.
- Все операции внутри Resources/Inventory персистят через `ISaveService`, который тоже async. Сбой save — отдельная проблема save-системы.
- Полноценная транзакционность приедет в Phase 2 через snapshot-pattern (как описано в Rewards-System.md).

Это сознательный долг Phase 0. Зафиксирован в open questions.

---

### 12.5. Book-box rolling (Phase 0)

Phase 0 рандом полностью на клиенте. Изолирован в `BookBoxRewardExpander`, который вызывается внутри `LocalRewardGrantService`, если `spec.Id` соответствует book-box паттерну.

```csharp
internal sealed class BookBoxRewardExpander
{
    private readonly IBookCatalog _catalog;   // источник всех book_id
    private readonly ISalesRandom _random;    // переиспользуем из BookSell

    public RewardSpec Expand(RewardSpec boxSpec)
    {
        // boxSpec.Items содержит один RewardItem с зашитым "пулом" в Category
        // или используется зашитая в expander таблица:
        //   "book_box_common_15"  → 15 случайных книг из всего каталога
        //   "book_box_rare_8"     → 8 случайных rare (фильтр по tag)
        //   "book_box_dystopic_1" → 1 случайная dystopic
        //
        // Возвращает новый RewardSpec с N InventoryItem-ами.
    }
}
```

**Phase 1 migration:** `BookBoxRewardExpander` удаляется. Сервер возвращает уже развёрнутый spec. `LocalRewardGrantService` тоже либо удаляется, либо остаётся для оффлайн-режима / non-IAP лотов — решение по ходу Phase 1.

**Что критично сейчас:**
- Не размазывать рандом по UI или ShopService. Только внутри Rewards модуля.
- Не хранить «какие книги выпали» в shop-конфиге. Конфиг знает только `book_box_common_15`. Содержимое — Rewards-проблема.

---

### 12.6. Save-схема

Новый save-модуль `shop` (отдельно от `decor.placement`).

```jsonc
"shop": {
  "version": 1,
  "lots": {
    "newspaper_decor_vintage_globe":   { "purchases": 1 },
    "newspaper_decor_coffee_pot":      { "purchases": 0 },
    "newspaper_book_common_15":        { "purchases": 3 },
    "newspaper_book_rare_8":           { "purchases": 1 },
    "newspaper_book_genre_dystopic":   { "purchases": 0 }
  }
}
```

**Миграция из `decor.placement`:**

При первой загрузке после релиза Phase 0:
- Если `decor.placement.FirstDayRewardClaimed == true` → `shop.lots.newspaper_decor_vintage_globe.purchases = 1`
- Если `decor.placement.FirstDayPurchaseDone == true` → `shop.lots.newspaper_decor_coffee_pot.purchases = 1`

Старые поля `FirstDayRewardClaimed` / `FirstDayPurchaseDone` после миграции **не удаляем** в первом релизе Phase 0 — оставляем как fallback на случай отката. Удаление — следующий релиз после того, как Phase 0 прошёл production.

**Версионирование:** `version: 1`. Любое изменение схемы (добавление полей, переименование) — поднимаем версию и пишем migrator. Это упреждающая мера, в Phase 0 миграций версий не требуется.

---

### 12.7. Phase 0 — Definition of Done

Phase 0 считается закрытой, когда выполнено всё:

- [ ] Созданы asmdef'ы `Game.Rewards.API`, `Game.Rewards`, `Game.Shop.API`, `Game.Shop`, `Game.Shop.UI` со структурой из §12.1.
- [ ] `IRewardGrantService` + `LocalRewardGrantService` реализованы, покрыты unit-тестами (Resource grant, InventoryItem grant, book-box expansion, failure case).
- [ ] `IShopService` + `ShopService` реализованы. Тесты: успешная покупка, NotEnoughCurrency, LimitReached, LotNotFound, корректный inc счётчика, корректный event.
- [ ] `ShopConfig` грузится через `IConfigsService`. JSON содержит:
  - 3 book-box лота в storefront `newspaper.books` (`newspaper_book_common_15` за 20g, `newspaper_book_rare_8` за 30g, `newspaper_book_genre_dystopic` за 40g — цены и количество **черновые, не итоговые**).
  - 2 decor-лота в storefront `newspaper.decor`, мигрированные из текущего хардкода `DecorRewardService` (free vintage_globe, paid coffee_pot за 50g, оба Disposable).
- [ ] `BookBoxRewardExpander` с захардкоженной таблицей пулов / правил роллов. Покрыт тестом с `FakeSalesRandom`.
- [ ] Новый save-модуль `shop` реализован. Миграция из `decor.placement.FirstDayReward*` работает (тест на старый save → корректный новый state).
- [ ] `DecorRewardService` либо удалён, либо ужат до thin facade поверх `IShopService.BuyAsync("newspaper_decor_*")` — решение по факту анализа всех вызывающих сторон.
- [ ] `NewspaperWindow` UI содержит две секции (Books / Decor). Каждая показывает лоты соответствующего storefront'а. Кнопка `Buy` дёргает `IShopService.BuyAsync`. Sold-out лоты скрываются или disabled с пометкой.
- [ ] Toast / RewardsWindow показывает фактически выданное (`result.Granted`), не запрошенное.
- [ ] Покупка → пересохранение save → перезапуск приложения → счётчики покупок сохранились, sold-out decor не появился снова.
- [ ] Нет confirmation-диалога (по §11 решено).

**Не в скоупе Phase 0:**
- Аналитика покупок (Phase 1).
- Анимации открытия коробки.
- Иконки лотов из Addressables (если иконки есть — ок, не блокер; если нет — placeholder спрайт ОК).
- Подсветка «новое» / «лимит сегодня».
- Локализация текстов.

---

## 13. Phase plan — детально

| Phase | Скоуп | Rewards состояние | Shop состояние | Server |
|-------|-------|-------------------|----------------|--------|
| **0** | Newspaper Books (3 коробки) + Newspaper Decor (мигрирован). DoD выше. | `LocalRewardGrantService` + `BookBoxRewardExpander`. Только `Resource` и `InventoryItem` kinds. | `Game.Shop.*` со статичным конфигом, без аналитики, без диалога. | Нет. |
| **1** | Classic Shop (вкладки Books / Boxes / Decor). Daily reset (по `ICurrentDayProvider` или calendar). Аналитика (`shop_window_opened`, `shop_lot_purchased`, `shop_lot_failed`). Confirmation dialog по порогу цены. | Добавляются: `IRewardSpecProvider`, `IRewardHandler` (handler pattern), иконки для UI. | Появляется `IShopCatalogProvider` — пока всё ещё локальный, но контракт асинхронный. | Опционально. |
| **2** | Location vendors + Offers / Sale. Событийная активация лотов (`ActivationEvents`). | Появляется `ServerRewardGrantService` (опционально, флагом). `IRewardPlayerStateSyncService` для применения snapshot'ов. Idempotency key. | `IShopCatalogProvider` может загружать каталог с сервера. `BookBoxRewardExpander` удаляется — ролит сервер. | **Да**: `POST /shop/purchase`, `POST /rewards/grant`, `GET /shop/catalog`. |
| **3** | Bank (gems за реальные деньги через IAP) + BattlePass. | Добавляется `IRewardIntentService` (для рекламных и server-confirmed flow'ов). Полный snapshot-applier из Rewards-System.md. | `ShopPrice.Currency` поддерживает `"gems"` и `"inapp:<product_id>"`. IAP-флоу через отдельную ветку `BuyIapProductAsync`, как в Heroes. | IAP verification, S2S callback (как для рекламы). |
| **Late** | Crafting Supplies (если фича реставрации войдёт). | Без изменений. | Новая вкладка в Classic Shop, новая категория Inventory. | Без изменений. |

**Принцип миграции:** контракты `IShopService` и `IRewardGrantService` НЕ меняются между фазами. Меняется только реализация и конфиг. Если приходится менять контракт — это сигнал, что мы что-то упустили при дизайне Phase 0.

---

## 14. Server API placeholder (Phase 2+, TBD)

Заглушка контракта, чтобы при появлении бэкенда не начинать дизайн с нуля. Формат — черновой, не для имплементации.

```http
GET /shop/catalog?storefront_id=newspaper.books&player_id=...
→ 200 {
    "storefront_id": "newspaper.books",
    "version": 42,
    "lots": [
      {
        "lot_id": "newspaper_book_common_15",
        "price": { "currency": "gold", "amount": 20 },
        "reward_id": "book_box_common_15",
        "limit": { "mode": "unlimited" },
        "purchases": 3
      },
      ...
    ]
  }

POST /shop/purchase
body: {
  "player_id": "...",
  "lot_id": "newspaper_book_common_15",
  "idempotency_key": "uuid-..."   // обязательно
}
→ 200 {
    "status": "success",
    "granted_reward": {
      "id": "book_box_common_15",
      "items": [ {"id":"book_42","category":"book","amount":1,"kind":"InventoryItem"}, ... ]
    },
    "state_snapshot": { ... }     // delta или full — решаем при дизайне
  }
→ 409 { "status": "limit_reached" | "not_enough_currency" | "lot_inactive" }
→ 5xx → клиент ретраит с тем же idempotency_key
```

**Обязательные требования к серверной реализации (фиксируем сейчас):**
- Идемпотентность `POST /shop/purchase` через `idempotency_key`.
- Ролит book-box **сервер**, не клиент.
- Source of truth по purchase count — сервер. Клиент кеширует и применяет snapshot.
- Аналитика покупок генерируется сервером (server-side analytics), не клиентом (клиентскую можно оставить как complementary, но не как источник).

Детальная спека API — отдельный документ на старте Phase 2.
