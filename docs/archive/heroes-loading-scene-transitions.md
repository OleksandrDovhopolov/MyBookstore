# Heroes — Логика загрузки игры и переходов между сценами

> Проект: `C:\Projects\heroes`  
> Дата анализа: 2026-06-09

---

## Обзор архитектуры

Загрузка построена на **Command-паттерне** (`IProgressQueueCommand`, `BoxCommandWithDependencies`).  
Параллельные очереди координируются через `DependentPhasesController<T>` — каждая команда может ждать завершения определённых фаз прежде чем продолжить.  
Прогресс отображает синглтон `LoadingScreen`. Метрики фиксирует `LinearLoadingTime`.

---

## 1. Точка входа — `Bootstrapper`

**Файл:** `Assets/Root/Bootstrapper.cs`  
**ExecutionOrder:** `-200` (первый среди MonoBehaviour)

### Что делает

1. **`Register()`** — создаёт DI-контейнер VContainer, регистрирует все сервисы через `RootGameRegister` (или `RootGameRegister_Lite` в лёгком режиме).
2. **`Bootstrap()`**:
   - `DontDestroyOnLoad(this)` — объект переживает смену сцен.
   - `CheckLocationLoadingScenario()` — **только в редакторе**: если стартовая сцена уже является локацией или BattleEpisode, устанавливает дебаг-флаги и переключает `LoadSceneMode` на `Additive`.
   - `LoadBootScene()` — асинхронно загружает сцену `"main"` (или `"main_lite"` в лёгком режиме).

### Дебаг-флаги (Editor only)

| Флаг | Назначение |
|---|---|
| `UseDebugSimplifiedStart` | Пропустить полный флоу загрузки |
| `DebugStartLocationName` | Принудительно открыть конкретную локацию |
| `RunEpisodeInsteadOfLocation` | Запустить Battle Episode вместо локации |
| `IsBootStrappedFromBrebuiltScene` | Сцена-локация — prebuild или нет |

---

## 2. Основная загрузка — `RootEntry`

**Файл:** `Assets/Root/RootEntry.cs`  
**ExecutionOrder:** `-1`  
Находится на сцене `"main"`, загружаемой `Bootstrapper`.

### Жизненный цикл

```
Awake()
  └─ RegisterServices()        ← DI (если ещё не зарегистрировано), инициализация сервисов
     └─ GameStateService.SetLoadingGame()

Start()
  ├─ PrepareBuildInCatalog     ← только на девайсе
  └─ StartLoading()
```

### Структура очередей в `StartLoading()`

Все очереди создаются с `QueueExecuteMode.Manual` и добавляются в `BoxCommandWithDependencies` ("RootMainBox"). Веса прогресса указаны в скобках.

```
RootMainBox
├── CdnQueue        (10%)
├── DeviceQueue     (5%)
├── ServerQueue     (15%)
├── AnalyticsQueue  (1%)
├── PreloadRequiredAssetsQueue  (20%)
├── ThirdPartyQueue (10%)
└── StartGameQueue  (оставшийся прогресс)
```

#### CdnQueue
- Ждёт: `GetCatalogFromServer`, `PrepareBuildInCatalog`
- `AddressablesInitializeCommand` — инит Addressables
- `ShaderWarmUpCommand` — прогрев шейдеров (не блокирует)
- `TryPreloadBuildInDependenciesCommand` → отмечает фазу `InitAddressables`
- Ждёт: `InitServices`, `StaticDataReady`
- `TryPreloadFeaturesByEventsCommand`

#### DeviceQueue
- `CheckFreeSpaceCommand`
- `LoadOrCreateLocalSaveCommand` → фаза `LoadLocalSave`, определяет `RunType`
- Ждёт: `InitAddressables`, `UpdateDataAfterLogin`
- `InitializeServicesCommand` → фаза `InitServices`
- `IapCommandsFactory.GetStartIapInitializationCommand()`

#### ServerQueue
- Ждёт: `LoadLocalSave`
- `FindServerBaseUrlCommand`
- `CreateServerConnectionCommand` → фаза `GetCatalogFromServer`
- `ReadLocalStaticDataCommand`
- `ServerLoginCommand` → фаза `StaticDataReady`
- `GetStaticDataFromServerCommand`
- `TryLoadProgressFromServer` → фаза `LoadServerProgress`
- `InitialSyncClientServerDataCommand` → фаза `SyncClientServerData`
- `GetStartDataFromServerCommand`
- `UpdateDataAfterLoginCommand` → фаза `UpdateDataAfterLogin`
- Ждёт: `AuthenticateSocial`
- `CheckGDPRCommand` → фаза `CheckGdpr`
- `IncrementSessionNumberCommand`
- `LoadPvpSeasonsCommand`

