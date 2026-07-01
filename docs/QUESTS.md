# Quests

Спека будущей фичи квестов для MyBookstore. Основана на разборе продовой quest-системы из `heroes`: цепочки персонажей, продажи книг нужных жанров/тегов, декор как условие, исследование локаций и постоянные последствия в мире.

Целевое имя новой runtime-сборки: `Game.Quest` (в едином стиле с `Game.Inventory`, `Game.Decor`, `Game.SalesStats`). Старый placeholder `Assets/Game/Features/Quest/Quest.asmdef` (`name: "Quest"`, пустой) **удалён**.

> **Статус (2026-06-29): MVP-ядро реализовано и поставлено на паузу.**
> Готово: GAME-4 (продажи по локации/дню), сборки `Game.Quest`/`.API`/тесты, API+enum+конфиги, условия
> `decorEquipped`/`haveItem`/`weatherIs` (+продажные `soldGenre*`), `QuestsService` (lifecycle/цепочки/auto-award),
> save (Этап 5, Awarded/Failed не переигрываются), baseline «после старта задачи» (Этап 4b).
> В **следующих итерациях**: награды + permanent effects (GAME-3/Этап 6), визиты `visitLocation`/`locationIs`
> (GAME-5), реальная цепочка-слайс `quests.json` (Этап 7), UI журнала/HUD, персонажи.
> Принятые решения зафиксированы в [ADR-0007](adr/0007-quest-system.md).

---

## 0. Prerequisites (блокеры до старта реализации)

Перед `GAME-2` нужно закрыть зависимости, которых в коде сейчас нет:

1. **Продажи по локациям и по дням (БЛОКЕР).** Текущий `Game.SalesStats` (`ISalesStatsReader`) хранит только глобальный кумулятив: `GetSold(BookGenre)` и `TotalSold` — без разреза по локации и без дневного счётчика. Условия вида «продать 15 `Fantasy` **на `FarBeach`**» и «продать 15 за **один день**» невозможны, пока `SalesStats` не научится считать продажи по `(локация)` и `(день)`. Это нужно сделать **до** реализации квестов. См. TODO `GAME-4`.
2. **Сезоны — вне MVP.** Сезонной системы (`Season`/`Spring`/`Summer`/`Autumn`/`Winter`) в проекте нет, и в ближайший MVP она **не входит**. Поэтому квесты MVP **не имеют зависимости на сезон**: условие `SeasonIs` пока не реализуется, а «сезонные пики» заменяются доступными триггерами (день/погода/прогресс продаж). Когда сезоны появятся, `SeasonIs` добавляется как ещё один condition-factory без изменения модели квестов.
3. **Погода — уже есть.** Погода доступна per-day как `MorningDayContext.WeatherId` / `ActiveModifierIds` (`"weather_clear"` и т.п.), так что `WeatherIs` реализуем сразу.

## 1. Что берем из продовой системы

Из `heroes-quest-system.md` полезно перенести не конкретные классы один-в-один, а архитектурные решения:

