# Character System

Архитектурная справка по фиче `Game.Characters` для MyBookstore. Персонаж — это **индекс над story-progression**: сами действия и состояния живут в `Game.Quest`, а `Characters` проецирует их в discovered-состояние и memories и отдаёт read-side для Journal.

> Все имена персонажей, memory и квестов в этом документе (`Character_01`, `memory_01`, `quest_01` и т.п.) — **только примеры-плейсхолдеры** для иллюстрации модели. Реальные id задаются в `characters.json`.

> **Status:** Stages 1-3 are implemented (module, configs, read-side, Quest integration, Journal classes, manual memory unlock, unseen counter). Stage 4 (presence/modifiers) is **deferred** until a design decision; the specification is preserved in §15.

---

## 0. Scope

- **В фокусе (сделано):** `character + memory` как data/read-side слой, интеграция с `Game.Quest` (discovery + memory unlock + события + персист), классы Journal (секция Characters).
- **Stamps вне фокуса.** Stamps — это завершённые *location*-задачи; относятся к локациям, не к персонажам. `Game.Characters` владеет только memories.
- **Shared memory отложена.** «Одна memory у двух персонажей» в текущей модели не поддерживается (одна memory = один персонаж). Потребует `memory → несколько characterId`.
- **Character modifiers / presence отложены** (§15) — нужно дизайн-решение, нужна ли механика.
- **Кросс-персонажные condition-фабрики** (§14) — отдельный будущий шаг.

---

## 1. Роль фичи

- Персонаж не владеет состоянием квеста — он его **читатель/проектор**.
- Source of truth для прогресса — `Game.Quest` (`QuestState` терминально и персистится самим Quest).
- `Game.Characters` хранит только то, что не выводится из квестов: persisted-флаг `Discovered` и леджер открытых memory (для одноразовости событий и устойчивости read-model).
- `Game.Characters` **не** дублирует quest lifecycle, condition-parser, rewards или save-машину квестов.

### 1.1. Черта vs действие — что можно класть в `CharacterConfig`

Правило: **`CharacterConfig` хранит постоянные черты персонажа. Сценарные действия в нём не живут** —
они принадлежат событию, которое их вызывает (квест). Это конкретизация строчки из самого конфига:
*«A character is an index over story progression — the actions themselves live in Game.Quest»*.

| Можно (черта — верна всегда) | Нельзя (действие — верно однократно/в контексте) |
|---|---|
| `FavoriteGenres` — Eddi любит Fact/Travel в любой сцене | `CustomerScriptConfig.PassiveAttempts` — «Fact hit, потом Travel miss» |
| `PortraitKey`, `DisplayNameKey`, `RoleKey` | одноразовые реплики, форсированные исходы, beat-sheet визита |
| `DiscoveryQuestIds` — связь, а не поведение | |

Проверка: *«верно ли это про персонажа всегда, в любой сцене?»* Да → черта, сюда. Нет → это сценарий
конкретной встречи, ему место в `CustomerScriptConfig`.

Исторический пример: `PassiveAttempts` (форсированный `Fact hit → Travel miss` у Eddi для туториала дня 1)
сначала пытались положить на персонажа — и он применялся бы к Eddi **навсегда, в любом квесте**, который
его заспавнит. Теперь одноразовая встреча живёт в `customer_scripts.json` (`eddi_intro`), а `QuestConfig`
остаётся только про quest-state.

---

## 2. Структура модуля

```text
Assets/Game/Features/Characters/
  API/        Game.Characters.API.asmdef         // контракты + read-models + DTO журнала
  Services/   (в Game.Characters.asmdef)         // runtime: сервис, фабрика, persistence
  UI/         Game.Characters.UI.asmdef          // Journal: window/view/row + view-models + builder
  Tests/Editor/ Game.Characters.Tests.Editor.asmdef
  Game.Characters.asmdef                          // runtime (корень фичи)
```

