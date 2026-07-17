# TUTORIAL_SYSTEM — архитектура туториала и статус

Статус: 🚧 в работе. **Движок (Layer 2) и Day 1 v1 реализованы** (роадмап §6, этапы 1–6) — на JSON-авторинге;
Layer 1 (tutorial-квесты) заведён. Дата: 2026-07-06.

> ⚠️ **2026-07-17: принято решение о переезде авторинга с `tutorials.json` на C#-классы — план в §9.**
> Переезд **ещё не выполнен**; на момент записи Day 1 v1 работает по-старому, из `tutorials.json`.
> После §9-этапа 1 Day 1 переедет в `Game.Tutorial.Content/TutorialDay1`, а `tutorials.json` будет удалён.
>
> Что это означает для документа: §4.1–§4.3 (схема JSON, string-дискриминаторы шагов, window-id checker)
> описывают **уходящее** состояние. §2 (двухслойная модель), §4.4–§4.5 (condition `tutorialCompleted`,
> таргет-реестр) и §5 (runtime/жизненный цикл) — **остаются в силе**. Из §7 отменяется валидатор id-шников
> (становится работой компилятора). §8 (NodeCanvas) — отложен, см. §9.

Документ описывает архитектуру системы обучения и её текущее состояние. Разделы §1–§5 — как устроено
(реализовано); §6 — роадмап со статусами; §6.1 — что вошло в Day 1 v1; §7–§8 — риски и платные опции;
§9 — план переезда на C#-секвенции (актуальная работа).

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

Game.Tutorial       (refs: UniTask, VContainer, MessagePipe, Save, Game.Conditions.API,
    │                Game.Quest.API, Game.Core.UI, DayCycle, Game.Bootstrap.Loading, Game.Tutorial.API,
    │                Infrastructure, Unity.TextMeshPro)  — НЕ ссылается на Game.Preparation/Location
    ├── Services/         TutorialService.cs, TutorialSaveState.cs, TutorialSaveKeys.cs
    ├── Conditions/       TutorialCompletedCondition.cs, TutorialCompletedConditionFactory.cs
    └── Presentation/     TutorialOverlayController.cs (canvas @3600 в рантайме), TutorialOverlaySettings.cs
                          (ScriptableObject), TutorialTextPanelView.cs (TMP)

Game.Tutorial.Content
    └── TutorialDay1.cs + typed step classes — C#-источник forced-step контента Day 1

Game.Bootstrap (Assets/Game/Core/Installers/Features/)
    └── TutorialVContainerBindings.cs  — регистрация сервиса/реестра/overlay/условия/C#-контента
