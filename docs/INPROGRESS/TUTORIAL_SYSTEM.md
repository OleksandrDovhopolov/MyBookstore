# TUTORIAL_SYSTEM — архитектура туториала и статус

Статус: 🚧 в работе. **Движок (Layer 2) и Day 1 v1 реализованы** (роадмап §6, этапы 1–6); Layer 1
(tutorial-квесты) заведён. **Осталось**: §7 (cheat/валидатор/аналитика) и полировка (немодальный callout,
строгий day-gate, локализация). Дата: 2026-07-06.

Документ описывает архитектуру системы обучения и её текущее состояние. Разделы §1–§5 — как устроено
(реализовано); §6 — роадмап со статусами; §6.1 — что вошло в Day 1 v1; §7–§8 — риски и платные опции.

> Связанные документы: [FTUE.md](../FTUE.md) (требования к движку и vision scripted Day 1),
> [QUESTS.md](../QUESTS.md) + [ADR-0007](../adr/0007-quest-system.md) (квест-система),
> [CHARACTER_SYSTEM.md](../CHARACTER_SYSTEM.md), [CHARACTERS_AND_QUESTS.md](../CHARACTERS_AND_QUESTS.md),
> [ASMDEF_RULES.md](../ASMDEF_RULES.md) (правило слоёв), [GameFlowLoop.md](../GameFlowLoop.md) (hub ↔ location).

---

## 1. Цель и референс

Референс — **Challenges из Tiny Bookshop**: задания дают персонажи локации («продай 15 Fantasy-книг
за лето», «укрась магазин 5 Calming-предметами»), задания видны в журнале, награды двигают сюжет
и открывают предметы. Это «мягкое» обучение через квесты, а не принудительный оверлей.

Наш туториал решает две задачи:

1. **Day 1 on-ramp** — направить игрока в первые минуты (упрощённая версия scripted Day 1 из
   [FTUE.md](../FTUE.md); диалоговой системы нет и она здесь не требуется).
2. **Долгосрочное обучение** — challenges-style задания от персонажей на протяжении игры.

## 2. Гибридная модель: два слоя

| Слой | Механизм | Для чего | Новый код |
|---|---|---|---|
| **Layer 1 — soft** | Tutorial-квесты через существующий `IQuestsService` (`QuestConfig.Type = "tutorial"` уже поддержан) | Challenges-style задания: «продай 3 книги жанра», «закончи первый день», «поставь декор». Отображаются в журнале, прогресс через `Game.Conditions`, персистентность уже есть (`SaveBackedQuestsRepository`) | Практически нет — только контент в `quests.json` |
| **Layer 2 — forced** | Тонкий step-движок: затемнение + подсветка + текст + гейтинг ввода | Только критичные моменты Day 1: «нажми Start Day», «подтверди полку», объяснение первой продажи | Новая фича `Game.Tutorial` + примитивы в `Infrastructure` |

Границы: Layer 2 **наблюдает** за Layer 1 (шаг `awaitQuest` ждёт события квеста), но квест-система
о туториале не знает. Обратная связь — только через condition-leaf `tutorialCompleted`
(см. §4.4): квест может активироваться «после завершения forced-интро», не ссылаясь на сборку туториала.

Паттерны взяты из production-туториала крупного 4X-проекта (heroes): разделение config/save/service,
event-driven активация, один эксклюзивный runner, pointer/blackout как независимые view-контроллеры,
one-way completion, editor-валидатор, cheat-панель. NodeCanvas оттуда не берём (§8).

## 3. Сборки и размещение

Правило слоёв ([ASMDEF_RULES.md](../ASMDEF_RULES.md)): `Infrastructure` не знает о фичах,
фичи ссылаются друг на друга только через `.API`.

