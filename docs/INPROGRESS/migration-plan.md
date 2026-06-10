# План миграции: Loading & Bootstrap

Трекер переноса кода загрузки в MyBookstore из двух источников. Источники описаны в [sources.md](sources.md).

**Стратегия:** Research даёт недостающие куски того же стиля (VContainer + IAsyncStartable); Pet#2 даёт оркестрацию и UX-слой сверху. Сначала закрываем «белые пятна» из Research, потом надстраиваем паттерны из Pet#2.

**Ветка:** `feature/loading-migration` от `RemoteConfig`. Каждая фаза = отдельный коммит, чтобы откатывать точечно.

---

## Phase 0 — Подготовка

- [x] Сделать diff Research vs MyBookstore → [pet-gap.md](pet-gap.md)
- [x] Работаем в текущей ветке `RemoteConfig`, без отдельной `feature/loading-migration`
- [x] Research-source — по последнему состоянию (без пина на коммит)

**Корректировка после анализа:** Research уже содержит полноценный Phase/Group/Operation-орхестратор. Это смещает работу: бóльшая часть «оркестрации», которая раньше планировалась в Phase 2 (источник Pet#2), теперь делается в Phase 1 (источник Research). См. [pet-gap.md](pet-gap.md) раздел 1.

---

## Phase 1 — Перенос из Research

Закрываем TODO-стабы из [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) и одновременно подменяем три текущих `IAsyncStartable` на единый Orchestrator-driven флоу.

Детальная таблица файлов и шагов: см. [pet-gap.md](pet-gap.md). Чек-листом ниже трекаем по этапам.

| # | Этап | Закрывает TODO | Статус |
|---|---|---|---|
| 1 | Скопировать ядро `Bootstrap/Loading/` (11 файлов) | `GameLoadingVContainerBindings` (Orchestrator/Aggregator) | [x] |
| 2 | Адаптировать `AddressablesUpdateOperation` под `IAddressablesCatalogService` | — | [x] |
| 3 | RemoteConfig: вместо двух Research-операций — единая `RemoteConfigInitOperation` через `IRemoteConfigService.InitializeAsync()` | — | [x] |
| 4 | Адаптировать `SaveDataLoadOperation` под `ISaveService.LoadAsync()` | — | [x] |
| 5 | `WarmupOperation` — скопировано как есть | — | [x] |
| 6 | `UiManagerConfigureOperation`, `AuthorizationGateOperation`, `SceneTransitionOperation` — N/A, отложено в Phase 2 | — | [x] |
| 7 | Добавлен `ConfigsWarmupOperation` (`IConfigsService.WarmupAsync()`) — закрывает функцию старого `ConfigsWarmupEntryPoint` | — | [x] |
| 8 | `LoadingScreenView.cs` — скопирован в упрощённой форме (без UIShared deps). Префаб — на пользователе. Регистрация в DI отложена | `UiSystemVContainerBindings` (loading screen — частично) | [x] |
| 9 | `LoadingOrchestratorEntryPoint` (заменяет Addressables/Configs warmup + BookDuneProbe) | `RegisterGameLoading()` стаб | [x] |
| 10 | Зарегистрировать `LoadingOrchestrator`, `LoadingProgressAggregator`, entry point в `GameLoadingVContainerBindings`. Снять регистрации старых entry points из `ConfigsVContainerBindings`, `InfrastructureVContainerBindings` | `GameLoadingVContainerBindings` стабы | [x] |
| 11 | Удалить `BookDuneProbeEntryPoint.cs` (+ .meta) — диагностика больше не нужна | — | [x] |
| 12 | Удалены `AddressablesWarmupEntryPoint.cs` и `ConfigsWarmupEntryPoint.cs` (+ .meta) после успешного smoke-теста | — | [x] |
| 13 | Создать Editor-тесты `Game.Bootstrap.Loading.Tests.Editor.asmdef` + `LoadingOrchestratorTests.cs` | — | [x] |
| 14 | Обновить `Game.Bootstrap.asmdef`: добавить ref `Game.Bootstrap.Loading` | — | [x] |
| 15 | Smoke-тест в Editor: запуск → лог `[LoadingOrchestrator] Loading complete.` → приложение продолжает работу. Пройден. По пути починена латентная регистрация `LocalDiskStorage` (factory-делегат вместо ctor-резолва) | — | [x] |
| 16 | После smoke-теста: подключить `LoadingScreenView` в DI и в `LoadingOrchestratorEntryPoint` (вместо `Debug.Log`) | — | [ ] |

**Не делаем в Phase 1** (по твоему решению):
- `UIManager`, `IWindowFactory`, `IWindowRouter` — отложено в следующие итерации
- `IAnalyticsService` — отложено
- `GameInstaller` фичи (Inventory/Shop/Quest и т.д.) — по мере того, как фичи будут писаться

---

## Phase 2 — Перенос из Pet#2

После Phase 1 оркестрация уже на месте. От Pet#2 в Phase 2 остаются «добивающие» элементы:

| # | Шаг | Что переносим | Риск | Статус |
|---|---|---|---|---|
| 1 | `GameStateService` | enum состояний игры + сервис + сигналы | Низкий | [ ] N/A — отложено |
| 2 | Debug-start флаги | `_useDebugFeatures` + `_skipFullLoading` на `BootstrapInstaller` SO (Editor-only). Статика `DebugStartFlags`. Чекается в `LoadingOrchestratorEntryPoint.BuildPhases()`. Auto-detect по сцене (Pet#2-style) не делали — у MyBookstore нет `LocationView` | Низкий | [x] |
| 3 | `LinearLoadingTime` (или эквивалент) | Если в Pet#2 есть структурированные метрики времени фаз помимо логов | Низкий | [ ] N/A — отложено |
| 4 | `LoadLocationCommand` + `GameSceneController` | Когда появится понятие «локация» и переходы между сценами | Высокий, отложить | [ ] |
| 5 | UIManager / WindowFactory / WindowRouter / TransitionAnimationService | Полноценная UI-подсистема | Высокий | [ ] |
| 6 | `IAnalyticsService` | Аналитика | Средний | [ ] |

Шаги 5-6 — отдельные крупные подпроекты, не часть текущей миграции загрузки. Заведём отдельные планы перед стартом.

---

## Технические правила переноса (для обеих фаз)

1. Не тащить `using`-и слепо. Каждую нерезолвящуюся ссылку — либо переписать на своё, либо вырезать с пометкой `// TODO: original from <source> used X`.
2. Никаких `_Lite` вариантов. Один путь.
3. `sealed`/`internal` → `public` только если действительно нужно из другой сборки.
4. Asmdef-границы: команды загрузки положить в новую сборку `Game.Core.Loading` (или в существующую `Core.Installers`), не размазывать по фичам.
5. Имена классов из источника не переименовывать.
6. Тесты в рамках этой миграции не пишем (кроме smoke-теста в Editor). Архитектура ещё подвижна.

---

## Definition of Done (текущая итерация = Phase 1)

Считаем Phase 1 закрытой, когда:
- Все 11 строк в таблице Phase 1 либо `[x]`, либо отмечены «N/A — отложено».
- Editor-запуск проходит через новый `BootstrapEntryPoint`: видим прогресс-бар, операции выполняются по фазам, на ошибке RemoteConfig/Save показывается экран с кнопкой retry.
- `LoadingOrchestratorTests.cs` зелёные.
- В [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) обновлён раздел «Что ещё не реализовано»: убраны закрытые пункты, добавлены отложенные на Phase 2.
