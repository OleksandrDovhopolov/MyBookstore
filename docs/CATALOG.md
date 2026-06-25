# 📚 Docs Catalog — точка входа

Карта всех `.md`-документов проекта: что за что отвечает, как документы пересекаются и в какую
сторону идут зависимости. **Начинай отсюда.** При добавлении нового `.md` — заведи строку в этом
каталоге.

> Статус заполнения: сейчас детально описаны только **правила формата**, **ADDRESSABLES**,
> **UI_SYSTEM** и **все ADR**. Остальные строки — заглушки (`⏳`), их описания дополняются по мере
> работы.

---

## 🗂 Навигация / meta

| Документ | Что описывает | Связи |
|---|---|---|
| ✅ [CATALOG.md](CATALOG.md) | Главная карта `docs/`: какие документы есть в проекте, как они сгруппированы и где отмечены связи между ними. | Ссылается на все актуальные разделы каталога; не включает `docs/archive/` в рабочий контур. |

---

## Легенда

**Тип документа:**

- 📐 **Правило / конвенция формата** — как писать код/доки. Не описывает функционал.
- 🧱 **Инфраструктура** — фундамент. *Только от неё зависят*; сама не зависит от фич/UI/систем выше.
- 🖼 **UI-система** — фреймворк окон.
- 🎮 **Игровая система / фича** — геймплейный функционал.
- 🧭 **ADR** — зафиксированное архитектурное решение (может ссылаться и быть указанным).
- 🚧 **In-progress / спека** — в работе (`docs/INPROGRESS/`).
- 🛠 **Improvement** — заметка по улучшению существующей системы (`docs/improvements/`).
- 📋 **Процесс / трекинг** — TODO, PROGRESS.
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

## 🧱 Инфраструктурные системы (фундамент — *только от них зависят*)

| Документ | Что описывает | Зависит от | На неё ссылаются / пересечения |
|---|---|---|---|
| ✅ [ADDRESSABLES.md](ADDRESSABLES.md) | Единая обёртка над Unity Addressables — `ProdAddressablesWrapper` (namespace `Infrastructure`): ref-counted кэш хэндлов, sync/async загрузка, группы и warmup по лейблам, релиз по объекту/адресу/группе. Прямых вызовов `Addressables.*` в проде нет. | — (ничего; чистая инфраструктура) | [UI_SYSTEM.md](UI_SYSTEM.md) (загрузка префабов окон), [INPROGRESS/NEWSPAPER_REWARDS_SPRITE_SERVICE.md](INPROGRESS/NEWSPAPER_REWARDS_SPRITE_SERVICE.md) (sprite provider), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) |
| ⏳ [LOGGING_SYSTEM.md](LOGGING_SYSTEM.md) | _на будущее_ | — | [improvements/LOGGING_SYSTEM_REVIEW.md](improvements/LOGGING_SYSTEM_REVIEW.md), [improvements/LOGGING_ASMDEF_IMPROVEMENT.md](improvements/LOGGING_ASMDEF_IMPROVEMENT.md) |
| ⏳ [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) | _на будущее_ | — | — |
| ⏳ [CONFIG_CACHE_SYSTEM.md](CONFIG_CACHE_SYSTEM.md) | _на будущее_ | — | [ADR-0002](adr/0002-config-system-architecture.md) |
| ⏳ [CONFIG_SERVER_API.md](CONFIG_SERVER_API.md) | _на будущее_ | — | [ADR-0002](adr/0002-config-system-architecture.md) |
| ⏳ [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](CONFIG_EDITOR_WINDOW_MVP_SPEC.md) | _на будущее_ | — | [ADR-0002](adr/0002-config-system-architecture.md) |
| ⏳ [API_ENDPOINTS.md](API_ENDPOINTS.md) | _на будущее_ | — | — |
| ⏳ [FIREBASE_INTEGRATION.md](FIREBASE_INTEGRATION.md) | _на будущее_ | — | — |
| ⏳ [SECRETS.md](SECRETS.md) | _на будущее_ | — | — |
| ⏳ [README_Commands.md](README_Commands.md) | _на будущее_ (система команд) | — | — |

---

## 🖼 UI-система