| Сборка | Ответственность | Ключевые references |
|---|---|---|
| `Game.Characters.API` | `ICharactersService`, `ICharacter`, `ICharacterMemory`, `CharacterJournalEntry/Memory`, события | `Configs`, `Game.Quest.API` |
| `Game.Characters` | `CharactersService` (`ISaveHook`+`IDisposable`), `CharacterModelFactory`, persistence | `Configs`, `Save`, `Game.Characters.API`, `Game.Quest.API`, `UniTask` |
| `Game.Characters.UI` | Journal-классы (Этап 3) | `Game.Characters.API`, `Game.Quest.API`, `Game.Core.UI`, `Infrastructure`, `VContainer`, `UniTask`, `Unity.TextMeshPro` |
| `Game.Characters.Tests.Editor` | EditMode-тесты | + `Game.Characters.UI` |

Граница: `Game.Characters` ссылается на `Game.Quest.API`, но **не** на `Game.Quest`. Runtime `IQuestsService` даёт DI. UI зависит только от `Game.Characters.API`, не от runtime-сборки.

---

## 3. Граница ответственности

`Game.Quest` владеет: состояниями (`Pending/Active/ReadyToAward/Awarded/Failed`), задачами и прогрессом, парсингом/оценкой condition-деревьев, переходами `NextQuestIds`, наградами и world-effects, событиями (`QuestStarted/QuestCompleted/QuestAwarded/QuestFailed/TaskCompleted/TaskProgressChanged`).

`Game.Characters` владеет: профилем персонажа (из конфига), persisted `Discovered`, леджером открытых memory, связкой `characterId ↔ quests/chains`, read-model для Journal.

`Game.Characters` **не** считает прогресс задач, не парсит условия, не выдаёт quest-награды, не хранит копию quest-state, не дёргает реализации других фич напрямую (только через API).

---

## 4. Данные

Конфиги живут в общей сборке `Configs` (`Game.Configs.Models`, `[ConfigFile]`), как `QuestConfig`/`BookConfig`. Файл — `characters.json` (JSON-массив).

```csharp
[ConfigFile("characters")]
public sealed class CharacterConfig : IConfig
{
    public string Id { get; set; }
    public string DisplayNameKey { get; set; }
    public string RoleKey { get; set; }
    public string DescriptionKey { get; set; }
    public string PortraitKey { get; set; }                 // Addressables-ключ портрета (пусто → заглушка)
    public string[] FavoriteGenres { get; set; }            // жанры персонажа для passive-профиля
    public bool HiddenInJournal { get; set; }               // service/protagonist character is hidden from People

    public string[] DiscoveryQuestIds { get; set; }         // явные discovery-связи (intro/dialogue-квесты без memory)
    public string[] DiscoveryQuestChainIds { get; set; }

    public CharacterMemoryConfig[] Memories { get; set; }
}

public sealed class CharacterMemoryConfig
{
    public string Id { get; set; }
    public string TitleKey { get; set; }
    public string DescriptionKey { get; set; }
    public string PhotoKey { get; set; }                    // memory в Journal — это фотография
    public string QuestId { get; set; }                     // открывается, когда этот квест Awarded
    public string QuestChainId { get; set; }                // открывается, когда финальный квест цепочки Awarded
    public bool UnlockedAtStart { get; set; }               // FTUE/manual bootstrap unlocks without a quest
    public int Order { get; set; }                          // authored Memories sort order, higher = earlier
    public bool IsGolden { get; set; }                      // UI-флаг значимости (награды всё равно через Game.Quest)
}
```

> `DiscoveryQuestIds` / `DiscoveryQuestChainIds` открывают персонажа, когда связанный квест/цепочка **стартовали** (`state != Pending`), а не когда квест завершён или выдан (`Awarded`). Для открытия строго после завершения используйте memory-связь или отдельное quest-condition решение.

> Скриптовые passive-действия (например authored hit/miss для intro-покупателя) живут в `CustomerScriptConfig`, а не на `CharacterConfig`. `FavoriteGenres` — стабильная черта персонажа; форсированные исходы продажи — действия конкретной встречи.

Save (module key `"characters"`, `StateSchemaVersion = 2`):

