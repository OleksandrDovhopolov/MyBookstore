# TransitionAnimationService — переход между сценами

**Project**: Research (Unity)  
**Date**: 2026-06-21

---

> ⚠️ **Reference-документ из проекта Research — НЕ описывает MyBookstore.**
> Здесь cover/reveal на **DOTween**; в MyBookstore DOTween **не используется**. У нас сейчас
> `ITransitionAnimationService` = `NoOpTransitionAnimationService` (хук), точки cover/reveal вызывает
> `GameFlowService`; реальная анимация будет сделана своим UI-кодом отдельной задачей. Документ —
> референс для будущей реализации. Актуально: [GameFlowLoop.md](../GameFlowLoop.md), [UI_SYSTEM.md](../UI_SYSTEM.md).

---

## Общая схема

```
Bootstrap Scene                         SampleScene (Game)
──────────────────────────────────────  ──────────────────────────────────────
Phase 4 → SceneTransitionOperation      MainSceneBootstrap.Start()
  1. PlayCoverAsync()  ──────────────►  [экран закрыт панелями]
  2. LoadSceneAsync("SampleScene")           ↓
                                        Show<GameplaySceneController>()
                                             ↓
                                        WaitUntil(IsWindowShown)
                                             ↓
                                        GameplayReadyGate.MarkReadyAsync()
                                          → RunInitializersAsync()   [батчинг фич]
                                          → PlayRevealAsync()  ◄─────── открыть экран
                                          → TrackEvent(AppStarted)
```

---

## TransitionAnimationService

**Файл**: `Assets/Game/UI/UIShared/Scripts/TransitionAnimation/TransitionAnimationService.cs`  
**Расположение**: MonoBehaviour-компонент на `UIManagerCanvas.prefab`  
**Регистрация**: Глобальный singleton через `BootstrapInstaller.cs:50-60`

```csharp
builder.Register<TransitionAnimationService>(resolver =>
{
    var uiManager = resolver.Resolve<UIManager>();
    var transitionService = uiManager.GetComponent<TransitionAnimationService>();
    if (transitionService == null)
        throw new MissingReferenceException(...);
    return transitionService;
}, Lifetime.Singleton);
```

Живёт на том же GameObject что и UIManager — DontDestroyOnLoad. ✓

### Настройки (из UIManagerCanvas.prefab)

| Параметр | Значение | Описание |
|----------|----------|----------|
| `_coverDurationSeconds` | `0.35` | Длительность закрытия |
| `_revealDurationSeconds` | `0.35` | Длительность открытия |
| `_coverEase` | `7` (InOutQuad) | Easing при закрытии |
| `_revealEase` | `7` (InOutQuad) | Easing при открытии |
| `_leftContainerRectTransform` | fileID 7194076735699871163 | Левая панель |
| `_rightContainerRectTransform` | fileID 7590687266943396647 | Правая панель со стартовой позицией (1500, 0) |

### Методы

**`PlayCoverAsync(CancellationToken ct)`** — закрывает экран:
- Обе панели анимируются в `Vector2.zero` (центр экрана)
- Start-позиции кэшируются при `Start()` или лениво при первом вызове
- Параллельная анимация через `DOTween.Sequence().Join(...)`

**`PlayRevealAsync(CancellationToken ct)`** — открывает экран:
- Обе панели возвращаются на сохранённые `_leftStartPosition` / `_rightStartPosition`

**`KillActiveSequence()`** — прерывает текущую анимацию (вызывается перед новой и в `OnDestroy`)

### Важная деталь: `AwaitTweenAsync`

```csharp
private static async UniTask AwaitTweenAsync(Tween tween, CancellationToken ct)
```

Корректная интеграция DOTween с CancellationToken:
- `tween.OnComplete(...)` → завершает `UniTaskCompletionSource`
- `tween.OnKill(...)` → если не completed → `TrySetCanceled()`
- `ct.Register(...)` → убивает tween при отмене токена
- Один `await tcs.Task.AttachExternalCancellation(ct)` ждёт итог

---

## Визуальная структура UIManagerCanvas