#### PreloadRequiredAssetsQueue
- Ждёт: `InitAddressables`
- Параллельно: `InitRootMBs`, `InitializeLocalizationCommand`, `InitAudioCommand`, `LoadUIPropsCommand` → фаза `LoadUiProps`
- Ждёт: `InitServices`
- `PreloadAvatarCommand`

#### ThirdPartyQueue
- Ждёт: `GetCatalogFromServer`
- `InitAwsCommand` (не блокирует)
- `InitializeUnityServicesCommand` (не блокирует)
- `AuthenticateSocialCommand` → фаза `AuthenticateSocial`
- Ждёт: `CheckGdpr`
- `InitSocialCommand` (Facebook + Google, не блокируют)

#### StartGameQueue
- Ждёт: `InitServices`
- `StartGameCommand` (90% веса) + `InitAdditionalScriptsCommand`

### Обработка ошибок загрузки

- Каждая очередь сообщает об ошибке через `OnCommandInQueueCompleteWithFailure()`.
- Показывается `WindowsService.ShowLoadingErrorWindowWithDelayedCallback()` или специальное окно (нет интернета, нет места).
- Поддерживается **ретрай** конкретной упавшей команды: `queue.RetryCompletedCommand()`.
- История ошибок пишется в `LoadingErrorHistory` (локальный файл).

### После загрузки — `OnLoadingComplete()`

1. Запускается `SaveLoop`.
2. Запускается `AfterLoadQueue` (через 1 сек):
   - Параллельно: `PreloadWidgetsCommand`, `PreloadHeroesAtStartCommand`, `PreloadBattleAssetsCommand`, `PreloadMapAssetsCommand`, `PreloadEventExpeditionsAssetsCommand`, `TrySyncChatsRoomsCommand`.
3. `GameStateService.SetCompleteLoading(true)`.
4. Трекинг аналитики времени загрузки.

---

## 3. Запуск стартовой локации — `StartGameCommand`

**Файл:** `Assets/Root/Initialization/StartGameCommand.cs`

Завершает загрузку переходом в первую локацию:

```
ExecInternalAsync()
  ├── if RunEpisodeInsteadOfLocation  →  RunLoadedEpisode()   (дебаг)
  └── else                            →  LoadStartLocation()
         └── LoadLocationCommand(GetStartLocationName(), "start_game", isFirstLoad: true)
```

### `GetStartLocationName()` — логика выбора стартовой локации

```
1. Дебаг-флаг DebugStartLocationName  →  использовать его
2. Нет сохранённой локации            →  FirstSessionConfig.StartLocation
3. Экспедиция закрыта или null        →  CitySettings.CityLocationName
4. Иначе                              →  CurrentLocation.GetCurrentLocation()
```

---

## 4. Переход между локациями — `LoadLocationCommand`

**Файл:** `Assets/Game/GameScenes/LoadLocationCommand.cs`

Используется как при первом старте (`StartGameCommand`), так и при переходах в игре (`GameScenesService.LoadLocation()`).

### Флоу

```
ExecInternalAsync()
  ├── LoadingScreen.ShowWhileLocationLoading()
  ├── GameStateService.SetLoadingLocation(name)
  ├── LocationAudioLoader.UnloadAudioBanks()
  ├── await ConfigsService.GetAsync<LocationConfig>()
  └── RunLoadingProcess()
        └── BoxCommandWithDependencies ("LocMainBox")
              ├── PrimaryQueue   (70%)
              └── SecondaryQueue (30%)
```

#### PrimaryQueue (70%)

| Команда | Описание |
|---|---|
| `LoadBackCommand` | Загружает фоновое изображение экрана загрузки (пропускается при первой загрузке) |
| `UnloadCurrentScene` | Выгружает текущую локацию |
| `DownloadDependenciesCommand` | Скачивает Addressables-зависимости локации |
| `LoadAndRunLocationCommand` | Загружает сцену и полностью инициализирует локацию ← **основная работа** |
| `TryStartScriptingVideoCommand` | Запускает видео из скриптинга (не при первой загрузке) |

