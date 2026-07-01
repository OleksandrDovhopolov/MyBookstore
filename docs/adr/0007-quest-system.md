# ADR-0007: Quest system over the Conditions engine

- **Status:** Accepted (MVP-ядро реализовано, дальнейшие модули — позже)
- **Date:** 2026-06-29
- **Deciders:** project owner
- **Related:** [ADR-0001](0001-save-data-modular-payload.md) (save-модули), [ADR-0002](0002-config-system-architecture.md) (конфиги),
  [ADR-0004](0004-stock-model-hybrid-sale-chance.md) / [ADR-0006](0006-passive-sales-requested-genre.md) (продажи/профиль покупателя),
  [QUESTS.md](../QUESTS.md), [TODO.md](../TODO.md) (GAME-2…GAME-6)

## Context

Нужна сюжетная система квестов: у персонажей — цепочки квестов с задачами вида «продай N книг
жанра/на локации/за день», «поставь декор», «получи предмет», «дождись погоды», с постоянными последствиями в
мире после завершения. Персонажей в проекте пока нет → квесты на старте обезличены (`characterId` опционален).

В проекте уже есть data-driven движок условий `Game.Conditions` (`IConditionParser` → `ICondition.Evaluate()` →
`ConditionResult`, композиты `all/any/not`, per-feature `IConditionFactory`), на котором построен `LocationUnlock`.
Главный вопрос — не строить ли «свой язык условий», а как переиспользовать существующий и что должно жить в самой
квест-фиче. Доп. ограничения: сезонов в проекте нет; «редкости» книг в RPG-смысле нет (есть `Genre`+`Tags`+`Mood`).

## Decision

### 1. Квесты — поверх `Game.Conditions`, а не вместо (вариант A)
`Game.Conditions` = «выполнено ли требование сейчас» + прогресс (`Current/Target`). `Game.Quest` = владелец того,
чего у условия нет: lifecycle, цепочки, порядок задач, save, награды/эффекты, baseline, события для UI.
`ConditionConfig` — это **не новый DSL**, а тот же `JObject`, который парсит существующий `IConditionParser`.
`SoldGenreCondition` — прототип квестовой цели «без владельца/срока/состояния/награды».

### 2. Сборка `Game.Quest` (+ `.API`, + `.Tests.Editor`)
Имя в едином стиле с `Game.Inventory`/`Game.Decor`/`Game.SalesStats`. Старый пустой placeholder `Quest.asmdef` удалён.
Конфиги — **JSON `IConfig` в `Game.Configs`** (`[ConfigFile("quests")]`), как `BookConfig`/`LocationConfig`; условия —
`JObject` (как `LocationConfig.Unlock`); `QuestConfig.Type` — строка (Configs не ссылается на `Game.Quest.API`).
**Не** ScriptableObject. Награда/эффект — POCO (`QuestRewardConfig`/`QuestWorldEffectConfig`), маппинг в рантайме.

### 3. Условия живут в фичах-владельцах
Каждое leaf-условие = `IConditionFactory`, регистрируемый своей фичей в DI (движок находит их через коллекцию
`IConditionFactory`, как `SoldGenreConditionFactory`):
- `Game.SalesStats`: `soldGenre`, `soldGenreAtLocation`, `soldGenreInSingleDay`.
- `Game.Decor`: `decorEquipped`; `Game.Inventory`: `haveItem`; `Game.DayCycle`: `weatherIs`.
fail-closed (битый JSON → never-met), стабильные `reasonKey`. `seasonIs` **вне MVP** (сезонов нет); сезонные «пики»
заменены доступными триггерами (день/погода/прогресс продаж).

### 4. Prerequisite GAME-4 — учёт продаж по локации/дню
`Game.SalesStats` расширен `SoldByLocationGenre` + `SoldByDayGenre` (save schema v2) + reader-getters; контекст
продажи (`SaleContext{LocationId,Day}`) прокинут через `SalesDayResult`/`SalesDayCommitService` (единый чокпоинт).

### 5. `QuestsService` — lifecycle (детерминированные правила)
`Quest`: `Pending → Active → ReadyToAward → Awarded` (+ `Failed`); `Task`: `Pending → Active → Completed` (+ `Failed`).
- **Auto-award (MVP):** все задачи Completed → `ReadyToAward` → сразу `Awarded` (события `QuestCompleted`→`QuestAwarded`).
- **Активация:** head (квест, не входящий в `NextQuestIds`) авто-активируется по met `ActivationConditions`; цепочка —
  `NextQuestIds` форс-активирует successor'а как **hard transition** (activation-conditions игнорируются). MVP — линейная цепочка (0..1).
- **Fail имеет приоритет** над completion. `CanBeReset` в MVP **игнорируется** (Completed не откатывается).
- Прогресс задачи = `ConditionResult` (UI рисует «23/30», как у `LocationUnlock`). События — **C# events**, не сигнал-шина.
- Конфиг-валидация при build (лог + fail-closed): пустой/дубль id, дубль task id, `NextQuestIds>1`, неизвестный successor, цикл, несколько heads.

### 6. Регистрация и init-тайминг
`QuestsService` — `ISaveHook`, регистрируется в **global** scope (`BootstrapInstaller`) и **force-construct** в
`Bootstrap.Construct` (как `LocationUnlock`), чтобы его `AfterLoadAsync` отработал в `SaveDataLoadOperation` после
тёплых конфигов. Пере-оценка условий — по доменным сигналам `ISalesStatsService.Changed`,
`IDecorPlacementService.PlacementChanged`, `IInventoryService.Changed`, `IDayProgressService.PhaseChanged`
(pull-модель `ICondition` + reentrancy-safe `Reevaluate`).