```csharp
public sealed class SavedCharacters
{
    public Dictionary<string, SavedCharacter> Characters { get; set; }
    public HashSet<string> SeenMemoryIds { get; set; }      // flat ledger of seen memoryId values
}

public sealed class SavedCharacter
{
    public bool Discovered { get; set; }                    // persisted-флаг открытия
    public HashSet<string> UnlockedMemoryIds { get; set; }  // manual/quest ledger of unlocked memories
}
```

> Save key is `"characters"` plus a separate `schemaVersion` (like `"quests"`/`"inventory"`), **not** the string `"characters.v1"`. Migration from v1 to v2 does not require custom code: missing `SeenMemoryIds` is initialized by the service.

---

## 5. Слои данных

```text
CharacterConfig         // статика из characters.json
  + SavedCharacter      // persisted: Discovered + UnlockedMemoryIds
  + IQuestsService      // текущее состояние квестов (source of truth)
  -> CharacterModel / CharacterJournalEntry   // пересобираемые read-models
```

`CharacterModel`/`CharacterJournalEntry` — пересобираемые на каждом чтении. UI не склеивает `CharacterConfig` + save + `QuestState` сам, а получает готовую read-model из `ICharactersService`.

---

## 6. CharacterModelFactory (правила derive)

`internal CharacterModelFactory : ICharacterModelFactory` зависит **только** от `IQuestsService`. Строится внутри `CharactersService` (в DI не регистрируется).

```csharp
internal interface ICharacterModelFactory
{
    CharacterModel Create(CharacterConfig config, SavedCharacter saved);
    CharacterJournalEntry CreateJournalEntry(CharacterConfig config, SavedCharacter saved);
    bool IsUnlockedByQuest(CharacterMemoryConfig mc);      // чистый quest-derive
    bool IsDiscoveredByQuest(CharacterConfig config);      // чистый quest-derive
}
```

Правила:

- **memory by `QuestId`**: unlocked ⇔ `GetQuestState(questId) == Awarded`.
- **memory by `QuestChainId`**: unlocked ⇔ `GetChain(chainId)?.FinalQuest?.State == Awarded`.
- **read-model `memory.Unlocked` = `IsUnlockedByQuest(mc) || saved.UnlockedMemoryIds.Contains(mc.Id)`** — quest-derive remains primary; the ledger supports manual/FTUE unlock and one-shot events.
- **read-model `Discovered` = `saved?.Discovered ?? false`** (persisted-флаг).
- **`IsDiscoveredByQuest`**: true, если любой из `DiscoveryQuestIds` / memory-`QuestId` имеет `GetQuestState != Pending`, либо любой `DiscoveryQuestChainId` / memory-`QuestChainId` имеет `GetChain(c)?.CurrentQuest?.State != Pending`.
- **Journal-link** для chain-memory: `LinkedQuestId` и `LinkedQuestState` оба из `FinalQuest` (id и state согласованы).

Неизвестный quest/chain → `Pending` → locked, без исключений.

Фабрика не пишет save, не подписывается на события, не открывает memories, не активирует квесты, не знает о UI.

---

## 7. CharactersService

`public sealed class CharactersService : ICharactersService, ISaveHook, IDisposable`. Зависимости: `ISaveService`, `IConfigsService`, `IQuestsService`, `ICharactersRepository`. Фабрику создаёт сам из `IQuestsService`.

- `AfterLoadAsync`: `BuildCatalog()` → load save → `Reconcile()` → silent unseen recompute → `Subscribe()`.
- `BuildCatalog`: строит каталог из `IConfigsService.GetAll<CharacterConfig>()` и **обратный индекс** `questId/chainId → characterId` из `DiscoveryQuestIds` + `DiscoveryQuestChainIds` + memory `QuestId`/`QuestChainId`.
- `Reconcile()` (до подписки, без фаяринга): засеять `Discovered`/`UnlockedMemoryIds` из текущего состояния квестов — покрывает переходы, которые Quest зафаярил в своём `AfterLoadAsync` **до** нашей подписки, и не «переигрывает» события при каждом запуске.
- Подписка на `QuestStarted`/`QuestAwarded`; событие → по индексу найти персонажа → пересчитать discovery (raise при новом) и memories (raise при новом).
- `TryUnlockMemory(characterId, memoryId)`: idempotent manual unlock through the ledger; validates the character and memory, does not fire twice, and raises `MemoryUnlocked` only for a real new unlock.
- `UnseenMemoryCount` / `HasUnseenMemories`: count unlocked memories missing from `SeenMemoryIds`; recomputed after load, quest/manual unlock, and `MarkAllMemoriesSeen()`.
- `MarkAllMemoriesSeen()`: marks currently unlocked memories as seen. Journal calls this when entering the `Memories` tab; repeated calls are no-ops.
- `BeforeSaveAsync`: если `_dirty` — пишет `_saved` через repository.
- `Dispose`: отписка.