| Идея | Как использовать в MyBookstore |
|---|---|
| `QuestState`: `Pending`, `Active`, `ReadyToAward`, `Awarded`, `Failed` | Базовая машина состояний квеста. Для cozy-flow `ReadyToAward` полезен как момент показа финального диалога/награды, даже если награда потом выдается автоматически. |
| `QuestTaskState`: `Pending`, `Active`, `Completed`, `Failed` | Каждый этап цепочки должен быть отдельной задачей с прогрессом: продать N книг, посетить локацию, поставить декор, дождаться сезона/погоды. |
| Data-driven `QuestDbData` / `QuestTaskDbData` | Квесты должны быть конфигами, а не зашитой логикой. Минимум: id, title, description, chain id, tasks, activation conditions, completion conditions, rewards/effects. |
| `ActivationEvents`, `CompleteEvents`, `FailEvents` | Вместо жёстких вызовов из разных фич — единая модель условий. В MyBookstore она **уже есть**: `Game.Conditions` (`IConditionParser` → `ICondition.Evaluate()` → `ConditionResult`, композиты `AllOf/AnyOf/Not`, per-feature `IConditionFactory`). Квесты переиспользуют её как есть — см. §11. |
| `QuestsToBornOnComplete` | Нужна последовательная цепочка: завершение одного квеста активирует следующий. Для сюжетных арок это основной механизм. |
| `IQuestChain.CurrentQuest` | UI и gameplay должны быстро понимать текущий активный этап цепочки, не обходя все квесты вручную. |
| Save-модель, где `Pending` не сохраняется | Экономит save и упрощает миграции: сохраняем только активные, завершенные и проваленные/заблокированные состояния. |
| Сигналы изменения состояний | Другие фичи смогут реагировать на `QuestStarted`, `QuestTaskCompleted`, `QuestAwarded`: unlock локаций, бонусы спроса, декор-награды, газетные события. |
| Watching/Tracking | Полезно для HUD: игрок может закрепить активную цепочку, особенно сезонную, чтобы видеть прогресс. |
| ViewModel не источник истины | UI должен кэшировать отображение, но состояния квестов живут в сервисе/save. |

Что не переносим на старте:

- `LootBox` как обязательную систему наград. В MyBookstore награды чаще конкретные: декор, permanent modifier, unlock, ресурс, newspaper/event flag.
- `Achievement`, `Mission`, `BattlePass` типы. Для первой версии достаточно `Story`, `Side`, `Tutorial`.
- NodeCanvas-ноды. В текущем проекте лучше сначала сделать чистую domain/application модель и DI-регистрацию, а редакторские инструменты добавить позже.
- Жесткую зависимость на персонажей. Персонажей пока нет, поэтому `characterId` должен быть optional/future field.

---

## 2. Целевая роль фичи

Фича `Quests` отвечает за:

- хранение состояния квестов и задач;
- запуск цепочек по условиям;
- подсчет прогресса задач;
- выдачу наград и постоянных эффектов после завершения;
- публикацию событий для UI, локаций, продаж, декора, инвентаря и прогрессии.

Система должна поддерживать N персонажей, у каждого M цепочек. Пока персонажей нет, цепочки считаются обезличенными:

```text
QuestChain
  id: "chain_01"
  characterId: null
  quests: ["quest_01_a", "quest_01_b", "quest_01_c", "quest_01_finale"]
```

Когда появится фича персонажей, `characterId` станет ссылкой на персонажа, но состояние и цепочки менять не придется.

---

## 3. Предлагаемая asmdef-структура

Целевое имя новой сборки: `Game.Quest` — единый стиль с остальными фичами проекта (`Game.Inventory`, `Game.Decor`, `Game.SalesStats`, `Game.Conditions`, `Game.Rewards`). Старый placeholder `Game.Features/Quest/Quest.asmdef` (`name: "Quest"`) удалён.

Минимальный вариант на первую итерацию:

```text
Assets/Game/Features/Quest/
  API/
    Game.Quest.API.asmdef
  Game.Quest.asmdef
  Tests/Editor/
    Game.Quest.Tests.Editor.asmdef
```

Рекомендуемые зависимости:

| Сборка | Назначение | References |
|---|---|---|
| `Game.Quest.API` | публичные интерфейсы, DTO, enum состояния | `com.cysharp.unitask` при необходимости |
| `Game.Quest` | сервисы, save hook, condition adapters, DI bindings | `Game.Quest.API`, `Infrastructure`, `Game.Conditions.API`, `Game.Inventory.API`, `Game.Decor.API`, `Game.SalesStats.API`, `Game.LocationEntry.API`, `Game.LocationUnlock.API`, `Game.Rewards.API`, `VContainer`, `VContainer.Unity` |
| `Game.Quest.Tests.Editor` | EditMode-тесты машины состояний и прогресса | `Game.Quest`, `Game.Quest.API`, `nunit.framework.dll` |

