# Release Tasks

Цель этого файла — довести текущий проект до релиза в его текущем состоянии: завершить критичные куски, наполнить контентом, собрать APK и опубликовать. Это не backlog для новых идей и не место для расширения игры.

Старый рабочий список: [TODO.md](TODO.md). После утверждения и переноса задач сюда старый TODO нужно будет почистить, чтобы не держать два конкурирующих источника правды.

## Scope Rule

Перед добавлением любой новой задачи в этот файл нужно явно оценить её критичность прямо сейчас.

Допускаются:
- баги;
- критические проблемы;
- блокеры прохождения;
- блокеры сборки, публикации, безопасности, first-run/FTUE;
- минимальные задачи по контенту, без которых текущая игра не имеет завершённого релизного пути.

Не допускаются без отдельного решения:
- новые фичи;
- расширение механик сверх уже начатого;
- UI-polish, который не блокирует понимание или прохождение;
- архитектурные улучшения "на будущее";
- идеи, которые делают игру лучше, но увеличивают release scope.

Главное правило: если задача не помогает завершить, наполнить и зарелизить текущую игру, она не попадает в этот файл.

## Part 1 — From TODO / Existing Docs

### GAME-2 — Finish `Game.Quest` Slice

Источник: [TODO.md → GAME-2](TODO.md).

Что сделать:
- Собрать реальную цепочку квестов на боевом конфиге вместо заглушки.
- Доделать `JournalWindow` в части, необходимой для отображения текущих квестов/персонажей/прогресса.
- Сверить поведение квестов с [adr/0007-quest-system.md](adr/0007-quest-system.md).
- Награды и permanent effects оставить в GAME-3, если без них нельзя завершить релизный путь.

Критичность: critical. Без квестового слайса прогрессия и контентная дуга не ощущаются завершёнными.

### GAME-7 — Resolve `SelectedBookIds` vs `ShelfBookIds`

Источник: [TODO.md → GAME-7](TODO.md).

Что сделать:
- Зафиксировать источник правды для фаз: `preparation.session.SelectedBookIds` как выбор подготовки, `book_sell.shelf_state.ShelfBookIds` как живое состояние полки продаж.
- Проверить resume-сценарии: релонч в Preparation, релонч в Sales, failed location entry, продолжение после продаж.
- Если оба модуля остаются, описать контракт синхронизации между preparation и shelf state.
- Добавить/обновить тесты на рассинхрон, продажу книги, новый день и повторный вход в Preparation.

Критичность: high. Это риск рассинхрона сейва и полки, особенно перед APK/smoke.

### GAME-10 — Finish Tutorial Release Slice

Источник: [TODO.md → GAME-10](TODO.md), [INPROGRESS/TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md).

Что сделать:
- Добавить debug/cheat поддержку: list, force-run, force-complete, reset, replay Day 1 через сброс `ftue.*`.
- Добавить editor/EditMode validation для tutorial target ids, `TutorialTargetTag`, quest ids и `quests.json`.
- Добавить минимальную tutorial-аналитику: sequence start, step start, sequence complete.
- Закрыть устойчивость Day 1: корректный resume посреди дня и cancel-path.
- Проверить player-facing skip и pointer/highlight только там, где это нужно для релизного первого опыта.
- Убрать временную связность tutorial UI id из `GameplaySceneController`, если она создаёт риск поломки релиза.

Критичность: critical. Tutorial — первый контакт игрока с игрой; сломанный Day 1 будет выглядеть как сломанный продукт.

### GAME-17 — Validate Day Shelf vs Scripted Customer Scripts

Источник: [TODO.md → GAME-17](TODO.md), [INPROGRESS/TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md).

Что сделать:
- Проверить, что forced hit жанры из `customer_scripts.json` реально есть на полке нужного дня.
- Проверить, что forced miss жанры тоже есть на полке, если урок объясняет "книга была, но продажа не гарантирована".
- Сделать провал forced hit дефектом контента (`LogError` / failing validation), не тихим warning.
- Добавить EditMode-тест или editor-валидатор по всем scripted customer entries.

