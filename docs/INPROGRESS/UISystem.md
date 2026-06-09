# UISystem — Документация

**Пакет:** `com.dovhopolov.uisystem` v1.1.0  
**Unity:** 2021.3+  
**Лицензия:** MIT

---

## Обзор

UISystem — это менеджер UI-окон для Unity. Управляет стеком окон, анимациями переходов, попапами, виджетами и блокировкой ввода во время переходов. Построен на Command-паттерне: каждое показ/скрытие — это команда, обрабатываемая через очередь в корутине.

---

## Архитектура

```
UIManager (MonoBehaviour)
├── UIStack          — стек команд и активных окон
├── UIStorage        — кэш инстансов окон (через WindowFactoryBase)
├── LockMonitor      — ручная блокировка блок-панели
└── UiFilter         — фильтры: можно ли создать/обработать команду

WindowController<TView>   — логика окна (не MonoBehaviour)
WindowView (MonoBehaviour) — визуальная часть, живёт на префабе
WindowAnimation            — базовый класс анимации, вешается на префаб
```

---

## Типы окон

| Тип      | Поведение |
|----------|-----------|
| `Page`   | Полноэкранная страница. При открытии новой страницы текущая скрывается. |
| `Popup`  | Попап поверх текущей страницы. Открывается в стеке страницы, не в корневом. |
| `Widget` | Независимый UI-элемент. Не участвует в стеке, показывается немедленно. |

---

## Быстрый старт

### 1. Настройка сцены

Добавить `UIManager` как MonoBehaviour на Canvas-объект. В Inspector указать:
- `_blockPanel` — GameObject для блокировки ввода во время анимаций
- `_windowsRoot` — RectTransform-корень, куда спавнятся окна

### 2. Реализация фабрики

Наследоваться от `WindowFactoryBase` и реализовать загрузку префабов (например, через Addressables):

```csharp
public class MyWindowFactory : WindowFactoryBase
{
    public override async Task<T> CreateAsync<T>() where T : class, IWindowController
    {
        var attr = typeof(T).GetCustomAttribute<WindowAttribute>();
        var prefab = await Addressables.LoadAssetAsync<GameObject>(attr.PrefabAddressableReference).Task;
        var go = Object.Instantiate(prefab, _root);
        var view = go.GetComponent<WindowView>();
        var controller = Activator.CreateInstance<T>();
        controller.Configurate(view, _uiManager, attr);
        return controller;
    }

    public override T CreaseSync<T>() where T : class, IWindowController
    {
        // синхронная версия (Resources.Load и т.п.)
    }

    protected override T Create<T>(WindowView windowPrefab, WindowAttribute attr) where T : class, IWindowController
    {
        var go = Object.Instantiate(windowPrefab, _root);
        var controller = Activator.CreateInstance<T>();
        controller.Configurate(go, _uiManager, attr);
        return controller;
    }
}
```

### 3. Инициализация UIManager

```csharp
[SerializeField] private UIManager _uiManager;

void Start()
{
    var factory = new MyWindowFactory();
    var eventHandler = new MyEventHandler(); // или null
    _uiManager.Configurate(factory, eventHandler);
}
```

---

## Создание окна

### Шаг 1 — View (MonoBehaviour на префабе)

```csharp
public class MainMenuView : WindowView
{
    [SerializeField] private Button _playButton;
    public Button PlayButton => _playButton;
}
```

### Шаг 2 — Controller

```csharp
[Window("Prefabs/MainMenu", WindowType.Page, isRoot: true)]
public class MainMenuController : WindowController<MainMenuView>
{
    protected override void OnInit()
    {
        View.PlayButton.onClick.AddListener(OnPlayClicked);
    }

    public override void UpdateWindow()
    {
        // обновить данные при каждом показе
    }

    protected override void OnShowComplete()
    {
        // окно полностью показано
    }

    protected override void OnHideComplete(bool isClosed)
    {
        // окно скрыто; isClosed = true если закрыто, false если перекрыто новым
    }

    private void OnPlayClicked()
    {
        UIManager.Show<GameController>();
    }
}
```

> `[Window]` — это `[WindowAttribute]`. Первый аргумент — ключ Addressables для префаба.

---

## Передача аргументов

```csharp
public class ProductArgs : WindowArgs
{
    public int ProductId { get; }
    public ProductArgs(int id) => ProductId = id;

    // если один и тот же тип окна открывается с разными данными
    public override int GetPrimaryKey() => ProductId;
}

// Открыть с аргументами
_uiManager.Show<ProductController>(new ProductArgs(42));

// Получить аргументы в контроллере
protected override void OnShowStart()
{
    var args = Arguments as ProductArgs;
    // использовать args.ProductId
}
```

Для виджетов нужен подкласс `WidgetArgs`:

```csharp
public class ToastArgs : WidgetArgs
{
    public string Message { get; }
    public ToastArgs(string msg) => Message = msg;
}
```

---

## API UIManager

| Метод | Описание |
|-------|----------|
| `Show<T>(args, showType)` | Показать окно (fire-and-forget) |
| `ShowAsync<T>(args, showType)` | Показать окно и дождаться открытия, вернуть контроллер |
| `Hide()` | Скрыть текущее верхнее окно/попап |
| `Hide<T>(forceClose)` | Скрыть конкретный тип окна; `forceClose=true` — без анимации |
| `HideAll()` | Сбросить весь стек |
| `HideAll(params Type[])` | Скрыть все, кроме указанных типов |
| `GetWindowSync<T>()` | Получить инстанс контроллера синхронно |
| `GetWindowAsync<T>()` | Получить инстанс контроллера асинхронно |
| `IsWindowActive<T>()` | Окно в стеке или отображается |
| `IsWindowShown<T>()` | Окно сейчас видно (`IsShown == true`) |
| `IsWindowSpawned<T>()` | Окно было создано (есть в кэше) |
| `IsWindowActiveOrBuffered<T>()` | Активно или в буфере команд |
| `GetTopWindow()` | Вернуть самое верхнее видимое окно |
| `SetManualLock(obj)` | Заблокировать блок-панель вручную, вернуть `Lock` |
| `ClearCache()` | Уничтожить все инстансы окон и очистить кэш |

