# IContentWidgetView — Система контент-виджетов

## Обзор

`IContentWidgetView` — интерфейс, определяющий контракт для всех всплывающих виджетов, которые отображаются над кликнутым UI-элементом. Система состоит из четырёх слоёв:

| Слой | Класс / Интерфейс | Роль |
|---|---|---|
| Данные | `ContentWidgetDataBase` + подклассы | Передача данных в виджет |
| Реестр | `WidgetRegistry` | Маппинг тип данных → prefab |
| Хост | `ContentWidgetView` | Инстанцирование, позиционирование, кэш |
| Контроллер | `ContentWidgetController` | MVC-точка входа из UIManager |
| Реализации | `InventoryWidgetView`, etc. | Конкретная UI-логика |

---

## Интерфейс IContentWidgetView

**Файл:** `Assets/Game/UI/UIShared/Scripts/ContentWidget/ContentWidgetDataBase.cs`

```csharp
public interface IContentWidgetView
{
    bool Setup(ContentWidgetDataBase data);
    IEnumerator OnViewCreated();
}
```

### Контракт методов

**`Setup(ContentWidgetDataBase data)`**
- Вызывается хостом сразу после активации экземпляра виджета.
- Реализация делает downcast `data` до своего конкретного подтипа, заполняет UI-контролы и навешивает обработчики событий.
- Возвращает `false` при ошибке (null, неверный тип, пустой контент) — хост немедленно скрывает виджет.
- **Важно:** всегда вызывать `RemoveAllListeners()` перед `AddListener()`, так как экземпляр кэшируется и переиспользуется.

**`OnViewCreated()`**
- Возвращает `IEnumerator` — корутина для отложенной работы (например, ожидание `EndOfFrame` для layout rebuild).
- Если нет отложенной работы — достаточно `yield return null`.

---

## ContentWidgetDataBase

**Файл:** `Assets/Game/UI/UIShared/Scripts/ContentWidget/ContentWidgetDataBase.cs`

```csharp
public abstract class ContentWidgetDataBase { }
```

Пустой абстрактный маркер-класс. Служит единственным базовым типом для:
- generic-ограничения в `WidgetRegistry.Register<TData>()`;
- сигнатуры `IContentWidgetView.Setup()`.

Хост не смотрит внутрь данных — он передаёт непрозрачную ссылку `ContentWidgetDataBase` в разрезолвенный виджет.

---

## WidgetRegistry

**Файл:** `Assets/Game/UI/UIShared/Scripts/ContentWidget/WidgetRegistry.cs`

```csharp
public static class WidgetRegistry
{
    private static readonly Dictionary<Type, IContentWidgetView> Prefabs = new();

    public static void Register<TData>(IContentWidgetView prefab)
        where TData : ContentWidgetDataBase
    {
        Prefabs[typeof(TData)] = prefab;
    }

    public static IContentWidgetView GetPrefab(Type dataType)
    {
        Prefabs.TryGetValue(dataType, out var prefab);
        return prefab;
    }
}
```

### Ключевые особенности

- **Статический глобальный словарь** — живёт всё время работы приложения, скоупинга нет.
- Generic-ограничение `where TData : ContentWidgetDataBase` — проверка корректности типа на этапе компиляции.
- Ключ — точный `Type` (без учёта полиморфизма): подкласс `InventoryWidgetData` требует отдельной регистрации.
- Повторная регистрация одного типа **молча перезаписывает** предыдущую.

### Кто регистрирует

Регистрация происходит в `Awake()` window-view'ов через сериализованные ссылки на prefab из Inspector:

| Window | Регистрируемые типы |
|---|---|
| `InventoryWindowView` | `InventoryWidgetData`, `InventoryResourceWidgetData` |
| `CardCollectionView` | `ContentWidgetData` (cards) |

---

## ContentWidgetView (хост)

**Файл:** `Assets/Game/UI/UIShared/Scripts/ContentWidget/ContentWidgetView.cs`
**Наследование:** `ContentWidgetView : WindowView`

### Сериализованные поля

