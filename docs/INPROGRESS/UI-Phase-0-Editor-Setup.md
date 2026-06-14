# UI Phase 0 — ручные шаги в Unity Editor

Эти шаги нельзя сделать из кода — нужны вкладки Project Settings, Inspector и Addressables. Код Phase 0 уже на месте, осталось собрать ассеты и связать их.

## 1. Sorting Layers

**Edit → Project Settings → Tags and Layers → Sorting Layers**, добавить **в этом порядке** (порядок = z-приоритет, ниже список = выше отрисовка):

1. `UI_Hud`
2. `UI_Main`
3. `UI_Additional`
4. `UI_System`
5. `UI_Develop`

`Default` оставить выше `UI_Hud` (or wherever Unity положил его — не важно, у нас на каждом окне `Canvas.overrideSorting = true`).

## 2. Префаб `UIManagerCanvas`

Путь: `Assets/Game/Core/UI/Prefabs/UIManagerCanvas.prefab`.

Иерархия:
```
UIManagerCanvas                 ← root, Canvas + CanvasScaler + GraphicRaycaster + UICanvasRoot
├── HudRoot                     ← RectTransform (anchors stretch, offsets 0)
├── WindowsRoot                 ← RectTransform (anchors stretch, offsets 0)
└── Blocker                     ← Image (alpha 0, raycast target ON), disabled by default
```

Настройки на root:
- **Canvas**: Render Mode = `Screen Space - Overlay`, Sort Order = `0`, Sorting Layer = `Default`
- **CanvasScaler**: UI Scale Mode = `Scale With Screen Size`, Reference Resolution = `1080 × 1920`, Match = `1` (Height)
- **GraphicRaycaster**: дефолт
- **UICanvasRoot** (наш компонент):
  - `Hud Root` → ссылка на child `HudRoot`
  - `Windows Root` → ссылка на child `WindowsRoot`
  - `Blocker` → ссылка на child `Blocker` (GameObject)

После создания префаба:
- **Удалить** объект из сцены — он будет инстанцироваться через DI как DontDestroyOnLoad

## 3. Привязка префаба в BootstrapInstaller

1. Открыть `BootstrapInstaller` asset (под `Script Installers` на `GlobalLifetimeScope.prefab`)
2. Поле `Ui Canvas Root Prefab` → перетащить `UIManagerCanvas.prefab`

## 4. Префабы для smoke-test

Создать три простых префаба в `Assets/Game/Core/UI/SmokeTest/Prefabs/`:

| Префаб | Компоненты на root |
|---|---|
| `SmokeMainPage.prefab` | RectTransform + Canvas + CanvasGroup + `SmokeWindowView` + `FadeAnimation` + Image (фон, цвет на выбор) |
| `SmokeAdditionalPopup.prefab` | то же + Image меньше + TextMeshPro надпись "Popup" |
| `SmokeSystemDialog.prefab` | то же + Image меньше + TextMeshPro надпись "System" |

Для каждого:
- На `WindowView` инспектор: поле `Animation` → перетащить `FadeAnimation` с того же объекта
- `Canvas` остаётся без `overrideSorting` — система выставит сама
- `CanvasGroup.alpha = 1` (FadeAnimation сбросит в 0 при show)

## 5. Addressables

**Window → Asset Management → Addressables → Groups**, добавить **в одну группу** (например, `UI` или `Default Local Group`):

| Asset | Address |
|---|---|
| `UIManagerCanvas.prefab` | `Prefabs/UI/UIManagerCanvas` *(используется только если хочешь грузить через Addressables; сейчас передаётся через SerializeField — оставляем без адреса, не критично)* |
| `SmokeMainPage.prefab` | `Prefabs/UI/SmokeTest/SmokeMainPage` |
| `SmokeAdditionalPopup.prefab` | `Prefabs/UI/SmokeTest/SmokeAdditionalPopup` |
| `SmokeSystemDialog.prefab` | `Prefabs/UI/SmokeTest/SmokeSystemDialog` |

Адреса в таблице должны **точно** совпадать со строками в `[Window]` атрибутах смокового кода.

## 6. Запуск

1. Открыть `Boot` сцену в Editor (или ту сцену, с которой запускается `GlobalLifetimeScope`)
2. Press **Play**
3. В Hierarchy под `DontDestroyOnLoad` появится `UIManagerCanvas(Clone)`
4. Через ~1 сек в логах:
   ```
   [Smoke] runner waiting 1s before first window
   [Smoke] SmokeMainPage ShowStart
   [Smoke] SmokeMainPage ShowComplete
   ...
   [Smoke] runner finished successfully
   ```
5. Визуально три окна должны последовательно появиться через fade-in и затем закрыться

## 7. Проверка сортировки

В Play mode выбрать любое из smoke-окон в Hierarchy. На его `Canvas`-компоненте:
- `Override Sorting` = ✓
- `Sorting Layer` = соответствующий (`UI_Main` / `UI_Additional` / `UI_System`)
- `Order in Layer` = 10 / 20 / 30 (порядок инкремента)

## 8. Удаление smoke-test после проверки

Когда Phase 0 проверена, удалить:
- `Assets/Game/Core/UI/SmokeTest/` (вся папка)
- `Assets/Game/Core/UI/SmokeTest/Prefabs/` префабы и Addressables-записи к ним
- `Assets/Game/Core/Installers/Features/UiSmokeTestVContainerBindings.cs`
- В `BootstrapInstaller.cs` строку `builder.RegisterUiSmokeTest();`

После этого можно стартовать Phase 1 (реальные окна).