Критичность: critical. Рассинхрон ломает tutorial Day 1 и объяснение sale chance.

### GAME-22 — Define Primary Genre Contract

Источник: [TODO.md → GAME-22](TODO.md), [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md), [QUEST_FLOW.md](QUEST_FLOW.md).

Что сделать:
- Принять решение, что считается главным жанром книги: `genres[0]`, весь массив `genres`, или отдельное условие `primaryGenre`.
- Зафиксировать решение в [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md) и XML-doc у `BookConfig.PrimaryGenre`.
- Покрыть тестом книгу с двумя жанрами, чтобы active request и quest progress не разошлись.

Критичность: critical before multi-genre content. Сейчас у всех книг один жанр, но первая multi-genre книга может сломать квестовый прогресс.

### Quest Flow P1/P2 — Fix Impossible Kids / Fact Active Requests

Источник: [QUEST_FLOW.md → Registry P1/P2](QUEST_FLOW.md), [TODO.md](TODO.md).

Что сделать:
- Починить Kids-запросы, которые сейчас требуют `qualities contains "Fantasy"` и поэтому нерешаемы.
- Починить Fact-запросы, которые сейчас требуют `qualities contains "History"` и поэтому нерешаемы.
- Прогнать валидатор active requests и убедиться, что у каждого требуемого жанра есть решаемые книги.

Критичность: critical. Тара и Милли становятся непроходимыми, а за ними блокируются локации.

### Quest Flow P3 — Add `postcard` Source

Источник: [QUEST_FLOW.md → P3](QUEST_FLOW.md), [TODO.md](TODO.md).

Что сделать:
- Добавить реальный источник `postcard`, потому что Капитан требует 10 открыток.
- Выбрать минимальную релизную механику: например, выдача за завершение дня, магазин, квестовая награда или другой уже существующий канал.
- Проверить, что `haveItem postcard min 10` достижим без читов.

Критичность: critical. Без открыток Капитан непроходим и Рынок блокируется.

### Quest Flow P4 — Add `map` Source

Источник: [QUEST_FLOW.md → P4](QUEST_FLOW.md), [TODO.md](TODO.md).

Что сделать:
- Добавить реальный источник `map`, потому что Деревня требует `map` для unlock.
- Выбрать минимальный релизный источник: quest reward, shop lot или существующий reward flow.
- Проверить, что Деревня достижима без читов при выполнении остальных условий.

Критичность: critical for late progression. Без карты финальная локация недостижима.

### Quest Flow P5 — Fix `days.json` / Active Request Pace

Источник: [QUEST_FLOW.md → P5](QUEST_FLOW.md), [TODO.md](TODO.md).

Что сделать:
- Продлить/настроить `days.json` после дня 2.
- Проверить `activeRequestCount`, особенно дни с hard overrides и `applyModifiers: false`.
- Убедиться, что квесты с `activePickGenre` физически выполнимы в разумном темпе.

Критичность: critical. Это корень темпа прогрессии и причина, почему P1/P2 могут оставаться невыполнимыми даже после правки данных.

### Quest Flow P6 — Bind Captain To Port

Источник: [QUEST_FLOW.md → P6](QUEST_FLOW.md), [TODO.md](TODO.md).

Что сделать:
- Добавить или использовать location gate для `CustomerScriptConfig`, чтобы Капитан появлялся в Порту.
- Проверить, что `activationQuestId` и location условие не конфликтуют.
- Покрыть сценарий тестом или валидатором scripted customer config.

Критичность: high. Это не всегда технический блокер, но ломает дизайн цепочки и ожидание игрока.

### Journal — Add Memories Content And Order

Источник: [INPROGRESS/JOURNAL_WINDOW.md](INPROGRESS/JOURNAL_WINDOW.md), [TODO.md](TODO.md).

Что сделать:
- Добавить memories в `characters.json`: `id`, `titleKey`, `descriptionKey`, `photoKey`, связь с `questId` или `questChainId`.
- Добавить способ сортировки: рекомендовано `order` в `CharacterMemoryConfig` для первого релизного захода.
- Добавить плоский read-model для memories, если вкладка Memories нужна в релизном журнале.
- Не делать вынос memories в отдельный конфиг сейчас, если нет явной причины.