```
Infrastructure (референс DOTween/UI уже есть; новых asmdef-ссылок не добавляли)
└── Assets/Game/Infrastructure/TutorialUI/
    ├── ITutorialTargetRegistry.cs / TutorialTargetRegistry.cs — DI-реестр id→RectTransform
    │                                (зеркало IResourceAnimationTargetRegistry; purge null)
    ├── TutorialTargets.cs        — статический ФАСАД над реестром (зеркало Infrastructure.Audio.Audio):
    │                                Bind/Clear/Register/Unregister/TryGetTarget — для MonoBehaviour-тегов
    ├── TutorialTargetTag.cs      — MonoBehaviour: [SerializeField] string _targetId; OnEnable→Register,
    │                                OnDisable→Unregister (через фасад)
    ├── TutorialTargetIds.cs      — const id таргетов (hub.start_day_button, …)
    ├── TutorialBlackoutView.cs   — затемнение из 4 слайсов вокруг «дырки»; дырка без Graphic; без шейдеров
    ├── TutorialPointerView.cs    — рука/стрелка, синус-баунс в Update
    └── ScreenRectUtility.cs      — RectTransform → rect в координатах overlay-канваса; per-frame пересчёт

Game.Tutorial.API   (autoReferenced: false; refs: UniTask)
    ├── ITutorialService.cs
    └── TutorialSignals.cs        — readonly structs: TutorialSequenceStarted / TutorialStepChanged /
                                     TutorialSequenceCompleted

Game.Tutorial       (refs: UniTask, VContainer, MessagePipe, Save, Configs, Game.Conditions.API,
    │                Game.Quest.API, Game.Core.UI, DayCycle, Game.Bootstrap.Loading, Game.Tutorial.API,
    │                Infrastructure, Unity.TextMeshPro)  — НЕ ссылается на Game.Preparation/Location
    ├── Services/         TutorialService.cs, TutorialSaveState.cs, TutorialSaveKeys.cs, TutorialTriggers.cs
    ├── Steps/            ITutorialStepHandler.cs, TutorialStepHandlerRegistry.cs, TutorialStepTypes.cs,
    │                     ITutorialWindowChecker.cs + 6 handler-классов (§4.2)
    ├── Conditions/       TutorialCompletedCondition.cs, TutorialCompletedConditionFactory.cs
    └── Presentation/     TutorialOverlayController.cs (canvas @3600 в рантайме), TutorialOverlaySettings.cs
                          (ScriptableObject), TutorialTextPanelView.cs (TMP)

Game.Bootstrap (Assets/Game/Core/Installers/Features/)
    ├── TutorialVContainerBindings.cs  — регистрация сервиса/хендлеров/реестра/overlay/условия
    └── TutorialWindowChecker.cs       — реализация ITutorialWindowChecker (знает окна: preparation/
                                          location/results) — concrete-ссылки на окна живут ЗДЕСЬ

Configs
    └── Assets/Game/Features/Configs/Models/TutorialSequenceConfig.cs + TutorialStepConfig.cs
        ([ConfigFile("tutorials")]) + Assets/Configs/tutorials.json
```

Ключевые решения по размещению:

- **TMP-текст-панель (`TutorialTextPanelView`) живёт в `Game.Tutorial`**, а не в Infrastructure —
  чтобы не добавлять `Unity.TextMeshPro` в `Infrastructure.asmdef`. В Infrastructure только
  blackout/pointer/targets/rect-utility.
- **`TutorialSequenceConfig` не ссылается на `Game.Tutorial`**: тип шага — string-дискриминатор
  (`"showText"`, `"highlightClick"`, …), маппинг на handler-классы — в registry внутри `Game.Tutorial`.
- **Почему не расширение `Game.Ftue`**: Ftue — узкий bootstrap-модуль (сидирование + welcome-письмо),
  без ссылок на Quest/Conditions/MessagePipe/Infrastructure. Движок туториала — общая система
  (позже: туториал газеты, декора), Day 1 — лишь первый клиент.
- **DI**: `TutorialVContainerBindings` в `Game.Bootstrap` (рядом с `MessagePipeVContainerBindings`);
  сервис — **глобальный singleton в `BootstrapInstaller`** (как `QuestsService`) — ему нужны
  подписки на `IQuestsService` / `IGameFlowService` / `IDayProgressService` и сейв, живущие сквозь
  hub ↔ location. Брокеры сигналов — в общий `RegisterMessagePipeBus`.
- **Sibling-паттерн**: будущая мягкая подсветка кнопки журнала (по `QuestStarted`) — тонкий
  listener в `Game.Quest.UI`, переиспользующий `TutorialPointerView` + `TutorialTargets` из
  Infrastructure **без** участия движка.

## 4. Данные

### 4.1 tutorials.json — схема секвенции

Схема (иллюстративно; реальная секвенция Day 1 — в §6.1):

