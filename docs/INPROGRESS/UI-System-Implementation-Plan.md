# UI System — план реализации (итерация 1)

**Статус:** обсуждение архитектуры завершено по ключевым точкам, идём к Phase 0.
**Контекст:** заменяет внешнюю зависимость `com.dovhopolov.uisystem` собственным кодом
внутри проекта. Существующий код пакета будет перенесён позже отдельным шагом.

Связанные документы:
- [UI-Window-System.md](UI-Window-System.md) — референс из другого проекта, чьи концепции мы заимствуем
- [UISystem.md](UISystem.md) — документация исходного пакета `com.dovhopolov.uisystem`

---

## 1. Цели

1. **Свой код в проекте** — UI-система живёт под `Assets/Game/...` (а не в пакете), все правки идут через обычный PR-flow
2. **Слои с первого дня** (`Main / Additional / System / Develop`) — у текущей системы UISystem не было разделения, отсюда баги с сортировкой и фокусом
3. **Автоматическая сортировка через `UISortingController`** — снять с разработчика ручное управление `sortingOrder`
4. **VContainer + Addressables + UniTask** — единый стек проекта, никаких корутин в новом коде
5. **Удобный async API** — `ShowAsync<T>(args)` возвращает контроллер; для wait-for-close — `await window.WaitForCloseAsync()`
6. **Минимум абстракций на старте.** Props-as-SO, MVVM, custom-команды — потенциально полезны, но **в первой итерации НЕ делаем**. См. §15

---

## 2. Терминология

| Термин | Что это |
|---|---|
| **WindowType** | *Стек-поведение* окна: `Page`, `Popup`, `Widget`. Прибит к коду через `[Window]`. |
| **WindowLayer** | *Слой отображения*: `Main`, `Additional`, `System`, `Develop`. Может меняться в рантайме через `AsSystem()` / `AsAdditional()`. |
| **WindowController<TView>** | Логика окна, не MonoBehaviour. |
| **WindowView** | MonoBehaviour на префабе, держит ссылки на UI-элементы. |
| **WindowArgs** | Параметры открытия + fluent-конфигурация слоя. |
| **UIManager** | Точка входа, фасад. Делегирует в UIStack/UISortingController. |
| **UIStack** | Внутренний стек активных окон + очередь команд `Show/Hide`. |
| **UISortingController** | Назначает `SortingLayer` + `sortingOrder` каждому окну при появлении. |

---

## 3. Иерархия сцены (DontDestroyOnLoad)

```
GlobalLifetimeScope         ← VContainer root
UIManagerCanvas             ← Canvas (Screen Space Overlay)
├── HudRoot                 ← RectTransform; HUD-виджеты, GameplaySceneController
│   (sorting: HudLayer, order 0..)
├── WindowsRoot             ← RectTransform; все окна Main/Additional/System
│   (per-window Canvas с override sortingOrder)
└── Blocker                 ← полноэкранный raycast-blocker
TransitionViewLayer         ← отдельный объект; живёт сам, через DI (см. §11)
```

**Решение по HUD:** отдельный `HudRoot`, ниже WindowsRoot по сортировке. HUD никогда не попадает в очередь окон, не участвует в Page/Popup-стеке. GameplaySceneController остаётся обычным префабом, но крепится в `HudRoot`, не в `WindowsRoot`.

---

## 4. WindowType × WindowLayer — гибридная схема

`WindowType` задаётся в коде через атрибут и **не меняется**:

```
[Window("Prefabs/UI/ShopWindow", WindowType.Page)]
public class ShopController : WindowController<ShopView> { ... }
```

`WindowLayer` задаётся **в момент открытия** через args:

```
uiManager.ShowAsync<ShopController>(new ShopArgs(...));              // дефолтный слой Main
uiManager.ShowAsync<ConfirmController>(new ConfirmArgs(...).AsAdditional());
uiManager.ShowAsync<NoInternetController>(new NoInternetArgs().AsSystem());
```

### Дефолтный слой по типу

| WindowType | Дефолтный Layer |
|---|---|
| `Page` | `Main` |
| `Popup` | `Additional` |
| `Widget` | `Main` (фоновый), но без участия в стеке |

### Что значит каждый слой

