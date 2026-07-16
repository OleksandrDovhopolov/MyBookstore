# TODO

Рабочий список задач. Разбит по категориям: **Геймплей**, **Инфраструктура**, **Визуал**.
Новые задачи добавляются в конец соответствующей категории.

Статусы: `[ ]` — todo, `[~]` — в работе, `[x]` — готово.

---

## 🎮 Геймплей

- [~] **GAME-2. Фича `Game.Quest` — доделать слайс.**
  Что сделать:
  - Собрать реальную цепочку квестов на боевом конфиге вместо заглушки.
  - Доделать UI журнала (`JournalWindow`).
  - Сверить поведение с решениями из [adr/0007-quest-system.md](adr/0007-quest-system.md).
  - Награды/эффекты оставить в отдельной задаче GAME-3; условия визита закрыты в GAME-5.

- [ ] **GAME-3. Permanent quest effects / world state (= Этап 6).**
  Что сделать:
  - Выдавать награды через `QuestRewardConfig` → `IRewardGrantService`.
  - Применять постоянные эффекты через `QuestWorldEffectConfig`-хендлеры.
  - Сохранить `timestamp` и `appliedEffects` для завершённых квестов.
  - Сделать применение эффектов идемпотентным при повторной загрузке.
  - Поднять `QuestsSaveKeys.StateSchemaVersion`.

- [x] **GAME-4. Учёт продаж по локации и дню для квестов.**
  Что сделано:
  - `Game.SalesStats` расширен счётчиками `SoldByLocationGenre` и `SoldByDayGenre`.
  - Продажа получает контекст `SaleContext { LocationId, Day }`.
  - `SalesDayCommitService` записывает продажи в едином commit-чокпоинте.
  - Добавлены reader-getters для условий `soldGenreAtLocation` и `soldGenreInSingleDay`.
  - Покрыто EditMode-тестами и зафиксировано в [adr/0007-quest-system.md](adr/0007-quest-system.md).

- [x] **GAME-6. Диалоги покупателей (базовый слайс).**
  Что сделано:
  - `DialogStep` (middle-step, держит interaction lock до завершения UI, релиз на `Exit`); sink +
    контроллер (`DialogueStarted` / `CompleteDialogue`).
  - Квест-driven спавн: `QuestConfig.DialogueId` + `QuestSchedulingCustomerSpawner` (читает
    `IQuestsService.GetActiveQuests()`) + fire-once `IDeliveredDialoguesService` (день о диалогах не знает).
  - Движок графа `DialogueEngine` (view-agnostic) + окно-лента `DialogWindow` / `DialogWindowView` /
    `DialogLineView`: реплики со сторонами (L/R по говорящему), typewriter, DOTween-появление, скролл; чит-модуль.
  - Контент `dialogues.json` (граф `{ nodeId, lines:[{ speaker, text }], options }`), англ. тексты.
  - Варианты ответа (кнопки **в ленте**, пул `UIListPool<DialogOptionView>`), ветвящийся диалог `eddy1`;
    bridge-узлы (опция с пустым `text` = невидимый авто-переход по ленте); фикс-ширина бабла (`DialogLineView`).
  - Спека, факт и расхождения — [INPROGRESS/CUSTOMER_DIALOG_STEP.md](INPROGRESS/CUSTOMER_DIALOG_STEP.md).
  - Остаток (значимый выбор, world-HUD 2.2, реактивные диалоги, локализация, портреты, …) — см. **Backlog** ниже.

- [ ] **GAME-7. Разобрать дублирование `SelectedBookIds` и `ShelfBookIds`.**
  Что сделать:
  - Зафиксировать источник правды для фаз: `preparation.session.SelectedBookIds` как черновик/подтверждённый выбор подготовки, `book_sell.shelf_state.ShelfBookIds` как живое состояние полки продаж.
  - Проверить, можно ли убрать лишнее зеркало без потери resume-сценариев: релонч в Preparation, релонч в Sales, возврат после failed location entry, продолжение дня после продаж.
  - Если оба модуля остаются, явно описать контракт синхронизации: когда `ConfirmAsync` копирует выбранные книги в shelf state, когда продажи удаляют книги только из shelf state, когда новый Preparation seed берёт survivors с прошлой полки.
  - Добавить/обновить тесты на рассинхрон `Confirmed=false`, продажу книги, новый день и повторный вход в Preparation.

