# World HUD

World-space UI над объектами игровой сцены: баблы/подсказки/индикаторы, привязанные к мировым
объектам и следящие за их позицией (billboard к камере, fade-in/out, тап-взаимодействие).

В MyBookstore фреймворк живёт в `Assets/Game/Core/WorldHud/`. Первый продакшен-потребитель —
**мысли-баблы покупателей** в SalesDay (`CustomerThoughtBubble` над `CustomerVisual`): состояния
Thinking → BookPicked/Comment/Rejection. Phase 0 (каркас + smoke-test + customer-баблы) реализован
в коде; ниже — ручная сборка в редакторе.

Документ состоит из двух частей:
1. **Phase 0 — Editor Setup** — что собрать руками в Unity для текущей реализации MyBookstore.
2. **Reference — система Bubbles** — архитектурный референс из соседнего проекта (heroes/Expeditions),
   на который опирается дизайн. Это **не** код MyBookstore — берём идеи, а не классы.

---

## Часть 1. Phase 0 — Editor Setup

Код Phase 0 готов. Эта инструкция собирает префабы, прописывает Addressables, добавляет камеру в сцену и привязывает поля Inspector'а.

После выполнения всех шагов: при запуске boot-сцены через 1 секунду в сцене появляется куб с баблом «Hello world» (smoke-test), а внутри SalesDay над каждым клиентом висит world-space бабл с состояниями Thinking/BookPicked.

### 1. Main Camera в GameplayScene

`Assets/Scenes/GameplayScene.unity` сейчас пустая. Добавляем камеру:

1. Открыть сцену
2. GameObject → Camera (имя по умолчанию «Main Camera»)
3. Tag: `MainCamera`
4. Position: `(0, 0, -10)`
5. Projection: **Orthographic**
6. Size: `5`
7. Clear Flags: `Solid Color`, цвет тёмно-серый
8. Сохранить сцену

Без камеры баблы с `Billboard=true` не смогут повернуться (`WorldHud.SnapToTarget` пропускает поворот если `Camera.main == null`, так что критической ошибки не будет, но баблы окажутся повёрнуты в сторону мирового forward).

### 2. Префаб `SmokeWorldHudBubble.prefab`

Путь: `Assets/Game/Core/WorldHud/Prefabs/SmokeWorldHudBubble.prefab`

Структура:
```
SmokeWorldHudBubble                    ← root
├── RectTransform (anchors center, Width 200, Height 80)
├── Canvas (Render Mode = World Space, Sort Order = 0)
├── CanvasScaler (Constant Pixel Size, Scale Factor 1)
├── GraphicRaycaster (раскастер можно оставить, но raycast target на компонентах — OFF)
├── CanvasGroup (alpha=1)
├── SmokeWindowView equivalent: SmokeWorldHudBubble компонент с серилайзед `_canvasGroup` и `_label`
└── child Image (фон-бабл, белый овал, raycast target OFF)
    └── child TextMeshPro - Text (UI)  «Hello world», raycast target OFF
        ← assign to SmokeWorldHudBubble._label
```

Важные настройки на root:
- root.localScale: **`(0.01, 0.01, 1)`** — стандартный масштаб для World Space UI, чтобы он смотрелся нормального размера рядом с обычными мировыми объектами (~1 unit = 1 метр)
- CanvasGroup.alpha = 1, blocksRaycasts = false, interactable = false

### 3. Префаб `CustomerVisualPlaceholder.prefab`

Путь: `Assets/Game/Features/BookSell/UI/Customer/Prefabs/CustomerVisualPlaceholder.prefab`

Структура:
```
CustomerVisualPlaceholder              ← root, scale (1,1,1)
├── SpriteRenderer (любой белый/цветной квадратный sprite, scale через GameObject scale 0.5×1)
├── CustomerVisual (наш компонент, _figure → ссылка на SpriteRenderer)
└── child empty GameObject "BubbleAnchor"  ← local position (0, 1.2, 0), пустой Transform
    ← assign to CustomerVisual._bubbleAnchor
```

Минимально — это просто белый прямоугольник 0.5×1 unit на высоте Y=0.5. Можно поставить что угодно визуально, важна только привязка `BubbleAnchor` к Transform над фигуркой.

### 4. Префаб `CustomerThoughtBubble.prefab`

Путь: `Assets/Game/Features/BookSell/UI/Customer/Prefabs/CustomerThoughtBubble.prefab`