| Layer | Поведение |
|---|---|
| **Main** | Стек страниц, один активный экран в любой момент. Новый Page прячет предыдущий. |
| **Additional** | Модальные окна поверх Main. Не в очереди. Привязаны к родительскому Main, умирают вместе с ним. |
| **System** | Критические сообщения. Свой стек. Всегда поверх Main и Additional. Не зависят от состояния игры. |
| **Develop** | Дебаг-окна, читы. Выше всех. В релизной сборке отключаются. |

### AsAdditional / AsSystem / AsMain — fluent API

Реализуется как методы на `WindowArgs`:
- `WindowArgs.AsMain()`
- `WindowArgs.AsAdditional()`
- `WindowArgs.AsSystem()`
- `WindowArgs.AsDevelop()`

Каждый метод выставляет внутреннее поле `Layer` и возвращает `this`.

---

## 5. Стек и очереди

- **Main** — `UIStack<MainSlot>`, поведение «открытие нового скрывает предыдущий»
- **Additional** — `List<AdditionalContainer>` с привязкой к parent Main. Не в очереди
- **System** — отдельный стек, выше Main
- **Develop** — отдельный стек, выше System

Фокус (для обработки Back/Esc):
```
SystemStack > Develop (если активен в Editor) > LastAdditional > MainStack
```

### Команды Show/Hide

Остаются как в текущем UISystem (`UIShowCommand`, `UIHideCommand`), но:
- Командный процессор знает про Layer, маршрутизирует в нужный стек
- Команды получают `Layer` из `WindowArgs`, не из атрибута

---

## 6. Сортировка — UISortingController

**Стратегия:** каждый WindowLayer имеет свой **Unity SortingLayer** (Project Settings → Tags & Layers → Sorting Layers). Внутри слоя окна сортируются через **per-Canvas `sortingOrder`**.

### Что настраивается в Unity

Добавляем SortingLayer'ы (один раз в проекте):
```
Default (engine default)
UI_Hud           (order in editor: 100)
UI_Main          (200)
UI_Additional    (300)
UI_System        (400)
UI_Develop       (500)
```

### Что делает UISortingController в коде

При появлении окна:
1. По `args.Layer` определяет нужный `SortingLayer`
2. Берёт текущий максимум `sortingOrder` в этом слое
3. Назначает новому окну `sortingOrder = max + step` (step = 10 — место под анимации/sub-elements)
4. Применяет на корневой `Canvas` окна (`Canvas.overrideSorting = true`)

### Почему именно так

- Окна разных слоёв **физически не могут перепутаться** — это гарантирует engine на уровне SortingLayer'а
- Внутри слоя порядок управляется числом, очевидно дебажится
- Per-Canvas даёт изоляцию rebuild'ов (правка одного окна не дёргает остальные)
- Решает класс проблем «System-окно ушло за Main» раз и навсегда

---

## 7. Жизненный цикл окна

Хуки на `WindowController<TView>`:

| Хук | Когда |
|---|---|
| `OnInit()` | Один раз при создании, после конфигурации View. До первого `Show`. |
| `OnShowStart()` | Перед анимацией появления. `Arguments` уже доступны. |
| `OnShowComplete()` | После анимации появления. |
| `UpdateWindow()` | При каждом повторном `Show` (выход из кеша или поверхностный refresh). |
| `OnHideStart(bool isClosed)` | Перед анимацией скрытия. `isClosed=false` если окно лишь перекрыто новым. |
| `OnHideComplete(bool isClosed)` | После анимации скрытия. |
| `OnDispose()` | Перед уничтожением (см. §9 Кеширование). |

Лайфсайкл идентичен текущему UISystem — переносить будет легко.

---

## 8. WindowArgs

Базовый класс:
```
WindowArgs
├── Layer                  ← WindowLayer (default зависит от WindowType)
├── ParentWindow           ← опциональная ссылка на parent (для Additional)
├── PrimaryKey             ← int, по умолчанию 0
├── AsMain() / AsAdditional() / AsSystem() / AsDevelop()
└── WithParent(IWindow)
```

`GetPrimaryKey()` остаётся как в текущем UISystem — позволяет открывать один тип окна с разными данными как разные инстансы.

---

## 9. Кеширование

Per-window флаг через атрибут:
```
[Window("Prefabs/UI/ShopWindow", WindowType.Page, keepInCache: true)]
```