Критичность: high. Без контента вкладка memories пустая, а журнал не даёт ощущение прогресса.

### INF-4 — Localization

Источник: [TODO.md → INF-4](TODO.md), [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md), [INPROGRESS/JOURNAL_WINDOW.md](INPROGRESS/JOURNAL_WINDOW.md).

Что сделать:
- Добавить минимальный слой локализации.
- На релизном этапе нужен только английский язык.
- Завести localization configs/tables, чтобы UI не показывал ключи вроде `character.eddi.name`.
- Перевести player-facing строки, которые сейчас захардкожены или лежат как raw keys.

Критичность: high. Для первого APK можно ограничиться English-only, но показывать ключи игроку нельзя.

### INF-6 — Move Save To Infrastructure + Versioning

Источник: [TODO.md → INF-6](TODO.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md).

Что сделать:
- Централизовать `ISaveService` в инфраструктурном слое.
- Зафиксировать схему версионирования и миграции save-модулей.
- Проверить, что текущие feature save modules не ломаются при переносе.

Критичность: high. Это фундамент прогресса, но делать аккуратно: если быстрый релиз ближе, не расширять задачу сверх нужного.

### INF-8 — Force Construct `CharactersService`

Источник: [TODO.md → INF-8](TODO.md), [INPROGRESS/JOURNAL_WINDOW.md](INPROGRESS/JOURNAL_WINDOW.md).

Что сделать:
- Форс-конструировать `ICharactersService` на bootstrap до `SaveDataLoadOperation`.
- Убедиться, что `CharactersService` регистрирует save hook и `AfterLoadAsync` выполняется.
- Проверить, что Journal не пустой из-за позднего создания сервиса.

Критичность: critical. Это прямой bugfix, который может ломать персонажей, memories и Journal.

### INF-9 — Replace Manual Save-Hook Force Construction

Источник: [TODO.md → INF-9](TODO.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md).

Что сделать:
- Ввести единый `SaveHookBootstrapper : IStartable`.
- Регистрировать save-aware сервисы как `ISaveHook`.
- Убрать `save.RegisterHook(this)` из конструкторов и мёртвые bootstrap injections, если это безопасно в рамках релиза.

Критичность: high, но после INF-8. Это устраняет корневую причину похожих багов, но не должно раздувать релиз, если быстрый фикс достаточен.

### INF-13 — Close Default Admin Credentials And Public Swagger

Источник: [TODO.md → INF-13](TODO.md), [SERVICES/SECRETS.md](SERVICES/SECRETS.md), [SERVICES/CONFIG_SERVER_API.md](SERVICES/CONFIG_SERVER_API.md).

Что сделать:
- Убедиться, что `ADMIN_USER` / `ADMIN_PASS` — Basic-auth для `/api/admin/*`, а не креды базы.
- Убрать дефолтные `admin` / `admin` из окружения API.
- Закрыть публичный Swagger: только Development или тот же auth/gate.
- Проверить вручную, что старые креды не работают, новые работают, `/swagger` не раскрыт публично.

Критичность: critical before public backend. Это безопасность, не polish.

### Build Checklist For APK

Источник: [BUILD.md](BUILD.md), [TODO.md](TODO.md).

Что сделать:
- Разобраться с warning по `Assets/Configs/hard_requests.json`.
- Опубликовать/синхронизировать живые configs и прогнать `Sync Bundled Defaults to StreamingAssets`.
- Прогнать `Run Pre-Build Validation` и runtime validators через Play mode.
- Собрать Addressables.
- Проверить Firebase Android config.
- Проверить Android Player Settings: IL2CPP, ARM64, API level, keystore, scenes.
- Проверить `BootstrapInstaller.asset`: debug off, full loading on, tutorial settings, first-day path.
- Проверить FTUE на чистой установке.
- Собрать APK и сделать smoke: старт, configs, active request, dialogue, FTUE/tutorial.

Критичность: critical. Это релизный gate.

### Addressables LiveOps Bug