| Поле | Тип | Назначение |
|---|---|---|
| `_container` | `RectTransform` | Внешняя панель, репозиционируется над кликнутым элементом |
| `_contentContainer` | `RectTransform` | Родитель для инстанцируемых виджетов |
| `_verticalOffset` | `float` (default: 24) | Отступ над верхней границей кликнутого элемента |

### Приватное состояние

| Поле | Назначение |
|---|---|
| `_cachedViews` (`Dictionary<Type, MonoBehaviour>`) | Кэш: один экземпляр на тип данных |
| `_activeView` (`MonoBehaviour`) | Текущий видимый виджет — деактивируется при следующем показе |

### Метод ShowContentView

```
ShowContentView(ContentWidgetDataBase contentData, RectTransform contentRectTransform)
```

Полный pipeline показа виджета:

1. Валидация — если `contentData == null`, вызов `HideContentWidget()`.
2. `StopAllCoroutines()` — сброс таймера авто-скрытия и текущего resize.
3. `DeactivateActiveView()` — деактивация предыдущего виджета.
4. `WidgetRegistry.GetPrefab(contentData.GetType())` — резолв prefab-прототипа.
5. `GetOrCreateViewInstance()` — возврат из кэша или `Instantiate` нового экземпляра.
6. `TryGetComponent<IContentWidgetView>()` — runtime-проверка интерфейса на инстансе.
7. `gameObject.SetActive(true)`.
8. `view.Setup(contentData)` — заполнение UI.
9. `RepositionAboveClickedTransform()` — конвертация координат clicked-элемента в canvas-пространство (поддержка multi-canvas / multi-camera через `RectTransformUtility`).
10. `StartCoroutine(ResizeAndRepositionCoroutine(view.OnViewCreated()))` — запуск корутины виджета, затем повторное позиционирование и `ClampToParentBounds()`.
11. `StartCoroutine(HidePopupCoroutine())` — авто-скрытие через 10 секунд (`ContentWidgetHideDelay`).

### ClampToParentBounds

Предотвращает визуальный выход виджета за границы родительского `RectTransform`, зажимая `_container.anchoredPosition` с учётом pivot'а.

### Dispose / OnDestroy

Останавливает корутины, уничтожает все кэшированные GameObject'ы, очищает словарь.

---

## ContentWidgetController (MVC-контроллер)

**Файл:** `Assets/Game/UI/UIShared/Scripts/ContentWidget/ContentWidgetController.cs`
**Атрибут:** `[Window("ContentWidget", WindowType.Widget)]`
**Наследование:** `ContentWidgetController : WindowController<ContentWidgetView>`

### ContentWidgetArgs

```csharp
public class ContentWidgetArgs : WindowArgs
{
    public ContentWidgetDataBase ContentWidgetData { get; }
    public RectTransform RectTransform { get; }
}
```

### Lifecycle

| Событие | Действие |
|---|---|
| `OnShowStart()` | `View.ShowContentView(Args.ContentWidgetData, Args.RectTransform)` |
| `OnShowComplete()` | Подписка `View.CloseClick += CloseWindow` |
| `CloseWindow()` | `UIManager.Hide<ContentWidgetController>()` |
| `OnHideStart()` | Отписка `CloseClick` |

**Вызов из feature-кода:**
```csharp
UIManager.Show<ContentWidgetController>(new ContentWidgetArgs(data, rectTransform));
UIManager.Hide<ContentWidgetController>();
```

---

## Классы данных (ContentWidgetDataBase подклассы)

### InventoryWidgetData
**Файл:** `Assets/Game/Features/Inventory/Runtime/Implementation/UI/ContentWidget/InventoryWidgetData.cs`

```csharp
string ItemId
Action<string> ButtonPressed
```
Для инвентарных предметов с действием "использовать".

### InventoryResourceWidgetData
**Файл:** `Assets/Game/Features/Inventory/Runtime/Implementation/UI/ContentWidget/InventoryResourceWidgetData.cs`

```csharp
string ItemId
int ItemAmount
Sprite ItemSprite     // предзагруженный спрайт
Action<string> ButtonPressed
```
Для ресурсных предметов — содержит иконку и количество, загруженные до открытия виджета.

