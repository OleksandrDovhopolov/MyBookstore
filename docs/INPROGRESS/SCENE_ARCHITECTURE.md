# Архитектура сцен и Hub

Живой документ. Фиксирует архитектурное решение по структуре сцен + UI и набор окон, открывающихся из хаба. Обновляется по мере итераций.

Связано: [LOCATION_BUILDING.md](LOCATION_BUILDING.md), [CORE_LOOP.md](../CORE_LOOP.md), [UI_SYSTEM.md](../UI_SYSTEM.md).

---

## 1. Решение — Hybrid

**3 сцены + diegetic Hub + UI Windows как «развороты» внутри хаба.**

Это паттерн cozy-жанра (Tiny Bookshop, Spiritfarer, Cozy Grove, A Little to the Left): хаб — это «дом» (эмоциональный якорь), функционал открывается окнами поверх хаба, тяжёлые сцены (Sales location) грузятся отдельно.

| Слой | Реализация |
|---|---|
| **Хаб** | Сцена с фоном-интерьером фургона. Persistent в Morning/Preparation/Results |
| **Точки входа** | Diegetic interactable spots (книжная полка, карта, ледгер, газета, ...) с glowing-icon маркерами affordance |
| **Функционал** | UI Window'ы через `UIManager` поверх хаба — Newspaper, Ledger, Map, Bookshelf, Decor, Shop, Inventory |
| **Sales day** | Отдельная сцена с outdoor-локацией (Promenade / Lighthouse / ...) через Addressables |

---

## 2. Рассмотренные альтернативы

### Вариант 1 — UI-hub (отклонён)

Сцена-карта/хаб со стандартным UI, отдельные сцены под Decor и Preparation.

❌ Убивает атмосферу cozy-жанра — «меню → экран → меню → экран».
❌ Каждая смена сцены = стуттер на мобиле (даже с Addressables).
❌ Игнорирует визуальный язык Tiny Bookshop (бумажные ярлычки, чек-листы, рукописный декор).

### Вариант 2 — Всё diegetic, без UI (отклонён)

Каждая функция встроена в фургон визуально, без полноэкранных окон.

❌ Map view физически не помещается на стене фургона без zoom-камеры (это 2 недели работы).
❌ Инвентарь книг 60+ позиций — это список, не «полка из 12 книг».
❌ Decor placement требует drag-drop сетки слотов — diegetic не работает.
❌ Каждая новая фича (крафт, BattlePass, ...) требует выделить место в каравану — не масштабируется.

### Hybrid (выбран)

✅ Diegetic affordance (точки входа) сохраняет атмосферу.
✅ UI Windows для собственно функционала — масштабируется на любые будущие фичи.
✅ Windows стилизуются под бумагу/дерево/блокнот → не ломают визуальный язык.
✅ Это то, как реально сделана Tiny Bookshop: glowing icons над книжной полкой, картой, ледгером в её хабе — это и есть UI affordance markers.

---

## 3. Сцены (3 total)

| Сцена | Роль | Состояние |
|---|---|---|
| `BootScene` | Загрузка | Существует |
| `HubScene` | Интерьер фургона. Persistent в Morning / Preparation / Results | **Новая.** Заменяет текущий setup, где Morning/Preparation/Results живут как scene-placed views в `GameplayScene` |
| `LocationScene` | Outdoor-локация для Sales-дня (Promenade / Lighthouse / Beach / ...) | Существует как `GameplayScene` — перепрофилируется |

### Переходы

```
BootScene
  ↓ (после warmup)
HubScene  ←──────────────────────────┐
  ↓ (тап «Drive out» в Preparation)  │
LocationScene (Sales day)            │
  ↓ (день завершён)                  │
HubScene + auto-open LedgerWindow ───┘
```

### Анимация перехода

Hub ↔ Location переход должен быть «уютным», не блёклым fade-out. Закладывается через `ITransitionAnimationService` (в roadmap UI_SYSTEM). Возможные варианты: перелистывание страницы / закрытие окна каравана / fade через тёплый glow.

