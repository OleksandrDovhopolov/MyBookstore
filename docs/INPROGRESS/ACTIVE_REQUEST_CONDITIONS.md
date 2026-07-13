# Active-purchase requests — condition-based model (пересмотр)

- **Статус:** 🚧 In-progress / draft (2026-07-13)
- **Тип:** спека фичи (пересмотр активной продажи)
- **Related:** [ADR-0003](../adr/0003-customer-simulation.md), [ADR-0006](../adr/0006-passive-sales-requested-genre.md),
  [CORE_LOOP.md](../CORE_LOOP.md)

> ## ⚠️ ADR — не забыть обновить
> Активная продажа сейчас зафиксирована в [ADR-0003](../adr/0003-customer-simulation.md) (миниигра подбора
> поверх взвешенного скоринга). Этот документ описывает переход на **булеву модель условий**.
> **Когда модель будет принята — оформить новый `ADR-0009` «Active requests over a condition tree» и пометить
> активную часть ADR-0003 как частично superseded.** Пассивную часть ([ADR-0006](../adr/0006-passive-sales-requested-genre.md))
> этот пересмотр НЕ трогает. Пока ADR не написан — источник истины по решению здесь.

---

## 1. Зачем меняем

Текущая legacy-активная продажа: `RequestConfig` (requests.json) — плоский набор мягких предпочтений
(`DesiredGenres/DesiredQualities/MaxPrice`), а `RecommendationScoringService` считает **взвешенный
балл** (жанр +3, quality +2, цена +1, локация +1) и раздаёт тир Excellent/Normal/Failed. Игрок сам
выбирает книгу, система оценивает «насколько удачно».

Ограничения этой модели:
- нет операторов сравнения — невыразимы `publicationYear между 1850..1900`, `pages ≤ 200`, `NOT genre X`;
- `Published`/`Pages` у книги есть, но в скоринге **не участвуют**;
- нет булевой логики (AND/OR/NONE), нет исключений.

Новая модель: запрос — **булев предикат над книгой** (подходит / не подходит). Data-driven, расширяется
регистрацией хендлеров без изменения схемы (open/closed). Соответствует data-driven принципам
[ADR-0002](../adr/0002-config-system-architecture.md).

## 2. Модель данных

Файл `sample_requests.json` — **JSON-массив** (как все конфиги; загрузчик делает `JArray.Parse`).
C#-модель — [`RequestDefinitionConfig`](../../Assets/Game/Features/Configs/Models/RequestDefinitionConfig.cs)
(`[ConfigFile("sample_requests")]`), группы —
[`RequestConditionGroup`](../../Assets/Game/Features/Configs/Models/RequestConditionGroup.cs), лист —
[`RequestCondition`](../../Assets/Game/Features/Configs/Models/RequestCondition.cs).

```json
{
  "id": "req_scarlet_01",
  "genre": "Crime",
  "bookTitle": "A Study in Scarlet",
  "enabled": true,
  "conditions": {
    "all": [
      { "type": "genres", "operator": "contains", "value": "Crime" },
      { "type": "publicationYear", "operator": "between", "value": { "min": 1850, "max": 1900 } },
      { "type": "pages", "operator": "lessOrEqual", "value": 200 }
    ]
  }
}
```

- `genre` / `bookTitle` — **метаданные авторинга** (вокруг какой книги придумывался запрос). В матчинге не участвуют.
- `conditions` — плоские группы `all` / `any` / `none`.

### Семантика групп

Книга матчит запрос, когда: `(all pass) AND (any passes) AND (none pass)`.
Пустая/отсутствующая группа нейтральна (== true), поэтому запрос указывает только нужные группы.

| Группа | Смысл | Пусто |
|---|---|---|
| `all` | все условия проходят (AND) | true |
| `any` | хотя бы одно (OR) | true |
| `none` | ни одного (NOR / исключение) | true |

### Условие (лист)

