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
- ~~Spawn semantics: regular customer spawning assigns the first `N` enabled valid condition requests to `Passive -> Active -> Passive` customers~~ — **пересмотрено 2026-09-30, см. §8.1.** Спавнер решает только, *какие слоты* получают активный запрос; *какой именно* запрос — решает `ActiveRequestStep` в момент захвата interaction lock, по актуальной полке. Селектор дня живёт на `CustomerContext` в единственном экземпляре. Hard override days keep the exact customer count and warn if capacity is below request count.
- Validation: invalid condition requests are filtered before spawning; direct evaluator calls fail closed and log an error.
- Scope: active purchase predicates stay in BookSell and do not reuse the global `Game.Conditions` quest/location engine.

## 8. Follow-up / backlog

Функционал активных покупок на condition-модели считается готовым. Исторические открытые вопросы, которые
не входят в готовый слайс или стали будущими улучшениями, перенесены в
[TODO.md → GAME-14](../TODO.md#-backlog).

### 8.1 Принятые решения по выдаче запроса (2026-09-30)

Разбор [GAME-23](RELEASE_TASKS.md#game-23--active-requests-nobody-can-answer-from-todays-shelf) /
[QA-11](RELEASE_TASKS.md#qa-11--raise-active-request-success-probability). Замеры на текущем контенте:
на тот момент 19 запросов × 663 книги, полка 30 слотов — медианная вероятность, что подходящая книга окажется
на полке, **45 %**; у шести запросов — от 9 до 21 %.

- **Выбор запроса становится shelf-aware и late-bound.** Запрос выбирается не при построении плана дня
  в `RegularCustomerSpawner.BuildCustomers`, а в момент, когда покупатель захватывает interaction lock в
  `ActiveRequestStep` — непосредственно перед `ctx.Sink.OnActiveRequestStarted`. Там доступен
  `ctx.Shelf.AvailableForSelection()`, то есть самое свежее состояние полки. Это устраняет разрыв между
  «полка на старте дня» и «полка в момент запроса» by construction, а не компенсацией.
- **Самоедство покупателя — by design, не баг.** `PassiveActivePassiveArchetype` ставит пассивную покупку
  перед активным запросом, а `RequestedGenrePassiveResolver` выбирает жанр из того же
  `Profile.DesiredGenres`, по которому подбирался запрос. Покупатель может купить книгу, которой потом
  сам же ответил бы. Решено оставить: покупатель действует только в рамках своих жанров, это
  последовательно. Именно поэтому late binding несёт всю нагрузку один — компенсирующего механизма нет.
- **Пустой результат shelf-aware фильтра закрывается рероллом** (§8.3), а не отказом от выдачи запроса.
  Вариант «не создавать активного покупателя, если отвечать нечем» рассмотрен и отклонён.
- **Порог в `ActiveRequestValidator`.** Разовая правка контента чинит сегодняшний день; порог не даёт
  завести проблему заново. Внедрено: `n < 5` — error, `n < 15` — warning (n = 15 даёт ≈ 50 % попадания
  на полку из 30, n = 5 — 21 %). Отчёт печатает оценку P(hit) по каждому запросу и список самых узких.
  Отдельно держать в уме `Travel`: в каталоге всего 77 книг этого жанра, узкий Travel-запрос
  рискованнее узкого Classic-запроса (224 книги).
  <br>Вместимость полки в валидаторе продублирована константой: `Book.Sell.Editor` не может ссылаться
  на `Game.Preparation` (ASMDEF_RULES §2), поэтому `ShelfCapacity` держат синхронной с
  `PreparationSessionService.DefaultDailyBookSlots` вручную. Число влияет только на печатаемую
  вероятность, не на вердикт.
- **Бакеты жёсткости в композиторе текста удалены.** Правило скрывало числовое условие из текста,
  оставляя его действующим: на момент разбора пять запросов из девятнадцати отгружались с невидимым
  игроку условием (`req_psycho_01`, `req_fact_02`, `req_classic_02`, `req_drama_02`, `req_kids_01`).
  Это вторая, независимая причина жалоб QA-11: не «не повезло с полкой», а «ответил по тексту и не
  засчитали». Теперь озвучивается каждое условие, влияющее на предикат, — значит ослаблять запросы
  можно любым способом, скрыться нечему. См.
  [ACTIVE_REQUEST_TEXT_COMPOSER.md §2](INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md).

### 8.1.1 Фактический результат правок (2026-09-30)

Шесть узких запросов ослаблены, целевая полоса — 15…35 подходящих книг:

| Запрос | Было | Стало | Что изменили |
|---|---|---|---|
| `req_fantasy_02` | 2 (9 %) | 36 (82 %) | снято `pages between 700–900` |
| `req_fireupon_01` | 2 (9 %) | 23 (66 %) | снято `pages between 550–650` |
| `req_scarlet_01` | 3 (13 %) | 16 (53 %) | снято `publicationYear`, оставлено `pages ≤ 200` |
| `req_travel_02` | 3 (13 %) | 35 (81 %) | снято `pages > 500` |
| `req_kids_02` | 3 (13 %) | 30 (76 %) | `containsAll` → `containsAny` |
| `req_classic_01` | 5 (21 %) | 32 (78 %) | `Dystopia` → `containsAny[Dystopia, Horror]` |

По пулу: медианная вероятность 45 % → **61 %**, минимальная 9 % → **37 %**, запросов ниже порога
ошибки не осталось. Пять запросов (`req_psycho_02`, `req_fact_02`, `req_scarlet_02`, `req_psycho_01`,
`req_fact_01`) сидят в полосе 10–14 книг и дают warning — это следующий эшелон, в правку не входил.

### 8.1.2 Расширение пула до 50 (2026-09-30)

Добавлен 31 запрос, пул вырос с 19 до **50**. Итоговое покрытие: Classic 9, Drama 9, Fantasy 9,
Fact 7, Crime 6, Kids 6, Travel 4. Медианная вероятность по пулу — **68 %**, минимальная не изменилась
(37 %, всё тот же `req_psycho_02`): все новые запросы легли в полосу 19–29 подходящих книг, поэтому
новых warning'ов не появилось.

Ограничения, под которые собирался контент:
- **Только существующие термины и полосы.** Ни одного нового качества, жанра или полосы не заведено —
  иначе условие осталось бы невысказанным, а ловить это пока нечем
  ([DEF-7](RELEASE_TASKS.md#def-7--unvoiced-active-request-conditions-close-the-gap-allow-numbers)).
- **`Female Author` не используется в `none`** — негативной формы у него намеренно нет (§5 композитора).
- **Числовые условия не используются в `none`** — композитор их не озвучивает.
- **Якорь существует и матчит собственный запрос**, длина ≤ 34 символов: длинное название не влезает
  в баббл (у `req_fact_01` из-за этого стоит ручной `descriptionKey`).
- Смысловая сочетаемость жанра и качества проверялась вручную: комбинации вроде `Kids + Gore` или
  `Fact + Magic` проходят по числам, но читаются абсурдно.

Два решения приняты вручную, потому что механика упиралась в замысел: у `req_classic_01` якорная книга
*Nineteen Eighty-Four*, и `Dystopia` сама по себе даёт ровно 5 книг — второе качество подбиралось так,
чтобы остаться в теме и в полосе; `req_scarlet_01` ослаблен снятием года, а не страниц, потому что
снятие страниц давало всего 9 книг.

> ⚠️ Кандидаты на расширение обязаны иметь фразу в `request_phrases.json`. Качество без записи в
> лексиконе останется невысказанным — то есть воспроизведёт ровно тот немой предикат, который чинили.

### 8.2 Backlog: частичное совпадение вместо бинарного исхода

**Не в релизном scope.** Записано, чтобы §7 не читался как закрытый вопрос: там зафиксировано
«matching book → `Excellent` + 10 gold; non-matching → `Failed` + 0», и это принятое решение, а не
единственно возможное.

Идея: запрос из трёх условий, где книга удовлетворяет двум, даёт промежуточный тир и часть золота.
Это поднимает воспринимаемую успешность активной продажи, не трогая ни пул запросов, ни выдачу книг —
самый дешёвый способ убрать ощущение «всё или ничего».

> ⚠️ **Ловушка сериализации.** `RecommendationTier` пишется в save-модуль `book_sell.last_day_result`.
> Значение `1` освободилось от удалённого тира `Normal`, но **переиспользовать его нельзя**: старые
> сейвы прочитают новый тир как старый. Новому тиру нужно новое, ранее не занятое значение.
> Это знание невозможно восстановить из кода — в enum остался только комментарий о пропуске единицы.

### 8.3 Backlog: замена запроса за валюту (reroll)

**Не в релизном scope**, но проектируется вместе с §8.1: реролл — штатный выход из ситуации, когда на
полке нечем ответить, и потому заменяет отклонённый fallback.

Контракт:
- **Реролл обязан выдавать решаемый запрос.** Он смотрит на полку в момент нажатия — то есть технически
  невозможен без late binding из §8.1. Без этой гарантии реролл продаёт воздух.
- **Замена вслепую.** Игрок видит текущий запрос, платит и получает новый, не зная заранее какой.
- **Цена фиксированная.** Прогрессия цены в течение дня — отдельное решение, сейчас не берём.
- **Оффер не показывается** (именно не показывается, а не блокируется), когда золота не хватает или
  когда решаемых запросов на полке нет вовсе.
