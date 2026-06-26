# Location Unlock System — спека фичи

> Статус: INPROGRESS. Референс-паттерн (приоритеты доступности, реактивность) взят из
> [archive/REF_FEATURES_SERVICE.md](../archive/REF_FEATURES_SERVICE.md) — это сервис из **другой**
> кодовой базы, в MyBookstore его нет. Берём идею, а не код.
>
> **Реализовано (вертикальный срез):**
> - Слой 1 — `SalesStats` (persistent счётчики продаж по жанрам, батч-запись). `Assets/Game/Features/SalesStats/`.
> - Слой 2 — `Conditions` engine (`ICondition`, AllOf/AnyOf/Not, parser/registry, fail-closed) + лист `soldGenre`. `Assets/Game/Features/Conditions/`.
> - Слой 3 — `LocationUnlock` (`Locked/Unlockable/Unlocked`, `TryUnlockAsync` со списанием `UnlockCost`, реактивность по `Changed`) + фильтрация `DayConfig.TargetLocationIds` в `MorningContextResolver`. `Assets/Game/Features/LocationUnlock/`.
>
> Тесты написаны (edit-mode), но не прогонялись из CLI — требуется Unity Editor. Открытые провайдеры (`playerLevel`, per-character репутация) — см. раздел 12.

## 1. Задача

Открытие локаций по произвольному набору условий, который должен расширяться с появлением новых
фич **без переделки движка**. Примеры условий:

1. Локация X: уровень ≥ 5 **и** продано 30 книг жанра Crime **и** продано 5 книг жанра Kids.
2. Локация Y: репутация с персонажем Z ≥ K.
3. Позже — «поймать пару рыб» и любые другие.

## 2. Архитектурное решение

Система **двухслойная** — это ответ на вопрос «должно ли это быть инфраструктурой»: да, слой
условий — инфраструктурный фундамент, анлок локаций — доменный потребитель поверх него.

```
┌─────────────────────────────────────────────┐
│ Домен: LocationUnlock                         │  знает про локации, цену, факт покупки, UI
│   ILocationUnlockService                      │
├─────────────────────────────────────────────┤
│ Инфра: Conditions engine                      │  ничего не знает про локации
│   ICondition / AllOf·AnyOf·Not / Factory      │
├─────────────────────────────────────────────┤
│ Источники данных (read-only seams)            │  каждый — отдельная фича со своим save
│   ISalesStatsReader · IPlayerLevelProvider    │
│   IReputationProvider · ...                    │
└─────────────────────────────────────────────┘
```

Жёсткое правило: **условие читает данные только через свой read-only provider, никогда из event
bus**. Event bus / `Changed`-события — это сигнал «пересчитай», а не источник истины. Иначе
расширяемость превращается в кашу из подписок.

## 3. Состояния локации (Unlockable ≠ Unlocked)

Три разных состояния — разводим намеренно, т.к. у локации есть `UnlockCost`
([LocationConfig.cs:11](../../Assets/Game/Features/Configs/Models/LocationConfig.cs)):

| Состояние | Значение |
|---|---|
| `Locked` | условия ещё не выполнены |
| `Unlockable` | условия выполнены, но игрок ещё не заплатил `UnlockCost` |
| `Unlocked` | локация куплена/открыта — **отдельный persistent save-state** |

«Условия выполнены» (вычисляется движком на лету) **не равно** «локация открыта» (факт покупки,
который сохраняется). Анлок — это явный `TryUnlockAsync` со списанием `UnlockCost`.

## 4. Формат условий в конфиге (data-driven)

Расширяем `locations.json` блоком `unlock`. Композиты — ключи `all` / `any` / `not`, лист — объект
с дискриминатором `type`:

```json
{
  "id": "university",
  "displayName": "University",
  "unlockCost": 500,
  "unlock": {
    "all": [
      { "type": "playerLevel", "min": 5 },
      { "type": "soldGenre", "genre": "Crime", "min": 30 },
      { "type": "soldGenre", "genre": "Kids",  "min": 5 }
    ]
  }
}
```

