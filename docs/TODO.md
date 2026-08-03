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
  - Скриптовый спавн: `CustomerScriptConfig.ActivationQuestId`/`DialogueId` + `ScriptedCustomerSpawner`
    (читает quest-state) + fire-once `IDeliveredDialoguesService` (день о диалогах не знает).
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
    `TutorialTargetTag`; questId ↔ `quests.json`);
    аналитика (`seq_start`/`step_start` автоматом, `seq_complete` явно).
  - ✅ **Text-only немодальный callout** закрыт: текст без dim/lock, обновление на месте, teardown по концу run'а.
    Открыто отдельно: pointer/highlight для контролов свободного взаимодействия (Open Shop, список жанров,
    динамический «+» жанра) + динамическая регистрация таргетов из `PreparationGenreRowView`.
  - ✅ **Day 1 с Eddi / результат 1** закрыт: Eddi-факты приходят из BookSell через MessagePipe-сигналы,
    Day 1 ждёт завершения Eddi-диалога через browsing, обновляет callout по sale/fail с жанром из сигнала
    и не зависит напрямую от `Book.Sell`. Каждый Day 1 текст начинается как blocking-intro: sales tick ставится
    на паузу через `SalesPauseRequested` → location-scope `IInteractionLock`, после тапа текст остаётся callout'ом.
  - ✅ **Строгий day-gate** закрыт в C# `ITutorialSequence.IsEligible()`; отдельный `currentDayIs`
    condition-factory не нужен.
  - **Устойчивость Day 1** (известные ограничения v1 в §6.1): корректный resume посреди дня и cancel-path
    (закрыл Location/Preparation, не подтвердив) — recovery/блокировка закрытия окон.
  - **Локализация** текста туториала (сейчас ASCII/English) — через INF-4; поле `textKey` зарезервировано.
  - **Полировка**: feather-дырка шейдером за тем же API `TutorialBlackoutView`; player-facing Skip;
    вариант `awaitWindow("closed")`; опц. мягкий pointer на кнопку журнала по `QuestStarted`.
  - **Future soft pointer step**: вынести стрелку в отдельный step только когда понадобится сценарий
    "pointer without highlightClick"; текущий `TutorialHighlightClickStep` остаётся владельцем стрелки для
    blocking highlight-click флоу.
  - **Temporary tutorial UI coupling**: заменить знание `tutorial_day_1` / `click_genre_panel` / `text_4`
    внутри `GameplaySceneController` на явный gameplay/UI signal или policy для подавления auto-close у
    sale-chance `ContentWidget`; tutorial-content должен владеть id-шниками шагов.
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

- [x] **GAME-15. Согласовать стартовый пресет FTUE с каталогом книг.**
  Закрыто: runtime читает `books_converted.json`, где хватает всех 7 стартовых жанров. Хардкод-пресет в
  [FtueBootstrapper.cs:28-37](../Assets/Game/Features/Ftue/Services/FtueBootstrapper.cs) теперь сеет 54 книги:
  `Fantasy 10, Crime 10, Drama 12, Classic 6, Fact 6, Travel 6, Kids 4`.
  Вынести пресет из хардкода в `ftue.json` всё ещё отдельная задача, парная с рефактором `DailyBookSlots`.

- [x] **GAME-16. `CustomerScriptConfig` (Candidate E) — один дом для сценарных покупателей.**
  Закрыто: `QuestConfig` больше не несёт поведение встречи; Eddi и day-2 forced miss живут в
  `customer_scripts.json`.
  - `CustomerScriptConfig` (`[ConfigFile("customer_scripts")]`): `DayIndex?` или `ActivationQuestId` (ровно
    одно), `DialogueId?`, `CharacterId?`, `PassiveAttempts: ScriptedPassivePurchaseConfig[]`.
  - `ScriptedCustomerSpawner` — единственный decorator над `RegularCustomerSpawner`: заменяет обычные слоты
    в начале списка, валидирует dialogue, применяет fire-once по delivered-dialogues и берёт профиль персонажа
    из `CharacterConfig.FavoriteGenres`.
  - Eddi: `eddi_intro` (`activationQuestId=q_intro_eddi`, `characterId=eddi`, `dialogueId=eddy1`,
    `Fact forceHit:true → Travel forceHit:false`); day-1 wave 2: `day2_missed_sale`.
  - `QuestConfig`/`IQuest` очищены от `CharacterId`/`DialogueId`/`ScriptedPassivePurchases`; старые
    quest-spawner классы удалены.

