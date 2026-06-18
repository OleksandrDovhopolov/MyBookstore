# World HUD — система Bubbles (баблы над объектами сцены)

## Что это такое

**Bubbles** — система UI-элементов (подсказок, таймеров, индикаторов), которые отображаются над объектами игровой сцены в мировом пространстве (world-space canvas). Они привязаны к `LocationObject`-ам, следят за их позицией через `IsoObject`, умеют прятаться/показываться, поддерживают анимированное появление и могут взаимодействовать с тапом.

Система живёт в двух местах:
- **Интерфейс и логика View** — `Assets/Game/UI/Bubbles/`
- **Данные, менеджмент и резолверы видимости** — `Assets/Game/Expeditions/View/Bubbles/`

---

## Архитектура. Ключевые классы

### 1. `BubbleData` — модель данных
`Assets/Game/Expeditions/View/Bubbles/Data/BubbleData.cs`

Абстрактный базовый класс для всех данных бабла. Хранит:
- `LocationObjectView` — вью объекта сцены, к которому привязан бабл
- `TipType` (`ObjectTipType`) — тип подсказки (Pointer, Enemy, Timer, Talk, DropInfo и т.д.)
- `PrefabAddressRef` — Addressable-ссылка на префаб View
- `CloseAfterSeconds` — время автозакрытия (0 = бессрочно)
- `CanCloseByTap` — закрывать ли по тапу
- `MaxVisibilityDistance` — максимальная дистанция видимости
- `CloseOnStateExit` — закрывать ли при смене состояния объекта

Каждый конкретный тип бабла имеет свой Data-класс: `TalkBubbleData`, `TimerBubbleData`, `EnemyBubbleData`, `PointerBubbleData`, `DropCostBubbleData`, `FactoryCraftBubbleData` и ещё ~20 наследников.

---

### 2. `AbstractBubbleView<TData>` — базовый View
`Assets/Game/UI/Bubbles/AbstractBubbleView.cs`

Generic MonoBehaviour. Реализует `IBubbleView`, `ITappableView`, `IScreenPositionSource`.

**Позиционирование:**
- В базовом (`Static`) режиме — один раз ставит `RectTransform.position = LocationObjectView.GetBubblePosition()` при `SetData`.
- В динамическом (`Dynamic`) режиме — каждый `LateUpdate` пересчитывает позицию через `SetDynamicPositionAndRotation()`:
  1. Переводит world-позицию объекта в screen-пространство
  2. Если бабл уходит за край вьюпорта — `BubbleDynamicZone` сдвигает его в видимую зону
  3. Бабл поворачивается стрелкой в сторону реального положения объекта (`_rotationRoot.rotation`)
  4. Глубина по Z фиксирована: `Z_DEPTH = -4.2f`

**Lifecycle:**
```
Prepare(container) → SetData(data) → SetupData() → SetVisibility(true)
                                                    ↓
                                             SetVisibilityAnimated()
                                             _appearController.SetVisibilityAnimated()
                                                    ↓
                                             Close() → BubbleManagerRemoveRequestSignal
```

**Поведение при тапе (`ITappableView.HandleTap`):**
- Если объект за пределами вьюпорта — центрирует камеру
- Иначе — делегирует тап на родительский `ITappableView` объекта (вызывает `Button.onClick`)

**Метод переопределения поведения:** подкласс реализует `SetupData()` (обновить UI по `Data`) и опционально `OnShowEnd()`, `OnCloseStart()`.

---

### 3. `BubbleContainer` — контейнер на сцене
`Assets/Game/UI/Bubbles/BubbleContainer.cs`

MonoBehaviour на игровом объекте сцены. Реализует `IBubbleContainer`.

Привязывает UI-бабл к изометрическому объекту:
- Хранит `IsoObject` (компонент движка IsoTools)
- Каждый `Update` обновляет `isoObject.position = ObjectView.Position` и `isoObject.size`
- `BubbleParent` — это дочерний `Transform`, в котором будет создан префаб View

Пул: контейнеры не уничтожаются, а возвращаются в `IAddressablesObjectsPoolService`.

---

### 4. `BubblesManager` — оркестратор
`Assets/Game/Expeditions/View/Bubbles/BubblesManager.cs`

MonoBehaviour. Центральный менеджер всей системы для одной локации (`LocationView`).

**Отвечает за:**
- Хранение соответствия `ILocationObject → IBubbleView` в словаре `_objectsBubbles`
- Создание/удаление баблов через `IAddressablesObjectsPoolService` (async, UniTask)
- Обновление видимости через `BubblesVisibilityResolver`
- Реакцию на ~25 сигналов шины событий: `ObjectStateChanged`, `ShowObjectCustomTipSignal`, `MiningProcessStartSignal`, `EnqueueCraftProcessSignal`, `LocationObjectBattleStartedSignal` и др.

