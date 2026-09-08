# 📚 Docs Catalog — точка входа

Карта всех `.md`-документов проекта: что за что отвечает, как документы пересекаются и в какую
сторону идут зависимости. **Начинай отсюда.** При добавлении нового `.md` — заведи строку в этом
каталоге.

> Статус заполнения: детально описаны **правила формата**, **сервисные системы из `docs/SERVICES/`**
> (ADDRESSABLES, UI_SYSTEM и пр.) и **все ADR**. Остальные строки — заглушки (`⏳`), их описания
> дополняются по мере работы.

---

## 🗂 Навигация / meta

| Документ | Что описывает | Связи |
|---|---|---|
| ✅ [CATALOG.md](CATALOG.md) | Главная карта `docs/`: какие документы есть в проекте, как они сгруппированы и где отмечены связи между ними. | Ссылается на все актуальные разделы каталога; не включает `docs/archive/` в рабочий контур. |

---

## Легенда

**Тип документа:**

- 📐 **Правило / конвенция формата** — как писать код/доки. Не описывает функционал.
- 🧱 **Инфраструктура / сервис** — фундамент (`docs/SERVICES/`). *Только от неё зависят*; сама не зависит от фич/UI/систем выше.
- 🖼 **UI-система** — фреймворк окон.
- 🎮 **Игровая система / фича** — геймплейный функционал.
- 🧭 **ADR** — зафиксированное архитектурное решение (может ссылаться и быть указанным).
- 🚧 **In-progress / спека** — в работе (`docs/INPROGRESS/`).
- 🛠 **Improvement** — заметка по улучшению существующей системы (`docs/improvements/`).
- 📋 **Процесс / трекинг** — TODO.
- 🗄 **Архив** — историческое, не поддерживается (`docs/archive/`).

**Статус строки:** ✅ заполнено · ⏳ заглушка (на будущее).

**Правило направления зависимостей (🧱):** документ, помеченный 🧱, описывает систему, у которой
**нет исходящих зависимостей** на фичи/UI — на неё *только ссылаются*. Это совпадает с правилами
слоёв из [ASMDEF_RULES.md](ASMDEF_RULES.md) (Infrastructure → не знает о фичах). Стрелка `→` ниже
читается как «зависит от».

---

## 📐 Правила формата (отдельно от функционала)

| Документ | Что описывает | Связи |
|---|---|---|
| ✅ [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md) | Язык в коде — **только English** (комментарии, XML-doc, логи, исключения, TODO, видимые строки). RU допустим в `docs/INPROGRESS`, `docs/archive`, reference-заметках и в game-content JSON. Применяется по мере касания кода, без массового рефактора. | Действует на весь `Assets/**/*.cs`. |
| ✅ [ASMDEF_RULES.md](ASMDEF_RULES.md) | Организация `.asmdef`: слои (Domain → Application → Presentation → Facade, плюс API/Tests), **направление зависимостей строго вниз**, `noEngineReferences`/`autoReferenced`, именование, матрица зависимостей фич, типичные ошибки, чеклист. | Задаёт правило, на котором стоит маркер 🧱 (инфраструктура не зависит от фич). Фичи ссылаются друг на друга только через `.API`. |

---

## 🧱 Инфраструктурные / сервисные системы (`docs/SERVICES/`, фундамент — *только от них зависят*)