### ContentWidgetData (CardCollection)
**Файл:** `Assets/Game/Features/CardCollection/CardsCollectionImpl/Scripts/UI/ContentWidget/ContentWidgetData.cs`

```csharp
IReadOnlyList<string> CardPackAddresses
IReadOnlyList<ContentWidgetResourceData> Resources  // struct: string Address + int Amount
static ContentWidgetData Empty  // sentinel для пустого состояния
```
Все поля readonly, передаются через конструктор. Создаётся через extension-метод `ContentWidgetDataMapper.ToContentWidgetData(this RewardSpec)`.

---

## Конкретные реализации IContentWidgetView

### InventoryWidgetView
**Файл:** `Assets/Game/Features/Inventory/Runtime/Implementation/UI/ContentWidget/InventoryWidgetView.cs`

- Сериализованный: `Button _inventoryButton`
- `Setup`: `RemoveAllListeners()` → `AddListener(OnInventoryButtonClickedHandler)`
- `OnViewCreated`: `yield return null`
- По клику: вызывает `ButtonPressed(ItemId)` → в `InventoryWindowController` запускает `ConsumeItemAsync` и закрывает виджет.

### InventoryResourceWidgetView
**Файл:** `Assets/Game/Features/Inventory/Runtime/Implementation/UI/ContentWidget/InventoryResourceWidgetView.cs`

- Сериализованные: `Button _inventoryButton`, `Image _image`, `TextMeshProUGUI _amountText`
- `Setup`: устанавливает `_image.sprite` и `_amountText.text`, переподписывает кнопку
- `OnViewCreated`: `yield return null`
- По клику: вызывает `ButtonPressed(ItemId)` → текущая реализация только скрывает виджет (функция "использовать ресурс" не реализована, выводится `Debug.LogWarning`).

### CardsOfferWidgetView
**Файл:** `Assets/Game/Features/CardCollection/CardsCollectionImpl/Scripts/UI/ContentWidget/CardsOfferWidgetView.cs`

- Сериализованные: `RectTransform _container`, `UIListPool<ContentItemView> _itemsPool`, `HorizontalLayoutGroup _itemsGroup`
- **Асинхронная загрузка спрайтов** через `ProdAddressablesWrapper.LoadAsync<Sprite>()` (UniTask, последовательно: сначала паки карт, затем ресурсы).
- `Setup`:
  1. `_itemsPool.DisableAll()` — recycle всех pooled item'ов
  2. Отмена и пересоздание `CancellationTokenSource` (связан с `this.GetCancellationTokenOnDestroy()`)
  3. Создание `ContentItemView` из пула для каждого card pack и ресурса
  4. Запуск `LoadContentSpritesSequentially(...).Forget()` — async без await (fire-and-forget с отменой)
  5. Возврат `false` если ни одного pool-item не активировано
- `OnViewCreated`: `ResizeToLayout()` — ждёт `WaitForEndOfFrame()`, затем `LayoutRebuilder.ForceRebuildLayoutImmediate()` + `Canvas.ForceUpdateCanvases()`, чтобы `HorizontalLayoutGroup` правильно рассчитал размеры до репозиционирования хостом.

**Ключевые отличия от Inventory-виджетов:**
- Спрайты грузятся лениво из Addressables (не предзагружены в data).
- Переменное количество дочерних item-view'ов через `UIListPool`.
- Единственная реализация с нетривиальным `OnViewCreated`.
- `CancellationTokenSource` гарантирует отмену in-flight загрузок при повторном `Setup` или уничтожении компонента.

---

## Поток данных

