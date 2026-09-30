# Книги с вопросами

Ручной список, не генерируется. Здесь то, что машина обнаружить не может: правда ли написанное
и правильно ли размечена книга. Машинная часть — флаг `recognized` в
[book_descriptions.json](book_descriptions.json) и предупреждения
`Tools/Configs/Validate Book Descriptions`.

Правила письма — [BOOK_DESCRIPTION_STYLE.md](BOOK_DESCRIPTION_STYLE.md), процесс —
[RELEASE_TASKS.md](../RELEASE_TASKS.md) §CONTENT-1.

Закрывая вопрос, удаляйте пункт целиком, а не помечайте «done» — иначе файл превратится в архив.

Собрано по батчам 1–10 (384 книги из 620).

---

## A. Вопросы к тексту

### A1. Текст надо переписать — он мимо содержания

#### book35 — And The World Spins Anyway · Georgie Jones, 2025, 120 стр.

> Barely more than a hundred pages of poems and quiet reflection, fond of weather and open ground, content to sit with a question rather than settle it. Gentle reading for an evening with nowhere to be.

Форма угадана (стихи, объём), тема — нет: «погода и открытые пространства» выведены из тега `Nature`.
По факту это дебютный сборник стихов **и коротких рассказов** про то, чтобы отложить соцсети и
почувствовать себя менее одиноким.

#### book29 — Alvar Aalto · 1963, 176 стр.

> Slim, specialist and unapologetic about it: a mid-century account of one architect's work and the working life behind it. If modern buildings are your thing, here is a quiet afternoon well spent.

Не врёт, но почти ничего не говорит. По факту это визуальная монография: фотографии, планы, наброски,
около шестидесяти лет работ.

#### book606 — Happily Ever After Cookbook; Original Recipes for Book Lovers · 2022

> Recipes built for readers, to be cooked with one hand and a novel propped open under the other. Slim, cheerful and deeply niche: exactly the gift for the friend whose kitchen doubles as a library.

Упущено главное: рецепты придумали авторы романтических романов вместе с шеф-поварами. Плюс жанр
`Travel` — см. B4.

#### book359 — This Book Will Bury Me · Ashley Winstead, 2025

> Long, dark and deliberately nasty contemporary crime, heavy on dread and worse, for readers who want a mystery that keeps twisting and never once flinches at the ugly parts.

Текст описывает профиль тегов, а не книгу. Переписать, когда будет содержание.

#### book423 — The Cream Tea Killer · Judy Leigh, 2025

> Cozy crime with a tearoom appetite: light, brisk, gently nosy, and part of a series that treats a killing as something to be sorted out over a warm scone and a very strong pot of tea.

Чайный регистр взят из названия. Неизвестно даже, какой это том серии.

#### book468 — Ghost Stories; From the Grave · «Collection», 2007

> Short, shivery tales gathered for one dark evening: a slim collection that prefers atmosphere and unanswered questions to tidy explanations, and can be finished before the candle burns down.

Антологию невозможно опознать: **в поле автора стоит плейсхолдер `Collection`**. См. B6.

### A2. Приемлемо как есть — факты взяты из названия, ничего не выдумано

Писавший книгу не знал и честно об этом сказал, но всё сказанное следует из названия, жанра, тегов
и объёма. Достаточно взгляда, переписывать не обязательно.

| id | книга | что именно не проверено |
|---|---|---|
| book350 | Women Travel Solo; 30 Inspiring Stories… | «тридцать женщин» — из названия, архив подтверждает. Не упомянуты фотографии |
| book355 | Theodora Hendrix and the Curious Case of the Cursed Beetle | серия известна, конкретный том нет; все существительные из названия |
| book11 | см. B1 — сначала надо решить, что это за книга | — |
| book145 | Le Corbusier, 1938 | слим-том неопознаваемой редакции; описан только формат |
| book237 | The Complete Illustrated Encyclopedia of Birds of the World | числа видов и картинок взяты из названия |
| book305 | Nature; Encyclopedia of Discovery, 2002 | анонимная компиляция; описан формат |
| book308 | The New Encyclopedia of British, European & African Birds | как book237 |
| book397 | The Encyclopedia of the Weird and Wonderful | автор известен, книга нет |
| book466 | Frank Lloyd Wright, 141 стр. | монография/каталог, издание не опознано |
| book471 | Heinemann Mathematics, 38 стр. | серия известна, конкретная брошюра нет |
| book481 | Jean Dubuffet, 74 стр., 1958 | почти наверняка выставочный каталог, но какой — неясно |
| book554 | So Easy So Good, 2025 | слишком свежая |
| book567 | Zaha Hadid, 160 стр., 1989 | ранняя монография, издание не опознано |
| book591 | Dinosaurs and other Prehistoric Life | детский иллюстрированный справочник, структура неизвестна |
| book598 | The Bookshop Book | см. B1 — поля перепутаны с book600 |
| book623 | British Boutique Hotels | книга неизвестна; описан формат |

