# GameFlowLoop — игровой цикл и переходы между сценами

Живой документ. Описывает верхнеуровневый игровой цикл и систему перехода между сценами.
Это «карта»: детали фаз и фич — в связанных доках (ссылки внизу и по тексту).

Связано: [CORE_LOOP.md](CORE_LOOP.md), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md),
[UI_SYSTEM.md](UI_SYSTEM.md), [FTUE.md](FTUE.md),
[INPROGRESS/SCENE_ARCHITECTURE.md](INPROGRESS/SCENE_ARCHITECTURE.md),
[INPROGRESS/LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md),
[adr/0005-customer-visuals-in-location-scene.md](adr/0005-customer-visuals-in-location-scene.md).

---

## ⚠️ Supersedes SCENE_ARCHITECTURE.md §3, §8

Этот документ — **источник истины** по структуре сцен и переходам. Он отменяет два решения из
[SCENE_ARCHITECTURE.md](INPROGRESS/SCENE_ARCHITECTURE.md):

| Было (SCENE_ARCHITECTURE §3/§8) | Стало (этот документ) |
|---|---|
| Переименовать `GameplayScene`→`LocationScene` + новый `HubScene` | `GameplayScene` **остаётся хабом**; добавляется **новая** `LocationScene` |
| Чистая смена `single`-сценой; additive назван антипаттерном (§8) | **Additive**: хаб остаётся загружен, локация грузится поверх и выгружается при возврате |

Причина: хаб (gold-HUD, Preparation, точка возврата) должен переживать вход/выход в локацию без
перезагрузки, а локация — быть отдельной выгружаемой сценой (в перспективе её **контент** грузится из
Addressables; сама сцена остаётся обычной сценой в Build Settings). Решение **финальное**, а не
временный компромисс до отдельного `HubScene`.

---

## 1. Цикл

```
Bootstrap  ──(Single, _mainSceneName)──►  GameplayScene (ХАБ)
                                              │
                       Preparation.Confirm / Drive Out
                                              │  IGameFlowService.EnterLocationAsync()
                                              ▼
                                        LocationScene (additive) — Sales-день
                                              │  SalesDayController.DayCompleted
                                              │  IGameFlowService.ReturnToHubAsync()
                                              ▼
                                        GameplayScene (ХАБ) + ResultsWindow поверх
                                              │  Next Day (без reload сцены)
                                              ▼
                                        Morning / Preparation … цикл повторяется
```

Фазы дня (`Morning → Preparation → Sales → Results`) — см. [CORE_LOOP.md](CORE_LOOP.md). Их
state-машина (`IDayProgressService`) не меняется: меняется только **где** происходит фаза Sales —
теперь в отдельной `LocationScene`.

## 2. Сцены

| Сцена | Роль | Загрузка |
|---|---|---|
| `Bootstrap` | Загрузка/orchestrator (см. [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md)) | стартовая |
| `GameplayScene` | **Хаб**: gold-HUD, Preparation, точка возврата. Базовая сцена | `Single` (из Bootstrap) |
| `LocationScene` | **Локация Sales-дня**: customers, shelf, `SalesScreenView` | `Additive` (из хаба) |

`LocationScene` — обычная Unity-сцена в Build Settings. Addressables в перспективе используется только
для **контента локации** (префаб/арт — см. [INPROGRESS/LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md),
[adr/0005-customer-visuals-in-location-scene.md](adr/0005-customer-visuals-in-location-scene.md)),
а не для самой сцены.

## 3. DI-скопы

```
GlobalLifetimeScope (DontDestroyOnLoad)
  — Save, Configs, Resources, Progression, UIManager, IWindowFactory, IGameFlowService, ISceneTransitionService
   ├── GameplayLifetimeScope   (хаб; GameplayInstaller — Preparation, DayCycle-views, hub UI)
   └── LocationLifetimeScope   (локация; LocationInstaller — BookSell + sales + customer anchors)
```

`GameplayLifetimeScope` и `LocationLifetimeScope` — **оба дети `GlobalLifetimeScope` и сёстры**. При
additive-загрузке родитель `LocationLifetimeScope` задаётся явно через
`LifetimeScope.EnqueueParent(global)` (VContainer 1.16.x) — иначе VContainer может выбрать неверного
родителя при двух одновременно загруженных scene-скопах. Детали DI — [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md).