Конфиги уже грузятся через Newtonsoft с RC-override merge
([ConfigsService.cs:128](../../Assets/Game/Features/Configs/ConfigsService.cs)) — структуру условий
можно будет переопределять с сервера бесплатно.

### Переходный формат `JObject` — с дисциплиной

В `LocationConfig` поле хранится как сырой `JObject`, чтобы фича `Configs` не знала про доменные
типы условий:

```csharp
public Newtonsoft.Json.Linq.JObject Unlock { get; set; }
```

**Но `JObject` не должен гулять по домену.** Сразу за загрузкой стоит обязательный слой
`parser → registry → ICondition`. `JObject` живёт только до парсера; дальше домен и UI работают
исключительно с типизированным деревом `ICondition`. Иначе UI, валидация и тесты станут хрупкими.

### `RequiredLevel` — legacy shortcut, не убиваем сразу

`RequiredLevel` ([LocationConfig.cs:16](../../Assets/Game/Features/Configs/Models/LocationConfig.cs))
остаётся как legacy-ярлык. Политика во избежание «двух истин»:

- если задан `unlock` — `RequiredLevel` **игнорируется** (запрещаем смешивать; парсер логирует
  warning при одновременном задании);
- если `unlock` отсутствует, а `RequiredLevel > 0` — парсер маппит его в эквивалент
  `{ "type": "playerLevel", "min": RequiredLevel }`.

Цель — одна истина об условиях на каждую локацию.

## 5. Инфра-слой — Conditions engine

Фича: `Assets/Game/Features/Conditions/`.

```csharp
// API/ICondition.cs — sync-read, как геттер Reputation
public interface ICondition
{
    ConditionResult Evaluate();
}

// API/ConditionResult.cs — не только IsMet, но и progress payload для UI
public readonly struct ConditionResult
{
    public bool IsMet { get; }
    public long Current { get; }                       // прогресс: числитель (23)
    public long Target  { get; }                       // прогресс: знаменатель (30)
    public string ReasonKey { get; }                   // "soldGenre.Crime" — ключ локализации
    public IReadOnlyList<ConditionResult> Children { get; } // для композитов → дерево в UI
}
```

Composites (`AllOf` / `AnyOf` / `Not`) тоже реализуют `ICondition` и агрегируют детей + прокидывают
`Children`, чтобы экран карты показал «Crime 23/30, Kids 4/5».

Лист зависит только от своего provider:

```csharp
public sealed class SoldGenreAtLeast : ICondition
{
    private readonly ISalesStatsReader _sales;
    private readonly BookGenre _genre;
    private readonly int _min;
    public ConditionResult Evaluate() =>
        ConditionResult.Progress(_sales.GetSold(_genre), _min, $"soldGenre.{_genre}");
}
```

Расширяемость через фабрики по дискриминатору:

```csharp
public interface IConditionFactory
{
    string Type { get; }              // "soldGenre", позже "fishCaught"
    ICondition Create(JObject node);
}
// Registry: type → factory. Parser сам обходит all/any/not, листья отдаёт реестру.
```

Новая фича (рыбалка) → новый `FishCaughtAtLeast` + `FishCaughtConditionFactory` + `IFishingStatsReader`,
регистрируем в DI. **Движок и LocationUnlockService не трогаются.**

## 6. Read-only провайдеры (seams)

Закладываем интерфейсы сразу, чтобы новые условия не требовали переделки движка:

| Provider | Статус источника |
|---|---|
| `ISalesStatsReader` | **новый** (раздел 8) — первый срез |
| `IPlayerLevelProvider` | seam есть, **источника уровня в проекте нет** → временный стаб + TODO |
| `IReputationProvider` | оборачивает существующий `IProgressionService.Reputation` |
| `ICharacterRelationshipReader` | позже (per-character репутации сейчас нет) |
| `IFishingStatsReader` | позже |

## 7. Домен-слой — LocationUnlockService

Фича: `Assets/Game/Features/LocationUnlock/`.

```csharp
public interface ILocationUnlockService
{
    bool IsUnlocked(string locationId);
    LocationUnlockStatus GetStatus(string locationId);              // Locked / Unlockable / Unlocked
    IReadOnlyList<RequirementProgress> GetRequirements(string id);  // для UI «23/30 Crime»
    UniTask<bool> TryUnlockAsync(string locationId, CancellationToken ct); // условия + списание UnlockCost
    event Action<string> Unlocked;
}
```