---

## 4. UI Windows над HubScene

Каждое окно открывается тапом по diegetic-элементу в HubScene. Не отдельные сцены — обычные `WindowController` через `UIManager`.

| Window | Diegetic-точка входа | Содержание | Источник |
|---|---|---|---|
| `NewspaperWindow` | Газета на столе | Утренние новости + Newspaper-shop | ✅ существует |
| `LedgerWindow` | Открытый блокнот «Today's Ledger» | Итоги дня (Results) + история дней | **Новый** — заменяет `ResultsScreenView` |
| `MapWindow` | Карта на стене | Региональная карта + выбор завтрашней локации | **Новый** — часть текущей Preparation |
| `BookshelfWindow` | Книжная полка | Инвентарь книг + выбор N книг на завтра | **Новый** — часть Preparation |
| `DecorPlacementWindow` | Ящик «New Arrivals» / свободный горшок | Выбор декор-слотов на завтра | **Новый** — часть Preparation |
| `ClassicShopWindow` | Радио / отдельный пункт | Магазин букиниста | ✅ существует |
| `InventoryWindow` | Корзина под столом | Полный инвентарь | ✅ существует |
| `RewardsWindow` | Открывается автоматически после покупки/успеха | Награды | ✅ существует |

**Стилизация окон:** все окна оформляются как **diegetic объекты** (страница блокнота, чек-лист на бумаге, чалкбоард-меню, разворот книги). НЕ как обычный мобильный UI с прямоугольными панелями.

---

## 5. Loop с этой архитектурой

```
Boot → HubScene (load player save)

Morning phase:
  • Тап Newspaper → NewspaperWindow → закрыть
  • Тап Ledger → LedgerWindow (вчерашние итоги) → закрыть
  • Кнопка-affordance «Continue to Preparation»
    (или автоматически после прочтения газеты — UX-решение позже)

Preparation phase:
  • Тап Map → MapWindow → выбрал локацию → закрыть
  • Тап Bookshelf → BookshelfWindow → выбрал книги → закрыть
  • Тап Decor crate → DecorPlacementWindow → разложил декор → закрыть
  • Тап «Drive out» (руль / ключи / дверь фургона) — финальный коммит

→ HubScene unload, LocationScene load (Addressables по выбранной локации)

Sales phase:
  • LocationScene активна, игрок видит локацию + припаркованный фургон
  • Пассивные продажи + active recommendations (см. CORE_LOOP §3)

→ LocationScene unload, HubScene reload

Results phase:
  • HubScene активна
  • LedgerWindow автоматически открыт со свежими итогами
  • Кнопка «Next Day» в окне

→ Loop повторяется
```

---

## 6. Импакт на существующий код

### `DayPhase` enum

Остаётся как state machine (логика «что игрок может/не может делать»). Больше **не управляет переключением scene-placed views** — управляет тем, **какие точки в HubScene подсвечены / активны**.

| `DayPhase` | Активно в Hub |
|---|---|
| `Morning` | Newspaper, Ledger (если есть итоги вчерашнего дня) |
| `Preparation` | Map, Bookshelf, DecorCrate + кнопка «Drive out» |
| `Sales` | (игрок в `LocationScene`, хаб выгружен) |
| `Results` | Ledger автоматически открыт с новыми итогами + «Next Day» |

### Что удаляется / переезжает

- `MorningScreenView` → удалить. Логика морнинг-резолва остаётся в `MorningSessionService`, но screen не рисуется (контент уходит в `NewspaperWindow`).
- `PreparationScreenView` → удалить. Логика — в `MapWindow` + `BookshelfWindow` + `DecorPlacementWindow`.
- `ResultsScreenView` → удалить. Контент в `LedgerWindow`.
- `SalesScreenView` → **остаётся** в `LocationScene` (это в Sales-сцене и есть основной gameplay UI).