---

## B. Вопросы к данным

Текст переписывать бессмысленно, пока не поправлены поля. Последствия геймплейные: запросы покупателей
матчатся по `genres` и `qualities` (`BookConditionRequestEvaluator`), причём **по точному совпадению
строки**.

### B1. Перепутанные поля между строками

**book598 и book600 — у них обменяны автор и год.** Это видно изнутри данных, без внешних источников:

| id | title | author | published |
|---|---|---|---|
| book598 | The Bookshop Book | Lucy Mangan | 2018 |
| book600 | Bookworm; A Memoir of Childhood Reading | Jen Campbell | 2014 |

*The Bookshop Book* — это Джен Кэмпбелл, 2014. *Bookworm* — Люси Мэнган, 2018. То есть **поменять
автора и год местами между двумя строками, и обе становятся верными**. Плюс у обеих жанр `Travel`,
который не подходит ни одной: одна про книжные магазины мира, другая про детское чтение.

Исходное описание `book600` — про книжные магазины, то есть оно тоже «уехало» в чужую строку.

**book10 и book11 — одинаковое название, разные книги:**

| id | title | author | published | pages | genres |
|---|---|---|---|---|---|
| book10 | A Fire Upon the Deep | Vernor Vinge | 1992 | 605 | Fantasy |
| book11 | A Fire Upon the Deep | James Lovegrove & Nancy Holder | 2018 | 334 | Crime/Fantasy/Travel |

Авторы 2018 года писали novelization-серии — либо название чужое, либо автор. `Crime` не подходит ни
к чему.

### B2. Неверный год издания

| id | книга | в конфиге | должно быть |
|---|---|---|---|
| book282 | The Lorax · Dr. Seuss | 1911 | 1971 (автор родился в 1904) |
| book547 | The Poetry of Architecture · John Ruskin | 1800 | Раскин родился в 1819 |
| book486 | Les Miserables · Victor Hugo | 1800 | 1862 (Гюго родился в 1802) |
| book09 | A Dictionary of the English Language | 1747 | 1755 (1747 — год «Плана»); 418 стр. — это сокращённое издание |
| book557 | The Strange Case of Dr. Jekyll and Mr. Hyde | 1875 | 1886 |
| book156 | Misery · Stephen King | 1978 | 1987 |
| book283 | The Lost Symbol · Dan Brown | 1992 | 2009 |
| book232 | The Color Purple | 1976 | 1982 |
| book260 | The Great Gatsby | 1920 | 1925 |
| book364 | Treasure Island | 1880 | 1883 |
| book196 | Slaughterhouse-Five | 1968 | 1969 |
| book200 | The Anthropocene Reviewed | 2019 | 2021 |
| book337 | The Templars | 2018 | 2017 |
| book41 | Architecture in Britain; 1530-1830 | 1950 | 1953 |
| book182 | Richard Serra; Interviews, etc., 1970-1980 | 1970 | сборник не может быть старше последнего интервью — 1980 |
| book378 | Upgrading and Repairing PCs | 1988 | 1988 — первое издание, но 1293 страницы соответствуют куда более позднему |

Год участвует в условиях `publicationYear`/`between`, так что это не косметика.

