# Книги с вопросами

Ручной список, не генерируется. Здесь то, что машина обнаружить не может: правда ли написанное
и правильно ли размечена книга. Машинная часть — флаг `recognized` в
[book_descriptions.json](book_descriptions.json) и предупреждения
`Tools/Configs/Validate Book Descriptions`.

Правила письма — [BOOK_DESCRIPTION_STYLE.md](BOOK_DESCRIPTION_STYLE.md), процесс —
[RELEASE_TASKS.md](../RELEASE_TASKS.md) §CONTENT-1.

Закрывая вопрос, удаляйте пункт целиком, а не помечайте «done» — иначе файл превратится в архив.

---

## A. Вопросы к тексту: писавший не знал книгу (`recognized: false`)

Текст в этих строках написан только по названию, жанру, тегам, году и объёму — без сюжета, имён
и мест. Формально он корректен, но может быть мимо содержания.

### book350 — Women Travel Solo; 30 Inspiring Stories of Adventure, Curiosity and the Power of Self-Discovery

> Thirty women, thirty solo departures, and thirty accounts of what curiosity does to a person who goes alone. Warm, brisk company for anyone still talking themselves into booking the ticket.

**Вопросов по сути нет.** Все факты взяты из названия, архив их подтверждает. Не упомянуты
фотографии, которыми книга снабжена — потеря детали, не ошибка. Можно принимать как есть.

### book35 — And The World Spins Anyway · Georgie Jones, 2025, 120 стр.

> Barely more than a hundred pages of poems and quiet reflection, fond of weather and open ground, content to sit with a question rather than settle it. Gentle reading for an evening with nowhere to be.

**Мимо содержания.** Форма угадана (стихи, объём), тема — нет: «погода и открытые пространства»
выведены из тега `Nature`. По факту это дебютный сборник стихов **и коротких рассказов**, и он про
то, чтобы отложить соцсети и почувствовать себя менее одиноким. Переписать с этим уточнением.

### book29 — Alvar Aalto · Alvar Aalto, 1963, 176 стр.

> Slim, specialist and unapologetic about it: a mid-century account of one architect's work and the working life behind it. If modern buildings are your thing, here is a quiet afternoon well spent.

**Не врёт, но почти ничего не говорит.** По факту это визуальная монография: фотографии, планы,
наброски, около шестидесяти лет работ. Переписать, назвав это альбомом.

### book606 — Happily Ever After Cookbook; Original Recipes for Book Lovers · Various Authors, 2022

> Recipes built for readers, to be cooked with one hand and a novel propped open under the other. Slim, cheerful and deeply niche: exactly the gift for the friend whose kitchen doubles as a library.

**Упущено главное.** Рецепты придумали авторы романтических романов вместе с шеф-поварами — это и
есть суть книги. Переписать с этим. Отдельно см. пункт B про жанр.

---

## B. Вопросы к данным: строка каталога описывает не то, что в ней написано

Здесь текст переписывать бессмысленно, пока не поправлены поля. Последствие геймплейное: запросы
покупателей матчатся по `genres` и `qualities`, то есть игрок получает на запрос жанра книгу не того
жанра — и читает описание, которое с жанром расходится.

### book600 — Bookworm; A Memoir of Childhood Reading · Jen Campbell, 2014, 273 стр.

> Growing up is retold here through the books that did the raising, one shelf at a time. Funny, fond and easy company, the kind of memoir that keeps nudging you towards a reread.

**Строка склеена из двух разных книг.** Название — мемуар о детском чтении (это книга Люси Мэнган,
2018). Автор, год и исходное описание — это *The Bookshop Book* Джен Кэмпбелл, 2014, про необычные
книжные магазины мира. Файл противоречит сам себе, поэтому какая-то половина строки заведомо неверна.

Плюс единственный жанр — `Travel`, хотя ни один из двух вариантов книги не про путешествия.

**Решить, что оставляем (название или автора с годом), и только потом писать текст.** Текущий
черновик написан по названию, то есть, возможно, не про ту книгу.

### Кулинарные книги с жанром `Travel`

Семь Real-книг с жанром `Travel` несут quality `Cooking`. Часть из них — путешествия через еду, и
это защитимо; часть — просто кулинария, попавшая не в тот жанр.

| id | книга | жанры | вердикт |
|---|---|---|---|
| book606 | Happily Ever After Cookbook | Travel/Fact | кулинария, путешествий нет — жанр неверен |
| book607 | Cook As You Are · Ruby Tandoh | Travel/Fact | кулинария, путешествий нет — жанр неверен |
| book608 | Cook Korean! · Robin Ha | Travel/Fact | комикс-кулинария, путешествий нет — жанр неверен |
| book609 | A Cook's Tour · Anthony Bourdain | Travel | еда как путешествие — `Travel` защитим |
| book413 | Round the World in Eighty Dishes | Fact/Travel | кухни мира, ещё не переписана — `Travel` защитим |
| book527 | The Book of Tea | Travel/Classic | эссе о чайной церемонии; `Cooking` сомнителен, `Travel` спорен |
| book631 | Provence to Pondicherry | Fact/Travel | рецепты Франции и Азии, ещё не переписана — пограничный случай |

Чинится правкой `genres`/`qualities`. Стоит помнить, что именно эта разметка делала `Travel`
дефицитным жанром: из 22 книг с `Travel` первым жанром часть — не про путешествия.

### Эссе об архитектуре с жанром `Travel`

| id | книга | жанры |
|---|---|---|
| book498 | Mont-Saint-Michel and Chartres · Henry Adams | Travel/Classic |
| book556 | The Stones of Venice · John Ruskin | Travel/Classic |

Мягкий случай: исторически это близко к путевой литературе, и описания написаны правдиво. Но у
`book498` стоит quality `Fiction`, хотя это эссеистика — вот это точно неверно.

### book07, book10 — Vernor Vinge с жанром `Fantasy`

> **book07:** Two human fleets circle a world of spider-like people racing through their own industrial revolution, and only one fleet means them well.
>
> **book10:** An old evil wakes in the high reaches of the galaxy, while two marooned children fall in among sword-carrying pack creatures on a medieval world.

Первый жанр — `Fantasy`, притом что обе книги — твёрдая космическая фантастика, и `Science Fiction`
честно стоит в qualities. Описания написаны по фактическим тегам, поэтому как «фэнтези» эти карточки
читаться не будут.

**Отдельно у `book10` мусор в данных:** quality записан как
`"Age Rating Mature [customers do not accept this as fantasy]"` — редакторская пометка попала в
значение тега. Это значение участвует в матчинге запросов, так что его надо вычистить, а замечание
перенести в комментарий или в это файл.

### book42 — Ariel · Sylvia Plath

> Late poems written at four in the morning, brilliant and furious and entirely unwilling to be comforted. Slim, difficult, and the collection that changed what a poem was allowed to sound like.

Сборник стихов помечен как `Non Fiction`. Описание честное, но тег неверен — поэзия не документальная
литература.