- [ ] **GAME-8. Clamp progress у завершённых квестов в UI.**
  Что сделать:
  - В `QuestViewModelBuilder` для задач/квестов в `Completed`, `ReadyToAward` и `Awarded` показывать `min(current, target) / target`.
  - Не показывать overflow вроде `6/3` для уже завершённого квеста; ожидаемый вид — `3/3`.
  - Добавить EditMode-тест на completed/awarded quest, где live condition progress больше цели.

- [x] **GAME-9. Compact baseline для sales-задач квестов.**
  Что сделано:
  - Полный per-task `SalesStatsStateDto` заменён на compact `SalesStatsBaselineDto` по решению [ADR-0008](adr/0008-quest-sales-progress-persistence.md).
  - Для `soldGenre` хранится baseline только нужного жанра.
  - Для `soldGenreAtLocation` хранится baseline только пары `(locationId, genre)`.
  - Для `soldGenreInSingleDay` хранится день активации и count нужного жанра на момент активации.
  - Сохранена pull-based модель conditions/scoped reader; event-driven progress оставлен будущим направлением.
  - Quest-save поднят до v4: `Tasks` сохраняются читаемым списком `{ Id, State, SalesBaseline }`, enum-ы пишутся строками.
  - Legacy full-baseline compatibility удалена: проект в активной разработке, старые сейвы сбрасываются.

- [~] **GAME-10. Туториал — завершить оставшееся.** Движок (Layer 2) и Day 1 v1 реализованы; спека и
  статус — [INPROGRESS/TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md) (§6 роадмап, §6.1 Day 1 v1).
  Осталось:
  - **§7 — debug/качество**: cheat-модуль в `Game.Cheat` (list/force-run/force-complete/reset + сброс
    `ftue.*` = replay Day 1); editor-валидатор id-шников (target ↔ `TutorialTargetIds` ↔ скан префабов на
    `TutorialTargetTag`; questId ↔ `quests.json`; window id ↔ `TutorialWindowChecker`; парс типов шагов);
    аналитика (`seq_start`/`step_start` автоматом, `seq_complete` явно).
  - **Немодальный callout-режим** (pointer+текст **без** dim; тип шага `pointAt`/`callout`) — чтобы
    подсвечивать контролы на экранах свободного взаимодействия (Open Shop, список жанров, динамический
    «+» жанра) + динамическая регистрация таргетов из `PreparationGenreRowView` через фасад `TutorialTargets`.
  - **Строгий day-gate**: condition-factory `currentDayIs` (сейчас Day 1 играет один раз при первом hub,
    не строго «день == 1»).
  - **Устойчивость Day 1** (известные ограничения v1 в §6.1): корректный resume посреди дня и cancel-path
    (закрыл Location/Preparation, не подтвердив) — recovery/блокировка закрытия окон.
  - **Локализация** текста туториала (сейчас ASCII/English) — через INF-4; поле `textKey` зарезервировано.
  - **Полировка**: feather-дырка шейдером за тем же API `TutorialBlackoutView`; player-facing Skip;
    вариант `awaitWindow("closed")`; опц. мягкий pointer на кнопку журнала по `QuestStarted`.
  - **Ремайндер по editor-обвязке** (если ещё не сделано): prefab текст-панели, asset
    `TutorialOverlaySettings` + назначение в `BootstrapInstaller`, `TutorialTargetTag` на кнопке Start Day
    (`hub.start_day_button`), `Tools/Configs/Sync Bundled Defaults` для билда.

