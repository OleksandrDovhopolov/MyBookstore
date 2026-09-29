# Active-purchase requests — condition-based model (пересмотр)

- **Статус:** ✅ Accepted / implemented (2026-07-14)
- **Тип:** спека фичи (пересмотр активной продажи)
- **Related:** [ADR-0003](../adr/0003-customer-simulation.md), [ADR-0006](../adr/0006-passive-sales-requested-genre.md),
  [CORE_LOOP.md](../CORE_LOOP.md)

> ## Follow-up
> Условная модель **внедрена**, а legacy-скоринг (`RequestConfig` + `RecommendationScoringService`) **удалён** —
> активная продажа теперь работает только на булевых условиях. Оставшиеся не-MVP пункты вынесены в
> [TODO.md → GAME-14](../TODO.md#-backlog).

---

## 1. Зачем меняли

Прежняя активная продажа была взвешенным скорингом (`RequestConfig` + `RecommendationScoringService`):
плоский набор мягких предпочтений (`DesiredGenres/DesiredQualities/MaxPrice`), балл (жанр +3, quality +2,
цена +1, локация +1) и тир Excellent/Normal/Failed. Её ограничения:
- нет операторов сравнения — невыразимы `publicationYear между 1850..1900`, `pages ≤ 200`, `NOT genre X`;
- `Published`/`Pages` у книги есть, но в скоринге **не участвовали**;
- нет булевой логики (AND/OR/NONE), нет исключений.

Эта модель **удалена**. Активный запрос теперь — **булев предикат над книгой** (подходит / не подходит).
Data-driven, расширяется регистрацией хендлеров без изменения схемы (open/closed). Соответствует
data-driven принципам [ADR-0002](../adr/0002-config-system-architecture.md).

## 2. Модель данных

Файл `sample_requests.json` — **JSON-массив** (как все конфиги; загрузчик делает `JArray.Parse`).
C#-модель — [`RequestDefinitionConfig`](../../Assets/Game/Features/Configs/Models/RequestDefinitionConfig.cs)
(`[ConfigFile("sample_requests")]`), группы —
[`RequestConditionGroup`](../../Assets/Game/Features/Configs/Models/RequestConditionGroup.cs), лист —
[`RequestCondition`](../../Assets/Game/Features/Configs/Models/RequestCondition.cs).

```json
{
  "id": "req_scarlet_01",
  "description": "Хочу почитать, что-то из криминального.",
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

Источник: исходно вкладка `Unique Qualities` импортированной таблицы; сейчас словарь **закрыт** и
живёт в `BookQualityVocabularyTests.AllowedQualities` — C# авторитетный, этот список его зеркалит.
Добавление значения — правка в двух местах одной задачей.

**Сравнение строгое, целиком строкой.** `BookConditionRequestEvaluator.Contains` сравнивает значение
условия с тегом книги через `OrdinalIgnoreCase`, без всякой нормализации разделителей: `Non-Fiction`
и `Non Fiction` — **два разных тега**, и условие на одно не найдёт книги со вторым. Единственное
место, где дефис и пробел сворачиваются, — `LexiconActiveRequestTextComposer`, и там нормализуется
значение **условия** при поиске фразы в лексиконе, а не теги книги.

Именно поэтому словарь приведён к одному написанию (было 64 значения, стало 51):

- опечатки, встречавшиеся по одному разу, сведены к канону: `Bigraphy` / `Biobraphy` → `Biography`,
  `Female-Author` → `Female Author`, `Humor` → `Humour`, `Self-Help` → `Self Help`;
- расколотые понятия слиты: `Mature Reading` / `Mature Rating` → `Age Rating Mature`,
  `Non-Fiction` → `Non Fiction`;
- склейка `Philosophical Contemporary` разобрана на два тега;
- значения жанров (`Crime`, `Fact`, `Kids`), попавшие в поле qualities, удалены — жанр спрашивают
  через `genres`.

Значения видны игроку: `BookCardView` рендерит их в лейбл карточки как есть, без локализации.
Поэтому редакторские пометки внутри значения недопустимы — тест это запрещает.

Разрешённые значения (51):

- `Academic`
- `Age Rating Mature`
- `Animals`
- `Biography`
- `Coming of Age`
- `Contemporary`
- `Cooking`
- `Detective`
- `Dry`
- `Dystopia`
- `Encyclopedic`
- `Epic`
- `Female Author`
- `Fiction`
- `Folklore`
- `Gore`
- `Graphic Novel`
- `Happy Ending`
- `Historic`
- `Hobby`
- `Horror`
- `Humour`
- `Light Reading`
- `Long`
- `Magic`
- `Manga`
- `Mystery`
- `Nature`
- `Niche`
- `Non Fiction`
- `Novel`
- `Outdated`
- `Philosophical`
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

## 3. Legacy удалён

Старый скоринг удалён целиком: `RequestConfig`, `IRecommendationScoringService` /
`RecommendationScoringService`, конфиг `requests.json` и переключатель режимов (`ActiveRequestMode` /
`ActiveRequestSourceKind`). Активная продажа работает только на `RequestDefinitionConfig` +
`sample_requests.json`; `IActiveRequestScoringService` всегда вызывает `IBookConditionRequestEvaluator`.

Осталось как **общая** инфраструктура (не legacy-only): `RecommendationResult` / `RecommendationTier` /
`RecommendationReason` / `ScoreBreakdown` / `RequestDifficulty` — их использует условный путь и окно
миниигры (`Excellent`/`Failed`, `Difficulty.Unknown`, `RecommendationResult.Skipped`).

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

`BookConfig.Genres` — **массив жанров**. Условия активного запроса (`genres contains`, `containsAny`,
`containsAll`) проверяют полный список жанров книги.

`BookConfig.PrimaryGenre` = первый элемент `Genres`, но это не "жанр продажи". Он используется как
display/shelf-жанр: визуал книги, группировка полки и пассивная полка остаются привязаны к первому жанру.

Жанр продажи фиксируется в момент продажи:
- пассивная продажа использует `PassiveSaleEvent.ResolvedGenre`;
- активная продажа использует первое пересечение `ActiveRequestRuntime.RequiredGenres` с `BookConfig.Genres`
  в порядке жанров запроса.

Одна продажа увеличивает ровно один счётчик прогресса. Если жанр продажи не известен, статистика
использует fallback на `PrimaryGenre`.

## 7. Accepted implementation decisions (2026-07-13)

- Runtime seam: active purchase flow uses the neutral `ActiveRequestRuntime` (built only via `FromCondition`); no mode switch — conditions is the only path.
- Conditions scoring: matching book => `Excellent` and `10` gold; non-matching book => `Failed` and `0` gold; skip => `Skipped` and `0` gold. `Normal` is unused by the active flow.
- Request text v1: generated programmer-readable text from the condition tree; `Difficulty = Unknown`.
- Condition semantics: `genres` checks only `BookConfig.Genres`; `qualities` checks only `BookConfig.Qualities`; sample content must target the field where the value actually lives.
- Spawn semantics: regular customer spawning assigns the first `N` enabled valid condition requests to `Passive -> Active -> Passive` customers; the rest remain passive-only. Hard override days keep the exact customer count and warn if capacity is below request count.
- Validation: invalid condition requests are filtered before spawning; direct evaluator calls fail closed and log an error.
- Scope: active purchase predicates stay in BookSell and do not reuse the global `Game.Conditions` quest/location engine.

## 8. Follow-up / backlog

Функционал активных покупок на condition-модели считается готовым. Исторические открытые вопросы, которые
не входят в готовый слайс или стали будущими улучшениями, перенесены в
[TODO.md → GAME-14](../TODO.md#-backlog).