Memory unlock idempotency goes through `HashSet<string>.Add` (ledger): repeated `QuestAwarded` or `TryUnlockMemory` does not fire the event a second time.

---

## 8. Интеграция с Game.Quest

`Game.Quest` **не изменён** под Characters: никаких character-aware методов в `IQuestsService`. Маршрутизация — на стороне Characters через обратный индекс из собственного конфига; читаются только generic `GetQuestState(id)` / `GetChain(id)` (+ события `QuestStarted`/`QuestAwarded`).

- **Discovery**: персонаж открыт, когда стартовал (`!= Pending`) любой из его `DiscoveryQuestIds`/`DiscoveryQuestChainIds` или любой memory-квест. Покрывает intro/dialogue-квест без memory.
- **Memory unlock**: на `QuestAwarded`, если квест/финал цепочки соответствует memory.
- **Manual/FTUE unlock**: `TryUnlockMemory` unlocks a memory without a quest link. Before checking `ftue.applied`, `FtueBootstrapper` unlocks every `CharacterMemoryConfig.UnlockedAtStart`, so old saves with FTUE already applied still receive the intro memory without granting starter resources again.
- **Timing**: `QuestsService` фаярит начальные/offline-переходы в своём `AfterLoadAsync` до нашей подписки → обязателен `Reconcile()` на загрузке (засев без фаяринга).

Пример (id — плейсхолдеры):

```json
{
  "id": "character_01",
  "displayNameKey": "character.character_01.name",
  "portraitKey": "portrait_character_01",
  "discoveryQuestIds": ["quest_intro_01"],
  "memories": [
    { "id": "memory_01", "questChainId": "chain_01", "isGolden": false },
    { "id": "memory_02", "questId": "quest_02", "isGolden": true }
  ]
}
```

---

## 9. API

```csharp
public interface ICharactersService
{
    ICharacter TryGetCharacter(string characterId);
    IEnumerable<ICharacter> GetAllCharacters();
    IEnumerable<ICharacter> GetDiscoveredCharacters();
    bool IsDiscovered(string characterId);
    bool IsMemoryUnlocked(string characterId, string memoryId);
    bool TryUnlockMemory(string characterId, string memoryId);
    CharacterJournalEntry GetJournalEntry(string characterId);
    int UnseenMemoryCount { get; }
    bool HasUnseenMemories { get; }
    void MarkAllMemoriesSeen();

    event Action<ICharacter> CharacterDiscovered;
    event Action<ICharacterMemory> MemoryUnlocked;
    event Action UnseenMemoriesChanged;
}

public interface ICharacter
{
    string Id { get; }
    bool Discovered { get; }
    CharacterConfig Config { get; }
    IReadOnlyList<ICharacterMemory> Memories { get; }
}

public interface ICharacterMemory
{
    string Id { get; }
    string CharacterId { get; }
    bool Unlocked { get; }
    bool IsGolden { get; }
    string QuestId { get; }
    string QuestChainId { get; }
}
```

`CharacterJournalEntry` is the flat Journal read-model: `CharacterId`, `Discovered`, `HiddenInJournal`, `DisplayNameKey`, `RoleKey`, `PortraitKey`, `FavoriteGenres`, `CharacterJournalMemory[]` (`MemoryId`, `CharacterId`, `Unlocked`, `IsGolden`, `Order`, `TitleKey`, `DescriptionKey`, `PhotoKey`, `LinkedQuestId`, `LinkedQuestState`).

