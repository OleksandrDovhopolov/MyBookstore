# DI Architecture: WindowFactoryDI & Scene Scope Resolution

**Project**: Research (Unity/VContainer)  
**Date**: 2026-06-21

---

> ⚠️ **Reference-документ из проекта Research — НЕ описывает MyBookstore.**
> В MyBookstore окна создаёт глобальный `AddressablesWindowFactory` (инжект глобальным resolver-ом), а
> `WindowFactoryDI` / `SetResolver` / `ClearSceneResolver` **не используются**. Для окон со scene-scoped
> зависимостями данные передаются через `WindowArgs` (как `RecommendationMinigameWindow`). Родительство
> scene-скопов при additive — через `LifetimeScope.EnqueueParent` в `GameFlowService`.
> Актуально: [GameFlowLoop.md](../GameFlowLoop.md), [UI_SYSTEM.md](../UI_SYSTEM.md), [BOOTSTRAP_AND_LOADING.md](../BOOTSTRAP_AND_LOADING.md).

---

## Иерархия контейнеров

```
VContainerSettings
└── RootLifetimeScope → GlobalLifetimeScope.prefab (DontDestroyOnLoad)
    └── _scriptableObjectInstallers: [BootstrapInstaller.asset]
        → WindowFactoryDI(IObjectResolver diContainer)  ← получает globalResolver
        → UIManager
        → SaveService, Analytics, TransitionAnimationService, ...

Bootstrap.unity (plain LifetimeScope, дочерний от Global)
→ Bootstrap.cs инжектируется из глобального контейнера ✓

SceneManager.LoadSceneAsync("SampleScene")

SampleScene → GameplayLifetimeScope.prefab (дочерний от Global, по VContainerSettings)
    └── _monoInstallers: [GameInstaller (из prefab), доп. installer (scene override)]
        → ICameraService, IInventoryService, IFishingService, ...

GameInstaller.RegisterBuildCallback:
    resolver.Resolve<WindowFactoryDI>().SetResolver(resolver)
    // resolver = child container = Global + Game services
```

---

## Ключевые файлы

| Файл | Роль |
|------|------|
| `Assets/Game/Core/Installers/Scripts/BootstrapInstaller.cs` | Регистрирует глобальные сервисы (WindowFactoryDI, UIManager, Save, Analytics) |
| `Assets/Game/Core/Installers/Scripts/GameInstaller.cs` | Регистрирует игровые сервисы (Camera, Inventory, Fishing, Crafting, HUD...) |
| `Assets/Game/UI/UIShared/Scripts/WindowFactoryDI/WindowFactoryDI.cs` | Создаёт и инжектирует контроллеры окон |
| `Assets/Game/Core/Installers/Scripts/Scopes/GlobalLifetimeScope.prefab` | Root scope (VContainerSettings), содержит BootstrapInstaller |
| `Assets/Game/Core/Installers/Scripts/Scopes/GameplayLifetimeScope.prefab` | Scene scope, содержит GameInstaller |
| `Assets/Game/Core/Installers/VContainerSettings.asset` | Указывает GlobalLifetimeScope как RootLifetimeScope |
| `Library/PackageCache/com.dovhopolov.uisystem@.../Core/UIStorage.cs` | Кэширует созданные окна по типу |

---

## Корень проблемы

`WindowFactoryDI` регистрируется в `BootstrapInstaller.cs:48`:
```csharp
builder.Register<WindowFactoryDI>(Lifetime.Singleton);
```

VContainer инжектирует в конструктор (`WindowFactoryDI.cs:19`):
```csharp
public WindowFactoryDI(UIManager uiManager, IObjectResolver diContainer)
```

`diContainer` = **глобальный резолвер** (только сервисы из BootstrapInstaller).  
Игровые сервисы (`IInventoryService`, `IFishingService` и т.д.) **недоступны**.

При инжекции контроллера окна (`WindowFactoryDI.cs:56`):
```csharp
_diContainer.Inject(controller);
```
→ ошибка **"no such registration"** если контроллер зависит от игровых сервисов.

---

## SetResolver — уже реализованное решение

`GameInstaller.cs:140-143`:
```csharp
builder.RegisterBuildCallback(resolver =>
{
    resolver.Resolve<WindowFactoryDI>().SetResolver(resolver);
});
```

`resolver` здесь — дочерний контейнер `GameplayLifetimeScope`.  
Он видит **и глобальные, и игровые сервисы** (VContainer parent-child resolution).  
После вызова `SetResolver` — инжекция контроллеров окон работает корректно.