```
UIManagerCanvas (UIManager + TransitionAnimationService)
├── WindowsRoot          ← корень для всех окон (WindowFactoryDI._root)
├── Blocker              ← блокирует ввод во время анимаций
└── TransitionViewLayer  ← слой перехода (поверх всего)
    ├── LeftContainer    ← декоративные облака, скользит слева
    │   ├── Cloud, Cloud(1), Cloud(4) ...
    └── RightContainer   ← декоративные облака, скользит справа (startPos: x=1500)
        ├── Cloud, Cloud(1), Cloud(2) ...
```

---

## Точка 1: SceneTransitionOperation (Bootstrap → Game)

**Файл**: `Assets/Game/Core/Bootstrap/Loading/Operations/SceneTransitionOperation.cs`  
**Фаза**: Phase 4 (финализация), после WarmupOperation

```csharp
protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
{
    await UniTask.Yield(PlayerLoopTiming.Update, ct);

    await _transitionAnimationService.PlayCoverAsync(ct);  // ← закрыть панелями

    var asyncOperation = SceneManager.LoadSceneAsync(_sceneName);  // ← грузить сцену
    while (!asyncOperation.isDone)
    {
        ReportProgress(asyncOperation.progress >= 0.9f ? 1f : asyncOperation.progress);
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }
}
```

**Timeout**: 15 секунд. **RetryPolicy**: 1 попытка. **isCritical**: true.

Важно: сцена грузится через обычный `SceneManager.LoadSceneAsync` (single mode),  
а НЕ через VContainer-aware метод. Родительский scope подхватывается через VContainerSettings.

---

## Точка 2: GameplayReadyGate (открытие игровой сцены)

**Файл**: `Assets/Game/UI/UIShared/Scripts/GameplayReadyGate/GameplayReadyGate.cs`  
**Регистрация**: `GameInstaller.cs:74` — scene-scoped singleton

```csharp
builder.Register<IGameplayReadyGate, GameplayReadyGate>(Lifetime.Singleton);
```

`GameplayReadyGate` инжектируется с:
- `TransitionAnimationService` — глобальный (из GlobalLifetimeScope)
- `IEnumerable<IGameplayReadyInitializer>` — все зарегистрированные инициализаторы фич

```csharp
public async UniTask MarkReadyAsync(CancellationToken ct)
{
    await RunInitializersAsync(ct);              // ← инициализировать фичи
    await _transitionAnimationService.PlayRevealAsync(ct);  // ← убрать панели
    _analytics.TrackEvent(AppStarted);
    _isReady = true;
}
```

### Защита от повторного вызова

- `_isReady` — флаг, одноразовый
- `_isMarkingReady` — флаг конкурентного вызова → ждёт через `WaitUntilReadyAsync`

---

## Триггер: MainSceneBootstrap

**Файл**: `Assets/Game/Core/Bootstrap/MainScene/MainSceneBootstrap.cs`

```csharp
private async UniTaskVoid LoadGameplayAsync(CancellationToken ct)
{
    _uiManager.Show<GameplaySceneController>();
    await UniTask.WaitUntil(() => _uiManager.IsWindowShown<GameplaySceneController>(), ct);
    
    await _gameplayReadyGate.MarkReadyAsync(ct);  // ← запускает Reveal
}
```

`MainSceneBootstrap` инжектируется из `GameplayLifetimeScope` (child scope),  
поэтому имеет доступ к `IGameplayReadyGate`. Запускается в `Start()` — после всех `Awake()`.

---

## IGameplayReadyInitializer — расширяемый хук перед Reveal

Интерфейс: `Assets/Game/UI/UIShared/Scripts/GameplayReadyGate/IGameplayReadyInitializer.cs`

```csharp
public interface IGameplayReadyInitializer
{
    UniTask InitializeAsync(CancellationToken ct);
}
```

Выполняются **последовательно** перед `PlayRevealAsync`. Каждый должен завершиться прежде чем экран откроется.

### Текущие реализации

| Класс | Файл | Назначение |
|-------|------|-----------|
| `BattlePassGameplayReadyInitializer` | `Features/BattlePass/Runtime/State/...` | Синхронизирует BattlePass-состояние с сервером при старте |

**Регистрация в `BattlePassInstaller.cs:61`**:
```csharp
builder.Register<BattlePassGameplayReadyInitializer>(Lifetime.Singleton)
       .As<IGameplayReadyInitializer>();
```