- [ ] **GAME-11. Определять размер книги по `pages` из конфига.**
  Что сделать:
  - Читать `pages` из `books.json` / `BookConfig` и выводить производный размер книги без ручного поля в контенте.
  - Правила классификации: `XS <= 200`, `S > 200 && <= 400`, `M > 400 && <= 700`, `L > 700`.
  - Найти все места, где нужен размер книги (визуал/полки/продажи/фильтры), и заменить хардкод/ручную классификацию на единый resolver.
  - Покрыть boundary-тестами значения `200`, `201`, `400`, `401`, `700`, `701`.

- [ ] **GAME-12. Определять возрастную категорию книги по `published` из конфига.**
  Что сделать:
  - Читать `published` из `books.json` / `BookConfig` и выводить производную категорию книги без ручного поля в контенте.
  - Правила классификации: `Fresh` — 21 century, `New` — 20 century, `Classic` — below 20 century.
  - Уточнить формат `published` в конфиге (год или дата) и централизовать парсинг/валидацию.
  - Покрыть boundary-тестами границы веков.

- [ ] **GAME-15. Согласовать стартовый пресет FTUE с каталогом книг.**
  Хардкод-пресет в [FtueBootstrapper.cs:28-37](../Assets/Game/Features/Ftue/Services/FtueBootstrapper.cs)
  просит 27 книг по 7 жанрам (`Fantasy 5, Crime 5, Drama 6, Classic 3, Fact 3, Travel 3, Kids 2`), но текущий
  `books.json` покрывает только 4 жанра (`Crime 20, Classic 20, Drama 20, Fantasy 3`) — в логе сыплются
  warning'и `genre '…' missing from catalog` (Fact/Travel/Kids) и `Fantasy: catalog has 3, requested 5`
  ([строки 114 и 125](../Assets/Game/Features/Ftue/Services/FtueBootstrapper.cs)). FTUE не падает, но сеет 17
  книг вместо 27. Что сделать (выбрать направление):
  - **Контент:** завезти книги жанров `Fact`/`Travel`/`Kids` и добить `Fantasy` до нужного числа в
    `books.json` → Sync в StreamingAssets + Publish на сервер.
  - **или Код:** привести `PresetCounts` к реально существующим жанрам/числам, чтобы лог был чистым.
  - Заодно вынести пресет из хардкода в `ftue.json` (уже помечено как MVP-заглушка в
    [комментарии:22-24](../Assets/Game/Features/Ftue/Services/FtueBootstrapper.cs)), парно с рефактором
    `DailyBookSlots`.

- [ ] **GAME-16. Вынести сценарную встречу из `QuestConfig` в `CustomerScriptConfig` (Candidate E).**
  **Триггер: делать, когда появится ВТОРАЯ сценарная встреча.** Сегодня она ровно одна —
  `q_intro_eddi` / диалог `eddy1`.
  Сейчас `QuestConfig` несёт поведение встречи двумя полями: `DialogueId` («когда квест Active, в Sales
  приходит персонаж с этим диалогом — однократно») и `ScriptedPassivePurchases` (beat-sheet той же встречи:
  `Fact hit → Travel miss`). Это осознанный временный якорь, а не свалка — оба поля описывают **одну**
  сущность и потому лежат рядом; fire-once обоим даёт delivered-dialogues store по `DialogueId`.
  Почему не сейчас:
  - Один экземпляр не оправдывает новое существительное: `CustomerScriptConfig` + `StepFactory` + валидация —
    каркас под контент, которого пока нет.
  - Вынести только beats (оставив `DialogueId` на квесте) = размазать одну встречу по двум файлам — хуже текущего.
  - Вынести всё = двигать `DialogueId` (читают quest-aware спавнеры через `IQuest.Config`) и `CharacterId`
    (торчит на `IQuest`, Quest.API) — правка API квестов ради одного контент-кейса.
  - Сам Candidate E просит подождать устаканивания step-словаря, а он менялся недавно (Candidate D:
    `IPassivePurchaseStep`, `CompletedAndEndPassiveChain`).
  Что сделать по триггеру: см. [INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md)
  «Candidate E» — скрипт вбирает `DialogueId` + `ScriptedPassivePurchases` (и, вероятно, `CharacterId`),
  в `QuestConfig` остаётся ссылка `customerScriptId`. Заодно решить вопрос оттуда же: может ли
  director-вставка (`CommentStep`) вклиниваться в сценарную последовательность, и нужен ли opt-out для FTUE.

