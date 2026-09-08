# Quest Flow — фактический путь всех квестов

Что реально происходит от появления персонажа до разблокировки локации, по каждому из четырёх квестов
в `Assets/Configs/quests.json`, и какие оговорки остаются.

Это документ **состояния**, не спека. Спека механики — [QUESTS.md](QUESTS.md), модель условий активного
запроса — [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md), разблокировка локаций —
[LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md).

> **Статус на 2026-09-04.** Блокеры P1–P6 закрыты: Kids/Fact активные запросы решаемы (`sample_requests.json`),
> открытки выдаются за день (P3), у `map` есть источник в газете (P4), задан темп активных запросов
> (день 2 = 1, дефолт = 3; P5), капитан привязан к Порту (P6), открытки списываются при сдаче
> квеста Капитана (P9). Остаётся некритичная доработка P7.

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

✅ **Разрешено (P2 закрыт).** Исторически блокер был в legacy-файле `hard_requests.json`: все 7 Fact-запросов
требовали `qualities contains "History"` — качества `History` в каталоге нет ни у одной книги (у Fact-книг
есть `Historic`, 58 шт. — путаница словарей). Но рантайм активные запросы этот файл **никогда не грузил**:
модель замаплена только на `sample_requests.json` (`[ConfigFile("sample_requests")]`), и там оба Fact-запроса
(`req_fact_01`, `req_fact_02`) решаемы реальными Fact-книгами (14 и 11 совпадений по каталогу). Legacy-файл
`hard_requests.json` удалён, чтобы не вводить в заблуждение.

Регрессию стережёт `ActiveRequestValidator` (меню `Tools → Configs → Validate Active Requests` + гейт
`PreBuildValidationGate`) и тест `ActiveRequestSolvabilityTests`: оба ловят «голодающий» жанр, у которого ни
одна книга не может получить `Excellent`.

**Как считается зачёт** (важно при починке): засчитывается только `RecommendationTier.Excellent`
(`SalesDayCommitService`). После GAME-22 активная продажа засчитывается в жанр, по которому прошёл
активный запрос; `BookConfig.PrimaryGenre` остаётся fallback, когда жанр продажи не известен.

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

✅ **Разрешено (P1 закрыт), той же природы, что P2.** В legacy `hard_requests.json` все 7 Kids-запросов были
неразрешимы вдвойне: `genres contains "Horror"`/`"Tragedy"` (это вообще не жанры — их нет в `BookGenre`) плюс
`qualities contains "Fantasy"` (такого качества в каталоге 0). Но этот файл рантаймом не грузился; живой файл
`sample_requests.json` содержит решаемые Kids-запросы `req_kids_01`, `req_kids_02` (20 и 3 совпадения). Файл
`hard_requests.json` удалён. Регрессию стережёт тот же `ActiveRequestValidator` + тест
`ActiveRequestSolvabilityTests` (см. блок Милли).

---

## 4. Капитан — `q_captain_postcards` 🟡

| | |
|---|---|
| **Вызов** | `activationConditions: { visitLocation loc_port, min 1 }` — активация **до** диалога |
| **Спавн** | `captain_quest_intro` с `activationQuestId`; приходит, пока квест `Active`, один раз (fire-once по `captain_quest_1`) |
| **Задача** | `haveItem` `postcard` 10 |
| **Награда** | `captain_recommendation` (quest_item) + `ship` (decor) |
| **Дальше** | `captain_recommendation` ×1 + `fuel_canister` ×5 + `soldTotal` 200 → **Рынок** |

✅ Корабль выдаётся только квестом — в `shop.json` лота нет.
✅ `soldTotal` для Рынка существует и зарегистрирован (`SoldTotalConditionFactory`).
✅ Открытки выдаются за завершение дня: `economy.json.dayCompletionRewards` (`postcard` ×1).
✅ Капитан привязан к Порту через `CustomerScriptConfig.LocationId`.
✅ Открытки списываются при сдаче квеста: `q_captain_postcards.costs` требует `postcard` ×10, а
`QuestRewardGranter` списывает cost один раз после успешной выдачи награды.