```jsonc
[
  {
    "id": "day1_hub_intro",
    "priority": 10,                  // меньше = раньше при нескольких eligible
    "context": "hub",                // hub | location | any — gate активации (§5.1)
    "trigger": "hubReady",           // hubReady | locationLoaded | phaseChanged | questStarted | questCompleted
    "triggerParam": null,            // напр. questId для questStarted / phase для phaseChanged
    "resumePolicy": "restart",       // restart | fromStep
    "activationConditions": null,    // JObject-дерево (движок Game.Conditions); null = всегда
    "steps": [
      { "id": "welcome",     "type": "showText",
        "text": "…", "textKey": null, "placement": "bottom" },
      { "id": "point_start", "type": "highlightClick",
        "target": "hub.start_day_button", "pointer": true,
        "text": "…", "placement": "aboveTarget", "padding": 16 },
      { "id": "wait_prep",   "type": "awaitWindow", "window": "preparation" },
      { "id": "wait_sale",   "type": "awaitQuest",
        "questId": "tut_first_sale", "event": "taskCompleted", "taskId": 0 }
    ]
  }
]
```

Текст — сырой ASCII/English (локализации в проекте нет), поле `textKey` зарезервировано для будущей
миграции. **Условия `dayIs`/`currentDayIs` пока НЕ реализованы** (нет строгого day-gate — см. §6.1
«известные ограничения»); `activationConditions` использует уже существующие leaf-условия
(`Game.Conditions`) либо `tutorialCompleted` (§4.4).

### 4.2 Типы шагов — 6 штук (все реальные)

Каждый тип — string-дискриминатор → отдельный небольшой handler-класс с собственной подпиской
(push-модель, без re-evaluation-цикла). Generic-движок `ICondition` используется только для
`activationConditions` (pull при триггере). Все 6 хендлеров реализованы (Проход 2 — showText/
highlightClick/awaitPhase; Проход 3 — awaitWindow/awaitQuest/awaitLocation); стаб-база удалена.
У всех «await»-хендлеров — **initial-check текущего состояния до подписки** (иначе на resume, если
событие уже прошло, шаг зависнет).

| type | Гейтинг | Advance |
|---|---|---|
| `showText` | Полное затемнение + `IUIManager.SetManualLock` | Любой тап |
| `highlightClick` | Blackout-слайсы (дырка над `target`); **`SetManualLock` НЕ использовать** — глобальный blocker `LockMonitor` перехватил бы клик в дырку. Слайсы сами блокируют всё вне дырки, target получает реальный клик. Потеря таргета (destroy/`SetActive(false)`/`!interactable`) → warning + auto-advance | Клик по target через дырку |
| `awaitWindow` | нет | `ITutorialWindowChecker.TryGetShown(id)` поллинг ~4 Гц; неизвестный id → auto-advance |
| `awaitQuest` | нет | initial-check + события `IQuestsService` (Started/TaskCompleted/Completed/Awarded) по `questId` (+опц. `TaskId`) |
| `awaitPhase` | нет | initial-check + `IDayProgressService.PhaseChanged` |
| `awaitLocation` | нет | initial-check + `IGameFlowService.LocationLoadedChanged` |

### 4.3 Window-id checker

`IUIManager` даёт только generic `IsWindowShown<T>()` — произвольные class-name-строки в JSON
недопустимы. Чтобы движок не зависел от UI-фич, введён интерфейс **`ITutorialWindowChecker`
(`bool TryGetShown(id, out shown)`)** в `Game.Tutorial`, а его реализация `TutorialWindowChecker`
живёт в **bootstrap-слое** (`Game.Bootstrap`, где ссылки на окна уже есть):

```csharp
// TutorialWindowChecker (Game.Bootstrap): id → IsWindowShown<T>()
{ "preparation", ui.IsWindowShown<PreparationWindow> },
{ "location",    ui.IsWindowShown<LocationWindow> },
{ "results",     ui.IsWindowShown<ResultsWindow> },
// неизвестный id → TryGetShown возвращает false → handler делает auto-advance (+ валидатор §7)
```

Так `Game.Tutorial.asmdef` НЕ ссылается на `Game.Preparation`/`Game.Location`.

### 4.4 Связь с квестами: condition `tutorialCompleted`

