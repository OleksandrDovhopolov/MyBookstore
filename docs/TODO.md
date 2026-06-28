# TODO

Рабочий список задач. Разбит по категориям: **Геймплей**, **Инфраструктура**, **Визуал**.
Новые задачи добавляются в конец соответствующей категории.

Статусы: `[ ]` — todo, `[~]` — в работе, `[x]` — готово.

---

## 🎮 Геймплей

- [ ] **GAME-1. Replace location demand genre whitelist with weighted demand.**
  Current `LocationDemandProfileProvider` treats `LocationConfig.DemandGenres` as the pool of genres customers can request/passively buy. This is too strict for a Tiny Bookshop-like model: every stocked genre should remain sellable, while location demand genres get a higher chance/weight. Rework passive demand calculation so `DemandGenres` means boosted/preferred genres, not allowed-only genres. Update tests around `LocationDemandProfileProvider` / `RequestedGenrePassiveResolver` to cover non-demand genres still being sellable with lower chance.

- [ ] **GAME-2. Спроектировать и реализовать фичу `Game.Quest`.**
  Создать новую сборку `Game.Quest` (`Game.Quest`/`Game.Quest.API`/`Game.Quest.Tests.Editor`, единый стиль с
  `Game.Inventory`/`Game.Decor`) по спеке [QUESTS.md](QUESTS.md): data-driven квесты, задачи, цепочки,
  save-состояния `Pending/Active/ReadyToAward/Awarded/Failed`, события прогресса и permanent effects. Условия
  переиспользуют существующий движок `Game.Conditions` (новые `IConditionFactory` по образцу `SoldGenreConditionFactory`),
  а не пишутся заново. Сезонов в MVP нет — без `seasonIs`. Первая версия работает без персонажей: `characterId` в
  цепочке опционален/null, чтобы позже подключить N персонажей с M цепочками без миграции базовой модели.
  **Зависит от GAME-4.** Старый placeholder `Quest.asmdef` удалён.

- [ ] **GAME-4. Продажи по локациям и по дням в `Game.SalesStats` (prerequisite для квестов).**
  Текущий `ISalesStatsReader` отдаёт только глобальный кумулятив (`GetSold(BookGenre)`, `TotalSold`) — без разреза
  по локации и без дневного счётчика. Квестовые условия `soldGenreAtLocation` / `soldGenreInSingleDay` / `soldByTags`
  (продажи по `BookConfig.Tags`) на этом не построить. Расширить запись/чтение продаж измерениями `(локация)`,
  `(день)` и тегами книги; обновить тесты `SalesStatsServiceTests`. Проектировать вместе с baseline-snapshot прогресса
  квеста (scoped reader, см. [QUESTS.md](QUESTS.md) §11.2) — чтобы «продай 15 после старта задачи» считалось от момента
  активации, а не от lifetime-итога. Должно быть сделано **до** GAME-2.

- [ ] **GAME-3. Permanent quest effects / world state.**
  После завершения квестов сохранять уникальные состояния мира: достроенный замок на Far Beach дает постоянный
  `+2` клиента в день и открывает Cave; включенный маяк открывает ночную торговлю на побережье и бонус к
  `Mystery/Thriller`; цветущий навес фургона усиливает `Poetry/Romance`; найденные/показанные улики меняют
  доступность детективных цепочек. Эффекты должны быть идемпотентными при повторной загрузке save.

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