🟡 **Активация с задержкой.** `QuestsService.Subscribe()` подписан на sales / decor / inventory /
dayProgress и **не на посещения локаций**. Визит фиксируется при входе, но квест активируется на следующей
переоценке — практически в тот же день при первой продаже.

---

## 5. Цепочка локаций целиком

| Локация | `unlockCost` | `unlock` (условия) | Даёт квест | Статус |
|---|---|---|---|---|
| **Парк** | — | — | — | ✅ открыт сразу |
| **Кампус** | `millie_letter` ×1, `fuel_canister` ×2 | — | Милли | 🔴 упирается в P2 |
| **Порт** | `port_trade_permit` ×1, `fuel_canister` ×2 | — | Тара | 🔴 упирается в P1 |
| **Рынок** | `captain_recommendation` ×1, `fuel_canister` ×5 | `soldTotal` 200 | Капитан | ✅ достижим |
| **Деревня** | `map` ×1, `fuel_canister` ×15 | `soldGenre` Fantasy 150, Kids 150 | — | ✅ достижима |

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
| **P1** ✅ | Kids-запросы решаемы. Неразрешимые жили в legacy `hard_requests.json`, который рантайм не грузил; живой `sample_requests.json` содержит валидные Kids-запросы. Legacy-файл удалён | — | Закрыто: `sample_requests.json` + тест `ActiveRequestSolvabilityTests` |
| **P2** ✅ | Fact-запросы решаемы. Та же природа, что P1 — блокер был только в удалённом legacy `hard_requests.json` | — | Закрыто: `sample_requests.json` + тест `ActiveRequestSolvabilityTests` |
| **P3** ✅ | Открытки выдаются: `economy.json.dayCompletionRewards` (postcard ×1) начисляется за каждый завершённый день в `SalesDayCommitService.GrantDayCompletionRewardsAsync` (идемпотентно, покрыто тестами) | — | Закрыто: `economy.json` + `SalesDayCommitService` |
| **P4** ✅ | У `map` есть источник: лот `newspaper_quest_item_map` в `shop.json` (200 gold, Disposable) выдаёт `map` (quest_item) для открытия Деревни | — | Закрыто: `shop.json` |
| **P5** ✅ | Темп активных запросов задан: день 1 = 0, день 2 = 1 (`days.json`), день 3+ = 3 (`SalesTraffic.asset._defaultActiveRequestCount`) | — | Закрыто: `days.json` + `SalesTraffic.asset` |
| **P6** ✅ | Капитан привязан к Порту: добавлено поле `CustomerScriptConfig.LocationId`, скрипт `captain_quest_intro` помечен `locationId: loc_port`, `ScriptedCustomerSpawner.IsEligible` фильтрует по `setup.LocationId` | — | Закрыто: `CustomerScriptConfig` + `customer_scripts.json` + `ScriptedCustomerSpawner` |
| **P7** 🟡 | Активация по `visitLocation` с задержкой | квест появляется не в момент входа | `QuestsService.Subscribe()` |
| **P8** ✅ | `activePickGenre` считал `genres[0]`, а условие запроса — весь массив `genres` | двужанровая книга давала «отлично», но не засчитывалась в квест | Закрыто GAME-22: активная продажа фиксирует жанр запроса |
| **P9** ✅ | Открытки списываются при сдаче квеста Капитана через `QuestConfig.Costs` и `QuestRewardGranter` | — | Закрыто: `quests.json` + `QuestRewardGranter` |

**Порядок разбора.** P1–P6 и P9 закрыты. Остаётся мелкая доработка P7 (задержка активации по
`visitLocation`) — на проходимость цепочки она не влияет.

Против повторения P1/P2 стоит гейт: `Tools → Configs → Validate Active Requests` и автоматическая
проверка на билде (`PreBuildValidationGate`, см. [BUILD.md §0](BUILD.md)), плюс тест
`ActiveRequestSolvabilityTests`. Все трое ловят и нерешаемый запрос, и «голодающий жанр», у которого ни одна
книга не может получить `Excellent`.

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