Если доменная логика быстро станет большой, можно разнести на `Game.Quest.Domain` и `Game.Quest.Application`, но первая версия может быть средней фичей по правилам `docs/ASMDEF_RULES.md`.

---

## 4. Данные квестов

Реализованная модель конфига (`Game.Configs/Models`, JSON `quests.json`). Условия — **одно `JObject`-дерево** на поле (как `LocationConfig.Unlock`), `Type` — строка (`Game.Configs` не ссылается на `Game.Quest.API`):

```csharp
[ConfigFile("quests")]
QuestConfig
{
    string Id;
    string Type;        // "story" | "side" | "tutorial" (парсится QuestTypeExtensions)
    string ChainId;
    string CharacterId; // null до появления персонажей
    string TitleKey;
    string DescriptionKey;
    string[] NextQuestIds;        // MVP: 0 или 1 (линейная цепочка)
    QuestTaskConfig[] Tasks;
    JObject ActivationConditions; // дерево условий (null/empty = авто-активный head)
    JObject FailConditions;       // null = квест не фейлится
    QuestRewardConfig[] Rewards;
    QuestWorldEffectConfig[] WorldEffects;
}
```

Задача:

```csharp
QuestTaskConfig
{
    int Id;
    string DescriptionKey;
    JObject CompletionConditions; // null/empty = задача завершается сразу при активации
    JObject ActivationConditions; // null/empty = активна вместе с квестом
    bool CanBeReset;              // в MVP игнорируется (Completed не откатывается)
}
```

Награда и эффект (POCO в `Game.Configs/Models`, маппинг в рантайме — Этап 6):

```csharp
QuestRewardConfig      { string Kind; string Id; string Category; int Amount; }  // Kind → Game.Rewards.API.RewardKind
QuestWorldEffectConfig { string Type; JObject Params; }                            // применяется effect-хендлером
```

Для сюжетных квестов нужны типовые условия. Каждое — это `IConditionFactory` (`type` + `Create(JObject)`), регистрируемый владеющей фичей в DI, ровно как существующий `SoldGenreConditionFactory` (`"soldGenre"`). Имена локаций/декора/предметов в примерах — плейсхолдеры.

| Условие | Пример | Статус |
|---|---|---|
| `soldGenre` | продать N книг жанра (глобально) | **есть** — `SoldGenreCondition` |
| `soldGenreAtLocation` | продать 15 `Fantasy` на `location_01` | **готово** (GAME-4) — `Game.SalesStats` |
| `soldGenreInSingleDay` | продать 15 `Fantasy` за один день | **готово** (GAME-4) — `Game.SalesStats` |
| `soldByTags` | продать книги с тегами `{nature, academic}` жанра `Fact` | требует учёта продаж по `Tags` (есть `BookConfig.Tags/Mood`) — см. §12 |
| `decorEquipped` | установить `decor_donation_box` / `decor_fireplace` | **готово** (Этап 3) — `Game.Decor` (`IDecorPlacementService`) |
| `haveItem` | найден квест-предмет | **готово** (Этап 3) — `Game.Inventory` (`Has/GetCount`) |
| `weatherIs` | дождь, шторм, снег, солнце | **готово** (Этап 3) — `Game.DayCycle` (`ICurrentDayWeatherProvider` поверх `IMorningContextResolver`) |
| `visitLocation` | посетить `location_01` 3 раза | **GAME-5** — нужна persisted-подсистема визитов |
| `locationIs` | находиться на `location_01` | **GAME-5** — нужен current-location seam |
| `decorEquippedForDays` | держать `decor_fireplace` 3 игровых дня | позже (нужен дневной таймер декора) |
| `collectResourceBySales` | собрать 100 монет через копилку | позже (механика копилки) |
| `clickWorldObject` | интерактивный объект мира | позже |
| `consumeItem` | израсходовать квест-предмет | позже (quest items) |
| ~~`seasonIs`~~ | ~~сезон~~ | **вне MVP** — сезонов нет (Prereq §0). |
| `pinInvestigationCase` | дело на доске расследований | отложено до Investigation Board |