- [ ] **GAME-17. Валидация связки «полка дня ↔ сценарный скрипт» (дни 1 и 2).**
  Уроки туториала держатся на согласии независимых мест, и **ни одно не валидируется** — при рассинхроне
  `ScriptedPassivePurchaseResolver` пишет одну строку в лог, а урок молча ломается.

  **День 1 (forced HIT):** `FtueBootstrapper.PresetCounts` сеет книгу жанра `Fact` →
  `FirstDayEntryFlow.BuildFirstDayShelfPreset()` прибивает `Fact`/`Travel` на полку (`AddFirstByGenre`) →
  `customer_scripts.json` / `eddi_intro.passiveAttempts` требует `Fact forceHit: true`. Инвариант: жанр
  форсированного **хита обязан быть на полке** — иначе `ScriptedPassivePurchaseResolver` не находит сток,
  форсированный хит становится miss, а `RemoveRemainingPassivePurchases` (Candidate D) тут же выкидывает второй
  beat → урок про sale chance исчезает.

  **Day-1 wave 2 (forced MISS):** `day2_missed_sale.passiveAttempts` требует `Travel forceHit: false`, а урок —
  «книги в жанре есть, но продажа не гарантирована». Инвариант **обратный, но родственный**: жанр
  форсированного промаха обязан быть **на полке дня 2** — иначе «книги были, а не продалось» это ложь (книг не
  было), и урок читается неверно. Полка дня 2 стокается не из FTUE-пресета (это day-1 seeding), а из
  preparation/restock-флоу — валидировать против него.

  Что сделать:
  - Провал форсированного **хита** — дефект контента: `LogError` вместо `LogWarning` (уже частично — проверить).
  - Editor-валидатор/EditMode-тест по **всем** записям `customer_scripts.json`: жанр каждой `PassiveAttempt`
    существует в `BookConfig.PrimaryGenre` и **присутствует на полке того дня**, к которому привязан скрипт
    (день из `DayIndex`, либо день визита для `ActivationQuestId`-скриптов). По образцу валидатора id-шников
    из GAME-10 §7; EditMode предпочтительнее editor-валидатора (падает в CI).
  - Связано с GAME-15 (пресет FTUE vs каталог книг) — чинить парно.

