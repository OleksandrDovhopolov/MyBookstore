# Переход Bootstrap → GameplayScene

## Содержание

1. [Обзор флоу](#1-обзор-флоу)
2. [Bootstrap.cs — точка входа](#2-bootstrapcs--точка-входа)
3. [LoadingOrchestrator — движок фаз](#3-loadingorchestrator--движок-фаз)
4. [Фазы загрузки](#4-фазы-загрузки)
5. [SceneTransitionOperation — переход в сцену](#5-scenetransitionoperation--переход-в-сцену)
6. [MainSceneBootstrap — инициализация игровой сцены](#6-mainscenebootstrap--инициализация-игровой-сцены)
7. [GameplayReadyGate — главный барьер готовности](#7-gameplayreadygate--главный-барьер-готовности)
8. [IGameplayReadyInitializer — паттерн расширения](#8-igameplayreadyinitializer--паттерн-расширения)
9. [Потребители IGameplayReadyGate](#9-потребители-igameplayreadygate)
10. [Полная диаграмма](#10-полная-диаграмма)
11. [Справка: параметры операций](#11-справка-параметры-операций)

---

## 1. Обзор флоу

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

---

## 2. Bootstrap.cs — точка входа

`Assets/Game/Core/Bootstrap/Bootstrap.cs`

MonoBehaviour в Bootstrap-сцене. Все зависимости инжектируются через `[Inject]`.

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

**Retry-механизм:** при провале критичной операции пользователь видит "Check your internet connection and try again." и кнопку retry. Оркестратор резюмирует с фазы, где была ошибка.

---

## 3. LoadingOrchestrator — движок фаз

`Assets/Game/Core/Bootstrap/Loading/LoadingOrchestrator.cs`

Сингл-класс без наследования. Координирует выполнение фаз и агрегирует прогресс.

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

---

## 4. Фазы загрузки

Фазы строятся в `Bootstrap.BuildPhases()`. Каждая `LoadingPhase` содержит одну или несколько `LoadingGroup`.

### Phase 1 — `phase_technical_init` (Sequential)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `UiManagerConfigureOperation` | `ui_manager_configure` | ✅ | 0.1 | 5s | 1 |
| `FirebaseDependenciesOperation` | `firebase_dependencies` | ❌ | 0.1 | 8s | 2×0.5s |
| `AddressablesUpdateOperation` | `addressables_update` | ✅ | 0.3 | 20s | 2×1s |

- **UiManager**: синхронная операция, конфигурирует `UIManager.Configurate(windowFactory, eventHandler)`
- **Firebase**: `RemoteConfigLoader.EnsureDependenciesAsync()` — проверка зависимостей SDK. Не критична: игра запустится без Firebase
- **Addressables**: `AddressablesUpdater.CheckAndUpdateAsync()` — проверяет и скачивает обновлённый каталог с CDN. Критична: без актуального каталога ассеты могут не найтись

### Phase 2 — `phase_authorization` (Sequential)

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

Таймаут 5 минут — достаточно для ручного логина. Но операция **критична**: без авторизации нет токена для серверных запросов.

### Phase 3 — `phase_data_load` (Parallel)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `RemoteConfigFetchOperation` | `remote_config_fetch` | ❌ | 0.2 | 10s | 2×0.5s |
| `SaveDataLoadOperation` | `save_data_load` | ✅ | 0.3 | 10s | 2×0.3s |

- **RemoteConfig**: `RemoteConfigLoader.FetchAndActivateAsync()` — фетч Firebase RC. Не критична: игра запустится на дефолтных конфигах
- **SaveData**: `saveService.LoadAllAsync()` — загружает сохранение с диска/сервера. Критична: без сохранения нельзя войти в прогресс игрока

Обе операции выполняются параллельно — экономия времени при хорошей сети.

### Phase 4 — `phase_finalization` (Sequential)

| Операция | ID | Critical | Weight | Timeout | Retry |
|---|---|---|---|---|---|
| `WarmupOperation` | `warmup` | ❌ | 0.1 | 5s | 1 |
| `SceneTransitionOperation` | `scene_transition` | ✅ | 0.15 | 15s | 1 |

- **Warmup**: placeholder, можно расширить прогревом шейдеров/префабов
- **SceneTransition**: ключевая операция перехода (см. раздел 5)

---

## 5. SceneTransitionOperation — переход в сцену

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

---

## 6. MainSceneBootstrap — инициализация игровой сцены

`Assets/Game/Core/Bootstrap/MainScene/MainSceneBootstrap.cs`

MonoBehaviour в GameplayScene. VContainer инжектирует зависимости.

**Инжектируемые зависимости:**
- `UIManager` — показывает окна
- `IGameplayReadyGate` — барьер готовности

**`LoadGameplayAsync()`:**

```csharp
_uiManager.Show<GameplaySceneController>();          // 1. открыть главное UI окно геймплея
await UniTask.WaitUntil(IsWindowShown<GameplaySceneController>);  // 2. ждать пока UI готово
await _gameplayReadyGate.MarkReadyAsync(ct);         // 3. сигнализировать о готовности
```

**GameplaySceneController** — главное окно игровой сцены. Пока оно не показалось, сцена не считается готовой. Это гарантирует, что UI полностью инициализировано до открытия экрана.

---

## 7. GameplayReadyGate — главный барьер готовности

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

Внутри используется `UniTaskCompletionSource` — стандартный паттерн "one-shot event" в UniTask.

**Порядок событий при MarkReady:**
```
[все инициализаторы завершены]
    → PlayRevealAsync()      ← СНАЧАЛА анимация (экран открывается)
    → TrackEvent(AppStarted) ← ПОТОМ аналитика
    → TrySetResult()         ← ПОТОМ разблокировать ожидателей
```

---

## 8. IGameplayReadyInitializer — паттерн расширения

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

**Текущие реализации:**

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

---

## 9. Потребители IGameplayReadyGate

Системы, которым нужно дождаться полной готовности сцены перед стартом, инжектируют `IGameplayReadyGate` и вызывают `WaitUntilReadyAsync()`.

### OrchestratorRunner

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

---

## 10. Полная диаграмма

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

---

## 11. Справка: параметры операций

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
