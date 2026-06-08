# DI System Improvements

Список улучшений для системы биндингов на базе `BaseLifetimeScope` / VContainer.
Улучшения **не применены** — система перенесена в исходном виде. Этот файл описывает следующую итерацию.

---

## 1. Null-safety в итерации инсталлеров

**Проблема.** `BaseLifetimeScope` итерирует `_monoInstallers` и `_scriptableObjectInstallers` без проверки на `null`. Если элемент случайно удалён из инспектора или не задан — выброс `NullReferenceException` без диагностического сообщения.

**Решение.**

```csharp
private void InstallMonoBehaviours(IContainerBuilder builder)
{
    foreach (var installer in _monoInstallers)
    {
        if (installer == null)
        {
            Debug.LogError($"[{GetType().Name}] Null MonoInstaller in list. Check inspector.", this);
            continue;
        }
        installer.InstallBindings(builder);
    }
}
```

Аналогично для `InstallScriptableObjects`.

---

## 2. `InstallBindings` — из abstract в virtual

**Проблема.** `InstallBindings` объявлен `abstract`, что обязывает каждый дочерний scope реализовывать метод. На практике оба scope (`GlobalLifetimeScope`, `GameplayLifetimeScope`) оставляют его пустым — вся работа идёт через инсталлеры из инспектора. Пустые `override` создают ложный контракт.

**Решение.** Сделать метод `virtual` с пустой реализацией по умолчанию.

```csharp
// Вместо: protected abstract void InstallBindings(IContainerBuilder builder);
protected virtual void InstallBindings(IContainerBuilder builder) { }
```

Это не ломает существующий код — наследники могут по-прежнему переопределять метод, но не обязаны.

---

## 3. Явная ссылка parent scope

**Проблема.** `GameplayLifetimeScope` не имеет явной ссылки на `GlobalLifetimeScope`. Родительский скоуп находится через fallback на `VContainerSettings.RootLifetimeScope`. Если в настройках будет смена root или появится промежуточный scope — связь молча сломается.

**Решение.** Явно указывать parent через `parentReference` в инспекторе (`GameplayLifetimeScope` → поле `Parent`) или через override `FindParent()`:

```csharp
public class GameplayLifetimeScope : BaseLifetimeScope
{
    protected override LifetimeScope FindParent() =>
        FindObjectOfType<GlobalLifetimeScope>();
}
```

---

## 4. Разбить `GameInstaller` на несколько `MonoInstaller`

**Проблема.** `GameInstaller` — God Installer: регистрирует Camera, Location, HUD, WebClient, ResourceManager, Inventory, Fishing, Crafting, Rewards, Orchestration и т.д. Это нарушает SRP, усложняет merge-конфликты и делает installer хрупким.

**Решение.** Разбить на независимые компоненты, каждый из которых назначается в `_monoInstallers` через инспектор:

```
GameplayLifetimeScope._monoInstallers:
  [ ] NetworkInstaller      — WebClient, AuthTokenProvider
  [ ] ResourcesInstaller    — ResourceManager, currencies
  [ ] InventoryInstaller    — InventoryService
  [ ] ShopInstaller         — ShopService, offers
  [ ] FeaturesInstaller     — BookSell, Quest, RewardDrop
  [ ] IapInstaller          — IAP platform adapter
```

Порядок в списке `_monoInstallers` определяет порядок регистрации — документировать зависимости явно.

---

## 5. Общий интерфейс `IInstaller` для тестируемости

**Проблема.** `MonoInstaller` и `ScriptableObjectInstaller` не имеют общего интерфейса. Нельзя писать юнит-тесты на инсталлеры без Unity-окружения и сложно создавать mock-инсталлеры.

**Решение.** Ввести интерфейс:

```csharp
public interface IInstaller
{
    void InstallBindings(IContainerBuilder builder);
}

public abstract class MonoInstaller : MonoBehaviour, IInstaller { ... }
public abstract class ScriptableObjectInstaller : ScriptableObject, IInstaller { ... }
```

Это позволит в тестах создавать `FakeInstaller : IInstaller` и передавать его без Unity runtime.

---

## 6. Порядок `RegisterBuildCallback` — потенциальная неочевидность

**Проблема.** `BaseLifetimeScope.Configure` регистрирует `OnBuildCallback` в конце:

```
1. InstallBindings(builder)
2. InstallScriptableObjects(builder)   ← инсталлеры могут сами вызывать RegisterBuildCallback
3. InstallMonoBehaviours(builder)      ← аналогично
4. builder.RegisterBuildCallback(OnBuildCallback)   ← всегда последний
```

Если инсталлер регистрирует BuildCallback, он выполнится **до** `OnBuildCallback` scope. Это не ошибка, но неочевидно при отладке инициализации.

**Решение.** Добавить `PreBuildCallback` (вызывается до инсталлеров) для scope-уровневой инициализации, которая должна быть первой:

```csharp
protected sealed override void Configure(IContainerBuilder builder)
{
    builder.RegisterBuildCallback(PreBuildCallback);   // до всех
    InstallBindings(builder);
    InstallScriptableObjects(builder);
    InstallMonoBehaviours(builder);
    builder.RegisterBuildCallback(OnBuildCallback);    // после всех
}

protected virtual void PreBuildCallback(IObjectResolver resolver) { }
protected virtual void OnBuildCallback(IObjectResolver resolver) { }
```

---

## 7. Диагностика и валидация

**Проблема.** Нет инструмента для проверки корректности настройки сцены: пустые листы инсталлеров, незаданные обязательные ссылки — всё обнаруживается только в рантайме.

**Решение.** Добавить `#if UNITY_EDITOR` валидацию в `OnValidate`:

```csharp
#if UNITY_EDITOR
private void OnValidate()
{
    if (_monoInstallers != null)
        foreach (var i in _monoInstallers)
            if (i == null) Debug.LogWarning($"[{name}] MonoInstaller list has null entry", this);

    if (_scriptableObjectInstallers != null)
        foreach (var i in _scriptableObjectInstallers)
            if (i == null) Debug.LogWarning($"[{name}] ScriptableObjectInstaller list has null entry", this);
}
#endif
```

---

## 8. asmdef изоляция для фич

**Проблема.** Все feature bindings сейчас в одной сборке `Game.Bootstrap`. Это создаёт implicit coupling — изменение в одной фиче перекомпилирует весь модуль.

**Решение.** Каждая фича получает свой `asmdef`:

```
Assets/Game/Features/BookSell/Game.Feature.BookSell.asmdef
Assets/Game/Features/Shop/Game.Feature.Shop.asmdef
...
```

`Game.Bootstrap.asmdef` ссылается на feature assemblies через `references`. Это ускоряет итерацию компиляции и явно документирует межмодульные зависимости.

---

## Приоритеты

| # | Улучшение | Усилие | Влияние |
|---|-----------|--------|---------|
| 1 | Null-safety в итерации | Низкое | Высокое (production safety) |
| 2 | virtual InstallBindings | Минимальное | Среднее (чистота кода) |
| 3 | Явный parent scope | Низкое | Высокое (устойчивость) |
| 4 | Разбить God Installer | Среднее | Высокое (maintainability) |
| 5 | IInstaller интерфейс | Низкое | Среднее (testability) |
| 6 | Pre/OnBuildCallback | Низкое | Низкое (edge cases) |
| 7 | Editor валидация | Низкое | Среднее (DX) |
| 8 | asmdef изоляция | Среднее | Среднее (compile time) |
