# Bootstrap & Game Loading — Архитектура

## Содержание

1. [Обзор](#1-обзор)
2. [DI-скопы: GlobalLifetimeScope и GameplayLifetimeScope](#2-di-скопы)
3. [BaseLifetimeScope — механика сборки контейнера](#3-baselifetimescope)
4. [BootstrapInstaller — что регистрируется глобально](#4-bootstrapinstaller)
5. [GameInstaller — что регистрируется посцены](#5-gameinstaller)
6. [Entry Points — порядок запуска на старте](#6-entry-points)
7. [Подсистема Configs](#7-подсистема-configs)
8. [Подсистема Save](#8-подсистема-save)
9. [Подсистема Infrastructure](#9-подсистема-infrastructure)
10. [Переходы между сценами — текущий статус](#10-переходы-между-сценами)
11. [Диаграмма зависимостей](#11-диаграмма-зависимостей)
12. [Что ещё не реализовано (TODO)](#12-что-ещё-не-реализовано)
13. [Приложение: Reference из Research (для отложенных пунктов)](#13-приложение-reference-из-research-для-отложенных-пунктов)
14. [Архитектурные альтернативы: почему `ILoadingOperation`, а не `ICommand`](#14-архитектурные-альтернативы-почему-iloadingoperation-а-не-icommand)

---

## 1. Обзор

DI-фреймворк — **VContainer**. Приложение использует двухуровневую иерархию скопов:

| Скоп | Класс | Время жизни |
|---|---|---|
| Global | `GlobalLifetimeScope` | Всё время работы приложения (DontDestroyOnLoad) |
| Gameplay | `GameplayLifetimeScope` | Время жизни игровой сцены |

`GameplayLifetimeScope` является дочерним к `GlobalLifetimeScope` и может резолвить его зависимости.

---

## 2. DI-скопы

### GlobalLifetimeScope
`Assets/Game/Core/Installers/Scopes/GlobalLifetimeScope.cs`

- Наследует `BaseLifetimeScope`
- `InstallBindings()` — пуст (вся логика делегируется через `_scriptableObjectInstallers`)
- Назначаемый инсталлер: **`BootstrapInstaller`** (ScriptableObject, через инспектор)

### GameplayLifetimeScope
`Assets/Game/Core/Installers/Scopes/GameplayLifetimeScope.cs`

- Наследует `BaseLifetimeScope`
- `InstallBindings()` — пуст (логика через `_monoInstallers`)
- Назначаемый инсталлер: **`GameInstaller`** (MonoBehaviour, через инспектор)

---

## 3. BaseLifetimeScope

`Assets/Game/Core/Installers/Core/BaseLifetimeScope.cs`

Абстрактный базовый класс для обоих скопов. Переопределяет `Configure()` из VContainer's `LifetimeScope`:

```
Configure(builder)
  ├── InstallBindings(builder)          ← переопределяется в наследниках (сейчас пусто)
  ├── InstallScriptableObjects(builder) ← итерирует _scriptableObjectInstallers
  ├── InstallMonoBehaviours(builder)    ← итерирует _monoInstallers
  └── builder.RegisterBuildCallback(OnBuildCallback)
```

Два типа инсталлеров:
- **`ScriptableObjectInstaller`** — ScriptableObject, назначается в Global-скоп (через `Assets/`)
- **`MonoInstaller`** — MonoBehaviour, назначается в Gameplay-скоп (через сцену)

---

## 4. BootstrapInstaller

`Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.cs`

Тип: `ScriptableObjectInstaller`. Регистрирует глобальные синглтоны, которые **переживают смену сцен**:

```csharp
builder.RegisterGameLoading();   // GameLoadingVContainerBindings   — стабы (TODO)
builder.RegisterAnalytics();     // AnalyticsVContainerBindings      — стабы (TODO)
builder.RegisterSave();          // SaveVContainerBindings           — активно
builder.RegisterInfrastructure();// InfrastructureVContainerBindings — активно
builder.RegisterConfigs();       // ConfigsVContainerBindings        — активно
builder.RegisterUiSystem();      // UiSystemVContainerBindings       — стабы (TODO)
```

---

## 5. GameInstaller

`Assets/Game/Core/Installers/Bootstrap/GameInstaller.cs`

Тип: `MonoInstaller`. Регистрирует **сцено-специфичные** сервисы (могут использовать глобальные):

```csharp
builder.RegisterResources();    // стабы (TODO)
builder.RegisterInventory();    // стабы (TODO)
builder.RegisterShop();         // стабы (TODO)
builder.RegisterRewardDrop();   // стабы (TODO)
builder.RegisterIap();          // стабы (TODO)
builder.RegisterQuest();        // стабы (TODO)
builder.RegisterBookSell();     // стабы (TODO)
```

---

## 6. Entry Points и оркестрация загрузки

Единственный entry point загрузки — `LoadingOrchestratorEntryPoint` (`Assets/Game/Core/Bootstrap/Loading/`). Реализует `IAsyncStartable`. VContainer вызывает его `StartAsync()` после сборки контейнера.

### Архитектура: Phase → Group → Operation

```
LoadingOrchestrator
    ├── Phase: технические init (Sequential)
    │     ├── AddressablesUpdateOperation     ← IAddressablesCatalogService.InitializeAndUpdateAsync
    │     └── RemoteConfigInitOperation       ← IRemoteConfigService.InitializeAsync
    ├── Phase: данные (Parallel)
    │     ├── ConfigsWarmupOperation          ← IConfigsService.WarmupAsync
    │     └── SaveDataLoadOperation           ← ISaveService.LoadAsync
    └── Phase: финализация (Sequential)
          ├── WarmupOperation                 ← prefab/shader prewarm placeholder
          └── SceneTransitionOperation        ← ISceneTransitionService.TransitionToAsync(gameplayScene)
```

### Переход между сценами

`ISceneTransitionService` — generic-обёртка над `SceneManager.LoadSceneAsync` (`Game.Bootstrap.Loading`). Принимает имя сцены + `IProgress<float>` + `CancellationToken`. Применяется как `SceneTransitionOperation` в финальной фазе orchestrator-а, но может вызываться из любого места (gameplay→preparation и т.п.).

Имя стартовой gameplay-сцены конфигурируется на `BootstrapInstaller` SO (поле `_gameplaySceneName`, default `GameplayScene`) и пробрасывается через DTO `LoadingSettings` (Loading-сборка не знает про `Game.Bootstrap` → избегаем циклической asmdef-зависимости).

`GlobalLifetimeScope.Awake` вызывает `DontDestroyOnLoad(gameObject)` **после** `base.Awake()` — иначе `RegisterComponentInHierarchy<LoadingScreenView>()` ищет view в DDOL-сцене и падает с `is not in this scene DontDestroyOnLoad`. После build контейнера scope переезжает в DDOL, контейнер переживает `LoadSceneAsync(Single)`.

`LoadingScreenView` живёт в boot-сцене и **умирает** при переходе. Это намеренно — к финалу `SceneTransitionOperation` лоадер уже не нужен. Обработчики событий orchestrator-а имеют Unity-style `if (_view != null)` чек на случай поздних callback-ов.

Каждая операция имеет:
- `IsCritical` — критическая или нет (false → не валит загрузку, только логируется)
- `RetryPolicy` (попыток + задержка) — оркестратор сам повторяет
- `Timeout` per-operation + глобальный таймаут 60с
- `Weight` — вклад в общий прогресс
- `DisplayPriority` — какое описание показывать при параллельном выполнении

### Debug-флаги (Editor only)

`Game.Bootstrap.Loading.DebugStartFlags` — статические флаги, проставляются из `BootstrapInstaller` SO:

- `UseDebugFeatures` — мастер-свитч
- `SkipFullLoading` — пропускает Addressables update + RemoteConfig init (используются bundled-каталог + base configs)

Сбрасываются на каждом старте Play через `RuntimeInitializeOnLoadMethod`.

### LoadingScreenView

Префаб в boot-сцене, регистрируется через `RegisterComponentInHierarchy<LoadingScreenView>()`. Орхестратор-entry-point ведёт его:
- `SetVisible(true)` / `SetProgress(value)` / `SetStatus(description)` во время загрузки
- `SetError + WaitForRetryClickAsync` при падении критической операции (retry-петля)
- На успехе `SetVisible(false)` НЕ вызывается — view уничтожается вместе с boot-сценой

### Что отложено

- **Retry UI-флоу** — кнопка retry в префабе и привязка `NotifyRetryClicked()` ещё не сделаны. Сейчас при падении пользователь увидит ошибку, но retry-клика дождаться невозможно. Запланировано на отдельную итерацию.
- **«Tap to Start» UX и cover/reveal анимации** — сейчас auto-переход сразу после завершения операций.
- **`MainSceneBootstrap` + `IGameplayReadyGate`** в gameplay-сцене — добавим когда появятся потребители (фичи, которым нужно дождаться полной готовности сцены).

---

## 7. Подсистема Configs

`Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs`

### Архитектура двух слоёв

```
IConfigsService (ConfigsService)
    │
    ├── IConfigSource (base layer)
    │       ├── [PROD]   ServerConfigSource   → HTTP → backend API
    │       │                └── fallback: LocalFolderConfigSource (bundled defaults)
    │       └── [EDITOR] LocalFolderConfigSource (Assets/Configs/*.json)
    │                    или ServerConfigSource (Tools/Configs/Use Server Source)
    │
    └── IConfigOverrideSource (RemoteConfigOverrideSource)
            └── IRemoteConfigService
                    ├── [BOOKSTORE_FIREBASE_RC] FirebaseRemoteConfigService
                    │       └── Firebase.RemoteConfig.FirebaseRemoteConfig
                    └── [default]               NullRemoteConfigService (no-op)
```

### Ключевые классы

| Класс | Описание |
|---|---|
| `ConfigsService` | Основной сервис; `WarmupAsync()` — первый вызов до игровых систем |
| `ConfigsWarmupEntryPoint` | Entry point; активирует RC, затем прогревает кэш |
| `FirebaseRemoteConfigService` | Реальный RC. Требует define `BOOKSTORE_FIREBASE_RC` |
| `NullRemoteConfigService` | No-op stub. Активен по умолчанию |
| `RemoteConfigOverrideSource` | Берёт значения из RC и применяет поверх base конфигов |
| `RemoteConfigLoader` | Вспомогательный класс; проверяет Firebase dependencies |
| `BookDuneProbeEntryPoint` | Smoke-тест. Читает `book_dune`, логирует в консоль |

### Firebase RC — включение
1. Импортировать `FirebaseRemoteConfig_*.unitypackage`
2. Добавить `BOOKSTORE_FIREBASE_RC` в `Project Settings → Player → Scripting Define Symbols`
3. В `ConfigsVContainerBindings` регистрация переключится автоматически (`#if`)

### Editor-инструменты
- `Tools/Configs/Use Server Source` — переключает Editor между локальной папкой и сервером
- `Tools/Configs/Clear Server Snapshot` — удаляет `persistentDataPath/configs/` (etag-snapshot)

---

## 8. Подсистема Save

`Assets/Game/Core/Installers/Features/SaveVContainerBindings.cs`

### Компоненты

| Класс | Интерфейс | Описание |
|---|---|---|
| `PersistentInstallPlayerIdentityProvider` | `IPlayerIdentityProvider` | UUID, сохраняется между запусками |
| `LocalDiskStorage` | `ISaveStorage` | Текущий MVP. Сохраняет JSON на диск |
| `HttpSaveStorage` | `ISaveStorage` | HTTP-режим (закомментирован, для раскомментирования) |
| `SaveService` | `ISaveService` | Основной сервис сохранений |
| `SaveSyncBootstrap` | — | Синхронизация local vs server при старте (только для HTTP-режима) |

### Жизненный цикл SaveService

```
[Первый запрос к данным]
    └── EnsureLoadedAsync() → LoadAsync()
            ├── ISaveStorage.LoadAsync()
            ├── DeserializeOrDefault()  ← fallback на дефолт при corrupted JSON
            ├── _isLoaded = true
            └── foreach hook: AfterLoadAsync()

[Изменение данных]
    └── UpdateModuleAsync(key, value, schemaVersion)
            ├── _isDirty = true
            └── MarkDirty() → DebouncedSaveAsync(600ms)

[Автосохранение]
    └── SaveAsync()
            ├── foreach hook: BeforeSaveAsync()
            ├── [if not dirty] → return
            ├── Bump Revision, обновить Timestamp, вычислить SHA256 hash
            └── ISaveStorage.SaveAsync(json)
```

### Особенности SaveService
- **Debounce** 600ms — несколько вызовов `MarkDirty()` подряд дают один `SaveAsync`
- **Rate limit** 500ms — между сохранениями не менее 500ms (кроме `ForceWithSync`)
- **Semaphore** — потокобезопасность; hooks выполняются до захвата семафора
- **BlockAutosave()** — блокирует авто-сохранение (возвращает `IDisposable`), при release сохраняет если `_hasPendingAutosave`
- **Integrity**: SHA256-хэш от JSON + `HashSalt`, вычисляется и верифицируется при каждом сохранении
- **Payload limit**: warn при > 30 KB total, warn при > 5 KB per module

### SaveSyncBootstrap — стартовая синхронизация (HTTP-режим)
Алгоритм last-write-wins по `MetaData.Revision`:
```
local.Revision > server.Revision → push local → server
server.Revision > local.Revision → overwrite local ← server
equal                            → no-op
server = null (первый запуск)   → push local → server
local = null                     → SaveService.LoadAsync возьмёт с сервера
```

---

## 9. Подсистема Infrastructure

`Assets/Game/Core/Installers/Features/InfrastructureVContainerBindings.cs`

Регистрируется в GlobalLifetimeScope. Используется Configs, Save и другими фичами.

| Класс | Интерфейс | Описание |
|---|---|---|
| `UnityCommandLogger` | `ICommandLogger` | Логирует HTTP-команды в Unity Console |
| `NoOpCommandErrorReporter` | `ICommandErrorReporter` | Stub (нет UI-нотификаций пока) |
| `UnityWebRequestFactory` | `IRequestFactory` | Фабрика UnityWebRequest |
| `ConnectionService` | `IConnectionService` | Обёртка с проверкой доступности сети |
| `AddressablesCatalogService` | `IAddressablesCatalogService` | Инит и обновление Addressables-каталога |

`ProdAddressablesWrapper` — не биндится в DI; используется напрямую (`Load/Release`).

---

## 10. Переходы между сценами

### Текущий статус

- **`LoadingOrchestrator`, `LoadingProgressAggregator`, `LoadingScreenView`** — реализованы. См. раздел 6.
- **`ISceneTransitionService` + переход boot→game** — реализованы. Generic API через `SceneManager.LoadSceneAsync`. Подробнее в разделе 6 «Переход между сценами».
- **«Tap to Start» UX и cover/reveal анимации** — НЕ реализованы. Сейчас auto-переход сразу после завершения операций.
- **`GameplayLifetimeScope`** — каркас существует, но gameplay-сцена пока пустая (Lit 2D URP template). Скоуп станет нужен когда появятся per-scene сервисы.

### `UiSystemVContainerBindings` — стабы остаются

```csharp
// UIManager (DontDestroyOnLoad) — не реализован
// IWindowFactory                — не реализован
// ITransitionAnimationService   — не реализован
// IWindowRouter / nav stack     — не реализован
```

---

## 11. Диаграмма зависимостей

```
[App Start]
    │
    ▼
GlobalLifetimeScope (DontDestroyOnLoad)
    │
    ├── BootstrapInstaller (ScriptableObject)
    │       ├── RegisterInfrastructure()
    │       │       ├── ICommandLogger
    │       │       ├── ICommandErrorReporter
    │       │       ├── IRequestFactory
    │       │       ├── IConnectionService
    │       │       └── IAddressablesCatalogService
    │       │               └── [EntryPoint] AddressablesWarmupEntryPoint
    │       │
    │       ├── RegisterConfigs()
    │       │       ├── IRemoteConfigService (Firebase RC / Null)
    │       │       ├── IConfigOverrideSource
    │       │       ├── IConfigSource (Server / Local)
    │       │       ├── IConfigsService
    │       │       ├── [EntryPoint] ConfigsWarmupEntryPoint
    │       │       └── [EntryPoint] BookDuneProbeEntryPoint (diagnostic)
    │       │
    │       ├── RegisterSave()
    │       │       ├── IPlayerIdentityProvider
    │       │       ├── ISaveStorage (LocalDisk / Http)
    │       │       └── ISaveService
    │       │
    │       ├── RegisterGameLoading()  ← TODO stubs
    │       ├── RegisterAnalytics()    ← TODO stubs
    │       └── RegisterUiSystem()     ← TODO stubs
    │
    └── GameplayLifetimeScope (child scope, per-scene)
            │
            └── GameInstaller (MonoBehaviour)
                    ├── RegisterResources()   ← TODO
                    ├── RegisterInventory()   ← TODO
                    ├── RegisterShop()        ← TODO
                    ├── RegisterRewardDrop()  ← TODO
                    ├── RegisterIap()         ← TODO
                    ├── RegisterQuest()       ← TODO
                    └── RegisterBookSell()    ← TODO
```

---

## 12. Что ещё не реализовано

| Компонент | Место | Приоритет |
|---|---|---|
| Retry UI (кнопка + `NotifyRetryClicked` биндинг) | `LoadingScreenView` префаб | Средний |
| «Tap to Start» UX и cover/reveal анимации | `LoadingScreenView` / `TransitionAnimationService` | Низкий |
| `MainSceneBootstrap` + `IGameplayReadyGate` в gameplay-сцене | новая фича | По мере появления потребителей |
| `UIManager` (DontDestroyOnLoad) | `UiSystemVContainerBindings` | Высокий |
| `IWindowFactory` | `UiSystemVContainerBindings` | Высокий |
| `IWindowRouter` | `UiSystemVContainerBindings` | Средний |
| `ITransitionAnimationService` | `UiSystemVContainerBindings` | Низкий |
| `IAnalyticsService` | `AnalyticsVContainerBindings` | Средний |
| `SaveSyncBootstrap` регистрация | `SaveVContainerBindings` | Зависит от бэкенда |
| `HttpSaveStorage` активация | `SaveVContainerBindings` | Зависит от бэкенда |
| Все GameplayLifetimeScope фичи | `GameInstaller` | По приоритету фич |

### Сделано в этой миграции

- `LoadingOrchestrator`, `LoadingProgressAggregator`, `LoadingPhase/Group/Operation`-каркас в `Game.Bootstrap.Loading` (новая сборка)
- 6 операций: `AddressablesUpdate / RemoteConfigInit / ConfigsWarmup / SaveDataLoad / Warmup / SceneTransition`
- `LoadingOrchestratorEntryPoint` — заменил три прежних entry point
- `LoadingScreenView` подключён через `RegisterComponentInHierarchy`, ведёт прогресс/статус/ошибку
- `ISceneTransitionService` + `SceneTransitionService` — generic-обёртка над SceneManager для переходов между любыми сценами
- `LoadingSettings` DTO — передача настроек из `BootstrapInstaller` SO в Loading-сборку без циклической asmdef-зависимости
- `GlobalLifetimeScope.DontDestroyOnLoad` — контейнер переживает `LoadSceneAsync(Single)`
- Debug-флаги (`DebugStartFlags` + поля на `BootstrapInstaller` SO)
- Editor-тесты `LoadingOrchestratorTests` (retry, critical failure, timeout, weighted aggregator)
- Попутно: фикс латентной регистрации `LocalDiskStorage` через factory-делегат (default `string` параметр)

---

## 13. Приложение: Reference из Research (для отложенных пунктов)

Раздел сохраняет описание того, как переход boot→gameplay был построен в проекте Research (источник миграции). Используется как референс для **отложенных** пунктов из раздела 12 — `MainSceneBootstrap`, `IGameplayReadyGate`, `IGameplayReadyInitializer`, фаза авторизации, cover/reveal анимации.

В MyBookstore сейчас реализована **только часть** этой архитектуры (см. разделы 6 и 10). Полный Research-флоу был 4-фазным с авторизацией; в MyBookstore — 3 фазы, без авторизации.

### 13.1 Обзор Research-флоу

```
[App Start — Bootstrap Scene]
        Bootstrap.Start()
              │
    RunBootstrapAsync()
              │
    LoadingOrchestrator.RunAsync()
              │
    ┌─────────┴──────────────────────────────────┐
    │  Phase 1: Technical Init (Sequential)       │
    │  Phase 2: Authorization (Sequential)        │
    │  Phase 3: Data Load (Parallel)             │
    │  Phase 4: Finalization (Sequential)         │
    └─────────┬──────────────────────────────────┘
              │ SceneTransitionOperation
              │  → PlayCoverAsync()  (прячет загрузочный экран за transition)
              │  → SceneManager.LoadSceneAsync(mainSceneName)
              ↓
[GameplayScene загружена]
        MainSceneBootstrap.Start()
              │
    uiManager.Show<GameplaySceneController>()
              │
    WaitUntil(IsWindowShown)
              │
    GameplayReadyGate.MarkReadyAsync()
              │
    ┌─────────┴──────────────────────┐
    │  RunInitializersAsync()         │  ← все IGameplayReadyInitializer последовательно
    │  PlayRevealAsync()              │  ← анимация "открытие" экрана
    │  TrackEvent(AppStarted)         │  ← аналитика
    └─────────┬──────────────────────┘
              │
    _isReady = true
    _readyTcs.TrySetResult()          ← разблокирует всех WaitUntilReadyAsync()-ожидателей
```

### 13.2 Bootstrap.cs — точка входа (Research)

`Assets/Game/Core/Bootstrap/Bootstrap.cs` — MonoBehaviour в Bootstrap-сцене, все зависимости через `[Inject]`.

**Инжектируемые зависимости:**

| Зависимость | Роль |
|---|---|
| `UIManager` | Управление UI-окнами |
| `WindowFactoryDI` | Фабрика окон через DI |
| `SaveService` | Сохранения |
| `RemoteConfigLoader` | Firebase RC |
| `IAuthorizationService` | Авторизация игрока |
| `LoadingOrchestrator` | Исполнитель фаз загрузки |
| `TransitionAnimationService` | Анимации перехода (cover/reveal) |

**Параметры (SerializeField):**

| Поле | Значение | Назначение |
|---|---|---|
| `_minimumLoadingSeconds` | 2f | Минимальное время на экране загрузки |
| `_globalLoadingTimeoutSeconds` | 60f | Глобальный таймаут всего флоу |
| `_mainSceneName` | — | Имя сцены для перехода |
| `_loadingScreenView` | — | Ссылка на view загрузочного экрана |

**Логика `RunBootstrapAsync()`:**

```
1. Показать LoadingScreenView
2. Создать LoadingAuthorizationGate (обёртка над IAuthorizationService + LoadingScreenView)
3. BuildPhases(authGate) → список из 4 LoadingPhase
4. orchestrator.SetPhases(phases)
5. Подписаться на ProgressChanged / ActiveDescriptionChanged → обновлять View

6. LOOP (пока не отменено):
    result = await orchestrator.RunAsync(startPhaseIndex, globalTimeout, ct)
    if (result.IsSuccess) → break
    Показать ошибку в View
    Ждать нажатия "Retry"
    startPhaseIndex = result.FailedPhaseIndex   ← resume с упавшей фазы
    orchestrator.ResetFromPhase(startPhaseIndex)

7. Дождаться minimumLoadingSeconds (если прошло меньше)
```

> **В MyBookstore эквивалент:** `LoadingOrchestratorEntryPoint` (раздел 6) — без UIManager/WindowFactory/Auth, без minimumLoadingSeconds, retry-петля та же.

### 13.3 LoadingOrchestrator — движок фаз (Research)

`Assets/Game/Core/Bootstrap/Loading/LoadingOrchestrator.cs` — sealed класс, координирует выполнение фаз и агрегирует прогресс.

**Ключевые события:**

| Событие | Когда |
|---|---|
| `ProgressChanged(float)` | При каждом обновлении взвешенного прогресса (≥ 0.0001 delta) |
| `ActiveDescriptionChanged(string)` | При смене активной операции |
| `CriticalFailure(LoadingFailure)` | При провале критичной операции |

**`RunAsync(startPhaseIndex, globalTimeout, ct)`:**
- Создаёт linked CancellationToken с globalTimeout
- Итерирует фазы начиная с `startPhaseIndex`
- Для каждой группы вызывает `ExecuteSequentialGroupAsync` или `ExecuteParallelGroupAsync`
- При критичном провале → `CriticalFailure?.Invoke()` → `return Failed(failure, phaseIndex)`
- После всех фаз → `CurrentProgress = 1f` → `return Success()`

**Параллельный режим (`ExecuteParallelGroupAsync`):**
- Все операции группы запускаются через `UniTask.WhenAll`
- Прогресс обновляется polling-циклом каждые 100ms
- Если одна операция критично упала → `groupCts.Cancel()` → остальные получают отмену

**Прогресс (`LoadingProgressAggregator`):**
- Взвешенный прогресс: `sum(op.Progress * op.Weight) / sum(op.Weight)`
- Сглаживание: `smoothed += (raw - smoothed) * 0.25f` (exponential moving average)
- Прогресс только растёт (`Math.Max(smoothed, next)`)

**Retry на уровне операции (`ExecuteOperationWithPolicyAsync`):**
- До `maxAttempts` попыток с задержкой `delayBetweenAttempts`
- Таймаут операции — отдельный linked CancellationToken
- Тип ошибки логируется: `result=failed/timeout`

> В MyBookstore перенесён 1:1 — единственная разница в составе фаз/операций. См. `Assets/Game/Core/Bootstrap/Loading/LoadingOrchestrator.cs`.

### 13.4 Фазы загрузки (Research, 4 фазы)

Фазы строятся в `Bootstrap.BuildPhases()`. Каждая `LoadingPhase` содержит одну или несколько `LoadingGroup`.

#### Phase 1 — `phase_technical_init` (Sequential)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `UiManagerConfigureOperation` | `ui_manager_configure` | ✅ | 0.1 | 5s | 1 |
| `FirebaseDependenciesOperation` | `firebase_dependencies` | ❌ | 0.1 | 8s | 2×0.5s |
| `AddressablesUpdateOperation` | `addressables_update` | ✅ | 0.3 | 20s | 2×1s |

#### Phase 2 — `phase_authorization` (Sequential)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `AuthorizationGateOperation` | `authorization_gate` | ✅ | 0.15 | 5 min | 1 |

Логика `LoadingAuthorizationGate.WaitUntilAuthorizedAsync()`:
```
HasCachedToken → скрыть кнопки логина, сразу return
Нет токена    → показать кнопки (Guest / Facebook)
              → WaitForLoginSelectionAsync() (бесконечно)
              → AuthorizeAsync(method)
              → если success → скрыть кнопки, return
              → если fail   → продолжить ждать
```

Таймаут 5 минут — достаточно для ручного логина. Операция критична: без авторизации нет токена для серверных запросов.

#### Phase 3 — `phase_data_load` (Parallel)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `RemoteConfigFetchOperation` | `remote_config_fetch` | ❌ | 0.2 | 10s | 2×0.5s |
| `SaveDataLoadOperation` | `save_data_load` | ✅ | 0.3 | 10s | 2×0.3s |

#### Phase 4 — `phase_finalization` (Sequential)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `WarmupOperation` | `warmup` | ❌ | 0.1 | 5s | 1 |
| `SceneTransitionOperation` | `scene_transition` | ✅ | 0.15 | 15s | 1 |

> **В MyBookstore сейчас:** Phase 1 — без `UiManagerConfigureOperation`; `FirebaseDependenciesOperation + RemoteConfigFetchOperation` объединены в `RemoteConfigInitOperation` через существующий `IRemoteConfigService.InitializeAsync()`. Фаза авторизации отсутствует. Phase 3 заменена на `ConfigsWarmupOperation + SaveDataLoadOperation`. Phase 4 — `WarmupOperation + SceneTransitionOperation`. Итого 3 фазы, 6 операций.

### 13.5 SceneTransitionOperation — переход в сцену (Research)

`Assets/Game/Core/Bootstrap/Loading/Operations/SceneTransitionOperation.cs`

```csharp
protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
{
    await UniTask.Yield(PlayerLoopTiming.Update, ct);         // даём кадр отрисоваться
    await _transitionAnimationService.PlayCoverAsync(ct);     // 1. закрыть экран (cover)
    var asyncOperation = SceneManager.LoadSceneAsync(_sceneName);  // 2. начать загрузку сцены
    while (!asyncOperation.isDone)
    {
        ReportProgress(asyncOperation.progress >= 0.9f ? 1f : asyncOperation.progress);
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
    ReportProgress(1f);
}
```

**Что происходит:**
1. `PlayCoverAsync()` — transition animation закрывает экран (игрок видит fade/curtain)
2. `LoadSceneAsync()` — Unity выгружает Bootstrap-сцену и загружает GameplayScene в фоне
3. Пока загружается — репортим прогресс (Unity даёт 0–0.9 в процессе, 1.0 только по завершению)
4. Экран остаётся закрытым до `PlayRevealAsync()` в `GameplayReadyGate`

**Важно:** экран открывается НЕ здесь, а позже — в `GameplayReadyGate.MarkReadyAsync()`. Это гарантирует, что игрок видит геймплейную сцену только после того, как все feature-инициализаторы отработали.

> **В MyBookstore сейчас:** аналогичная `SceneTransitionOperation`, но без `PlayCoverAsync`/`PlayRevealAsync` (cover/reveal-анимации отложены — см. раздел 12). Прогресс репортится так же.

### 13.6 MainSceneBootstrap (Research, не реализовано в MyBookstore)

`Assets/Game/Core/Bootstrap/MainScene/MainSceneBootstrap.cs` — MonoBehaviour в GameplayScene. VContainer инжектирует зависимости.

**Инжектируемые зависимости:**
- `UIManager` — показывает окна
- `IGameplayReadyGate` — барьер готовности

**`LoadGameplayAsync()`:**

```csharp
_uiManager.Show<GameplaySceneController>();          // 1. открыть главное UI окно геймплея
await UniTask.WaitUntil(IsWindowShown<GameplaySceneController>);  // 2. ждать пока UI готово
await _gameplayReadyGate.MarkReadyAsync(ct);         // 3. сигнализировать о готовности
```

**GameplaySceneController** — главное окно игровой сцены. Пока оно не показалось, сцена не считается готовой. Гарантирует, что UI полностью инициализировано до открытия экрана.

> **В MyBookstore:** не реализовано. Появится, когда у gameplay-сцены будет реальный UI-каркас (требует UIManager — отложен).

### 13.7 GameplayReadyGate — главный барьер готовности (Research, не реализовано в MyBookstore)

`Assets/Game/UI/UIShared/Scripts/GameplayReadyGate/GameplayReadyGate.cs`

Реализует `IGameplayReadyGate`. Регистрируется в `GameInstaller` как Singleton:
```csharp
builder.Register<IGameplayReadyGate, GameplayReadyGate>(Lifetime.Singleton);
```

**Интерфейс:**
```csharp
public interface IGameplayReadyGate
{
    bool IsReady { get; }
    UniTask WaitUntilReadyAsync(CancellationToken ct);
    UniTask MarkReadyAsync(CancellationToken ct);
}
```

**`MarkReadyAsync()` — детальный флоу:**

```
if (_isReady) → return                               // уже готово
if (_isMarkingReady)                                 // параллельный вызов
    → WaitUntilReadyAsync()                          // ждём завершения первого
    → return

_isMarkingReady = true
try {
    1. RunInitializersAsync(ct)
       │   for each IGameplayReadyInitializer:
       │       await initializer.InitializeAsync(ct)  // строго последовательно
       │
    2. await _transitionAnimationService.PlayRevealAsync(ct)
       │   // открывает экран — игрок видит геймплей
       │
    3. _analytics.TrackEvent(AppStarted)
       │
    4. _isReady = true
    5. _readyTcs.TrySetResult()                      // разблокирует WaitUntilReadyAsync()
}
finally { _isMarkingReady = false }
```

**`WaitUntilReadyAsync()`:**
```csharp
if (_isReady) return UniTask.CompletedTask;   // уже готово — мгновенно
return _readyTcs.Task.AttachExternalCancellation(ct);  // ждём сигнала
```

Внутри `UniTaskCompletionSource` — стандартный паттерн "one-shot event" в UniTask.

**Порядок событий при MarkReady:**
```
[все инициализаторы завершены]
    → PlayRevealAsync()      ← СНАЧАЛА анимация (экран открывается)
    → TrackEvent(AppStarted) ← ПОТОМ аналитика
    → TrySetResult()         ← ПОТОМ разблокировать ожидателей
```

> **В MyBookstore:** не реализовано. Добавим, когда появится первая фича, которой нужно дождаться полной готовности сцены (например, OrchestratorRunner-аналог для live-ops).

### 13.8 IGameplayReadyInitializer — паттерн расширения (Research, не реализовано в MyBookstore)

`Assets/Game/UI/UIShared/Scripts/GameplayReadyGate/IGameplayReadyInitializer.cs`

```csharp
public interface IGameplayReadyInitializer
{
    UniTask InitializeAsync(CancellationToken ct);
}
```

**Как добавить новый инициализатор:**

1. Создать класс, реализующий `IGameplayReadyInitializer`
2. Зарегистрировать в инсталлере фичи:
```csharp
builder.Register<MyFeatureGameplayReadyInitializer>(Lifetime.Singleton)
       .As<IGameplayReadyInitializer>();
```
3. VContainer автоматически соберёт все `IGameplayReadyInitializer` в `IEnumerable<>` и инжектирует в `GameplayReadyGate`

**Текущие реализации в Research:**

| Класс | Фича | Что делает |
|---|---|---|
| `BattlePassGameplayReadyInitializer` | BattlePass | `IBattlePassStartupSync.InitializeAsync()` — синхронизация прогресса BP с сервером |

**Обработка ошибок в инициализаторах:**

`BattlePassGameplayReadyInitializer` ловит все `Exception` (кроме `OperationCanceledException`) и логирует `Warning` — ошибка в инициализаторе **не блокирует** открытие геймплея:

```csharp
catch (Exception exception)
{
    Debug.LogWarning($"[...] Initial Battle Pass sync failed. {exception.Message}");
    // не re-throw — gate продолжает
}
```

### 13.9 Потребители IGameplayReadyGate (Research)

Системы, которым нужно дождаться полной готовности сцены перед стартом, инжектируют `IGameplayReadyGate` и вызывают `WaitUntilReadyAsync()`.

#### OrchestratorRunner

`Assets/Game/Features/EventOrchestration/Module/Infrastructure/OrchestratorRunner.cs`

MonoBehaviour, управляет live-ops событиями (card collection events и т.д.).

```csharp
private async UniTask RunAsync(CancellationToken ct)
{
    await _gameplayReadyGate.WaitUntilReadyAsync(ct);   // ← ждёт открытия экрана
    await _orchestrator.InitializeAsync(ct);             // загружает расписание событий
    await UniTask.WhenAll(
        RunTickLoopAsync(ct),       // тикает каждые ~1s
        RunRefreshLoopAsync(ct));   // обновляет расписание каждые ~60s
}
```

Оркестратор НЕ стартует до тех пор, пока игрок физически не видит геймплейную сцену. Это предотвращает триггер событий в момент загрузки.

### 13.10 Полная диаграмма (Research)

```
Bootstrap Scene
─────────────────────────────────────────────────────────────────────────
Bootstrap.Start()
  ↓
RunBootstrapAsync()
  ↓ показать LoadingScreen
  ↓ создать LoadingAuthorizationGate
  ↓ BuildPhases()
  ↓
LoadingOrchestrator.RunAsync()
  │
  ├─ PHASE 1: phase_technical_init [Sequential]
  │     UiManagerConfigureOperation  [critical, w=0.1, t=5s]
  │     FirebaseDependenciesOperation [non-critical, w=0.1, t=8s, retry×2]
  │     AddressablesUpdateOperation   [critical, w=0.3, t=20s, retry×2]
  │
  ├─ PHASE 2: phase_authorization [Sequential]
  │     AuthorizationGateOperation   [critical, w=0.15, t=5min]
  │       └─ WaitUntilAuthorizedAsync()
  │            ├─ HasCachedToken → immediate
  │            └─ No token → show login UI → wait for tap
  │
  ├─ PHASE 3: phase_data_load [Parallel]
  │     ┌─ RemoteConfigFetchOperation [non-critical, w=0.2, t=10s, retry×2]
  │     └─ SaveDataLoadOperation      [critical, w=0.3, t=10s, retry×2]
  │
  └─ PHASE 4: phase_finalization [Sequential]
        WarmupOperation               [non-critical, w=0.1, t=5s]
        SceneTransitionOperation      [critical, w=0.15, t=15s]
          ↓
          PlayCoverAsync()            ← transition закрывает экран
          SceneManager.LoadSceneAsync(mainSceneName)

═══════════════════════════════════════════════════════════════════════════
                         [Сцена загружена]
═══════════════════════════════════════════════════════════════════════════

Gameplay Scene
─────────────────────────────────────────────────────────────────────────
MainSceneBootstrap.Start()
  ↓
LoadGameplayAsync()
  ↓
uiManager.Show<GameplaySceneController>()
  ↓
WaitUntil(IsWindowShown<GameplaySceneController>)
  ↓
GameplayReadyGate.MarkReadyAsync()
  │
  ├─ RunInitializersAsync()       [sequential]
  │     BattlePassGameplayReadyInitializer
  │       └─ IBattlePassStartupSync.InitializeAsync()   ← sync BP от сервера
  │
  ├─ PlayRevealAsync()            ← экран открывается, игрок видит геймплей
  ├─ TrackEvent(AppStarted)       ← аналитика
  ├─ _isReady = true
  └─ _readyTcs.TrySetResult()     ← разблокировать ожидателей

               ↓ разблокировано
OrchestratorRunner.WaitUntilReadyAsync() → InitializeAsync() → tick/refresh loops
```

### 13.11 Справка: параметры операций (Research)

| Операция | Вес прогресса | Critical | Timeout | Max Retry |
|---|---|---|---|---|
| `UiManagerConfigureOperation` | 0.1 | ✅ | 5s | 1 |
| `FirebaseDependenciesOperation` | 0.1 | ❌ | 8s | 2 |
| `AddressablesUpdateOperation` | 0.3 | ✅ | 20s | 2 |
| `AuthorizationGateOperation` | 0.15 | ✅ | 5min | 1 |
| `RemoteConfigFetchOperation` | 0.2 | ❌ | 10s | 2 |
| `SaveDataLoadOperation` | 0.3 | ✅ | 10s | 2 |
| `WarmupOperation` | 0.1 | ❌ | 5s | 1 |
| `SceneTransitionOperation` | 0.15 | ✅ | 15s | 1 |

**Итоговый суммарный вес:** 1.5 (используется для нормализации взвешенного прогресса)

**Операция с флагом Critical=true:** при провале после исчерпания retry → `LoadingRunResult.Failed(failedPhaseIndex)` → Bootstrap показывает ошибку и предлагает retry с этой фазы.

**Операция с Critical=false:** при провале — логируется, игра продолжается.

> **Сравнение с MyBookstore:** см. реальные операции в разделе 6 и в `Assets/Game/Core/Bootstrap/Loading/Operations/`. Веса/таймауты/retry-policy в портированных операциях скопированы 1:1.

---

## 14. Архитектурные альтернативы: почему `ILoadingOperation`, а не `ICommand`

В проекте есть два неконкурирующих, но архитектурно похожих «фундамента единицы работы»:

- **`ICommand`** (сборки `Game.Commands.Abstractions` / `Game.Commands` / `Game.Http`) — общий контракт «единица работы» для фич и REST-запросов. См. `docs/README_Commands.md`. Используется в `Save` (`HttpSaveStorage`), `Configs` (`ServerConfigSource`, `GetConfigCommand`, `GetConfigsManifestCommand`).
- **`ILoadingOperation`** (сборка `Game.Bootstrap.Loading`) — единица работы **только для бутстрапа**, со встроенными retry/timeout/critical/weight/displayPriority и агрегатором прогресса с EMA-сглаживанием.

В референсном проекте Heroes (см. `docs/archive/heroes-loading-scene-transitions.md`) вся загрузка построена на `ICommand`: `IProgressQueueCommand` + `BoxCommandWithDependencies` + `DependentPhasesController<T>`. То есть один контракт для всего — загрузки, фич, HTTP.

В MyBookstore выбран другой путь — Operation-based, источник миграции **Research**, а не Heroes. Решение зафиксировано в `docs/archive/pet-gap.md` §1, §7. Эта секция объясняет почему и когда стоит пересмотреть выбор.

### Сравнение

| Аспект | Heroes (`ICommand`-based) | MyBookstore (`ILoadingOperation`-based) |
|---|---|---|
| Базовый юнит | `ICommand` (общий с фича-командами и HTTP) | `ILoadingOperation` (loading-only) |
| Координация | `DependentPhasesController<T>` + фазы-маяки | `LoadingPhase` → `LoadingGroup` → `Sequential/Parallel` |
| Прогресс | очередь с весами; сумма по очередям руками | взвешенный агрегат + EMA-сглаживание встроены |
| Retry / Timeout | пер-команда, через окно ошибок + UI | декларативно в `LoadingOperationBase` (RetryPolicy, Timeout, IsCritical) |
| Ошибки | window-based (`ShowLoadingErrorWindowWithDelayedCallback`) | `LoadingFailure` + retry-loop в `Bootstrap.cs` |
| Зависимости | Тянет `WindowsService`, `LinearLoadingTime`, `GameStateService` | Только `UniTask` + `UnityEngine.Debug` |
| Тесты | завязаны на window-сервис и DI | `LoadingOrchestratorTests` — чистый NUnit, без Unity-зависимостей |

### Аргументы за `ILoadingOperation` (текущий выбор)

1. **Изолированный готовый блок.** Из `pet-gap.md` §1: «11 файлов ядра + 8 файлов операций, все в одном namespace `Game.Bootstrap.Loading`, внешних зависимостей нет кроме UniTask и UnityEngine.Debug.» Копируется одним заходом.
2. **Встроенные UX-фичи прогресса** — взвешенный агрегат + EMA-сглаживание + `DisplayPriority` для параллельных операций. В Heroes эти вещи дописаны поверх Command-стека вручную.
3. **Тестируемость** — `LoadingOrchestratorTests` покрывают retry, critical failure, timeout, monotone progress без Unity-зависимостей.
4. **Размер задачи** — 3 фазы и 6 операций. На таком масштабе плюсы единого `ICommand` (один контракт во всём проекте) не окупают переписывание оркестратора и операций.

### Аргументы за `ICommand` (отвергнутая альтернатива)

1. **Один контракт «единицы работы» во всём проекте** — концептуально красиво. Сейчас загрузка живёт в своём «карантине», фича-команды — в `Game.Commands`.
2. **`DependentPhasesController<T>`** даёт более гибкие зависимости, чем плоские группы. Полезно, когда фазы не просто Sequential/Parallel, а образуют граф (как при загрузке локаций в Heroes: `LoadLocalSave → FindServerUrl → CreateServerConnection → GetCatalogFromServer`).
3. **`ICommandsFactory.GetProgressQueueCommand(...)`** из `Game.Commands` уже умеет делать очереди с весами — для in-game загрузок локаций он мог бы оказаться удобнее.

### Когда пересмотреть решение

1. **In-game loading локаций.** Когда `Sales`/`Preparation`-сцены перестанут быть простым `SceneManager.LoadSceneAsync(Single)` и начнут грузить контент локации (как `LoadLocationCommand` в Heroes раздел 4–5: PrimaryQueue + SecondaryQueue, зависимости между «UnloadCurrentScene → DownloadDependencies → LoadAndRunLocationCommand»). На таком масштабе `DependentPhasesController` может выиграть у плоских `LoadingPhase`. Сделать ADR с прямым сравнением.
2. **Тяжёлая платёжно-серверная воронка на старте.** Если фаза `phase_data_load` дорастёт до 10+ операций с нетривиальным графом зависимостей (login → sync → static data → progress → seasons + UI props параллельно), и `LoadingGroup` начнёт «гнуться» — стоит посмотреть в сторону Heroes-style зависимостей.
3. **Унификация UI-флоу ошибок.** Если появится централизованный `IErrorWindowService`, разумный шаг — повесить его на единый контракт (`ICommand` его уже имеет через `ICommandErrorReporter`).

### Что НЕ является аргументом для перехода

- «Чтобы было как в Heroes». Heroes — референсный проект, а не source-of-truth.
- «Чтобы был один интерфейс». На текущем размере это эстетика, не функциональность.
- «`ICommand` поддерживает HTTP». Подсистема Save/Configs/HTTP уже на `ICommand`, и пересечения с бутстрапом нет.

### Итог

Текущий выбор `ILoadingOperation` обоснован масштабом задачи и качеством готового кода из Research. Базовый `ICommand`-стек в проекте есть и работает (Save, Configs, Http) — он не конкурент `ILoadingOperation`, а инструмент для другого уровня (фичи, REST). Пересмотр стоит откладывать до момента, когда сложность загрузки превысит возможности `LoadingPhase`/`LoadingGroup` — это произойдёт **не раньше** реальной in-game загрузки локаций.

Источники для ретроспективы:
- `docs/README_Commands.md` — спецификация `ICommand`/`Game.Http`-стека.
- `docs/archive/heroes-loading-scene-transitions.md` — полная картина Heroes-style загрузки на `ICommand`.
- `docs/archive/pet-gap.md` §1, §7 — оригинальное решение взять Research-source.
