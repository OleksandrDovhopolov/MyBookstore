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

### QA-3 — First Load Buttons Flicker

Статус: сделано. Мигания кнопок при первой загрузке больше нет.

Что проверено:
- UI на первом запуске/первой загрузке показывает стабильное состояние.
- Кнопки не появляются кратко в неправильном active/interactable состоянии.

Критичность была medium: это визуальный polish первого впечатления.

### CONTENT-1 — Replace Copied Book Localization Texts

Статус: сделано. Описания книг переписаны и применены; работа закрыта серией последних коммитов
`ff3d732`, `a3ff153`, `f26bfb2`, `2d6db07`, `7aab379` и материалами в `docs/content`.

Что сделано:
- Все 620 каталожных Real-книг переписаны батчами и применены к `Assets/Configs/localization_books_en.json`.
- Bundled defaults синхронизированы с `Assets/StreamingAssets/Configs/localization_books_en.json`.
- В `docs/content` сохранены стиль-гайд, seed-архив исходного текста, рабочие черновики и batch reports
  `REWRITE_REPORT_batch_01..16`.
- Fake-книги не переписывались: они остаются отдельным контентным вопросом, потому что относятся к чужому
  сеттингу и не должны использоваться как релизный контент.
- Проверочный контур держит результат: валидаторы/тесты сверяют черновики, seed-архив, применённую
  локализацию и generated rewrite reports.

Критичность была high: задача убирает copied book descriptions из релизного player-facing контента.

### REL-6 — App Signing

Статус: сделано. Release-сборка подписывается собственным keystore; проверено вручную в Unity.

Что сделано:
- Создан release keystore `user.keystore` **вне репозитория** (`G:\MyBookstore\Key\user.keystore`), в git не
  попадает — по правилу [SERVICES/SECRETS.md](SERVICES/SECRETS.md).
- В `Project Settings → Player → Publishing Settings` подключён Custom Keystore с alias; проверено, что release
  build подписывается этим ключом, а не debug-ключом.
- Пароли keystore/key, alias и сам ключ (в виде base64) сохранены в менеджере паролей Bitwarden как единственный
  читаемый бэкап — GitHub Secrets значения обратно не отдаёт.
- Секретные файлы и пароли в репозиторий не коммитятся.

Осознанно вынесено:
- **Google Play App Signing** пока не включаем — решение отложено до момента публикации в Play Console (REL-4).
  Сейчас keystore выступает как полноценный app-signing ключ; при включении App Signing он станет upload-ключом.
