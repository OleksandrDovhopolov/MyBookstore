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

## Done

### ANL-1 — Minimal Release Analytics (Firebase)

Статус: сделано и проверено на устройстве, коммит `e3cdd8e7`. **Дальнейшее расширение аналитики вне релизного scope** — новые события не добавляем.

Что сделано:
- Живой пайплайн вместо заглушки: `CompositeAnalyticsService` + `FirebaseAnalyticsProvider` забинжены через `RegisterGameAnalytics` в `BootstrapInstaller`; `NullAnalyticsService` удалён вместе с перекрывающейся перегрузкой `RegisterAnalytics`, из-за которой пайплайн молча оставался мёртвым. Три конфиг-SO (`AnalyticsConfig`, `AnalyticsRoutingConfig`, `AnalyticsMappingConfig`) подключены к `BootstrapInstaller.asset`.
- `AnalyticsStartupOperation` в `phase_technical_init` сразу после consent-гейта: поднимает Firebase через `CheckAndFixDependenciesAsync`, ставит `user_id` из `save.http.player_id.v1` (адаптер закрывает коллизию двух одноимённых `IPlayerIdentityProvider`), инициализирует пайплайн и шлёт `session_started`. Операция некритичная — аналитика никогда не блокирует загрузку.
- Firebase-провайдер регистрируется только под `#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`; в Editor работает только `debug`-провайдер.
- События: `session_started`, `day_started`, `day_completed`, `active_sale_completed`, `quest_started`, `quest_completed`, `character_discovered`, `location_unlocked`, `decor_changed` — плюс существовавший ранее `item_purchased`. Слушатели живут в сборке `Game.Bootstrap` (`Assets/Game/Core/Installers/Features/Analytics/`), поэтому ни один asmdef фич не пришлось менять.
- Пассивные продажи **намеренно не шлются по попытке**, а агрегируются в `day_completed` (`passive_sales_count`, `passive_misses`): их количество растёт вместе с дневным трафиком покупателей и забило бы сигнал.
- Бюджет параметров Firebase (жёсткий лимит 25/событие): общие параметры урезаны с 13 до 6, `_maxParameterCount` снижен 50 → 25. `day_completed` — самое тяжёлое событие — укладывается в 17.
- Экран согласия приведён в соответствие: честный текст вместо «we do not collect analytics», кнопки Accept/Decline, `ConsentPolicy.Version` поднят до 2 (старые записи согласия, выданные под прежним текстом, аннулированы).
- При Decline блокировка двойная: события отбрасываются по `CanSendAnalytics`, а `CompositeAnalyticsService.Initialize()` вообще не поднимает Firebase — значит манифестный `firebase_analytics_collection_enabled=false` остаётся в силе и не собираются даже автоматические события Firebase.

Аналитика смотрится в Firebase-проекте `mybookstore-13b53`. Для отладки на устройстве: `adb shell setprop debug.firebase.analytics.app com.bobak.mybookstore`, дальше DebugView.

### INF-8 — Force Construct `CharactersService`

Статус: сделано. `Bootstrap.cs` форс-конструирует `ICharactersService` через `Construct(...)` до `SaveDataLoadOperation`, save-хук регистрируется, `AfterLoadAsync` отрабатывает — Journal наполняется. Задача оставалась в списке по инерции; проверено при аудите аналитики.

### INF-13 — Close Default Admin Credentials And Public Swagger

Статус: сделано. Backend-аудит подтвердил, что Swagger включается только в `Development`, `/api/admin/*` закрыт Basic auth, `ADMIN_USER` / `ADMIN_PASS` не используются как креды базы, production без admin credentials падает на старте, а `admin` / `admin` явно запрещены вне `Development`.

Дополнительно проверено: в Railway установлены реальные admin credentials, не дефолтные `admin` / `admin`.

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
- Tutorial-аналитика: инфраструктура готова (ANL-1), `TutorialAnalyticsSteps` уже шлёт `tutorial_checkpoint`. Расширять не нужно.
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

