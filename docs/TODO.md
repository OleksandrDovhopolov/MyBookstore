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
  - Награды/эффекты и условия визита оставить в отдельных задачах GAME-3 и GAME-5.

- [ ] **GAME-3. Permanent quest effects / world state (= Этап 6).**
  Что сделать:
  - Выдавать награды через `QuestRewardConfig` → `IRewardGrantService`.
  - Применять постоянные эффекты через `QuestWorldEffectConfig`-хендлеры.
  - Сохранить `timestamp` и `appliedEffects` для завершённых квестов.
  - Сделать применение эффектов идемпотентным при повторной загрузке.
  - Поднять `QuestsSaveKeys.StateSchemaVersion`.

- [x] **GAME-5. `LocationVisits` + условия `visitLocation` / `locationIs`.**
  Что сделать:
  - Добавлен persisted-счётчик визитов по локациям (`location_visits`, schema v1).
  - Визит записывается только после успешного входа в локацию.
  - Текущая локация хранится runtime-only и очищается при возврате в hub.
  - Добавлены condition-factory `visitLocation` и `locationIs`.
  - Добавлены DI-регистрация, force-construction save-hook'а и EditMode-тесты.

- [ ] **GAME-6. Runtime `DialogStep` через `CustomerDirector`.**
  Что сделать:
  - Добавить `DialogStep` как middle-step покупателя.
  - Вставлять runtime-диалоги через `CustomerDirector.InsertNext(...)`.
  - Для заранее известных сюжетных/квестовых покупателей использовать `ScriptedSequenceArchetype` / `QuestCharacterArchetype`.
  - Держать interaction lock до завершения dialogue UI.
  - Освобождать interaction lock на `Exit`.

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

- [ ] **GAME-9. Compact baseline для sales-задач квестов.**
  Что сделать:
  - Заменить полный `SalesStatsStateDto` в `SavedQuest.TaskBaseline` на compact DTO по решению [ADR-0008](adr/0008-quest-sales-progress-persistence.md).
  - Для `soldGenre` хранить baseline только нужного жанра.
  - Для `soldGenreAtLocation` хранить baseline только пары `(locationId, genre)`.
  - Для `soldGenreInSingleDay` хранить только данные, нужные для отсечения продаж до активации задачи: день активации и count нужного жанра на момент активации.
  - Сохранить текущую pull-based модель conditions/scoped reader; event-driven progress оставить будущим направлением.
  - Добавить миграцию/совместимость со старым save, где baseline ещё полный `SalesStatsStateDto`, и EditMode-тесты на reload.

---

## 🛠️ Инфраструктура

- [ ] **INF-4. Localization.** Слой локализации (ключи вместо строк, таблицы переводов, рантайм-смена
  языка). Закладывать заранее — под Steam-релиз на нескольких языках.

- [ ] **INF-5. Детерминированный (seeded) RNG.** Отдельный сервис для воспроизводимой генерации
  дневного спроса/покупателей и тестов баланса. Заменить обычный рандом (напр. в `RandomizeAsync`).

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

---

## 🎨 Визуал

- [ ] **VIS-1. Анимация «полёта» золота из HUD к кнопке** (в newspaper-окне):
  золото вылетает из HUD-счётчика и летит к кнопке покупки. Зависит от подключённого DoTween.