---

## 5. Состояния

Квест:

```text
Pending -> Active -> ReadyToAward -> Awarded
              |
              v
            Failed
```

Задача:

```text
Pending -> Active -> Completed
              |
              v
            Failed
```

Правила:

- `Pending` не пишется в save.
- `Active` и `ReadyToAward` сохраняют полный прогресс задач (per-task state). Завершённые задачи восстанавливаются из save, а не переоцениваются → реверсия немонотонного условия (декор сняли, погода сменилась) прогресс **не откатывает**.
- `Awarded` сохраняет **только id** (Этап 5). `timestamp` и `applied effects` добавятся в Этап 6 (со сменой `StateSchemaVersion`).
- `Failed` нужен редко: например, пропущенные one-shot события. Для Active-квеста **fail имеет приоритет** над completion. Сезонного ожидания нет (сезоны вне MVP).
- `CanBeReset` (для задач «держать декор N дней» / «выбирать солнечные локации 7 дней») в модели есть, но **в MVP игнорируется** — Completed-задачи не откатываются. Полноценный rollback — позже.
- **Auto-award (MVP):** когда все задачи `Completed`, квест проходит `ReadyToAward` → `Awarded` автоматически (события `QuestCompleted`, затем `QuestAwarded`); `NextQuestIds` активируются после `Awarded`. `TryAwardAsync` — явный идемпотентный путь.

---

## 6. Permanent effects

Логика «изменение мира после финала» ценна не только наградой. Для этого у квеста должны быть постоянные эффекты, применяемые один раз при `Awarded`.

Примеры эффектов:

| Effect | Что меняет |
|---|---|
| `LocationCustomerBonus` | +N клиентов в день на локации |
| `UnlockLocation` | открывает новую локацию или подлокацию |
| `UnlockInvestigation` | открывает глобальный квест/доску расследований |
| `GenrePriceMultiplier` | +X% к цене жанра при условиях: вечер, локация, погода |
| `CustomerArchetypeWeight` | повышает шанс редких покупателей, романтиков, писателей, туристов |
| `UnlockDecor` | добавляет уникальный декор в инвентарь |
| `VanVisualUpgrade` | постоянный визуальный апгрейд фургона |
| `LocationVisualState` | меняет вид локации: достроенный замок, включенный маяк, цветущий сад |

Важно: permanent effects должны быть идемпотентными. Повторная загрузка save не должна повторно начислять декор, золото или модификаторы.

---

## 7. Примеры цепочек

> **MVP-замечание.** Сезонных гейтов в MVP нет (Prereq §0): сезонные «пики» заменены доступными триггерами — погодой (`weatherIs`), продажей «за один день» и прогрессом продаж. Все id (`location_NN`, `decor_*`, `quest_*`) и теги в таблицах — **плейсхолдеры**; в коде жанр ограничен `BookGenre` (`Classic/Crime/Drama/Fact/Fantasy/Kids/Travel`), а тонкая выборка «особенных» книг делается по `BookConfig.Tags`/`Mood` (см. §12), а не по RPG-редкости.

### Цепочка A — строительство объекта на локации (world-state)

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| intro | посетить `location_01` 3 раза | открыть следующую задачу |
| ramp | продать 15 `Fantasy` на `location_01` | декор `decor_donation_box` |
| funding | установить копилку и накопить 100 монет с продаж | открыть финал |
| finale | продать 15 `Fantasy` за один день на `location_01` (`soldGenreInSingleDay`) | `LocationCustomerBonus(location_01, +2)`, `UnlockLocation(location_02)`, старт глобального квеста |