- [x] **GAME-18. Condition-driven запуск туториалов (re-evaluation loop в `TutorialService`).**
  **Статус:** минимальная доменная re-eval петля закрыта через `ITutorialReevaluationGate` +
  bootstrap bridge на sales/inventory/decor события. `TutorialService` остаётся единственным сервисом,
  который запускает туториалы, и у него уже есть activation-скан с проверкой `seq.IsEligible()`
  ([TutorialService.cs](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)). После переезда с JSON
  условия живут внутри C#-секвенций; не хватает **входящих событий**, от которых скан запускается.

  Проблема: **condition — это фильтр при триггере, а не повод запуститься.** `IsEligible` вызывается только
  из `OnTrigger` ([:218](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)) и `TryStartAsync`,
  а trigger enum values ровно 5 (`HubReady`, `LocationLoaded`, `PhaseChanged`, `QuestStarted`, `QuestCompleted`).
  Следствия:
  - `day == 1` — сработает (условие истинно весь день, `locationLoaded` — точный момент проверки).
  - «Купил предмет» — **нет**: инвентарь изменился, условие стало истинным, движку никто не сказал.
    Туториал поднимется позже, когда случайно прилетит один из 5 триггеров — момент запуска оторван
    от события на минуты.
  - «Диалог» — нет ни триггера, ни leaf-condition (`IDeliveredDialoguesService` движку не виден).
  - Добавлять по триггеру на источник не масштабируется: N новых `TutorialTrigger` enum values + N подписок +
    N asmdef-связей `Game.Tutorial` → все фичи.

  Решение — скопировать паттерн из `QuestsService` (**референс реализации, не зависимость**):
  `Subscribe()` ([QuestsService.cs:606](../Assets/Game/Features/Quest/Services/QuestsService.cs)) подписан
  на источники изменений (`_sales.Changed`, `_decor.PlacementChanged`, `_inventory.Changed`,
  `_dayProgress.PhaseChanged`), каждый зовёт `Reevaluate()` → автоактивация всех eligible по
  `IsActivationMet()`. Условие — единственная правда, событие — лишь повод пересчитать.

  Реализовано:
  - `ITutorialReevaluationGate.RequestReevaluation()` вызывает существующий trigger-agnostic scan.
  - `TutorialReevaluationBridge` в bootstrap-слое слушает `_sales.Changed`, `_decor.PlacementChanged`,
    `_inventory.Changed` и просит туториал пересканировать eligible sequences без ссылок
    `Game.Tutorial` → feature-слои.
  - `TutorialShopDecor` — проверочный stub на покупку decor через inventory-state; реальный overlay/content
    остаётся отдельной задачей.

  Острые углы (продумать до реализации):
  - **Туториал рисует overlay — квест меняет число.** У квестов `Reevaluate` зовётся синхронно внутри
    колбэка `_inventory.Changed`, и им это безразлично. Туториал в этот момент поднимет модальный blackout —
    возможно, посреди анимации окна магазина сразу после клика «купить». Нужен отложенный старт. Механизм
    есть: `ITutorialAutoStartGate` + `_rescanPending` + transition-guard; pending теперь означает
    «пересчитать, когда станет безопасно», а не «переиграть последний trigger».
  - **`_running` guard теряет события — регресс.** `OnTrigger` early-return'ит на `_running`
    ([:220](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)) и **ничего не запоминает**.
    У квестов состояние остаётся eligible и следующий `Reevaluate` подхватит; у туториала условие может
    стать истинным пока бежит другая секвенция — и потеряться навсегда. Лечится re-scan'ом в `finally`
    у `RunSequenceAsync` ([:343](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)); баг иначе
    будет плавающий.
  - **One-way completion становится критичнее.** `day == 1` истинно весь день → без `CompletedSequenceIds`
    секвенция перезапускалась бы на каждый re-eval. Защита есть
    ([:264](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)), но теперь несёт нагрузку,
    которую раньше страховал одноразовый триггер.
  - **`do/while` из `Reevaluate` не копировать** ([QuestsService.cs:356](../Assets/Game/Features/Quest/Services/QuestsService.cs)):
    цикл до стабилизации нужен квестам (активация одного активирует следующий); у туториала runner
    эксклюзивный, старт терминален — достаточно одного прохода.

  Порядок работ (шаги разносить):
  1. ✅ **Строгий day-gate в `TutorialDayOne.IsEligible()`** — закрыт в рамках текущей trigger-модели,
     без re-evaluation loop и без `currentDayIs` condition-factory.
  2. ✅ **Минимальный re-evaluation loop** — закрыт:
     trigger-agnostic scan, pending-scan вместо pending-trigger, re-scan после успешного завершения run'а
     и доменные re-scan события inventory/decor/sales через bootstrap bridge. Новые condition-factory leaf'ы
     добавлять только под конкретный будущий tutorial-content.

  Зачем это нужно уже сейчас: со второй секвенцией (туториал дня 2) цена отсутствия day-gate меняется.
  Порядок сейчас держится не на номере дня, а на цепочке «day1 завершился → следующий по priority».
  Дырки: (а) `SkipActiveAsync`/abort **не** помечают complete
  ([:337](../Assets/Game/Features/Tutorial/Services/TutorialService.cs)) → выход посреди дня 1 проиграет
  туториал дня 1 **на дне 2**, а всё остальное сдвинется; (б) повторный вход в локацию в тот же день (если
  `GameFlowLoop` его допускает — **проверить**) запустит туториал дня 2 в первый день; (в) при сдвиге
  будущий шаг с подсветкой панели не найдёт таргет → туториал молча деградирует, если такой шаг будет
  реализован fail-open. Ломается не happy path, а recovery.

  Дизайн-решение, которое надо принять явно: строгое `day == 1` означает «пропустил — потерял навсегда»
  (на дне 2 условие ложно), что **противоположно** текущей догоняющей очереди. Компромисс в духе остальных
  C#-контента: `CurrentDay == 1` + единый `tutorial_day_1` — «не раньше дня 1, но догонит,
  если пропустил».

