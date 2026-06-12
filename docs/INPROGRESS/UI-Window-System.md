# UI Window System — Архитектура

## Содержание

1. [Обзор слоёв](#1-обзор-слоёв)
2. [IWindowsService — публичный API](#2-iwindowsservice--публичный-api)
3. [IWindowsController — центральный контроллер](#3-iwindowscontroller--центральный-контроллер)
4. [Очереди окон (UIQueue)](#4-очереди-окон-uiqueue)
5. [Слои отображения (WindowShowType)](#5-слои-отображения-windowshowtype)
6. [Props-система](#6-props-система)
7. [Виджет и его жизненный цикл](#7-виджет-и-его-жизненный-цикл)
8. [WidgetLifecycleController — создание и управление экземпляром](#8-widgetlifecyclecontroller--создание-и-управление-экземпляром)
9. [Сигнальная шина](#9-сигнальная-шина)
10. [Команды (async-паттерн)](#10-команды-async-паттерн)
11. [Сортировка (UISortingController)](#11-сортировка-uisortingcontroller)
12. [MVVM-вариант окна](#12-mvvm-вариант-окна)
13. [Кеширование виджетов](#13-кеширование-виджетов)
14. [Оценка переносимости в другой проект](#14-оценка-переносимости-в-другой-проект)

---

## 1. Обзор слоёв

```
┌─────────────────────────────────────────────────┐
│  IWindowsService  (game-specific facade)        │  ← используют фичи игры
├─────────────────────────────────────────────────┤
│  IUIService       (master UI service)           │  ← HUD, tooltips, fader,
│                                                 │    loading, notifications
├─────────────────────────────────────────────────┤
│  IWindowsController                             │  ← маршрутизация по слоям,
│                                                 │    управление сортировкой
├────────────────┬──────────────┬─────────────────┤
│  MainQueue     │  SystemQueue │  DevelopQueue   │  ← три независимых очереди
│  IWindowsQueue │  IWindowsQueue  IWindowsQueue  │    (UIQueue<IWindowProps>)
├────────────────┴──────────────┴─────────────────┤
│  AdditionalWindowContainer[]                    │  ← модальные окна (не в очереди)
├─────────────────────────────────────────────────┤
│  WidgetLifecycleController<TProps>              │  ← создание / show / hide / close
├─────────────────────────────────────────────────┤
│  IWidgetFactory  (Addressables + cache)         │  ← загрузка префаба, пул
├─────────────────────────────────────────────────┤
│  UIWidget<TProps> / Window<TProps>              │  ← MonoBehaviour, lifecycle hooks
└─────────────────────────────────────────────────┘
```

**Ключевой принцип:** Props — это ScriptableObject-конфигурация окна. Он передаётся через все слои без изменений и является единственным «ключом» к виджету.

---

## 2. IWindowsService — публичный API

**Файл:** `Assets/CoreApi/UI/Controllers/IWindowsService.cs`  
**Namespace:** `Wdk.Api`

Это *game-specific* фасад поверх `IUIService`. Предназначен для использования в бизнес-логике игры.

### Что умеет

| Группа | Методы |
|---|---|
| Универсальное открытие | `OpenAndWaitForCloseAsync(IWindowProps)`, `OpenAndWaitForClose(IWindowProps, Action)` |
| Управление | `GetCurrent()`, `Close()`, `CloseAll()`, `CloseAllExcept()`, `CloseRecursively()` |
| Ошибки / сеть | `ShowNoInternetWindow`, `ShowServerConnectionErrorWindow`, `ShowServerErrorWindow`, `ShowLogingErrorWindow`, … |
| Покупки / банк | `ShowBank`, `ShowBankAsync`, `ShowBankTransactionErrorWindow`, … |
| Геймплей | `ShowBattlePassWindow`, `ShowPvpLobby`, `ShowRiftWindow`, `ShowTowerWindow`, … |
| Бой | `ShowDefeatWindowAsync`, `ShowVictoryWindowAsync` |
| Системное | `ShowServerMaintenanceWindow`, `ShowUpdateGameWindowAsync`, `ShowMultipleLoginWindow`, … |

### Характер интерфейса

`IWindowsService` **не является обобщённым**. Каждый тип окна — отдельный метод со своими параметрами. Это сознательный трейдоф: удобство вызова vs. засорённость интерфейса. Есть комментарий в коде от автора, признающего этот факт:

```csharp
// Я не понимаю, зачем это всё пропихивается через этот интерфейс вместо использования пропсов
// (как во ВСЕХ остальных местах, кроме боя)?
```

Это значит, что **этот интерфейс — проблемная точка дизайна**. Бо́льшая часть системы ниже (контроллер, очередь, виджеты) — хорошо абстрагирована. `IWindowsService` — нет.

---

## 3. IWindowsController — центральный контроллер

**Файл:** `Assets/CoreApi/UI/Controllers/IWindowsController.cs`  
**Реализация:** `Assets/Core/UI/Scripts/Windows/WindowsController.cs`

Принимает `IWindowProps` и маршрутизирует в нужный слой.

```csharp
public interface IWindowsController : IServiceWithInitialization {
    void Show(IWindowProps props);
    void Close(IWindowProps props);
    void CloseAll();
    void CloseAllExcept(IWindowProps props);
    void CloseWindowsWithParents(IWindowProps props);

    IWindow GetFocusedWindow();
    IWindowProps GetCurrentWindowProps();
    IWindow GetLastQueueWindow();

    bool TryHandleExitRequest();
    void TryHandleOkRequest();

    bool HasPropsInQueue<T>();

    // Sorting
    int SortingLayer { get; }
    int SortingOrderStep { get; }
    int SortingOrderMain { get; }
    int SortingOrderAdditional { get; }
}
```

### Как работает маршрутизация

`WindowsController.Show()` смотрит на `props.ShowType` и направляет окно в нужный поток:

```csharp
switch (props.ShowType) {
    case WindowShowType.Main:       ShowMainWindow(props);       break;
    case WindowShowType.Additional: ShowAdditionalWindow(props); break;
    case WindowShowType.System:     ShowSystemWindow(props);     break;
    case WindowShowType.Develop:    ShowDevelopWindow(props);    break;
}
```

### Получение фокусного окна

Приоритет: `SystemQueue` > `AdditionalWindow` поверх MainQueue > `MainQueue`.

```csharp
public IWindow GetFocusedWindow() {
    var systemWindow = SystemQueue.GetCurrentWindow();
    if (systemWindow != null) return systemWindow;

    var mainWindow = MainQueue.GetCurrentWindow();
    if (mainWindow == null) return GetFocusedAdditionalWindow();

    var lastAdditional = GetAdditionalWindowsFor(mainWindow.WindowProps)
        ?.LastOrDefault(c => c.Widget.IsShown())?.Window;
    return lastAdditional ?? mainWindow;
}
```

### Управление иерархией Parent/Child

`CloseWindowsWithParents(props)` — рекурсивно закрывает цепочку parent-окон:

```csharp
while (props != null) {
    SetIsEnabledAutoShowLastElement(props.ParentWindowProps == null);
    Close(props);
    props = props.ParentWindowProps;
}
```

---

## 4. Очереди окон (UIQueue)

**Файл:** `Assets/Core/UI/Scripts/Queue/UIQueue.cs`  
**Реализации:** `WindowsQueue`, `UINotificationsQueue`, и др.

Очередь — центральный механизм управления стеком. Хранит `LinkedList<IQueueElement<TProps>>`.

### Три способа добавить элемент

| Метод | Поведение |
|---|---|
| `PushBack(props)` | Добавить в начало (высокий приоритет). Текущее окно остаётся. |
| `PushFront(props)` | Добавить в конец (низкий приоритет). Будет показан после всех. |
| `AddImmediately(props)` | Показать прямо сейчас. Предыдущее — спрятать. |

`AddImmediately` — основной для Windows: окно появляется мгновенно, предыдущее уходит в `Hidden`.

### Автоматическое восстановление предыдущего окна

При закрытии элемента (через `WidgetOnCloseSignal`) очередь автоматически вызывает `Show(LastElement)` — предыдущее окно возвращается. Это поведение можно временно отключить через `SetIsEnabledAutoShowLastElement(false)` (используется при пакетном закрытии).

### SingleInstanceInQueue

Если `props.SingleInstanceInQueue == true`, перед добавлением из очереди удаляются все элементы того же типа:

```csharp
if (props.SingleInstanceInQueue) {
    RemoveWidgetsOfType(props.GetType());
}
```

---

## 5. Слои отображения (WindowShowType)

**Файл:** `Assets/CoreApi/UI/Queue/WindowShowType.cs`

```csharp
public enum WindowShowType {
    Main,       // стандартные игровые окна
    Additional, // модальные окна поверх Main
    System,     // критические системные окна (ошибки, обновления)
    Develop     // дебаг-окна, читы
}
```

### Additional-окна — особый случай

Additional-окна **не попадают в очередь**. Они управляются через отдельный список `List<AdditionalWindowContainer>`.

Каждый `AdditionalWindowContainer` хранит ссылку на родительское Main-окно (`ParentWindow`). При закрытии Main-окна все его Additional-окна закрываются автоматически.

Если нет активного Main-окна, Additional автоматически переклассифицируется в Main:

```csharp
private void ShowAdditionalWindow(IWindowProps props) {
    var parentWindow = MainQueue.GetCurrentWindow() ?? GetFocusedAdditionalWindow();
    if (parentWindow == null) {
        ShowMainWindow(props.AsMain()); // автоматическая деградация
        return;
    }
    // ...
}
```

---

## 6. Props-система

**Файлы:**  
- `Assets/CoreApi/UI/Props/IWindowProps.cs`  
- `Assets/Core/UI/Scripts/Windows/WindowProps.cs`

Props — это **ScriptableObject**, конфигурирующий конкретный тип окна. Он одновременно:
- хранит настройки отображения (сортировка, анимации, HUD)
- является «ключом» для WidgetFactory (по нему грузится префаб)
- хранит ссылку на parent/child в цепочке

### IWindowProps

```csharp
public interface IWindowProps : IUIQueueProps {
    bool ResetGesturesOnOpen { get; }
    bool PreventDropAnimation { get; }
    bool StopDropAnimationOnClose { get; }
    WindowShowType ShowType { get; }
    bool ShowOnHUD { get; }
    bool HideOnHUDWithAnimation { get; }
    IUISortingData SortingData { get; }
    bool LockInputOnLoading { get; }

    IWindowProps ParentWindowProps { get; }
    IWindowProps ChildWindowProps { get; }

    // Fluent API
    IWindowProps AsAdditional();
    IWindowProps AsMain();
    IWindowProps AsSystem();
    IWindowProps WithName(string newName);

    List<IWindowProps> GetExamples(); // для дебаг-читов
}
```

### Fluent API смены слоя

Props поддерживают fluent-смену слоя без создания нового объекта:

```csharp
windowsService.Open(myProps.AsAdditional());
windowsService.Open(myProps.AsSystem());
```

### Подокна (Sub-Windows)

Окно может открыть подокно через `ShowSubWindow(childProps)`. При этом устанавливается двусторонняя ссылка parent↔child. Метод `CloseSubWindows(includeCurrent)` рекурсивно закрывает всю цепочку.

### GetExamples() — для редактора/читов

Каждый конкретный Props обязан реализовать `GetExamples()`. Это список вариантов, с которыми можно открыть окно прямо из редактора (кнопка **Show window** в инспекторе Unity).

---

## 7. Виджет и его жизненный цикл

### IWidget

**Файл:** `Assets/CoreApi/UI/Widget/IWidget.cs`

```csharp
public interface IWidget : IVisibility {
    GameObject GameObject { get; }
    WidgetStatus Status { get; }
    bool IsStable { get; }  // true если не анимируется
    IUIProps RawProps { get; }

    UniTask Construct();            // аналог Awake — вызывается один раз
    UniTask InjectAsync(IUIProps);  // пробрасывание зависимостей, вызывается при каждом Show из кеша
    void Close();
    void CloseImmediate();
    void Dispose();                 // очистка перед уничтожением / возвратом в пул
}
```

### UIWidget\<TProps\> — базовая реализация

**Файл:** `Assets/Core/UI/Scripts/Base/Widget/Old/UIWidget.cs`

MonoBehaviour. Управляет `Canvas`, `GraphicRaycaster`, `UIVisibilityController`.

#### Статусная машина

```
Initializing → Initialized → Shown ←→ Hidden → Closing → Closed
```

#### Переопределяемые хуки (ShouldBeEmpty по умолчанию)

| Хук | Когда |
|---|---|
| `OnConstructAsync()` | Один раз после инстанцирования (до props) |
| `OnConstructed()` | После `OnConstructAsync` |
| `OnPropsApplied()` | При получении props (каждый раз) |
| `OnAfterPropsAppliedAsync()` | После `OnPropsApplied` (асинхронная инициализация) |
| `BeforeShow()` | Перед началом анимации появления |
| `AfterShow()` | После завершения анимации появления |
| `BeforeHide()` | Перед началом анимации скрытия |
| `AfterHide()` | После завершения анимации скрытия |
| `OnClosed()` | После скрытия, до `WidgetOnCloseSignal` |
| `OnDisposed()` | Перед пулом/уничтожением |

#### BlockLayer во время анимации

Если `NeedBlockUnderWidgetWhileAnimating = true` (как в `Window<TProps>`), система автоматически накладывает блокирующий слой на время анимации появления/скрытия:

```csharp
_blockLayer = this switch {
    { NeedBlockUnderWidgetWhileAnimating: true, Props: { NeedBlockWidgetWhileAnimating: true } } 
        => UIService.BlockOverFocusedWindow(Props.Name),
    { NeedBlockUnderWidgetWhileAnimating: true } 
        => UIService.BlockUnderWindows(Props.Name),
    _ => null
};
```

### IWindow : IWidget

**Файл:** `Assets/CoreApi/UI/Widget/IWindow.cs`

```csharp
public interface IWindow : IWidget {
    IWindowProps WindowProps { get; }
    bool TryHandleExitRequest(); // например, нажатие Back
    void TryHandleOkRequest();   // например, нажатие Enter
}
```

### Window\<TProps\> — базовый класс окна

**Файл:** `Assets/Core/UI/Scripts/Windows/Window.cs`

Расширяет `UIWidget<TProps>`, реализует `IWindow`. Добавляет:
- Применение сортировки из `Props.SortingData` в `BeforeShow()`
- `ResetGestures` при показе (если в props)
- `CloseAllTooltips` при скрытии
- Методы `ShowSubWindow` / `ShowSubWindowAsync` / `CloseSubWindows`
- Дефолтный `TryHandleExitRequest()` → `Close()`

---

## 8. WidgetLifecycleController — создание и управление экземпляром

**Файл:** `Assets/Core/UI/Scripts/Base/Widget/WidgetLifecycleController.cs`

Посредник между очередью и конкретным виджетом. Отвечает за асинхронное создание и буферизацию команд до готовности объекта.

```csharp
internal class WidgetLifecycleController<TProps> {
    public async UniTask<bool> InstantiateAsync() { ... }
    public void Show()  { /* если не создан — ставит в очередь */ }
    public void Hide()  { /* если не создан — ставит в очередь */ }
    public void Close() { /* если не создан — ставит в очередь */ }
}
```

### Deferred execution

Если виджет ещё не создан (Addressables не вернул результат), команды `Show/Hide/Close` сохраняются в `_onInstantiate` и выполняются сразу после завершения `InstantiateAsync`:

```csharp
public void Show() {
    if (!_isInstantiated) {
        _onInstantiate = Show; // отложить
        return;
    }
    _widget?.SetVisibilityState(VisibilityState.Appearing);
}
```

---

## 9. Сигнальная шина

**Файл:** `Assets/CoreApi/UI/UISignals.cs`

Все взаимодействия между слоями — через сигналы, не прямые вызовы.

| Сигнал | Когда |
|---|---|
| `WidgetInstantiateSignal` | Начало асинхронного создания виджета |
| `WidgetOnBeforeShowSignal` | До начала анимации появления |
| `WidgetOnAfterShowSignal` | После завершения анимации появления |
| `WidgetOnBeforeHideSignal` | До начала анимации скрытия |
| `WidgetOnAfterHideSignal` | После завершения анимации скрытия |
| `WidgetOnCloseSignal` | Виджет полностью закрыт — очередь реагирует и убирает элемент |
| `WidgetStabilityChangedSignal` | Смена состояния анимации (IsStable изменился) |
| `QueueAddWidgetSignal` | Элемент добавлен в очередь |
| `QueueShowWidgetSignal` | Элемент стал текущим в очереди |
| `QueueHideWidgetSignal` | Элемент скрыт очередью |
| `QueueRemoveWidgetSignal` | Элемент удалён из очереди |

`UIQueue` подписывается на `WidgetOnCloseSignal` для удаления закрытого элемента и автоматического показа следующего. `WindowsController` подписывается для очистки `_additionalWindows`.

---

## 10. Команды (async-паттерн)

**Файл:** `Assets/Core/UI/Scripts/Commands/OpenAndWaitClosingWindowCommand.cs`

Для сценариев «показать окно и ждать его закрытия» (модальный флоу) существует командная система.

```csharp
public class OpenAndWaitClosingWindowCommand : AbstractCommand {
    public OpenAndWaitClosingWindowCommand(IWindowProps props) {
        // Компоновка из двух команд:
        // 1. WaitForWindowBeforeShowCommand — ждёт WidgetOnBeforeShowSignal
        // 2. WaitForClosingWindowCommand   — ждёт WidgetOnCloseSignal
        _queueCommand = CommandsFactory.GetQueueCommand(waitBefore, waitClose);
    }

    protected override void ExecInternal() {
        TryExecInternalAsync();
        UIService.ShowWindow(_props); // открываем окно
    }

    protected override async UniTask ExecInternalAsync() {
        await _queueCommand.ExecuteAsync(); // ждём закрытия
        NotifyComplete();
    }
}
```

Использование:

```csharp
await new OpenAndWaitClosingWindowCommand(props).ExecuteAsync();
// продолжение после закрытия окна
```

---

## 11. Сортировка (UISortingController)

**Файл:** `Assets/Core/UI/Scripts/Sorting/UISortingController.cs`

Каждое окно при появлении получает `SortingData` (layerId + order), которые назначает `WindowsController`:

```csharp
private void FillSorting(IWindowProps props, int defaultOrder, int currentMaxOrder) {
    var order = SortingOrderStep + (currentMaxOrder > 0 ? currentMaxOrder : defaultOrder);
    props.SortingData.Fill(SortingLayer, order);
}
```

`UISortingController` (компонент на префабе) применяет эти данные к `Canvas`, `Renderer`, `SortingGroup`. Это позволяет окнам корректно перекрывать друг друга вне зависимости от иерархии в сцене.

---

## 12. MVVM-вариант окна

**Файл:** `Assets/Core/UI/Scripts/Windows/WindowWidget.cs`

```csharp
public abstract class WindowWidget<TProps, TView, TViewModel> : MvvmWidget<TProps>, IWindow
    where TProps : WindowProps
    where TView : MonoBehaviour
    where TViewModel : ...
```

Расширяет `MvvmWidget<TProps>` (который расширяет `UIWidget<TProps>`). Добавляет:
- Typed `TView` и `TViewModel`
- `EventBindingContext` для декларативного биндинга
- Тот же lifecycle что и `Window<TProps>`

Выбор между `Window<TProps>` и `WindowWidget<TProps, TView, TViewModel>` — дело вкуса. Первый проще, второй — структурирован под MVVM.

---

## 13. Кеширование виджетов

Управляется `WidgetFactory` + `ActiveWidgetsCache` в `UIService`.

`UIProps` (базовый props) хранит флаг `KeepInCacheAfterClose`. Если `true` — виджет после закрытия не уничтожается, а возвращается в пул и при следующем открытии берётся оттуда. При этом вызывается `Dispose()` + `InjectAsync(newProps)` — виджет переинициализируется с новыми данными.

---

## 14. Оценка переносимости в другой проект

### Что хорошо абстрагировано (переносимо)

| Компонент | Переносимость | Зависимости |
|---|---|---|
| `UIQueue<TProps>` | Высокая | `IWidgetFactory`, `ISignalBus` |
| `WidgetLifecycleController<TProps>` | Высокая | `IWidgetFactory`, `ISignalBus` |
| `UIWidget<TProps>` | Высокая | `UIDependencies` (static locator) |
| `Window<TProps>` | Высокая | Наследует `UIWidget` |
| `WindowProps` | Высокая | ScriptableObject, Unity |
| `IWindowsController` | Высокая | Чистый интерфейс |
| `WindowsController` | Средняя | `IUiParams` (Transform-корни), `ISignalBus` |
| Сигнальная архитектура | Высокая | Любой `ISignalBus` |
| `OpenAndWaitClosingWindowCommand` | Высокая | `ICommandsFactory`, `IUIService` |

### Проблемные точки

| Проблема | Описание |
|---|---|
| `UIDependencies` — статический локатор | `UIWidget` использует статик для доступа к `UIService`, `SignalBus`, `Logger`. Это coupling без DI-контейнера. В другом проекте нужно заменить на инъекцию через конструктор или другой механизм. |
| `IWindowsService` — game-specific | Интерфейс жёстко содержит методы конкретной игры (`ShowBankAsync`, `ShowPvpLobby`, …). Для другого проекта его надо писать с нуля. Ядро системы это не затрагивает. |
| `[InjectGenerate]` / Zenject/VContainer | Контроллеры помечены атрибутами конкретного DI-фреймворка. Потребуется перерегистрация. |
| `ICommandsFactory` | Система команд — проектная. `OpenAndWaitClosingWindowCommand` легко переписать под другой async-механизм. |
| `Addressables` | `WidgetFactory` использует Unity Addressables для загрузки префабов. Стандартный Unity-подход, но требует настройки в новом проекте. |

### Итог

**Ядро системы (Queue + LifecycleController + UIWidget + Props) — переносимо** при условии подмены:
1. Статического `UIDependencies` → DI-инъекция
2. Регистрации в DI-контейнере
3. Настройки Addressables-групп
4. Написания нового `IWindowsService`-фасада под нужды проекта

`IWindowsController`, `UIQueue`, `WidgetLifecycleController` — **проектно-нейтральные компоненты**, не содержат игровой специфики.