UI получает готовую read-model, а не `QuestConfig` напрямую.

---

## 10. Journal — секция Characters (Этап 3)

Слой `Game.Characters.UI` по паттерну проекта (`WindowController<TView>` + `WindowView` + pooled rows; контроллер не регистрируется в DI — `new` + `_resolver.Inject`).

- `JournalCharactersViewModelBuilder` (чистый, тестируемый): `Build(IEnumerable<ICharacter>, Func<string, CharacterJournalEntry>)` → список `JournalCharacterItemModel` (с `Locked => !IsDiscovered`, счётчиками memory, `PortraitKey`).
- `JournalWindow`/`JournalWindowView`/`JournalCharacterRowView`: live-рефреш по `CharacterDiscovered`/`MemoryUnlocked`.
- `People` shows only discovered characters and also filters `HiddenInJournal`, so service characters such as `owner` stay out of the list.
- `Memories` builds a flat feed from all characters, including `HiddenInJournal`, but shows only unlocked memories and sorts by `Order` descending.
- Entering the `Memories` tab calls `MarkAllMemoriesSeen()` and clears the unseen counter.

Префаб + Addressables-адрес `JournalWindow` + кнопка открытия — ручной шаг в редакторе (вне «классов»).

---

## 11. Регистрация в DI

`Assets/Game/Core/Installers/Features/CharactersVContainerBindings.cs`:

```csharp
public static void RegisterCharacters(this IContainerBuilder builder)
{
    builder.Register<ICharactersRepository, SaveBackedCharactersRepository>(Lifetime.Singleton);
    builder.Register<CharactersService>(Lifetime.Singleton).As<ICharactersService>();
    // ICharacterModelFactory намеренно НЕ регистрируется — строится внутри CharactersService.
}
```

В `BootstrapInstaller` — `RegisterCharacters()` **после** `RegisterQuest()` (нужен `IQuestsService`; `CharactersService` — `ISaveHook`, конструируется до загрузки save; `IDisposable` диспоузит VContainer).

---

## 12. Доставка конфига

- Editor: `Assets/Configs/characters.json` (live-источник).
- Build: `Assets/StreamingAssets/Configs/characters.json` + запись в `manifest.json`. Поддерживается через `Tools/Configs/Sync Bundled Defaults to StreamingAssets` (копирует все `Assets/Configs/*.json` и регенерит manifest). Bundled defaults нужны как fallback даже при серверном источнике.

---

## 13. Пример набора персонажей (только иллюстрация)

Модель не зависит от конкретного числа персонажей. Для наглядности — обобщённые архетипы цепочек (id — плейсхолдеры):

| Персонаж | Архетип цепочки | Golden memory | Задействованные системы |
|---|---|---|---|
| `Character_01` | mentor/endgame-цепочка, доставки, финальный «наследник» | `memory_01_golden` | Quest, Inventory, Shop |
| `Character_02` | декор/погода-зависимая ветка, выпуск | `memory_02_golden` | Quest, Decor, DayCycle, Locations |
| `Character_03` | рукописи/печать/публикации, клубы | `memory_03_golden` | Quest, Inventory, Location events |
| `Character_04` | vendor-локация, продажи на месте, world-state | `memory_04_golden` | Quest, Shop, SalesStats |
| `Character_05` | коллекция фрагментов, локация-исследование | `memory_05_golden` | Quest, Collection, Locations |

Замечания по модели (без привязки к конкретному контенту):

- У персонажа обычно **одна golden memory** в конце цепочки. `IsGolden` — флаг значимости для UI; награды (в т.ч. предметы) идут стандартным quest-reward flow в `Game.Quest`, `Game.Characters` наград не выдаёт.
- **Не каждый NPC = персонаж.** Минорные NPC, выдающие только location-контент, **не** получают `CharacterConfig`/memories — это «диктор» контента локаций (относится к stamps/локациям).
- Случай «общая memory у двух персонажей» — это отложенный shared-memory кейс (см. §0).

---

## 14. Кросс-персонажные условия (будущее, отложено)

