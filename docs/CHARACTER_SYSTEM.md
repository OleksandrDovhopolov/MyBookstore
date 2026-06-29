# Character System

Документ описывает, как построить отдельный модуль персонажей для MyBookstore, вдохновляясь структурой Tiny Bookshop. Текущий результат: сбор информации, архитектурное решение, границы asmdef-модуля и список вопросов перед реализацией.

Источники:

- https://tiny-bookshop.fandom.com/wiki/Characters
- https://tiny-bookshop.fandom.com/wiki/Challenges
- https://tiny-bookshop.fandom.com/wiki/Journal#Characters

Дата разбора: 2026-06-29.

---

## 1. Что важно из Tiny Bookshop

В Tiny Bookshop персонажи не являются просто NPC-аватарами. Они работают как слой над квестами, журналом и миром:

- Есть 8 named characters: Tilde, Anne, Fern, Walt, Klaus, Maryam, Moira, Harper.
- Challenges ведут игрока через историю, открывают предметы и появляются в Journal после unlock.
- Завершенные location challenges отображаются как stamps, а завершенные NPC challenges - как memories у персонажей.
- Journal имеет отдельные разделы: Stamps, Characters, Equipped Items, Calendar.
- Character-раздел Journal показывает найденных персонажей и связанные с ними memories.
- Большинство NPC-цепочек зависит от уже существующих систем: продажи по жанрам, продажи в конкретной локации, продажи за день, рекомендации, экипированный декор/предметы, погода, сезон/дата, посещение локации, диалоги и unlock предыдущих challenges.

Вывод для MyBookstore: персонажи должны стать отдельной фичей, но основной прогресс их историй должен идти через `Game.Quest`. `Game.Characters` не должен дублировать quest lifecycle, condition parser, rewards или save-машину квестов.

---

## 2. Архитектурное решение

Создать отдельный модуль:

```text
Assets/Game/Features/Characters/
  API/
    Game.Characters.API.asmdef
  Game.Characters.asmdef
  Tests/Editor/
    Game.Characters.Tests.Editor.asmdef
```

Роли модулей:

| Модуль | Ответственность |
|---|---|
| `Game.Characters.API` | Публичные интерфейсы, DTO/read models, события открытия персонажа и memories |
| `Game.Characters` | Runtime-сервис, config mapping, save-backed состояние, связь с quest events |
| `Game.Characters.Tests.Editor` | Тесты открытия персонажей, memories, связки с квестами |

`Game.Quest` уже готов к этой интеграции: в `QuestConfig` и `IQuest` есть nullable `CharacterId`. Это правильная точка связи: квест знает, какому персонажу принадлежит, но персонаж не владеет состоянием квеста.

---

## 3. Asmdef-зависимости

### `Game.Characters.API`

Минимально:

```json
{
  "name": "Game.Characters.API",
  "rootNamespace": "Game.Characters.API",
  "references": [],
  "autoReferenced": false
}
```

Если API сразу будет использовать async-команды, добавить `UniTask`, но на старте можно обойтись синхронным read-side контрактом.

### `Game.Characters`

Рекомендуемые references:

```json
{
  "name": "Game.Characters",
  "rootNamespace": "Game.Characters",
  "references": [
    "Configs",
    "Save",
    "Game.Characters.API",
    "Game.Quest.API",
    "Game.Conditions.API",
    "Game.LocationEntry.API",
    "Game.LocationUnlock.API",
    "Game.Inventory.API",
    "Game.Decor.API",
    "Game.SalesStats.API",
    "DayCycle",
    "VContainer",
    "VContainer.Unity"
  ],
  "autoReferenced": true
}
```

Важная граница: `Game.Characters` ссылается на `Game.Quest.API`, но не на `Game.Quest`. Runtime-реализацию `IQuestsService` дает DI в `BootstrapInstaller`.

### `Game.Characters.Tests.Editor`

```json
{
  "name": "Game.Characters.Tests.Editor",
  "rootNamespace": "Game.Characters.Tests.Editor",
  "references": [
    "Game.Characters",
    "Game.Characters.API",
    "Game.Quest.API"
  ],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false
}
```

---

## 4. Граница ответственности

`Game.Quest` владеет:

