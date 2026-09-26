# Active Request Text Composer — лексикон и прогон 19 запросов

- **Статус:** ✅ Implemented (CONTENT-2)
- **Тип:** контент + спека композитора текста активного запроса
- **Related:** [ACTIVE_REQUEST_CONDITIONS.md](../ACTIVE_REQUEST_CONDITIONS.md), [CORE_LOOP.md](../CORE_LOOP.md)

Активный запрос в мини-игре рекомендации больше не показывает техническую строку условий. Текст
собирается из `conditions` композитором
[`LexiconActiveRequestTextComposer`](../../Assets/Game/Features/BookSell/Services/LexiconActiveRequestTextComposer.cs)
по лексикону из `request_phrases.json`; сами фразы живут в `localization_quests_en.json`.

Ниже — модель, авторский лексикон и фактический вывод композитора по всем 19 запросам.

---

## 1. Модель: слоты

```
[opener] [lead] [core][, constraint][, exclusion]. [anchor]
```

| Слот | Источник | Обязателен |
|---|---|---|
| `opener` | пул «шума»: приветствие, настроение, спешка. **К запросу отношения не имеет** | нет (1 из 9 вариантов — пустой) |
| `lead` | пул зачинов («I'm after», «I'd like», …) | да |
| `core` | `genres` + `qualities` из групп `all` / `any` | да |
| `constraint` | `pages` / `publicationYear` → мягкая полоса, без цифр | по жёсткости (см. §2) |
| `exclusion` | группа `none` → «but nothing …» | да, если группа есть |
| `anchor` | `bookTitle` курсивом (`<i>`, TMP rich text) | да |

Внутри `core` фрагменты идут **жанр → качества → числовая полоса**, независимо от того, в каком
порядке условия записаны в конфиге: иначе получается «I want with a detective in it, a classic».

Якорь — не флейвор, а механика: все 19 референсных книг существуют в каталоге и матчат собственный
запрос (проверено прогоном `sample_requests.json` × `books.json`). Если книга на полке — это
гарантированно верный ответ.

## 2. Правило отбора: сколько условий озвучивать

Зависит от жёсткости запроса — числа подходящих книг в каталоге (663 книги). Считается на лету
через `IBookConditionRequestEvaluator`, в контенте не дублируется.

| Жёсткость | Матчей | Что вербализуем |
|---|---|---|
| tight | ≤ 5 | всё: жанр, все qualities, числовые полосы, `none` |
| medium | 6–15 | жанр, qualities, `none`; числовое — только `between` (узкая полоса) |
| loose | > 15 | жанр, qualities, `none`; числовое отбрасываем |

При 2–3 подходящих книгах умолчать о `pages 550–650` — значит сделать запрос нечестным. При 37
подходящих (`req_canterville_01`) перечислять всё — значит получить робота.

## 3. Лексикон

38 атомарных терминов покрывают все листья 19 запросов. Конфиг `request_phrases.json` (57 записей)
хранит **только ключи**; строки — в `localization_quests_en.json` (83 ключа).

### 3.1 Жанры (7)

| Значение | Позитивная форма | Негативная (в `none`) |
|---|---|---|
| `Classic` | a classic | but nothing too classic |
| `Crime` | a crime story | but nothing criminal |
| `Drama` | something dramatic | but nothing heavy |
| `Fact` | something factual | but nothing dry and factual |
| `Fantasy` | a fantasy | but nothing fantastical |
| `Kids` | a kids' book | but nothing childish |
| `Travel` | a travel book | but nothing about travelling |

> ⚠️ `equal` на `genres`/`qualities` в
> [BookConditionRequestEvaluator.cs:241](../../Assets/Game/Features/BookSell/Services/BookConditionRequestEvaluator.cs)
> уходит в тот же `Contains()`, что и `contains` — **`equal` ≠ «только этот жанр»**. Термин
> матчится по `type`+`value`, оба оператора попадают в одну запись.

### 3.2 Качества (20)

| Значение | Позитивная форма | Негативная (в `none`) |
|---|---|---|
| `Academic` | properly academic | but nothing academic |
| `Biography` | someone's life story | but not a biography |
| `Detective` | with a detective in it | but no detectives |
| `Dry` | dry and matter-of-fact | but not dry |
| `Dystopia` | with a grim future in it | but nothing bleak |
| `Female Author` | written by a woman | — (намеренно нет, см. §5) |
| `Gore` | not squeamish about blood | but nothing gory |
| `Graphic Novel` | told in pictures | but not a picture book |
| `Historic` | set back in history | but nothing historical |
| `Horror` | genuinely scary | but nothing scary |
| `Magic` | with a bit of magic in it | but no magic |
| `Mystery` | with a proper mystery in it | but no mysteries |
| `Poetry` | poetry | but not poetry |
| `Pop Science` | science made readable | but not pop science |
| `Romance` | a romance | but nothing romantic |
| `Series` | part of a series | but not part of a series |
| `Space` | set out in space | but nothing about space |
| `Tragic` | a sad one | but nothing sad |
| `Travel Guide` | an actual guide, not someone's memoir | but not a guidebook |
| `YA` | for a younger crowd | but nothing for teenagers |

