# Quest Flow — фактический путь всех квестов

Что реально происходит от появления персонажа до разблокировки локации, по каждому из четырёх квестов
в `Assets/Configs/quests.json`, и где путь сейчас рвётся.

Это документ **состояния**, не спека. Спека механики — [QUESTS.md](QUESTS.md), модель условий активного
запроса — [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md), разблокировка локаций —
[LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md).

> **Статус на 2026-07-30.** Из четырёх квестов проходим **один** (Эдди). Три блокера — в реестре ниже.

Легенда: ✅ работает · 🟡 работает с оговоркой · 🔴 блокер

---

## 0. Общий конвейер

Все квесты идут по одному пути; отличаются только шагом 1–2.

```
CustomerScriptConfig            ScriptedCustomerSpawner отбирает скрипт:
 (dayIndex | activationQuestId)  ровно одно из двух полей, плюс fire-once по dialogueId
        │
        ▼
QuestCharacterArchetype  ──▶  DialogStep (держит interaction lock)
        │
        ▼
IDeliveredDialoguesService.IsDelivered(dialogueId) ──▶ activationConditions: dialogueDelivered
        │                            (DialoguePresenter marks delivered, then requests quest re-evaluation)
        ▼
QuestsService: Pending ─▶ Active ─▶ (все задачи Completed) ─▶ ReadyToAward ─▶ Awarded (auto)
        │
        ├─ переоценка pull-based: подписки на sales / decor / inventory / dayProgress.PhaseChanged
        └─ baseline: для sales-условий счётчик фиксируется в момент активации задачи
                     (MaybeCaptureBaseline) → нужно N продаж ПОСЛЕ выдачи квеста, не суммарно
        ▼
QuestRewardBridge (ISaveHook.BeforeSaveAsync) ──▶ IRewardGrantService
        │  выдаёт награды при Awarded, ledger в "quest_rewards.granted" (без повторов)
        │  ⚠ выдача происходит на ПЕРВОМ сохранении после Awarded, не мгновенно
        ▼
Предмет в инвентаре ──▶ LocationConfig.unlockCost ──▶ кнопка «Разблокировать» в LocationWindow
                                                      TryUnlockAsync списывает предметы
```

**Важно про награды-«доступы».** Ни один квест не применяет world-effect. «Открывает доступ к локации» — это
обычный `quest_item` в инвентаре, а локация проверяет его через `unlockCost`. Обработчиков
`QuestWorldEffectConfig` в проекте нет вообще, поле `worldEffects` у всех квестов пустое.

**Порядок локаций.** Парк открыт сразу (нет `unlock`/`unlockCost`) и должен идти **первым** в
`locations.json` — `PickFirstUnlockedLocationId` берёт первую открытую в порядке каталога.

---

## 1. Эдди — `q_intro_eddi` ✅

| | |
|---|---|
| **Вызов** | `customer_scripts.json` → `eddi_quest_intro`, `dayIndex: 2`, персонаж `eddi` |
| **Активация** | `activationConditions: { type: dialogueDelivered, dialogueId: eddy1 }` |
| **Задачи** | `soldGenre` Crime 10 · Drama 10 · Classic 10 · Fantasy 15 |
| **Награда** | `fuel_canister` ×2 (consumable) |
| **Дальше** | канистры идут в `unlockCost` любой локации |

**Проходим.** Единственное, что могло его убить, — жёсткий гейт по спросу локации, но его нет:
`SalesTuningDemandGenreWeightProvider.GetWeight` даёт повышенный вес профильным жанрам и **1.0 всем
остальным**, `EconomyBasedSaleChanceCalculator` устроен так же. Crime и Fantasy продаются в Парке, просто
медленнее.

🟡 **Оговорка — объём.** Из-за baseline нужно 45 целевых продаж **после дня 2**. Crime в спросе только у
Порта и Рынка, то есть до их открытия он идёт на базовом весе. Это вопрос темпа, не проходимости.

---

## 2. Милли — `q_intro_millie` 🔴

| | |
|---|---|
| **Вызов** | `millie_intro`, `dayIndex: 2`, персонаж `millie` |
| **Активация** | `activationConditions: { type: dialogueDelivered, dialogueId: millie1 }` |
| **Задача** | `activePickGenre` Fact 5 — пять **отличных** активных рекомендаций Fact-книгой |
| **Награда** | `millie_letter` (quest_item) + `fuel_canister` ×1 |
| **Дальше** | `millie_letter` ×1 + `fuel_canister` ×2 → **Кампус** |