`{ "type": ..., "operator": ..., "value": ... }`. `type` и `operator` — **строки** (не enum), чтобы новый
тип/оператор добавлялся хендлером без правки модели.

**Операторы (общий фиксированный набор):**

| Категория | Операторы | Форма `value` |
|---|---|---|
| Равенство | `equal`, `notEqual` | scalar (string / number) |
| Числовые | `greater`, `greaterOrEqual`, `less`, `lessOrEqual` | number |
| Диапазон | `between` | `{ "min": n, "max": n }` (включительно) |
| Списки | `contains`, `notContains` | scalar string |
| Списки (мн.) | `containsAny`, `containsAll`, `containsNone` | array of strings |

**Типы условий (по реально заполненным полям книги):** `genres`, `qualities`, `publicationYear`, `pages`.

> ⚠️ Поля `authorSex/size/price/rarity/country/language` из ранних набросков **в контенте не заполнены** —
> под них нет колонок в исходной таблице книг, и в первой версии они **не используются**. Добавлять типы
> под них — только когда появится контент.

### Словарь `qualities` из `Unique Qualities`

Источник: `Tiny_Bookshop_Books.xlsx`, вкладка `Unique Qualities`.

Нормализация:
- дефис и пробел считаются одним разделителем: `Non-Fiction` == `Non Fiction`;
- опечаточные варианты `Bigraphy` / `Biobraphy` не используются — оставляем только `Biography`;
- варианты `Humour` / `Humor` сведены к `Humor`.

Разрешённые значения:

- `Academic`
- `Age Rating Mature`
- `Age Rating Mature [customers do not accept this as fantasy]`
- `Animals`
- `Biography`
- `Coming of Age`
- `Contemporary`
- `Cooking`
- `Crime`
- `Detective`
- `Dry`
- `Dystopia`
- `Encyclopedic`
- `Epic`
- `Fact`
- `Female Author`
- `Fiction`
- `Folklore`
- `Gore`
- `Graphic Novel`
- `Happy Ending`
- `Historic`
- `Hobby`
- `Horror`
- `Humor`
- `Kids`
- `Light Reading`
- `Long`
- `Magic`
- `Manga`
- `Mature Rating`
- `Mature Reading`
- `Mystery`
- `Nature`
- `Niche`
- `Non Fiction`
- `Novel`
- `Outdated`
- `Philosophical`
- `Philosophical Contemporary`
- `Play`
- `Plot Twist`
- `Poetry`
- `Political`
- `Pop Science`
- `Queer`
- `Romance`
- `Science Fiction`
- `Self Help`
- `Series`
- `Short`
- `Space`
- `Thriller`
- `Tragic`
- `Travel Guide`
- `Very Long`
- `Whodunnit`
- `YA`

### Полиморфный `value` → `JToken`

Конфиг-слой на Newtonsoft (`ConfigsService` → `obj.ToObject<T>`), поэтому `value` хранится как
`Newtonsoft.Json.Linq.JToken` (это «Вариант 3» из обсуждения). Не «universal fields» и не строка
`"1850|1900"` — типобезопасность парсинга остаётся на хендлере, который знает форму своего оператора.

## 3. Сосуществование со старой системой (НЕ удалять)

Старый расчёт **сохраняется целиком**:
- `RequestConfig` + `requests.json` — остаются;
- [`RecommendationScoringService`](../../Assets/Game/Features/BookSell/Services/RecommendationScoringService.cs)
  и `IRecommendationScoringService` — **не трогаем и не удаляем**;
- новый `RequestDefinitionConfig` + `sample_requests.json` — отдельный конфиг-тип, грузится параллельно.

Переключение моделей — явным seam на уровне выбора/спавна запросов (по образцу
[ADR-0006](../adr/0006-passive-sales-requested-genre.md): две стратегии за одним интерфейсом, старая — legacy,
откат одной строкой). Конкретный seam проектируется в следующей итерации (см. Open questions).

## 4. Escape-hatch для невыразимой логики