### VContainer scopes

```
GlobalLifetimeScope (Save, Configs, Inventory, Resources, Progression, UI, Audio — переживают смены сцен)
 ├── HubLifetimeScope (живёт пока загружена HubScene)
 │    ├── HubInteractableRouter
 │    ├── HubAffordanceController (подсветка по DayPhase)
 │    └── Window controllers (открываются по требованию)
 └── LocationLifetimeScope (живёт пока загружена LocationScene)
      ├── ICustomerSpawner
      ├── SalesDayController
      ├── LocationContext (слоты декора, шелф-якоря — см. LOCATION_BUILDING §5)
      └── Location-specific services
```

### Save / state

Без изменений. Save-модули уже модульные:
- `PreparationSession.SaveState` коммитится при «Drive out».
- `SalesDayResult` коммитится по завершении Sales-дня.
- `DayProgressService` управляет фазой и счётчиком дней.

---

## 7. HubScene — компоненты

### `HubBackgroundView` (MonoBehaviour)

Spritе-фон интерьера фургона. Один большой sprite (либо несколько слоёв: задник, стол с предметами, foreground props). Пока — placeholder на сгенерированной картинке.

### `HubInteractableSpot` (MonoBehaviour, N экземпляров)

```
[Поля]
- string Id (newspaper / ledger / map / bookshelf / decor_crate / shop / inventory)
- Collider2D (tap area)
- GameObject GlowIcon (child — glowing icon affordance)
- UnityEvent OnTap

[Поведение]
- Принимает OnPointerClick через TapInputRouter (см. LOCATION_BUILDING §9 — interactables)
- HubAffordanceController устанавливает GlowIcon.SetActive(bool) по текущему DayPhase
```

### `HubInteractableRouter` (plain C#)

Подписан на `OnTap` всех `HubInteractableSpot`. Маршрутизирует Id → конкретный `UIManager.ShowAsync<TWindow>`.

```csharp
// псевдокод
switch (id) {
    case "newspaper":    _ui.ShowAsync<NewspaperWindow>(...);
    case "ledger":       _ui.ShowAsync<LedgerWindow>(...);
    case "map":          _ui.ShowAsync<MapWindow>(...);
    case "bookshelf":    _ui.ShowAsync<BookshelfWindow>(...);
    case "decor_crate":  _ui.ShowAsync<DecorPlacementWindow>(...);
    case "shop":         _ui.ShowAsync<ClassicShopWindow>(...);
    case "inventory":    _ui.ShowAsync<InventoryWindow>(...);
}
```

### `HubAffordanceController` (IStartable)

Слушает `IDayProgressService.PhaseChanged`. По текущей фазе вызывает `SetActive` у glow-иконок соответствующих интерактивных точек.

### `DriveOutButton` (MonoBehaviour)

Кнопка «выехать на день» — diegetic (руль фургона / ключи / дверь). Видна только в `Preparation` фазе + Preparation validated (`MinDailyBooks` выбраны, локация выбрана). Тап → `PreparationSessionService.ConfirmAsync` → `SceneTransitionService.LoadAsync(LocationScene)`.

---

## 8. Антипаттерны (что НЕ делать)

❌ **Scene per window.** `MapScene`, `DecorScene` отдельными сценами — это Вариант 1. Стуттер + потеря состояния хаба + перегрузка save системы.

❌ **Всё diegetic без UI.** Соблазнительно, но не масштабируется на функции с большим объёмом данных (инвентарь 60+ позиций, региональная карта с zoom).

❌ **HubScene additive поверх LocationScene** или наоборот. Чистая смена single-scene проще и предсказуемее.

❌ **Возвращение Morning/Preparation/Results как under-screens хаба.** Это вернёт UI-hub архитектуру. Diegetic affordance + окна — единственный путь.

❌ **UI Windows как «обычные мобильные панели» с прямоугольными контейнерами.** Они должны выглядеть как страница блокнота / чалкбоард / разворот книги.

