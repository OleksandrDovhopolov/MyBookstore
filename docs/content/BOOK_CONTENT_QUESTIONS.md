# Книги с вопросами

Ручной список, не генерируется. Здесь то, что машина обнаружить не может: правда ли написанное
и правильно ли размечена книга. Машинная часть — флаг `recognized` в
[book_descriptions.json](book_descriptions.json) и предупреждения
`Tools/Configs/Validate Book Descriptions`.

Правила письма — [BOOK_DESCRIPTION_STYLE.md](BOOK_DESCRIPTION_STYLE.md), процесс —
[RELEASE_TASKS.md](../RELEASE_TASKS.md) §CONTENT-1.

Закрывая вопрос, удаляйте пункт целиком, а не помечайте «done» — иначе файл превратится в архив.

Собрано по батчам 1–6 (205 книг из 620).

---

## A. Вопросы к тексту: писавший не знал книгу (`recognized: false`)

Текст написан только по названию, жанру, тегам, году и объёму — без сюжета, имён и мест. Формально
корректен, но может быть мимо содержания.

### book350 — Women Travel Solo; 30 Inspiring Stories of Adventure, Curiosity and the Power of Self-Discovery

> Thirty women, thirty solo departures, and thirty accounts of what curiosity does to a person who goes alone. Warm, brisk company for anyone still talking themselves into booking the ticket.

**Вопросов по сути нет.** Все факты из названия, архив их подтверждает. Не упомянуты фотографии,
которыми книга снабжена — потеря детали, не ошибка. Можно принимать как есть.

### book35 — And The World Spins Anyway · Georgie Jones, 2025, 120 стр.

> Barely more than a hundred pages of poems and quiet reflection, fond of weather and open ground, content to sit with a question rather than settle it. Gentle reading for an evening with nowhere to be.

**Мимо содержания.** Форма угадана (стихи, объём), тема — нет: «погода и открытые пространства»
выведены из тега `Nature`. По факту это дебютный сборник стихов **и коротких рассказов** про то, чтобы
отложить соцсети и почувствовать себя менее одиноким. Переписать с уточнением.

### book29 — Alvar Aalto · Alvar Aalto, 1963, 176 стр.

> Slim, specialist and unapologetic about it: a mid-century account of one architect's work and the working life behind it. If modern buildings are your thing, here is a quiet afternoon well spent.

**Не врёт, но почти ничего не говорит.** По факту это визуальная монография: фотографии, планы,
наброски, около шестидесяти лет работ. Переписать, назвав это альбомом.

### book606 — Happily Ever After Cookbook; Original Recipes for Book Lovers · Various Authors, 2022

> Recipes built for readers, to be cooked with one hand and a novel propped open under the other. Slim, cheerful and deeply niche: exactly the gift for the friend whose kitchen doubles as a library.

**Упущено главное:** рецепты придумали авторы романтических романов вместе с шеф-поварами. Плюс см. B4
про жанр `Travel`.

### book355 — Theodora Hendrix and the Curious Case of the Cursed Beetle, 2021

> Monsters, curses and a beetle that plainly should have been left alone. Recent middle-grade spookiness, long enough to last a week of bedtimes, and creepy in the cheerful way rather than the sleepless way.

Серия (девочка, выращенная монстрами) известна, конкретный том — нет. Все существительные взяты из
названия. Проверить, про что именно этот том.

### book359 — This Book Will Bury Me · Ashley Winstead, 2025

> Long, dark and deliberately nasty contemporary crime, heavy on dread and worse, for readers who want a mystery that keeps twisting and never once flinches at the ugly parts.

Текст описывает профиль тегов, а не книгу. Слишком свежая. Переписать, когда будет содержание.

### book423 — The Cream Tea Killer · Judy Leigh, 2025

> Cozy crime with a tearoom appetite: light, brisk, gently nosy, and part of a series that treats a killing as something to be sorted out over a warm scone and a very strong pot of tea.

Чайный регистр взят из названия. Неизвестно даже, какой это том серии.

### book468 — Ghost Stories; From the Grave · «Collection», 2007

> Short, shivery tales gathered for one dark evening: a slim collection that prefers atmosphere and unanswered questions to tidy explanations, and can be finished before the candle burns down.

Антологию опознать невозможно — **в поле автора стоит плейсхолдер `Collection`**. Пока не выяснится,
что это за сборник, текст останется общим. См. также B7.

### book11 — см. B1: строку нельзя описывать, пока не решено, что это за книга

---

## B. Вопросы к данным

Текст переписывать бессмысленно, пока не поправлены поля. Последствия геймплейные: запросы покупателей
матчатся по `genres` и `qualities` (`BookConditionRequestEvaluator`), то есть игрок получает на запрос
книгу не того жанра — и читает описание, которое с жанром расходится.