Источник: [SERVICES/ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [TODO.md](TODO.md).

Что сделать:
- Заменить hardcoded label `"Spring_Collection"` на динамический `scheduleItem.Id` или другой корректный runtime id.
- Проверить поведение, если addressables group отсутствует.
- Решить, входит ли CardCollection/liveops в релизный APK; если нет, явно выключить/изолировать путь.

Критичность: high if feature ships, medium otherwise. Hardcoded debug label в релизе опасен.

## Part 2 — Release Additions

### REL-1 — Quest / Memory Appearance UI And Gameplay Button Attention

Что сделать:
- Добавить player-facing появление нового квеста и нового memory.
- Добавить анимацию в кнопку на `GameplayScene`, ведущую в Journal/Quest UI.
- Добавить восклицательный знак или другой индикатор "есть новое".
- Индикатор должен исчезать после просмотра соответствующего нового элемента.

Критичность: high. Это не новая механика, а видимость уже существующей прогрессии.

### REL-2 — Settings: Sound On / Off Only

Что сделать:
- Добавить минимальные настройки.
- В релизный scope входит только включение/выключение звука.
- Добавить тоггл отзыва согласия на аналитику и кнопку открытия Privacy & Terms — UK GDPR требует, чтобы отозвать согласие было так же легко, как дать. См. REL-5, где отзыв сознательно оставлен вне scope.
- Не добавлять сложные графические настройки, аккаунты, cloud save UI или дополнительные toggles.

Критичность: medium. Желательно для APK, но не должно расширяться.

### REL-3 — English-Only Localization Configs

Что сделать:
- Добавить конфиги локализации только для английского языка.
- Перенести player-facing строки в localization configs.
- Сохранить возможность будущего добавления языков без переделки UI.

Критичность: high. Связано с INF-4, но зафиксировано как релизный минимальный scope: English only.

### REL-4 — Register Google Play Developer Account

Что сделать:
- Зарегистрировать аккаунт разработчика Google Play.
- Подготовить доступы, payment/profile данные и всё, что нужно для публикации.
- Зафиксировать, какие данные/ассеты нужны для store listing.

Критичность: critical for release. Это внешняя задача, без неё публикация в Google Play невозможна.

### REL-5 — GDPR Consent On First Launch

Статус: код и UI готовы. Осталось только контентно-юридическое — своя страница и правильные ссылки (см. «Осталось сделать» в конце).

Что сделано:
- `ConsentGateOperation` показывает окно первого запуска в `phase_technical_init` — после `AddressablesUpdateOperation`, до `RemoteConfigInitOperation`, то есть до первого обращения к Firebase.
- `ConsentService` + `PlayerPrefsConsentStore` хранят решение в PlayerPrefs (`consent.*.v1`). PlayerPrefs, а не `ISaveService`, потому что решение нужно читать задолго до `SaveDataLoadOperation`.
- Дефолты — deny по всем категориям. Бамп `ConsentPolicy.Version` перепоказывает окно и до повторного согласия обнуляет ранее выданные флаги.
- `IInteractiveLoadingOperation` приостанавливает 60-секундный глобальный дедлайн загрузки, пока окно открыто, иначе игрок получал бы ложный экран «проверьте интернет».
- Android-манифест: `firebase_analytics_collection_enabled=false`, `firebase_crashlytics_collection_enabled=false`, `google_analytics_adid_collection_enabled=false`, `google_analytics_ssaid_collection_enabled=false`; `AD_ID` снимается через `tools:node="remove"`.
- `PrivacyLinksBuildCheck` роняет сборку, если на `BootstrapInstaller` не задан https-URL политики.
- Префаб `Assets/Game/Features/Privacy/ConsentWindow.prefab` собран и заведён в Addressables-группу `UI` под адресом `ConsentWindow`. Окно открывается.
- `Tools/Privacy/Reset Consent` чистит только ключи `consent.*`, чтобы можно было перепроверять первый запуск, не сбрасывая звук и player id.

Границы этого слайса:
- Релиз не собирает аналитику вообще (`NullAnalyticsService` остаётся забинденным), поэтому экран сформулирован как privacy notice + подтверждение Terms, а не как согласие на аналитику.
- Одна кнопка Continue без Decline и без пути отзыва — валидное уведомление, но **не** валидное согласие по UK GDPR/PECR. Как только сбор включат, экран обязан получить Accept/Decline, а REL-2 — тоггл отзыва.

Осталось сделать:

1. **Создать свою страницу Privacy + Terms.** Сейчас в `BootstrapInstaller.asset` прописаны чужие ссылки на `themergegames.com` — это домен другого проекта, и по ним игрок попадёт на политику чужого продукта. Нужна собственная публичная страница, покрывающая: кто разработчик и как с ним связаться; какие данные собираются (аналитики и крашей в этом релизе нет — сбор выключен в манифесте; наружу уходят только save-данные на собственный сервер и `player_id` из `save.http.player_id.v1`); зачем они нужны; третьи стороны (Firebase Remote Config, Cloudflare R2 для Addressables, собственный config/save-сервер); сроки хранения; права пользователя и как запросить удаление данных.
2. **Поменять ссылки в `Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.asset`** — поля `_privacyPolicyUrl` и `_termsOfUseUrl`. Если одна страница покрывает оба документа, `_termsOfUseUrl` можно оставить пустым: `PrivacyLinkSettings.TermsOfUseUrl` сам падает обратно на privacy-ссылку.
3. Обновить форму Data Safety в Google Play Console: ни advertising ID, ни сбора данных.

Важно про пункт 2: `PrivacyLinksBuildCheck` проверяет только что URL непустой и начинается с `https://`. Нынешние чужие ссылки эту проверку **проходят**, то есть автоматика от такой ошибки не защитит — сверять домен нужно глазами перед релизной сборкой.

Критичность: critical before store release. Особенно если есть analytics/ads.

### REL-6 — App Signing

Что сделать:
- Подготовить keystore/signing config для Android.
- Убедиться, что release build подписывается корректно.
- Не коммитить секретные файлы и пароли; следовать [SERVICES/SECRETS.md](SERVICES/SECRETS.md).

Критичность: critical for APK/Play release.

### REL-7 — App Icons

Что сделать:
- Подготовить и подключить иконки приложения для Android.
- Проверить вид на launcher и в build settings.
- Не расширять задачу до полного brand redesign.

Критичность: high. Без нормальной иконки релиз выглядит незавершённым.

### REL-8 — Add Sounds

Что сделать:
- Добавить минимальный набор звуков в проект.
- Приоритет: UI clicks, quest/memory notification, purchase/sale feedback, error/blocked action.
- Подключить через существующий audio layer, без расширения до большой audio-системы.

Критичность: medium. Важно для ощущения продукта, но scope должен быть минимальным.

### REL-9 — Finish Decorations Release UX

Что сделать:
- Принять решение: расширить текущую сцену выбора декораций или заменить на простое UI окно включения/выключения предметов.
- Для релиза выбрать самый короткий путь, который позволяет игроку понятно включать/выключать owned decor.
- Не добавлять новые decor-механики сверх уже существующих modifiers/ownership/placement, если они не блокируют релиз.

Критичность: high. Декор уже влияет на прогрессию/магазин, игроку нужен понятный способ им управлять.

### REL-10 — Configure Decor Shop Progression

Что сделать:
- Настроить магазин декораций так, чтобы не всё было доступно сразу.
- Определить минимальные unlock/availability правила для релизной прогрессии.
- Проверить, что нужные для прохождения предметы доступны вовремя, а late-game decor не ломает баланс.

Критичность: high. Сейчас "всё доступно сразу" ломает progression pacing.

## Explicitly Not Release Scope Unless Reclassified

Эти типы задач не добавлять сюда автоматически:
- новые системы монетизации;
- server-driven shop catalog, если локальный shop достаточен для первого релиза;
- новые локации сверх уже запланированного контентного пути;
- новые типы покупателей/архетипы;
- расширенная A/B infrastructure;
- full UI redesign;
- сложная multi-language локализация;
- долгие архитектурные рефакторы без прямого релизного риска.

Если такая задача всё же кажется нужной, сначала записать причину, почему она стала blocker/critical именно сейчас.