---

## 9. Риски и mitigation

| Риск | Mitigation |
|---|---|
| HubScene background image требует много арта (всё в одном кадре) | Принять — это hero-asset проекта. Стартовать с AI-плейсхолдера, финал — у художника |
| Affordance discoverability — игрок не понимает, куда тапать | Glowing icon markers (как на референсе) при появлении фазы. FTUE первой сессии «тапни сюда» |
| Добавление новых функций в будущем — куда впихнуть в фургоне | Заложить «гибкие точки»: радио, окно вид наружу, ящик `New Arrivals`. Каждая может стать новым interactable |
| Анимация перехода Hub ↔ Location | Закладывается через `ITransitionAnimationService` (в roadmap UI_SYSTEM) |
| Сцена Hub статична — может надоесть | Future: мелкие анимации (огонь в лампе мигает, листья за окном падают, кот спит на стуле), смена дневного цикла (утро/вечер) |
| Hot-reload локации без перезагрузки Hub | LocationScene грузится через Addressables по `LocationConfig.PrefabAddress` — реконструкция дешёвая |

---

## 10. Конкретные next steps

1. ✅ **Архитектура зафиксирована** (этот документ).
2. ⬜ Создать заглушку `HubScene` (пустая сцена) и `HubBackgroundView` (placeholder — текущая reference картинка фургона).
3. ⬜ Создать `HubInteractableSpot` MonoBehaviour. Расставить ~7 экземпляров по сцене.
4. ⬜ Создать `HubInteractableRouter` + `HubAffordanceController`.
5. ⬜ Создать заготовки 3 новых окон: `LedgerWindow`, `MapWindow`, `BookshelfWindow`, `DecorPlacementWindow`. Пока пустые с заголовком.
6. ⬜ Перенести логику текущих `MorningScreenView` / `PreparationScreenView` / `ResultsScreenView` в эти окна (по одному).
7. ⬜ Прибить `GameplayScene` → переименовать в `LocationScene`. Удалить из неё всё, что не Sales.
8. ⬜ `SceneTransitionService` уже есть — использовать для Hub ↔ Location смены.
9. ⬜ Smoke-test loop: Boot → Hub → клик Map → выбор → клик Drive Out → Location → день → Hub + Ledger.

---

## 11. Открытые вопросы (будут наполняться)

- **Кнопка перехода Morning → Preparation** — отдельная affordance или автоматически после прочтения газеты?
- **Какой interactable отвечает за Shop** (Newspaper-shop живёт в `NewspaperWindow`, а `ClassicShopWindow`)? Радио? Дверь? Отдельный «Bookkeeper» NPC через окно фургона?
- **HubScene меняется день/ночь?** Простой подмен освещения через 2D Lights, либо отдельные varianty фона.
- **Стиль перехода Hub ↔ Location** — перелистывание страницы / закрытие окна каравана / fade через тёплый glow? Решить после первой готовой локации.
- **Tutorial / FTUE первой сессии** — как объяснить игроку, что glowing icons кликабельны? Стрелка-указатель? Bubbletext? Animated cursor?
- **«Hero asset» HubScene background** — рисовать одной картинкой или по слоям? Слоистый дороже, но даёт мелкие анимации (лампа мигает, листья падают).

---

## Связано

- [LOCATION_BUILDING.md](LOCATION_BUILDING.md) — построение outdoor-локаций (LocationScene)
- [CORE_LOOP.md](../CORE_LOOP.md) — фазы дня и игровой цикл
- [UI_SYSTEM.md](../UI_SYSTEM.md) — Window framework, через который реализуются все окна хаба
- [DECOR.md](../DECOR.md) — декор-слоты, источник для `DecorPlacementWindow`
- [INVENTORY.md](../INVENTORY.md) — инвентарь книг, источник для `BookshelfWindow`
- [SHOP.md](../SHOP.md) — Newspaper / Classic Shop windows