Структура (World Space Canvas):
```
CustomerThoughtBubble                  ← root
├── RectTransform (Width 200, Height 150)
├── Canvas (Render Mode = World Space)
├── CanvasScaler (Constant Pixel Size)
├── GraphicRaycaster (raycast target всего content — OFF)
├── CanvasGroup (alpha=1)
├── CustomerThoughtBubbleView (наш компонент)
├── CustomerThoughtBubble (контроллер)
├── child Image "BubbleBackground" (овал-фон, raycast OFF)
└── 4 child GameObject'a — sub-views, каждый со своим CanvasGroup:
    ├── DotsGroup (CanvasGroup, alpha=0, gameObject inactive)
    │   └── TextMeshPro «...» или 3 Image dots
    ├── BookGroup (CanvasGroup, alpha=0, inactive)
    │   └── BookScaleTarget (empty Transform, scale (1,1,1))
    │       └── Image "BookIcon" (placeholder sprite)
    ├── CommentGroup (CanvasGroup, alpha=0, inactive)
    │   └── TextMeshPro "CommentText"
    └── RejectionGroup (CanvasGroup, alpha=0, inactive)
        └── RejectionScaleTarget (empty Transform)
            ├── Image "RejectedBookIcon" + красный крест поверх
            └── Image "ReplacementBookIcon" (рядом, меньшего размера)
```

Привязки в инспекторе на `CustomerThoughtBubbleView`:
- `Dots Group` / `Book Group` / `Comment Group` / `Rejection Group` → соответствующие 4 CanvasGroup
- `Book Icon` → Image внутри BookGroup
- `Book Scale Target` → BookScaleTarget Transform внутри BookGroup
- `Comment Text` → TMP_Text внутри CommentGroup
- `Rejected Book Icon` / `Replacement Book Icon` → Image-ы внутри RejectionGroup
- `Rejection Scale Target` → RejectionScaleTarget Transform внутри RejectionGroup

На root `WorldHud._canvasGroup` → корневой CanvasGroup.

root.localScale: **`(0.01, 0.01, 1)`** (как и smoke bubble).

### 5. Addressables

В Addressables Groups (Window → Asset Management → Addressables → Groups → группа `UI` или Default):

| Asset | Address |
|---|---|
| `SmokeWorldHudBubble.prefab` | `WorldHud/SmokeWorldHudBubble` |
| `CustomerThoughtBubble.prefab` | `WorldHud/CustomerThoughtBubble` |

`CustomerVisualPlaceholder.prefab` — **НЕ через Addressables**. Он передаётся через `[SerializeField]` на `GameInstaller` MonoBehaviour в GameplayScene.

### 6. Привязка префабов на GameInstaller

`GameInstaller` — MonoBehaviour, лежит на корне `GameplayLifetimeScope`'a в GameplayScene.unity. Найди его в Hierarchy, в инспекторе раздел `BookSell — World HUD Phase 0`:

- `Customer Visual Prefab` → перетащить `CustomerVisualPlaceholder.prefab` из Project
- `Customer Spawn Root` → опционально, пустой Transform в сцене как родитель для всех CustomerVisual (можно оставить None — будут спавниться в корень сцены)

### 7. Verification

#### Сценарий A — Smoke test (World HUD framework)

1. Press Play в boot-сцене
2. Загрузка завершается, переходим в GameplayScene
3. Через ~1 сек в Hierarchy появляется `WorldHudSmokeCube` в `(0, 0, 0)`
4. Через ~1.2 сек над ним появляется `SmokeWorldHudBubble(Clone)` с надписью «Hello world», плавно проявляется
5. Бабл следит за кубом (если в Scene view подвинуть куб — бабл едет следом)
6. Бабл смотрит лицом к камере (billboard)
7. Через ~4.2 сек fade-out, куб уничтожен
8. В Console: `[WorldHudSmoke] finished successfully`

Если возникает ошибка `No Location found for Key=WorldHud/SmokeWorldHudBubble` — проверь что Play Mode Script стоит на **`Use Asset Database (fastest)`** в Addressables Groups.

#### Сценарий B — CustomerThoughtBubble (нужен живой SalesDay)

1. Запустить SalesDay (через `SalesScreenView` debug-кнопку «Start Day» или аналог)
2. Когда первый клиент спавнится (`CustomerPhase.Spawned/Approaching`):
   - В сцене должен появиться `CustomerVisual(<id>)(Clone)` (белая фигурка)
   - Над ним — `CustomerThoughtBubble(Clone)` с активным sub-view DotsGroup (точки «...»)
3. Когда клиент переходит в `InMinigame`:
   - Бабл crossfade'ит из DotsGroup в BookGroup, виден placeholder book icon
4. Когда клиент `Leaving`:
   - Бабл fade-out, потом detach