VContainer автоматически собирает `IEnumerable<IGameplayReadyInitializer>` из всех зарегистрированных реализаций.

---

## Ожидание готовности в других сервисах

**OrchestratorRunner** (`Features/EventOrchestration/...`) ждёт `GameplayReadyGate` перед стартом:

```csharp
await _gameplayReadyGate.WaitUntilReadyAsync(ct);
await _orchestrator.InitializeAsync(ct);
// запуск tick-loop и refresh-loop
```

Паттерн: сервисы которые не должны стартовать до показа геймплея вызывают `WaitUntilReadyAsync`.

---

## Полная последовательность (Bootstrap → Игра видна)

```
[Game Start]
VContainerSettings → GlobalLifetimeScope (DontDestroyOnLoad)
  BootstrapInstaller: UIManager + TransitionAnimationService + WindowFactoryDI + ...

Bootstrap.unity → plain LifetimeScope (child of Global)
  → [Inject] Bootstrap.cs

Bootstrap.cs.Start() → RunBootstrapAsync()

Phase 1: UiManagerConfigureOperation
  → UIManager.Configurate(WindowFactoryDI)

Phase 2: AuthorizationGateOperation

Phase 3: RemoteConfigFetchOperation + SaveDataLoadOperation (parallel)

Phase 4:
  ├── WarmupOperation (placeholder, ~1 frame)
  └── SceneTransitionOperation
        ├── PlayCoverAsync()       [0.35s, InOutQuad] ← экран закрыт
        └── LoadSceneAsync("SampleScene")

SampleScene загружается (Bootstrap выгружается):
  GameplayLifetimeScope.Awake() → child of GlobalLifetimeScope
    GameInstaller:
      → register IGameplayReadyGate (GameplayReadyGate)
      → register BattlePassGameplayReadyInitializer as IGameplayReadyInitializer
      → RegisterBuildCallback: SetResolver(childResolver) на WindowFactoryDI
      → RegisterBuildCallback: InitializeHud()

MainSceneBootstrap.Start()
  → UIManager.Show<GameplaySceneController>()
  → WaitUntil(IsWindowShown<GameplaySceneController>)

GameplayReadyGate.MarkReadyAsync()
  → BattlePassGameplayReadyInitializer.InitializeAsync()  [server sync]
  → PlayRevealAsync()            [0.35s, InOutQuad] ← экран открыт
  → Analytics: AppStarted
  → _isReady = true

OrchestratorRunner: WaitUntilReadyAsync() unblocks → InitializeAsync()
```

---

## Как добавить новый хук перед открытием экрана

Реализовать `IGameplayReadyInitializer` и зарегистрировать в нужном installer:

```csharp
public sealed class MyFeatureReadyInitializer : IGameplayReadyInitializer
{
    private readonly IMyFeatureService _service;

    public MyFeatureReadyInitializer(IMyFeatureService service)
    {
        _service = service;
    }

    public async UniTask InitializeAsync(CancellationToken ct)
    {
        await _service.SyncInitialStateAsync(ct);
    }
}

// В installer:
builder.Register<MyFeatureReadyInitializer>(Lifetime.Singleton)
       .As<IGameplayReadyInitializer>();
```

VContainer автоматически добавит в список — `GameplayReadyGate` получит через `IEnumerable<IGameplayReadyInitializer>`.

---

## Потенциальные проблемы

| Сценарий | Риск |
|----------|------|
| `PlayCoverAsync` отменяется токеном | `KillActiveSequence` → tween.Kill → OnKill → TrySetCanceled — корректно |
| `PlayRevealAsync` вызывается без предшествующего Cover | Панели резко "прыгают" на стартовые позиции (нет guard) |
| Инициализатор бросает исключение | `BattlePassGameplayReadyInitializer` логирует и проглатывает (кроме OperationCanceledException) — **Reveal всё равно сыграет** |
| `MarkReadyAsync` вызван до `PlayCoverAsync` | `PlayRevealAsync` немедленно анимирует панели на стартовые позиции (могут быть уже там — no-op визуально) |
| `_leftContainerRectTransform == null` | `PlayCoverAsync`/`PlayRevealAsync` пропускают этот контейнер, не ломают |