- `keepInCache: true` — после `Hide(closed)` инстанс остаётся в `UIStorage`, при следующем `Show` берётся оттуда, вызывается `OnDispose` → `UpdateWindow` с новыми args
- `keepInCache: false` (default) — инстанс уничтожается при закрытии

**Эвристика:** кешировать тяжёлые окна (Shop, Inventory, Map), не кешировать диалоги/попапы.

---

## 10. BlockLayer

Существующий `_blockPanel` расширяется логикой parent-окна:

- На время **show/hide анимации любого окна** Blocker автоматически активен — блокирует ввод глобально
- Дополнительно при анимации **Additional-окна** под ним подсвечивается `parentWindow` через **dimmed overlay** (затемнение). Это новое поведение, заимствовано из подхода A (`BlockUnderWindows`).
- Manual lock через `SetManualLock("loading")` остаётся как в текущем UISystem

В первой итерации **достаточно глобального Blocker**, dimmed overlay для parent — Phase 2.

---

## 11. Async API

Базовый набор:
```
UniTask<TController> ShowAsync<TController>(WindowArgs args = null)
UniTask HideAsync<TController>(bool forceClose = false)
UniTask WaitForCloseAsync(IWindowController controller)  // экстеншен
```

Wait-for-close сценарий (эквивалент `OpenAndWaitClosingWindowCommand` из подхода A):
```
var confirm = await uiManager.ShowAsync<ConfirmController>(args);
await confirm.WaitForCloseAsync();
// ...продолжение
```

Не делаем отдельный командный класс — `ShowAsync + WaitForCloseAsync` покрывает кейс.

---

## 12. TransitionViewLayer

**Решение:** живёт отдельно от UISystem, **не управляется через UIManager**. Инжектится через DI как `ISceneTransitionService`. UIManager его не знает.

Причина: TransitionViewLayer — про загрузку сцен (включает спиннер, прогресс, лоадинг-скрин), а не про UI-окна. Если затащить его в UISystem, появится coupling с системой сцен, который не нужен.

Контракт:
```
ISceneTransitionService.PlayInAsync()   // затемнение
ISceneTransitionService.PlayOutAsync()  // проявление
```

Сама загрузка Addressables / SceneManager происходит между PlayIn и PlayOut.

---

## 13. Интеграция с VContainer

### Регистрация в GlobalLifetimeScope

Через новый `UiSystemVContainerBindings.RegisterUiSystem()`:

```
builder.Register<IUIManager, UIManager>(Lifetime.Singleton);
builder.Register<IUIStack, UIStack>(Lifetime.Singleton);
builder.Register<IUISortingController, UISortingController>(Lifetime.Singleton);
builder.Register<IWindowFactory, AddressablesWindowFactory>(Lifetime.Singleton);
builder.Register<IUIStorage, UIStorage>(Lifetime.Singleton);
builder.Register<IUiFilter, UiFilter>(Lifetime.Singleton);
```

### Инъекция в WindowController

`WindowFactory.CreateAsync<T>()` после `Activator.CreateInstance<T>()` зовёт `_objectResolver.Inject(controller)` — VContainer инжектит зависимости в публичные поля/свойства/Inject-методы. Это даёт автоматическую DI в каждый WindowController без ручной передачи через args.

---

## 14. Интеграция с Addressables

- Префабы окон лежат в `Assets/UI/Windows/` (или аналогичной группе Addressables)
- Адрес = строка из `[Window]` атрибута (`"Prefabs/UI/ShopWindow"`)
- `AddressablesWindowFactory.CreateAsync<T>()`:
  1. `Addressables.LoadAssetAsync<GameObject>(addr).ToUniTask()`
  2. `Instantiate` в правильный root (HudRoot / WindowsRoot, см. Layer)
  3. `GetComponent<WindowView>()`
  4. `Activator.CreateInstance<T>()` + `Configurate(view, manager, attr)` + `_resolver.Inject(controller)`
  5. Кешируем handle для `Release` при дестрое

Pre-warm для часто открываемых окон (Shop, Inventory) — опционально через `WindowFactory.PreloadAsync<T>()`.

---

## 15. Что НЕ делаем в первой итерации

