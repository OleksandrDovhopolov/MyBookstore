# Quests

Спека будущей фичи квестов для MyBookstore. Основана на разборе продовой quest-системы из `heroes` и примерах Tiny Bookshop: цепочки персонажей, продажи книг нужных жанров/тегов, декор как условие, исследование локаций и постоянные последствия в мире.

Целевое имя новой runtime-сборки: `Game.Quest` (в едином стиле с `Game.Inventory`, `Game.Decor`, `Game.SalesStats`). Старый placeholder `Assets/Game/Features/Quest/Quest.asmdef` (`name: "Quest"`, пустой) **удалён**.

---

## 0. Prerequisites (блокеры до старта реализации)

Перед `GAME-2` нужно закрыть зависимости, которых в коде сейчас нет:

1. **Продажи по локациям и по дням (БЛОКЕР).** Текущий `Game.SalesStats` (`ISalesStatsReader`) хранит только глобальный кумулятив: `GetSold(BookGenre)` и `TotalSold` — без разреза по локации и без дневного счётчика. Условия вида «продать 15 `Fantasy` **на `FarBeach`**» и «продать 15 за **один день**» невозможны, пока `SalesStats` не научится считать продажи по `(локация)` и `(день)`. Это нужно сделать **до** реализации квестов. См. TODO `GAME-4`.
2. **Сезоны — вне MVP.** Сезонной системы (`Season`/`Spring`/`Summer`/`Autumn`/`Winter`) в проекте нет, и в ближайший MVP она **не входит**. Поэтому квесты MVP **не имеют зависимости на сезон**: условие `SeasonIs` пока не реализуется, а «сезонные пики» из Tiny Bookshop заменяются доступными триггерами (день/погода/прогресс продаж). Когда сезоны появятся, `SeasonIs` добавляется как ещё один condition-factory без изменения модели квестов.
3. **Погода — уже есть.** Погода доступна per-day как `MorningDayContext.WeatherId` / `ActiveModifierIds` (`"weather_clear"` и т.п.), так что `WeatherIs` реализуем сразу.

## 1. Что берем из продовой системы

Из `heroes-quest-system.md` полезно перенести не конкретные классы один-в-один, а архитектурные решения:

| Идея | Как использовать в MyBookstore |
|---|---|
| `QuestState`: `Pending`, `Active`, `ReadyToAward`, `Awarded`, `Failed` | Базовая машина состояний квеста. Для cozy-flow `ReadyToAward` полезен как момент показа финального диалога/награды, даже если награда потом выдается автоматически. |
| `QuestTaskState`: `Pending`, `Active`, `Completed`, `Failed` | Каждый этап цепочки должен быть отдельной задачей с прогрессом: продать N книг, посетить локацию, поставить декор, дождаться сезона/погоды. |
| Data-driven `QuestDbData` / `QuestTaskDbData` | Квесты должны быть конфигами, а не зашитой логикой. Минимум: id, title, description, chain id, tasks, activation conditions, completion conditions, rewards/effects. |
| `ActivationEvents`, `CompleteEvents`, `FailEvents` | Вместо жёстких вызовов из разных фич — единая модель условий. В MyBookstore она **уже есть**: `Game.Conditions` (`IConditionParser` → `ICondition.Evaluate()` → `ConditionResult`, композиты `AllOf/AnyOf/Not`, per-feature `IConditionFactory`). Квесты переиспользуют её как есть — см. §11. |
| `QuestsToBornOnComplete` | Нужна последовательная цепочка: завершение одного квеста активирует следующий. Для Tiny Bookshop-подобных арок это основной механизм. |
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
  id: "far_beach_sand_empire"
  characterId: null
  quests: ["far_beach_intro", "sand_inspiration", "sand_funding", "sand_finale"]
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

Минимальная модель конфига:

```csharp
QuestConfig
{
    string Id;
    string ChainId;
    string CharacterId; // null до появления персонажей
    QuestType Type;     // Story / Side / Tutorial
    string TitleKey;
    string DescriptionKey;
    string[] NextQuestIds;
    QuestTaskConfig[] Tasks;
    QuestRewardConfig[] Rewards;
    QuestWorldEffectConfig[] WorldEffects;
    ConditionConfig[] ActivationConditions;
    ConditionConfig[] FailConditions;
}
```

Задача:

```csharp
QuestTaskConfig
{
    int Id;
    string DescriptionKey;
    ConditionConfig[] ActivationConditions;
    ConditionConfig[] CompletionConditions;
    string[] RequiredItemIds;
    string[] PointerIds;
    bool CanBeReset;
}
```

Для Tiny Bookshop-подобных квестов нужны типовые условия. Каждое — это `IConditionFactory` (`type` + `Create(JObject)`), регистрируемый владеющей фичей в DI, ровно как существующий `SoldGenreConditionFactory` (`"soldGenre"`).

