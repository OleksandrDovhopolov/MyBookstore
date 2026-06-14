# UI Phase 1 — ручные шаги в Unity Editor

Code Phase 1 готов. Осталось: собрать 2 префаба, прописать Addressables, добавить debug-панель на UIManagerCanvas, удалить Phase 0 smoke-артефакты.

---

## 1. Префаб `ConfirmDialog.prefab`

Путь: `Assets/Game/Core/UI/Common/Confirm/Prefabs/ConfirmDialog.prefab`.

Структура:
```
ConfirmDialog                     ← root
  ├── RectTransform (stretch full, offsets 0)
  ├── Canvas (Render Mode = Screen Space - Overlay в prefab edit; в рантайме станет nested)
  ├── CanvasGroup (alpha=1)
  ├── ConfirmDialogView (наш компонент)
  ├── FadeAnimation (Duration=0.25)
  ├── Backdrop                    ← child Image, полупрозрачный чёрный (alpha 0.5), stretch full, raycast target ON
  └── Panel                       ← child центральная панель (Image, 600×400, anchor center)
        ├── Title                 ← TMP_Text «Title»
        ├── Body                  ← TMP_Text «Body text…»
        ├── ConfirmButton         ← Button с подписью «OK»
        │     └── Label           ← TMP_Text внутри кнопки
        └── CancelButton          ← Button с подписью «Cancel»
              └── Label           ← TMP_Text внутри кнопки
```

На `ConfirmDialogView` через инспектор привязать:
- `Title Label` → Title (TMP_Text)
- `Body Label` → Body (TMP_Text)
- `Confirm Button` → ConfirmButton
- `Confirm Button Label` → Label внутри ConfirmButton
- `Cancel Button` → CancelButton
- `Cancel Button Label` → Label внутри CancelButton
- На базовом WindowView: `Animation` → FadeAnimation на корне

## 2. Префаб `SettingsWindow.prefab`

Путь: `Assets/Game/Core/UI/Common/Settings/Prefabs/SettingsWindow.prefab`.

Структура:
```
SettingsWindow                    ← root
  ├── RectTransform (stretch full)
  ├── Canvas + CanvasGroup
  ├── SettingsWindowView
  ├── FadeAnimation
  ├── Background                  ← child Image (любой цвет, не прозрачный)
  └── Content                     ← child Panel
        ├── OpenCounterLabel      ← TMP_Text «Open count: 0»
        ├── ConfirmResultLabel    ← TMP_Text «»  (пустой по умолчанию)
        ├── ResetButton           ← Button «Reset»
        ├── CloseButton           ← Button «Close»
        └── OpenConfirmButton     ← Button «Open Confirm»
```

На `SettingsWindowView`:
- `Open Counter Label` → OpenCounterLabel
- `Confirm Result Label` → ConfirmResultLabel
- `Reset Button` → ResetButton
- `Close Button` → CloseButton
- `Open Confirm Button` → OpenConfirmButton
- На базовом WindowView: `Animation` → FadeAnimation на корне

## 3. Addressables

В Addressables Groups добавить (в группу `UI` или Default), **с короткими адресами**:

| Asset | Address |
|---|---|
| `ConfirmDialog.prefab` | `UI/Common/ConfirmDialog` |
| `SettingsWindow.prefab` | `UI/Common/SettingsWindow` |

Адреса должны точно совпадать со строками в `[Window(...)]` атрибутах. Проверь, что Play Mode Script стоит на **`Use Asset Database (fastest)`**, чтобы изменения подхватывались без сборки.

## 4. Добавить `UiPilotDebugPanel` на `UIManagerCanvas`

Открыть `Assets/Game/Core/UI/Prefabs/UIManagerCanvas.prefab` (тот же что в Phase 0).

На корневой GameObject (где висит `UICanvasRoot`) → **Add Component → UiPilotDebugPanel**.

`UiPilotDebugPanel` помечен `#if UNITY_EDITOR || DEVELOPMENT_BUILD` — в release-сборке компонент скомпилируется в no-op (классы не будет совсем), но в префабе ссылка останется. Unity это переживёт (просто отсутствующий скрипт), но если хочешь чистоту — оставь как есть, в реальном release-build всё равно DEVELOPMENT_BUILD флаг можно включать.

## 5. Удалить Phase 0 smoke-артефакты

После того как Phase 1 окна работают, можно убрать:

**Файлы кода:**
- Удалить папку `Assets/Game/Core/UI/SmokeTest/` целиком
- Удалить `Assets/Game/Core/Installers/Features/UiSmokeTestVContainerBindings.cs`

**Префабы:**
- Удалить `Assets/Game/Core/UI/SmokeTest/Prefabs/SmokeMainPage.prefab`
- Удалить `Assets/Game/Core/UI/SmokeTest/Prefabs/SmokeAdditionalPopup.prefab`
- Удалить `Assets/Game/Core/UI/SmokeTest/Prefabs/SmokeSystemDialog.prefab`

**Addressables:**
- Удалить записи `SmokeMainPage`, `SmokeAdditionalPopup`, `SmokeSystemDialog` из группы UI

`BootstrapInstaller.cs` уже не вызывает `RegisterUiSmokeTest()` — этот шаг уже сделан.

## 6. Запуск и проверка

Press Play в Boot-сцене.

Через ~1 сек в левом-верхнем углу появятся 3 OnGUI-кнопки:
- **Show Settings**
- **Show Confirm (await)**
- **Hide Top**

### Сценарий A — Settings cache (валидирует `keepInCache=true`)

1. Show Settings → видна `Open count: 1`
2. Кликнуть Close (кнопка внутри Settings) → fade-out
3. Show Settings снова → **`Open count: 2`**. ✓ Кеш работает.
4. Reset → `Open count: 0`, переоткрыть → `Open count: 1`

### Сценарий B — ConfirmDialog WaitForResultAsync

1. Show Settings → Open Confirm
2. Поверх Settings появляется ConfirmDialog (sortingLayer = UI_Additional)
3. Confirm → диалог fade-out, в Settings label показывает `Result: Confirmed`
4. Open Confirm → Cancel → label `Result: Cancelled`

### Сценарий C — Auto-close детей

1. Show Settings → Open Confirm (открыт)
2. Кликнуть Hide Top **два раза**:
   - Первый Hide Top закроет ConfirmDialog (он в фокусе)
   - Второй Hide Top закроет Settings
3. Альтернативно: открыть Settings → Open Confirm → нажать кнопку Close в Settings (через какой-то extra-debug или прямо через Hide Top на Settings) → ConfirmDialog должен закрыться вместе с Settings (auto-close детей)

### Сценарий D — Sorting в Hierarchy

С активными Settings + Confirm в Hierarchy → `UIManagerCanvas(Clone)/WindowsRoot/`:
- `SettingsWindow(Clone)` → Canvas → sortingLayer = `UI_Main`, order = 10
- `ConfirmDialog(Clone)` → Canvas → sortingLayer = `UI_Additional`, order = 10

Если значения совпадают — sorting работает.

## 7. Что дальше

После прохождения сценариев A, B, D можно стартовать Phase 2 (миграция существующих экранов, ShopWindow, NoInternet wiring).