- [ ] **GAME-19. `TutorialSignalLatch` — убрать бойлерплейт подписок из секвенций туториала.**
  **Триггер: делать, когда ВТОРАЯ секвенция начнёт латчить sales-факты** (сейчас такая одна — `TutorialDayOne`).
  Сегодня `TutorialDayOne` вручную держит три `ISubscriber<>`, три bool-флага, два поля жанра, `ResetLatch()` и
  `DisposeSubscriptions()` — ~40 строк механики на одну секвенцию. Вынести в переиспользуемый латч-хелпер
  уровня секвенции: заводится в `OnRunStarted()`, диспозится в `OnRunEnded()`, отдаёт шагам `Func<bool>` /
  `Func<string>`. Ориентировочная форма:
  `_latch.Watch<SalesPassiveSaleHappened>(m => IsEddi(m.CharacterId), m => m.Genre)`.
  Шаги остаются `TutorialAwaitFactStep` — механика ожидания не меняется.

  **Отклонено и не переизобретать: `ITutorialStep`, который сам подписывается на сигнал и ждёт его.**
  Причины (разбор 2026-07-17):
  - Подписки живут с `OnRunStarted()` **осознанно**: день продаж стартует сам
    (`SalesScreenView.OnInit` → `StartDayAsync`), независимо от туториала. Шаг, подписывающийся в
    `ExecuteAsync`, видит только будущее — всё, что случилось до него, потеряно → висяк или таймаут с
    ложным текстом. Латч (флаг + initial-check в `TutorialAwaitFactStep`) существует именно для этого.
  - Окно гонки реальное: между `Browsing` и продажей ~1-2 с, а между шагами есть `await PersistAsync(ct)`
    (запись в сейв, то есть кадры). Корректность повисла бы на скорости диска.
  - `IBufferedSubscriber<T>` не спасает: реплеит только **последнее** сообщение типа, а
    `SalesPassiveSaleHappened` летит от всех покупателей → продажа обычного покупателя затрёт Eddi; плюс
    буфер переживает день и протечёт в следующий run.
  - Правомерен такой шаг только для сигнала, который по определению не может случиться раньше шага (реакция
    на действие, которое сам туториал только что разблокировал). У дня 1 таких нет — все три факта порождает
    параллельно идущая симуляция.