### INF-9 — Replace Manual Save-Hook Force Construction

Источник: [TODO.md → INF-9](TODO.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md).

Что сделать:
- Ввести единый `SaveHookBootstrapper : IStartable`.
- Регистрировать save-aware сервисы как `ISaveHook`.
- Убрать `save.RegisterHook(this)` из конструкторов и мёртвые bootstrap injections, если это безопасно в рамках релиза.

Критичность: high. Блокирующая зависимость INF-8 уже закрыта (см. Done), так что задачу можно брать в любой момент. Это устраняет корневую причину похожих багов, но не должно раздувать релиз, если быстрый фикс достаточен.

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
- Настройки аналитики (отладочный лог и `environment`) вручную **не трогать** — они выводятся из типа сборки, см. REL-12 и [BUILD.md §5](BUILD.md).
- Проверить FTUE на чистой установке.
- Собрать APK и сделать smoke: старт, configs, active request, dialogue, FTUE/tutorial.
- Проверить на чистой установке оба пути согласия: Accept → события видны в Firebase DebugView; Decline → в логе нет ни одного `[Analytics] Sent`, в DebugView тишина.

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
- Добавить кнопку открытия Privacy & Terms.
- Не добавлять сложные графические настройки, аккаунты, cloud save UI или дополнительные toggles.