`Game.Tutorial` регистрирует через `IConditionFactory` leaf-условие
`{ "type": "tutorialCompleted", "sequenceId": "day1_hub_intro" }`. Квесты могут гейтиться на
завершение forced-интро **без** ссылки `Game.Quest → Game.Tutorial`.

### 4.5 Таргет-реестр (подсветка кнопок)

**DI-сервис `ITutorialTargetRegistry`** (Infrastructure, зеркало `IResourceAnimationTargetRegistry`:
`Register/Unregister/TryGetTarget`, `StringComparer.Ordinal`, purge null при lookup) + **статический фасад
`TutorialTargets`** (зеркало `Infrastructure.Audio.Audio`), к которому реестр биндится один раз на
bootstrap (`RegisterBuildCallback → TutorialTargets.Bind(...)`). Идентификация цели вешается на объект
компонентом **`TutorialTargetTag`** (MonoBehaviour), а не регистрируется контроллером.

- `TutorialTargetTag`: `[SerializeField] string _targetId;` `OnEnable → TutorialTargets.Register(id, rect)`,
  `OnDisable → Unregister`. Так решается «в MonoBehaviour нет constructor injection», и additive
  hub↔location обрабатывается автоматически. Стоимость нового таргета — один компонент + строка id.
- Идентификаторы — `TutorialTargetIds` (`hub.start_day_button`, задел на decor/prep/hud); тот же словарь
  сверяет валидатор (§7).