```
Клик пользователя на UI-элемент (инвентарный предмет, сундук с наградой)
    │
    ▼
Feature Controller
    │  создаёт конкретный ContentWidgetDataBase (InventoryWidgetData, etc.)
    │  оборачивает в ContentWidgetArgs(data, clickedElement.RectTransform)
    ▼
UIManager.Show<ContentWidgetController>(args)
    │
    ▼
ContentWidgetController.OnShowStart()
    │  вызывает View.ShowContentView(args.ContentWidgetData, args.RectTransform)
    ▼
ContentWidgetView.ShowContentView()
    │  1. StopAllCoroutines, DeactivateActiveView
    │  2. WidgetRegistry.GetPrefab(contentData.GetType())   ← type-keyed lookup
    │  3. GetOrCreateViewInstance()                          ← кэш или Instantiate
    │  4. TryGetComponent<IContentWidgetView>()             ← runtime-проверка
    │  5. gameObject.SetActive(true)
    │  6. view.Setup(contentData)                            ← заполнение UI
    │  7. RepositionAboveClickedTransform()                  ← canvas-координаты
    │  8. StartCoroutine(ResizeAndRepositionCoroutine(       ← OnViewCreated + reclamp
    │         view.OnViewCreated()))
    │  9. StartCoroutine(HidePopupCoroutine())               ← авто-скрытие 10с
    ▼
Виджет виден, позиционирован над кликнутым элементом

Действие пользователя или таймаут
    │
    ▼
UIManager.Hide<ContentWidgetController>()
    │  ContentWidgetController.OnHideStart → отписка CloseClick
    │  ContentWidgetView остаётся живым с кэшированными экземплярами
```

---

## Цепочка наследования

```
MonoBehaviour
  └── (реализует) IContentWidgetView
        ├── InventoryWidgetView
        ├── InventoryResourceWidgetView
        └── CardsOfferWidgetView

WindowView
  └── ContentWidgetView               (хост-панель)

WindowController<ContentWidgetView>
  └── ContentWidgetController
```

---

## Паттерны и архитектурные решения

### Двойная проверка интерфейса
`WidgetRegistry` хранит прототип как `IContentWidgetView`. `GetOrCreateViewInstance` кастует к `MonoBehaviour` для `Instantiate`, затем хост снова вызывает `TryGetComponent<IContentWidgetView>()` на новом инстансе. Это вынужденно — Unity's `Instantiate` принимает `UnityEngine.Object`, через интерфейс напрямую инстанцировать нельзя. Второй `TryGetComponent` — defensive runtime assertion.

### Кэширование экземпляров
`_cachedViews` хранит один `MonoBehaviour` на тип данных на время жизни `ContentWidgetView`. Исключает повторные `Instantiate`/`Destroy`. Следствие: `Setup()` **обязан** снимать и повторно навешивать слушатели событий на каждый вызов.

### Статический глобальный реестр
Намеренный trade-off простоты vs. гибкости. Регистрация — одна строка в `Awake`. Нет DI, нет per-scene скоупинга, нет механизма отмены регистрации. При сосуществовании двух экземпляров одного окна — последний `Awake` молча перезаписывает предыдущую регистрацию.

### Авто-сброс корутин
`StopAllCoroutines()` в начале каждого `ShowContentView` гарантирует, что 10-секундный таймер всегда свежий и любой in-progress resize предыдущего показа отменён.

### Отмена Addressables-загрузок (CardsOfferWidgetView)
`CancellationTokenSource` пересоздаётся при каждом `Setup()` и связан с `destroyCancellationToken`. Повторный показ с новыми данными корректно отменяет предыдущие in-flight загрузки, не допуская появления устаревших спрайтов.

---

## Добавление нового виджета (чек-лист)

1. **Создать класс данных:** `public class MyWidgetData : ContentWidgetDataBase { ... }`
2. **Создать MonoBehaviour-реализацию:** `public class MyWidgetView : MonoBehaviour, IContentWidgetView { ... }`
   - В `Setup`: downcast к `MyWidgetData`, заполнить UI, переподписать кнопки.
   - В `OnViewCreated`: `yield return null` (или `ResizeToLayout()` если нужен layout rebuild).
3. **Создать prefab** с компонентом `MyWidgetView`, сериализовать нужные UI-ссылки.
4. **Добавить сериализованное поле** с ссылкой на prefab в нужный Window-view.
5. **Зарегистрировать в Awake:** `WidgetRegistry.Register<MyWidgetData>(myWidgetViewPrefab);`
6. **Вызывать показ:** `UIManager.Show<ContentWidgetController>(new ContentWidgetArgs(new MyWidgetData(...), rectTransform));`
