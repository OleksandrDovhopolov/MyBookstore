# GAME-6 — Runtime DialogStep в симуляции покупателей

> Статус: INPROGRESS (спека к реализации). Связано: [ADR-0003](../adr/0003-customer-simulation.md)
> (симуляция покупателей), [CUSTOMER_STEP_PIPELINE_REFACTOR.md](CUSTOMER_STEP_PIPELINE_REFACTOR.md),
> [QUESTS.md](../QUESTS.md) / [CHARACTERS_AND_QUESTS.md](../CHARACTERS_AND_QUESTS.md) (квест-персонажи),
> [WORLD_HUD.md](WORLD_HUD.md) (будущий вариант презентации 2.2), `UI_SYSTEM` (окна, вариант 2.1).

## 1. Цель (MVP)

Покупатель (в т.ч. квест-персонаж) приходит → **происходит диалог** → дальше стандартный процесс
пассивных покупок → уход. Для MVP: диалог с **2–3 вариантами ответа** (не больше), но варианты
**чисто разговорные** — ведут к другим репликам / разному завершению беседы и **не влияют на
геймплей** (не меняют исход квеста, награду, план покупателя). Следствие: домен и `CompleteDialogue()`
**не меняются** (см. §2); по узлам ходит презентационный движок диалога.

> Статус: Этапы 1–2 (домен + контроллер) реализованы. Изменение требований (варианты ответа) затрагивает
> только Этапы 3 и 5 — домен/контроллер уже готовы и не переделываются.

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
- **Payload — чистый DTO с одним ключом** `DialogueId` (корневой узел; граф резолвит презентация из
  `dialogues.json`), как `CustomerCommentPayload` оставляет текст view. Квест-осведомлённость входит на
  уровне архетипа/спавнера, не в шаге.
- **Контент диалогов — JSON-конфиг** `dialogues.json` (MVP: пара диалогов).
- **Варианты ответа — чисто разговорные (решение зафиксировано).** 2–3 опции, ведут к другим репликам
  или к концу беседы; **геймплейного эффекта нет**. Поэтому `CompleteDialogue()` остаётся **без
  параметров**, а домен (`DialogStep`, payload) про варианты не знает. Геймплейно-значимый выбор
  (`CompleteDialogue(choiceId)` / план-ветвление через `Customer.InsertNext`) — **вне MVP** (§4).
- **Движок диалога — на презентационной стороне, view-agnostic.** Читает граф `DialogueConfig` по
  корневому `DialogueId`, держит текущий узел, отдаёт вью «реплики + опции», по выбору переходит к
  следующему узлу/концу и в конце зовёт `CompleteDialogue()`. Вью (окно/HUD) — тупой рендер. **Одна
  беседа = один held-lock `DialogStep`**; движок крутит узлы **внутри** одной сессии — не разбивать
  беседу на несколько шагов, иначе lock отпустится между узлами и другой покупатель влезет в середину.

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
- `DialoguePayload` (DTO, неймспейс `Book.Sell.API`, рядом с `CustomerCommentPayload`): **только**
  `DialogueId` (ключ контента). Реплики домен не несёт — их резолвит контент/презентация из
  `dialogues.json` по id (единый источник правды, дружит с локализацией). Fail-fast: пустой id →
  `ArgumentException`. Без Unity-типов.
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

### Этап 3 — Конфиг диалогов (мелкий граф под 2–3 варианта)
- `DialogueConfig` (`Game.Configs.Models`) c атрибутом `[ConfigFile("dialogues")]` (маппинг типа на файл,
  как у `QuestConfig`). Не «Id + Lines», а **плоский граф**: `Id` (корень) + список узлов; узел =
  `{ NodeId, Lines, Options[] }`; опция = `{ TextKey/Text, Next: nodeId | end }`. **Не строить Twine** —
  «2–3 опции, не больше», глубина мелкая; достаточно «узел → опции → [end | next]». Финальный узел без
  опций (или опция `Next=end`) = конец беседы → движок зовёт `CompleteDialogue()`.
- `dialogues.json` — JSON-массив, MVP наполнить парой диалогов, минимум один с выбором:
  ```json
  [
    {
      "id": "dlg_intro_tilde",
      "nodes": [
        { "nodeId": "root", "lines": ["Слышал, у вас открылась лавка…"],
          "options": [
            { "text": "Заходите!",        "next": "warm" },
            { "text": "Мы ещё готовимся.", "next": "cool" }
          ] },
        { "nodeId": "warm", "lines": ["Тогда до встречи."], "options": [] },
        { "nodeId": "cool", "lines": ["Понимаю, загляну позже."], "options": [] }
      ]
    },
    {
      "id": "dlg_quest_01",
      "nodes": [ { "nodeId": "root", "lines": ["Мне нужна одна книга. Поможете?"], "options": [
        { "text": "Конечно", "next": "end" }, { "text": "Позже", "next": "end" } ] } ]
    }
  ]
  ```
  Источник правды — `Assets/Configs/dialogues.json`. Рантайм (`StreamingAssetsConfigSource`) грузит только
  файлы из `manifest.json`, поэтому нужна копия `Assets/StreamingAssets/Configs/dialogues.json` **и** запись
  `"dialogues.json"` в `manifest.json`. Канонически это делает `Tools/Configs/Sync Bundled Defaults`
  (перегенерит манифест из `Assets/Configs/`); в GAME-6 сделан точечный ручной patch (Sync заодно втянул бы
  `tutorials.json`). `.meta` новых json — закоммитить.