**Каждый термин нужен в двух формах.** `Gore` показателен: в `req_fantasy_01` он в `all`, в
`req_fireupon_01` — в `none`.

Фразы фрагментов не должны содержать запятых: фрагменты склеиваются через `, `, и внутренняя
запятая ломает список. Исключение — `Travel Guide`, где запятая несёт смысл противопоставления.

### 3.3 Полосы `pages` (7)

| Условие | Фраза |
|---|---|
| `less 100` | very short |
| `lessOrEqual 200` | thin enough to finish on the bus |
| `between 300–500` | a sensible size |
| `greater 400` | a thick one |
| `greater 500` | a real brick |
| `between 550–650` | on the hefty side |
| `between 700–900` | the fatter the better |

### 3.4 Полосы `publicationYear` (4)

| Условие | Фраза |
|---|---|
| `less 1900` | an old one |
| `between 1850–1900` | proper old-fashioned stuff |
| `between 1990–2000` | nineties, ideally |
| `greater 2015` | something recent |

Цифры в текст не попадают никогда — только полоса. Точную границу докалибрует якорь.

### 3.5 Комбо-правила

Вся НФ в каталоге лежит под жанром `Fantasy`, а «космос» — это quality `Space`. Пословно выходит
«a fantasy set out in space», поэтому пара схлопывается:

| Пара | Фраза |
|---|---|
| `genres:Fantasy` + `qualities:Space` | proper space sci-fi |

Затрагивает 3 запроса из 19. Больше комбо на текущем контенте не требуется.

### 3.6 Пулы обёртки

`opener` (9, включая пустой): `""`, «Sooo…», «Just dropping by real quick!», «Hi! Ugh, this rain…»,
«Sorry, I've only got a few minutes.», «I've been on my feet all morning, forgive me.», «Right, my
turn finally!», «I have to pick up my kid in a bit, so —», «Don't mind me, I'll be quick.»

`lead` (4): «I'm after», «I'd like», «I'm looking for», «I want».

`anchor` (5): «Something like *{0}*?», «Do you have anything like *{0}*?», «*{0}* is one of my
all-time favourites.», «Think *{0}*.», «*{0}*, that sort of thing.»

Выбор из пула — **детерминированный**: индекс = FNV-1a от `request.Id` (свой стабильный хеш, не
`string.GetHashCode()`). Запрос всегда читается одинаково, ничего не перекатывается после reload
дня. От `dayIndex` намеренно не зависим: `GetRequests()` дня не видит, а `UnityRandomSalesRandom`
не сеется — разнообразие даёт сам пул из 19 запросов.

---

## 4. Фактический вывод композитора

Ниже — то, что реально возвращает `Compose()` на живом контенте. В скобках — жёсткость и число
подходящих книг.

**`req_scarlet_01`** (tight/3)
> I'm looking for a crime story, proper old-fashioned stuff, thin enough to finish on the bus.
> Do you have anything like *A Study in Scarlet*?

**`req_scarlet_02`** (medium/12)
> Sorry, I've only got a few minutes. I want a classic, with a detective in it, part of a series.
> Something like *A Study in Scarlet*?

**`req_psycho_01`** (medium/13)
> Just dropping by real quick! I'm after a crime story, genuinely scary. Think *American Psycho*.

**`req_psycho_02`** (medium/10)
> Sorry, I've only got a few minutes. I'd like a crime story, not squeamish about blood or a sad
> one, nineties, ideally. *American Psycho*, that sort of thing.

**`req_fact_01`** (override)
> Just dropping by real quick! I need something factual and properly academic, a decent size, not a
> monster. Something like *Design Patterns*?

Единственный ручной текст: полное название *Design Patterns; Elements of Reusable Object-Oriented
Software* (58 символов) не влезает в баббл, поэтому у этого запроса сохранён `descriptionKey`.

**`req_fact_02`** (medium/11)
> I have to pick up my kid in a bit, so — I want something factual, science made readable or set
> back in history. *A Short History of Nearly Everything* is one of my all-time favourites.

