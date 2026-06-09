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

## 6. Entry Points

Entry Points — классы, реализующие `IAsyncStartable` из VContainer.Unity. VContainer вызывает их `StartAsync()` после сборки контейнера. Все работают с `CancellationToken` и устойчивы к `OperationCanceledException`.

### Порядок запуска (при старте приложения)

```
[App Start]
    │
    ├─ 1. AddressablesWarmupEntryPoint.StartAsync()
    │       └── IAddressablesCatalogService.InitializeAndUpdateAsync()
    │           ├── (success) каталог обновлён с CDN (Cloudflare R2)
    │           └── (network error) продолжаем на встроенном каталоге, ошибка логируется
    │
    ├─ 2. ConfigsWarmupEntryPoint.StartAsync()
    │       ├── IRemoteConfigService.InitializeAsync()  ← Firebase RC (или NullRC)
    │       │     └── (error) предупреждение, продолжаем без RC override'ов
    │       └── IConfigsService.WarmupAsync()           ← загружаем base configs в кэш
    │
    └─ 3. BookDuneProbeEntryPoint.StartAsync()           ← DIAGNOSTIC только, удалить когда не нужен
            └── IConfigsService.GetAsync<BookConfig>("book_dune") → лог в консоль
```

> Entry points диспатчатся VContainer'ом асинхронно. Порядок их выполнения относительно друг друга определяется порядком `RegisterEntryPoint<>` в биндингах.

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

### Текущий статус: **НЕ РЕАЛИЗОВАНЫ**

`GameLoadingVContainerBindings` содержит только TODO-стабы:

```csharp
// LoadingProgressAggregator    — не реализован
// LoadingOrchestrator           — не реализован
// ISceneTransitionService       — не реализован
// Loading screen prefab         — не реализован
```

`UiSystemVContainerBindings` аналогично:

```csharp
// UIManager (DontDestroyOnLoad) — не реализован
// IWindowFactory                — не реализован
// ITransitionAnimationService   — не реализован
// IWindowRouter / nav stack     — не реализован
```

### Как задумано (по комментариям в коде)

Переходы планируются через `ISceneTransitionService` (синглтон в GlobalScope):
- Загрузочный экран — DontDestroyOnLoad-префаб, управляется из глобального скопа
- `LoadingOrchestrator` координирует шаги загрузки через систему прогресса
- `GameplayLifetimeScope` создаётся при переходе в игровую сцену, уничтожается при выходе

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
| `ISceneTransitionService` | `GameLoadingVContainerBindings` | Высокий |
| `LoadingOrchestrator` | `GameLoadingVContainerBindings` | Высокий |
| `LoadingProgressAggregator` | `GameLoadingVContainerBindings` | Средний |
| Loading screen prefab | `GameLoadingVContainerBindings` | Средний |
| `UIManager` (DontDestroyOnLoad) | `UiSystemVContainerBindings` | Высокий |
| `IWindowFactory` | `UiSystemVContainerBindings` | Высокий |
| `IWindowRouter` | `UiSystemVContainerBindings` | Средний |
| `ITransitionAnimationService` | `UiSystemVContainerBindings` | Низкий |
| `IAnalyticsService` | `AnalyticsVContainerBindings` | Средний |
| `SaveSyncBootstrap` регистрация | `SaveVContainerBindings` | Зависит от бэкенда |
| `HttpSaveStorage` активация | `SaveVContainerBindings` | Зависит от бэкенда |
| Все GameplayLifetimeScope фичи | `GameInstaller` | По приоритету фич |

> `BookDuneProbeEntryPoint` зарегистрирован в `ConfigsVContainerBindings` как диагностика — убрать строку `builder.RegisterEntryPoint<BookDuneProbeEntryPoint>()` когда RC-флоу проверен.