- состояниями `Pending`, `Active`, `ReadyToAward`, `Awarded`, `Failed`;
- задачами и прогрессом задач;
- parsing/evaluation condition trees через `Game.Conditions`;
- переходами `NextQuestIds`;
- наградами и future world effects;
- событиями `QuestStarted`, `QuestCompleted`, `QuestAwarded`, `QuestFailed`, `TaskCompleted`, `TaskProgressChanged`.

`Game.Characters` владеет:

- профилем персонажа;
- состоянием discovered/undiscovered;
- списком memories персонажа;
- связкой `characterId -> quest chains`;
- relationship metadata;
- правилами появления NPC;
- read model для Journal Characters tab;
- временными character modifiers, если NPC присутствует в текущей локации/дне.

`Game.Characters` не должен:

- сам считать прогресс задач;
- сам парсить condition DSL;
- сам выдавать quest rewards;
- хранить копию quest state;
- напрямую дергать реализации других фич вместо API.

---

## 5. Данные

Нужен новый config-файл `characters.json` в `Game.Configs`.

Черновая модель:

```csharp
[ConfigFile("characters")]
public sealed class CharacterConfig : IConfig
{
    public string Id { get; set; }
    public string DisplayNameKey { get; set; }
    public string RoleKey { get; set; }
    public string DescriptionKey { get; set; }

    public string DiscoveryConditionId { get; set; } // optional shortcut, либо JObject
    public JObject DiscoveryConditions { get; set; }

    public string[] DefaultLocationIds { get; set; }
    public string[] FavoriteGenreIds { get; set; }
    public CharacterMemoryConfig[] Memories { get; set; }
    public CharacterRelationConfig[] Relations { get; set; }
    public CharacterScheduleRuleConfig[] ScheduleRules { get; set; }
    public CharacterModifierConfig[] Modifiers { get; set; }
}
```

Memory лучше хранить как привязку к квесту или цепочке:

```csharp
public sealed class CharacterMemoryConfig
{
    public string Id { get; set; }
    public string TitleKey { get; set; }
    public string DescriptionKey { get; set; }
    public string QuestId { get; set; }
    public string QuestChainId { get; set; }
    public bool IsGolden { get; set; }
}
```

Save-состояние персонажей:

```csharp
public sealed class SavedCharacters
{
    public Dictionary<string, SavedCharacter> Characters { get; set; }
}

public sealed class SavedCharacter
{
    public bool Discovered { get; set; }
    public HashSet<string> UnlockedMemoryIds { get; set; }
    public string LastKnownLocationId { get; set; }
}
```

Ключ save-модуля: `characters.v1`.

---

## 6. API

Публичный контракт:

```csharp
public interface ICharactersService
{
    ICharacter TryGetCharacter(string characterId);
    IEnumerable<ICharacter> GetDiscoveredCharacters();
    IEnumerable<ICharacter> GetAllCharacters();
    CharacterJournalEntry GetJournalEntry(string characterId);

    bool IsDiscovered(string characterId);
    bool IsMemoryUnlocked(string characterId, string memoryId);

    event Action<ICharacter> CharacterDiscovered;
    event Action<ICharacterMemory> MemoryUnlocked;
}
```

Read models:

```csharp
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

UI не должен читать `QuestConfig` напрямую для Journal Characters. Он должен получать готовую read model из `ICharactersService`, где уже сопоставлены персонаж, memory и текущее состояние связанного квеста.

---

## 7. Интеграция с Game.Quest

Связь строится в три слоя:

1. `QuestConfig.CharacterId` связывает квест с персонажем.
2. `CharacterMemoryConfig.QuestId` или `QuestChainId` говорит, какая memory открывается.
3. `CharactersService` подписывается на `IQuestsService.QuestAwarded` и открывает memory, если awarded quest соответствует memory config.

Пример:

```json
{
  "id": "harper",
  "displayNameKey": "character.harper.name",
  "memories": [
    {
      "id": "restore_the_crest",
      "questChainId": "harper_restore_crest",
      "isGolden": false
    },
    {
      "id": "one_for_the_history_books",
      "questId": "harper_one_for_history_books",
      "isGolden": true
    }
  ]
}
```

Поведение:

- Если awarded конкретный `QuestId`, открыть memory с этим `QuestId`.
- Если awarded финальный квест цепочки, открыть memory с этим `QuestChainId`.
- Если персонаж еще не discovered, discovery можно выполнить автоматически при первом started/awarded quest с его `CharacterId`, либо через отдельные `DiscoveryConditions`.

Рекомендация: для MVP открыть персонажа при первом `QuestStarted` или `QuestAwarded` с `CharacterId`. Отдельные discovery conditions добавить позже, когда появятся явные NPC-spawn/диалоговые события.

---

## 8. Journal Characters

Tiny Bookshop использует Journal как основной mission log. Для MyBookstore лучше разделить данные и UI:

`Game.Characters` дает read model:

```csharp
public sealed class CharacterJournalEntry
{
    public string CharacterId { get; set; }
    public bool Discovered { get; set; }
    public string DisplayNameKey { get; set; }
    public string RoleKey { get; set; }
    public CharacterJournalMemory[] Memories { get; set; }
}