- DI-потребители (step-handler'ы) инжектят `ITutorialTargetRegistry` напрямую (фасад — только для тегов).
- Если id не найден — handler `highlightClick` пишет warning и **auto-advance** (не soft-lock).
- Rect таргета пересчитывается **каждый кадр** (`ScreenRectUtility`): таргеты едут на твинах
  `AnimatedShowHidePanel` и смещаются SafeArea.
- Overlay (blackout/pointer/text) создаётся в **рантайме** `TutorialOverlayController.EnsureRoot()`
  (canvas @3600 под `IUICanvasRoot.WindowsRoot`, зеркало `ResourceAnimationService.EnsureRoot`) —
  **overlay-префаб не нужен**; из префабов только мини-панель текста.

## 5. Runtime

### 5.1 Жизненный цикл

1. **Init** (глобальный скоуп): загрузить модуль `tutorial.state` (`ISaveService`, schema v1),
   зарегистрировать save-hook, подписаться на триггеры (`PhaseChanged`, `LocationLoadedChanged`,
   события квестов), оценить resume.
2. **Активация-скан** на каждом триггер-событии: eligible = совпал trigger + `ContextAllows` +
   id не в `CompletedSequenceIds` + `activationConditions` выполнены. **Context — gate активации,
   а НЕ mid-run abort** (`day1_hub_intro` стартует в hub и потом ждёт Sales в локации — mid-run
   abort его бы сломал): `hub → !IsLocationLoaded`, `location → IsLocationLoaded`, `any/null → true`.
   Плюс **transition-guard**: старт пропускается, если `IsTransitioning`, кроме триггера
   `locationLoaded` (он и происходит во время перехода). **Один эксклюзивный runner**.
3. **Step loop**: перед каждым шагом персист `{ActiveSequenceId, NextStepIndex=i}` (i = текущий, ещё
   не завершённый шаг) → publish `TutorialStepChanged` → handler конфигурирует overlay/ждёт advance.
   Последний шаг: id → `CompletedSequenceIds`, publish `TutorialSequenceCompleted`,
   `IQuestReevaluationGate.RequestReevaluation()`. **One-way completion**.
4. **Skip / teardown**: `SkipActiveAsync` отменяет per-run `CancellationTokenSource` → long-running
   шаг размотается `OperationCanceledException`, handler в `finally` прячет overlay; run завершается
   без пометки complete. Потеря highlight-таргета обрабатывается внутри шага (auto-advance), не
   рвёт весь run.
5. **Resume после релонча**: `ResumeActiveSequence` (с guard `if (_running) return;`, context НЕ
   проверяется) поднимает сохранённую активную секвенцию; `restart` — с нуля (у `day1_hub_intro`, т.к.
   есть window-зависимые шаги, а окна при релонче не восстанавливаются); `fromStep` — с `NextStepIndex`
   (для hub-only секвенций).

### 5.2 ITutorialService (API)

```csharp
bool IsRunning { get; }
string ActiveSequenceId { get; }
IReadOnlyCollection<string> CompletedSequenceIds { get; }
UniTask<bool> TryStartAsync(string sequenceId, bool force, CancellationToken ct); // force — debug
UniTask SkipActiveAsync(CancellationToken ct);   // debug; player-facing skip — флаг на будущее
UniTask ResetAsync(string sequenceId, CancellationToken ct);  // debug replay
// прогресс — MessagePipe-сигналы (кросс-фичевое наблюдение), не C#-события
```

### 5.3 Гейтинг и слои

Overlay — **НЕ окно UIManager**: канвас создаётся **в рантайме** `TutorialOverlayController.EnsureRoot()`
под `IUICanvasRoot.WindowsRoot` со своим `sortingOrder` (зеркало `ResourceAnimationService.EnsureRoot`) —
overlay-префаб не нужен. Причины «не окно»: `HideTopAsync`/Android-back и focus-chain его не видят
(back не закроет туториал и не будет им съеден); окна продолжают открываться/закрываться под ним
(шаг подсвечивает кнопку, клик по которой открывает PreparationWindow — стековый blocker с этим
конфликтовал бы).

Таблица sortingOrder (фиксируем как константы):

| Слой | Order |
|---|---|
| System (окна) | 3000 |
| Resource animations ([AnimationBuilder.md](AnimationBuilder.md)) | 3500 |
| **Tutorial overlay** | **3600** |
| Develop / cheats | 4000 |

Правило `SetManualLock`: держится сервисом **только** на full-block шагах (`showText`), чтобы
никакая другая система не открыла/закрыла окно посреди шага. На `highlightClick` — не используется (§4.2).

### 5.4 Интеграционные точки

| Система | Использование |
|---|---|
| `IDayProgressService.PhaseChanged` | Триггер + advance (`awaitPhase`) |
| `IGameFlowService.LocationLoadedChanged / IsTransitioning / IsLocationLoaded` | Триггер, advance (`awaitLocation`), context-loss |
| `IQuestsService` (события) | Advance (`awaitQuest`), Layer 1 |
| `ISaveService` | Модуль `tutorial.state` (schema v1) |
| `IUIManager` | `IsWindowShown<T>` (через реестр), `SetManualLock` (только showText) |

Сейв-ключ `ftue.first_location_tutorial` (`FtueSaveKeys`) **вытесняется** модулем `tutorial.state`;
на этапе реализации пометить deprecated в [FTUE.md](../FTUE.md) и комментариях `FtueSaveKeys`
(миграция не нужна — данных в этом модуле нет).

## 6. Roadmap — 7 этапов

**Статус:** этапы **1–4 и 6 — ✅ реализованы**; **5 — ✅** (tutorial-квесты заведены в `quests.json` +
condition `tutorialCompleted`; опц. мягкий pointer на журнал — не сделан); **7 — ⏳ не начат**
(cheat/валидатор/аналитика). Таблица ниже — исходный план с критериями проверки (историческая).

| # | Этап | Содержание | Критерий проверки |
|---|---|---|---|
| 1 | **Infrastructure-примитивы** | `TutorialUI/`: blackout (4 слайса + дырка), pointer, `TutorialTargets` + `TutorialTargetTag`, `ScreenRectUtility`. Отдельная тест-сцена с кнопками и driver-скриптом | Затемнение закрывает экран, кликабельна только кнопка в дырке, pointer баунсит, дырка следит за движущейся кнопкой; портрет + SafeArea (Device Simulator). Игровые asmdef не подключены |
| 2 | **Конфиг + скелет сервиса** | `TutorialSequenceConfig` + `tutorials.json` (одна dummy-секвенция); asmdef'ы API/impl; `TutorialService` (global scope, сейв, активация-скан, сигналы); шаги логируют и auto-advance | Fresh save → в консоли активация/шаги/завершение; `tutorial.state` в сейве; релонч не ретриггерит; удаление сейва — триггерит снова |
| 3 | **Первый forced-шаг end-to-end** | `TutorialOverlayController` + `ShowTextStep` + `HighlightClickStep`; теги на StartDay/Decor кнопках; реальная 2-шаговая hub-секвенция | Welcome-текст блокирует всё, тап продвигает; дырка над Start Day, кликабельна только она; клик и продвигает туториал, и запускает день; completion персистится |
| 4 | **Остальные шаги + устойчивость** | `awaitWindow` (реестр), `awaitQuest/Phase/Location`; context-loss на `IsTransitioning` и hub↔location; `SetManualLock` на gated-шагах; resume | Тест-секвенция hub→location проходит; quit посреди секвенции → чистый рестарт; back во время gated-шага не ломает |
| 5 | **Квест-слой (Layer 1)** | 2–3 tutorial-квеста в `quests.json` (challenges-style, от персонажей); condition `tutorialCompleted`; опц.: мягкий pointer на журнал по `QuestStarted` | Квесты видны в журнале с Day 1, прогресс тикает от реальных продаж, награда выдаётся, **прогресс переживает релонч** (персистентность квестов уже есть) |
| 6 | **Контент Day 1** | Полный упрощённый Day 1 в tutorials.json + quests.json по [FTUE.md](../FTUE.md), без диалогов: hub intro → stocking hint (highlight confirm) → location arrival text → первая продажа через `awaitQuest` → wrap-up. Финализировать `TutorialTargetIds`, теги в Preparation/Location views | Сквозной прогон fresh-save Day 1; Day 2 — без туториала |
| 7 | **Debug/replay + polish** | Cheat-модуль в `Game.Cheat` (list/force-run/force-complete/reset одной или всех + сброс `ftue.*` = полный replay Day 1); editor-валидатор (target id: json ↔ `TutorialTargetIds` ↔ скан префабов на `TutorialTargetTag`; questIds ↔ quests.json; window id ↔ реестр; парс типов шагов); аналитика (`tutorial_seq_start`/`step_start` автоматом, `seq_complete` явно — heroes-конвенция) | Cheat-панель реиграет Day 1 на прогресснутом сейве; валидатор ловит намеренно сломанный id; события видны в debug-провайдере |

### 6.1 Day 1 v1 — что реализовано (упрощённо)

**Обновлено:** day 1 теперь входит **сразу в локацию** (авто-сток, см. [FTUE.md](../FTUE.md)
«Day-1 direct entry»), поэтому хаб-секвенция `day1_hub_intro` **заменена** на `tutorial_day_1`.

Секвенция `tutorial_day_1` (tutorials.json, `context:"location"`, `trigger:"locationLoaded"`,
`resumePolicy:"restart"`): welcome (пассивные продажи) → showText про sale chance → `awaitWindow("results")`
→ wrap-up. Триггер поднимается `GameFlowService.EnterLocationAsync` (→ `LocationLoadedChanged`); он —
исключение из transition-guard, так что стартует прямо во время перехода в локацию. `awaitWindow("results")`
требует `"results"` в `TutorialWindowChecker`. Требует `_tutorialAutoStart = 1` в `BootstrapInstaller`.

**Форма — только `showText` + `awaitWindow`** (без `highlightClick`): игрок уже в локации, кнопки
Open Shop / список жанров ещё не имеют target-id (`TutorialTargetIds` пока только `hub.*`). Подсветка
in-location контролов — follow-up (новые id + `TutorialTargetTag` в Location-view). **`awaitQuest(конкретная
продажа)` как блокирующий шаг НЕ используем** (вероятностная продажа → риск зависания). Layer-1 квесты
(`tut_first_day`/`tut_first_sale`) — отдельно, журнальные.

**Известные ограничения v1:** нет строгого day-gate (one-way completion, играет один раз при первом входе
в локацию); resume посреди Day 1 — best-effort (`restart`); при `_firstDayEntry = Hub` day 1 идёт **без**
скриптового туториала (хаб-секвенция ретайрнута).

**Следующее для визуальной подсветки контролов** (Open Shop / список жанров / динамический «+»):
немодальный callout-режим (pointer+текст **без** dim; тип шага `pointAt`/`callout`) + динамическая
регистрация таргетов из `PreparationGenreRowView` через фасад `TutorialTargets`; строгий `currentDayIs`
condition-factory для day-gate.

## 7. Риски / открытые вопросы

| Риск | Митигация |
|---|---|
| Android-back во время gated-шага закрывает окно *под* оверлеем → context-loss | Abort-and-retrigger уже корректен; UX (мигание) проверить плейтестом; при необходимости — back-suppression через Lock, только на `showText` |
| Поллинг `IsWindowShown` (0.25 с) | Для прототипа ок; сигнал `WindowShown` из UIManager — поздняя оптимизация, не зависимость |
| Прямоугольная жёсткая дырка | Шейдер с feather — позже, drop-in за тем же API `TutorialBlackoutView` |
| Нет локализации | Сырой RU-текст + зарезервированный `textKey`; миграция механическая |
| Дрейф string-id (таргеты/окна/квесты) | Валидатор §7 (⏳ не сделан), включая скан префабов на `TutorialTargetTag` |
| Резолв таргета сделан DI-реестром `ITutorialTargetRegistry` + фасадом `TutorialTargets` (для тегов) | Реализовано (§4.5); purge null при lookup |
| Resume/cancel-path Day 1 (§6.1) | Осознанные ограничения v1; строгий day-gate и recovery — позже |

## 8. Платные решения (если появится бюджет)

Сейчас пишем своё: тонкий движок — ~5–6 небольших классов, что дешевле интеграции стороннего
ассета в стек VContainer/UniTask/MessagePipe.

| Ассет | Цена | Когда имеет смысл |
|---|---|---|
| **NodeCanvas** (Paradox Notion) — рекомендация | ~$70 | Три графовых системы в одном ассете: Behaviour Trees, FSM, **Dialogue Trees**. Проверен в production-туториале крупного 4X-проекта (граф + blackboard + root-gate-паттерн); переиспользуем для AI/квестов/диалогов. Брать, когда секвенции станут ветвистыми (диалоги, условные переходы) |
| Tutorial Master 2 | ~$30 | Узкоспециальный (только туториалы); дешевле, но не переиспользуем |

### 8.1 Сложность будущего перехода на NodeCanvas

**Вывод: переход средней сложности и предсказуемый — дизайн специально изолировал точку замены.**
NodeCanvas заменяет **только Layer 2** (forced-step-движок); Layer 1 (tutorial-квесты через
`IQuestsService`) к NodeCanvas отношения не имеет. То есть мигрирует меньшая и наиболее
изолированная часть. Есть живой пруф — heroes собрал ровно эту связку (VContainer + UniTask +
NodeCanvas), так что интеграция не изобретается, а копируется.

**Переиспользуется как есть (стоимость ≈ 0):**
- Все примитивы `Infrastructure/TutorialUI` (blackout, pointer, `TutorialTargets`, `ScreenRectUtility`) —
  чистый View-слой; NodeCanvas-нода дёргает их так же, как step-handler. В heroes ноды
  `ShowTutorialPointer/Blackout` управляли ровно такими независимыми view-контроллерами.
- `TutorialOverlayController` (runtime canvas), `TutorialTargetIds`, `ITutorialWindowChecker`.
- Save-модель (`CompletedSequenceIds`, one-way completion) — концептуально та же.
- Layer 1 целиком (квесты, `quests.json`, `tutorialCompleted`).

**Переписывается:**

| Компонент | Что происходит | Оценка |
|---|---|---|
| 6 step-handler'ов | Становятся `ActionTask`/`ConditionTask`-нодами. Логика внутри переносится ~1:1; меняется обёртка: `ITutorialStepHandler` + `UniTask` → nodeвая модель `OnExecute/OnUpdate → Success/Running` | ~6 классов, механически |
| `tutorials.json` | **Не остаётся** — контент переезжает в графы-ассеты (ручное пересоздание в редакторе, не конверсия) | Зависит от объёма |
| `TutorialSequenceConfig` | Меняет форму: вместо `steps[]` — `AssetReference` на граф + trigger (как heroes `TutorialConfig{Id, Graph, ActivationEvents}`) | Небольшая |
| `TutorialService` | Активация-скан, эксклюзивный runner, save, context-loss **остаются**; заменяется только «step loop» → «load graph (Addressables) → run → track complete» | Средняя |

> Уточнение к «остаётся контент» из общей формулировки: при переходе именно на NodeCanvas контент
> **не остаётся** — JSON-секвенции переезжают в графы. Остаются примитивы и Layer 1.

**Риски интеграции в стек (основная цена):**
- **DI ↔ POCO-ноды.** Ноды NodeCanvas не создаются VContainer'ом — десериализуются из графа. Нужен
  статический мост (heroes' `TutorialsDependencies`), заполняемый при старте из скоупа. Шаг назад по
  чистоте DI, но пишется один раз.
- **UniTask ↔ tick-модель.** Событийные advance-условия (`await LocationLoadedChanged`, событие квеста)
  превращаются в ноды, которые поллят флаг или подписываются в `OnExecute` и ставят success в колбэке.
- **Addressables-латентность графа.** Граф — ассет, грузится перед запуском (у heroes — отдельные
  preload-таски). Совместимо с `ProdAddressablesWrapper`, но добавляет async-шаг.
- **Git-diff графов** хуже, чем текущий читаемый `tutorials.json`; **vendor lock-in** (граф не прочитать
  без NodeCanvas) + кривая обучения редактору.

### 8.2 Связь NodeCanvas с диалоговой системой (почему покупается не ради туториала)

Ключевое различие, которое снимает путаницу: **`WindowController` — это слой отображения (View),
а не движок диалога.** Он отвечает на вопрос «как нарисовать один экран диалога» (портрет, имя,
текст, кнопки выбора). Он **не** отвечает на вопрос «какая реплика идёт следующей и как выбор
ветвит разговор». Для этого нужен второй слой — **flow-движок диалога** (граф реплик):

- узлы: реплика, развилка выбора, условие («показать только если квест X сдан»),
  side-effect (выдать награду, стартовать квест, поставить флаг), переход/цикл;
- runtime, который идёт по этому графу и на каждом узле дёргает View, чтобы показать реплику и
  дождаться выбора игрока.

**То есть диалоговая система = ваш диалоговый `WindowController` (View) + flow-движок (граф).**
Строя диалог «на `WindowController`», вы делаете именно View — и это правильно. Но flow-движок
всё равно понадобится, и у вас два пути:

1. **Написать свой** — диалоги в JSON (узлы `id/text/choices→nextId/conditions/effects`) + runtime
   `DialogueService`, который ходит по графу и вызывает `IDialogueView`. Это **та же форма**, что и
   forced-step-движок туториала: «walker по графу + View». По сути третий hand-rolled граф-раннер
   в проекте — после туториала и квест-машины поверх `Game.Conditions`.
2. **NodeCanvas Dialogue Tree** — готовый flow-движок ровно под ветвистый диалог (узлы реплик,
   множественного выбора, условий, действий, система актёров) + визуальный редактор.

Отсюда суть фразы «окупается не ради туториала одного»: forced-туториал, диалоговый flow и
квест-машина — это **три экземпляра одной абстракции** (граф шагов с условиями и эффектами,
проходимый в runtime и дёргающий View). Сейчас вы (или будете) писать каждый отдельно. NodeCanvas —
один граф-runtime под все три: BehaviourTree/FSM для туториала и квестов, DialogueTree для диалога,
с общим редактором, blackboard и библиотекой condition/action-нод. Интеграционная цена (§8.1) платится
один раз и **амортизируется** по трём системам. Если использовать только под линейный туториал —
не окупается; под туториал + диалоги + сложные квесты — окупается.

**Важно для вашего плана диалоговой системы:** NodeCanvas **не заменяет** `WindowController` и не
конфликтует с ним — они на разных слоях и сосуществуют. Диалоговое окно на `WindowController`,
которое вы построите сейчас, **не выкидывается** при возможном переходе на NodeCanvas: оно становится
View, который дёргают Dialogue-ноды (`Say`-узел вызывает ваш `IDialogueView.ShowLineAsync(...)` и ждёт
выбор, затем граф идёт дальше). Ровно так же, как blackout/pointer туториала остаются при его переходе
на NodeCanvas. Поэтому строить диалоговый **View** на `WindowController` — правильно и безопасно
независимо от решения по NodeCanvas; открытый вопрос только один — **чем гонять flow** (свой
JSON-walker сейчас vs NodeCanvas DialogueTree потом), и этот выбор можно отложить, не переделывая View.

### 8.3 Критерий перехода

Держаться критерия: переходить, **когда секвенции станут по-настоящему ветвистыми** — диалоги с
выбором, условные переходы, дерево реакций NPC. В этот момент NodeCanvas окупается (и закрывает
диалоговый flow, которого нет, и AI/квест-графы — покупается не ради туториала одного). Пока
секвенции линейные — свой тонкий движок дешевле в сумме владения; изоляция дизайна гарантирует, что
переход не подорожает от того, что его отложили.