| Условие | Пример | Статус |
|---|---|---|
| `soldGenre` | продать N книг жанра (глобально) | **есть** — `SoldGenreCondition` |
| `soldGenreAtLocation` | продать 15 `Fantasy` на `FarBeach` | требует расширения `SalesStats` (разрез по локации) — Prereq §0 |
| `soldGenreInSingleDay` | продать 15 `Fantasy` за один день | требует дневного счётчика в `SalesStats` — Prereq §0 |
| `soldByTags` | продать книги с тегами `{nature, academic}` жанра `Fact` | требует учёта продаж по `Tags` (есть `BookConfig.Tags/Mood`) — см. §12 |
| `visitLocation` | посетить `FarBeach` 3 раза | новый factory |
| `equipDecor` | установить `harper_castle_donation_box` | новый factory (`Game.Decor`) |
| `decorEquippedForDays` | ездить с `fireplace` 3 игровых дня | новый factory |
| `collectResourceBySales` | собрать 100 монет через копилку | новый factory |
| `weatherIs` | дождь, шторм, снег, солнце | реализуем сразу (`MorningDayContext.WeatherId`) |
| `clickWorldObject` | лодка у маяка, мусорный бак, ящик | новый factory |
| `haveItem` / `consumeItem` | найден журнал, нож, пакет семян | новый factory (`Game.Inventory` / quest items) |
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
- `Active` и `ReadyToAward` сохраняют полный прогресс задач.
- `Awarded` сохраняет id, timestamp и примененные permanent effects.
- `Failed` нужен редко: например, если дизайн позже введет пропущенные one-shot события. Сезонное ожидание не должно быть `Failed`; это активный квест с невыполненным сезонным условием.
- `CanBeReset` нужен для задач вида "держать декор установленным N дней" или "выбирать солнечные локации 7 дней": если условие сбилось, прогресс может откатиться или паузиться согласно конфигу.

---

## 6. Permanent effects

Tiny Bookshop-логика ценна не только наградой, а изменением мира после финала. Для этого у квеста должны быть постоянные эффекты, применяемые один раз при `Awarded`.

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

> **MVP-замечание.** Сезонных гейтов в MVP нет (Prereq §0): сезонные «пики» Tiny Bookshop заменены доступными триггерами — погодой (`weatherIs`), продажей «за один день» и прогрессом продаж. Жанры `Maritime`/`History`/`Botany` в таблицах иллюстративны; в коде жанр ограничен `BookGenre` (`Classic/Crime/Drama/Fact/Fantasy/Kids/Travel`), а тонкая выборка «особенных» книг делается по `BookConfig.Tags`/`Mood` (см. §12), а не по RPG-редкости.

### Far Beach: An Empire of Sand

Цепочка про строительство песчаного замка.

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| `Afternoon at the Seaside` | посетить `FarBeach` 3 раза весной/летом | открыть следующую задачу |
| `The Adventure Begins` | продать 15 `Fantasy` на `FarBeach` | декор `Harper's Castle Donation Box` |
| `For Bookstonbury!` | установить копилку и накопить 100 монет с продаж | открыть летний финал |
| `An Empire of Sand` | продать 15 `Fantasy` за один день на `FarBeach` (`soldGenreInSingleDay`) | `LocationCustomerBonus(FarBeach, +2)`, `UnlockLocation(Cave)`, старт глобального квеста |

### Mega Marche: The Case of the Hideous Mascot

Детективная цепочка с доской расследований.

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| `Going Shopping` | посетить `MegaMarche` 3 раза | открыть место преступления |
| `Evidence Board` | закрепить дело на доске, найти `Knife`, получить `CCTV note` | открыть демонстрацию улики |
| `Display Evidence` | повесить `Knife` как декор и приехать в `CafeLiberte` | вызвать свидетеля |
| `Snowy Confession` | в снежный день (`weatherIs: snow`) иметь активное дело и выставленный нож | декор `Press Tape`, продвижение ветки Клауса |

### Old Lighthouse: The Lost Lighthouse

Атмосферная цепочка про маяк, осень и шторм.

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| `Road to the Lighthouse` | открыть `OldLighthouse` | старт цепочки |
| `Caretaker's Solitude` | продать 10 книг с тегами `{maritime, history}` у маяка (`soldByTags`) | Оливер начинает диалог |
| `Soggy Logbook` | в шторм (`weatherIs: storm`) кликнуть старую лодку | предмет `Soggy Logbook` |
| `Dry the Logbook` | установить `Fireplace` или `Heater` на 3 игровых дня | восстановленный журнал |
| Финал | вернуть журнал Оливеру | `GenrePriceMultiplier(MysteryThriller, +20%, evening/coast)`, ночная торговля на побережье, декор `Oliver's Old Compass` |