---

## Режимы показа (UIShowType)

```csharp
// Manual (по умолчанию) — показывает немедленно
_uiManager.Show<ShopController>(showType: UIShowCommand.UIShowType.Manual);

// Ordered — добавляется в очередь по приоритету, ждёт пока стек не освободится
_uiManager.Show<NotificationController>(showType: UIShowCommand.UIShowType.Ordered);
```

`Ordered` полезно для системных уведомлений или туториальных шагов: окно встанет в очередь после текущего и покажется в нужный момент, отсортированное по `Priority` из `WindowAttribute`.

---

## Блокировка ввода

UIManager автоматически включает `_blockPanel` пока идёт анимация. Для ручной блокировки:

```csharp
// заблокировать
var myLock = _uiManager.SetManualLock("loading");

// разблокировать
myLock.Dispose();

// или через using
using var myLock = _uiManager.SetManualLock("loading");
await LoadSomething();
// lock освободится автоматически
```

---

## Фильтры окон

Позволяют динамически запрещать открытие/обработку определённых окон:

```csharp
public class TutorialFilter : IUiWindowFilter
{
    public bool CanBeExecuted(Type windowType)
    {
        // запретить открывать ShopController во время туториала
        return windowType != typeof(ShopController);
    }
}

// подключить
_uiManager.UiFilter.AddFilter(new TutorialFilter());

// отключить
_uiManager.UiFilter.RemoveFilter(myFilter);
```

---

## Подписка на события

Реализовать `UIManagerEventHandlerBase` и передать в `Configurate`:

```csharp
public class MyEventHandler : UIManagerEventHandlerBase
{
    public override void WindowShowEventInvoke(IWindowController window)
        => Debug.Log($"Shown: {window.GetType().Name}");

    public override void WindowHideEventInvoke(IWindowController window, bool isClosed)
        => Debug.Log($"Hidden: {window.GetType().Name}, closed={isClosed}");

    public override void WindowAnimationEventInvoke(IWindowController window, WindowAnimationType type) { }
    public override void StackCommandProcessedEventInvoke(UICommand cmd) { }
    public override void StackCommandProcessEventInvoke(UICommand cmd) { }
    public override void StackCommandProcessAddEventInvoke(UICommand cmd) { }
}
```

---

## Анимации

Создать компонент-наследник `WindowAnimation` и добавить его на GameObject префаба:

```csharp
public class FadeAnimation : WindowAnimation
{
    [SerializeField] private float _duration = 0.3f;
    public override float ShowAnimationTime => _duration;

    public override IEnumerator AnimationIn()
    {
        // fade in логика
        yield return new WaitForSeconds(_duration);
    }

    public override IEnumerator AnimationOut(float hideAnimationTime)
    {
        yield return new WaitForSeconds(hideAnimationTime);
    }
}
```

В Inspector `WindowView._animation` назначить этот компонент.

---

## Переопределение `IsCloseBlocked`

Если нужно запретить закрытие окна через `Hide()`:

```csharp
[Window("Prefabs/CriticalDialog", WindowType.Popup)]
public class CriticalDialogController : WindowController<CriticalDialogView>
{
    public override bool IsCloseBlocked => true; // нельзя закрыть через UIManager.Hide()
}
```

---

## Структура файлов пакета

```
Assets/UISystem/
├── UIManager.cs                  — точка входа
├── Core/
│   ├── UIStack.cs                — стек команд и окон
│   ├── UIStorage.cs              — кэш инстансов
│   ├── UiFilter.cs               — система фильтров
│   ├── IUiWindowFilter.cs        — интерфейс фильтра
│   └── YieldCollection.cs        — хелпер для параллельных корутин
├── WindowController/
│   ├── IWindowController.cs      — интерфейс + базовый WindowController<T>
│   ├── WindowView.cs             — базовый View (MonoBehaviour)
│   ├── WindowAttribute.cs        — атрибут для контроллеров
│   ├── WindowAnimation.cs        — базовый класс анимаций
│   ├── WindowType.cs             — enum Page/Popup/Widget
│   └── UIManagerEventHandlerBase.cs — базовый обработчик событий
├── WindowFactory/
│   └── WindowFactoryBase.cs      — абстрактная фабрика
├── Args/
│   ├── WindowArgs.cs             — базовый класс аргументов
│   └── IPrimaryKeyGenerator.cs   — интерфейс ключа
├── Commands/
│   ├── UICommand.cs              — базовая команда
│   ├── UIShowCommand.cs          — команда показа
│   └── UIHideCommand.cs          — команда скрытия
├── Lock/
│   ├── Lock.cs                   — IDisposable-локер
│   └── LockMonitor.cs            — реестр активных локов
├── Check/
│   ├── WindowReflection.cs       — запись об окне в стеке
│   ├── WindowReflectionContainer.cs — упорядоченный контейнер
│   └── KeyList.cs                — список с доступом по ключу
├── Extension/
│   └── UISystemExtension.cs      — Task.WaitWhile хелпер
└── Sample/
    └── UIManagerCanvas.prefab    — готовый Canvas-префаб с UIManager
```