### 7. Save (Этап 5)
`SavedQuests`: `Active`/`ReadyToAward` — полное состояние задач; `Awarded`/`Failed` — id; **`Pending` не сохраняется**.
Терминалы при загрузке восстанавливаются **молча** (без событий) → Awarded **не переигрывается** (фундамент
идемпотентности наград, Этап 6). Завершённые задачи восстанавливаются из save, а не переоцениваются → реверсия
немонотонного условия (декор сняли, погода сменилась) **не откатывает** прогресс. Batched-write через `MarkDirty`.

### 8. Baseline «после старта задачи» (Этап 4b)
Продажные условия в `CompletionConditions` считают прогресс **от активации задачи**, а не lifetime. Механика:
`SalesStatsService` реализует `ISalesStatsBaselineSource` (`CaptureBaseline()` → `SalesStatsStateDto`;
`CreateScopedReader(baseline)` → `current − baseline`, в т.ч. best-day по дням). При активации продажной задачи
`QuestsService` снимает baseline **один раз**, пересобирает её completion через scoped-парсер (глобальные factory +
продажные с baselined reader) и хранит snapshot в `SavedQuest.TaskBaseline` (save schema v2, миграция v1→v2 с
warning). **Авто** для всех продажных условий (вкл. single-day); без флагов. Activation/Fail остаются lifetime.

### 9. Редкость книг → теги
RPG-редкости нет. «Особенная» книга = редкая комбинация `Genre`+`Tags`(+`Mood`) в `BookConfig`; `RarityWeight` —
вес появления, не «тир». Квест-предметы (журнал, нож, семена) — отдельная уникальная категория, не «редкие книги».

## Consequences

### Positive
- Единый формат условий и прогресса (квесты, unlock-локаций — один движок, один UI прогресса).
- Чистая граница: добавить новое условие = новый `IConditionFactory` в своей фиче, движок не трогаем.
- Save переживает рестарт корректно: терминалы не переигрываются, немонотонный прогресс не теряется.
- baseline даёт честную семантику «после старта» без новых типов условий (подмена reader).
- `characterId` заложен опционально → персонажи подключатся без миграции модели.

### Negative / costs
- `Game.Quest` (runtime-оркестратор) ссылается на runtime `Game.SalesStats` и `Game.Conditions` (а не только `.API`) —
  ради сборки scoped-парсера и продажных factory. Осознанное исключение из «фичи зависят только на `.API`».
- baseline хранит **полный** `SalesStatsStateDto` на каждую активную продажную задачу (MVP-cost; later — компактный baseline).
- single-day scoped — только глобально по жанру (нет day×location в DTO).
- `QuestsService` — крупный класс (lifecycle + re-eval + chains + save + baseline).

### Что НЕ выбрано
- Форк/перенос `Game.Conditions` внутрь квестов — дублирование, расхождение с `LocationUnlock`.
- Своя абстракция условий в квестах — ломает единый формат/UI прогресса.
- ScriptableObject-конфиги — расходится с JSON-конвенцией проекта; `JObject`-условия в SO неудобны.
- Сезоны (`seasonIs`) — вне MVP.
- Seam'ы `ISalesConditionFactorySource` / `IConditionParserFactory` — отброшены, чтобы не тащить `Game.Conditions.API`
  в `Game.SalesStats.API` и не расширять общий Conditions API ради одного кейса; в `Game.SalesStats.API` оставлен
  только `ISalesStatsBaselineSource`.
- Opt-in флаг `sinceStart` у условий — baseline сделан автоматическим для всех продажных.

## Status / scope

**Реализовано и поставлено на паузу (MVP-ядро):** GAME-4, сборки, API+конфиги, условия `decorEquipped`/`haveItem`/
`weatherIs`, `QuestsService` (lifecycle/цепочки/auto-award), save (Этап 5), baseline (Этап 4b). Покрыто EditMode-тестами.

**Следующие итерации (вне этого ADR):**
- **GAME-3 / Этап 6** — грант наград (`IRewardGrantService`) + permanent effects (`QuestWorldEffectConfig`-хендлеры),
  идемпотентность (Awarded += timestamp + appliedEffects, schema bump).
- **GAME-5** — persisted-подсистема визитов → условия `visitLocation` / `locationIs`.
- **Этап 7** — реальная цепочка-слайс `quests.json` («An Empire of Sand») + end-to-end тесты.
- UI журнала/HUD (Journal: Stamps/Characters/Equipped/Calendar), фича персонажей, Investigation Board.

## Implementation notes
- Код — английский (LANGUAGE_POLICY). Конфиги: `Game.Configs/Models/QuestConfig` (+ Task/Reward/WorldEffect).
- API/enum: `Game.Quest.API` (`QuestState`/`QuestTaskState`/`QuestType` + extensions; `IQuest`/`IQuestTask`/`IQuestChain`/`IQuestsService`).
- Рантайм: `Game.Quest/Services` (`QuestsService`/`Quest`/`QuestTask`/`QuestChain`) + `Services/Persistence`
  (`SavedQuests`/`IQuestsRepository`/`SaveBackedQuestsRepository`/`QuestsSaveKeys`).
- SalesStats baseline: `ISalesStatsBaselineSource`, `ScopedSalesStatsReader` (nested), `SalesConditionTypeIds`.
- DI: `QuestVContainerBindings.RegisterQuest()` в `BootstrapInstaller`; `Bootstrap.Construct` force-construct `IQuestsService`.