#### SecondaryQueue (30%)

| Команда | Описание |
|---|---|
| Ждёт `DownloadDependencies` | Синхронизация с PrimaryQueue |
| `BannersLoader.GetLoadActiveBannersCommand` | Загрузка активных баннеров |
| `PreloadLocalizationTableCommand` | Локализация для локации |
| Ждёт `LoadLocationScene` | Синхронизация с PrimaryQueue |
| `LocationAudioLoader.GetLoadLocationAudioBankCommand` | Аудио-банк локации |
| `LoadAdditionalAudioCommand` | Доп. аудио из конфига |

### После загрузки

- `LocationLoadingCompleteSignal` → подхватывает `GameStateService`.
- Трекинг аналитики (`TrackLocationLoadingComplete`, `TrackLocationChanged`).
- `AfterLoadQueue` через 5 сек: `PreloadBuildingsCommand`, `PreloadArenaBacks`.
- Через 30 сек: `StartPreloadNextExpeditionsCommand`.

### Обработка ошибок

- Ошибки в `LoadAndRunLocationCommand` — критические: показывается `ShowCriticalLoadingErrorWindow` с кнопкой рестарта приложения.
- Остальные ошибки — ретрай через `ShowLoadingErrorWindowWithDelayedCallback`.

---

## 5. Инициализация локации — `LoadAndRunLocationCommand`

**Файл:** `Assets/Game/Expeditions/Loading/LoadAndRunLocationCommand.cs`

Комментарий в коде явно описывает порядок инициализации:

```
// 1. Загрузка всех ассетов (сцена, скриптинг)
// 2. Получение данных из них и формирование сетапа локации
// 3. Создание логической сущности локации, инициализация данными
// 4. Формирование и связывание визуала с логикой
// 5. Активация логики — объекты сообщают о своём состоянии
// 6. Вьюхи переходят в нужное визуальное состояние
```

### Очередь инициализации

```
PrepareRandomBattlesCommand          ← подготовка случайных боёв
LoadLocationSceneAndScriptCommand    ← загрузка Unity-сцены + скриптинг (Blackboard)
  └── GameScenesService.TryLoadScene()
LoadConfigs                          ← LocationConfig из загруженной сцены
InitEnvironment                      ← FogViewsManager, ScriptingService.StartLocationScript()
CreateBakedBuildingsCommand
CreateSetup                          ← LocationSetup (объекты, туманы, навигация) + данные из сейва
CreateLocation                       ← логическая сущность Location, Location.Bind(saveEntry)
PreloadLocationObjectPrefabsAsync
─── Параллельно ───────────────────
│   LoadInteractionAssetsAsync
│   PreInitLocationCacheCommand
│   LoadRandomMobsCommand
───────────────────────────────────
CreateBakedGrowingsCommand
ActivateCamera                       ← LocationCamera.Setup() + Activate()
ActivateInteractions                 ← GestureController.Activate()
SetupObjectsViewsAsync               ← визуальные представления объектов
ActivateLocation                     ← Location.Activate(), ExpeditionServiceInternal.Bind()
```

---

## 6. Загрузка Unity-сцены — `LoadLocationSceneAndScriptCommand`

**Файл:** `Assets/Game/Expeditions/Loading/LoadLocationSceneAndScriptCommand.cs`

```
ExecInternalAsync()
  ├── Application.backgroundLoadingPriority = ThreadPriority.Normal
  ├── await TryLoadLocationSceneAsync()   ← GameScenesService.TryLoadScene()
  │     └── GameSceneController.TryLoadScene()
  │           └── AddressablesCommandsFactory.GetLoadSceneCommand()
  ├── CompleteLoadSceneEvent?.Invoke()    ← сигнализирует LoadLocationCommand о готовности сцены
  ├── await TryLoadGlobalBb() + TryLoadExpeditionBb()  (параллельно)
  ├── Валидация: scene != null, LocationView найден, SpriteSheet найден
  └── View = locationView
```

---

## 7. Управление сценами — `GameSceneController`

**Файл:** `Assets/Game/GameScenes/GameSceneController.cs`

Низкоуровневый контроллер, хранит ссылку на одну загруженную сцену.