🔴 **Блокер: ни одна Fact-книга не может получить «отлично».** Все 7 Fact-запросов в `hard_requests.json`
идентичны по условиям — `genres contains "Fact"` **AND** `qualities contains "History"`. Качество `History`
есть у 9 книг, все жанра Classic/Drama; у Fact-книг его нет ни у одной. Ближайшее — `Historic` (1 книга),
то есть в контенте опечатка плюс путаница словарей: описание `req_hard_30` говорит «popular-science», а
условие требует историю.

Реально лежит на Fact-книгах: `Pop Science` (5), `Non Fiction` (4), `Nature` (4), `Space` (2),
`Historic` (1), `Biography` (1), `Political` (2), `Cooking` (1).

**Как считается зачёт** (важно при починке): засчитывается только `RecommendationTier.Excellent`
(`SalesDayCommitService`), и жанр берётся у **выбранной книги** через `BookConfig.PrimaryGenre`, то есть
**только `genres[0]`** — см. проблему **P8**.

---

## 3. Тара — `q_tara_kids` 🔴

| | |
|---|---|
| **Вызов** | `tara_quest_intro`, `dayIndex: 5`, персонаж `tara` |
| **Активация** | `activationConditions: { type: dialogueDelivered, dialogueId: tara_quest_1 }` |
| **Задача** | `activePickGenre` Kids 5 |
| **Награда** | `port_trade_permit` (quest_item) + `lavender` (decor) |
| **Дальше** | `port_trade_permit` ×1 + `fuel_canister` ×2 → **Порт** |

✅ Механика вызова в порядке. `DayConfig` для дня 5 не нужен — `IsEligible` сравнивает `script.DayIndex`
с `setup.Day`, а трафик и число активных запросов берутся из дефолтов `SalesTrafficSettings`. Диалог без
`passiveAttempts` не занимает слот покупателя.

✅ Лаванда выдаётся только квестом: лот `newspaper_decor_lavender` удалён из `shop.json`. Отдельный флаг
«скрыть» не нужен — витрина строится **только** из лотов `shop.json` (`ShopOfferSource.BuildOffers`),
`decors.json` её не наполняет.

🔴 **Блокер, тот же по природе, что у Милли: ни одна Kids-книга не может получить «отлично».** Все 7
Kids-запросов требуют `qualities contains "Fantasy"`. Качество `Fantasy` есть у 6 книг — все Crime/Classic;
у Kids-книг его нет. Автор снова подставил **название жанра в слот качества**.

Реально лежит на Kids-книгах: `Kids` (все 10), `Animals` (3), `Humor` (2), `Nature` (2),
`Coming of Age` (2), `Magic` (1), `Philosophical` (1), `Contemporary` (1).

---

## 4. Капитан — `q_captain_postcards` 🔴

| | |
|---|---|
| **Вызов** | `activationConditions: { visitLocation loc_port, min 1 }` — активация **до** диалога |
| **Спавн** | `captain_quest_intro` с `activationQuestId`; приходит, пока квест `Active`, один раз (fire-once по `captain_quest_1`) |
| **Задача** | `haveItem` `postcard` 10 |
| **Награда** | `captain_recommendation` (quest_item) + `ship` (decor) |
| **Дальше** | `captain_recommendation` ×1 + `fuel_canister` ×5 + `soldTotal` 200 → **Рынок** |

✅ Корабль выдаётся только квестом — в `shop.json` лота нет.
✅ `soldTotal` для Рынка существует и зарегистрирован (`SoldTotalConditionFactory`).

🔴 **Блокер: открытки никто не выдаёт.** Предмет `postcard` заведён в `consumables.json` (категория
`consumable` = Stack, поэтому `GetCount` считает штуки и `min: 10` отработает), но механики «1 открытка за
завершение игрового дня» не существует. Валидатор ссылок это и сообщает:
`Item 'postcard' (consumable) is not granted by any quest reward or shop lot`.

🟠 **Капитан появится не обязательно в Порту.** У `CustomerScriptConfig` нет поля локации — `IsEligible`
требует ровно одно из `dayIndex` / `activationQuestId` и локацию не учитывает. Он придёт на первом
отыгранном дне после активации квеста, на любой локации.

🟡 **Активация с задержкой.** `QuestsService.Subscribe()` подписан на sales / decor / inventory /
dayProgress и **не на посещения локаций**. Визит фиксируется при входе, но квест активируется на следующей
переоценке — практически в тот же день при первой продаже.

🟡 **Открытки не списываются.** «Собрать для него 10 открыток» — `haveItem` только проверяет наличие.
Механики «отдать предметы за квест» нет (списание есть только у `unlockCost` локаций). После награды
10 открыток останутся в инвентаре. `CanBeReset` = `false`, так что потратить их позже задачу не откатит.

---

## 5. Цепочка локаций целиком