### UI при двух скопах

Окна создаёт **глобальный** `AddressablesWindowFactory` и инжектит контроллеры **глобальным**
resolver-ом. Окнам, которым нужны scene-scoped зависимости из `LocationLifetimeScope`, эти зависимости
передаются через `WindowArgs` (как у `RecommendationMinigameWindow`), а не через scene-resolver.
`ResultsWindow` показывается уже в хабе и зависит только от глобальных сервисов. Подробнее —
[UI_SYSTEM.md](UI_SYSTEM.md).

## 4. IGameFlowService

Глобальный singleton, оркестрирует цикл поверх `ISceneTransitionService` и `ITransitionAnimationService`.

| Метод | Что делает |
|---|---|
| `EnterLocationAsync(ct)` | cover → `LoadAdditive(LocationScene, parent=Global)` → выключить `GameplayHubRoot` → reveal |
| `ReturnToHubAsync(ct)` | cover → `Unload(LocationScene)` → включить `GameplayHubRoot` → `SetActiveScene(GameplayScene)` → reveal |

- **Reentrancy/failure guards**: переход «в процессе» игнорирует повторные вызовы (двойной клик Drive
  Out / повторный возврат); отмена и ошибки load/unload оставляют консистентное состояние.
- **Имя `LocationScene`** конфигурируется в `GameFlowSettings` (не в `BootstrapInstaller`).
- **`ITransitionAnimationService`** сейчас no-op/лёгкий fade своим кодом. DOTween **не используется**.
  (Research-описание cover/reveal — в [INPROGRESS/TRANSITION_ANIMATION_SERVICE.md](INPROGRESS/TRANSITION_ANIMATION_SERVICE.md),
  как референс для будущей реализации, не как зависимость.)

### Точки входа/возврата

- **Вход**: `PreparationWindow.Confirm` (окно Подготовки, открывается из `GameplaySceneController.StartGameAsync`) → `EnterLocationAsync()`.
- **Возврат**: `SalesDayController.DayCompleted` → `ReturnToHubAsync()` → `ResultsWindow` в хабе.
- **Next Day**: `ResultsSummarySessionService.AdvanceToNextDayAsync` применяет прогресс и **закрывает Results /
  возвращает хаб в Morning/Preparation без перезагрузки сцены** (раньше делал reload активной сцены).

## 5. Первый вход / обучение — TODO (отдельная задача)

Для первого входа игрока планируется: сразу загрузка `LocationScene` + обучение (tutorial). В текущей
итерации это **вне scope** — в `IGameFlowService` оставлен seam (точка ветвления «первый вход»), но он
дёргает обычный путь. Реализация FTUE-ветки и tutorial — отдельной задачей. Базовое FTUE-сидирование
(стартовые gold/книги) уже существует и не связано с этим seam — см. [FTUE.md](FTUE.md).

---

## Связанные документы

- [INPROGRESS/LOCATIONSCENE_EDITOR_SETUP.md](INPROGRESS/LOCATIONSCENE_EDITOR_SETUP.md) — ручная настройка сцен/префабов в редакторе.
- [CORE_LOOP.md](CORE_LOOP.md) — фазы дня (Morning/Preparation/Sales/Results).
- [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md) — загрузка, DI-скопы, `ISceneTransitionService`.
- [UI_SYSTEM.md](UI_SYSTEM.md) — окна, `IWindowFactory`, `WindowArgs`.
- [FTUE.md](FTUE.md) — стартовое сидирование (не tutorial).
- [INPROGRESS/SCENE_ARCHITECTURE.md](INPROGRESS/SCENE_ARCHITECTURE.md) — окна хаба и diegetic-точки (частично superseded — см. выше).
- [INPROGRESS/LOCATION_BUILDING.md](INPROGRESS/LOCATION_BUILDING.md) — построение контента локации.
- [adr/0005-customer-visuals-in-location-scene.md](adr/0005-customer-visuals-in-location-scene.md) — визуал покупателей в сцене локации.