| Откладываем | Когда вернёмся |
|---|---|
| **Props-as-SO** | Phase 2. После того как 3-4 окна будут готовы — посмотрим, какие из них реально многовариантные. Кандидаты: ConfirmDialog (10+ вариаций), TutorialStep, OfferPopup |
| **MVVM-вариант (`WindowWidget<TProps,TView,TViewModel>`)** | Не планируем вообще, если только в HUD не появятся 10+ реактивных полей. Controller↔View + `UpdateWindow()` достаточно |
| **Dimmed overlay под Additional** | Phase 2. Стандартный Blocker глобально — достаточно для MVP |
| **Кастомные команды** (`AbstractCommand`/`OpenAndWaitClosingWindowCommand`) | Не нужны: `ShowAsync` + `WaitForCloseAsync` покрывают |
| **Develop-слой UI поверх рабочего слоя** | Phase 2. Сейчас читы будут через отдельный debug-canvas |

---

## 16. Этапы реализации

### Phase 0 — каркас (этот спринт)

1. Sorting Layers в Project Settings (`UI_Hud`, `UI_Main`, `UI_Additional`, `UI_System`, `UI_Develop`)
2. Папка `Assets/Game/Core/UI/...` + asmdef `Game.UI` (зависимости: VContainer, UniTask, Addressables)
3. Базовые типы: `WindowType` (enum), `WindowLayer` (enum), `WindowAttribute`, `IWindowController`, `WindowController<TView>`, `WindowView`, `WindowArgs`
4. `UIManager` + `UIStack` + `UISortingController` + `AddressablesWindowFactory` + `UIStorage`
5. `UiSystemVContainerBindings.RegisterUiSystem()`, подключение в `BootstrapInstaller`
6. Префаб `UIManagerCanvas` под DontDestroyOnLoad (HudRoot, WindowsRoot, Blocker)

### Phase 1 — пилотные окна

7. Settings (Page, Main, keepInCache=false)
8. Shop (Page, Main, keepInCache=true) — проверка кеша
9. ConfirmDialog (Popup, Additional) — проверка модальности и fluent `AsAdditional()`
10. NoInternetWindow (Page, System) — проверка слоя System поверх всего
11. HUD — миграция GameplaySceneController в HudRoot

### Phase 2 — обогащение по мере необходимости

- Props-as-SO для ConfirmDialog (если вариаций станет ≥ 5)
- Dimmed overlay parent для Additional
- Pre-warm часто открываемых окон
- Develop-слой с примерами окон

### Phase 3 — миграция legacy

12. Перенос `com.dovhopolov.uisystem` исходников в `Assets/Game/Core/UI/Legacy/` (если нужно — только то, что ещё не переписано)
13. Удаление зависимости от пакета `com.dovhopolov.uisystem`

---

## 17. Открытые мелочи (решаем по мере появления)

- Анимации: пока fade-in/out на UniTask + DOTween (если уже в проекте) или просто `CanvasGroup.alpha` + `UniTask.Delay`. Решим в момент написания первой `WindowAnimation`.
- Куда складываются ошибки при `LoadAssetAsync` фейле — экран `LoadFailedWindow` (System) или Debug.LogError + silent return? Скорее всего first option.
- Имя метода: `Show / Hide` (как в UISystem) vs `Open / Close` (как в подходе A). Голос за **`Show/Hide`** — меньше переименований при миграции.
- Args без подкласса: разрешать `ShowAsync<T>()` без args (создаст пустой `WindowArgs` под капотом) — да.

---

## 18. Готовый scope MVP (для прикидки нагрузки)

Из обсуждения:
1. Settings
2. Shop
3. Shop after day start / end (newspaper)
4. Inventory
5. Dialog (Popup)
6. Map (5 points) — Page
7. Book selection before day — Page
8. Unique offers — Popup
9. Collections — post-MVP
10. Restoration — post-MVP

**Кандидаты на per-window cache** (keepInCache=true): Shop, Inventory, Map, Book selection.
**Кандидаты на Props-as-SO в Phase 2:** Dialog (если будут вариации копирайта/иконок), Unique offers (если будет несколько активных одновременно).

Ориентация: **только portrait**. Анимации: **только fade-in/out**.

---

## 19. Следующий шаг

Phase 0, шаги 1-6. Перед стартом — review этого документа и финальный пас по §17 (мелочи).