```
TryLoadScene(sceneName, sceneAddress)
  ├── если загрузка идёт → await завершения
  ├── если уже загружена та же сцена → вернуть её
  ├── если загружена другая сцена → ошибка (лог), return null
  └── LoadSceneFromAddressables(sceneAddress)

TryUnloadCurrentScene()
  ├── если SceneInstance → AddressablesService.UnloadSceneAsync()
  └── иначе → SceneManager.UnloadSceneAsync()
```

В редакторе адрес `scene` заменяется на `scene_editor` если не используется Addressables-локатор или не задан флаг `PreferPrebuiltLocationsInPlayMode`.

---

## 8. Состояния игры — `GameStateService`

**Файл:** `Assets/Game/GameScenes/GameStateService.cs`

Отслеживает текущее состояние через сигналы:

| Состояние (`GameStateType`) | Когда |
|---|---|
| `LoadingGame` | `Bootstrapper` → `RootEntry.Awake()` |
| `LoadingExpedition` | `LoadLocationCommand.ExecInternalAsync()` |
| `Expedition` | После `LocationLoadingCompleteSignal` |
| `PrepareBattle` | `ActivateBattlePrepareSignal` |
| `Battle` | `BattleStartedSignal` |
| `BattleEnd` | `BattleBeforeOpenResultSignal` |
| `PrepareBE` / Battle episode | `BattleEpisodeStartedSignal` |

---

## Полная схема загрузки (упрощённо)

```
[Сцена boot/любая]
      │
      ▼
Bootstrapper.Awake()
  ├── Register() → DI-контейнер VContainer
  └── Bootstrap()
        ├── CheckLocationLoadingScenario()  [Editor]
        └── SceneManager.LoadSceneAsync("main")
              │
              ▼
        RootEntry.Awake()
          └── RegisterServices()

        RootEntry.Start()
          └── StartLoading()
                ├── CdnQueue ──────────┐
                ├── DeviceQueue ───────┤  параллельно,
                ├── ServerQueue ───────┤  координация через
                ├── AnalyticsQueue ────┤  DependentPhasesController
                ├── PreloadReqQueue ───┤
                ├── ThirdPartyQueue ───┘
                └── StartGameQueue
                      └── StartGameCommand
                            └── LoadLocationCommand("city"/"location_x", isFirstLoad: true)
                                  ├── PrimaryQueue
                                  │     ├── UnloadCurrentScene
                                  │     ├── DownloadDependencies
                                  │     └── LoadAndRunLocationCommand
                                  │           ├── LoadLocationSceneAndScriptCommand
                                  │           │     └── GameSceneController.TryLoadScene()  [Addressables]
                                  │           ├── CreateSetup / CreateLocation
                                  │           └── ActivateLocation
                                  └── SecondaryQueue
                                        ├── LoadBanners
                                        └── LoadAudio
```

---

## Ключевые зависимости между фазами

```
PrepareBuildInCatalog ──┐
                        ├──► CdnQueue: AddressablesInitialize
GetCatalogFromServer ───┘

LoadLocalSave ──────────────► ServerQueue: FindServerUrl → CreateServerConnection
                                                          └──► GetCatalogFromServer (фаза)

LoadLocalSave + InitAddressables ──► AnalyticsQueue: StartAnalytic

InitAddressables ──────────────────► PreloadReqQueue: InitLocalization + Audio + UI Props

InitServices + StaticDataReady ────► CdnQueue: TryPreloadFeaturesByEvents
InitServices + LoadUiProps ─────────► ServerQueue: CheckGDPR

InitServices ──────────────────────► StartGameQueue: StartGameCommand
```

---

## Где искать что

| Задача | Файл |
|---|---|
| DI-регистрация всех сервисов | `Assets/Root/RootGameRegister.cs` |
| Параметры EntryPoint (ссылки на prefab) | `Assets/Root/Initialization/EntryParams.cs` |
| Экран загрузки (UI) | `Assets/Game/GameScenes/LoadingScreen.cs` |
| Метрики времени загрузки | `Assets/Root/Initialization/` (`LinearLoadingTime`, `PhLoadPhase`) |
| Состояния игры | `Assets/Game/GameScenes/GameStateService.cs` |
| Переход между локациями из игры | `Assets/Game/GameScenes/GameScenesService.cs` |
| Конфиги локаций | `Assets/Game/GameScenes/FirstSessionConfig.cs` |
| Лёгкий режим (Lite) | `Assets/Root/RootEntry_Lite.cs`, `RootGameRegister_Lite.cs` |