**`req_classic_01`** (tight/5)
> Hi! Ugh, this rain… I'm after a classic, with a grim future in it. *Nineteen Eighty-Four* is one
> of my all-time favourites.

**`req_classic_02`** (medium/15)
> Don't mind me, I'll be quick. I'd like a classic, a romance, written by a woman. *Emma*, that
> sort of thing.

**`req_drama_01`** (loose/18)
> Just dropping by real quick! I'd like something dramatic, poetry, a sad one. *Ariel*, that sort
> of thing.

**`req_drama_02`** (medium/15)
> Don't mind me, I'll be quick. I'm after something dramatic, for a younger crowd. Think *Better
> Than the Movies*.

**`req_travel_01`** (loose/20)
> Hi! Ugh, this rain… I'm after a travel book, an actual guide, not someone's memoir. *1,000 Places
> to See Before You Die*, that sort of thing.

**`req_travel_02`** (tight/3)
> I have to pick up my kid in a bit, so — I'd like a travel book, someone's life story or set back
> in history, a real brick. Something like *A Tramp Abroad*?

**`req_kids_01`** (loose/20)
> Sorry, I've only got a few minutes. I want a kids' book. Think *Frog and Toad Together*.

**`req_kids_02`** (tight/3)
> Right, my turn finally! I'm looking for a kids' book, told in pictures, with a proper mystery in
> it. Think *The Invention of Hugo Cabret*.

**`req_fantasy_01`** (loose/24)
> Just dropping by real quick! I'm looking for a fantasy, with a bit of magic in it, not squeamish
> about blood. *A Game of Thrones*, that sort of thing.

**`req_fantasy_02`** (tight/2)
> I've been on my feet all morning, forgive me. I want a fantasy, set out in space or dry and
> matter-of-fact, the fatter the better. Do you have anything like *A Deepness in the Sky*?

**`req_canterville_01`** (loose/37)
> Don't mind me, I'll be quick. I'd like a classic, with a bit of magic in it, but nothing heavy.
> Think *The Canterville Ghost*.

**`req_anathem_01`** (loose/23)
> Just dropping by real quick! I'm after proper space sci-fi, but nothing gory. Something like
> *Anathem*?

**`req_fireupon_01`** (tight/2)
> Sorry, I've only got a few minutes. I'm after proper space sci-fi, on the hefty side, but nothing
> gory. Something like *A Fire Upon the Deep*?

## 5. Правка контента: `req_anathem_01`

Исходно у запроса стояло `none: qualities contains Female Author`. Композитор честно собирал
«…proper space sci-fi, but nothing written by a woman» — такое не отгружаем; умолчать тоже нельзя,
иначе часть «подходящих на слух» книг проваливается по невидимому условию. Исключение заменено на
`none: qualities contains Gore` — как в соседнем `req_fireupon_01`. Число подходящих книг выросло с
18 до 23.

Негативная форма `Female Author` в лексиконе намеренно отсутствует: если такое исключение появится
снова, композитор пропустит фрагмент и запишет warning, а валидатор поднимет это до ошибки.

## 6. Что где лежит

| | |
|---|---|
| Лексикон | `Assets/Configs/request_phrases.json` (57 записей) |
| Фразы | `Assets/Configs/localization_quests_en.json` (83 ключа `request.*`) |
| Модель конфига | `Assets/Game/Features/Configs/Models/RequestPhraseConfig.cs` |
| Композитор | `Assets/Game/Features/BookSell/Services/LexiconActiveRequestTextComposer.cs` |
| Подключение | `ConfigActiveRequestRuntimeProvider`, `ActiveSaleCheatModule`, `BookSellVContainerBindings` |
| Тесты | `LexiconActiveRequestTextComposerTests`, `ActiveRequestTextContentTests` |

`IBookConditionRequestEvaluator.BuildDebugText` **не удалён** — техническая строка условий осталась
инструментом `ActiveRequestValidator` и чит-панели, просто больше не показывается игроку.

Ручная проверка — через чит-панель: `ActiveSaleCheatModule` рисует кнопку на каждый запрос и
открывает окно рекомендации напрямую, играть день не нужно.

## 7. Вне объёма

- `bookId` в `RequestDefinitionConfig` вместо свободной строки `BookTitle` + проверка якоря в
  `ActiveRequestValidator`. Все 19 заголовков сматчились с `localization_books_en.json` точь-в-точь,
  миграция разовая. Пока якорь подставляется как есть и на втором языке поедет мимо локализации.
- Второй язык: фрагменты локализуемы, но **сборка предложения — в коде и заточена под EN**
  (порядок слотов, склейка через запятую, «or» между вариантами). Осознанный размен; пересматривать
  при появлении второго локаля.