Иногда нужен мета-гейт «открыть финальный квест, когда сделаны N golden memory у разных персонажей». Поскольку **memory — проекция awarded-квеста**, такие гейты авторятся как условия над состоянием квестов, а не над memory-словарём (иначе цикл `Conditions → Characters → Quest.API`).

План (отдельный шаг, ещё не реализован): добавить в фиче Quest `IConditionFactory`, зависящий только от `IQuestsService`, два типа:

```json
{ "type": "questState", "quest": "quest_02", "state": "Awarded" }
{ "type": "questsAwardedCount", "quests": ["quest_a", "quest_b", "quest_c", "quest_d"], "min": 4 }
```

`questsAwardedCount` заменяется на `allOf` из нескольких `questState` (composite уже есть). `Game.Characters` спец-логики не получает.

> Тонкость DI: такая фабрика упирается в цикл (`QuestsService → IConditionParser → IConditionFactoryRegistry → фабрика → IQuestsService`) и требует ленивого резолва — поэтому это самостоятельная задача.

---

## 15. Этап 4 — presence/modifiers (отложено)

> **Статус: не делаем сейчас.** Спецификация сохранена; перед реализацией нужно дизайн-решение, нужна ли механика «эффекты от присутствия персонажа» вообще (в текущей модели продаж модификаторы идут от **экипированных предметов/декора**, не от присутствия NPC).

Если механика подтверждена — паттерн уже есть в проекте: `IDecorModifierProvider` (контракт в `Book.Sell.API`, реализация в `Game.Decor`, потребляется в `EconomyBasedSaleChanceCalculator`). Character-модификаторы повторяют его:

- **Presence** — `ICharacterPresenceService.GetPresentCharacters(locationId, day)` поверх `CharacterScheduleRuleConfig[]` + `IDayProgressService.Current`; гейт по `Discovered`.
- **Modifiers** — контракт `ICharacterModifierProvider.GetActiveModifiers(locationId, day)` в `Book.Sell.API`, реализация в `Game.Characters`; типы эффектов: `saleChance.genre.<X>`, `saleChance.any`, `moneyPerSale`, `decorTagEffect.<tag>`.
- **Интеграция** — домножить `charMod` рядом с `decorMod` в `EconomyBasedSaleChanceCalculator.Compute`; параллельные швы для money-per-sale / daily-expense. Только через API; `Game.Characters` не ссылается на impl `Book.Sell`.
- **Config** — добавить отложенные `CharacterScheduleRuleConfig[]`, `CharacterModifierConfig[]` в `CharacterConfig`.

Порядок: (0) гейт-решение → (1) presence + тесты → (2) modifier-провайдер + тесты → (3) интеграция в sale-chance последним шагом.

---

## 16. Этапы реализации

| Этап | Состав | Статус |
|---|---|---|
| 1 | Модуль (API/runtime/tests), `CharacterConfig`/`CharacterMemoryConfig`, `characters.json`, read-side сервис, save | ✅ |
| 2 | Интеграция с Quest: discovery (`DiscoveryQuestIds` + memory-квесты, обратный индекс), memory unlock, события, reconcile-on-load, персист, идемпотентность | ✅ |
| 3 | Journal-классы (`Game.Characters.UI`): builder + item-models + window/view/row; показываем всех, заглушка при `Locked`, портрет по `PortraitKey` | ✅ (префаб/Addressables/кнопка — ручной шаг) |
| 4 | presence/modifiers | ⏸ отложено (§15) |
| — | кросс-персонажные condition-фабрики, shared memory, stamps | ⏸ будущее |

---

## 17. Зафиксированные решения

- Memory открывается на `Awarded` (не `ReadyToAward`).
- Golden memory — флаг `IsGolden`, не отдельная сущность; награды через `Game.Quest`.
- Discovery — через явные `DiscoveryQuestIds`/`DiscoveryQuestChainIds` + memory-квесты (не через character-aware API в Quest).
- read-model `Unlocked = questDerived || ledger`; `Discovered` — persisted-флаг.
- `Game.Quest` под Characters не меняется; связь — `CharacterId` как пассивное data-поле + generic-чтение `IQuestsService`.
- UI зависит только от `Game.Characters.API`.