```

Ключевые решения по размещению:

- **TMP-текст-панель (`TutorialTextPanelView`) живёт в `Game.Tutorial`**, а не в Infrastructure —
  чтобы не добавлять `Unity.TextMeshPro` в `Infrastructure.asmdef`. В Infrastructure только
  blackout/pointer/targets/rect-utility.
- **Forced-step контент больше не грузится из `tutorials.json`**: секвенции реализуют `ITutorialSequence`,
  шаги — `ITutorialStep`, триггеры/контекст/resume-policy заданы enum'ами в `Game.Tutorial.API`.
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

### 4.1 C#-секвенции

Актуальная модель: Day 1 описан классом `TutorialDay1 : ITutorialSequence` в `Game.Tutorial.Content`.
Сервис получает `IReadOnlyList<ITutorialSequence>` из DI, материализует `GetSteps()` один раз на запуск и
резюмится по `NextStepId` с fallback на старый `NextStepIndex`.

Историческая JSON-схема ниже оставлена как reference для того, от какой формы уехал Layer 2; она больше не
является runtime-источником контента.

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
миграции. Строгий day-gate живёт в C#-секвенциях через `ITutorialSequence.IsEligible()`; отдельный
condition-factory `currentDayIs` для этого не пишется.

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
| `awaitWindow` | нет | typed `Func<bool>`/`IUIManager.IsWindowShown<T>()` поллинг ~4 Гц; неизвестный тип окна → compile error |
| `awaitQuest` | нет | initial-check + события `IQuestsService` (Started/TaskCompleted/Completed/Awarded) по `questId` (+опц. `TaskId`) |
| `awaitPhase` | нет | initial-check + `IDayProgressService.PhaseChanged` |
| `awaitLocation` | нет | initial-check + `IGameFlowService.LocationLoadedChanged` |

### 4.3 Typed window checks

`IUIManager` даёт generic `IsWindowShown<T>()`; C#-контент вызывает его типизированно. Старый
`ITutorialWindowChecker`/string id мост удалён вместе с JSON-слоем. Так `Game.Tutorial.asmdef` НЕ
ссылается на `Game.Preparation`/`Game.Location`; конкретные window refs живут в content-сборке только там,
где реально нужны.

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
- DI-потребители (C# content/steps) инжектят `ITutorialTargetRegistry` напрямую (фасад — только для тегов).
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
| 2 | **Скелет сервиса** | asmdef'ы API/impl; `TutorialService` (global scope, сейв, активация-скан, сигналы); каталог из DI-registered `ITutorialSequence` | Fresh save → в консоли активация/шаги/завершение; `tutorial.state` в сейве; релонч не ретриггерит; удаление сейва — триггерит снова |
| 3 | **Первый forced-шаг end-to-end** | `TutorialOverlayController` + `ShowTextStep` + `HighlightClickStep`; теги на StartDay/Decor кнопках; реальная 2-шаговая hub-секвенция | Welcome-текст блокирует всё, тап продвигает; дырка над Start Day, кликабельна только она; клик и продвигает туториал, и запускает день; completion персистится |
| 4 | **Остальные шаги + устойчивость** | `awaitWindow` (реестр), `awaitQuest/Phase/Location`; context-loss на `IsTransitioning` и hub↔location; `SetManualLock` на gated-шагах; resume | Тест-секвенция hub→location проходит; quit посреди секвенции → чистый рестарт; back во время gated-шага не ломает |
| 5 | **Квест-слой (Layer 1)** | 2–3 tutorial-квеста в `quests.json` (challenges-style, от персонажей); condition `tutorialCompleted`; опц.: мягкий pointer на журнал по `QuestStarted` | Квесты видны в журнале с Day 1, прогресс тикает от реальных продаж, награда выдаётся, **прогресс переживает релонч** (персистентность квестов уже есть) |
| 6 | **Контент Day 1** | Полный упрощённый Day 1 в `TutorialDay1` C# content + quests.json по [FTUE.md](../FTUE.md), без диалогов: location arrival text → sale chance → results window → wrap-up | Сквозной прогон fresh-save Day 1; Day 2 — без туториала |
| 7 | **Debug/replay + polish** | Cheat-модуль в `Game.Cheat` (list/force-run/force-complete/reset одной или всех + сброс `ftue.*` = полный replay Day 1); editor-валидатор (target id ↔ `TutorialTargetIds` ↔ скан префабов на `TutorialTargetTag`; questIds ↔ quests.json); аналитика (`tutorial_seq_start`/`step_start` автоматом, `seq_complete` явно — heroes-конвенция) | Cheat-панель реиграет Day 1 на прогресснутом сейве; валидатор ловит намеренно сломанный id; события видны в debug-провайдере |

### 6.1 Day 1 v1 — что реализовано (упрощённо)

**Обновлено:** day 1 теперь входит **сразу в локацию** (авто-сток, см. [FTUE.md](../FTUE.md)
«Day-1 direct entry»), поэтому хаб-секвенция `day1_hub_intro` **заменена** на `tutorial_day_1`.

Секвенция `tutorial_day_1` (`TutorialDay1`, `Context = Location`, `Trigger = LocationLoaded`,
`ResumePolicy = Restart`): ждёт завершения Eddi-диалога (переход Eddi в `Browsing`) → немодальные callout-биты
по первой удачной/неудачной пассивной покупке (жанр подставляется из sales-сигналов) → typed await `ResultsWindow` → wrap-up. Триггер поднимается
`GameFlowService.EnterLocationAsync` (→ `LocationLoadedChanged`); он — исключение из transition-guard, так что
стартует прямо во время перехода в локацию. Требует `_tutorialAutoStart = 1` в `BootstrapInstaller`.

**Форма — `callout` + финальный `showText` + `awaitWindow`** (без `highlightClick`): игрок уже в локации, кнопки
Open Shop / список жанров ещё не имеют target-id (`TutorialTargetIds` пока только `hub.*`). Подсветка
in-location контролов — follow-up (новые id + `TutorialTargetTag` в Location-view). **`awaitQuest(конкретная
продажа)` как блокирующий шаг НЕ используем** (вероятностная продажа → риск зависания). Layer-1 квесты
(`tut_first_day`/`tut_first_sale`) — отдельно, журнальные.

**Известные ограничения v1:** resume посреди Day 1 — best-effort (`restart`); при `_firstDayEntry = Hub`
day 1 идёт **без** скриптового туториала (хаб-секвенция ретайрнута).

**Следующее для визуальной подсветки контролов** (Open Shop / список жанров / динамический «+»):
pointer/highlight для таргетов + динамическая регистрация таргетов из `PreparationGenreRowView` через
фасад `TutorialTargets`. Text-only немодальный callout уже есть; привязка к таргетам — follow-up этапа 5.

## 7. Риски / открытые вопросы

| Риск | Митигация |
|---|---|
| Android-back во время gated-шага закрывает окно *под* оверлеем → context-loss | Abort-and-retrigger уже корректен; UX (мигание) проверить плейтестом; при необходимости — back-suppression через Lock, только на `showText` |
| Поллинг `IsWindowShown` (0.25 с) | Для прототипа ок; сигнал `WindowShown` из UIManager — поздняя оптимизация, не зависимость |
| Прямоугольная жёсткая дырка | Шейдер с feather — позже, drop-in за тем же API `TutorialBlackoutView` |
| Нет локализации | Сырой RU-текст + зарезервированный `textKey`; миграция механическая |
| Дрейф string-id (таргеты/окна/квесты) | Валидатор §7 (⏳ не сделан), включая скан префабов на `TutorialTargetTag` |
| Резолв таргета сделан DI-реестром `ITutorialTargetRegistry` + фасадом `TutorialTargets` (для тегов) | Реализовано (§4.5); purge null при lookup |
| Resume/cancel-path Day 1 (§6.1) | Осознанные ограничения v1; recovery — позже |

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
  чистый View-слой; NodeCanvas-нода дёргает их так же, как C# step/content. В heroes ноды
  `ShowTutorialPointer/Blackout` управляли ровно такими независимыми view-контроллерами.
- `TutorialOverlayController` (runtime canvas), `TutorialTargetIds`, typed window checks.
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

> **2026-07-17: критерий сработал в пользу «не переходить».** Секвенции остались линейными, контент
> правит один человек (он же программист) — визуальный редактор не покупает ничего, а интеграционная цена
> из §8.1 платится независимо от прайса (в т.ч. у бесплатных xNode / Unity Behavior / GraphView). Вместо
> графов выбран **C#-авторинг** (§9): он снимает ту же боль (словарь шагов), но без DI↔POCO-моста,
> Addressables-латентности, vendor lock-in и деградации git-диффов. §8 остаётся актуальным как анализ на
> случай, если ветвистость всё-таки появится; §8.1 («что переиспользуется / что переписывается») дословно
> применим и к переезду на C#, минус перечисленные риски интеграции.

---

## 9. Переезд авторинга на C#-секвенции (2026-07-17)

### 9.1 Почему

Диагноз — **не «JSON слабый», а «словарь растёт 1:1 с контентом»**. Единственная секвенция на 4 шага
(`tutorial_day_1`) уже потребовала четыре новых элемента словаря: немодальный callout, наблюдение за
пассивной продажей, bespoke hit-area для не-Button таргета, condition `currentDayIs`. Когда словарь растёт
вровень с контентом — словарь *и есть* контент, а индирекция становится чистым налогом, амортизируемым
ровно по одному использованию.

Сравнение внутри проекта решает спор: `books.json` / `quests.json` / `dialogues.json` — **много экземпляров
одной формы** (добавить квест = строчка данных, ноль кода) → данные, JSON правилен. Туториальные дни —
**уникальные сценарные последовательности с нулевым переиспользованием** между собой → код.

Главный выигрыш — **растворяются слои косвенности, существующие только потому, что JSON-строка не умеет
ссылаться на тип**:

| Слой (§4.2–§4.3, §4.5) | Зачем существует | Судьба в C# |
|---|---|---|
| `ITutorialWindowChecker` + `TutorialWindowChecker` | строка `"results"` не ссылается на `ResultsWindow` | `_ui.IsWindowShown<ResultsWindow>()` |
| condition-factories (`currentDayIs`, …) | `"type":"dayIs"` не вызывает `_dayProgress` | `() => _dayProgress.CurrentDay == 1` |
| string-дискриминаторы + registry + 6 хендлеров | диспетчеризация по строке | шаг сам себя исполняет |
| `TutorialTargetIds` + скан префабов в валидаторе | адресация таргета строкой | типизированный доступ (**реестр остаётся**, см. §9.2) |

Побочно отпадает пункт §7 «editor-валидатор id-шников»: опечатка становится ошибкой компиляции, а не
runtime-warning'ом с auto-advance. Для Day 1 с Eddi выигрыш конкретен: факты продаж приходят из
`ISalesDayController` через `SalesTutorialSignalsBridge` → глобальный MessagePipe → leaf-сигналы
`Game.GameplayUI.Signals`. Так `Game.Tutorial.Content` не зависит от location-scope `Book.Sell`.

Референс формата — heroes (§2): `TutorialWhileWaiter(() => предикат)`, `TutorialSilentStepAction(() => эффект)`,
`AddOpenDailyQuestSteps(steps)` — произвольный предикат, произвольный side-effect и подпрограмма; ровно те
три вещи, которых у JSON нет.

**Что переезд НЕ чинит** (не переоценивать): немодальный callout и non-Button hit-area — работа по View,
нужна одинаково; re-evaluation loop (TODO GAME-18) живёт в `TutorialService` и нужен одинаково.

### 9.2 Зафиксированные решения

1. **Day-gate — строгий.** `IsEligible()` смотрит на `_dayProgress.Current.CurrentDay`, а не на историю секвенций.
   Причина: контент приварен к состоянию мира дня (приход Eddi — fire-once через delivered-dialogues; день-1-туториал,
   доигранный на дне 2, ждал бы продажу, которой не будет → тихий висяк). Догоняющий вариант («пропустил —
   доиграй позже») отвергнут. Строгий гейт лечит **два живых бага**, которые старая модель «очередь по priority»
   не лечит: (а) повторный вход в локацию в тот же день (подтверждено — возможен) после завершения day-1-секвенции
   выдаёт скану туториал дня 2 **в первый день**; (б) релонч сбрасывает день, но `tutorial.state` переживает →
   на перезапущенном дне 1 day-1 отсеян как completed, подхватывается day-2.
   **Следствие: day-gate — не полировка, а условие существования второй секвенции.**
2. **Таргет-реестр остаётся.** `ITutorialTargetRegistry` + `TutorialTargets` + `TutorialTargetTag` (§4.5) не трогаем —
   они решают «MonoBehaviour в сцене ↔ POCO в DI», и эта проблема не зависит от формата авторинга. Уходит только
   строковый id как *единственный* способ адресации + скан префабов из валидатора §7.
3. **`GetSteps()` — чистый и пересобираемый.** Никакого состояния в полях секвенции между вызовами
   (`resumePolicy: restart` пересоздаёт шаги с нуля). Подписки на события живут в run'е и снимаются в `finally`.
4. **Движок не трогаем.** Активация-скан, эксклюзивный runner, сейв `tutorial.state`, one-way completion, resume,
   transition-guard, `ITutorialAutoStartGate`, флаг `_autoStart` — остаются (§5 в силе). Меняется **только источник
   шагов**: `TutorialSequenceConfig.Steps[]` → `ITutorialSequence.GetSteps()`.
5. **Тексты не уносим в код.** Идут через `textKey` → INF-4 (этап 8). Это же снимает главный контраргумент
   к переезду: `tutorials.json` сегодня бесплатно наследует конфиг-пайплайн (`ServerConfigSource`, `AdminApiClient`,
   `ConfigEditorWindow`, `ConfigHistoryWindow`) — т.е. правится на сервере без билда. Но крутить там осмысленно
   только **тексты**, а они уезжают в INF-4 в любом случае; после этого в конфиге остался бы чистый флоу —
   ровно та часть, в которой JSON плох, и с нулевой live-ops-ценностью.

### 9.3 Этапы

Результаты, к которым идём: **(1)** день 1 с Eddi и неблокирующими текстами; **(2)** день 2 с NPC,
блокирующими текстами и ожиданием клика.

| # | Этап | Содержание | Критерий |
|---|---|---|---|
| 1 | **Каркас** | Новый asmdef `Game.Tutorial.Content` наверху графа (refs: `Game.Tutorial(.API)`, `Game.Core.UI`, `DayCycle`; **без** `VContainer` — POCO-ctor). Контракты `ITutorialSequence`/`ITutorialStep` + enum'ы `TutorialContext`/`TutorialResumePolicy`/`TutorialTrigger` в `Game.Tutorial.API`. `TutorialService`: ctor теряет `IConfigsService`/`IConditionParser`/`TutorialStepHandlerRegistry`, получает `IReadOnlyList<ITutorialSequence>`; каталог, `IsEligible`, шаг-луп переключаются. Порт `showText` + `awaitWindow` в step-классы, `TutorialDay1`. **Снос JSON-слоя**: обе `tutorials.json`, `TutorialSequenceConfig`/`TutorialStepConfig`, `TutorialStepTypes`, registry, `ITutorialStepHandler`, 6 хендлеров, `ITutorialWindowChecker`+`TutorialWindowChecker`, `TutorialTriggers`. Перегенерировать `manifest.json` через `Tools/Configs/Sync Bundled Defaults` | **Fresh save → день 1 играется бит-в-бит как сегодня**, но из класса. Старого пути не осталось. EditMode-тесты зелёные |
| 2 | **Строгий day-gate** | `IsEligible() => _dayProgress.Current.CurrentDay == N` (решение §9.2-1). Condition-factory `currentDayIs` **не пишется никогда** — в C# это однострочник (отменяет шаг 1 из TODO GAME-18) | Две секвенции в каталоге, каждая играется в свой день; повторный вход в локацию и релонч не выдают чужой туториал |
| 3 | **Немодальный callout** | Режим текста **без dim и без блокировки ввода** в `TutorialOverlayController`. Показ/обновление/скрытие — три отдельные операции (callout не ждёт тап), а не один await'ящий шаг. Гашение при отмене run'а — в `finally` секвенции | Текст висит, игра под ним живая и кликабельная |
| 4 | **День 1 с Eddi** → **результат 1** | `SalesTutorialSignalsBridge` в `Book.Sell` публикует `CustomerPhaseChanged`/passive sale/fail как primitive MessagePipe-сигналы. `TutorialDayOne` подписывается в `OnRunStarted`, держит latch-факты Eddi, сначала ждёт завершения Eddi-диалога через переход в `Browsing`, затем показывает callout-биты до `awaitWindow(Results)`; sale/fail жанры берутся из сигналов лениво в момент показа текста. Сценарные покупки (`Fact forceHit:true` / `Travel forceHit:false`) уже в `quests.json` — не трогаем. `Game.Tutorial.Content` получает refs на `MessagePipe` + `Game.GameplayUI.Signals`, **без** ref на `Book.Sell` | Тексты привязаны к реальным моментам симуляции; гонка со стартом дня закрыта latch'ами; отписка на cancel гарантирована |
| 5 | **Блокирующий клик по таргету** | Порт `highlightClick`. Закрыть дыру «не-Button таргет» (`TutorialOverlayController` — «empty hole catches no raycast»): bespoke hit-area, иначе панель `_genreBookCountPool` даёт warning + auto-advance | Дырка над панелью/item'ом, клик проходит и продвигает туториал |
| 6 | **День 2 с NPC** → **результат 2** | `TutorialDay2`: `text_1..3` (блокирующие, advance по тапу) → подсветка `_genreBookCountPool` + клик → `ContentWidgetController` открывается сам от клика, поверх блокирующий `text_4` → конец. N случайных пассивных покупателей — существующий спавнер | Сквозной прогон дня 2 |
| 7 | **Тексты → `textKey`/INF-4** | Решение §9.2-5. Можно параллельно этапам 4–6, но **форму принять на этапе 1**, иначе переписывать все биты | Сырых строк в контент-классах нет |
| 8 | **Остаток §7 + полировка** | Cheat-модуль (list/force-run/force-complete/reset), аналитика. Валидатор id-шников **отменён** (§9.1). Re-evaluation loop (GAME-18 шаг 2) — только когда понадобится запуск от покупки предмета/диалога; `locationLoaded` пока достаточен | — |

Этапы 1–2 — рефакторинг с проверяемым «ничего не изменилось», удобно катить отдельными коммитами.
Этап 3 добавляет text-only немодальный callout; pointer/highlight остаются этапом 5.
Этап 4 даёт первый продуктовый результат Day 1 с Eddi; прямой ref `Game.Tutorial.Content → Book.Sell` не используется.

### 9.4 Подводные камни (найдены при review дизайна этапа 1)

- **`GetSteps()` вызывался бы дважды с разными инстансами — живой баг.** `ResumeActiveSequence` клампит
  `NextStepIndex` по `seq.Steps.Length`, затем `BeginRun` перечитывает `seq.Steps`. С фабричным `GetSteps()`
  это два разных набора объектов. **Материализовать один раз** и прокинуть `IReadOnlyList<ITutorialStep>`
  в `BeginRun`/`RunSequenceAsync`.
- **`NextStepIndex` становится зависимостью от порядка в коде.** Позиционный индекс в C#-списке: перестановка
  шагов в патче молча возобновит живых игроков не на том шаге. Резюмиться **по `step.Id`** с fallback на 0.
  (У конфига был тот же изъян, но порядок шагов теперь едет вместе с кодом — радиус поражения больше.)
- **`TutorialContext` default обязан быть `Any`** — сегодня `null`/`"any"` трактуется пермиссивно (`ContextAllows`).
- **Base-class trap.** `TypeAnalyzer` использует `BindingFlags.DeclaredOnly`: абстрактный `TutorialSequenceBase`
  с ctor-зависимостями + наследник без своего ctor → `VContainerException: Type does not found injectable
  constructor`. Каждая секвенция объявляет **свой публичный ctor**, ровно один (несколько — не ошибка, VContainer
  молча берёт с максимумом параметров, что хуже).
- **Секвенции — только Global-scope зависимости.** Bootstrap форс-конструирует `ITutorialService` рано (до
  `SaveDataLoadOperation`); зависимость из scene-скоупа `GameInstaller` уронит стартап. `IUIManager`,
  `IDayProgressService`, `TutorialOverlayController` — Global, безопасны.
- **Потеря опциональности.** `TutorialService` объявляет `IDayProgressService`/`IGameFlowService` как `= null`
  (graceful degradation). Секвенции с *обязательным* `IDayProgressService` ломают эту опциональность осознанно:
  контенту day-gate нужен всегда, а зависимость зарегистрирована в Global scope.
- **Молчаливый ноль.** VContainer резолвит `IReadOnlyList<T>` даже при нуле регистраций (fallback → пустой
  массив, без исключения) — забытая регистрация секвенции падает молча. **Лог количества в `AfterLoadAsync`
  оставить.**
- **Комментарий в `TutorialVContainerBindings` про «две Func-регистрации» — симптом, не причина.** VContainer
  бьёт по любому дублю `ImplementationType` среди синглтонов. Регистрация N секвенций по конкретным типам
  `.As<ITutorialSequence>()` безопасна — те же 6 хендлеров это уже доказывают. Сам `Func<ITutorialService>` +
  `TutorialCompletedConditionFactory` **не трогать**: их потребитель — квесты (§4.4), не туториал.
- **`Game.Tutorial.asmdef` может убрать `Configs`**, но **не** `Newtonsoft.Json.dll` / `overrideReferences` /
  `Game.Conditions.API` — `TutorialCompletedConditionFactory` реализует `ICondition Create(JObject)`.
- **`SectionValidator` / `ConfigEditorWindow` менять не нужно** (генерические / хардкодят другой список);
  `ConfigsService` читает `[ConfigFile]` лениво, так что опубликованная на сервере секция `tutorials` просто
  перестанет запрашиваться — серверная чистка опциональна.
- **Гонка со стартом дня.** Sales simulation тикает независимо от `IUIManager.SetManualLock`, поэтому Eddi может
  пройти нужные фазы до того, как linear runner дойдёт до соответствующего await-step. Day 1 решает это latch'ами:
  подписка ставится в `OnRunStarted`, шаг завершения диалога ждёт реальный переход Eddi в `Browsing` без timeout,
  а sale/fail-шаги ждут уже запомненные факты с timeout + warning.

### 9.5 Цена решения

`Game.Tutorial.Content` сможет дотянуться до всего в проекте — для контент-слоя это правильно (`Game.Bootstrap`
уже так живёт, §3: «concrete-ссылки на окна живут ЗДЕСЬ»), но это **дверь в одну сторону**: обратно к «туториал
не знает о фичах» возврата не будет. Правило слоёв ([ASMDEF_RULES.md](../ASMDEF_RULES.md)) не нарушается —
`Infrastructure` и `Game.Tutorial` остаются чистыми, «всезнающим» становится только контент-сборка наверху графа.

### 9.6 Статус реализации

- Этап 1 «Каркас» реализован в коммите `5e09f52c`.
- Этап 2 «Строгий day-gate» реализован в коммите `8a527db7`.
- Этап 3 «Немодальный callout» реализован в коммите `d5a0b43b`.
- Этап 4 «День 1 с Eddi» реализован в коммите `4b65dc81`.
