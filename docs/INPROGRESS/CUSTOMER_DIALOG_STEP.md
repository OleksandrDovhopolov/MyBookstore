# GAME-6 — Runtime DialogStep в симуляции покупателей

> Статус: INPROGRESS (спека к реализации). Связано: [ADR-0003](../adr/0003-customer-simulation.md)
> (симуляция покупателей), [CUSTOMER_STEP_PIPELINE_REFACTOR.md](CUSTOMER_STEP_PIPELINE_REFACTOR.md),
> [QUESTS.md](../QUESTS.md) / [CHARACTERS_AND_QUESTS.md](../CHARACTERS_AND_QUESTS.md) (квест-персонажи),
> [WORLD_HUD.md](WORLD_HUD.md) (будущий вариант презентации 2.2), `UI_SYSTEM` (окна, вариант 2.1).

## 1. Цель (MVP)

Покупатель (в т.ч. квест-персонаж) приходит → **происходит диалог** → дальше стандартный процесс
пассивных покупок → уход. Для MVP: **прямые диалоги без ветвления**.

Презентация диалога сейчас делается через окно ([WindowController](../../Assets/Game/Core/UI/Controller/WindowController.cs),
вариант **2.1**, реф gossip harbor), но с гарантией **простого перехода на world-HUD** (вариант 2.2,
[WORLD_HUD.md](WORLD_HUD.md)) без изменения домена.

## 2. Ключевые архитектурные решения (подтверждены)

- **`DialogStep` — зеркало [`ActiveRequestStep`](../../Assets/Game/Features/BookSell/Domain/Steps/ActiveRequestStep.cs).**
  Чистый домен (`ICustomerStep`, без Unity-типов). Захват interaction lock в `Tick` (не в `Enter`),
  `Running` пока держит, `Blocked` если lock занят, `Exit` → `ctx.Lock.Release(self)`.