**Процесс создания бабла (`CreateBubbleAsync`):**
1. Удалить текущий бабл объекта (`RemoveBubble`)
2. Асинхронно взять `BubbleContainer` из пула → `container.Setup(data)`
3. Асинхронно взять View-префаб из пула → `bubbleView.Prepare(container)`
4. Зарегистрировать `_objectsBubbles[obj] = bubbleView`
5. `SetBubbleData(bubbleView, data)` → `UpdateBubbleVisibility`

**Приоритеты при замене:** если объект уже имеет бабл, `NeedSkipTipBubble()` решает, нужно ли пропустить новый запрос, заменить существующий (`ActiveBubbleRule.Replace`) или пропустить если уже видим (`ActiveBubbleRule.Skip`).

**Скрытие во время боя:** при `LocationObjectBattleStartedSignal` бабл немедленно скрывается без анимации.

---

### 5. `DefaultBubbleDataProvider` — фабрика данных по умолчанию
`Assets/Game/Expeditions/View/Bubbles/DefaultBubbleDataProvider.cs`

Статический класс. `CreateDefault(LocationObjectView)` — определяет, какой `BubbleData` нужен объекту «по умолчанию», проверяя состояние объекта в приоритетном порядке:

| Приоритет | Условие | Тип бабла |
|---|---|---|
| 1 | Блок активации | `InteractionBlockBubbleData` |
| 2 | Growing активен | `GrowingTimerBubbleData` |
| 3 | TipIcon в состоянии | `SimpleBubbleData` / `SnapSizeSimpleBubbleData` |
| 4 | Здание (Building) | `BuildingTimerBubbleData`, `DungeonBubbleData`, `SummonBubbleData`, `MiningBubbleData`, `FactoryCraftBubbleData`, `BuildingBadgeBubbleData`... |
| 5 | Крафт | `CraftBubbleData` |
| 6 | Repair | `RepairObjectBubbleData` |
| 7 | Dynamite | `DynamiteBubbleData` |
| 8 | Quest NPC | `QuestNpcBubbleData` |
| 9 | TipIcon | `SimpleBubbleData` |
| 10 | Таймер на объекте | `TimerBubbleData` |
| 11 | Враг | `EnemyBubbleData` / `EnemyBubbleSelectorData` |
| 12 | ProcessTimer блок | `InteractionTimerBlockBubbleData` |

---

### 6. `BubblesVisibilityResolver` — мульти-резолвер видимости
`Assets/Game/Expeditions/View/Bubbles/VisibilityResolvers/BubblesVisibilityResolver.cs`

Агрегирует цепочку `IBubbleVisibilityResolver`. Бабл показывается только если **все** резолверы вернули `true`.

Порядок резолверов (от быстрого к медленному):

| # | Класс | Что проверяет |
|---|---|---|
| 1 | `VisibilityKeepInvisibleResolver` | Бабл принудительно скрыт сигналом |
| 2 | `VisibilityResolverByFogCover` | Объект скрыт туманом |
| 3 | `VisibilityResolverByEnemyLock` | Объект заблокирован врагом |
| 4 | `VisibilityResolverByWindows` | Открыто перекрывающее окно |
| 5 | `VisibilityResolverByShutters` | Открыт шаттер/занавес |
| 6 | `VisibilityResolverByInteraction` | Идёт взаимодействие с объектом |
| 7 | `VisibilityResolverByDistance` | Объект слишком далеко от камеры |
| 8 | `VisibilityResolverByDynamicBehaviour` | Dynamic-бабл вышел за порог `DynamicBubbleHideViewDist` |

Дополнительно: `_bubblesToForceKeepVisible` — сет баблов, которые всегда видны невзирая на резолверы (сигнал `SetBubbleKeepVisibleSignal`).

---

## Типы баблов (наследники `AbstractBubbleView`)

