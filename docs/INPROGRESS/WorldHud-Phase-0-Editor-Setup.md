# World HUD Phase 0 — Editor Setup

Код Phase 0 готов. Эта инструкция собирает префабы, прописывает Addressables, добавляет камеру в сцену и привязывает поля Inspector'а.

После выполнения всех шагов: при запуске boot-сцены через 1 секунду в сцене появляется куб с баблом «Hello world» (smoke-test), а внутри SalesDay над каждым клиентом висит world-space бабл с состояниями Thinking/BookPicked.

---

## 1. Main Camera в GameplayScene

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

## 2. Префаб `SmokeWorldHudBubble.prefab`

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

## 3. Префаб `CustomerVisualPlaceholder.prefab`

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

## 4. Префаб `CustomerThoughtBubble.prefab`

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

## 5. Addressables

В Addressables Groups (Window → Asset Management → Addressables → Groups → группа `UI` или Default):

| Asset | Address |
|---|---|
| `SmokeWorldHudBubble.prefab` | `WorldHud/SmokeWorldHudBubble` |
| `CustomerThoughtBubble.prefab` | `WorldHud/CustomerThoughtBubble` |

`CustomerVisualPlaceholder.prefab` — **НЕ через Addressables**. Он передаётся через `[SerializeField]` на `GameInstaller` MonoBehaviour в GameplayScene.

## 6. Привязка префабов на GameInstaller

`GameInstaller` — MonoBehaviour, лежит на корне `GameplayLifetimeScope`'a в GameplayScene.unity. Найди его в Hierarchy, в инспекторе раздел `BookSell — World HUD Phase 0`:

- `Customer Visual Prefab` → перетащить `CustomerVisualPlaceholder.prefab` из Project
- `Customer Spawn Root` → опционально, пустой Transform в сцене как родитель для всех CustomerVisual (можно оставить None — будут спавниться в корень сцены)

## 7. Verification

### Сценарий A — Smoke test (World HUD framework)

1. Press Play в boot-сцене
2. Загрузка завершается, переходим в GameplayScene
3. Через ~1 сек в Hierarchy появляется `WorldHudSmokeCube` в `(0, 0, 0)`
4. Через ~1.2 сек над ним появляется `SmokeWorldHudBubble(Clone)` с надписью «Hello world», плавно проявляется
5. Бабл следит за кубом (если в Scene view подвинуть куб — бабл едет следом)
6. Бабл смотрит лицом к камере (billboard)
7. Через ~4.2 сек fade-out, куб уничтожен
8. В Console: `[WorldHudSmoke] finished successfully`

Если возникает ошибка `No Location found for Key=WorldHud/SmokeWorldHudBubble` — проверь что Play Mode Script стоит на **`Use Asset Database (fastest)`** в Addressables Groups.

### Сценарий B — CustomerThoughtBubble (нужен живой SalesDay)

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

### Сценарий C — One-bubble-per-target invariant

В Editor временно можно вызвать AttachAsync дважды на тот же Transform (через debug кнопку в WorldHudSmokeRunner):
- Hierarchy всегда показывает **один** бабл на target
- В консоли никаких ошибок

**Критерий «ОК World HUD Phase 0»:** A полностью проходит + B первые два состояния (Approaching → Browsing/InMinigame) видны.

## 8. После проверки — что удалить

Когда Phase 0 World HUD проверена и работает:

- `Assets/Game/Core/WorldHud/SmokeTest/` (вся папка)
- `Assets/Game/Core/Installers/Features/WorldHudSmokeTestVContainerBindings.cs`
- Префаб `SmokeWorldHudBubble.prefab` + Addressables запись
- В `BootstrapInstaller.cs` убрать `builder.RegisterWorldHudSmokeTest();`

CustomerThoughtBubble + CustomerVisual префабы — оставить, это уже продакшен Phase 0.