- [ ] **GAME-21. Улучшения tutorial-движка (по мотивам `OnboardingFlowManager` из проекта bigmerge).**
  **Статус:** i/iii/iv/v закрыты в движке; ii (`tutorial parts` / stable checkpoints) оставлен отдельной
  будущей задачей, как и планировалось.
  Источник — разбор `OnboardingFlowManager.md` из другого проекта. Берём **точечные механики** для нашего движка
  (`TutorialService` / `ITutorialSequence` / `ITutorialStep`); их data-driven flow-слой (конфиги
  `onboarding_flow`/`conditions`, `ConditionTriggerController`) **НЕ берём** — это индирекция, которую §9
  ([INPROGRESS/TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md)) намеренно растворил; наш эквивалент — C#
  `IsEligible` + GAME-18 re-eval.

  1. ✅ **`CanStart` / гейт «верхнее окно — безопасное базовое, а не попап» (приоритет).**
     Проблема подтверждена логом 2026-07-21: `tutorial_shop_decor` стартовал **поверх открытого
     `NewspaperWindow`**, за ~16 мс до `RewardsWindow` — оверлей туториала конфликтует с активным попапом.
     Сейчас гейтинг ad-hoc (`TutorialHub.IsEligible` вручную чекает `!IsWindowShown<ResultsWindow>`). Ввести
     **дефолтный движковый гейт** «безопасно ли поднимать оверлей»: верхнее окно = HUD/база, стек UI не в
     переходе/анимации (аналог `Tutorial.CanStart` + `TutorialWindowWaitHelper.IsWindowShowed` из bigmerge).
     Место — рядом с `ContextAllows`/`TryStartEligible` в
     [TutorialService.cs](../Assets/Game/Features/Tutorial/Services/TutorialService.cs); соотнести с
     `ITutorialAutoStartGate` (сейчас ручной Block/Release) и transition-guard. Чинит overlay-collision
     системно, а не по одному окну.
  2. **Tutorial «parts» / стабильные чекпоинты (`CompleteTutorialPart` / `_completedTutorialParts`).**
     У bigmerge внутри одного туториала есть именованные промежуточные чекпоинты → resume с последнего
     **стабильного**, а не с нуля. У нас `ResumePolicy` = `Restart` | `FromStep` (по `NextStepId`), day-1 на
     `Restart` (best-effort, §6.1). Ввести именованные parts, чтобы mid-run resume был точнее Restart и надёжнее
     пер-шагового FromStep. Ложится в «stable checkpoint»-модель из [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md). Не
     срочно — брать под корректный mid-day-1 resume.
  3. ✅ **`TutorialDialogueStep` через window-waiter (образец `TutorialWindowWaiter` / `TutorialWindowCloseWaiter<T>`
     + `TutorialStaticWindow`).** Прямой референс для §6.3 `TutorialHub`: показать `DialogWindow`
     дженерик-шагом (silent action) → ждать закрытия окна `WindowCloseWaiter`-шагом. Реализовать
     `TutorialDialogueStep` (открыть окно по id графа `dialogues.json`, ждать закрытия) по этому паттерну;
     переиспользует `IUIManager.IsWindowShown/Spawned`. Нужен для реального контента `TutorialHub` (сейчас там
     stub-лог).
  4. ✅ **Дженерик `TutorialActionStep(Action)` (аналог `TutorialSilentStepAction`).**
     Сейчас узкие шаги: `TutorialLogStep` (только лог), `TutorialAssertStep` (чек+репорт). Добавить дженерик
     «выполнить действие → сразу дальше» (publish сигнала / side-effect / аналитика) — это heroes-style
     `TutorialSilentStepAction` из §9.1. Мелкий переиспользуемый примитив; основа для п.5.
  5. ✅ **Аналитика как silent-шаги на под-чекпоинтах.**
     Паттерн bigmerge: аналитика (`stage`/`step`/`state=start|end|skip`) эмитится дженерик-`SilentStepAction`'ами
     на логических чекпоинтах внутри `GetSteps()`, а не только на старт/конец секвенции — гранулярная воронка по
     под-шагам. Реализовать поверх п.4 (`TutorialActionStep` + `IAnalyticsService`). Смежно с аналитикой из
     GAME-10 §7 (`seq_start`/`step_start`/`seq_complete`).