### Цепочка B — детективная ветка (доска расследований)

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| intro | посетить `location_03` 3 раза | открыть место события |
| evidence | закрепить дело на доске, найти квест-предмет, получить подсказку | открыть демонстрацию улики |
| display | повесить улику как декор и приехать на `location_04` | вызвать свидетеля |
| finale | в снежный день (`weatherIs: snow`) иметь активное дело и выставленную улику | декор-награда, продвижение связанной ветки |

### Цепочка C — атмосферная ветка (погода + длительный декор)

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| open | открыть `location_05` | старт цепочки |
| tags | продать 10 книг с тегами `{maritime, history}` на `location_05` (`soldByTags`) | NPC начинает диалог |
| weather | в шторм (`weatherIs: storm`) кликнуть интерактивный объект | квест-предмет |
| sustain | держать `decor_fireplace` 3 игровых дня | восстановленный предмет |
| finale | вернуть предмет NPC | `GenrePriceMultiplier(Crime, +20%, evening/coast)`, ночная торговля, декор-награда |

---

## 8. Интеграции с текущими фичами

| Фича | Зачем нужна квестам |
|---|---|
| `DayCycle` | день, погода (`WeatherId`/`ActiveModifierIds`), длительность условий, daily reset. **Сезонов нет** — вне MVP (Prereq §0). |
| `Book.Sell` / `Game.SalesStats` | продажи по жанру, локации, дню, редкости |
| `LocationEntry` | визиты на локации, текущая локация |
| `LocationUnlock` | открытие новых мест и подлокаций |
| `Decor` | экипировка декора, декор-награды, визуальные апгрейды |
| `Inventory` | квестовые предметы и книги |
| `Rewards` | выдача декора/ресурсов/книг через общую наградную модель |
| `Conditions` | единый язык условий для активации/завершения |
| `Newspaper` | подсказки про погоду, слухи и сезонные окна |
| `GameplayUI` | журнал квестов, HUD-tracking, progress counters |

---

## 9. Первая реализация — статус

Минимальный vertical slice:

0. ✅ **Prereq (GAME-4):** `Game.SalesStats` считает продажи по локации и по дню.
1. ✅ Сборки `Game.Quest.API`, `Game.Quest`, `Game.Quest.Tests.Editor`.
2. ✅ `QuestState`, `QuestTaskState`, `QuestType` (+ extensions).
3. ✅ `QuestsService` с конфигами, задачами и цепочками; условия — через `IConditionParser`.
4. ✅ Save DTO + hook (`SavedQuests`, `SaveBackedQuestsRepository`): Active/RTA + терминалы; auto-award; idempotent restore.
5. ✅ Condition-factory: `soldGenre` / `soldGenreAtLocation` / `soldGenreInSingleDay` / `weatherIs` / `decorEquipped` / `haveItem`. ⏳ `visitLocation` → **GAME-5**.
6. ✅ baseline scoped-reader (прогресс «после старта задачи») — **Этап 4b** (`ISalesStatsBaselineSource` + scoped re-parse задачи + persist baseline).
7. ⏳ Награды и permanent effects (грант + `WorldEffectConfig`-хендлеры, идемпотентность) — **Этап 6 / GAME-3**.
8. ⏳ Реальная цепочка `An Empire of Sand` (`quests.json`) + end-to-end тесты — **Этап 7** (ждёт `visitLocation` из GAME-5).
9. ⏳ Условия визита `visitLocation`/`locationIs` — **GAME-5** (persisted-подсистема визитов).

---

## 10. Открытые вопросы

- ✅ **Решено:** источник конфигов — JSON `quests.json` в `Game.Configs` (`[ConfigFile("quests")]`), не ScriptableObject.
- ✅ **Решено:** триггер пере-оценки — доменные сигналы `ISalesStatsService.Changed`, `IDecorPlacementService.PlacementChanged`, `IInventoryService.Changed`, `IDayProgressService.PhaseChanged` (подписки в `QuestsService`).
- Нужен ли отдельный журнал квестов сразу, или достаточно HUD/debug-вывода до появления UI?
- Должны ли квестовые предметы быть обычными `InventoryItem`, или нужен отдельный `QuestItem` category?
- Как оформлять детективные цепочки до появления полноценной Investigation Board?

