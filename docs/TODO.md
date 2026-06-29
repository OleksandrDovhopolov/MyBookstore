# TODO

Рабочий список задач. Разбит по категориям: **Геймплей**, **Инфраструктура**, **Визуал**.
Новые задачи добавляются в конец соответствующей категории.

Статусы: `[ ]` — todo, `[~]` — в работе, `[x]` — готово.

---

## 🎮 Геймплей

- [ ] **GAME-1. Replace location demand genre whitelist with weighted demand.**
  Current `LocationDemandProfileProvider` treats `LocationConfig.DemandGenres` as the pool of genres customers can request/passively buy. This is too strict for a Tiny Bookshop-like model: every stocked genre should remain sellable, while location demand genres get a higher chance/weight. Rework passive demand calculation so `DemandGenres` means boosted/preferred genres, not allowed-only genres. Update tests around `LocationDemandProfileProvider` / `RequestedGenrePassiveResolver` to cover non-demand genres still being sellable with lower chance.

- [~] **GAME-2. Спроектировать и реализовать фичу `Game.Quest`.**
  Создать новую сборку `Game.Quest` (`Game.Quest`/`Game.Quest.API`/`Game.Quest.Tests.Editor`, единый стиль с
  `Game.Inventory`/`Game.Decor`) по спеке [QUESTS.md](QUESTS.md): data-driven квесты, задачи, цепочки,
  save-состояния `Pending/Active/ReadyToAward/Awarded/Failed`, события прогресса и permanent effects. Условия
  переиспользуют существующий движок `Game.Conditions` (новые `IConditionFactory` по образцу `SoldGenreConditionFactory`),
  а не пишутся заново. Сезонов в MVP нет — без `seasonIs`. Первая версия работает без персонажей: `characterId` в
  цепочке опционален/null, чтобы позже подключить N персонажей с M цепочками без миграции базовой модели.
  **Ядро готово (Этапы 1–5 + 4b) — на паузе:** сборки, API/enum/конфиги, условия `decorEquipped`/`haveItem`/`weatherIs`,
  `QuestsService` (lifecycle/цепочки/auto-award), save (Awarded/Failed не переигрываются), baseline «после старта задачи».
  **Осталось (поздние итерации):** награды/эффекты (GAME-3), условия визита (GAME-5), реальная цепочка-слайс + UI журнала.
  Решения зафиксированы в [adr/0007-quest-system.md](adr/0007-quest-system.md). Старый placeholder `Quest.asmdef` удалён.

- [x] **GAME-4. Продажи по локациям и по дням в `Game.SalesStats` (prerequisite для квестов).**
  `SalesStatsStateDto` расширен `SoldByLocationGenre` + `SoldByDayGenre` (schema v2); `ISalesStatsReader` +=
  `GetSold(genre, location)`, `GetSoldOnDay(day[, genre])`, `GetMaxSoldInSingleDay(genre)`; `SaleContext`
  прокинут через `SalesDayResult`/`SalesDayCommitService`. Condition-factory `soldGenreAtLocation`,
  `soldGenreInSingleDay` зарегистрированы. `soldByTags` отложен (нет учёта по тегам — добавить при необходимости).

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

- [x] **GAME-6. Quest baseline scoped-reader (Этап 4b).**
  Прогресс «продай N **после старта задачи**»: `SalesStatsService` реализует `ISalesStatsBaselineSource`
  (`CaptureBaseline`/`CreateScopedReader` → `current − baseline`, в т.ч. best-day по дням); `QuestsService` при
  активации продажной задачи снимает snapshot, пересобирает completion через scoped-парсер и хранит baseline в
  `SavedQuest.TaskBaseline` (save schema v2, миграция v1→v2). Авто для всех продажных условий.

---

## 🛠️ Инфраструктура

- [~] **INF-1. Загрузка спрайтов жанровых книг из Addressables + gating бутстрапа.**
  Сейчас `GameplaySceneView` ([GameplaySceneView.cs](../Assets/Game/UI/GameplayScene/GameplaySceneView.cs))
  держит спрайты жанров (`_classicGenreSprite` … `_fantasyGenreSprite`) как serialized-поля и резолвит
  их в `GetGenreSprite(BookGenre)`. Нужно грузить их из Addressables (через `IUiSpriteProvider` /
  `ProdAddressablesWrapper`) для `_genreBookCountPool`, по аналогии с newspaper/rewards.
  `MainSceneBootstrap` ([MainSceneBootstrap.cs](../Assets/Game/UI/GameplayScene/MainSceneBootstrap.cs))
  должен дождаться загрузки этих спрайтов перед показом контента:
  `await UniTask.WaitUntil(() => IsWindowShown && spritesLoaded)` — проверять **оба** условия
  (окно показано И спрайты загружены).
  Код готов: serialized-поля и `GetGenreSprite` убраны из view; маппинг `BookGenre → address`
  через `UiSpriteCatalog.TryGetAddress`; загрузку владеет `GameplaySceneController`
  (`LoadGenreSpritesAsync` + флаг `SpritesLoaded`); bootstrap ждёт оба условия.
  **Осталось (Unity Editor, вручную):** пометить 7 жанровых спрайтов как Addressable и заполнить
  `UiSpriteCatalog.asset` записями (Key = имя жанра, Address).

- [ ] **INF-2. Подключить DoTween** (импорт пакета + asmdef-ссылки + базовая обёртка/хелперы под анимации).

- [ ] **INF-3. Audio-сервис.** `IAudioService` с шинами music/sfx/ambient, громкостями в настройках
  и загрузкой клипов через Addressables. Базовый «уютный» звук — половина впечатления от cozy-sim.

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

---

## 🎨 Визуал

- [ ] **VIS-1. Анимация «полёта» золота из HUD к кнопке** (в newspaper-окне):
  золото вылетает из HUD-счётчика и летит к кнопке покупки. Зависит от **INF-2 (DoTween)**.

- [ ] **VIS-2. Свёрстать окно Preparation.** Добавить в окно полку с инвентарём в разрезе жанров.
  Окно: `PreparationWindow` (`Assets/Game/Features/Preparation/UI/`).

- [ ] **VIS-3. Свёрстать окно декора** (decor window).