- [ ] **GAME-22. Зафиксировать контракт «главный жанр» — `genres[0]` vs весь массив `genres`.**
  Сейчас две подсистемы читают `BookConfig.Genres` по-разному, и это разъедется на первой же книге с
  двумя жанрами.
  - **Весь массив** читает только условие активного запроса: `BookConditionRequestEvaluator.GetList`
    возвращает `book.Genres`, и `genres contains "Fact"` матчится по любому элементу.
  - **Только `genres[0]`** (через `BookConfig.PrimaryGenre`) читают все остальные: статистика и квесты
    (`SalesStatsService.TryResolveGenre` → `activePickGenre` / `soldGenre`), пассивные продажи
    (`GenreShelfPicker`, `WeightedPassiveSaleSelector`), спрос локации (`LocationDemandProfileProvider`),
    любимые жанры персонажей (`ScriptedCustomerSpawner`), итоги дня (`ResultsSummaryBuilder.SoldByGenre`),
    жанровые строки инвентаря (`BookGenreRowSource`) и **визуал** — ярлык и иконка жанра в
    `BookCardView` (`_genreLabel`, `LoadGenreIcon`) и спрайт в баббле покупателя (`CustomerBubbleBinder`).

  **Следствие расхождения.** Книга `["Classic","Fact"]` пройдёт Fact-запрос и получит `Excellent`, но
  в `activePickGenre Fact` не попадёт — уйдёт в счётчик `Classic`. То есть квест Милли будет визуально
  выполняться и не завершаться.

  **Почему это ещё не всплыло.** У всех 100 книг в `books.json` ровно один жанр — проверено.

  **Направление решения.** `genres[0]` — уже де-факто «основной тип книги», он определяет её визуал, и это
  осознанное решение; отдельная сущность-«главный жанр» не нужна. Значит выбор не 50/50: выбивается
  evaluator. Варианты:
  1. Оставить как есть, но **явно задокументировать** в [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md),
     что `genres contains` — это «есть среди жанров», а зачёт квеста — по основному. Дешевле всего, но
     расхождение остаётся ловушкой для контентщика.
  2. Свести evaluator к основному жанру (`genres[0]`) — самое согласованное поведение, но теряется
     возможность «книга подходит и как Classic, и как Fact».
  3. Добавить отдельный тип условия (`primaryGenre` рядом с `genres`), чтобы автор запроса выбирал сам.

  **Что сделать:** выбрать вариант, зафиксировать в
  [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md) и в XML-доке `BookConfig.PrimaryGenre`,
  покрыть тестом на двужанровой книге. Брать **до** того, как в `books.json` появится первая книга с
  несколькими жанрами. Смежно: GAME-12 (модель условий запроса).

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

- [ ] **INF-12. Валидаторы контента: окно вместо пунктов меню + подтянуть рантаймовые в build-гейт.**
  Сейчас проверки запускаются тремя разными способами: агрегат `Tools → Configs → Run Pre-Build
  Validation` (гоняет реестр `PreBuildValidationGate.Validators`), два индивидуальных пункта меню
  (`Validate Active Requests`, `Validate Book Box Pools`), и отдельно рантаймовые валидаторы — только при
  входе в Play mode. «Прогнать все проверки проекта» одной кнопкой сейчас нельзя в принципе.
  Что решено **не** делать: подменю `Tools/Validation/*`. `[MenuItem]` требует константу времени
  компиляции, поэтому меню невозможно сгенерировать из реестра — оно всегда будет ручным параллельным
  списком. При четырёх валидаторах это добавит третий список для синхронизации (реестр, таблица в
  [BUILD.md](BUILD.md) §0, меню) и ничего не даст.
  Что сделать, когда валидаторов станет ~6 или прогонять их понадобится чаще, чем перед билдом:
  - Окно `Tools/Validation/Dashboard` в `Game.Build.Editor` — единственной сборке, которая уже видит все
    валидаторы. В `Game.Configs.Editor` (к `ConfigEditorWindow`) не класть: его asmdef пришлось бы
    подписать на `Book.Sell.Editor` и `Game.Rewards.Editor`, а это инверсия слоёв (фичи зависят от
    `Configs`, не наоборот).
  - Окно итерирует `PreBuildValidationGate.Validators` — снять `private`, добавить в кортеж описание.
    Тогда реестр становится единственным источником правды и новый валидатор появляется в UI сам.
  - Индивидуальные пункты меню валидаторов при этом удалить — окно их заменяет, дубликат исчезает.
  Отдельно и независимо (даёт больше, чем любая перестановка меню):
  - Переписать `ItemReferenceValidator` и `DecorConfigValidator` на чтение JSON напрямую и внести их в
    реестр гейта. Сейчас они требуют `IConfigsService`, поэтому живут только в Play mode: в редакторе
    бросают и блокируют вход, в билде пишут `LogError` и пропускают сборку.
  - Именно они ловят предметы без источника (кейсы `postcard` и `map`) — то есть самый ценный класс
    контентных ошибок сейчас не защищён build-гейтом.
  Зафиксировано как «известный пробел» в [BUILD.md](BUILD.md) §0.

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