Тоггл отзыва согласия на аналитику **сознательно отложен** — вынесен в [Deferred](#deferred--сознательно-отложено), чтобы не потеряться.

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

Статус: код и UI готовы, включая Accept/Decline (см. ANL-1). Осталось только контентно-юридическое — своя страница, правильные ссылки и Data Safety (см. «Осталось сделать» в конце).

Что сделано:
- `ConsentGateOperation` показывает окно первого запуска в `phase_technical_init` — после `AddressablesUpdateOperation`, до `RemoteConfigInitOperation`, то есть до первого обращения к Firebase.
- `ConsentService` + `PlayerPrefsConsentStore` хранят решение в PlayerPrefs (`consent.*.v1`). PlayerPrefs, а не `ISaveService`, потому что решение нужно читать задолго до `SaveDataLoadOperation`.
- Дефолты — deny по всем категориям. Бамп `ConsentPolicy.Version` перепоказывает окно и до повторного согласия обнуляет ранее выданные флаги.
- `IInteractiveLoadingOperation` приостанавливает 60-секундный глобальный дедлайн загрузки, пока окно открыто, иначе игрок получал бы ложный экран «проверьте интернет».
- Android-манифест: `firebase_analytics_collection_enabled=false`, `firebase_crashlytics_collection_enabled=false`, `google_analytics_adid_collection_enabled=false`, `google_analytics_ssaid_collection_enabled=false`; `AD_ID` снимается через `tools:node="remove"`. Флаг аналитики остаётся `false` намеренно: сбор включается из кода (`SetAnalyticsCollectionEnabled(true)`) только после согласия, поэтому до решения игрока не собирается ничего.
- `PrivacyLinksBuildCheck` роняет сборку, если на `BootstrapInstaller` не задан https-URL политики.
- Префаб `Assets/Game/Features/Privacy/ConsentWindow.prefab` собран и заведён в Addressables-группу `UI` под адресом `ConsentWindow`. Окно открывается.
- `Tools/Privacy/Reset Consent` чистит только ключи `consent.*`, чтобы можно было перепроверять первый запуск, не сбрасывая звук и player id.

Согласие как согласие, а не уведомление (закрыто в ANL-1):
- Экран получил **Accept/Decline** и честный текст: аналитика собирается, advertising ID — нет. Прежняя формулировка «we do not collect analytics» и единственная кнопка Continue больше не соответствовали коду и были бы недействительным согласием по UK GDPR/PECR.
- `ConsentPolicy.Version` поднят до 2, поэтому записи согласия, выданные под старым текстом, аннулированы и игрок будет спрошен заново.
- Decline блокирует всё: события отбрасываются по `CanSendAnalytics`, а `CompositeAnalyticsService.Initialize()` не поднимает Firebase вообще — манифестный `firebase_analytics_collection_enabled=false` остаётся в силе, и не собираются даже автоматические события Firebase.

Остаётся незакрытым путь **отзыва** согласия — вынесен в DEF-1 и сознательно отложен. По UK GDPR отозвать согласие должно быть так же просто, как его дать; сейчас после решения на первом экране передумать нельзя.

Также сам экран будет переработан в REL-11: Terms и согласие на аналитику разделяются на одну кнопку Continue плюс отдельный переключатель.

Осталось сделать:

1. **Создать свою страницу Privacy + Terms.** Сейчас в `BootstrapInstaller.asset` прописаны чужие ссылки на `themergegames.com` — это домен другого проекта, и по ним игрок попадёт на политику чужого продукта. Нужна собственная публичная страница, покрывающая: кто разработчик и как с ним связаться; какие данные собираются (**анонимная геймплейная аналитика через Firebase Analytics** — прогресс по дням, квестам, локациям, покупки в игровом магазине; плюс save-данные на собственный сервер и `player_id` из `save.http.player_id.v1`; advertising ID не собирается, крашлитика выключена); зачем они нужны; третьи стороны (Firebase Analytics, Firebase Remote Config, Cloudflare R2 для Addressables, собственный config/save-сервер); сроки хранения; права пользователя и как запросить удаление данных.
2. **Поменять ссылки в `Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.asset`** — поля `_privacyPolicyUrl` и `_termsOfUseUrl`. Если одна страница покрывает оба документа, `_termsOfUseUrl` можно оставить пустым: `PrivacyLinkSettings.TermsOfUseUrl` сам падает обратно на privacy-ссылку.
3. **Обновить форму Data Safety в Google Play Console: теперь нужно декларировать сбор данных.** Раньше здесь стояло «ни advertising ID, ни сбора данных» — после ANL-1 это неверно. Декларировать: аналитика собирается, advertising ID не собирается, данные привязаны к сгенерированному идентификатору установки.

Важно про пункт 2: `PrivacyLinksBuildCheck` проверяет только что URL непустой и начинается с `https://`. Нынешние чужие ссылки эту проверку **проходят**, то есть автоматика от такой ошибки не защитит — сверять домен нужно глазами перед релизной сборкой.

Критичность: critical before store release — и выше, чем раньше: аналитика теперь реально собирается, поэтому расхождение между политикой, формой Data Safety и фактическим поведением стало настоящим, а не гипотетическим.

### REL-6 — App Signing

Что сделать:
- Подготовить keystore/signing config для Android.
- Убедиться, что release build подписывается корректно.
- Не коммитить секретные файлы и пароли; следовать [SERVICES/SECRETS.md](SERVICES/SECRETS.md).

Критичность: critical for APK/Play release.

### REL-7 — App Icons

Текущее состояние: в `ProjectSettings.asset` слоты иконок Android заведены, но **все пустые**
(`m_Textures: []` для всех трёх видов). Сборка получает дефолтную иконку Unity.

#### Что нарисовать — исходники

Нужно **четыре** исходных файла. Всё остальное Unity и Play Console получают из них.

| № | Файл | Размер | Формат | Назначение |
|---|---|---|---|---|
| 1 | `icon_adaptive_foreground` | 432×432 | PNG-24 **с альфой** | Передний слой adaptive-иконки Android (логотип/персонаж) |
| 2 | `icon_adaptive_background` | 432×432 | PNG-24 **без альфы** | Задний слой adaptive-иконки: сплошной цвет или простой паттерн, **без мелких деталей** |
| 3 | `icon_legacy` | 512×512 | PNG-32 | Legacy + Round слоты Android (старые лаунчеры). Готовая иконка целиком, со своим фоном |
| 4 | `icon_play_store` | 512×512 | PNG-32, ≤1 МБ | Иконка в листинге Google Play |

#### Safe zone для adaptive-иконки — главное правило

Adaptive-иконка это холст 108×108 dp, из которого лаунчер вырезает произвольную форму — круг,
squircle, квадрат со скруглением, каплю. Гарантированно видна только центральная область **66×66 dp**,
остальное может быть срезано маской и дополнительно уезжает при parallax-анимации.

В пересчёте на исходник 432×432 (это 4× от 108 dp):

- **весь холст** — 432×432 px;
- **гарантированно видимая зона** — центральный круг диаметром **264 px**;
- значимая графика (логотип, лицо персонажа, буквы) должна целиком помещаться в этот круг;
- в наружные 84 px с каждой стороны кладём только фон и «вылет» рисунка.

Типичная ошибка — нарисовать иконку впритык к краям 432×432: на круглой маске у неё срежет углы,
а на squircle-маске обрежет края.

#### Что подключить в Unity

`Project Settings → Player → Android → Icon`. Три группы слотов:

| Вид | Размеры, которые ждёт Unity | Что класть |
|---|---|---|
| **Adaptive** | 432, 324, 216, 162, 108, 81 | Foreground + Background из п.1 и п.2 |
| **Round** | 192, 144, 96, 72, 48, 36 | Файл из п.3 |
| **Legacy** | 192, 144, 96, 72, 48, 36 | Файл из п.3 |

Достаточно назначить **только самый большой размер в каждой группе** — Unity сам сожмёт остальные.
Заполнять все восемнадцать слотов вручную нужно, только если на мелких размерах логотип «замыливается»
и хочется отдельную упрощённую отрисовку.

#### Дополнительные ассеты для листинга Play (не иконки, но из того же брифа)

- **Feature graphic** — 1024×500 PNG/JPEG, без прозрачности. Обязателен для публикации.
- **Скриншоты телефона** — от 2 до 8 штук, портретные, 1080×1920 подойдут.

#### Проверка

- Собрать APK, поставить на устройство, посмотреть иконку в лаунчере.
- Проверить на круглой и на squircle-маске (в настройках лаунчера обычно переключается форма иконок) —
  логотип не должен обрезаться.
- Посмотреть иконку в списке приложений и в «Настройки → Приложения», где размер мельче.

Не расширять задачу до полного brand redesign.

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

### REL-11 — Split Terms Acceptance From Analytics Consent

Сейчас экран первого запуска смешивает две разные по смыслу вещи в одном решении: принятие Terms of Use (это договор — «прими или не пользуйся» здесь законно) и согласие на аналитику (это отдельная правовая категория, где нужен реальный выбор). Из-за слияния кнопки Accept/Decline получились равнозначными, хотя отказ от Terms и отказ от аналитики — разные вещи.

Что сделать:
- Переделать `ConsentWindow` на одну основную кнопку **Continue**, которая принимает Terms и закрывает окно.
- Согласие на аналитику вынести на этом же экране в отдельный переключатель/чекбокс рядом с текстом.
- Убрать кнопку Decline: её роль берёт на себя выключенный переключатель аналитики.
- Определить и зафиксировать дефолт переключателя (см. «Открытый вопрос» ниже).
- Вызывать `RecordDecision(analytics: <состояние тоггла>, attribution: false, personalizedAds: false)` вместо нынешних `AcceptAll()` / `RecordDecision(false, false, false)`.
- Переписать `BodyText`: отдельно про Terms, отдельно про аналитику и что она отключается тут же.
- Поднять `ConsentPolicy.Version` до 3 — формулировка и модель решения меняются, старые записи нужно аннулировать.

Что менять **не** нужно:
- `ConsentService.RecordDecision(analytics, attribution, personalizedAds)` уже принимает три независимых флага — API изначально спроектирован под покатегорийное согласие, сейчас используются только «всё true» и «всё false». Инфраструктуру дописывать не придётся.
- Двойная блокировка при отказе (`CanSendAnalytics` + guard в `CompositeAnalyticsService.Initialize()`) работает как есть и продолжит работать: выключенный тоггл даст ровно тот же путь, что нынешний Decline.
- `ConsentGateOperation` не трогать — он по-прежнему `isCritical: true` и ждёт закрытия окна.

Открытый вопрос, решить до реализации: дефолт переключателя. Включённый по умолчанию даёт заметно больше данных, но для ЕЭЗ/UK предвыбранное согласие не считается действительным. Выключенный по умолчанию безопаснее юридически и дешевле в поддержке. Решение зависит от географии релиза (страны распространения в Play Console) — если ЕЭЗ и UK из листинга исключены, расклад другой.

Критичность: high before store release. Не блокирует сборку APK, но должно быть закрыто до публикации вместе с REL-5.

### REL-12 — Turn Off Analytics Debug Logging For Release

Статус: **сделано**. Ручного шага перед сборкой нет — оба значения выводятся из типа сборки.

Зачем это понадобилось: при включённом флаге `DebugAnalyticsProvider` на **каждом** событии склеивает все параметры в строку и пишет её через `Debug.LogWarning`, а на Android запись в logcat синхронная. Самый тяжёлый момент — завершение дня, когда `day_completed` с десятью параметрами уходит одновременно с сохранением и анимациями результатов. Плюс `_environment: development` пометил бы весь релизный трафик как тестовый и испортил отчёты в Firebase.

Почему не ручной флаг: `DebugAnalyticsProvider.IsEnabled` завязан на `IsDebugLoggingEnabled`, поэтому простое выключение флага в ассете оставляло бы Editor вообще без включённых провайдеров (Firebase там не регистрируется из-за `#if`), и `CompositeAnalyticsService` начинал печатать `No enabled analytics providers.` на каждое событие. То есть ручной вариант делал Editor-лог не тише, а шумнее — и требовал не забыть переключить флаг обратно.

Как сделано:
- `AnalyticsBuildContext.IsDevelopmentBuild` (обёртка над `Debug.isDebugBuild`) — единая точка решения «dev или release» для обеих реализаций `IAnalyticsConfig`.
- `IsDebugLoggingEnabled` — сериализованный флаг может только **выключить** лог в dev-сборке, но не включить его в релизной.
- `Environment` — в dev-сборке берётся из поля, в релизной жёстко `production`.
- `DefaultAnalyticsConfig` зеркалит ту же логику: это фолбэк на случай, если SO не назначен в `BootstrapInstaller`, и забытое назначение не должно вернуть отладочный лог в релиз.
- Предупреждение `No enabled analytics providers.` печатается один раз, а не на каждое событие — в релизной Standalone-сборке провайдеров действительно ноль.
- `AnalyticsConfig.asset` не менялся: его значения теперь означают «настройки для dev-сборок».

Граница: Editor и Development Build — логи есть, `environment=development`. Обычная релизная сборка — логов нет, `environment=production`.

Критичность: medium. Закрыто.

### REL-13 — Loading Screen Art (первый экран)

Экран уже существует и работает: `Assets/Game/UI/LoadingScreen/LoadingScreen.prefab` + `LoadingScreenView`,
прогресс и описания фаз прокидывает `Bootstrap`. Задача про **арт и композицию**, а не про механику.

Это первое, что видит игрок после сплэша Unity, и он висит на экране заметное время — в логе с
устройства только `consent_gate` занял 21 секунду, плюс Addressables, Remote Config и загрузка сейва.

#### В каком разрешении делать

Канвас настроен как `Scale With Screen Size`, reference **1080×1920**, `Match = 0` (по ширине). Это значит:
ширина всегда ровно 1080 юнитов, а **высота «плавает» вместе с пропорциями устройства**.

| Устройство | Пропорции | Сколько юнитов по высоте получится |
|---|---|---|
| 1080×1920 | 9:16 | 1920 |
| 1080×2340 | 19.5:9 | 2340 |
| 1080×2400 | 20:9 | 2400 |
| Планшет 1536×2048 | 3:4 | **1440** — меньше, чем reference |

Отсюда правила для фона:

- **Рисовать фон 1080×2400**, а не 1080×1920. На современных вытянутых телефонах низ и верх разъедутся,
  и картинки 1920 просто не хватит — вылезет пустота.
- **Композиционно значимое держать в центральной полосе 1080×1440.** Это то, что видно и на вытянутом
  телефоне, и на планшете 3:4, где по высоте доступно всего 1440 юнитов.
- Логотип, прогресс-бар и текст фазы якорить к центру, а не к верхнему/нижнему краю — при якоре к краю
  на разных устройствах они будут гулять относительно фона.
- Верхние и нижние ~240 px исходника считать «вылетом»: там может быть только продолжение фона.

#### Safe area

`androidRenderOutsideSafeArea = 1`, то есть игра рисует под чёлку и home-indicator. В проекте уже есть
`SafeAreaLayout` (`Assets/Game/Infrastructure/UIShared/SafeAreaLayout/`) и зафиксированное правило из
[SAFE_AREA_FULLBLEED_OPTIONS.md](improvements/SAFE_AREA_FULLBLEED_OPTIONS.md): фон идёт во весь экран,
а интерактив и текст остаются в safe area. Для загрузочного экрана это значит — фон full-bleed,
а логотип и прогресс-бар внутри safe area.

#### Что подготовить

| Ассет | Размер | Примечание |
|---|---|---|
| Фон загрузочного экрана | 1080×2400 | Значимое — в центральной полосе 1080×1440 |
| Логотип игры | ~800 px по ширине, PNG с альфой | Ложится поверх фона |
| Плашка/заливка прогресс-бара | по вкусу дизайна | Можно 9-slice, чтобы не тянуть пиксели |

#### Связанное: сплэш Unity

`m_ShowUnitySplashScreen: 1` — перед вашим экраном показывается «Made with Unity». На Unity Personal
он обязателен и отключить его нельзя; на Plus/Pro выключается в `Player → Splash Image`. Если остаётся,
имеет смысл добавить свой логотип в `Splash Screen Logos`, чтобы переход к загрузочному экрану
не выглядел рваным. Решить до финальной сборки.

#### Проверка

Посмотреть на трёх пропорциях — 16:9, 20:9 и планшетных 3:4 (в Editor через Game view resolutions):
фон нигде не обрывается, логотип и прогресс-бар не наезжают на чёлку и не уходят за край.

Критичность: high. Это первый экран продукта и он висит долго; дефолтный вид читается как незаконченная игра.

## Deferred — сознательно отложено

Задачи, которые осознанно вынесены из текущего релизного scope, но не отменены. Держим здесь, чтобы не потерялись и чтобы не всплывали заново в Part 1/Part 2.

### DEF-1 — Analytics Consent Withdrawal Toggle

Тоггл отзыва согласия на аналитику в настройках. Изначально был частью REL-2, вынесен отдельно — **сейчас делать не будем**.

Что потребуется, когда возьмём:
- Переключатель в окне настроек, дёргающий `IAnalyticsConsentService.SetAnalyticsConsent(bool)` — метод уже существует, `ConsentService` его реализует.
- Учесть, что выключение посреди сессии не гасит уже поднятый Firebase: `SetAnalyticsCollectionEnabled(true)` вызывается один раз в `FirebaseAnalyticsProvider.Initialize()`. Отзыв должен либо дополнительно звать `SetAnalyticsCollectionEnabled(false)`, либо применяться со следующего запуска — это нужно решить явно, иначе тоггл будет наполовину декоративным.

Почему это не выброшено насовсем: UK GDPR требует, чтобы отозвать согласие было так же просто, как его дать. Пока согласие выдаётся на первом экране и отозвать его нельзя ничем, кроме переустановки, симметрия нарушена. Риск принят осознанно; если игра пойдёт в ЕЭЗ/UK, задачу нужно вернуть в релизный scope и пересмотреть вместе с REL-11.

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