public sealed class CharacterJournalMemory
{
    public string MemoryId { get; set; }
    public bool Unlocked { get; set; }
    public bool IsGolden { get; set; }
    public string TitleKey { get; set; }
    public string DescriptionKey { get; set; }
    public string LinkedQuestId { get; set; }
    public QuestState LinkedQuestState { get; set; }
}
```

UI-решение:

- Journal Characters tab показывает только discovered characters.
- Undiscovered можно скрывать или показывать как silhouettes позже.
- Memory открыта, если связанный quest/chain awarded.
- Golden memory - обычная memory с флагом `IsGolden`; отдельная логика наград не нужна.

---

## 9. Персонажи и memories из референса

Полный копипаст challenge-таблиц не нужен и опасен для поддержки. Для архитектуры достаточно выделить типы требований:

| Персонаж | Архетип цепочки | Нужные системы |
|---|---|---|
| Tilde | mentor/endgame heir chain, доставка вещей, hospital visit, несколько golden memories друзей | Quest, Inventory, Shop, Journal |
| Anne | растения, погода, coastal/inner-city locations, graduation | Quest, Decor/Items, DayCycle weather, Locations |
| Fern | manuscripts, printing press, newspaper, clubs | Quest, Newspaper, Inventory, Location events |
| Walt | waterfront vendor, покупки предметов, продажи на Waterfront, tourist/world state | Quest, Shop, SalesStats, Location state |
| Klaus | band, poster/fans, concert, van repair | Quest, Decor/Items, SalesStats, Events |
| Maryam | cafe business course, support event, family memory with Moira | Quest, Location, Relationships |
| Moira | spooky setup, winter/festival hooks, B.L.A.B.L.A. rule, shared Maryam memory | Quest, Decor tags, DayCycle/events, Relationships |
| Harper | seashells, sandcastle, cave, crest fragments, golden history memory | Quest, Collection, Locations, Inventory |

Из этого видно, что `Characters` - это не отдельная мини-игра. Это индекс над story progression, где сами действия остаются в quest tasks.

---

## 10. Character presence и modifiers

На старте лучше не смешивать memories и shop modifiers. Но future API стоит заложить так, чтобы продажи могли читать активные эффекты персонажей.

```csharp
public interface ICharacterPresenceService
{
    IEnumerable<CharacterPresence> GetPresentCharacters(string locationId, int day);
}

public interface ICharacterModifierProvider
{
    IEnumerable<CharacterModifier> GetActiveModifiers(string locationId, int day);
}
```

Примеры модификаторов:

- `saleChance.genre.classic +3%`;
- `saleChance.any +2%`;
- `moneyPerSale +1`;
- `decorTagEffect.spooky +50%`.

Рекомендация: для MVP не подключать modifiers к `Book.Sell`. Сначала сделать чистый Journal/memory слой. Modifiers добавить после того, как станет понятно, где именно в sales pipeline должен объединяться эффект декора, локации и персонажа.

---

## 11. Регистрация в DI

Добавить файл:

```text
Assets/Game/Core/Installers/Features/CharactersVContainerBindings.cs
```

Черновой вид:

```csharp
using Game.Characters.API;
using Game.Characters.Services;
using Game.Characters.Services.Persistence;
using VContainer;