### Botanical Garden: Klaus's Botanical Mystery

Весенняя цепочка про редкие фиалки.

| Этап | Условия/задачи | Награда/эффект |
|---|---|---|
| `Strange Bookmark` | получить зашифрованную закладку (после покупки книги поэзии) | открыть расследование |
| `The Seed Hunt` | продать 5 книг с тегами `{nature, academic}` жанра `Fact` в `BotanicalGarden` (`soldByTags`) | предмет `Ancient Seed Packet` |
| `Spring Bloom` | установить `GardenBox`, посадить семена | старт выращивания |
| `Sunny Days` | 7 игровых дней выбирать солнечные локации (`weatherIs: clear`) | вырастить фиалки |
| Финал | передать цветы Клаусу | `VanVisualUpgrade(BlossomAwning)`, `GenrePriceMultiplier(PoetryRomance, +15%)`, повышенный вес романтиков/писателей |

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

## 9. Первая реализация

Минимальный vertical slice:

0. **Prereq:** расширить `Game.SalesStats` учётом продаж по локации и по дню (см. §0, TODO `GAME-4`).
1. Создать сборки `Game.Quest.API`, `Game.Quest`, `Game.Quest.Tests.Editor`.
2. Ввести `QuestState`, `QuestTaskState`, `QuestType`.
3. Сделать in-memory `IQuestsService` с конфигами, задачами и цепочками; условия — через `IConditionParser` (§11).
4. Добавить save DTO и save hook для активных/завершённых квестов.
5. Подключить condition-factory: визит локации, продажа книг жанра (переиспользовать `soldGenre`), погода (`weatherIs`), экипированный декор. **Без сезона** (вне MVP).
6. Реализовать одну обезличенную цепочку `An Empire of Sand`.
7. Добавить EditMode-тесты: переходы состояний, прогресс задач, цепочка `NextQuestIds`, идемпотентность permanent effects.

---

## 10. Открытые вопросы

- Где будет жить источник конфигов квестов: `Configs` section `quests.json` или локальный ScriptableObject для первой итерации?
- Нужен ли отдельный журнал квестов сразу, или достаточно HUD/debug-вывода до появления UI?
- Должны ли квестовые предметы быть обычными `InventoryItem`, или нужен отдельный `QuestItem` category?
- Как оформлять детективные цепочки до появления полноценной Investigation Board?
- Триггер пере-оценки условий: на каких доменных сигналах квест дёргает `Evaluate()` (см. §11) — `SalesStatsChange`, смена дня, вход в локацию, экипировка декора?

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
  "id": "sand_inspiration",
  "completion": {
    "all": [
      { "type": "locationIs", "locationId": "far_beach" },
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
  • leaf: soldGenre, weatherIs, decorEquipped, visitedLocation, locationIs, ...
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

В Tiny Bookshop **нет** RPG-редкости (common/rare/gold). «Особенная» книга = редкая комбинация тегов, которую трудно собрать на складе. Проект уже устроен именно так — в `BookConfig`:

```csharp
public string Genre { get; set; }       // основной жанр (BookGenre: Classic/Crime/Drama/Fact/Fantasy/Kids/Travel)
public float  RarityWeight { get; set; } // ВЕС ПОЯВЛЕНИЯ в ассортименте, не «тир»
public string[] Tags { get; set; }       // survival, space, study, history, nature, ...  (совпадение с запросом +2)
public string[] Mood { get; set; }       // smart, tense, cozy, romantic, dark, ...        (совпадение +1)
```

**Рекомендация — вариант 1 (выбран):** квестовые требования формулируются как совпадение по `Genre` + `Tags` (+`Mood`), без ввода нового rarity-тира. «Редкий ботанический справочник» Клауса = `genre: Fact` + `tags ⊇ {nature, academic}`. Это переиспользует ту же scoring-механику, что и обычные продажи, и не плодит новых перечислений. Нужно лишь, чтобы `SalesStats` умел считать продажи по тегам (условие `soldByTags`) — это часть работ Prereq §0 (расширение учёта продаж).

Дополнительно:

- **Вариант 2 — `RarityWeight` как «труднодоступность», не как требование.** Поле уже есть и означает редкость появления лота. Можно опционально требовать книги ниже порога веса («раритетный лот»), но для ясности квеста предпочтительнее явное совпадение по тегам (вариант 1). RPG-тир не вводим.
- **Уникальные квестовые предметы — отдельная категория, не «редкость книг».** `Soggy Logbook`, `Ancient Seed Packet`, зашифрованная записка, `Knife` существуют в одном экземпляре, не продаются и не являются книгами. Их моделируем как `QuestItem`/уникальную категорию инвентаря (см. открытый вопрос в §10), а не через `BookConfig`/rarity.