**Отдельно — правила для переизданий и переводов.** У Эдгара По и манги стоят годы изданий, а не
написания: `book231` 1965, `book335` 1976, `book255` 1997 (245 стр. — значит это сборник, а не одна
повесть), `book446` 1994 (японский том), `book667` 2014 (англоязычный омнибус). У `book543` The
Odyssey `published: -700` — оценка написания, а в поле автора указан современный переводчик.
`book530` The Canterbury Tales — 1478, это печать Кэкстона, а не написание. `book325` — 1375 при
переводчике Армитидже (2007), `book248` The Essential Rumi — 1270 при подборке 1990-х, `book68` —
1885 при иллюстрациях Таши Тюдор середины XX века. Нужно одно правило: год первой публикации или год
конкретного издания.

### B3. Разнобой и опечатки в тегах — самое дорогое

Из **64 уникальных qualities семь использованы ровно один раз, и каждое — опечатка или склейка**:

| значение | книг | должно быть |
|---|---|---|
| `Bigraphy` | 1 (book182) | `Biography` (54) |
| `Biobraphy` | 1 (book103) | `Biography` |
| `Female-Author` | 1 (book04) | `Female Author` (250) |
| `Humor` | 1 (book223) | `Humour` (109) |
| `Self-Help` | 1 (book277) | `Self Help` (22) |
| `Philosophical Contemporary` | 1 (book187) | два тега, склеенные в один |
| `Age Rating Mature [customers do not accept this as fantasy]` | 1 (book10) | `Age Rating Mature` (55); пометку — в комментарий |

**Три понятия разъехались по нескольким написаниям.** Условия сравнивают строку целиком, поэтому это
разные теги, и запрос найдёт только часть книг:

| понятие | написания | всего книг |
|---|---|---|
| возрастной рейтинг | `Age Rating Mature` 55 · `Mature Reading` 28 · `Mature Rating` 12 · вариант с пометкой 1 | 96 |
| нон-фикшн | `Non Fiction` 64 · `Non-Fiction` 71 | 135 |
| селф-хелп | `Self Help` 22 · `Self-Help` 1 | 23 |

Сейчас `sample_requests.json` использует только `Gore`, `Space`, `Tragic`, `Historic`, `Magic`,
`Detective`, `Series`, `Horror`, `Academic`, `Pop Science`, `Dystopia`, `Romance`, `Female Author`,
`Poetry`, `YA`, `Travel Guide`, `Biography`, `Graphic Novel`, `Mystery`, `Dry` — ни одного из
расколотых. Поэтому вживую не бьёт, но выстрелит у первого же запроса на нон-фикшн или возраст.

**Значение жанра попало в qualities** — 9 строк: `Crime` (book02, book356), `Fact` (book15, book305),
`Kids` (book25, book300, book381, book636, book659).

**Дубли внутри одной строки:** `book106` (`Series` дважды), `book496` (`Fiction` дважды),
`book643` (`Nature` дважды).

### B4. Жанр не про эту книгу

**`Crime` на хорроре** — 10 книг: `book58`, `book109`, `book125`, `book133` (It), `book176` (Pet
Sematary), `book213` (Battle Royale), `book255`, `book322` (The Shining), `book335`, плюс `book445`
(Carrie), где `Horror` вообще нет в `genres`. Игрок, просящий детектив, получит «Кладбище домашних
животных».

**`Crime` на не-криминале:** `book629` (детский квест в библиотеке), `book652` (Thornhill — призрак и
травля), `book374` (романтическая история с перемещением во времени), `book371` (The Turn of the
Screw — готика, не преступление).

**`Travel` на кулинарии** — 7 книг с `Travel` и quality `Cooking`: `book606`, `book607`, `book608`
(жанр неверен), `book609` (Bourdain — защитим), `book413`, `book631` (кухни мира — защитимо),
`book527` (The Book of Tea — спорно). Плюс `book600` и `book598` — про книги, не про путешествия.

**`Travel` не по делу:** `book492` (Lord of the Flies — остров, но не путешествие), `book211`
(тематический обзор барокко), `book245` (трактат об интерьерах), `book308` (`Travel Guide` на
справочнике-энциклопедии), `book498` и `book556` (эссе об архитектуре — мягкий случай).