### B1. Строка склеена из двух разных книг

| id | что в строке | проблема |
|---|---|---|
| book600 | title *Bookworm; A Memoir of Childhood Reading*, author Jen Campbell, 2014, описание про книжные магазины мира | Название — мемуар Люси Мэнган (2018). Автор, год и исходное описание — *The Bookshop Book* Джен Кэмпбелл. Файл противоречит сам себе: одна из половин заведомо неверна. Жанр `Travel` не подходит ни к одному из вариантов |
| book11 | title *A Fire Upon the Deep*, author James Lovegrove & Nancy Holder, 2018, 334 стр., жанры Crime/Fantasy/Travel | **Название дублирует book10** (Vernor Vinge, 1992, 605 стр.). Два разных издания под одним названием; авторы 2018 года писали novelization-серии, так что либо название чужое, либо автор. `Crime` не подходит ни к чему |

Решить, что оставляем, и только потом писать текст. Текущие черновики написаны по названию.

### B2. Неверный год издания

| id | книга | в конфиге | должно быть |
|---|---|---|---|
| book282 | The Lorax · Dr. Seuss | **1911** | 1971 (автор родился в 1904) |
| book156 | Misery · Stephen King | **1978** | 1987 |
| book283 | The Lost Symbol · Dan Brown | **1992** | 2009 |
| book557 | The Strange Case of Dr. Jekyll and Mr. Hyde | **1875** | 1886 |

Год участвует в условиях запросов (`publicationYear`, `between`), так что это не косметика: книга
попадает или не попадает в выборку по неверному году. В текстах год не упомянут ни в одном из четырёх,
так что описания переписывать не нужно.

**Отдельно — непоследовательность с переизданиями.** У Эдгара По стоят годы изданий, а не написания:
`book231` 1965 (29 стр.), `book335` 1976 (31 стр.), `book255` 1997 (245 стр. — то есть это сборник, а
не одна повесть). У манги стоит год то японского тома, то англоязычного: `book446` 1994, `book667`
2014. Плюс `book478` 1984 против 1985, `book433` 2021 против 2020, `book594` 2017 (собрание, а
новеллы выходили раньше). Нужно одно правило: год первой публикации или год конкретного издания.

### B3. Возрастной тег существует в четырёх написаниях — 96 книг

| значение | книг |
|---|---|
| `Age Rating Mature` | 55 |
| `Mature Reading` | 28 |
| `Mature Rating` | 12 |
| `Age Rating Mature [customers do not accept this as fantasy]` | 1 (`book10`) |

Условия по `qualities` сравнивают строку целиком, поэтому это **четыре разных тега**: запрос на
`Age Rating Mature` не найдёт 41 книгу, которая помечена тем же по смыслу, но иначе написанным тегом.
Сейчас ни один запрос в `sample_requests.json` возрастной тег не использует, так что вживую это не
бьёт — но выстрелит у первого же запроса, который его возьмёт.

Заодно: у `book10` в значение тега попала редакторская пометка
`[customers do not accept this as fantasy]`. Пометку — в комментарий или в этот файл, из данных убрать.

Из 64 уникальных qualities каталога стоит вообще проверить весь список на такие дубли.

### B4. Жанр не про эту книгу

**`Crime` на хорроре** — 9 книг: `book58` Camp Damascus, `book109` Full Dark No Stars, `book125` Home
Before Dark, `book133` It, `book176` Pet Sematary, `book213` Battle Royale, `book255` The Fall of the
House of Usher, `book322` The Shining, `book335` The Tell-Tale Heart. Плюс `book445` Carrie, где
`Horror` вообще нет в `genres`. Игрок, просящий детектив, получит «Кладбище домашних животных».

**`Crime` на не-криминале:** `book629` Escape from Mr. Lemoncello's Library (детский квест в
библиотеке, преступления нет), `book652` Thornhill (история про травлю и призрака), `book374` The
Unmaking of June Farrow (романтическая история с перемещением во времени).

**`Travel` на кулинарии** — 7 книг с `Travel` и quality `Cooking`:

| id | книга | вердикт |
|---|---|---|
| book606 | Happily Ever After Cookbook | жанр неверен |
| book607 | Cook As You Are · Ruby Tandoh | жанр неверен |
| book608 | Cook Korean! · Robin Ha | жанр неверен |
| book609 | A Cook's Tour · Anthony Bourdain | еда как путешествие — защитим |
| book413 | Round the World in Eighty Dishes | кухни мира — защитим |
| book527 | The Book of Tea | `Cooking` сомнителен, `Travel` спорен |
| book631 | Provence to Pondicherry | пограничный случай |

`Travel` на мемуаре о чтении: `book600`. Эта разметка и делала `Travel` дефицитным жанром.