| Локация | `unlockCost` | `unlock` (условия) | Даёт квест | Статус |
|---|---|---|---|---|
| **Парк** | — | — | — | ✅ открыт сразу |
| **Кампус** | `millie_letter` ×1, `fuel_canister` ×2 | — | Милли | 🔴 упирается в P2 |
| **Порт** | `port_trade_permit` ×1, `fuel_canister` ×2 | — | Тара | 🔴 упирается в P1 |
| **Рынок** | `captain_recommendation` ×1, `fuel_canister` ×5 | `soldTotal` 200 | Капитан | 🔴 упирается в P3 |
| **Деревня** | `map` ×1, `fuel_canister` ×15 | `soldGenre` Fantasy 150, Kids 150 | — | 🔴 у `map` нет источника |

Канистры покупаются в магазине — лот `newspaper_consumable_fuel_canister`, витрина
`newspaper.consumables`, `limit.mode: Unlimited` (для Деревни нужно 15, `Disposable` сделал бы её
недостижимой).

Механика ручной разблокировки рабочая целиком: `LocationRowView._unlockButton` →
`LocationWindow.OnUnlockClicked` → `TryUnlockAsync` (проверяет весь список `GetCount` до первого
`RemoveAsync`), окно подписано на `IInventoryService.Changed`, а `CollectNewlyUnlocked` пропускает локации
с ценой (`if (HasCost(id))`) — платная локация сама не откроется.

---

## 6. Реестр проблем

| # | Проблема | Влияние | Где |
|---|---|---|---|
| **P1** 🔴 | Все 7 Kids-запросов нерешаемы (`qualities contains "Fantasy"`) | Тара непроходима → Порт, затем Рынок и Деревня | `hard_requests.json` |
| **P2** 🔴 | Все 7 Fact-запросов нерешаемы (`qualities contains "History"`) | Милли непроходима → Кампус | `hard_requests.json` |
| **P3** 🔴 | Открытки не выдаются | Капитан непроходим → Рынок | нет механики |
| **P4** 🔴 | У `map` нет источника | Деревня недостижима | дизайн не определён |
| **P5** 🟠 | `days.json` обрывается на дне 2; у дней 1–2 `activeRequestCount: 0` при `applyModifiers: false` (жёсткий override), с дня 3 берётся `SalesTrafficSettings.DefaultActiveRequestCount` = 1 | обе `activePickGenre`-задачи стартуют на ~1 запросе в день; в дни выдачи прогресс невозможен физически | `days.json` |
| **P6** 🟠 | Капитан спавнится на любой локации | расхождение с ТЗ «заход в Порт» | нет `locationId` в `CustomerScriptConfig` |
| **P7** 🟡 | Активация по `visitLocation` с задержкой | квест появляется не в момент входа | `QuestsService.Subscribe()` |
| **P8** 🟡 | `activePickGenre` считает `genres[0]`, а условие запроса — весь массив `genres` | двужанровая книга даст «отлично», но не зачтётся в квест | GAME-22 в [TODO.md](TODO.md) |
| **P9** 🟡 | Открытки не списываются при сдаче квеста | предметы остаются в инвентаре | нет механики |

**Порядок разбора.** P5 первым — он корень темпа для P1/P2 и вообще всей прогрессии после дня 2. Затем
P1/P2 (правка данных, самая дешёвая), затем P3. P6–P9 — доработки.

Против повторения P1/P2 стоит гейт: `Tools → Configs → Validate Active Requests` и автоматическая
проверка на билде (`PreBuildValidationGate`, см. [BUILD.md §0](BUILD.md)). Он ловит и нерешаемый запрос, и
«голодающий жанр», у которого ни одна книга не может получить `Excellent`.

---

## 7. Как проверить квест читом, не проходя его

Чит-панель (кнопка в HUD, `_useDebugFeatures = 1` в `BootstrapInstaller.asset`):

| Нужно | Модуль | Что делает |
|---|---|---|
| Открыть диалог и выдать квест | `DialogueCheatModule` | открывает любой граф из `dialogues.json` |
| Прогресс `activePickGenre` | `SalesStatsCheatModule` | зовёт `RecordActivePick` напрямую — обходит P1/P2 |
| Прогресс `soldGenre` / `soldTotal` | `SalesStatsCheatModule` | `RecordSold` по книге |
| Открытки, канистры | `ConsumableCheatModule` | ввод количества (Stack-категория) |
| Квестовые предметы | `QuestItemCheatModule` | Add/Remove по `quest_items.json` |
| Декор | `DecorationCheatModule` | Add/Remove по `decors.json` |
| Открыть мини-игру запроса | `ActiveSaleCheatModule` | конкретный запрос из `hard_requests.json` |

Награды выдаются в `BeforeSaveAsync`, поэтому после auto-award нужно дождаться сохранения — быстрее всего
завершить день.