---

## 11. Переиспользование существующего кода условий

В проекте уже есть полноценный data-driven condition-движок `Game.Conditions`, и его **не нужно** трансформировать в систему квестов — квесты строятся **поверх** него.

Что уже готово и переиспользуется как есть:

| Элемент | Файл | Роль для квестов |
|---|---|---|
| `ICondition` / `ConditionResult` | `Conditions/API` | Лист/узел условия с прогрессом (`Current`/`Target`/`Children`) — прямо ложится на прогресс задач и UI «23/30». |
| `IConditionFactory` + `IConditionFactoryRegistry` | `Conditions/API` | Каждое квестовое условие = новый factory, регистрируемый своей фичей в DI. |
| `IConditionParser` | `Conditions/Services` | Парсит JSON-дерево условий → `ICondition`. Это и есть формат `ActivationConditions`/`CompletionConditions` из §4. |
| `AllOfCondition` / `AnyOfCondition` / `NotCondition` | `Conditions/Services/Composites` | Композиция условий задачи без своего кода. |
| `SoldGenreCondition` + `SoldGenreConditionFactory` (`"soldGenre"`) | `SalesStats/Conditions` | Готовый шаблон-прототип квестового условия «продать N книг жанра». |

**Рекомендация — вариант A (выбран):** квесты зависят от `Game.Conditions.API`, хранят `ConditionConfig` как JSON-узлы и прогоняют их через `IConditionParser`. Новые лист-условия (`visitLocation`, `soldGenreAtLocation`, `weatherIs`, `equipDecor`, `haveItem`, `soldByTags`, ...) добавляются как `IConditionFactory` в **своих** фичах (по примеру `SoldGenreConditionFactory`) и подхватываются движком без его изменения. `Game.Quest` владеет только тем, чего в `Conditions` нет: машиной состояний, прогрессом/сохранением задач, цепочками `NextQuestIds`, наградами и permanent effects, а также **триггером пере-оценки** условий (`ICondition.Evaluate()` — pull-модель, ей нужен повод пересчитаться: доменные сигналы продаж/дня/локации/декора).

Отвергнутые варианты:

- **B. Перенести/форкнуть `Conditions` внутрь `Game.Quest`** — нет: движок уже общий (на нём строится, напр., `LocationUnlock`); дублирование и расхождение.
- **C. Завести свою абстракцию условий в квестах** — нет: повторяет `ICondition`/композиты, ломает единый формат и UI прогресса.

Итог: `Conditions` = «выполнено ли условие сейчас»; `Game.Quest` = машина состояний + цепочки + награды/эффекты поверх. `SoldGenreCondition` — эталон для всех новых factory.

### 11.1 `ConditionConfig[]` — это не новый язык

`ActivationConditions` / `CompletionConditions` / `FailConditions` квеста и задачи — это **тот же JSON/`JObject`**, который парсит существующий `IConditionParser` в `ICondition`. Квесты не вводят свой DSL, а переиспользуют формат `Conditions`:

```json
{
  "id": "quest_01_b",
  "completion": {
    "all": [
      { "type": "locationIs", "locationId": "location_01" },
      { "type": "soldGenre",  "genre": "Fantasy", "min": 15 }
    ]
  }
}
```

Задача квеста = **metadata + lifecycle + condition tree**: `id`/`description`/`pointers`/награды (это про `Game.Quest`) плюс дерево условий (это про `Game.Conditions`). `SoldGenreCondition` — фактически первый прототип квестовой цели «без владельца, срока, состояния и награды»; всё недостающее добавляет квест.

### 11.2 Baseline прогресса с момента активации (тонкий момент)

`SoldGenreCondition` читает **lifetime-счётчик** жанра (`ISalesStatsReader.GetSold`). Для квеста «продай 15 `Fantasy` **после старта задачи**» это неверно: к моменту активации игрок мог уже продать N книг, и условие зачтётся мгновенно. Поэтому **`Game.Quest` обязан сохранять baseline-snapshot счётчиков в момент активации задачи** и считать прогресс как `current - baseline`.