- [ ] **GAME-23. Вынести memories из `characters.json` в отдельный конфиг.**
  **Сейчас делать НЕ надо** (разбор 2026-08-03). Memories живут вложенно в
  [CharacterConfig.Memories](../Assets/Game/Features/Configs/Models/CharacterConfig.cs), и это оправдано:
  - Вложенность структурно выражает правило из [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) §0 «одна memory =
    один персонаж» (shared memory отложена). Отдельный файл снимает гарантию — «memory без персонажа» и
    «memory у двух персонажей» станут возможны, и ловиться будут в рантайме, а не на компиляции.
  - Разделение не упрощает ни одно из **9** мест чтения `config.Memories` (`CharacterModelFactory`,
    `CharactersService`, `FtueBootstrapper`), а `CharactersService.BuildCatalog` придётся распилить на два
    прохода: сейчас он строит каталог и обратный индекс `questId/chainId → characterId` за один обход
    `GetAll<CharacterConfig>()`.
  - Memory не самостоятельная сущность: своего lifecycle у неё нет, `Unlocked` — проекция состояния квеста.
    По этому признаку она ближе к `QuestConfig.Tasks` (вложенные), чем к `QuestConfig` в цепочке (плоские
    + `ChainId`).
  - Цена: новый тип с `[ConfigFile]`, запись в `manifest.json`, синк в StreamingAssets — иначе
    `PreBuildValidationGate` валит билд ([BUILD.md](BUILD.md)).

  **Триггер — делать, когда сработает любое из трёх:**
  1. **Объём.** У персонажей набралось по 5-10 воспоминаний с абзацами текста, и профиль персонажа
     (5 строк) тонет в контенте. Порог: файл перестал читаться глазами.
  2. **Разные авторы.** Тексты воспоминаний ведёт нарративщик, а `favoriteGenres`/`discoveryQuestIds` —
     геймдизайнер: разные файлы снимают конфликты в git.
  3. **Безличных memories стало много.** Одна запись у псевдо-персонажа `owner` — нормально; десяток
     превращает его в свалку.

  **Как делать, когда возьмётесь:** плоский массив `MemoryConfig : IConfig` с полем `characterId` — по
  образцу `quests.json`/`ChainId`, а не `{ "characterId": [...] }`: `IConfigsService` работает через
  `GetAll<T>()`/`Get<T>(id)`, плоский массив ложится без переходника. Формат сейва менять не придётся —
  `SavedCharacters.SeenMemoryIds` уже плоский root-level `HashSet<string>`, то есть id воспоминаний де-факто
  глобально уникальны; `UnlockedMemoryIds` остаётся per-character.

  **Что сделать сейчас вместо разделения** (даёт ту же безопасность без его цены) — две проверки в
  валидатор характеров, по образцу `LocationDemandConfigValidator`:
  - дубликаты `memory.Id` **между** персонажами — сейчас молча ломают `SeenMemoryIds` и леджер;
  - `QuestId`/`QuestChainId`, ссылающиеся на несуществующий квест — такая memory не откроется никогда,
    и об этом никто не сообщит.

---

## 🎨 Визуал