- [ ] **GAME-17. Валидация связки «FTUE-пресет ↔ полка дня 1 ↔ сценарный скрипт».**
  Туториал дня 1 держится на согласии трёх независимых мест: `FtueBootstrapper.PresetCounts` сеет книгу
  жанра `Fact` → `FirstDayEntryFlow.BuildFirstDayShelfPreset()` прибивает `Fact`/`Travel` на полку
  (`AddFirstByGenre`) → `q_intro_eddi.scriptedPassivePurchases` требует `Fact forceHit: true`. Ноль валидации:
  при рассинхроне `ScriptedPassivePurchaseResolver` пишет **warning** и возвращает miss, а
  `RemoveRemainingPassivePurchases` (Candidate D) тут же удаляет второй beat — то есть урок про sale chance
  молча исчезает, а в логе одна строка. Что сделать:
  - Провал форсированного хита — это дефект контента: `LogError` вместо `LogWarning`.
  - Editor-валидатор: жанры скрипта существуют в `BookConfig.PrimaryGenre` и покрыты FTUE-пресетом
    (по образцу валидатора id-шников из GAME-10 §7).
  - Связано с GAME-15 (пресет FTUE vs каталог книг) — чинить парно.

---

## 🛠️ Инфраструктура

- [ ] **INF-4. Localization.** Слой локализации (ключи вместо строк, таблицы переводов, рантайм-смена
  языка). Закладывать заранее — под Steam-релиз на нескольких языках.

- [ ] **INF-5. Детерминированный (seeded) RNG.** Отдельный сервис для воспроизводимой генерации
  дневного спроса/покупателей и тестов баланса.

- [ ] **INF-6. Свести Save в инфраслой + версионирование.** `ISaveService` сейчас живёт вне
  `Infrastructure` (используется в `PreparationSessionService`). Централизовать и задать схему
  версионирования/миграции сейва — фундамент игры с прогрессом.

- [ ] **INF-7. Группировка контента по Addressables-лейблам.** Лейблы по локациям/главам + прогрев
  групп при входе в локацию (`WarmupGroupByLabelAsync`). Рычаг для масштабирования контента и
  контент-паков без апдейта билда.

- [ ] **INF-8. `CharactersService` не форс-конструируется → save-hook не регистрируется.**
  `CharactersService` — `ISaveHook` (регистрируется через `save.RegisterHook(this)` в конструкторе) и
  ожидает, что его `AfterLoadAsync` отработает после загрузки сейва (как `QuestsService`). Но в
  `CharactersVContainerBindings` он зарегистрирован только `.As<ICharactersService>()` и **нигде не
  резолвится при старте**: в `Bootstrap.Construct` форс-конструируется `IQuestsService`, но не
  `ICharactersService` ([Bootstrap.cs](Assets/Game/Core/Installers/Bootstrap/Bootstrap.cs)). Итог:
  конструктор `CharactersService` не вызывается до `SaveDataLoadOperation`, hook не регистрируется,
  `AfterLoadAsync` не выполняется → каталог персонажей/леджер не строятся, Journal пуст, discovery не
  реконсайлится. Фикс: форс-конструировать `ICharactersService` на бутстрапе (добавить в список
  `[Inject]`-полей `Bootstrap.Construct`, рядом с `IQuestsService`), как другие `ISaveHook`-сервисы.
  Не связано с текущим DI-циклом `ConditionParser ↔ LocationUnlockService` — это отдельный баг загрузки.