**`Travel` на эссе об архитектуре:** `book498` Mont-Saint-Michel and Chartres, `book556` The Stones of
Venice. Мягкий случай — исторически близко к путевой литературе.

**`Fantasy` на твёрдой НФ:** `book07` A Deepness in the Sky, `book10` A Fire Upon the Deep. Обе —
космическая фантастика, `Science Fiction` честно стоит в qualities. Как «фэнтези» карточки читаться
не будут.

**`Kids` на взрослом:** `book566` Z for Zachariah (пост-ядерное выживание с `Horror`/`Dystopia`, единственный
жанр `Kids`), `book497` Miss Peregrine's (392 стр., YA, том кончается клиффхэнгером). Обе встанут в
Kids-полку рядом с книжками-картинками.

### B5. Отдельные неверные qualities

| id | книга | тег | почему неверно |
|---|---|---|---|
| book194 | Sky Island · L. Frank Baum | `Female Author` | автор мужчина; у того же автора в `book550` тега нет |
| book618 | The Boy, The Mole, The Fox and The Horse | `Manga` | британский альбом тушью и акварелью; `Graphic Novel` защитим, `Manga` нет |
| book513 | Peter Pan in Kensington Gardens | `Happy Ending` | книга кончается на закрытом окне детской — это меланхоличная ветка истории |
| book356 | There's A Ghost In This House | `Crime` | книжка-картинка на 44 страницы, преступления нет; `Mystery` уже стоит |
| book553 | The Snowy Day | жанр `Drama` | 32 страницы про снег, конфликта нет |
| book498 | Mont-Saint-Michel and Chartres | `Fiction` | это эссеистика |
| book42 | Ariel · Sylvia Plath | `Non Fiction` | сборник стихов |
| book91 | The Dry · Jane Harper | `Light Reading`, `Horror`, `Gore`, `Romance`, `Happy Ending` | мрачный процедурал про засуху; `Light Reading` особенно мимо |
| book383 | Violets Are Blue | `Magic` | убийцы лишь считают себя вампирами, сверхъестественного нет. То же у `book604` |
| book275 | The Last Time I Lied | `YA` + `Mature Reading` одновременно | взрослый триллер |
| book453 | The Chalk Man | `YA` | взрослый триллер |
| book117 | Gregor the Overlander | `YA` | герою одиннадцать, это middle-grade |
| book463 | The Eleventh Hour | `Novel` | 38-страничный стихотворный альбом-головоломка |
| book264 | Hatchet | `Short` + `Novel` одновременно | 181 страница |
| book500 | The Mousetrap and Other Plays | `Long` + `Very Long` + `Novel` | сборник пьес |
| book446 | Case Closed Vol. 3 | `Graphic Novel` + `Manga` + `Novel` | взаимоисключающие |
| book221 | Bleak House | `Long` + `Very Long` | оба сразу |
| book133 | It | ни `Long`, ни `Very Long` | 1168 страниц, при этом у книг вдвое короче теги стоят |
| book652 | Thornhill | нет `Long` | 533 страницы |
| book254 | The Eyes of Darkness | `Epic` | 340-страничный триллер |
| book90 | Dreamer's Pool | `Contemporary` | действие в раннесредневековой Ирландии; видимо, тег означает «недавно издано», но в других строках читается как эпоха |
| book205 | The Bad Beginning | жанр `Fantasy` | магии в серии нет, тег тянет книгу в запросы, которые она не может закрыть |

### B6. Редакторские пометки и плейсхолдеры вместо данных

| id | поле | значение |
|---|---|---|
| book10 | quality | `Age Rating Mature [customers do not accept this as fantasy]` — пометка внутри значения тега |
| book563 | quality | `Outdated` — оценка, а не свойство книги. Тег стоит у 41 книги; решить, это осознанный тег (покупатель может просить «не устаревшее») или мусор |
| book468 | author | `Collection` — плейсхолдер вместо автора |
| book353 | author | `L. B. Alleyne & A. Lang` — необычный порядок: серия известна по Эндрю Лэнгу. При этом `Female Author` отсутствует, хотя женщина указана первой |

### B7. Опечатки в авторах

| id | в конфиге | должно быть |
|---|---|---|
| book566 | Robert C. O'Brian | Robert C. O'Brien (он же автор *Mrs. Frisby and the Rats of NIMH*) |

---

## C. Требуют выборочной вычитки (`recognized: true`, но уверенность неполная)

Текст держится на уровне завязки, детали не утверждаются. Проверить, если будет время.

`book472` How Winston Delivered Christmas (заявлены «crafts and recipes»), `book353` The Yellow Fairy
Book (состав сказок намеренно размыт), `book137` Julie Chan is Dead, `book256` Famous Last Words,
`book206` Bait and Witch, `book278` The Lighthouse Mystery, `book64` Chemical Chaos.