**`Fantasy` на твёрдой НФ:** `book07`, `book10` (Vinge), `book139` (Jurassic Park — `Science Fiction`
стоит в qualities, то есть жанр и quality будто поменяли местами). `book400` (Here Be Dragons) —
исторический роман с жанром `Fantasy` и quality `Magic`, магии в нём нет.

**`Kids` на взрослом:** `book566` (пост-ядерное выживание, единственный жанр `Kids`), `book497`
(Miss Peregrine's — 392 стр., YA, клиффхэнгер).

**`Classic` не к месту:** `book616` (академическая история 2019 года), `book111` (2011 год + quality
`Contemporary` на той же строке), `book139` (техно-триллер 1990 года).

### B5. Отдельные неверные qualities

| id | книга | тег | почему неверно |
|---|---|---|---|
| book194 | Sky Island · L. Frank Baum | `Female Author` | автор мужчина; у того же автора в book550 тега нет |
| book353 | The Yellow Fairy Book | нет `Female Author` | женщина указана в авторах первой |
| book618 | The Boy, The Mole, The Fox and The Horse | `Manga` | британский альбом тушью и акварелью |
| book277 | (руководство по рисованию) | `Manga`, `Graphic Novel` | не комикс |
| book513 | Peter Pan in Kensington Gardens | `Happy Ending` | кончается на закрытом окне детской |
| book633 | Pygmalion | `Happy Ending`, `Plot Twist` | Шоу прямо отказался от романтической пары |
| book356 | There's A Ghost In This House | `Crime` | книжка-картинка, преступления нет |
| book553 | The Snowy Day | жанр `Drama` | 32 страницы про снег |
| book498 | Mont-Saint-Michel and Chartres | `Fiction` | эссеистика |
| book529 | Book of Yokai | `Fiction`, нет `Non Fiction` | научный обзор фольклора |
| book284 | The Lost Words | `Fiction` на строке жанра `Fact`, `Encyclopedic` | 112 стр. акростихов и картин |
| book346 | (четыре путешественницы) | `Fiction`, `Novel`, `Poetry` на строке `Fact` | биографические портреты |
| book42 | Ariel · Sylvia Plath | `Non Fiction` | сборник стихов |
| book248 | The Essential Rumi | `Non Fiction` | поэзия |
| book163 | The Nightingale | `Poetry` | прозаическая сказка |
| book379 | Valperga · Mary Shelley | `Poetry` + `Novel` | проза; теги противоречат друг другу |
| book103 | A Florence Diary | `Poetry`, `Outdated`+`Contemporary` | прозаический дневник; теги взаимоисключающи |
| book169 | The Notebooks of Malte Laurids Brigge | `Biography`, `YA` | роман |
| book388 | Who's Afraid of Virginia Woolf? | `Novel` (при `Play`), `Nature` | трёхактная пьеса |
| book500 | The Mousetrap and Other Plays | `Novel`, `Long`+`Very Long` | сборник пьес |
| book236 | The Complete Father Brown | `Novel`, `Long`+`Very Long` | рассказы |
| book446 | Case Closed Vol. 3 | `Graphic Novel`+`Manga`+`Novel` | взаимоисключающие |
| book91 | The Dry · Jane Harper | `Light Reading`, `Horror`, `Gore`, `Romance`, `Happy Ending` | мрачный процедурал |
| book383 | Violets Are Blue | `Magic` | убийцы лишь считают себя вампирами; то же у book604 |
| book368 | True Grit | `Horror`, `Gore`, `Mystery`, `Detective` | убийца известен с первой страницы |
| book39 | The Animals of Farthing Wood | `Horror` | мрачно, но не хоррор |
| book275 | The Last Time I Lied | `YA` + `Mature Reading` | взрослый триллер |
| book453 | The Chalk Man | `YA` | взрослый триллер |
| book117 | Gregor the Overlander | `YA` | герою одиннадцать |
| book463 | The Eleventh Hour | `Novel` | 38-страничный стихотворный альбом |
| book264 | Hatchet | `Short` + `Novel` | 181 страница |
| book221 | Bleak House | `Long` + `Very Long` | оба сразу; то же у book43, book250 |
| book133 | It | нет `Long`/`Very Long` | 1168 страниц |
| book652 | Thornhill | нет `Long` | 533 страницы |
| book471 | Heinemann Mathematics | `Encyclopedic` | 38-страничная брошюра |
| book254 | The Eyes of Darkness | `Epic` | 340-страничный триллер |
| book595 | Coffee First, Then the World | `Epic`, `Happy Ending` | нон-фикшн о велопробеге |
| book90 | Dreamer's Pool | `Contemporary` | раннесредневековая Ирландия |
| book670 | Dickens at Christmas | `Contemporary` | 1840-е |
| book616 | The Boundless Sea | `Folklore`, `Contemporary` при жанре `Classic` | академическая история |
| book205 | The Bad Beginning | жанр `Fantasy` | магии в серии нет |
| book406 | Know My Name | `Nature`, `Poetry`, `Academic` | мемуар; 13 тегов подряд выглядят как «на всякий случай» |
| book475 | I Know Why the Caged Bird Sings | `Nature` | мемуар |
| book580 | Selected Stories of Chekhov | `Animals`, нет тега сборника | рассказы |
| book309 | New Hampshire | нет `Fiction`/`Non-Fiction` | в отличие от других поэтических строк |
| book187 | The Saviour Fish | `Academic` | репортажная журналистика |
| book43 | (учебник athletic training) | `Self Help` | профессиональный учебник |
| book286 | Lysistrata | `Nature` | не применимо |
| book288 | Madeline | `Coming of Age`, `Historic` | 44-страничная книжка-картинка |
| book226 | Body in the Library | `Philosophical`, `Nature` | не применимо |
| book144 | Lady Audley's Secret | `Political` | тема — класс и респектабельность |
| book67 | (Chicago Manual) | `Outdated`, `Series` | живой справочник; `Series` здесь значит «издания» |
| book98 | Ficciones | жанр `Drama`; `Gore`, `Nature`, `Poetry` | сборник рассказов |
| book317 | The Raven | жанр `Fantasy` | готическая лирика |

### B6. Опечатки в названиях и авторах, плейсхолдеры

| id | поле | в конфиге | должно быть |
|---|---|---|---|
| book295 | title | Maybe You Should **Take** to Someone | **Talk** to Someone |
| book226 | title | `Body in the Library; Miss Marple  #2` | двойной пробел перед `#2`, и у книги есть ведущее «The» |
| book40 | author | Edgar **Allen** Poe | Allan (в book317 написано верно) |
| book370 | author | Mitch **Alborn** | Albom |
| book566 | author | Robert C. O'**Brian** | O'Brien |
| book116 | author | Dr **Allesandra** Pino | Alessandra |
| book468 | author | `Collection` | плейсхолдер вместо автора |
| book466, book481, book567 | author | Frank Lloyd Wright / Jean Dubuffet / Zaha Hadid | у монографий в авторе стоит субъект; book466 датирован 2008, а Райт умер в 1959 |
| book353 | author | `L. B. Alleyne & A. Lang` | серия известна по Эндрю Лэнгу; порядок необычен |
| book68, book325, book98, book131, book248, book543 | author | переводчик/иллюстратор внутри поля автора | решить, отдельное это поле или нет |
| book183 | title | Romancero Gitano | единственное название не по-английски во всём каталоге |

### B7. Прочее

- **`Outdated`** стоит у 41 книги. Это редакторская оценка, а не свойство книги. Решить, осознанный
  это тег (покупатель может просить «не устаревшее») или мусор — сейчас он стоит и на
  `The Origin of Species`, и на `Pygmalion`.
- **`book78`** The Dark is Rising — **вторая** книга цикла, не первая. `Series` стоит, но позиция в
  серии нигде не записана, так что игрок может получить её как точку входа.

---

## C. Требуют выборочной вычитки (`recognized: true`, уверенность неполная)

Текст держится на уровне завязки, детали не утверждаются.

`book472` How Winston Delivered Christmas, `book353` The Yellow Fairy Book, `book137` Julie Chan is
Dead, `book256` Famous Last Words, `book206` Bait and Witch, `book278` The Lighthouse Mystery,
`book64` Chemical Chaos, `book153` Merry Mister Meddle.