Плоские `all/any/none` = ровно один уровень (`any` — OR только над одиночными листьями). Формулы вида
`(A AND B) OR (C AND D)` или вложенность глубже одного уровня — **невыразимы**. По договорённости:

- рекурсивное дерево в общую схему/таблицу **не тащим**;
- для редкого такого запроса — **escape-hatch**: отдельное поле с сырым JSON условия на конкретный запрос
  (остальные остаются плоскими). Вводится только когда реально появится невыразимый запрос.

## 5. Конфиг-плюмбинг

- Editor/дев: `LocalFolderConfigSource` читает все `Assets/Configs/*.json` из папки — регистрация не нужна.
- Плеер-сборка: `StreamingAssetsConfigSource` грузит по `manifest.json` — `sample_requests.json` **добавлен в
  манифест**. Перед релизным билдом гонять `Tools/Configs/Sync Bundled Defaults to StreamingAssets`
  (копирует `Assets/Configs/*.json` и регенерит манифест).
- `.meta` для новых `.json` Unity сгенерит при импорте.

## 6. Зависимость: книги → список жанров

`BookConfig.Genres` — **массив жанров**. Для старых систем, которым пока нужен один жанр, используется
`BookConfig.PrimaryGenre` = первый элемент `Genres`. Условия используют `genres` + `contains`/`containsAll`
по полному списку жанров книги.

## 7. Accepted implementation decisions (2026-07-13)

- Runtime seam: active purchase flow uses `ActiveRequestRuntime`; legacy `RequestConfig` is wrapped by an adapter and remains available for rollback.
- Feature flag: `SalesTuning.ActiveRequestMode` switches `LegacyScoring` vs `Conditions`; production default is `Conditions`.
- Conditions scoring: matching book => `Excellent` and `10` gold; non-matching book => `Failed` and `0` gold; skip => `Skipped` and `0` gold. `Normal` is legacy-only.
- Request text v1: generated programmer-readable text from the condition tree; `Difficulty = Unknown`.
- Condition semantics: `genres` checks only `BookConfig.Genres`; `qualities` checks only `BookConfig.Qualities`; sample content must target the field where the value actually lives.
- Spawn semantics: in condition mode, regular customer spawning assigns the first `N` enabled valid condition requests to `Passive -> Active -> Passive` customers; the rest remain passive-only. Hard override days keep the exact customer count and warn if capacity is below request count.
- Validation: invalid condition requests are filtered before spawning; direct evaluator calls fail closed and log an error.
- Scope: active purchase predicates stay in BookSell and do not reuse the global `Game.Conditions` quest/location engine.

## 8. Historical TODO / follow-up

The original open questions below are kept as historical context. Gameplay, reward, seam, evaluator, and validator choices are superseded by the accepted implementation decisions above; ADR-0009 is still a follow-up documentation task.

- [ ] **Геймплей.** Условия дают «множество подходящих книг» (фильтр), но не «насколько хорошо игрок угадал»
      (градация скоринга). Определить, как миниигра использует match-set: строгий pass/fail? частичный балл
      по числу пройденных условий? гибрид (условия — кандидаты, скоринг — оценка выбора)?
- [ ] **Награда.** В `sample_requests.json` нет reward/difficulty (в отличие от `RequestConfig`). Решить, где
      живёт награда в новой модели.
- [ ] **Seam выбора модели** (legacy scoring ↔ conditions) — спроектировать по образцу ADR-0006.
- [ ] **Evaluator + реестр хендлеров** (`IConditionHandler` по `type`, общий набор операторов) — реализация.
- [ ] **Валидатор** конфигов условий (реестр типов/операторов, форма `value`) — иначе опечатки всплывают в рантайме.
- [ ] Проверить, не переиспользовать ли существующий движок `Game.Conditions` ([ADR-0007](../adr/0007-quest-system.md))
      вместо новой сущности.
- [ ] Оформить **ADR-0009** и пометить активную часть ADR-0003 как superseded (см. callout сверху).
- [x] Миграция `BookConfig` на `Genres[]` (см. §6).
