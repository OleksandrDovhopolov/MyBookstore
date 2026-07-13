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

Текущая активная продажа: `RequestConfig` (requests.json) — плоский набор мягких предпочтений
(`DesiredGenres/DesiredTags/DesiredMood/MaxPrice`), а `RecommendationScoringService` считает **взвешенный
балл** (жанр +3, тег +2, mood +1, цена +1, локация +1) и раздаёт тир Excellent/Normal/Failed. Игрок сам
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

**Типы условий (по реально заполненным полям книги):** `genres`, `tags`, `publicationYear`, `pages`.

> ⚠️ Поля `authorSex/size/price/rarity/country/language` из ранних набросков **в контенте не заполнены** —
> под них нет колонок в исходной таблице книг, и в первой версии они **не используются**. Добавлять типы
> под них — только когда появится контент.

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

## 6. Зависимость: книги → список жанров (следующая итерация)

Сейчас `BookConfig.Genre` — **одиночная строка**. Условия используют `genres` + `contains`/`containsAll`,
что подразумевает **массив жанров у книги**. В следующей итерации `BookConfig` будет обновлён
(`Genres[]` вместо одного `Genre`). До миграции хендлер `genres` трактует `contains`/`equal` по одиночному
полю (а `containsAll` по жанрам не имеет смысла на одиночном значении).

## 7. Open questions / TODO

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
- [ ] Миграция `BookConfig` на `Genres[]` (см. §6).