| Документ | Что описывает | Зависит от | Связи / пересечения |
|---|---|---|---|
| ✅ [UI_SYSTEM.md](UI_SYSTEM.md) | Фреймворк окон (`Game.UI`, `Assets/Game/Core/UI/`): `IUIManager.ShowAsync/Hide`, `WindowController<TView>`/`WindowView`, слои (`WindowType` × `WindowLayer`), сортировка, кэш `UIStorage`, стек, анимации, blocker, фильтр показа. | → [ADDRESSABLES.md](ADDRESSABLES.md) (загрузка префабов окон через `ProdAddressablesWrapper`) | [improvements/UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md) (роадмап), [INPROGRESS/DI_ARCHITECTURE_WINDOWFACTORY.md](INPROGRESS/DI_ARCHITECTURE_WINDOWFACTORY.md), [INPROGRESS/TRANSITION_ANIMATION_SERVICE.md](INPROGRESS/TRANSITION_ANIMATION_SERVICE.md). Потребители — все фичи с окнами. |

---

## 🧭 ADR — архитектурные решения (`docs/adr/`)

| ADR | Решение | Status | Пересечения (Related / Supersedes) |
|---|---|---|---|
| ✅ [0001](adr/0001-save-data-modular-payload.md) | **Save data — modular opaque payload.** `SaveData` = `Dictionary<string, ModulePayload>`; каждая фича владеет своей моделью и версией схемы, ядро Save не знает о данных фич. | Accepted | Указывается из [0003](adr/0003-customer-simulation.md); история — [archive/SAVE_SYSTEM.md](archive/SAVE_SYSTEM.md), [archive/SAVE_PATTERNS_FROM_PROD.md](archive/SAVE_PATTERNS_FROM_PROD.md), [archive/RESEARCH_SAVE_SYSTEM.md](archive/RESEARCH_SAVE_SYSTEM.md), [archive/SAVE_MIGRATION_FROM_RESEARCH.md](archive/SAVE_MIGRATION_FROM_RESEARCH.md) |
| ✅ [0002](adr/0002-config-system-architecture.md) | **Config system — клиентская архитектура.** Data-driven конфиги (`BookConfig`/`LocationConfig`/…), редактируемые без ребилда, версионирование, A/B. | Accepted | [CONFIG_CACHE_SYSTEM.md](CONFIG_CACHE_SYSTEM.md), [CONFIG_SERVER_API.md](CONFIG_SERVER_API.md), [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](CONFIG_EDITOR_WINDOW_MVP_SPEC.md) + серверный ADR |
| ✅ [0003](adr/0003-customer-simulation.md) | **Customer simulation — фаза «Продажа» как real-time симуляция** конкурентных агентов-покупателей (приход → бродят → пассивные/активные покупки). | Accepted | Supersedes пошаговый Sales MVP. Related: [0001](adr/0001-save-data-modular-payload.md), [CORE_LOOP.md](CORE_LOOP.md), [INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md). Уточняется [0004](adr/0004-stock-model-hybrid-sale-chance.md) |
| ✅ [0004](adr/0004-stock-model-hybrid-sale-chance.md) | **Stock model (hybrid) + passive sale chance.** Сток книг = per-title + агрегат по жанрам; пассивная продажа — вероятностный `baseSaleChance`, падающий по мере распродажи. | Accepted | Уточняет пассивную часть [0003](adr/0003-customer-simulation.md). Стадия-1 уточнена [0006](adr/0006-passive-sales-requested-genre.md). Related: [CORE_LOOP.md](CORE_LOOP.md), [FTUE.md](FTUE.md) |
| ✅ [0005](adr/0005-customer-visuals-in-location-scene.md) | **Customer visuals в world-space `LocationScene`.** Визуалы покупателей живут в отдельной additive-сцене поверх хаба; якоря/спавнер регистрируются в `LocationInstaller`. | Accepted (upd 2026-06-21) | Related: [0003](adr/0003-customer-simulation.md), [0004](adr/0004-stock-model-hybrid-sale-chance.md), [GameFlowLoop.md](GameFlowLoop.md), [INPROGRESS/SCENE_ARCHITECTURE.md](INPROGRESS/SCENE_ARCHITECTURE.md), [INPROGRESS/LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md), [INPROGRESS/LOCATIONSCENE_EDITOR_SETUP.md](INPROGRESS/LOCATIONSCENE_EDITOR_SETUP.md) |
| ✅ [0006](adr/0006-passive-sales-requested-genre.md) | **Passive sales — per-customer requested-genre roll.** Покупатель приходит с профилем 1‑3 жанров; попытка катит **один** жанр из запроса → жанр известен и в успехе, и в провале. Уточняет стадию-1 [0004](adr/0004-stock-model-hybrid-sale-chance.md); seam `IPassivePurchaseResolver`, старая модель сохранена за `LegacyShelfPassiveResolver`. | Accepted | Уточняет [0004](adr/0004-stock-model-hybrid-sale-chance.md). Related: [0003](adr/0003-customer-simulation.md), [CORE_LOOP.md](CORE_LOOP.md) |