- Persistence факта анлока: своя пара `ISaveHook` + `ILocationUnlockRepository` (множество купленных
  id), копия паттерна Progression
  ([ProgressionService.cs:36](../../Assets/Game/Features/Progression/Services/ProgressionService.cs)).
- `TryUnlockAsync`: проверяет `Unlockable` → списывает `UnlockCost` через `IResourcesService` →
  сохраняет id → поднимает `Unlocked`.
- **Реактивность** (проще, чем per-condition observers из референса): сервис подписан на `Changed`
  релевантных провайдеров; при сигнале пересчитывает ещё не открытые локации. Локаций мало —
  полный пересчёт дешевле.

## 8. Источник данных №1 — SalesStats

Фича: `Assets/Game/Features/SalesStats/`.

```csharp
public interface ISalesStatsReader        // для условий (инфра видит только это)
{
    int GetSold(BookGenre genre);
    int TotalSold { get; }
}

public interface ISalesStatsService : ISalesStatsReader  // полный контракт
{
    event Action<SalesStatsChange> Changed;
}
```

### Точка врезки и политика записи

Единственная точка фиксации продажи — `SoldBookCommitter.CommitSoldBook(bookId, source)`
([SoldBookCommitter.cs:31](../../Assets/Game/Features/BookSell/Services/SoldBookCommitter.cs)). Жанр
резолвится из `BookConfig` по `bookId`; нормализация ключей через готовый
[BookGenreCounts.Normalize](../../Assets/Game/Features/Configs/Models/BookGenreCounts.cs).

**Политика записи — буфер в памяти + batched flush, не save на каждую книгу.** Продаж за день много;
save-per-book даст лишний I/O-шум. Счётчики копятся в памяти и флашатся одним сейвом в конце дня
(переиспользуем существующий `FlushAsync` у коммиттера / конец дня), либо явным batched save.
Persistence — `ISaveHook` + репозиторий с `Dictionary<genre,int>`.

## 9. Интеграция в core loop — фильтрация TargetLocationIds

`DayConfig.TargetLocationIds` ([DayConfig.cs:29](../../Assets/Game/Features/Configs/Models/DayConfig.cs))
сейчас считаются доступными по умолчанию. Их **обязательно пропускать через `ILocationUnlockService`**:
утро не должно рекламировать закрытую локацию. Потребители утреннего контекста фильтруют
`TargetLocationIds` по `IsUnlocked` (или показывают как «locked» с прогрессом — на усмотрение UI).

## 10. Первый вертикальный срез

```
SalesStats (reader + write-врезка + batched save)
  + soldGenre condition (+ factory, registry, AllOf)
  + LocationUnlockService (status / TryUnlock / save факта анлока)
  + фильтрация DayConfig.TargetLocationIds по unlocked
```

Этот срез даёт не только проверку условий, но и реальный эффект в core loop. `playerLevel` /
per-character репутация подключаются позже отдельными провайдерами без изменения движка.

## 11. Раскладка по папкам (под конвенцию проекта)

```
Assets/Game/Features/
  Conditions/        API/      ICondition, ConditionResult, IConditionFactory
                     Services/ Parser, Registry, AllOf/AnyOf/Not, листья
                     Tests/Editor/
  LocationUnlock/    API/      ILocationUnlockService, LocationUnlockStatus,
                               RequirementProgress, ILocationUnlockRepository
                     Services/ LocationUnlockService, репозиторий
                     Tests/Editor/
  SalesStats/        API/      ISalesStatsReader, ISalesStatsService, SalesStatsChange
                     Services/ SalesStatsService, репозиторий
                     Tests/Editor/
```

## 12. Открытые вопросы

- Источник уровня игрока (`IPlayerLevelProvider`) — нужна отдельная фича уровня/прогрессии.
- Per-character репутация (`ICharacterRelationshipReader`) — текущая репутация глобальная.
- Точный момент batched-flush продаж (конец дня vs. явный save) — согласовать с day cycle.
</content>
</invoke>