- **Пауза сима — бесплатно.** Тик-луп ломается на удержанном локе
  ([SalesDayController.cs:145](../../Assets/Game/Features/BookSell/Services/SalesDayController.cs#L145)
  `if (_lock.IsHeld) break;`), так что спавн и остальные покупатели встают на паузу на время диалога
  без доп. кода.
- **Единственный мост домен↔презентация — `ISalesDaySink`.** Диалог сигналит фактом
  `OnDialogueStarted(customer, payload)`; контроллер ре-эмитит публичный эвент `DialogueStarted` и
  заводит вход `CompleteDialogue()`. Это **точная копия** пути активной мини-игры
  ([ISalesDaySink.OnActiveRequestStarted](../../Assets/Game/Features/BookSell/Domain/ISalesDaySink.cs) →
  контроллер [:318](../../Assets/Game/Features/BookSell/Services/SalesDayController.cs#L318) →
  `RecommendBook`/`Skip` → `ResolveActive` [:361-368](../../Assets/Game/Features/BookSell/Services/SalesDayController.cs#L361)
  → `ForceCompleteCurrentStep`).
- **Seam 2.1↔2.2 = один presentation-класс**, подписанный на `DialogueStarted`. Он решает, открыть
  окно (2.1) или world-HUD-бабл (2.2). Ни `DialogStep`, ни sink, ни контроллер об этом не знают.
- **Квест-флоу собирается архетипом** ([`ICustomerArchetype.BuildMiddle`](../../Assets/Game/Features/BookSell/Services/Archetypes/ICustomerArchetype.cs)),
  а не runtime-вставкой: `QuestCharacterArchetype.BuildMiddle` = `[DialogStep(payload), PassivePurchaseStep, …]`.
  Известно на спавне — значит запекаем в план (парал­лель [`PassiveActivePassiveArchetype`](../../Assets/Game/Features/BookSell/Services/Archetypes/PassiveActivePassiveArchetype.cs)).
- **Payload — чистый DTO** (упорядоченные реплики), как [`CustomerCommentPayload`](../../Assets/Game/Features/BookSell/Domain/Steps/CommentStep.cs).
  Квест-осведомлённость входит на уровне архетипа/спавнера, не в шаге.
- **Контент диалогов — JSON-конфиг** `dialogues.json` (MVP: пара диалогов).

### Анти-паттерны (не делать)
- Не открывать `UIManager.ShowAsync<...>` изнутри `DialogStep` или `SalesDayController` — это тащит
  Unity/UI в чистый домен (нарушает контракт `ICustomerStep`/`Customer` «no Unity types») и хардкодит
  2.1, ломая переход на 2.2.
- Не добавлять `InsertNext` в [`CustomerDirector`](../../Assets/Game/Features/BookSell/Services/Director/CustomerDirector.cs)
  — его там нет и не должно быть; он гоняет только `IPassiveSaleRule`. Реальные швы вставки:
  `Customer.InsertNext` (план) + архетип (сборка).
- Не тащить UI-зависимости в `CustomerContext` (там уже есть TODO, что покупатель зря знает про
  Location/декор — новую зависимость не добавляем).

## 3. Этапы реализации

### Этап 1 — Домен: `DialogStep` + payload + фаза
- `DialoguePayload` (DTO, domain-сборка `Book.Sell.Domain`): `DialogueId` + `IReadOnlyList<string> Lines`
  (или `Line` для MVP-одностроч­ника). Без Unity-типов.
- `CustomerPhase.InDialogue` — новое значение, чтобы View поставил персонажа в позу разговора
  (пробрасывается штатно через `OnPhaseChanged`).
- `DialogStep : ICustomerStep` по образцу `ActiveRequestStep`:
  - `Enter`: `self.SetPhase(CustomerPhase.InDialogue, ctx, forceNotify: true)` (без Think-подфазы —
    заговаривает сразу).
  - `Tick`: если lock ещё не взят — `ctx.Lock.TryAcquire(self)` → успех: `ctx.Sink.OnDialogueStarted(self, payload)`,
    `Running`; занят: `Blocked`. Если взят — `Running` (ждём резолва контроллером).
  - `Exit`: `ctx.Lock.Release(self)`.
- **Тесты (edit-mode):** acquire→Running+sink вызван один раз; занятый lock→Blocked; `Exit` релизит;
  форс-комплит через `ForceCompleteCurrentStep` проходит. Референс — `ActiveRequestStepTests`,
  `InteractionLockTests`.

### Этап 2 — Sink + контроллер
- `ISalesDaySink`: добавить `void OnDialogueStarted(Customer customer, DialoguePayload payload)`.
- `SalesDayController`:
  - реализовать `OnDialogueStarted`: сохранить `_dialogueCustomer`, поднять публичный эвент
    `event Action<DialoguePayload> DialogueStarted`.
  - публичный `void CompleteDialogue()` (по образцу `SkipCurrentRequest`): проверка `_dialogueCustomer != null`
    → сбросить поле → `customer.ForceCompleteCurrentStep(_ctx)` → `UpdateDayPhase()`.
  - учесть `ForceCompleteDay`: как и активный, сбросить `_dialogueCustomer` (lock отпустится на Exit при
    следующем проходе, тик короткозамкнётся на фазе — см. существующий комментарий в `ForceCompleteDay`).
- **Тесты:** `OnDialogueStarted` поднимает эвент; `CompleteDialogue` завершает шаг и снимает lock; вызов
  без активного диалога — no-op + warning.

### Этап 3 — Конфиг диалогов
- `DialogueConfig` (`Game.Configs.Models`) c атрибутом `[ConfigFile("dialogues")]` (маппинг типа на файл,
  как у `QuestConfig`), поля `Id` + `Lines`.
- `dialogues.json` — JSON-массив, MVP наполнить парой диалогов:
  ```json
  [
    { "id": "dlg_intro_tilde", "lines": ["Здравствуйте! Слышал, у вас открылась лавка…", "Загляну на днях."] },
    { "id": "dlg_quest_01",    "lines": ["Мне нужна одна книга. Поможете?"] }
  ]
  ```
  Положить в `Assets/Configs/dialogues.json` **и** `Assets/StreamingAssets/Configs/dialogues.json`
  (рантайм грузит из StreamingAssets; см. `Tools/Configs/Sync Bundled Defaults`).
- **Тест:** десериализация `dialogues.json` в `DialogueConfig` (референс — `QuestConfigDeserializationTests`).

### Этап 4 — Архетип квест-персонажа
- `QuestCharacterArchetype : ICustomerArchetype`, `BuildMiddle` → `[new DialogStep(payload), new PassivePurchaseStep(), …]`.
  Payload резолвится из `DialogueConfig` по id (id даёт спавнер/`SalesSessionSetup`).
- Проводка: спавнер, который знает, что запланирован квест-персонаж X с диалогом Y, строит этим
  архетипом (точка входа квест-осведомлённости). `ScriptedSequenceArchetype` — при необходимости
  позже, отдельным шагом.
- **Тесты:** `BuildMiddle` даёт `[DialogStep, Passive…]` в правильном порядке (референс — `CustomerArchetypeTests`).

### Этап 5 — Презентация 2.1 (окно) — точка свапа на 2.2
- `DialogWindow : WindowController<DialogWindowView>` + `DialogWindowArgs` (несёт `DialoguePayload`).
- Presentation-координатор (в LocationScene-презентации, рядом с тем, что слушает `ActiveRequestStarted`):
  подписан на `SalesDayController.DialogueStarted` → `UIManager.ShowAsync<DialogWindow>(args)`; при
  закрытии окна («Continue») → `controller.CompleteDialogue()`.
- **Это единственный класс, который меняется при переходе на 2.2**: вместо `ShowAsync<DialogWindow>`
  — драйв world-HUD-бабла ([WORLD_HUD.md](WORLD_HUD.md)), тот же `CompleteDialogue()` на закрытии.
- Префаб окна/вью создаёт и назначает пользователь.

## 4. Что НЕ входит в MVP (заложено, но не делаем сейчас)
- Реактивные диалоги через `Customer.InsertNext` (по образцу `PassiveSaleCommentRule` → `CommentStep`) —
  механизм существует, оставляем на потом.
- Ветвление: payload с вариантами + `CompleteDialogue(choice)`; домен при этом не меняется.
- Свап на world-HUD (2.2) — только когда решение будет принято; изолирован Этапом 5.

## 5. Верификация
- Edit-mode тесты по этапам 1–4 (домен/контроллер/конфиг/архетип) — гоняются из Unity Editor.
- Ручной прогон (из CLI не собрать): квест-персонаж приходит → открывается диалоговое окно, сим на
  паузе (спавн/другие покупатели стоят) → «Continue» закрывает окно и снимает lock → покупатель
  переходит к пассивным покупкам и уходит.

## 6. Migration note 2.1 → 2.2
Домен (`DialogStep`, payload), sink (`OnDialogueStarted`) и контроллер (`DialogueStarted`/`CompleteDialogue`)
UI-агностичны. Переход на world-HUD = замена одного presentation-подписчика `DialogueStarted`. Если это
правило соблюдено — миграция не трогает `Book.Sell` вообще.