- [ ] **INF-9. Убрать форс-конструирование save-hook'ов из `Bootstrap.cs` (чистый рефакторинг).**
  Сейчас `Bootstrap.cs` инжектит сервисы (`IInventoryService`, `IResourcesService`, `IProgressionService`,
  `IQuestsService`, …), которые никогда не вызывает — только чтобы VContainer их сконструировал, т.к. они
  сами регистрируются как `ISaveHook` в конструкторе (`save.RegisterHook(this)`). Без этого хук не успевает
  встать до `SaveService.LoadAsync`. Минусы: «мёртвые» поля, связанность Bootstrap с API-сборками фич, список
  save-aware сервисов размазан по Bootstrap (каждая новая фича = правка `Bootstrap.cs`, ср. баг INF-8).
  **Решение (рекомендуемый вариант B):** ввести единый `SaveHookBootstrapper : IStartable`, который резолвит
  `IEnumerable<ISaveHook>` из DI и в `Start()` вызывает `RegisterHook` для каждого; сервисы регистрировать
  `.As<ISaveHook>()` и убрать `RegisterHook(this)` из конструкторов. Поведение не меняется. Сделать **до**
  захода следующей save-aware фичи. Альтернативы (A — каждый сервис `IStartable`; C — `SaveService` принимает
  `IEnumerable<ISaveHook>` в конструкторе) рассмотрены и отклонены в пользу B. Закрывает корневую причину INF-8.

- [ ] **INF-10. Аудит `Assets/Game/Core/UI` на feature-specific классы.**
  Что сделать:
  - Просмотреть все классы в `Assets/Game/Core/UI` и отделить общее UI-ядро от конкретных окон, экранов и фич.
  - Вынести из `Game.Core.UI` конкретные окна вроде настроек, confirm/smoke/debug-экранов и любые feature-specific UI в соответствующие feature/shared UI сборки.
  - Вынести конкретные анимации/эффекты из core UI, оставив в ядре только базовые интерфейсы, абстракции, common helpers и generic window infrastructure.
  - Проверить asmdef-зависимости после выноса: `Game.Core.UI` не должен зависеть от конкретных gameplay/feature namespaces и не должен быть местом для продуктовых окон.

- [ ] **INF-11. `book_sell.last_day_result` растёт линейно от числа покупателей.**
  Единственный модуль сейва, размер которого зависит от размера дня: остальные 12 константные (24-704B),
  а этот в логах рос `230B` (1 покупатель) → `2894B` (33 покупателя) ≈ **~88B на покупателя**; при 49
  покупателях ≈ 4.3KB. Лимит payload — 30720B, так что сейчас не горит (после фикса трафика дни стали по
  6 покупателей, ~573B), но это единственная неограниченная величина в сейве. Что решить:
  - Хранить агрегаты (counts по тирам) вместо строки на покупателя;
  - либо кап на число строк;
  - либо **не персистить вовсе**: данные живут ровно один переход Sales→Results и восстановимы из
    `sales_stats` (v2, 244B).
  Смотреть парно с INF-6 (версионирование сейва).

---

## 📋 Backlog

Отложенное, не входящее в текущие MVP-слайсы. Берётся по мере необходимости.