Это не ломает идею conditions — это ответственность владельца (Quests). Два варианта реализации:

- **A. Scoped reader.** Квест при активации фиксирует baseline и передаёт лист-условию `ISalesStatsReader`-обёртку, которая вычитает baseline. Один и тот же `soldGenre` работает и для unlock-условий (lifetime), и для квестов (scoped) — отличается лишь внедрённый reader.
- **B. Отдельный тип условия** `soldGenreSinceTaskStarted`, который явно берёт baseline из состояния задачи.

Предпочтителен **A** (не плодит почти-дубликаты условий). Baseline кладётся в save задачи (рядом с `QuestTaskState`) и восстанавливается при загрузке — иначе прогресс «поедет» после перезапуска. Это пересекается с `GAME-4`: дневной/локационный разрез продаж и baseline-snapshot стоит проектировать вместе.

### 11.3 Разделение ответственности (итоговая граница)

```text
Game.Conditions = язык требований и прогресса
  • leaf: soldGenre, soldGenreAtLocation, soldGenreInSingleDay, weatherIs, decorEquipped, haveItem, visitLocation, locationIs, ...
  • composites: all / any / not
  • ConditionResult.Current/Target  -> прогресс-бар "23/30"

Game.Quest = владелец состояния, цепочек, наград и последствий
  • lifecycle: Pending -> Active -> ReadyToAward -> Awarded (+ Failed)
  • владелец/цепочка/персонаж (позже), порядок задач
  • save состояния квеста и задач + baseline прогресса (§11.2)
  • награды и идемпотентные permanent effects (§6)
  • UI tracking / журнал / уведомления
  • запуск следующего квеста (NextQuestIds) после завершения
```

Квесты **оркестрируют** существующий condition-framework, а не изобретают условия заново.

---

## 12. Редкость книг (rarity → теги/качества)

В этом проекте **нет** RPG-редкости (common/rare/gold). «Особенная» книга = редкая комбинация тегов, которую трудно собрать на складе. Проект уже устроен именно так — в `BookConfig`:

```csharp
public string Genre { get; set; }       // основной жанр (BookGenre: Classic/Crime/Drama/Fact/Fantasy/Kids/Travel)
public float  RarityWeight { get; set; } // ВЕС ПОЯВЛЕНИЯ в ассортименте, не «тир»
public string[] Tags { get; set; }       // survival, space, study, history, nature, ...  (совпадение с запросом +2)
public string[] Mood { get; set; }       // smart, tense, cozy, romantic, dark, ...        (совпадение +1)
```

**Рекомендация — вариант 1 (выбран):** квестовые требования формулируются как совпадение по `Genre` + `Tags` (+`Mood`), без ввода нового rarity-тира. Например, «редкий справочник» = `genre: Fact` + `tags ⊇ {nature, academic}`. Это переиспользует ту же scoring-механику, что и обычные продажи, и не плодит новых перечислений. Нужно лишь, чтобы `SalesStats` умел считать продажи по тегам (условие `soldByTags`) — это часть работ Prereq §0 (расширение учёта продаж).

Дополнительно:

- **Вариант 2 — `RarityWeight` как «труднодоступность», не как требование.** Поле уже есть и означает редкость появления лота. Можно опционально требовать книги ниже порога веса («раритетный лот»), но для ясности квеста предпочтительнее явное совпадение по тегам (вариант 1). RPG-тир не вводим.
- **Уникальные квестовые предметы — отдельная категория, не «редкость книг».** Квест-предметы (журнал, пакет семян, записка, улика и т.п.) существуют в одном экземпляре, не продаются и не являются книгами. Их моделируем как `QuestItem`/уникальную категорию инвентаря (см. открытый вопрос в §10), а не через `BookConfig`/rarity.