namespace Game.Bootstrap
{
    public static class CharactersVContainerBindings
    {
        public static void RegisterCharacters(this IContainerBuilder builder)
        {
            builder.Register<ICharactersRepository, SaveBackedCharactersRepository>(Lifetime.Singleton);
            builder.Register<CharactersService>(Lifetime.Singleton).As<ICharactersService>();
        }
    }
}
```

Регистрировать в `BootstrapInstaller`, рядом с `RegisterQuest()`, потому что:

- состояние персонажей должно жить глобально;
- Journal может открываться в разных сценах;
- сервису нужно подписаться на `IQuestsService` и восстановиться после загрузки save.

Порядок: `RegisterQuest()` до `RegisterCharacters()`, чтобы `IQuestsService` был доступен.

---

## 12. Этапы реализации

### Этап 0 - сейчас

- Собрать требования из wiki.
- Зафиксировать архитектурную границу.
- Создать этот MD.
- Не создавать кодовый asmdef, пока не согласованы вопросы ниже.

### Этап 1 - минимальный модуль

- Создать `Game.Characters.API`, `Game.Characters`, `Game.Characters.Tests.Editor`.
- Добавить `CharacterConfig`, `CharacterMemoryConfig`.
- Добавить `characters.json` с 1-2 тестовыми персонажами.
- Реализовать `ICharactersService` read-side.
- Реализовать save `characters.v1`.

### Этап 2 - интеграция с Quest

- Подписаться на `QuestStarted` для discovery.
- Подписаться на `QuestAwarded` для memories.
- Поддержать memory by `QuestId` и by `QuestChainId`.
- Покрыть тестами idempotency: повторный event не дублирует memory.

### Этап 3 - Journal

- Добавить read model для Journal Characters.
- Подключить UI позже, не блокируя доменную часть.
- Решить, показываем ли undiscovered персонажей.

### Этап 4 - presence/modifiers

- Добавить schedule/presence rules.
- Добавить active modifiers provider.
- Интегрировать с sales pipeline только через API.

---

## 13. Открытые вопросы и решения на сейчас

1. Нужен ли `CharacterId` на уровне `QuestChain`, если он уже есть на каждом `QuestConfig`?
   - Решение на сейчас: нет. Используем `QuestConfig.CharacterId`; chain owner вычисляется как общий `CharacterId` у членов цепочки. Если цепочка shared, оставляем `CharacterId = null` на chain-level и используем memories у нескольких персонажей.

2. Где хранить relationship-состояние вроде Maryam + Moira?
   - Решение на сейчас: metadata в `CharacterRelationConfig`, progression через shared quest/memory. Отдельный relationship XP не вводим.

3. Memory открывается на `ReadyToAward` или `Awarded`?
   - Решение на сейчас: на `Awarded`. `ReadyToAward` еще может ждать финального диалога/награды.

4. Что делать с golden memory?
   - Решение на сейчас: это `CharacterMemoryConfig.IsGolden = true`, а не отдельный тип сущности.

5. Нужны ли персонажи до Journal UI?
   - Решение на сейчас: да, но только как data/read-side сервис. UI можно подключить позже.

6. Нужно ли создавать asmdef модуль прямо сейчас?
   - Решение на сейчас: нет. Этот этап - сбор информации и спецификация. Кодовый модуль создаем следующим шагом после согласования границ.

7. Как открывать персонажа без диалоговой системы?
   - Решение на сейчас: discovery по первому `QuestStarted`/`QuestAwarded` с `CharacterId`. Когда появятся NPC/dialogue events, добавим отдельные discovery conditions.

8. Где жить character modifiers?
   - Решение на сейчас: API заложить в `Game.Characters`, но не подключать к продажам в первом MVP.

---

## 14. Definition of Done для следующего шага

- Есть отдельные asmdef: `Game.Characters.API`, `Game.Characters`, `Game.Characters.Tests.Editor`.
- `BootstrapInstaller` регистрирует `RegisterCharacters()` после `RegisterQuest()`.
- `characters.json` грузится через `Game.Configs`.
- `ICharactersService` возвращает discovered characters и journal entries.
- Awarded quest открывает соответствующую memory.
- Состояние discovered/memories переживает save/load.
- Есть EditMode-тесты на discovery, memory unlock, save restore, unknown quest safety.