---

## 🎮 Игровые системы и фичи

| Документ | Что описывает | Связи |
|---|---|---|
| ⏳ [CORE_LOOP.md](CORE_LOOP.md) | _на будущее_ (дизайн дневного цикла Morning→Preparation→Sales→Results) | [ADR-0003](adr/0003-customer-simulation.md), [ADR-0004](adr/0004-stock-model-hybrid-sale-chance.md), [FTUE.md](FTUE.md) |
| ⏳ [GameFlowLoop.md](GameFlowLoop.md) | _на будущее_ | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md) |
| ⏳ [FTUE.md](FTUE.md) | _на будущее_ | [CORE_LOOP.md](CORE_LOOP.md) |
| ⏳ [SHOP.md](SHOP.md) | _на будущее_ | — |
| ⏳ [INVENTORY.md](INVENTORY.md) | _на будущее_ | — |
| ⏳ [DECOR.md](DECOR.md) | _на будущее_ | — |
| ⏳ [REWARD_SYSTEM.md](REWARD_SYSTEM.md) | _на будущее_ | — |

---

## 🚧 In-progress / спеки (`docs/INPROGRESS/`)

| Документ | Связи |
|---|---|
| ⏳ [SCENE_ARCHITECTURE.md](INPROGRESS/SCENE_ARCHITECTURE.md) | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md) |
| ⏳ [LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md) | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md) |
| ⏳ [LOCATIONSCENE_EDITOR_SETUP.md](INPROGRESS/LOCATIONSCENE_EDITOR_SETUP.md) | [ADR-0005](adr/0005-customer-visuals-in-location-scene.md) |
| ⏳ [CUSTOMER_STEP_PIPELINE_REFACTOR.md](INPROGRESS/CUSTOMER_STEP_PIPELINE_REFACTOR.md) | [ADR-0003](adr/0003-customer-simulation.md) |
| ⏳ [NEWSPAPER_REWARDS_SPRITE_SERVICE.md](INPROGRESS/NEWSPAPER_REWARDS_SPRITE_SERVICE.md) | [ADDRESSABLES.md](ADDRESSABLES.md) |
| ⏳ [DI_ARCHITECTURE_WINDOWFACTORY.md](INPROGRESS/DI_ARCHITECTURE_WINDOWFACTORY.md) | [UI_SYSTEM.md](UI_SYSTEM.md) |
| ⏳ [TRANSITION_ANIMATION_SERVICE.md](INPROGRESS/TRANSITION_ANIMATION_SERVICE.md) | [UI_SYSTEM.md](UI_SYSTEM.md) |
| ⏳ [world-hud-bubbles-system.md](INPROGRESS/world-hud-bubbles-system.md) | — |
| ⏳ [WorldHud-Phase-0-Editor-Setup.md](INPROGRESS/WorldHud-Phase-0-Editor-Setup.md) | — |
| ⏳ [ART_BRIEF_PROMENADE_V1.md](INPROGRESS/ART_BRIEF_PROMENADE_V1.md) | — |

---

## 🛠 Improvements (`docs/improvements/`)

| Документ | Связи |
|---|---|
| ⏳ [UI_SYSTEM_FUTURE_PHASES.md](improvements/UI_SYSTEM_FUTURE_PHASES.md) | [UI_SYSTEM.md](UI_SYSTEM.md) |
| ⏳ [DI_IMPROVEMENTS.md](improvements/DI_IMPROVEMENTS.md) | — |
| ⏳ [LOGGING_SYSTEM_REVIEW.md](improvements/LOGGING_SYSTEM_REVIEW.md) | [LOGGING_SYSTEM.md](LOGGING_SYSTEM.md) |
| ⏳ [LOGGING_ASMDEF_IMPROVEMENT.md](improvements/LOGGING_ASMDEF_IMPROVEMENT.md) | [LOGGING_SYSTEM.md](LOGGING_SYSTEM.md), [ASMDEF_RULES.md](ASMDEF_RULES.md) |
| ⏳ [RESOURCE_IDS_OPEN_CLOSED.md](improvements/RESOURCE_IDS_OPEN_CLOSED.md) | — |