| View-класс | Data-класс | Назначение |
|---|---|---|
| `TimerBubbleView` | `TimerBubbleData` | Таймер обратного отсчёта |
| `TalkBubbleView` | `TalkBubbleData` | Текстовое сообщение NPC |
| `EmojiBubbleView` | `EmojiBubbleData` | Эмодзи над персонажем |
| `TalkWithEmojiBubbleView` | `TalkWithEmojiBubbleData` | Текст + эмодзи |
| `CloudEmojiBubbleView` | `CloudEmojiBubbleData` | Изображение в облаке |
| `PointerBubbleView` | `PointerBubbleData` | Стрелка-указатель |
| `PathPointerBubbleView` | `PathPointerBubbleData` | Указатель пути |
| `GroupPointerBubbleView` | `GroupPointerBubbleData` | Указатель на группу под туманом |
| `EnemyBubbleView` | `EnemyBubbleData` | Индикатор врага с силой |
| `EnemyInfoBubbleView` | `EnemyInfoBubbleData` | Подробная инфо о враге |
| `EnemyAutoFightBubbleView` | `EnemyAutoFightBubbleData` | Кнопка автобоя |
| `EnemyBubbleSelectorView` | `EnemyBubbleSelectorData` | Переключатель между бабл-врагами |
| `DropInfoBubbleView` | `DropInfoBubbleData` | Дроп ресурсов |
| `DropCostBubbleView` | `DropCostBubbleData` | Стоимость взаимодействия |
| `CompletionMarkBubbleView` | `CompletionMarkBubbleData` | Галочка завершения |
| `InteractionBlockBubbleView` | `InteractionBlockBubbleData` | Замок/блок взаимодействия |
| `InteractionTimerBlockBubbleView` | `InteractionTimerBlockBubbleData` | Таймер блока взаимодействия |
| `DynamiteBubbleView` | `DynamiteBubbleData` | Динамит / обратный отсчёт |
| `RepairObjectBubbleView` | `RepairObjectBubbleData` | Ремонт объекта |
| `StartEditObjectBubbleView` | `StartEditObjectBubbleData` | Начать редактирование |
| `QuestNpcBubbleView` | `QuestNpcBubbleData` | Квест-NPC |
| `GuestHeroBubbleView` | `GuestHeroBubbleData` | Гостевой герой |
| `BuildingTimerBubbleView` | `BuildingTimerBubbleData` | Таймер строительства |
| `CraftBubbleView` | `CraftBubbleData` | Крафт |
| `FactoryCraftBubbleView` | `FactoryCraftBubbleData` | Фабричный крафт (с очередью) |
| `MiningBubbleView` | `MiningBubbleData` | Добыча ресурсов |
| `LimitedMiningProduceBubbleView` | `LimitedMiningProduceBubbleData` | Ограниченная добыча |
| `LimitedMiningCompletedBubbleView` | `LimitedMiningCompletedBubbleData` | Добыча завершена |
| `DungeonBubbleView` | `DungeonBubbleData` | Подземелье — есть награда |
| `SummonBubbleView` | `SummonBubbleData` | Призыв возможен |
| `OrderBoardBubbleView` | `OrderBoardBubbleData` | Доска заказов |
| `BuildingBadgeBubbleView` | `BuildingBadgeBubbleData` | Бейдж на здание |
| `GrowingTimerBubbleView` | `GrowingTimerBubbleData` | Таймер роста |
| `SimpleBubbleView` | `SimpleBubbleData` | Иконка из TipIcon |

---

## Сигналы шины событий (вход/выход)

### Входящие (BubblesManager слушает)
| Сигнал | Действие |
|---|---|
| `ShowObjectCustomTipSignal` | Показать конкретный тип подсказки |
| `ShowObjectDefaultTipSignal` | Показать дефолтный бабл |
| `ShowObjectTalkSignal` | Показать Talk/Emoji бабл |
| `CloseBubbleSignal` | Закрыть бабл по типу |
| `RemoveBubbleSignal` | Удалить бабл из пула |
| `ResetBubbleSignal` | Пересоздать дефолтный бабл |
| `SetBubbleLockedSignal` | Заблокировать показ бабла |
| `SetBubbleKeepVisibleSignal` | Принудительно показать |
| `LocationObjectBattleStartedSignal` | Скрыть бабл на время боя |
| `LocationObjectBattleCompletedSignal` | Восстановить после боя |
| `TapEndedSignal` | Закрыть Talk-баблы с `CanCloseByTap` |
| `ActiveLocationChangedSignal` | Пересоздать баблы при смене локации |
| `MiningProcessStartSignal` / `...MinedResource` | Обновить mining-бабл |
| `EnqueueCraftProcessSignal` / `RemoveCraft...` | Обновить craft-бабл |
| и другие ~15 сигналов | ... |

### Исходящие
| Сигнал | Когда |
|---|---|
| `BubbleManagerRemoveRequestSignal` | Когда View просит себя удалить (конец анимации закрытия) |
| `BubbleClosedSignal` | После удаления бабла менеджером |

---

## Схема потока данных

```
GameEvent / Signal
       │
       ▼
 BubblesManager
       │  CreateDefault() / ShowTip()
       ▼
 DefaultBubbleDataProvider ──► BubbleData (конкретный)
       │
       ▼  async (UniTask)
 IAddressablesObjectsPoolService
       ├── BubbleContainer (IsoObject на сцене)
       └── AbstractBubbleView<TData> (UI)
               │
               ▼
       BubblesVisibilityResolver
       (8 резолверов в цепочке)
               │
               ▼
       SetVisibility(true/false)
       _appearController.SetVisibilityAnimated()
```

---

## Особенности

- **Пул объектов:** все контейнеры и View-префабы берутся из `IAddressablesObjectsPoolService` и возвращаются туда же при закрытии. Возврат в пул откладывается на конец кадра через `Loom.CallAtTheEndOfFrame`.
- **Race condition защита:** при async-создании бабла хранится `_objectsWithBubblesCreation`. Если за время ожидания состояние изменилось — объект возвращается в пул без показа.
- **Dynamic-режим:** бабл может выходить за пределы экрана и обновляться каждый `LateUpdate`. Угол стрелки вычисляется как `Vector3.SignedAngle(Vector3.down, toViewPoint, Vector3.forward)`.
- **Один бабл на объект:** на каждый `ILocationObject` одновременно активен не более одного `IBubbleView`.
- **Исключение из видимости:** баблы с флагом `ForceKeepVisible` игнорируют все резолверы.
