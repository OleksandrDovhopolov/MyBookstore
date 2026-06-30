# TODO

Рабочий список задач. Разбит по категориям: **Геймплей**, **Инфраструктура**, **Визуал**.
Новые задачи добавляются в конец соответствующей категории.

Статусы: `[ ]` — todo, `[~]` — в работе, `[x]` — готово.

---

## 🎮 Геймплей

- [ ] **GAME-1. Replace location demand genre whitelist with weighted demand.**
  Current `LocationDemandProfileProvider` treats `LocationConfig.DemandGenres` as the pool of genres customers can request/passively buy. This is too strict for a Tiny Bookshop-like model: every stocked genre should remain sellable, while location demand genres get a higher chance/weight. Rework passive demand calculation so `DemandGenres` means boosted/preferred genres, not allowed-only genres. Update tests around `LocationDemandProfileProvider` / `RequestedGenrePassiveResolver` to cover non-demand genres still being sellable with lower chance.

- [~] **GAME-2. Фича `Game.Quest` — доделать слайс.**
  Ядро готово (Этапы 1–5 + 4b): сборки `Game.Quest`/`.API`/`.Tests.Editor`, API/enum/конфиги, условия
  `decorEquipped`/`haveItem`/`weatherIs`, `QuestsService` (lifecycle/цепочки/auto-award), save
  (Awarded/Failed не переигрываются), baseline «после старта задачи». Решения — [adr/0007-quest-system.md](adr/0007-quest-system.md).
  **Осталось:** реальная цепочка-слайс (контент квестов на боевом конфиге, не заглушка) + UI журнала
  (`JournalWindow`). Награды/эффекты и условия визита вынесены в отдельные задачи — GAME-3 и GAME-5.

- [ ] **GAME-3. Permanent quest effects / world state (= Этап 6).**
  После завершения квестов выдавать награды (`QuestRewardConfig` → `IRewardGrantService`) и применять постоянные
  эффекты (`QuestWorldEffectConfig`-хендлеры): достроенный замок на Far Beach даёт `+2` клиента в день и открывает Cave;
  включённый маяк — ночная торговля + бонус `Mystery/Thriller`; цветущий навес усиливает `Poetry/Romance`; улики
  меняют доступность детективных цепочек. Эффекты **идемпотентны** при повторной загрузке (Awarded не переигрывается;
  bump `QuestsSaveKeys.StateSchemaVersion`: Awarded += timestamp + appliedEffects).

- [ ] **GAME-5. `LocationVisits` + условия `visitLocation` / `locationIs`.**
  Новая persisted-подсистема счётчика визитов по локациям + entry-hook (инкремент при входе в локацию) +
  current-location seam (по образцу GAME-4 для продаж). Condition-factory `visitLocation` («посетить N раз») и
  `locationIs` («находиться на локации сейчас»). Разблокирует слайс «An Empire of Sand».

---

## 🛠️ Инфраструктура

- [ ] **INF-2. Подключить DoTween** (импорт пакета + asmdef-ссылки + базовая обёртка/хелперы под анимации).

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
  золото вылетает из HUD-счётчика и летит к кнопке покупки. Зависит от **INF-2 (DoTween)**.

- [ ] **VIS-3. Свёрстать окно декора** (decor window).