---

## 📋 Процесс / трекинг

| Документ | Связи |
|---|---|
| ⏳ [TODO.md](TODO.md) | — |
| ⏳ [PROGRESS.md](PROGRESS.md) | — |
| ⏳ [TODO_SAVE_HOOK_BOOTSTRAP.md](TODO_SAVE_HOOK_BOOTSTRAP.md) | [ADR-0001](adr/0001-save-data-modular-payload.md) |

---

## 🗄 Архив (`docs/archive/`) — историческое, не поддерживается

Сохранено как контекст принятых решений; актуальность не гарантируется (см. [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md) — RU допустим).

- ⏳ [SAVE_SYSTEM.md](archive/SAVE_SYSTEM.md), [RESEARCH_SAVE_SYSTEM.md](archive/RESEARCH_SAVE_SYSTEM.md), [SAVE_MIGRATION_FROM_RESEARCH.md](archive/SAVE_MIGRATION_FROM_RESEARCH.md), [SAVE_PATTERNS_FROM_PROD.md](archive/SAVE_PATTERNS_FROM_PROD.md) → история [ADR-0001](adr/0001-save-data-modular-payload.md)
- ⏳ [INVENTORY_SYSTEM.md](archive/INVENTORY_SYSTEM.md), [inventory.md](archive/inventory.md)
- ⏳ [SCENE_TRANSITION_BOOTSTRAP_TO_GAMEPLAY.md](archive/SCENE_TRANSITION_BOOTSTRAP_TO_GAMEPLAY.md), [heroes-loading-scene-transitions.md](archive/heroes-loading-scene-transitions.md)
- ⏳ [heroes-shop-architecture.md](archive/heroes-shop-architecture.md), [REFERENCE_COZY_BOOKSHOP_DAY.md](archive/REFERENCE_COZY_BOOKSHOP_DAY.md)
- ⏳ [migration-plan.md](archive/migration-plan.md), [pet-gap.md](archive/pet-gap.md), [sources.md](archive/sources.md)

---

## Карта пересечений (кратко)

- **Фундамент:** [ADDRESSABLES.md](ADDRESSABLES.md) 🧱 — ни от чего не зависит; на неё опираются [UI_SYSTEM.md](UI_SYSTEM.md) и sprite-сервисы.
- **UI:** [UI_SYSTEM.md](UI_SYSTEM.md) → [ADDRESSABLES.md](ADDRESSABLES.md); вокруг неё — future-phases и in-progress спеки фабрики окон/переходов.
- **Sales-цепочка ADR:** [0003](adr/0003-customer-simulation.md) → уточняется [0004](adr/0004-stock-model-hybrid-sale-chance.md) → пассив-стадия-1 уточнена [0006](adr/0006-passive-sales-requested-genre.md) → визуальная часть [0005](adr/0005-customer-visuals-in-location-scene.md); все опираются на дизайн [CORE_LOOP.md](CORE_LOOP.md).
- **Config-кластер:** [ADR-0002](adr/0002-config-system-architecture.md) ↔ `CONFIG_*` документы.
- **Save-кластер:** [ADR-0001](adr/0001-save-data-modular-payload.md) ↔ архивные SAVE_*-доки.
- **Правила формата** ([ASMDEF_RULES.md](ASMDEF_RULES.md), [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md)) 📐 действуют поперёк всего и не описывают функционал.

---

## Как поддерживать каталог

1. Новый `.md` → добавь строку в нужную секцию (тип по легенде, статус `⏳`/`✅`).
2. Описание заполняй, когда документ стабилизировался.
3. В колонке «Связи» указывай встречные ссылки — каталог должен отражать, **как документы пересекаются**.
4. Для 🧱-инфраструктуры держи колонку «Зависит от» пустой (`—`): если там что-то появилось — это сигнал нарушения слоёв ([ASMDEF_RULES.md](ASMDEF_RULES.md)).