5. Когда клиент `Done`:
   - Через ~2 сек CustomerVisual уничтожается

#### Сценарий C — One-bubble-per-target invariant

В Editor временно можно вызвать AttachAsync дважды на тот же Transform (через debug кнопку в WorldHudSmokeRunner):
- Hierarchy всегда показывает **один** бабл на target
- В консоли никаких ошибок

**Критерий «ОК World HUD Phase 0»:** A полностью проходит + B первые два состояния (Approaching → Browsing/InMinigame) видны.

### 8. После проверки — что удалить

Когда Phase 0 World HUD проверена и работает:

- `Assets/Game/Core/WorldHud/SmokeTest/` (вся папка)
- `Assets/Game/Core/Installers/Features/WorldHudSmokeTestVContainerBindings.cs`
- Префаб `SmokeWorldHudBubble.prefab` + Addressables запись
- В `BootstrapInstaller.cs` убрать `builder.RegisterWorldHudSmokeTest();`

CustomerThoughtBubble + CustomerVisual префабы — оставить, это уже продакшен Phase 0.

---

## Часть 2. Reference — система Bubbles (внешний проект)

> ⚠️ **Референс-документ из соседнего проекта (heroes / Expeditions) — НЕ описывает код MyBookstore.**
> Пути вида `Assets/Game/Expeditions/`, `IsoObject`/IsoTools, фичи mining/craft/dungeon/summon — оттуда.
> Это полноценная world-HUD система, на которую опирается дизайн нашего `Core/WorldHud`. Берём
> архитектурные идеи (data-driven баблы, цепочка резолверов видимости, пул, один бабл на объект),
> а не классы.

### Что это такое

**Bubbles** — система UI-элементов (подсказок, таймеров, индикаторов), которые отображаются над объектами игровой сцены в мировом пространстве (world-space canvas). Они привязаны к `LocationObject`-ам, следят за их позицией через `IsoObject`, умеют прятаться/показываться, поддерживают анимированное появление и могут взаимодействовать с тапом.

Система живёт в двух местах:
- **Интерфейс и логика View** — `Assets/Game/UI/Bubbles/`
- **Данные, менеджмент и резолверы видимости** — `Assets/Game/Expeditions/View/Bubbles/`

### Архитектура. Ключевые классы

#### 1. `BubbleData` — модель данных
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

#### 2. `AbstractBubbleView<TData>` — базовый View
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

#### 3. `BubbleContainer` — контейнер на сцене
`Assets/Game/UI/Bubbles/BubbleContainer.cs`

MonoBehaviour на игровом объекте сцены. Реализует `IBubbleContainer`.

Привязывает UI-бабл к изометрическому объекту:
- Хранит `IsoObject` (компонент движка IsoTools)
- Каждый `Update` обновляет `isoObject.position = ObjectView.Position` и `isoObject.size`
- `BubbleParent` — это дочерний `Transform`, в котором будет создан префаб View

Пул: контейнеры не уничтожаются, а возвращаются в `IAddressablesObjectsPoolService`.

#### 4. `BubblesManager` — оркестратор
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

#### 5. `DefaultBubbleDataProvider` — фабрика данных по умолчанию
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

#### 6. `BubblesVisibilityResolver` — мульти-резолвер видимости
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

### Типы баблов (наследники `AbstractBubbleView`)

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

### Сигналы шины событий (вход/выход)

#### Входящие (BubblesManager слушает)
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

#### Исходящие
| Сигнал | Когда |
|---|---|
| `BubbleManagerRemoveRequestSignal` | Когда View просит себя удалить (конец анимации закрытия) |
| `BubbleClosedSignal` | После удаления бабла менеджером |

### Схема потока данных

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

### Особенности

- **Пул объектов:** все контейнеры и View-префабы берутся из `IAddressablesObjectsPoolService` и возвращаются туда же при закрытии. Возврат в пул откладывается на конец кадра через `Loom.CallAtTheEndOfFrame`.
- **Race condition защита:** при async-создании бабла хранится `_objectsWithBubblesCreation`. Если за время ожидания состояние изменилось — объект возвращается в пул без показа.
- **Dynamic-режим:** бабл может выходить за пределы экрана и обновляться каждый `LateUpdate`. Угол стрелки вычисляется как `Vector3.SignedAngle(Vector3.down, toViewPoint, Vector3.forward)`.
- **Один бабл на объект:** на каждый `ILocationObject` одновременно активен не более одного `IBubbleView`.
- **Исключение из видимости:** баблы с флагом `ForceKeepVisible` игнорируют все резолверы.