- [ ] **GAME-6-D. Диалоги — доработки.** Базовый слайс закрыт (см. GAME-6 в «Геймплей» и
  [INPROGRESS/CUSTOMER_DIALOG_STEP.md](INPROGRESS/CUSTOMER_DIALOG_STEP.md)). Осталось:
  - **Геймплейно-значимый выбор**: `CompleteDialogue(choiceId)` / ветвление плана/квеста. Ветвящийся контент
    уже есть (`eddy1`, два выбора + схождение), но выбор **косметический** — на геймплей/квест не влияет.
  - **Атрибуция выбора**: у `DialogueOptionConfig` нет `speaker`; выбранный вариант не отображается в ленте
    как реплика игрока.
  - **Skip** — сейчас disabled-заглушка; реальное поведение (домотать / закрыть).
  - **Свап на world-HUD (2.2)** — движок view-agnostic, меняется только вью ([WORLD_HUD.md](WORLD_HUD.md)).
  - **Реактивные диалоги** через `Customer.InsertNext` (по образцу `PassiveSaleCommentRule` → `CommentStep`).
  - **Node-level `next` (опц., чистит bridge-хак):** линейное продолжение между узлами уже работает через
    опцию с пустым `text` (невидимый авто-переход, `DialogWindow.IsBridgeNode`); по желанию — заменить на
    явный `next` у узла вместо «фейковой» опции (аккуратнее в данных/валидации).
  - **Персонаж `tilde` в `characters.json`** — квест `q_intro_tilde` ссылается на `characterId: "tilde"`,
    которого в конфиге нет (нужен для журнала/HUD/портретов).
  - **Локализация** реплик/имён/опций (raw-строки) — через INF-4.
  - **Портреты/аватары, цветовая тема бабла** по говорящему.
  - **UI-автотесты** окна/анимации (сейчас только ручной прогон через чит).

- [ ] **GAME-14. Active purchase conditions — follow-up после готового слайса.** Condition-based active
  purchases считаются готовыми; базовое поведение зафиксировано в
  [INPROGRESS/ACTIVE_REQUEST_CONDITIONS.md](INPROGRESS/ACTIVE_REQUEST_CONDITIONS.md). Осталось как backlog,
  без блокировки текущего функционала:
  - **ADR-0009:** оформить решение «Active requests over a condition tree» и пометить активную часть
    [adr/0003-customer-simulation.md](adr/0003-customer-simulation.md) как superseded. Пассивную часть
    [adr/0006-passive-sales-requested-genre.md](adr/0006-passive-sales-requested-genre.md) не трогать.
  - **Баланс v2:** после плейтеста решить, хватает ли строгого `Excellent/Failed` и фиксированной награды
    `10` gold, или нужен частичный балл / гибрид «условия как фильтр + оценка выбора» / authored
    `reward`/`difficulty` в `hard_requests.json`.
  - **Escape-hatch для сложных формул:** если появится запрос вида `(A AND B) OR (C AND D)`, добавить
    точечный raw-JSON/расширенный condition для конкретного запроса, не усложняя базовую плоскую схему.
  - **Новые типы условий только под контент:** `authorSex`, `size`, `price`, `rarity`, `country`, `language`
    добавлять только когда эти поля реально появятся в книгах/таблицах.
  - **Evaluator registry v2:** если число `type`/операторов начнёт расти, выделить явный реестр
    `IConditionHandler` вместо текущего достаточного evaluator-подхода.
  - **Docs cleanup:** убрать или пометить историческими оставшиеся упоминания `RequestConfig` /
    `RecommendationScoringService` там, где они выглядят как актуальная документация.

- [ ] **GAME-13. Архетипный микс покупателей + composition policy.**
  Сейчас `RegularCustomerSpawner` строит всех покупателей одним архетипом (`PassiveAttemptsArchetype`
  с общим диапазоном попыток из `SalesTuning`) — поведение однородное. Ввести микс архетипов
  (browser / buyer / active-request и т.п.) со своими профилями попыток и `ICustomerCompositionPolicy`,
  которая решает «какой архетип у каждого покупателя» — прямой аналог `ICustomerTrafficResolver`
  («сколько»). Диапазон пассивных попыток переезжает в per-archetype конфиг; число попыток лучше
  связать с профилем желаний покупателя, а не с глобальной константой. Задел уже есть: выключенный
  active-mix и `PickActiveIndices` в `Ten*`-спавнерах, `PassiveActivePassiveArchetype` /
  `ActiveRequestArchetype`, TODO про «несколько режимов» в `DefaultCustomerSpawner`. Брать под возврат
  active-mix.

---

## 🎨 Визуал
