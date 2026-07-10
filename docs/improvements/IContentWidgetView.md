# IContentWidgetView — Система контент-виджетов

> **Статус: принято к порту в MyBookstore (2026-07-07).** Ниже два пласта: **§0 — зафиксированные
> решения и маппинг на наш UI-фреймворк** (то, что делаем), и **§1+ — reference-чертёж из чужой
> кодовой базы** (пути `Assets/Game/UI/UIShared/Scripts/ContentWidget/`, `CardCollection/…` — их в
> MyBookstore **нет**, это прототип, а не существующий код). Порт делаем адаптированным под наши
> конвенции, не дословно. Тип документа — 🛠 improvement.

## 0. Решения для MyBookstore (source of truth)

### 0.1. Зачем

Нужен переиспользуемый **заякоренный инфо-виджет**: клик по UI-элементу → всплывающая вью рядом с
ним, показывающая контекстную информацию. Форма reference-паттерна (типизированные данные + реестр
`тип данных → prefab` + host, сам позиционирующий вью) подходит и принята. Основная ценность —
открытая расширяемость под будущие источники (§0.2, пункт 3).

### 0.2. Источники использования (принятый scope)

| # | Источник | Данные виджета | Статус |
|---|---|---|---|
| 1 | Клик по элементу пула жанров в [GameplaySceneView](../../Assets/Game/UI/GameplayScene/GameplaySceneView.cs) (`_genreBookCountPool` → `GameplayGenreBookCountItemView`) | вероятность продажи, % | Новое. Требует **добавить кнопку** на item + прокинуть click; число берётся из sales-модели (`baseSaleChance × locationMod × decorMod`, [ADR-0004](../adr/0004-stock-model-hybrid-sale-chance.md)/[0006](../adr/0006-passive-sales-requested-genre.md)) в момент клика. |
| 2 | Инфо-кнопка декора: `_infoButton` в [DecorInventoryCardView](../../Assets/Game/Features/Decor/UI/DecorInventoryCardView.cs) | инфо по декору (имя, бонусы, характеристики) | Виджет становится **альтернативой** попапу. |
| 3 | Прочие будущие источники | по типу данных | Открыто. Добавление = новый data-класс + view/prefab + одна регистрация в реестре. |

### 0.3. Решение по DecorInfoPopup (пункт 2 — важное)

- Новый виджет используется **вместо** окна [DecorInfoPopup](../../Assets/Game/Features/Decor/UI/DecorInfoPopup.cs) как способ показать инфо по декору у кнопки.
- **`DecorInfoPopup` НЕ удаляем** — окно остаётся в проекте.
- **Что именно показывать (виджет vs. `DecorInfoPopup`) решает вызывающий код** — оба способа
  сосуществуют; выбор — на стороне источника клика, не зашит в систему виджетов.
- Следствие: систему виджетов проектируем так, чтобы она **не зависела** от `DecorInfoPopup` и не
  требовала его миграции. Контент декор-виджета — отдельный `IContentWidgetView` (данные мапятся из
  `DecorConfig`, как в `DecorInfoPopup.Apply`), без общего кода с окном.

### 0.4. Обязательные адаптации под наш UI-фреймворк (не дословный порт)

| Тема | Reference (§1+) | Решение в MyBookstore |
|---|---|---|
| Точка показа | `UIManager.Show<ContentWidgetController>(args)` | `IUIManager.ShowAsync<TController>(WindowArgs)` ([IUIManager.cs](../../Assets/Game/Core/UI/Core/IUIManager.cs)); контроллер — `WindowController<TView>` c `[Window("…", WindowType.Widget)]`. |
| Args | `ContentWidgetArgs : WindowArgs` (data + RectTransform) | Подкласс нашего [WindowArgs](../../Assets/Game/Core/UI/Args/WindowArgs.cs): несёт `ContentWidgetDataBase` + `RectTransform` якоря. |
| **Слой показа** | `WindowType.Widget` (по умолчанию) | `WindowType.Widget → WindowLayer.Main` ([UIManager.cs:197](../../Assets/Game/Core/UI/Core/UIManager.cs#L197)). Порядок слоёв `Hud < Main < Additional < System`. Чтобы виджет надёжно был **поверх** источника (в т.ч. Popup-источников из п.3), показывать с `LayerOverride` — `WindowArgs.AsAdditional()`/`AsSystem()`. Для п.1 (панель жанров на HUD) `Main` и так выше HUD, но единый вызов через override держим ради предсказуемости. |
| **Позиционирование** | `RepositionAboveClickedTransform` + `ClampToParentBounds` (только «над» + кламп) | **Дописываем flip+кламп**: над якорем, если есть место, иначе под/сбоку; горизонтальный кламп к safe-краям; не накрывать сам якорь. Кламп-only из reference недостаточен для «виджет в разных частях экрана» — это фактически новый код, а не копия. |
| Асинхронность | корутины (`IEnumerator OnViewCreated`, `StartCoroutine`, авто-hide 10с) | UniTask, как везде в проекте. Авто-hide допустим, но на UniTask + `destroyCancellationToken`. |
| Реестр | статический глобальный `WidgetRegistry` | Допустим статический фасад, привязанный к DI (прецеденты: `ResourceCounterTargets`, `TutorialTargets.Bind`, `ResourceAnimationTargets`); предпочтительно DI-consistent. Регистрация — в `Awake` соответствующего window-view через сериализованные ссылки на prefab. |

### 0.5. Минимальное ядро к реализации

Небольшой переиспользуемый набор:
- host-окно, принимающее `(ContentWidgetDataBase data, RectTransform anchor)`;
- резолв view по типу данных через реестр;
- **flip+кламп** позиционирование (§0.4);
- авто-hide + один активный виджет за раз (тултип-семантика);
- кэш инстансов по типу (⇒ `Setup()` обязан переподписывать слушатели на каждый вызов).

Структура reference (§1+: `IContentWidgetView` / `ContentWidgetDataBase` / `WidgetRegistry` /
host / controller / args) — рабочий чертёж этого ядра; приводим к конвенциям выше.

---

## 1. Обзор reference-чертежа (из исходной кодовой базы)

> Ниже — описание системы из **другого** проекта, как прототип. Файловые пути к MyBookstore не
> относятся. Разделы 0.x выше имеют приоритет при расхождениях.

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