- **Тест:** десериализация `dialogues.json` в `DialogueConfig` + обход графа (узел → опция → next/end)
  (референс — `QuestConfigDeserializationTests`).

### Этап 4 — Архетип квест-персонажа (только архетип + тесты, БЕЗ прод-спавна)
- `QuestCharacterArchetype : ICustomerArchetype`, `BuildMiddle` → `[new DialogStep(payload), new PassivePurchaseStep() ×N]`.
  Детерминирован (фиксированный `passiveCount`, random не потребляет); payload передаётся в ctor.
- **⚠️ Никакого прод-спавна на этом этапе.** Реальное расписание/спавн `DialogStep`-покупателя перенесено
  в §Этап 5 — потому что единственный, кто зовёт `CompleteDialogue()`, это презентация (§Этап 5). Спавн
  диалога до неё навсегда удержит interaction lock и повесит день (`SalesDayController.Tick` встаёт на
  held lock). `QuestCharacterArchetype` в DI/спавнеры **не регистрируется**, используется только тестами.
- **Тесты:** структурный (`BuildMiddle` → `[DialogStep, Passive…]`, порядок, тот же payload; edge
  `passiveCount:0` / null payload) — `CustomerArchetypeTests`; интеграционный через
  `CustomerPlanBuilder` + контроллер (`Approach→Dialog→Passive→CompletePurchase→Leave`, завершение
  тестовым `CompleteDialogue()`, ассерты пассивной продажи + purchase-completed) — `SalesDayControllerTests`.

### Этап 5 — Презентация: движок диалога + вью (2.1 окно) — точка свапа на 2.2
Теперь это **не «просто открыть окно»**, а два компонента (как и просил дизайн: «движок + вью»):
- **Движок диалога** (presentation-side, view-agnostic): грузит граф `DialogueConfig` по корневому
  `DialogueId`, держит текущий узел, отдаёт вью «реплики + 2–3 опции», по выбранной опции переходит к
  `next`-узлу или к `end`; на `end` зовёт `controller.CompleteDialogue()`. Про Unity-окна не знает.
- **Вью** `DialogWindow : WindowController<DialogWindowView>` + `DialogWindowArgs` — тупой рендер:
  показать реплики + кнопки опций, вернуть выбранную опцию движку. (На 2.2 вью заменяется world-HUD-баблом,
  движок тот же.)
- **Координатор** (в LocationScene-презентации, рядом с тем, что слушает `ActiveRequestStarted`):
  подписан на `SalesDayController.DialogueStarted` **до первого `Tick`** → создаёт/запускает движок для
  пришедшего `(customer, payload)` и показывает вью. Завершение беседы → `CompleteDialogue()`.
- **Свап на 2.2 = замена только вью** (движок и координатор не трогаются): `ShowAsync<DialogWindow>` →
  драйв world-HUD-бабла ([WORLD_HUD.md](WORLD_HUD.md)), тот же `CompleteDialogue()` на `end`.
- Префаб окна/вью (реплики + до 3 кнопок опций) создаёт и назначает пользователь.
- **Реальное расписание/спавн (перенесено из §Этап 4 — включается ТОЛЬКО вместе с презентацией, иначе
  hang):** источник квест-персонажей дня (напр. поле `DayConfig.scheduledDialogueIds`) → `SalesSessionSetup`
  → setup-provider (`PreparationSalesSetupProvider`) → decorator-спавнер, который строит
  `QuestCharacterArchetype`-покупателя из `DialogueId`, и регистрация этого спавнера в DI. Это и есть
  «точка входа квест-осведомлённости»; делать её нужно в одном куске с движком/вью, чтобы у первого
  прод-спавна сразу был `CompleteDialogue()`-завершатель.

## 4. Что НЕ входит в MVP (заложено, но не делаем сейчас)
- Реактивные диалоги через `Customer.InsertNext` (по образцу `PassiveSaleCommentRule` → `CommentStep`) —
  механизм существует, оставляем на потом.
- **Геймплейно-значимый** выбор: `CompleteDialogue(choiceId)` (результат читает quest-система) или
  план-ветвление выбранной опцией через `Customer.InsertNext`. В MVP варианты ответа **чисто
  разговорные** (§1/§2), поэтому этот канал не строим, пока нет потребителя — домен и `CompleteDialogue()`
  остаются без параметров.
- Свап на world-HUD (2.2) — только когда решение будет принято; изолирован Этапом 5.

## 5. Верификация
- Edit-mode тесты по этапам 1–4 (домен/контроллер/конфиг/архетип) — гоняются из Unity Editor.
- Ручной прогон (из CLI не собрать): квест-персонаж приходит → открывается диалоговое окно с репликами
  и 2–3 опциями, сим на паузе (спавн/другие покупатели стоят) → выбор опции ведёт по узлам графа до
  конца беседы → `CompleteDialogue()` снимает lock → покупатель переходит к пассивным покупкам и уходит.
  Проверить ветку с выбором (разные `next`) и одноузловой диалог.

## 6. Migration note 2.1 → 2.2
Домен (`DialogStep`, payload), sink (`OnDialogueStarted`) и контроллер (`DialogueStarted`/`CompleteDialogue`)
UI-агностичны. Переход на world-HUD = замена одного presentation-подписчика `DialogueStarted`. Если это
правило соблюдено — миграция не трогает `Book.Sell` вообще.