`WindowFactoryDI.cs:25`:
```csharp
public void SetResolver(IObjectResolver resolver)
{
    _diContainer = resolver;
}
```

---

## Когда SetResolver не спасает (edge cases)

### 1. UIStorage кэширует окна по типу

`UIStorage.cs:68-96` — если окно было создано **до** вызова `SetResolver`  
(например, при открытии SampleScene напрямую через BootstrapPlayMode без полного flow),  
оно кэшируется с неверным резолвером и **не пересоздаётся** при повторном `Show<T>`.

### 2. Другие сцены без GameInstaller

Если сцена (UIScene, TileEditor и т.д.) не содержит `GameplayLifetimeScope` с `GameInstaller`,  
`SetResolver` не вызывается. `_diContainer` остаётся глобальным.  
Любое окно с зависимостью на игровые сервисы → **ошибка**.

### 3. Dispose после выгрузки сцены

При выгрузке игровой сцены дочерний контейнер `Dispose`-ится.  
`WindowFactoryDI` всё ещё держит ссылку на него.  
Следующее создание окна в новой сцене → исключение на disposed resolver.

---

## Диагностика

Добавить лог в `GameInstaller.cs` чтобы убедиться что SetResolver вызывается:

```csharp
builder.RegisterBuildCallback(resolver =>
{
    var factory = resolver.Resolve<WindowFactoryDI>();
    factory.SetResolver(resolver);
    Debug.Log("[GameInstaller] SetResolver called with child resolver");
});
```

---

## Рекомендуемое исправление

Хранить глобальный резолвер как неизменяемый fallback и сбрасывать сцен-резолвер при выгрузке.

### WindowFactoryDI.cs

```csharp
public class WindowFactoryDI : WindowFactoryBase
{
    private readonly UIManager _uiManager;
    private readonly IObjectResolver _globalResolver; // неизменяемый
    private IObjectResolver _sceneResolver;           // меняется через SetResolver

    private IObjectResolver ActiveResolver => _sceneResolver ?? _globalResolver;

    public WindowFactoryDI(UIManager uiManager, IObjectResolver diContainer)
    {
        _uiManager = uiManager;
        _globalResolver = diContainer;
    }

    public void SetResolver(IObjectResolver resolver) => _sceneResolver = resolver;
    public void ClearSceneResolver() => _sceneResolver = null;

    protected override T Create<T>(WindowView windowPrefab, WindowAttribute windowAttribute)
    {
        windowPrefab.gameObject.SetActive(false);
        var newGo = ActiveResolver.Instantiate(windowPrefab, _root);
        windowPrefab.gameObject.SetActive(true);

        var controller = Activator.CreateInstance<T>();
        ActiveResolver.Inject(controller);
        controller.Configurate(newGo, _uiManager, windowAttribute);

        return controller;
    }
}
```

### GameplayLifetimeScope.cs

Сброс при выгрузке сцены:

```csharp
public class GameplayLifetimeScope : BaseLifetimeScope
{
    protected override void InstallBindings(IContainerBuilder builder) { }

    protected override void OnDestroy()
    {
        // Сбросить сцен-резолвер при выгрузке, чтобы не держать disposed контейнер
        if (Container != null && Container.TryResolve<WindowFactoryDI>(out var factory))
        {
            factory.ClearSceneResolver();
        }
        base.OnDestroy();
    }
}
```

---

## Итоговая таблица

| Вопрос | Статус |
|--------|--------|
| WindowFactoryDI видит только Global до SetResolver? | **ДА** — confirmed |
| SetResolver реализован? | **ДА** — `GameInstaller.cs:140` |
| SetResolver вызывается в SampleScene? | **Нужно проверить** — добавь лог |
| Работает ли для SampleScene при полном flow? | **ДА** — если SetResolver срабатывает до первого `Show<T>` |
| Работает ли при смене/выгрузке сцен? | **НЕТ** — нужен `ClearSceneResolver` в `OnDestroy` |
| Работает ли в сценах без GameInstaller? | **НЕТ** — `_diContainer` остаётся globalResolver |

---

## Связанные вопросы

- `UIStorage.ClearCache()` — нужно ли вызывать при смене сцен?
- Нужно ли добавлять аналог GameInstaller в UIScene/TileEditor?
- Можно ли вынести часть игровых сервисов в Global scope чтобы избежать проблемы?