| Документ | Что описывает | Зависит от | На неё ссылаются / пересечения |
|---|---|---|---|
| ✅ [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md) | Единая обёртка над Unity Addressables — `ProdAddressablesWrapper` (namespace `Infrastructure`): ref-counted кэш хэндлов, sync/async загрузка, группы и warmup по лейблам, релиз по объекту/адресу/группе. Прямых вызовов `Addressables.*` в проде нет. | — (ничего; чистая инфраструктура) | [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) (загрузка префабов окон), [NEWSPAPER_REWARDS_SPRITE_SERVICE.md](SERVICES/NEWSPAPER_REWARDS_SPRITE_SERVICE.md) (sprite provider), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) |
| ✅ [AUDIO_SYSTEM.md](SERVICES/AUDIO_SYSTEM.md) | Аудио-сервис (`Assets/Game/Infrastructure/Audio/`, asmdef `Infrastructure`): шины music/sfx/ambient, `AudioCatalog`, музыкальный дирижёр хаб/день, папка `Assets/Game/Audio`, громкости в настройках и загрузка клипов через Addressables. | → [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md) | — |
| ✅ [ANIMATION_BUILDER.md](SERVICES/ANIMATION_BUILDER.md) | Текущая система UI-анимаций ресурсов: полёты `World -> Screen`, `Screen -> World`, `Screen -> Screen`, API/settings в `Infrastructure.ResourceAnimations`, runtime под `Game.Core.UI`, DOTween, particle pool, target registry. | → [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) | [ANIMATION_TARGET_SYSTEM.md](SERVICES/ANIMATION_TARGET_SYSTEM.md) |
| ✅ [ANIMATION_TARGET_SYSTEM.md](SERVICES/ANIMATION_TARGET_SYSTEM.md) | Reference-документ по `AnimationTarget` / `AnimationBuilder`: цели приземления, реестр таргетов и переиспользуемый движок летающих UI-наград. | — | [ANIMATION_BUILDER.md](SERVICES/ANIMATION_BUILDER.md) |
| ⏳ [LOGGING_SYSTEM.md](SERVICES/LOGGING_SYSTEM.md) | _на будущее_ | — | [improvements/LOGGING_SYSTEM_REVIEW.md](improvements/LOGGING_SYSTEM_REVIEW.md), [improvements/LOGGING_ASMDEF_IMPROVEMENT.md](improvements/LOGGING_ASMDEF_IMPROVEMENT.md) |
| ✅ [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) | Runtime bootstrap: фазы загрузки, порядок warmup/config/save/FTUE/finalization, `SaveService`, `HttpSaveStorage` и startup sync local vs server. | — | [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [API_ENDPOINTS.md](SERVICES/API_ENDPOINTS.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md), [GameFlowLoop.md](GameFlowLoop.md) |
| ✅ [CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md) | Runtime-путь загрузки data-driven конфигов: `IConfigsService`, base/override слои, LocalFolder/StreamingAssets/Server sources, snapshot и Firebase RC partial overrides. | — | [ADR-0002](adr/0002-config-system-architecture.md) |
| ✅ [CONFIG_SERVER_API.md](SERVICES/CONFIG_SERVER_API.md) | Контракт config backend: public manifest/config endpoints, admin publish/history/rollback/promote, ETag и окружения. | — | [ADR-0002](adr/0002-config-system-architecture.md) |
| ✅ [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) | Editor-инструмент публикации конфигов: Pull/Publish, bootstrap, conflict handling, history, typed/generic editing и валидация. | — | [ADR-0002](adr/0002-config-system-architecture.md), [SECRETS.md](SERVICES/SECRETS.md) |
| ✅ [API_ENDPOINTS.md](SERVICES/API_ENDPOINTS.md) | Контракт GameServer API: save `/api/v1/save/global`, resources/inventory/rewards/wheel/battle-pass/IAP/liveops/configs/admin. Save-секция отражает текущий клиентский `HttpSaveStorage`. | — | [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md), [CONFIG_SERVER_API.md](SERVICES/CONFIG_SERVER_API.md) |
| ⏳ [FIREBASE_INTEGRATION.md](SERVICES/FIREBASE_INTEGRATION.md) | _на будущее_ | — | [SECRETS.md](SERVICES/SECRETS.md) |
| ⏳ [NEWSPAPER_REWARDS_SPRITE_SERVICE.md](SERVICES/NEWSPAPER_REWARDS_SPRITE_SERVICE.md) | Спека долгоживущего sprite-сервиса для newspaper/rewards UI: кэширование, объединение локального `ScriptableObject` и Addressables за единым API, управление lifetime спрайтов вне окна. | → [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md) | Newspaper/rewards UI |
| ⏳ [SECRETS.md](SERVICES/SECRETS.md) | _на будущее_ (политика секретов/credentials) | — | [FIREBASE_INTEGRATION.md](SERVICES/FIREBASE_INTEGRATION.md), [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) |
| ⏳ [README_Commands.md](SERVICES/README_Commands.md) | _на будущее_ (система команд) | — | — |

---

## 🖼 UI-система

| Документ | Что описывает | Зависит от | Связи / пересечения |
|---|---|---|---|
| ✅ [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) | Фреймворк окон (`Game.UI`, `Assets/Game/Core/UI/`): `IUIManager.ShowAsync/Hide`, `WindowController<TView>`/`WindowView`, слои (`WindowType` × `WindowLayer`), сортировка, кэш `UIStorage`, стек, анимации, blocker, фильтр показа. | → [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md) (загрузка префабов окон через `ProdAddressablesWrapper`) | [improvements/UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md) (роадмап), [DI_ARCHITECTURE_WINDOWFACTORY.md](archive/DI_ARCHITECTURE_WINDOWFACTORY.md), [ANIMATION_BUILDER.md](SERVICES/ANIMATION_BUILDER.md). Потребители — все фичи с окнами. |
| ✅ [UI_RULES.md](UI_RULES.md) | Живые правила хранения и использования UI-ресурсов: общие ассеты, feature-owned ассеты, shared-кнопки, core UI-префабы, политика обновления. | → [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) | Дополняет [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) практическими правилами размещения окон, префабов, спрайтов, материалов и shared UI-ассетов. |

---

## 🧭 ADR — архитектурные решения (`docs/adr/`)

| ADR | Решение | Status | Пересечения (Related / Supersedes) |
|---|---|---|---|
| ✅ [0001](adr/0001-save-data-modular-payload.md) | **Save data — modular opaque payload.** `SaveData` = `Dictionary<string, ModulePayload>`; каждая фича владеет своей моделью и версией схемы, ядро Save не знает о данных фич. | Accepted | Указывается из [0003](adr/0003-customer-simulation.md), [0007](adr/0007-quest-system.md); текущее поведение — [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) |
| ✅ [0002](adr/0002-config-system-architecture.md) | **Config system — клиентская архитектура.** Data-driven конфиги (`BookConfig`/`LocationConfig`/…), редактируемые без ребилда, версионирование, A/B. | Accepted | [CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md), [CONFIG_SERVER_API.md](SERVICES/CONFIG_SERVER_API.md), [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) + серверный ADR |
| ✅ [0003](adr/0003-customer-simulation.md) | **Customer simulation — фаза «Продажа» как real-time симуляция** конкурентных агентов-покупателей (приход → бродят → пассивные/активные покупки). | Accepted | Supersedes пошаговый Sales MVP. Related: [0001](adr/0001-save-data-modular-payload.md), [CORE_LOOP.md](CORE_LOOP.md), [INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md). Уточняется [0004](adr/0004-stock-model-hybrid-sale-chance.md) |
| ✅ [0004](adr/0004-stock-model-hybrid-sale-chance.md) | **Stock model (hybrid) + passive sale chance.** Сток книг = per-title + агрегат по жанрам; пассивная продажа — вероятностный `baseSaleChance`, падающий по мере распродажи. | Accepted | Уточняет пассивную часть [0003](adr/0003-customer-simulation.md). Стадия-1 уточнена [0006](adr/0006-passive-sales-requested-genre.md). Related: [CORE_LOOP.md](CORE_LOOP.md), [FTUE.md](FTUE.md) |
| ✅ [0005](adr/0005-customer-visuals-in-location-scene.md) | **Customer visuals в world-space `LocationScene`.** Визуалы покупателей живут в отдельной additive-сцене поверх хаба; якоря/спавнер регистрируются в `LocationInstaller`. | Accepted (upd 2026-06-21) | Related: [0003](adr/0003-customer-simulation.md), [0004](adr/0004-stock-model-hybrid-sale-chance.md), [GameFlowLoop.md](GameFlowLoop.md), [SCENE_ARCHITECTURE.md](improvements/SCENE_ARCHITECTURE.md), [INPROGRESS/LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md), [LOCATIONSCENE_EDITOR_SETUP.md](archive/LOCATIONSCENE_EDITOR_SETUP.md) |
| ✅ [0006](adr/0006-passive-sales-requested-genre.md) | **Passive sales — per-customer requested-genre roll.** Покупатель приходит с профилем 1‑3 жанров; попытка катит **один** жанр из запроса → жанр известен и в успехе, и в провале. Уточняет стадию-1 [0004](adr/0004-stock-model-hybrid-sale-chance.md); seam `IPassivePurchaseResolver`, старая модель сохранена за `LegacyShelfPassiveResolver`. | Accepted | Уточняет [0004](adr/0004-stock-model-hybrid-sale-chance.md). Related: [0003](adr/0003-customer-simulation.md), [CORE_LOOP.md](CORE_LOOP.md) |
| ✅ [0007](adr/0007-quest-system.md) | **Quest system over the Conditions engine.** Квесты как машина состояний поверх `Game.Conditions`; цепочки/задачи, save-модули, permanent effects, prerequisites по `SalesStats`. MVP-ядро реализовано, дальнейшие модули — позже. | Accepted (2026-06-29) | Related: [0001](adr/0001-save-data-modular-payload.md), [0002](adr/0002-config-system-architecture.md), [0004](adr/0004-stock-model-hybrid-sale-chance.md), [0006](adr/0006-passive-sales-requested-genre.md), [QUESTS.md](QUESTS.md), [TODO.md](TODO.md) |
| ✅ [0008](adr/0008-quest-sales-progress-persistence.md) | **Quest sales progress persistence.** Продажные задачи квестов считают прогресс с момента активации задачи; принято compact baseline DTO вместо полного снимка `SalesStatsStateDto`. | Accepted (2026-07-04) | Related: [0001](adr/0001-save-data-modular-payload.md), [0007](adr/0007-quest-system.md), [QUESTS.md](QUESTS.md) |

---

## 🎮 Игровые системы и фичи

| Документ | Что описывает | Связи |
|---|---|---|
| ⏳ [CORE_LOOP.md](CORE_LOOP.md) | Дизайн дневного цикла Morning→Preparation→Sales→Results: фазы, скоринг, награды, принципы. | [ADR-0003](adr/0003-customer-simulation.md), [ADR-0004](adr/0004-stock-model-hybrid-sale-chance.md), [FTUE.md](FTUE.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) |
| ⏳ [GameFlowLoop.md](GameFlowLoop.md) | Карта сцен и переходов: хаб `GameplayScene` + additive `LocationScene`, DI-скопы, `IGameFlowService`. | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md), [CORE_LOOP.md](CORE_LOOP.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) |
| ✅ [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) | Текущее поведение modular save + day flow: модули сейва, defer-commit (`SalesDayCommitService`), entry fee, идемпотентность, последствия выхода из `LocationScene` посреди дня. | [ADR-0001](adr/0001-save-data-modular-payload.md), [ADR-0003](adr/0003-customer-simulation.md), [GameFlowLoop.md](GameFlowLoop.md), [FTUE.md](FTUE.md) |
| ⏳ [FTUE.md](FTUE.md) | Первый запуск: стартовое сидирование (gold/книги), save-ключи `ftue.*`, скриптовый день 1, требования к tutorial-движку. | [CORE_LOOP.md](CORE_LOOP.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) |
| ✅ [QUESTS.md](QUESTS.md) | Спека фичи `Game.Quest`: квесты как машина состояний поверх `Game.Conditions`, цепочки, задачи, save, permanent effects, prerequisites по `SalesStats`, обобщённые примеры цепочек (плейсхолдеры). | [ASMDEF_RULES.md](ASMDEF_RULES.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md), [TODO.md](TODO.md), [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md), [ADR-0007](adr/0007-quest-system.md) |
| ✅ [QUEST_FLOW.md](QUEST_FLOW.md) | **Состояние**, не спека: фактический путь всех 4 квестов (Эдди / Милли / Тара / Капитан) от спавна персонажа до разблокировки локации, цепочка локаций и реестр проблем P1–P9 с приоритетом. Проходим 1 из 4. | [QUESTS.md](QUESTS.md), [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md), [LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md), [TODO.md](TODO.md) |
| ✅ [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md) | Активная продажа: булевы условия (`all/any/none`, `type/operator/value`) вместо взвешенного скоринга; контракт жанра продажи для progress counting. **Указатель: оформить ADR-0009.** | [ADR-0003](adr/0003-customer-simulation.md), [ADR-0006](adr/0006-passive-sales-requested-genre.md), [ADR-0008](adr/0008-quest-sales-progress-persistence.md) |
| ✅ [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) | Архитектура фичи `Game.Characters`: персонаж как индекс над story-progression (действия живут в `Game.Quest`), discovered-состояние, memories, read-side для Journal. | [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md), [QUESTS.md](QUESTS.md) |
| ✅ [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) | Точка связи персонажей (`Game.Characters`) и квестов: discovery/memory, авторинг character-цепочек. `Characters` реализован (Этап 1–3); связь через пассивный `CharacterId` + обратный индекс. | [QUESTS.md](QUESTS.md), [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md), [CORE_LOOP.md](CORE_LOOP.md) |
| ⏳ [SHOP.md](SHOP.md) | _на будущее_ | — |
| ⏳ [INVENTORY.md](INVENTORY.md) | _на будущее_ (фича `Game.Inventory` — категории, two-bucket storage, use-handlers) | — |
| ✅ [LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md) | Система открытия локаций: save-модуль `location_unlock`, условия доступа и связь с дневным циклом. | [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md), [CORE_LOOP.md](CORE_LOOP.md) |
| ✅ [WORLD_HUD.md](WORLD_HUD.md) | World-space UI над объектами сцены: баблы/подсказки/индикаторы, Phase 0 editor setup и reference-система Bubbles. | [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [GameFlowLoop.md](GameFlowLoop.md) |
| ✅ [DECOR.md](DECOR/DECOR.md) | Дизайн `Game.Decor`: декор как progression hook магазина, модификаторы шансов продаж, коллекция и слоты, будущие поведенческие/атмосферные эффекты. | [ADR-0004](adr/0004-stock-model-hybrid-sale-chance.md), [INVENTORY.md](INVENTORY.md) |
| ✅ [DECOR_PLACEMENT_MVP.md](DECOR/DECOR_PLACEMENT_MVP.md) | MVP расстановки декора: обзор UX, статус реализации, save-hook, preview-контроллер, replace/apply/remove и editor-остатки. | [DECOR.md](DECOR/DECOR.md), [DECOR_PLACEMENT_CASES.md](DECOR/DECOR_PLACEMENT_CASES.md) |
| ✅ [DECOR_PLACEMENT_CASES.md](DECOR/DECOR_PLACEMENT_CASES.md) | Case-by-case reference для `DecorPlacementWindow`: клики по точкам/предметам, preview, replace, cancel/apply, availability visual. | [DECOR_PLACEMENT_MVP.md](DECOR/DECOR_PLACEMENT_MVP.md) |
| ⏳ [REWARD_SYSTEM.md](REWARD_SYSTEM.md) | _на будущее_ | — |
| ✅ [CONTENT_STRATEGY_MVP.md](CONTENT_STRATEGY_MVP.md) | Контент-план MVP: day 1 / day 1–7 / day 8–30, 5 персонажей × 5 локаций (плейсхолдеры), маппинг контента на существующие системы, фича «5 фрагментов фото» + альтернативный вариант (независимые Memories на персонажа). | [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md), [LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md), [CORE_LOOP.md](CORE_LOOP.md), [INVENTORY.md](INVENTORY.md), [REWARD_SYSTEM.md](REWARD_SYSTEM.md), [DECOR.md](DECOR/DECOR.md) |

---

## 🚧 In-progress / спеки (`docs/INPROGRESS/`)

| Документ | Связи |
|---|---|
| ⏳ [LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md) | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md) |
| ⏳ [CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md) | [ADR-0003](adr/0003-customer-simulation.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) |
| ⏳ [CUSTOMER_TRAFFIC_COUNT_SYSTEM.md](INPROGRESS/CUSTOMER_TRAFFIC_COUNT_SYSTEM.md) | Количество обычных покупателей в день: day overrides, финальные scripted overrides без модификаторов, contributor pipeline для декора/локации/погоды/ивентов. [ADR-0003](adr/0003-customer-simulation.md), [CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md) |
| ⏳ [JOURNAL_WINDOW.md](INPROGRESS/JOURNAL_WINDOW.md) | Historical Journal 5-tab window spec; current status, memory models, `hiddenInJournal`, `unlockedAtStart`, and unseen tracking. [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md), [FTUE.md](FTUE.md), [LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md) |
| ✅ [TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md) | Архитектура туториала: гибрид tutorial-квестов (`IQuestsService`) + тонкий forced-step-движок (`Game.Tutorial` + C# content + примитивы в `Infrastructure/TutorialUI`); таргет-реестр, гейтинг, roadmap из 7 этапов. [FTUE.md](FTUE.md), [QUESTS.md](QUESTS.md), [ADR-0007](adr/0007-quest-system.md), [ASMDEF_RULES.md](ASMDEF_RULES.md) |
| ✅ [AB_TESTING_READINESS.md](INPROGRESS/AB_TESTING_READINESS.md) | Аудит готовности к A/B: что даёт RC-override, почему `SalesTuning`/`SalesTrafficSettings` (ScriptableObject) вне контура конфигов, отсутствие variant/exposure в аналитике, схема «сервер = контроль, Firebase = назначение», план по ROI. [ADR-0002](adr/0002-config-system-architecture.md), [CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md), [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) |
| ⏳ [ART_BRIEF_PROMENADE_V1.md](INPROGRESS/ART_BRIEF_PROMENADE_V1.md) | — |

---

## 🛠 Improvements (`docs/improvements/`)

| Документ | Связи |
|---|---|
| ⏳ [UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md) | [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) |
| ⏳ [DI_IMPROVEMENTS.md](improvements/DI_IMPROVEMENTS.md) | — |
| ⏳ [LOGGING_SYSTEM_REVIEW.md](improvements/LOGGING_SYSTEM_REVIEW.md) | [LOGGING_SYSTEM.md](SERVICES/LOGGING_SYSTEM.md) |
| ⏳ [LOGGING_ASMDEF_IMPROVEMENT.md](improvements/LOGGING_ASMDEF_IMPROVEMENT.md) | [LOGGING_SYSTEM.md](SERVICES/LOGGING_SYSTEM.md), [ASMDEF_RULES.md](ASMDEF_RULES.md) |
| ⏳ [RESOURCE_IDS_OPEN_CLOSED.md](improvements/RESOURCE_IDS_OPEN_CLOSED.md) | — |

---

## 📋 Процесс / трекинг

| Документ | Связи |
|---|---|
| ⏳ [TODO.md](TODO.md) | Рабочий список задач (Геймплей/Инфраструктура/Визуал). Источник истины по задачам — Notion. |
| ✅ [BUILD.md](BUILD.md) | Подготовка к player-сборке (APK). Автоматический гейт `PreBuildValidationGate` (валит билд при рассинхроне StreamingAssets/manifest и при нерешаемых активных запросах) + ручной чеклист: Addressables, Firebase, Android Player Settings, флаги `BootstrapInstaller.asset`. | [SERVICES/CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md), [SERVICES/ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md) |

---

## 🗄 Архив (`docs/archive/`) — историческое, не поддерживается

Сохранено как контекст принятых решений; актуальность не гарантируется (см. [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md) — RU допустим).

- ⏳ [SCENE_TRANSITION_BOOTSTRAP_TO_GAMEPLAY.md](archive/SCENE_TRANSITION_BOOTSTRAP_TO_GAMEPLAY.md)
- ⏳ [sources.md](archive/sources.md)

---

## Карта пересечений (кратко)

- **Фундамент:** [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md) 🧱 — ни от чего не зависит; на неё опираются [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md), [AUDIO_SYSTEM.md](SERVICES/AUDIO_SYSTEM.md) и sprite-сервисы.
- **UI:** [UI_SYSTEM.md](SERVICES/UI_SYSTEM.md) → [ADDRESSABLES.md](SERVICES/ADDRESSABLES.md); вокруг неё — future-phases и in-progress спеки фабрики окон/переходов.
- **Дневной цикл:** дизайн [CORE_LOOP.md](CORE_LOOP.md) ↔ сцены [GameFlowLoop.md](GameFlowLoop.md) ↔ персист [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) ↔ первый запуск [FTUE.md](FTUE.md) — четыре слоя одного цикла, каждый владеет своим срезом.
- **Sales-цепочка ADR:** [0003](adr/0003-customer-simulation.md) → уточняется [0004](adr/0004-stock-model-hybrid-sale-chance.md) → пассив-стадия-1 уточнена [0006](adr/0006-passive-sales-requested-genre.md) → визуальная часть [0005](adr/0005-customer-visuals-in-location-scene.md); все опираются на дизайн [CORE_LOOP.md](CORE_LOOP.md).
- **Квесты/персонажи:** [ADR-0007](adr/0007-quest-system.md) ↔ [QUESTS.md](QUESTS.md) ↔ [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) ↔ [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md).
- **Config-кластер:** [ADR-0002](adr/0002-config-system-architecture.md) ↔ `SERVICES/CONFIG_*` документы.
- **Правила формата** ([ASMDEF_RULES.md](ASMDEF_RULES.md), [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md)) 📐 действуют поперёк всего и не описывают функционал.

---

## Как поддерживать каталог

1. Новый `.md` → добавь строку в нужную секцию (тип по легенде, статус `⏳`/`✅`). Сервисные доки кладём в `docs/SERVICES/`.
2. Описание заполняй, когда документ стабилизировался.
3. В колонке «Связи» указывай встречные ссылки — каталог должен отражать, **как документы пересекаются**.
4. Для 🧱-инфраструктуры держи колонку «Зависит от» пустой (`—`): если там что-то появилось — это сигнал нарушения слоёв ([ASMDEF_RULES.md](ASMDEF_RULES.md)).