- **Заливка секретов в GitHub Actions Secrets для CI-подписи** отложена в [DEF-5](#def-5--ci-apk-signing-via-github-secrets):
  пока APK собирается локально из Unity, CI-подпись не нужна.

Критичность была critical for APK/Play release.

### Build Checklist For APK

Статус: сделано на текущем этапе — dev-сборка APK успешно прошла через новое build-окно. Оставшиеся
device-side проверки (prod smoke / FTUE / consent) сознательно закрываются вместе с публикацией
([REL-4](#rel-4--register-google-play-developer-account)), а не блокируют сам APK.

Источник: [BUILD.md](BUILD.md), [TODO.md](TODO.md).

Что сделано:
- **Пайплайн сборки автоматизирован** editor-окном `Tools/Build/Release Build Window`
  ([ReleaseBuildWindow.cs](../Assets/Game/Core/Build/Editor/ReleaseBuildWindow.cs), коммит `75965646`
  + доработка versionCode). По кнопке Dev/Prod оно: переключает таргет на Android, ставит
  Development-флаг и APK/AAB, гоняет `SyncBundledDefaultsMenu.Sync()`, `PreBuildValidationGate.CollectErrors()`
  (fail-fast), `AddressableAssetSettings.BuildPlayerContent()`, затем `BuildPipeline.BuildPlayer()` по
  включённым сценам. Ручные шаги «синк configs / Run Pre-Build Validation / собрать Addressables /
  проверить Player Settings» больше не держатся в голове.
- **Dev vs Prod**: Dev — Development build + Unity debug-подпись; Prod — release build, форс IL2CPP + ARM64,
  подпись release keystore (пароль из session-поля или env `ANDROID_KEYSTORE_PASSWORD`, очищается после сборки).
- **versionCode**: авто-инкремент `bundleVersionCode` только для **Prod + AAB** (артефакт, который реально
  уходит в Play; Play отклоняет повторный код). Dev/APK номер не жгут. Ручной bump перед каждой заливкой в
  Play больше не нужен.
- **Billing safety — закрыто.** Firebase на бесплатном **Spark** (billing account не привязан, списать нельзя
  by design; не переходить на Blaze без нужды). Cloudflare R2 — Billing Budget Alert на **$10** на
  `bobakgamestudio@gmail.com`.
- **Privacy/Terms URL** — свой `https://mybookstore-legal.netlify.app/` в `BootstrapInstaller.asset`
  (одна страница покрывает Privacy + Terms через fallback `PrivacyLinkSettings`).
- **Аналитика dev/prod** выводится из типа сборки автоматически (REL-12), окно её не трогает.
- **Warning по `hard_requests.json`** — снят (legacy-файл удалён ранее).
- **Дубли permission в манифесте** (`INTERNET` / `ACCESS_NETWORK_STATE`) убраны из
  `Assets/Plugins/Android/AndroidManifest.xml` — Unity/Firebase добавляют их сами, Unity-мёрджер больше не варнит.

Оставшиеся ручные device-side проверки (к моменту публикации, вместе с REL-4):
- Собрать **Prod** APK/AAB и smoke на устройстве: старт, configs, active request, dialogue, FTUE/tutorial.
- FTUE на чистой установке.
- Оба пути согласия на чистой установке: Accept → события в Firebase DebugView; Decline → в логе ни одного
  `[Analytics] Sent`, в DebugView тишина.
- Подтвердить на реальном Prod-бинаре IL2CPP/ARM64 (dev-сборка их не форсит — возможен другой backend).

Критичность была critical: это релизный gate.

### REL-8 — Add Sounds

Статус: сделано.

Что сделано:
- Закрыта кодовая инфраструктура: `AudioCatalog`, музыка хаб/день, fade, fallback для UI-кликов/окон.
- Закрыты SFX-хуки для минимального релизного набора: покупки, blocked/error, unlock location, decor
  place/remove, пассивная продажа, excellent-рекомендация, journal badge notification, rewards popup,
  dialogue line, day completion reward и count-up золота.
- Настройки работают по правилу `Sound = Sfx + Ui`, `Music = Music + Ambient`.
- В редакторе назначены клипы в `AudioCatalog.asset`, кнопки и окна подключены к audio-компонентам.

Критичность была medium: звук важен для ощущения законченного продукта, но scope оставался минимальным.

### REL-9 — Finish Decorations Release UX

Статус: сделано, коммиты `907af61`, `a75defa`. Из двух путей выбран первый — доработка сценовой выкладки,
а не замена на UI-список: превью, подсветка целей, замена и анимации уже были написаны, список обесценил бы
их и превратил бы обустройство магазина в форму настроек.

Что сделано:
- Магазин сведён к 5 ячейкам под фактический каталог: 3 напольные (`cart_table_1/2/3`), 1 подвесная
  (`hang_main`), 1 настенная (`wall_main`). Раньше в `bookshops.json` было 8 слотов, из них 5 не имели
  якорей в префабе и были недостижимы, а третью подвесную ячейку нечем было заполнить в принципе —
  подвесного декора всего два, а предметы уникальны.
- В `DecorPlacementWindow.prefab` заведены все 5 якорей с корректными `_slotId` и прописаны в
  сериализованный массив `_slotAnchors` (без этого слот молча не рендерится).
- `preserveAspect` проставляется в коде при выдаче спрайта — декор больше не растягивается под бокс.
- Относительный размер: `DecorSizeVisualScale` (Small 0.6 / Medium 0.8 / Large 1.0) множится на авторенный
  бокс слота через `sizeDelta`, а не `localScale` — поэтому не конфликтует с анимацией появления.
- В UI начал учитываться `maxSize` ячейки: фильтр инвентаря и подсветка целей используют
  `IsDecorCompatibleWithSlot` (тип + размер) вместо проверки одного `PositionType`. Раньше сервис отбивал
  оверсайз через `SizeMismatch`, а UI об этом не знал и показывал недопустимые цели.
- Выбор другого предмета при занятой ячейке убирает панель слота целиком (`EnterReplacePreview` → `HideHud`).
- Защита от регрессий: `DecorConfigValidator.ValidateDecorSlotCoverage` ловит ячейку, которую нечем
  заполнить, и декор, которому не подходит ни одна ячейка; EditMode-тест
  `DecorPlacementWindowSlotAnchorTests` сверяет `_slotId` якорей с `bookshops.json` в обе стороны и
  ловит дубли/пустые id.

Критичность была high: декор влияет на прогрессию и магазин, игроку нужен понятный способ им управлять.

### BUG-3 — Captain Quest Opens With Missing Ship Addressable

Статус: сделано. Причина оказалась не в квесте: спрайты грузятся по id предмета, который напрямую
используется как Addressables-адрес, а у корабля адрес записи был `Ship` вместо `ship` — ключи
регистрозависимы.

Что сделано:
- Найден источник: `UiSpriteProvider.GetSpriteAsync(id)` вызывает `ProdAddressablesWrapper.LoadAsync<Sprite>(id)`,
  то есть id предмета и есть Addressables-адрес; весь декор-UI передаёт туда `decorId`.
- Адрес ассета `Ship.png` переименован в `ship` и совпал с id декора — `InvalidKeyException` в quest reward UI
  больше не воспроизводится.
- Попутно выяснилось, что `DecorConfig.IconAddress` не читался нигде в продакшн-коде: поле удалено из модели
  и из `decors.json`, фактический контракт «Addressables-адрес == id декора» зафиксирован комментарием в
  `DecorConfig`.
- Удалён декор `harper_castle_donation_box`, у которого вообще не было спрайта (белая карточка в магазине):
  убран из `decors.json`, из лота `newspaper_decor_harper_castle_donation_box` в `shop.json` и из
  `localization_items_en.json`; bundled defaults синхронизированы.
- Проверено, что `ship` выдаётся квестом `q_captain_postcards`, а не магазином, и что все 16 декораций
  остаются достижимыми (магазин или квест).

Не вошло в реализацию: config validation / regression test на невалидные sprite-address keys для quest UI.
Логичное место — строка в `PreBuildValidationGate.Validators` рядом с `BookBoxPoolValidator` и
`LocalizationKeyValidator`.

Критичность была high: ошибка появлялась на релизном quest flow и могла скрывать реальные проблемы UI.

### CONTENT-3 — Create Final Captain Sprite And Remove Old Placeholder

Статус: сделано, коммиты `839387a`, `c1323b1`.

Что сделано:
- Добавлен финальный спрайт капитана: `Characters/Captain.png` и `Characters/Avatar/Captain.png` в
  `Assets/Game/Features/Location/Sprites/`.
- В Addressables заведён адрес `captain_avatar`, поэтому аватар грузится общим `UiSpriteProvider` по ключу,
  как остальные портреты.
- Старый placeholder-спрайт заменён.

Критичность была medium: content polish для заметного персонажа релизного flow.

### REL-1 — Quest / Memory Appearance UI And Gameplay Button Attention

Статус: сделано.

Что сделано:
- Добавлено player-facing появление нового квеста и нового memory.
- Добавлена анимация в кнопку на `GameplayScene`, ведущую в Journal/Quest UI.
- Добавлен индикатор "есть новое" для новых quest/memory элементов.
- Индикатор исчезает после просмотра соответствующего нового элемента.

Критичность была high: это делает уже существующую прогрессию видимой для игрока.

### REL-14 — Fix Failing EditMode Tests

Статус: сделано. EditMode-тесты приведены в рабочее состояние перед релизной сборкой.

Что сделано:
- Зафиксирован и разобран список падавших EditMode-тестов.
- Исправлены причины падений: компиляционные расхождения тестовых фейков, устаревшие ожидания и реальные
  регрессии, если они были найдены.
- Красный тестовый прогон больше не маскирует новые поломки перед APK.

Критичность была high before release: EditMode-тесты остаются автоматической защитой логики, которую не
покрывает `PreBuildValidationGate`.

### REL-19 — Clean Redis From Old Project Data

Статус: сделано. Redis очищен от мусора старых проектов; в Redis остались только сейвы игроков.

Что сделано:
- Найден Redis instance/environment, который используется текущим проектом для save/config/backend данных.
- Проверены key prefixes/namespaces текущего проекта и старых проектов, чтобы не удалить рабочие данные.
- Удалены мусорные ключи старых проектов из Redis.
- После очистки проверено, что текущий проект не получает stale data от старых проектов.

Критичность была high: старые Redis-данные могли ломать тестирование, маскировать реальные save/config ошибки
и подмешивать состояние от прошлых проектов.

### REL-11 — Split Terms Acceptance From Analytics Consent

Статус: сделано.

Что сделано:
- First-run `ConsentWindow` больше не смешивает принятие Terms/Privacy и согласие на аналитику в одном
  бинарном `Accept/Decline` решении.
- Окно использует одну кнопку **Continue**: она принимает Terms/Privacy и закрывает consent gate.
- Согласие на аналитику вынесено в отдельный checked toggle `Send anonymous analytics`; выключенный toggle
  записывает `analytics: false`, но не мешает продолжить игру.
- `ConsentWindowController` пишет решение через
  `RecordDecision(analytics: View.AnalyticsConsent, attribution: false, personalizedAds: false)` и больше
  не использует `AcceptAll()` в first-run flow.
- `ConsentPolicy.Version` поднят до `3`, чтобы старые records версии `2` показали обновлённый экран ещё раз.
- Сервисный слой, `ConsentGateOperation` и double lockout в analytics pipeline не менялись.

Проверка:
- Тесты не запускались.
- Статически проверено, что старые `AcceptClick` / `DeclineClick` / `_acceptButton` / `_declineButton`
  больше не используются в `Assets/Game`.

Важно: analytics toggle оставлен включённым по умолчанию по принятому продуктному решению. Если релиз пойдёт
в ЕЭЗ/UK, этот default нужно пересмотреть вместе с GDPR/PECR риском.

### REL-5 — GDPR Consent On First Launch

Статус: сделано. First-launch consent flow готов; разделение Terms/Privacy и согласия на аналитику закрыто
в [REL-11](#rel-11--split-terms-acceptance-from-analytics-consent).

Что сделано:
- `ConsentGateOperation` показывает окно первого запуска в `phase_technical_init` до первого обращения к
  Firebase.
- `ConsentService` + `PlayerPrefsConsentStore` хранят решение в PlayerPrefs (`consent.*.v1`), чтобы consent
  читался до загрузки сейва.
- Дефолты consent-категорий — deny, а версия consent policy перепоказывает окно при изменении условий.
- Android-манифест запрещает автоматический сбор Firebase до явного решения игрока; `AD_ID` удалён.
- `PrivacyLinksBuildCheck` защищает сборку от пустой/non-https privacy-ссылки.
- `ConsentWindow` заведён в Addressables под адресом `ConsentWindow`, а `Tools/Privacy/Reset Consent`
  позволяет перепроверять первый запуск без сброса остальных PlayerPrefs.
- После REL-11 окно использует кнопку **Continue** для Terms/Privacy и отдельный toggle для аналитики.

Критичность была critical before store release: аналитика реально собирается только после consent, а первый
запуск больше не смешивает принятие Terms и согласие на аналитику в одно бинарное решение.

### REL-17 — Improve PreparationWindow Location Context UX

Статус: сделано.

Что сделано:
- В `PreparationWindow` добавлен блок demand-жанров выбранной локации на основе `LocationConfig.DemandGenres`.
- Для Preparation заведён отдельный `PreparationGenreIconView`; Journal UI-класс не переиспользуется между фичами.
- `PreparationWindowView` получил отдельный `UIListPool<PreparationGenreIconView>` для demand-иконок.
- `PreparationWindow` читает фактический `LocationId` после `StartOrResumeAsync`, рендерит demand-жанры и асинхронно догружает иконки через общий `IUiSpriteProvider`.
- Добавлены guard'ы для отсутствующих `_configs`, `_uiSprites`, prefab/parent у пула: окно не падает до ручной prefab-провязки.
- Ручная часть по prefab: item-префаб и `_demandGenrePool` в `PreparationWindow.prefab` провязываются в Unity Editor.

Проверка:
- Тесты не запускались.
- `Game.Preparation` успешно скомпилирован через Visual Studio MSBuild.

Критичность: medium. Это улучшает UX подготовки и помогает игроку принимать осмысленное решение перед стартом
дня продаж.

### REL-15 — Restyle QuestView Reward Claim Button

Статус: сделано.

Проблема: кнопка получения награды в `QuestView` визуально не подходила к стилю окна и выбивалась из
остального Journal/Quest UI.

Что сделано:
- Найдена view/prefab-кнопка, которая отвечает за claim награды в `QuestView`.
- Обновлён визуальный стиль кнопки так, чтобы она совпадала с текущим стилем Journal/Quest UI: размер,
  цвет, шрифт, иконка/текст, состояния normal/hover/pressed/disabled.
- Проверено, что кнопка всё ещё явно читается как основное действие для квеста в состоянии ReadyToAward.
- Проверены состояния: квест не готов к награде — кнопка скрыта/disabled как сейчас; квест готов —
  кнопка доступна; после claim — больше не предлагает получить награду.

Критичность: medium. Это visual polish, но на релизном Journal/Quest экране прежняя кнопка выглядела
незаконченно.

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

### INF-6 — Save Module Versioning Release Baseline

Статус: сделано. Полноценное чтение версий и миграции вынесены в [DEF-4](#def-4--save-module-version-reading--migrations);
для релизного baseline закрыт минимальный риск перед первым APK.

Что сделано:
- Все pre-release save module schema versions сброшены к `1`: `characters`, `quests`, `sales_stats`.
- `SaveService.CurrentSchemaVersion` оставлен `1`; `ConsentPolicy.Version` не тронут, потому что это PlayerPrefs
  policy для согласия, а не save-модуль.
- `SaveService.GetModuleAsync<T>` теперь мягко деградирует при битом/несовместимом module payload: пишет
  warning и возвращает `null`, чтобы caller поднял дефолт только этого модуля.
- Контракт сохранён: missing/null module возвращает `null`, `payload.Version` пока не сравнивается, чтение не
  мутирует save и не запускает миграции.
- Добавлены EditMode-тесты `SaveServiceTests` на broken payload, сохранность соседнего модуля и счастливый
  путь structured payload.

Ручной release step: перед APK всё ещё нужно стереть тестовые сейвы локально (`Tools → Save → Delete Save Files`)
и на сервере (`Tools → Save → Reset Player Server Save`). После релиза первый schema bump любого модуля нельзя
делать без DEF-4.

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

### INF-13 — Close Default Admin Credentials And Public Swagger

Статус: сделано. Backend-аудит подтвердил, что Swagger включается только в `Development`, `/api/admin/*` закрыт Basic auth, `ADMIN_USER` / `ADMIN_PASS` не используются как креды базы, production без admin credentials падает на старте, а `admin` / `admin` явно запрещены вне `Development`.

Дополнительно проверено: в Railway установлены реальные admin credentials, не дефолтные `admin` / `admin`.

### GAME-22 — Fix Genre Contract Divergence

Статус: сделано, коммит `6048122`. Активная продажа теперь фиксирует `SoldGenre` в момент `Excellent`-рекомендации:
берётся первое пересечение `ActiveRequestRuntime.RequiredGenres` с `BookConfig.Genres` в порядке запроса.
`SalesDayCommitService` передаёт этот жанр в `SaleContext`, а `SalesStatsService` считает `soldGenre` /
`activePickGenre` по атрибутированному жанру вместо безусловного `PrimaryGenre`.

Пассивная продажа осталась в прежнем контракте: используется `PassiveSaleEvent.ResolvedGenre`, а если
жанр продажи неизвестен, статистика fallback-ится на `BookConfig.PrimaryGenre`. Пассивная группировка
полки по `PrimaryGenre` сознательно не менялась — это отдельное балансовое решение.

### GAME-7 — Resolve `SelectedBookIds` vs `ShelfBookIds`

Статус: сделано. При аудите выяснилось, что почти всё уже было на месте, а часть документации устарела.

Как выяснилось на самом деле:
- **Источник правды зафиксирован**: [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md) описывает оба модуля и границы
  ответственности — Preparation confirm = input committed, Sales = provisional runtime buffer,
  Results = output committed.
- **Контракт синхронизации однонаправленный и уже реализован**: `PreparationSessionService.ConfirmAsync`
  пушит `SelectedBookIds` в `ISalesShelfStateService.SetShelfAsync`. Обратный поток ровно один — при сборе
  полки следующего дня непроданные книги читаются из `ShelfBookIds`.
- **Порядок confirm/entry уже исправлен**: afford-check → Confirm → списание входа → EnterLocation, плюс
  refund и восстановление Preparation на техническом сбое входа. SAVE_DAY_FLOW.md описывал это как
  нерешённую проблему — абзац обновлён, теперь помечен как implemented.

Что решено:
- **Потеря дня при выходе посреди Sales — намеренное поведение, подтверждено.** `BuildForDay` всегда
  собирает полку заново из `SelectedBookIds`, а `MarkSoldAsync` вызывается только в `SalesDayCommitService`
  на завершении дня. Ничего не остаётся закоммиченным наполовину, день переигрывается с чистого состояния.
  Зафиксировано в SAVE_DAY_FLOW.md → «Exit During LocationScene».
- **Тесты на resume-сценарии сознательно не добавлялись** — текущего состояния достаточно. Если начнём
  трогать defer-commit или порядок confirm/entry, тесты нужно будет завести до правок.

### JRN-1 — Memories Structure In `characters.json`

Статус: сделано. Инженерная часть закрыта: четыре story-персонажа получили по одной memory в
`characters.json`, `mem_owner_placeholder` удалён, сортировка Memories переведена в хронологию
`order` по возрастанию, а `CharacterMemoryReferenceValidator` добавлен в pre-build gate.

Тексты и фотографии остаются отдельно в [JRN-2](#jrn-2--memories-copy-and-photos): новые `titleKey` /
`descriptionKey` сейчас являются ключами будущей локализации, а `photoKey` рассчитан на будущие Addressables
assets и до их появления показывает fallback.

### REL-2 — Settings: Sound / Music + Privacy & Terms

Статус: сделано. Добавлены `SettingsWindowController` / `SettingsWindowView`, Sound/Music переключатели
через `UISwitch` и `IAudioService`, prefab `SettingsWindow`, Addressables address `SettingsWindow`,
HUD integration point и назначенная кнопка настроек на `GameplayScene`.

В окне настроек есть ссылка `Privacy Policy`, которая открывает `PrivacyLinkSettings.TermsOfUseUrl`.
Если отдельный Terms URL пустой, `PrivacyLinkSettings` fallback-ится на `PrivacyPolicyUrl`, поэтому одна
публичная страница может покрывать Privacy + Terms.

Что важно перед релизом: сама механика ссылки готова, но URL должен быть заменён на **мой Terms / Privacy**.
Сейчас в `BootstrapInstaller.asset` стоят тестовые чужие ссылки — это закрывается в REL-5 и обязательно
проверяется в Build Checklist перед APK.

Не вошло в REL-2:
- Тоггл отзыва согласия на аналитику сознательно отложен в [DEF-1](#def-1--analytics-consent-withdrawal-toggle).
- REL-11 закрыта отдельной задачей: экран первого запуска разделяет Terms acceptance и analytics consent.

### GAME-17 — Validate Day Shelf vs Scripted Customer Scripts

Статус: сделано, коммит `c1c1b1a`.

Корень проблемы оказался не в отсутствии проверок, а в **дублировании контракта**: жанры скриптованного
покупателя первого дня были захардкожены и в `customer_scripts.json`, и отдельно в C#
(`AddFirstByGenre("Fact")`, `AddFirstByGenre("Travel")`), причём связи между ними не было — правка конфига
молча ломала урок первого дня.

Что сделано:
- **Общий хелпер `CustomerScriptDayLookup`** в сборке `Configs` — `MatchesDay` и `PassiveGenresForDay`.
  Положен именно туда, потому что `GameplayUI` не ссылается на `Book.Sell`, а на `Configs` ссылаются обе.
- **Полка первого дня выводится из конфига**: `FirstDayEntryFlow` резервирует слот под каждый жанр из
  `passiveAttempts` дня 1. Хардкод убран.
- **Спавнер делегирует тот же хелпер** (`ScriptedCustomerSpawner.IsEligible` → `MatchesDay`), поэтому
  день-матчинг не может разойтись между двумя местами.
- **Forced miss покрыт заодно**: `PassiveGenresForDay` не фильтрует по `ForceHit`, поэтому жанр промаха
  тоже резервируется — урок «книга была, но продажа не гарантирована» обеспечен конструктивно.
- **Провал forced hit остаётся `LogError`** в `ScriptedPassivePurchaseResolver` — было и сохранено.
- **Тесты**: юнит на хелпер, контентный тест по обоим content-рутам (каждый жанр дня 1 имеет стартовый
  сток), тест полки, и регрессия на дрейф — `EnterAsync_WhenDayOneScriptGenreChanges_ReservesThatGenre`
  проверяет, что смена жанра в конфиге меняет резервируемый жанр. Ad-hoc третья копия day-1 матчинга в
  `CustomerScriptConfigDeserializationTests` заменена на хелпер.

Ограничение, зафиксированное осознанно: статически проверяется **только день 1** — его полка
детерминирована (FTUE-сид + правила пресета). Со дня 2 полку выбирает игрок, поэтому такие проверки там
невозможны. Сегодня это безопасно: `passiveAttempts` есть только у записей дня 1.

### Quest Flow P1/P2 — Fix Impossible Kids / Fact Active Requests

Источник: [QUEST_FLOW.md → Registry P1/P2](QUEST_FLOW.md).

Статус: сделано. Оказалось, что нерешаемые Kids/Fact-запросы жили только в legacy-файле `hard_requests.json`,
который рантайм **никогда не грузил** — активные запросы мапятся только на `sample_requests.json`
(`[ConfigFile("sample_requests")]`), а там Kids/Fact уже решаемы (Kids — 19 книг-победителей, Fact — 29 по
живому каталогу). То есть блокер был устранён ещё миграцией на `sample_requests.json`; эта задача закрыла
хвост — уборку legacy и защиту от регресса.

Что сделано:
- Удалён legacy `hard_requests.json` (из `Assets/Configs` и `Assets/StreamingAssets/Configs`, вычищен из
  `manifest.json`) и все ссылки на него в коде/доках (`LocalizationKeyValidator`, комментарии моделей,
  `QUEST_FLOW.md`, `ACTIVE_REQUEST_CONDITIONS.md`, `BUILD.md`, `TODO.md`).
- Добавлен регресс-тест `ActiveRequestSolvabilityTests` (`Book.Sell.Tests.Editor`): гоняет тот же
  `ActiveRequestValidator`, что и билд-гейт, по живому каталогу и требует, чтобы ни один активный запрос не
  был нерешаем и ни один жанр не «голодал»; отдельные кейсы прямо стерегут Kids и Fact.
- Проверено: валидатор по живому каталогу даёт 0 ошибок, `StarvedGenres` пуст; тестовая сборка компилируется
  без ошибок. Полный прогон Test Runner — при закрытом редакторе (сейчас Unity держал lock).

Осталось вне scope: `sample_requests.json` содержит по 2 Kids/Fact-запроса — для разнообразия их можно
дописать, но на проходимость квестов это не влияет.

### Quest Flow P3 — Add `postcard` Source

Источник: [QUEST_FLOW.md → P3](QUEST_FLOW.md).

Статус: сделано. Механика выдачи открыток уже была реализована; задача подтвердила это и закрыла статус.

Что сделано:
- Источник `postcard` — награда за завершение дня: `economy.json.dayCompletionRewards` (`postcard` ×1)
  начисляется в `SalesDayCommitService.GrantDayCompletionRewardsAsync` при коммите каждого завершённого дня.
- Идемпотентно: день в `CompletedDays` повторно не выдаёт награду.
- Покрыто тестами (`SalesDayCommitServiceTests`: `_GrantsDayCompletionRewards_Once`,
  `_AlreadyCompletedDay_DoesNotGrantDayRewards`, `_IgnoresInvalidDayRewardEntries`).
- `haveItem postcard 10` достижим за 10 завершённых дней без читов.

Связанное P9 закрыто отдельно: открытки списываются при сдаче квеста Капитана через `QuestConfig.Costs`.

### Quest Flow P4 — Add `map` Source

Источник: [QUEST_FLOW.md → P4](QUEST_FLOW.md).

Статус: сделано. Источник `map` уже добавлен; задача подтвердила дизайн-решение и закрыла статус.

Что сделано:
- `map` (quest_item) покупается в газете: лот `newspaper_quest_item_map` в `shop.json`
  (витрина `newspaper.consumables`, 200 gold, `Disposable` max 1) выдаёт `map` ×1.
- `map` гейтит Деревню: `loc_village.unlockCost` = `map ×1` + `fuel_canister ×15`
  (плюс условие `soldGenre Fantasy 150` и `soldGenre Kids 150`).
- Деревня достижима без читов при выполнении условий.

### Quest Flow P5 — Fix `days.json` / Active Request Pace

Источник: [QUEST_FLOW.md → P5](QUEST_FLOW.md).

Статус: сделано.

Что сделано:
- `days.json`: день 2 `activeRequestCount` `0 → 1` (день имеет `applyModifiers: false` → ровно 1 запрос).
  День 1 оставлен `0` (обучающий).
- `SalesTraffic.asset._defaultActiveRequestCount` `1 → 3` — значение для дней без своего `activeRequestCount`
  (день 3+). Рантайм читает дефолт из ассета (`SalesTrafficConfig.BuildSettings()`), поэтому правился ассет;
  C#-дефолты в `SalesTrafficConfig`/`SalesTrafficSettings` синхронизированы на 3 для консистентности.
- Итог расписания: день 1 = 0, день 2 = 1, день 3+ = 3 активных запроса/день — темп `activePickGenre`
  (Милли/Тара) стал реалистичным.

### Quest Flow P6 — Bind Captain To Port

Источник: [QUEST_FLOW.md → P6](QUEST_FLOW.md).

Статус: сделано.

Что сделано:
- В `CustomerScriptConfig` добавлено необязательное поле `LocationId` (location gate; null = любая локация).
- Скрипт `captain_quest_intro` в `customer_scripts.json` помечен `locationId: "loc_port"`.
- `ScriptedCustomerSpawner.IsEligible` получил независимый гейт: при заданном `LocationId` скрипт eligible
  только когда `setup.LocationId` совпадает. Обратная совместимость — скрипты без `LocationId` не изменились.
- Тесты `ScriptedCustomerSpawnerTests`: `LocationBoundScript_Spawns_OnlyAtMatchingLocation` и
  `LocationBoundScript_Skips_WhenDayRunsElsewhere`. Сборка тестов компилируется без ошибок.

### INF-4 — Localization

Статус: сделано. Добавлен лёгкий `Game.Localization` поверх текущего config pipeline без Unity Localization
package: `ILocalizationService`, `LocalizationService`, `LocalizationWarmupOperation`, `LocalizationLocator`
и prefab-компонент `LocalizedText` для `TMP_Text`.

Что сделано:
- Поддержан текущий релизный язык `en` и API под будущую смену языка: `CurrentLocale`, `LocaleChanged`, `SetLocale`.
- Добавлены плоские localization configs по доменам (`ui`, `dialogues`, `quests`, `characters`, `items`, `books`), которые проходят через существующий `Assets/Configs` / `StreamingAssets` pipeline.
- Player-facing поля в основных конфигах переведены на `*Key`; потребители UI и gameplay-экранов резолвят текст через `ILocalizationService`.
- Missing-key поведение единое: игра не падает, показывает ключ в формате ``[`key`]`` и пишет warning.
- Добавлены проверки ключей локализации и регрессионная защита от показа raw localization keys в основных UI/narrative surfaces.

Книги технически подключены через `localization_books_en.json`, но финальные тексты книг остаются отдельной
контентной задачей: текущие записи были импортированы как временные.

### REL-3 — English-Only Localization Configs

Статус: сделано. Релизный English-only слой конфигов подключён: игра использует только английский язык,
но структура файлов и API готовы к добавлению выбора языка в настройках позже.

Граница закрытия: задача закрывает технический релизный минимум English-only localization. Замена временных
книжных текстов не считается частью технической локализации и вынесена в отдельную release content task.

### GAME-2 — Finish `Game.Quest` Slice

Источник: [TODO.md → GAME-2](TODO.md), [adr/0007-quest-system.md](adr/0007-quest-system.md).

Статус: сделано. Слайс `Game.Quest` реализован и покрыт тестами; все четыре квеста проходимы (проверено
вручную). Блокеры проходимости (Quest Flow P1–P6, P9) закрыты отдельными задачами.

Что сделано (по подпунктам задачи):
- Реальная цепочка квестов на боевом конфиге: `QuestsService` грузит живой `quests.json` через
  `GetAll<QuestConfig>()`, 4 квеста (Эдди→Милли→Тара→Капитан) прошиты, заглушки нет.
- `JournalWindow` доделан: страницы квестов (с claim-flow награды), персонажей, воспоминаний, локаций, объектов.
- Поведение сверено с ADR-0007: квесты построены поверх `Game.Conditions`; есть юнит- и интеграционные тесты
  (`QuestsServiceTests`, `QuestsServiceSaveTests`, `QuestRewardGranterTests`, `QuestConditionsCompositionTests` и др.).
- Награды реализованы (`QuestClaimFlow` → `QuestRewardGranter`).

Границы / вынесено:
- **Permanent world-effects** (`QuestConfig.worldEffects`) не реализованы (`QuestsService` только сигналит) —
  осознанно оставлено в **GAME-3**, как и предусмотрено формулировкой GAME-2; контент их не использует
  (у всех квестов `worldEffects: []`).
- Описание награды в информ-виджете Journal пока показывает плейсхолдер-ключ
  `ui.journal.reward.description.placeholder`, когда у награды нет `DisplayName` — мелкий контентный хвост,
  на проходимость не влияет.
- Точность/темп попадания активных запросов под `activePickGenre` — вопрос тюнинга баланса, вне GAME-2.

### BUG-1 — Active Request Window Close/Cancel Buttons Stay Disabled

Статус: сделано.

Причина: окно `RecommendationMinigameWindow` — `keepInCache`, инстанс переиспользуется для каждого запроса.
При резолве/скипе `SetSelectionActionsInteractable(false)` гасил `SkipButton`/`ClearFocusButton`, а `OnShowStart`
их заново не включал (звал только `ClearSelection()`), поэтому состояние `interactable=false` протекало в
следующий показ.

Что сделано:
- В `OnShowStart` после `ClearSelection()` добавлен `SetSelectionActionsInteractable(true)` — при каждом показе
  Skip/ClearFocus снова активны; `RecommendButton` остаётся выключенным до выбора книги, временная блокировка
  во время обработки (`_resolutionPending`) не затронута.
- Файл: `Assets/Game/Features/BookSell/UI/Recommendation/RecommendationMinigameWindow.cs`. Компиляция `Book.Sell` — 0 ошибок.

Smoke: открыть active request → Skip/Close → открыть снова → обе кнопки доступны; во время резолва они
по-прежнему временно гаснут.

### DEBUG-1 — Active Request Book Parameters Debug Button

Статус: сделано.

Что сделано:
- В `RecommendationMinigameWindow` добавлен dev-only debug UI под `Debug.isDebugBuild`: на карточках книг
  появляется info-кнопка и метка `OK` / `NOT` по текущему active request.
- Info-кнопка открывает `ContentWidget` с названием книги, `genres` и `qualities`; клик по виджету закрывает его.
- Обычная витрина продаж не затронута: `BookCardView.Bind` сбрасывает debug UI в скрытое состояние, а
  включение происходит только из active request minigame.
- `IBookConditionRequestEvaluator` не расширялся; match считается через существующий `Evaluate(...).IsMatch`.
  Окно использует локальный stateless `BookConditionRequestEvaluator`, потому что window factory живёт в
  глобальном UI scope и не видит location-scoped BookSell-регистрации.

Проверка: прямая компиляция `Book.Sell` через Roslyn `csc` — 0 ошибок. Unity Editor batchmode не запускался,
потому что проект уже открыт в другом Unity-инстансе; тесты по задаче не запускались намеренно.

### BUG-2 — PreparationWindow Available Book Count Ignores Selected Books

Статус: сделано.

Причина: строка жанра `PreparationGenreRowView.Refresh()` печатала в лейбл «в наличии» полный owned-потолок
(`_available`) и не вычитала выставленное (`_quantity`), поэтому число не уменьшалось при добавлении книг на полку.

Что сделано:
- В `Refresh()` лейбл «в наличии» теперь показывает остаток `Mathf.Max(0, _available - _quantity)`; `_shelfCountLabel`
  = выбрано, их сумма = owned. Обновляется вживую (`StateChanged → SetState → Refresh`).
- Реальный inventory до confirm не трогается; `_available` остаётся потолком для клампа, «+» гаснет при остатке 0.
- Файл: `Assets/Game/Features/Preparation/UI/PreparationGenreRowView.cs`. Двойного счёта по мультижанровым книгам
  нет — доступность считается по `PrimaryGenre`.

Smoke: N владения, K на полке → «в наличии» N−K, больше N не даёт, снятие восстанавливает.

### REL-16 — Add Reset All Button To PreparationWindow

Статус: сделано (логика; кнопка в префабе `PreparationWindow` провязывается вручную).

Что сделано:
- `IPreparationSessionService.ResetAllAsync(ct)` + реализация в `PreparationSessionService`: очищает
  `GenreQuantities`, `UseExplicitSelectedBookIds=false`, `SelectedBookIds → пусто`, `PersistAsync`, `StateChanged`.
  Реальный inventory не трогается, день не подтверждается.
- `PreparationWindowView`: поле `_resetAllButton` + `ResetAllButton`.
- `PreparationWindow`: подписка/отписка в `OnInit`/`OnDispose` + `OnResetAllClicked → _session.ResetAllAsync`.
  После сброса `OnStateChanged` обновляет строки (0), счётчик `0/N` и Start (недоступен при 0).
- Обновлены два тест-фейка `IPreparationSessionService` (`FakePreparationSession`, `FakePreparation`).

Проверка: `Game.Preparation`, `Book.Sell.Tests.Editor`, `GameplayUI.Tests.Editor` — 0 ошибок.

### BUG-4 — Market Location Missing Sprite For SoldTotal Unlock Condition

Статус: сделано (fallback-спрайт назначается в префабе `LocationRowView` вручную).

Причина: у условия `soldTotal` `LocationListItemModel.ResolveSpriteId` возвращает `null` (это не жанр и не
`visitLocation`), поэтому `SpriteId` пустой; в `LocationRowView.LoadIconsAsync` пустой id пропускался
(`continue`), иконка скрывалась — слот пустой, без варнинга (`UiSpriteProvider` для пустого id ничего не грузит
и не логирует).

Что сделано:
- В `LocationRowView` добавлено сериализованное поле `_fallbackSprite` (назначается в префабе).
- `LoadIconsAsync` теперь ставит fallback везде, где спрайт не найден: пустой `SpriteId` условия/стоимости
  (напр. `soldTotal`) и промах загрузки location/condition/cost спрайта.
- Лог-варнинг добавляется только там, где его ещё нет — для пустого `SpriteId` (`UiSpriteProvider` уже
  логирует промах непустого id, дубля не делаем).
- Файл: `Assets/Game/Features/Location/UI/LocationRowView.cs`. Компиляция `Game.Location` — 0 ошибок.

Smoke: открыть Market до unlock — условие `soldTotal: 200` показывается с fallback-иконкой (после назначения
спрайта в префабе), в логе один осмысленный warning вместо пустого слота.

### REL-18 — Add Unlock Item Descriptions To LocationWindow

Статус: сделано.

Элемент требования разблокировки в `LocationWindow` теперь кликабельный и по клику открывает виджет с
подсказкой «где взять / как выполнить», вычисленной по реальным данным. Виджет — на общем `ContentWidget`-ядре,
без зависимостей на Shop/Inventory-фичи.

Что сделано:
- `LocationConditionItemView` стал кнопкой (`_button` + колбэк `Action<LocationRequirementRef, RectTransform>`);
  для cost-предмета передаётся itemId, для условия — reasonKey.
- Новый Location-виджет: `LocationRequirementInfoWidgetData` / `LocationRequirementInfoWidgetView` (+ префаб),
  показывается через `ContentWidgetController`; закрытие — close-кнопкой (`RequestClose`) и при закрытии окна.
- `LocationRequirementHintResolver`: источник предмета по реальным данным — скан `QuestConfig.Rewards` +
  `ShopConfig.RewardItems` → quest/shop/both/unknown; для условий — подсказка по типу (soldTotal/soldGenre/visitLocation).
- Новый режим размещения `ContentWidgetPlacementMode.VerticalOnly` — виджет строго сверху/снизу над кликнутым.
- Локализация: 12 ключей `location.req.*` в `localization_ui_en.json` (+ StreamingAssets-копия).
- Никаких ссылок на Shop/Inventory-фичи — `ShopConfig`/`QuestConfig` читаются как Configs-модели через `IConfigsService`.

Проверка: `Game.Location` — 0 ошибок; loc-ключи синхронизированы. Финальная вёрстка префабов виджета и Button
на элементе — в Editor.

### CONTENT-2 — Active Request Descriptions

Статус: сделано. Активный запрос показывает человеческий текст, собранный из его же `conditions`,
а не техническую debug-строку.

Что сделано:
- Композитор `LexiconActiveRequestTextComposer` (`Assets/Game/Features/BookSell/Services/`) собирает
  предложение из слотов opener / lead / core / constraint / exclusion / anchor.
- Лексикон `Assets/Configs/request_phrases.json` (57 записей, модель `RequestPhraseConfig`) хранит только
  loc-ключи; 83 строки `request.*` — в `localization_quests_en.json`. 38 терминов покрывают все листья 19 запросов.
- Сколько условий озвучивать, решает жёсткость запроса (число подходящих книг в каталоге): tight ≤ 5 —
  всё, medium 6–15 — без открытых числовых полос, loose > 15 — без числовых вовсе.
- Обёртка выбирается детерминированно по FNV-1a от `request.Id`: текст запроса стабилен, ничего не
  перекатывается после reload дня.
- `ActiveRequestRuntime.UseLocalizedDescriptions` и debug-fallback удалены; `BuildDebugText` остался
  инструментом `ActiveRequestValidator` и чит-панели.
- Контент: `req_anathem_01` — исключение `none: Female Author` заменено на `none: Gore` (непроизносимая
  формулировка). У `req_fact_01` сохранён ручной `descriptionKey` — полное название референсной книги не
  влезает в баббл. С остальных 18 запросов `descriptionKey` снят, чтобы override не перекрывал композитор.
- `LocalizationKeyValidator` проверяет `request_phrases.json` (поля `positiveKey` / `negativeKey`);
  `request_phrases.json` добавлен в манифест StreamingAssets.
- Тесты: `LexiconActiveRequestTextComposerTests` (юниты) и `ActiveRequestTextContentTests` (прогон живого
  контента: у каждого enabled-запроса читаемый текст без дампа условий и сырых loc-ключей).

Спека и фактический вывод по всем 19 запросам:
[docs/INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md](INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md).

Осознанно вынесено:
- `bookId` в `RequestDefinitionConfig` вместо свободной строки `BookTitle` + проверка якоря в
  `ActiveRequestValidator` — отдельной задачей.

## TODO

Единый список открытых релизных задач. Задачи, ожидающие внешние ресурсы, вынесены в
[Wait For Resources](#wait-for-resources) ниже.

### QA-1 — Fix Welcome Window

Что сделать:
- Проверить текущее состояние `WelcomeWindow` в первом запуске/возврате в игру.
- Исправить визуальные или UX-проблемы окна: layout, тексты, кнопки, safe area и поведение закрытия.
- Проверить smoke на чистой установке: окно выглядит корректно и не блокирует дальнейший flow.

Критичность: high. Это один из первых экранов игрока, он должен выглядеть финально.

### QA-2 — Fix Active Request Window Layout

Что сделать:
- Поправить вёрстку окна активного запроса: текст запроса, список книг, карточки, кнопки и debug/служебные элементы.
- Проверить разные длины текста и разные размеры/пропорции экрана.
- Убедиться, что элементы не перекрываются, кнопки доступны, а окно читается без ощущения временной сборки.

Критичность: high. Active request — центральная часть дневного gameplay loop.

### QA-4 — Gameplay Day Characters Overflow Screen

Баг: персонажи вылезают за экран во время игрового дня.

Что сделать:
- Проверить позиции/анимации покупателей и story-персонажей на разных aspect ratios.
- Исправить bounds/safe area/anchor logic так, чтобы персонажи не выходили за видимую область.
- Проверить smoke на широком телефоне, обычном 16:9 и планшетном соотношении.

Критичность: high. Персонажи за экраном выглядят как сломанный core gameplay.

### QA-5 — Multiple NPC Sprites And Request Bubble Portraits

Что сделать:
- Создать несколько спрайтов для NPC/покупателей, чтобы активные запросы не выглядели одинаково.
- Подключить выбор NPC-спрайта к данным запроса/покупателя.
- Отображать соответствующий NPC-спрайт в bubble активного запроса.
- Проверить fallback: если спрайт не задан или не загрузился, bubble показывает безопасную заглушку.

Критичность: medium. Это улучшает читаемость запросов и ощущение живого магазина.

### QA-6 — Add Success And Failure Sale Sounds

Что сделать:
- Добавить/подключить отдельный звук успешной продажи.
- Добавить/подключить отдельный звук неуспешной продажи или rejected recommendation.
- Проверить, что звуки уважают настройки `Sound` и не дублируются с существующими UI-click/reward sounds.

Критичность: medium. Это важная обратная связь в основном цикле продаж.

### QA-7 — Regenerate Shop And Start Game Button Sprites

Что сделать:
- Перегенерировать/заменить спрайты кнопки магазина и кнопки начала игры.
- Подключить новые ассеты в соответствующих prefab/UI.
- Проверить normal/pressed/disabled состояния и читаемость текста поверх кнопок.

Критичность: medium. Эти кнопки часто видны игроку и должны совпадать с финальным стилем.

### QA-8 — Analyze Location Start Price

Что сделать:
- Проанализировать цену стартовой локации: оставить `0` и скрыть текст цены или выставить реальные цены.
- Если цены выставляются, проверить, можно ли заблокировать игру состоянием, где игрок не может открыть
  нужную локацию и продолжить progression.
- Зафиксировать выбранное решение в конфигах и проверить LocationWindow/UI.

Критичность: high. Нельзя допустить softlock из-за стоимости локации.

### QA-9 — Fix Sale Probability Description Texts

Что сделать:
- Поправить тексты описания вероятности продажи.
- Проверить, что формулировки понятны игроку и соответствуют реальной логике/шансам.
- Синхронизировать локализацию и проверить UI, где эти тексты отображаются.

Критичность: medium. Непонятные probability-тексты ломают доверие к продаже.

### QA-10 — Restyle Dialogue Skip Button

Что сделать:
- Поменять кнопку `Skip` в окне диалога: визуальный стиль, размер, позиция и состояния.
- Проверить, что кнопка не выглядит как primary action и не конфликтует с основным чтением диалога.
- Поведение skip оставить прежним.

Критичность: medium. Это polish заметного story UI.

### GAME-23 — Active Requests Nobody Can Answer From Today's Shelf

Контекст: активные запросы валидируются только на **разрешимость в принципе** —
`ActiveRequestValidator` ругается, если запросу не подходит ни одна книга из 663. Но подходящих книг
у запроса может быть 2, а полка дня вмещает 30 слотов (`PreparationSessionService.DefaultDailyBookSlots`),
и набирается она из инвентаря игрока, а не из всего каталога. Вероятность, что нужная книга вообще
окажется на полке, для тонких запросов мизерная:

| Запрос | Подходящих книг | P(есть на полке), оценка сверху |
|---|---|---|
| `req_fantasy_02`, `req_fireupon_01` | 2 | ~9 % |
| `req_kids_02`, `req_scarlet_01`, `req_travel_02` | 3 | ~13 % |
| `req_classic_01` | 5 | ~21 % |
| `req_canterville_01` | 37 | ~83 % |

(оценка = случайная выборка 30 книг из 663; реальная ниже, потому что инвентарь игрока — подмножество каталога)

Усугубляет то, что выбор запроса полку **не видит**:
[`ProfileMatchedRequestSelector.Draw`](../Assets/Game/Features/BookSell/Services/Spawning/ProfileMatchedRequestSelector.cs)
фильтрует пул только по `MatchesProfile(profile.DesiredGenres)`. То есть покупателю может достаться
запрос, на который сегодня физически нечем ответить, — игроку остаётся Skip, и это читается как
несправедливость, а не как сложность.

Что сделать:
- **Основное — сделать выбор запроса shelf-aware.** Передать селектору полку дня и отбрасывать
  запросы, для которых на полке нет ни одной подходящей книги (`IBookConditionRequestEvaluator` уже
  под рукой). Fallback на текущее поведение + warning, если после фильтра пул пуст. Это чинит проблему
  без переписывания контента и не трогает ощущение сложности.
- **Порог в валидаторе.** Поднять планку с «≥ 1 книга» до warning'а ниже порога (ориентир — 8–10 книг,
  подобрать по факту) и добавить в отчёт `ActiveRequestValidationReport` оценку P(hit) при текущей
  вместимости полки. Ноль подходящих книг остаётся ошибкой.
- **Точечно ослабить самые тонкие запросы**, если после shelf-aware выбора они всё равно почти не
  выпадают: у `req_fantasy_02` / `req_fireupon_01` / `req_kids_02` / `req_scarlet_01` / `req_travel_02`
  расширить числовые полосы или убрать третье условие.
- **Перепроверить текст.** Композитор
  ([CONTENT-2](#content-2--active-request-descriptions)) выбирает многословность по числу подходящих
  книг: ослабление условий сдвинет запрос из бакета tight в medium и текст станет короче. Прогнать
  `ActiveRequestTextContentTests` и сверить §4 в
  [ACTIVE_REQUEST_TEXT_COMPOSER.md](INPROGRESS/ACTIVE_REQUEST_TEXT_COMPOSER.md).

Критичность: high. Не блокирует прохождение — игрок просто скипает запрос, — но активная продажа
это ядро лупа (10 gold за попадание) и единственный драйвер квестовых задач `activePickGenre`
(Millie, Tara). Запрос с ~9 % решаемости обесценивает и то и другое.

### QA-11 — Raise Active Request Success Probability

Что сделать:
- Повысить вероятность выполнения активных запросов, потому что сейчас они ощущаются слишком сложными.
- Согласовать решение с [GAME-23](#game-23--active-requests-nobody-can-answer-from-todays-shelf):
  shelf-aware выбор запроса, ослабление слишком узких условий и/или корректировка выдачи книг.
- Проверить, что active request остаётся задачей на выбор, но перестаёт выглядеть как случайная неудача.

Критичность: high. Active requests должны быть выполнимым ядром gameplay loop, а не постоянным Skip.

### QA-12 — Hide Already Sold Books In Active Request Window

Что сделать:
- Убирать из списка книги, которые уже проданы в текущий день, в окне активного запроса.
- Проверить, что такие книги нельзя выбрать повторно через UI и что логика не ломает текущий shelf/inventory state.
- Если все книги проданы/недоступны, показать корректное пустое состояние или оставить только доступные действия.

Критичность: high. Повторный показ уже проданных книг путает игрока и может ломать ожидания продажи.

### QA-13 — Hide Selected Book Close Button

Что сделать:
- Скрыть кнопку закрытия/сброса выбранной книги в active request UI.
- Сам функционал оставить доступным через существующий flow, если он нужен логике окна.
- Проверить, что скрытие кнопки не ломает выбор другой книги, skip/close окна и сброс состояния после продажи.

Критичность: medium. Нужно убрать лишний визуальный элемент без удаления полезной логики.

### REL-4 — Register Google Play Developer Account

Что сделать:
- Зарегистрировать аккаунт разработчика Google Play.
- Подготовить доступы, payment/profile данные и всё, что нужно для публикации.
- Зафиксировать, какие данные/ассеты нужны для store listing.

Критичность: critical for release. Это внешняя задача, без неё публикация в Google Play невозможна.

### JRN-3 — Memory Unlock Sources Beyond Quests

Продолжение [JRN-1](#jrn-1--memories-structure-in-charactersjson). Структура записей закрыта, но
**источники разблокировки memory жёстко ограничены квестами**, и это мешает авторить сюжетные
воспоминания на не-квестовые события.

Scope note: по [Scope Rule](#scope-rule) это архитектурное улучшение, а не блокер релиза — текущая игра
проходится и публикуется без него. Задача лежит в TODO как **решение, которое принимается после
закрытия остальных задач**: по остатку времени выбирается один из вариантов ниже, вплоть до «ничего не
делаем, оставляем вариант A».

#### Текущее состояние

`CharacterMemoryConfig` допускает ровно три источника, и `CharacterMemoryReferenceValidator` это
проверяет (`sourceCount != 1` → error):

| Источник | Как срабатывает | Кто исполняет |
|---|---|---|
| `unlockedAtStart` | один раз на первом запуске | `FtueBootstrapper.UnlockStartMemories()` |
| `questId` | квест переходит в `Awarded` | `CharacterModelFactory.IsUnlockedByQuest` |
| `questChainId` | финальный квест цепочки в `Awarded` | то же |

Read-model: `Unlocked = IsUnlockedByQuest(mc) || saved.UnlockedMemoryIds.Contains(mc.Id)`. Леджер в
save-модуле `"characters"` нужен для одноразовости события `MemoryUnlocked` и для ручного
`ICharactersService.TryUnlockMemory(characterId, memoryId)`.

#### Что именно не получается

1. **Несколько memory на одно событие.** Валидатор требует уникальности `questId` на memory
   (`reuses questId '...' already used by ...`). Одно событие → максимум одна memory. Исключение —
   `unlockedAtStart`: на него ограничения уникальности нет, поэтому «несколько memory на первом
   запуске» работает уже сегодня, просто конфигом.
2. **Memory на открытие локации.** Событие «локация открыта» не входит в три источника. В реестре
   условий нет leaf-условия `locationUnlocked`: есть `LocationIsCondition` (текущая локация) и
   `visitLocation` (счётчик входов из `LocationVisits`), а `LocationUnlockConditionBuilder` работает в
   обратную сторону — локация *потребляет* условия из `LocationConfig.Unlock`. Рантайм-сигнал при этом
   существует и не используется: `ILocationUnlockService.Unlocked` (`event Action<string>`).
3. **`unlockedAtStart` не бэкфиллится.** `FtueBootstrapper` выходит по маркеру `ftue.applied`. Новые
   стартовые memory, добавленные после релиза, существующим игрокам не откроются. Сейчас неважно
   (baseline v1, сейвы вайпаются), но перестанет быть неважным после публикации.
4. **Побочка с discovery.** `CharacterModelFactory.IsDiscoveredByQuest` считает персонажа открытым,
   если любой его memory-квест `!= Pending`. Технический квест-триггер раскрывает персонажа раньше
   сценария. Обходится паттерном `owner` (`hiddenInJournal: true`).

#### Варианты — выбрать один по остатку времени

**Вариант A — квесты-пустышки. 0 кода, только конфиг.**

Квест с `activationConditions` + один таск (например `visitLocation`), без `rewards`, memory вешается на
его `questId`. Работает, потому что квесты авто-авардятся: `QuestsService` — «Auto-award: completing all
tasks goes ReadyToAward → Awarded at once», ручной claim не нужен.

Цена: у `QuestConfig` нет флага hidden, поэтому технический квест виден игроку в списках квестов и
требует осмысленных `titleKey`/`descriptionKey`; одно событие = один квест = одна memory;
`visitLocation` срабатывает на первом *входе*, а не в момент разблокировки; персонаж раскрывается
раньше срока (лечится скрытым персонажем-контейнером).

**Вариант B — A + leaf-условие `locationUnlocked`. ~3 маленьких файла.**

По готовому шаблону `VisitLocationCondition` / `VisitLocationConditionFactory`: read-only seam над
`ILocationUnlockRepository`, фабрика с `TypeId`, регистрация в `ConditionsVContainerBindings`. В проекте
~11 условий сделаны одинаково, так что это шаблонная работа.

Даёт: точный триггер «локация открыта» вместо «зашёл в локацию», и условие переиспользуемо для любых
квестов и для гейтов других локаций. Не снимает пункты 1 и 4 — квест-пустышка всё ещё нужна.

**Вариант C — четвёртый источник `unlockCondition` в `CharacterMemoryConfig`. Средний рефактор внутри
одной фичи.**

Поле `JObject unlockCondition` по образцу `QuestConfig.ActivationConditions`, парсится через
`IConditionParser`, пересчёт по `IConditionChangeSource` (оба уже есть в `Game.Conditions.API`).

Затрагивает: `sourceCount` в валидаторе, `IsUnlockedByQuest` → `IsUnlockedByTrigger` в
`CharacterModelFactory`, реконсайл в `CharactersService`.

Даёт: «локация открыта», «есть предмет», «день ≥ N» авторятся прямо в `characters.json`, без квестов-
пустышек и без фантомов в UI. Снимает пункты 1, 2 и 4 сразу: ограничение уникальности `questId` можно
отпустить, потому что квест перестаёт быть единственным ключом.

**Вариант D — вынести memories в отдельный модуль `Game.Memories`. Полный рефактор.**

Фича уже наполовину отдельная: `memory.id` глобально уникальны (валидатор это требует), `SeenMemoryIds`
в `SavedCharacters` — плоский `HashSet<string>`, а не по персонажам, `JournalMemoriesViewModelBuilder`
обходит персонажей только чтобы развернуть их обратно в один список, и `TryUnlockMemory` уже публичный
API. Персонаж `owner` c `hiddenInJournal: true` — прямое признание, что memory нужен контейнер, а не
персонаж.

Состав: `memories.json` вместо вложенности в `CharacterConfig` (`characterId` становится опциональным
тегом «чья это memory» для People-таба); свой save-модуль `"memories"` — туда уезжают леджер и
`SeenMemoryIds`, модуль `"characters"` худеет до `Discovered`; подписка на `QuestStarted`/`QuestAwarded`
переезжает из `CharactersService` как есть; публичный `OpenMemory(memoryId)` без `characterId`.

Цена: `IsDiscoveredByQuest` придётся либо читать memories через `Game.Memories.API`, либо переводить
discovery целиком на `DiscoveryQuestIds` — это дизайн-решение, а не рефакторинг; появляется зависимость
`Game.Characters → Game.Memories.API` (не наоборот), сверить с [ASMDEF_RULES.md](ASMDEF_RULES.md);
плюс ещё один `SaveBacked*`-репозиторий, `ISaveHook` и биндинг.

Порядок внутри D, если берёмся: сначала конфиг (`memories.json` + `characterId` как тег), потом
save-модуль, и только потом `unlockCondition` — иначе валидатор переписывается дважды.

#### Окно для вариантов C и D

Save-миграция сейчас не нужна: `StateSchemaVersion = 1` — release baseline, пре-релизные сейвы
вайпаются ([INF-6](#inf-6--save-module-versioning-release-baseline)). После публикации любой перенос
леджера и `SeenMemoryIds` в другой модуль потребует настоящей миграции. То есть C и D дешевле всего
**до первого релиза**, дальше дорожают.

Что сделать:
- Дождаться закрытия остальных релизных задач и оценить остаток времени.
- Выбрать вариант: A (ничего не делаем сверх конфига), B, C или D.
- Если выбран A — дописать в `characters.json` нужные `unlockedAtStart` и квесты-триггеры, и решить,
  гасить ли бейдж unseen после FTUE через `MarkAllMemoriesSeen()`.
- Если выбран C или D — сделать до релиза, пока миграция сейва не нужна.
- Синхронизировать [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) с итоговой моделью (документ уже
  расходится с кодом: заявляет `StateSchemaVersion = 2` и миграцию v1→v2, в коде `= 1`).

Критичность: low для релиза, medium для дальнейшего контента. Блокером публикации не является: кейс
«несколько memory на первом запуске» закрывается конфигом уже сейчас, кейс «memory на открытие локации»
закрывается вариантом A без кода.

## Wait For Resources

Следующие задачи упираются во внешние ресурсы: арт, который нужно нарисовать, тексты, которые нужно
подготовить или заменить, и контентно-балансовые решения, которые нужно принять. Это не
[Deferred](#deferred--сознательно-отложено): задачи остаются в релизном scope, но ждут входные материалы.

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

### REL-10 — Configure Decor Shop Progression

Что сделать:
- Настроить магазин декораций так, чтобы не всё было доступно сразу.
- Определить минимальные unlock/availability правила для релизной прогрессии.
- Проверить, что нужные для прохождения предметы доступны вовремя, а late-game decor не ломает баланс.

Критичность: high. Сейчас "всё доступно сразу" ломает progression pacing.

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

### JRN-2 — Memories Copy And Photos

Контентная половина [JRN-1](#jrn-1--memories-structure-in-charactersjson). Структуру записей можно завести
без неё — `JournalMemoryRowView` показывает `_photoFallback` вместо отсутствующей картинки, — но вкладка
Memories станет презентабельной только с этими ресурсами.

#### Что нужно на каждую memory

| Что | Где живёт | Примечание |
|---|---|---|
| **Заголовок** | `titleKey` в `characters.json` | Короткая строка, English (REL-3) |
| **Описание** | `descriptionKey` | Абзац под фото; ширина колонки ~383 юнита |
| **Фотография** | `photoKey` | Ключ = **адрес в Addressables**, см. ниже |

#### Требования к фотографии

`photoKey` передаётся в `ProdAddressablesWrapper.LoadAsync<Sprite>` **напрямую** — то есть значение поля
обязано в точности совпадать с адресом ассета в Addressables. `UiSpriteCatalog` тут не участвует — он
нужен только для опционального прогрева и сейчас пуст.

- **Область показа:** ширина зафиксирована в коде — `JournalMemoryRowView.PhotoWidth = 342.16` юнита,
  высота тянется на всю высоту строки. Префаб строки — 725×400.
- **Пропорции:** примерно **342×400**, то есть слегка вертикальный кадр (~0.86). Точную высоту стоит
  подтвердить в редакторе на реальной строке.
- **Исходник:** 2× от области показа — ориентировочно **684×800 px**, PNG с альфой.
- **Регистрация:** положить в Addressables-группу **`Shared`** и задать адрес, равный `photoKey`. Там уже
  лежат `memory_moving_in` и `memory_placeholder` — пайплайн проверен, повторить по образцу.
- **Композиция:** карточки чередуют фото слева и справа (`ApplyLayout(imageLeft)`), поэтому кадр не должен
  зависеть от того, с какой стороны он стоит — без «смотрит в кадр» асимметрии.

#### Сколько единиц контента

По одной memory на каждого из четырёх story-персонажей — минимум. Итого на первый заход: **4 заголовка,
4 описания, 4 фотографии**. `mem_owner_placeholder` уже удалён в JRN-1.

#### Что ещё нужно, кроме текста и картинки

- **Триггер появления** — `questId`. Задаётся в JRN-1, но контент-автор должен понимать момент: memory
  разблокируется, когда связанный квест доходит до `Awarded`, и тексты пишутся под это событие.
- **Локализация как система.** Сейчас `JournalMemoryRowView` кладёт в лейблы **сам ключ**
  (`_titleLabel.text = model.TitleKey`), то есть игрок увидит `memory.owner.moving_in.title`. Пока не сделан
  INF-4/REL-3, написанные тексты физически не отобразятся. **Это блокирует приёмку задачи, но не написание
  текстов.**
- **Fallback-спрайт** на префабе стоит проверить глазами: он показывается и при пустом `photoKey`, и при
  неудачной загрузке, поэтому должен выглядеть как осмысленная заглушка, а не как битая картинка.

Критичность: high. Это единственная вкладка журнала, дающая ощущение накопленной истории; с одной карточкой
она читается как незаконченная.


## Deferred — сознательно отложено

Задачи, которые осознанно вынесены из текущего релизного scope, но не отменены. Держим здесь, чтобы не потерялись и чтобы не всплывали заново в TODO.

### GAME-10 — Finish Tutorial Release Slice

Источник: [TODO.md → GAME-10](TODO.md), [INPROGRESS/TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md).

Статус: отложено после первого релиза. В первом релизе отдельный tutorial slice не делаем: базовое
обучение и объяснение механик будут закрыты через диалоги.

Что сделать:
- Добавить debug/cheat поддержку: list, force-run, force-complete, reset, replay Day 1 через сброс `ftue.*`.
- Добавить editor/EditMode validation для tutorial target ids, `TutorialTargetTag`, quest ids и `quests.json`.
- Tutorial-аналитика: инфраструктура готова (ANL-1), `TutorialAnalyticsSteps` уже шлёт `tutorial_checkpoint`. Расширять не нужно.
- Закрыть устойчивость Day 1: корректный resume посреди дня и cancel-path.
- Проверить player-facing skip и pointer/highlight только там, где это нужно для релизного первого опыта.
- Убрать временную связность tutorial UI id из `GameplaySceneController`, если она создаёт риск поломки релиза.

Критичность: critical. Tutorial — первый контакт игрока с игрой; сломанный Day 1 будет выглядеть как сломанный продукт.

### DEF-1 — Analytics Consent Withdrawal Toggle

Тоггл отзыва согласия на аналитику в настройках. Изначально был частью REL-2, вынесен отдельно — **сейчас делать не будем**.

Что потребуется, когда возьмём:
- Переключатель в окне настроек, дёргающий `IAnalyticsConsentService.SetAnalyticsConsent(bool)` — метод уже существует, `ConsentService` его реализует.
- Учесть, что выключение посреди сессии не гасит уже поднятый Firebase: `SetAnalyticsCollectionEnabled(true)` вызывается один раз в `FirebaseAnalyticsProvider.Initialize()`. Отзыв должен либо дополнительно звать `SetAnalyticsCollectionEnabled(false)`, либо применяться со следующего запуска — это нужно решить явно, иначе тоггл будет наполовину декоративным.

Почему это не выброшено насовсем: UK GDPR требует, чтобы отозвать согласие было так же просто, как его дать. Пока согласие выдаётся на первом экране и отозвать его нельзя ничем, кроме переустановки, симметрия нарушена. Риск принят осознанно; если игра пойдёт в ЕЭЗ/UK, задачу нужно вернуть в релизный scope и пересмотреть вместе с REL-11.

### DEF-2 — Move `ISaveService` To Infrastructure Layer

Централизовать `ISaveService` в инфраструктурном слое и проверить, что текущие feature save modules не
ломаются при переносе. Изначально было первой половиной INF-6, вынесено отдельно — **сейчас делать не будем**.

Почему отложено: это перенос между слоями, а не исправление. Для игрока не меняется ничего, риска для
релиза не создаёт, а по «Scope Rule» этого файла попадает в «архитектурные улучшения на будущее».
Срочная release-baseline часть исходной задачи закрыта в [INF-6](#inf-6--save-module-versioning-release-baseline);
полный механизм миграций вынесен в [DEF-4](#def-4--save-module-version-reading--migrations).

Что учесть, когда возьмём:
- Перенос затрагивает четырнадцать save-модулей сразу, каждый со своим `ISaveHook` — это широкий, но
  механический диф; опасен не сложностью, а объёмом.
- Разумно делать **после** DEF-4, а не до: сначала зафиксировать контракт версий и миграций, потом
  переносить готовый контракт. В обратном порядке придётся трогать одни и те же файлы дважды.
- Связано с [DEF-3](#def-3--replace-manual-save-hook-force-construction) — там же убирается ручное
  форс-конструирование save-aware сервисов. Две задачи стоит планировать одной итерацией.

### DEF-3 — Replace Manual Save-Hook Force Construction

Изначально было `INF-9`, вынесено из текущего релизного scope — **сейчас делать не будем**.

Источник: [TODO.md → INF-9](TODO.md), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md).

Что потребуется, когда возьмём:
- Ввести единый `SaveHookBootstrapper : IStartable`.
- Регистрировать save-aware сервисы как `ISaveHook`.
- Убрать `save.RegisterHook(this)` из конструкторов.
- Убрать из `Bootstrap` мёртвые injections, которые сейчас нужны только для форс-конструирования save-aware сервисов.
- Проверить порядок старта: все hooks должны быть зарегистрированы до `SaveDataLoadOperation`.

Почему отложено: релизный симптом уже закрыт через [INF-8](#inf-8--force-construct-charactersservice) — `Bootstrap`
форс-конструирует нужные сервисы до загрузки сейва, поэтому приложение сейчас работает. Задача остаётся важной,
потому что убирает корневую хрупкость: новый `ISaveHook` легко забыть eager-resolve до `SaveService.LoadAsync`,
и тогда его `AfterLoadAsync` не выполнится.

Риск отсрочки принят при условии: до релиза не удалять текущие force-construction поля в `Bootstrap`, не добавлять
новые save-aware сервисы без явной eager-регистрации и не менять порядок bootstrap/load.

### DEF-4 — Save Module Version Reading & Migrations

Полный механизм чтения версий save-модулей и feature-side миграций. Исходно был срочной половиной INF-6,
но для первого релиза отложен после сброса pre-release сейвов и schema baseline `v1`.

Источник: [INF-6](#inf-6--save-module-versioning-release-baseline), [SAVE_DAY_FLOW.md](SAVE_DAY_FLOW.md),
[adr/0001-save-data-modular-payload.md](adr/0001-save-data-modular-payload.md).

Что потребуется, когда возьмём:
- Заставить загрузку читать `ModulePayload.Version` и сравнивать её с ожидаемой версией конкретного модуля.
- Добавить feature-side точку подключения миграций: фича объявляет миграции рядом со своей save-моделью и регистрирует их
  в DI, а ядро Save остаётся agnostic к внутренним DTO.
- Определить failure policy: при провале миграции или future version сбрасывать один модуль к дефолту, а не
  ронять весь save.
- Типизированно обработать legacy string payload, который сейчас попадает в graceful-default fallback.
- Покрыть тестами `v1 -> v2`, провал миграции, future version и сохранность остальных модулей.

Почему отложено: до первого релиза продовых сейвов нет, все module versions стартуют с `1`, а pre-release сейвы
стираются локально и на сервере. Задача становится обязательной до первого изменения формата save-модуля после
релиза.

### DEF-5 — CI APK Signing Via GitHub Secrets

Автоматическая подпись APK/AAB в CI. Изначально часть [REL-6](#rel-6--app-signing), вынесена отдельно —
**сейчас делать не будем**, потому что релизная сборка идёт локально из Unity, а не через CI.

Источник: [SERVICES/SECRETS.md §4](SERVICES/SECRETS.md) («CI Secrets»).

Что потребуется, когда возьмём:
- Залить в GitHub Actions Secrets репозитория `OleksandrDovhopolov/MyBookstore`:
  `ANDROID_KEYSTORE_BASE64` (base64 из `G:\MyBookstore\Key\user.keystore`), `KEYSTORE_PASSWORD`,
  `KEY_ALIAS`, `KEY_ALIAS_PASSWORD`. Значения — из записи Bitwarden `MyBookstore Android Keystore`.
- В build-workflow декодировать base64 обратно в keystore-файл перед `Unity -batchmode -build...` и передать
  пути/пароли в Android build настройки.
- Проверить, что секреты не логируются (GitHub маскирует, но не выводить их явно).

Почему отложено: CI-пайплайна для сборки APK пока нет. Ручной GitHub Secrets заводить нет смысла, пока
подпись делается в Unity локально. Значения уже безопасно лежат в Bitwarden, так что данные не потеряются.
Задача становится нужной, когда подключим автоматическую сборку/деплой APK.

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
