# Сборка билда (APK) — что сделать перед сборкой

Чеклист подготовки к player-сборке (Android/APK). Часть проверок **автоматические** — их полный список в
§0. Туда вынесено всё, что молча ломает контент (APK стартует, содержимое просто неверное), поэтому из
«не забыть» это переехало в «не соберётся». Остальное — стандартные Unity/Android проверки, которые
остаются на человеке.

> Связано: [SERVICES/CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md) (загрузка конфигов),
> [SERVICES/ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [SERVICES/FIREBASE_INTEGRATION.md](SERVICES/FIREBASE_INTEGRATION.md),
> [SERVICES/SECRETS.md](SERVICES/SECRETS.md), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md),
> [ACTIVE_REQUEST_CONDITIONS.md](ACTIVE_REQUEST_CONDITIONS.md).

---

## 0. Автоматический гейт — `PreBuildValidationGate`

Файл: `Assets/Game/Core/Build/Editor/PreBuildValidationGate.cs` (`IPreprocessBuildWithReport`,
`callbackOrder = 0` — раньше Addressables).

Запускается **сам на каждой player-сборке** и **валит билд** (`BuildFailedException`), если хоть один
hard-валидатор вернул ошибку. Warning-проверки пишутся в консоль, но билд не останавливают.

### Список валидаторов в гейте

Источник правды — массивы `PreBuildValidationGate.Validators` и `SoftValidators`. Эта таблица — их
человекочитаемая копия;
**при добавлении валидатора обновлять оба места.**

| Валидатор | Уровень | Что ловит | Почему это не видно иначе |
|---|---|---|---|
| `CollectBundledConfigErrors` (внутри гейта) | Error | Файл есть в `Assets/Configs`, но не в StreamingAssets; лежит в StreamingAssets, но удалён из источника; содержимое одноимённых файлов различается; файл забыт в `manifest.json` | В плеере `Directory.GetFiles` недоступен — не перечисленный в манифесте файл невидим, даже если физически попал в APK |
| `ActiveRequestValidator` | Error | Активный запрос, который не может удовлетворить ни одна книга каталога; жанр, ни одна книга которого не способна получить `Excellent` (тогда квест с `activePickGenre <жанр>` непроходим) | Синтаксически корректный запрос спавнится и просто никогда не решается |
| `DialogueDeliveredConditionReferenceValidator` | Error | Условие `dialogueDelivered` в квесте ссылается на несуществующий `dialogueId` (или не указывает его) | Условие fail-closed → квест молча никогда не стартует |
| `BookBoxPoolValidator` | Error | Лот-книжная коробка, чей пул не матчит ни одной книги (или матчит меньше, чем `rolls`); лот с `rewardId` вида `book_box_*`, для которого нет правила | Правила пула читают поля `BookConfig` напрямую: если в каталоге поля нет, книга садится на C#-дефолт, пул пустеет — ни ошибки парсинга, ни битой ссылки. Так `book_box_rare_8` (`RarityWeight >= 0.6`) сломался при замене каталога на тот, где нет `rarityWeight`: все книги получили дефолтные `0.5`, и лот начал брать золото, не выдавая ничего |
| `CollectOrphanConfigWarnings` | Warning | JSON в `Assets/Configs`, для которого нет ни одного `[ConfigFile]` | Такой файл уезжает в APK и manifest как мёртвый груз; сейчас ожидаемый пример — legacy `hard_requests.json`, живой файл запросов — `sample_requests.json` |

Правила общие для всех: валидаторы **чистые** (ничего не логируют и не показывают — решает вызывающий),
читают JSON напрямую (без `IConfigsService`, которого вне Play mode нет) и переиспользуют рантаймовый код,
чтобы вердикт не мог разойтись с игрой (`ActiveRequestValidator` → `BookConditionRequestEvaluator`,
`BookBoxPoolValidator` → `BookBoxPoolRules`). Исключение валидатора из списка не должно требовать правок в
самом гейте — прогон, префикс сообщения и обработка падения общие.

### Прогнать заранее, не запуская билд

| Меню | Что запускает |
|---|---|
| `Tools → Configs → Run Pre-Build Validation` | весь список выше |
| `Tools → Configs → Validate Active Requests` | только `ActiveRequestValidator` |
| `Tools → Configs → Validate Book Box Pools` | только `BookBoxPoolValidator` |

Гейт **не** проверяет: Addressables, Firebase, Player Settings, флаги `BootstrapInstaller.asset` — это
пункты 2–5 ниже, они остаются ручными.

**Известный пробел.** Ссылки на предметы (награды квестов, лоты магазина, `unlockCost`, условия `haveItem`)
валидирует `ItemReferenceValidator`, но он рантаймовый: в редакторе бросает и блокирует Play mode, а в билде
только пишет `LogError`. В гейт он не встроен, потому что требует `IConfigsService`, которого вне Play нет.
Пока это ловится входом в Play mode перед сборкой. Чтобы встроить — его надо переписать на чтение JSON
напрямую, как остальные из таблицы.

---

## 1. ⚠️ Опубликовать и синхронизировать конфиги (ОБЯЗАТЕЛЬНО)

**Почему.** В player-сборке базовый источник — `ServerConfigSource`: он сначала прогревает bundled defaults
из `Assets/StreamingAssets/Configs`, затем накладывает disk snapshot из `Application.persistentDataPath/configs/`,
а поверх него — свежую серверную версию из public config API. StreamingAssets — это оффлайн-baseline, а не
единственный источник правды.

Следствия:

- если менялись живые конфиги, актуальную версию нужно опубликовать на сервер; иначе онлайн-клиент может взять
  старую серверную секцию поверх свежего APK;
- snapshot переживает апдейт приложения, поэтому проверять билд нужно на чистой установке или после
  `Tools → Configs → Clear Server Snapshot`;
- для оффлайн/fresh-install baseline всё равно нужен Sync в StreamingAssets.

**Что сделать:**

1. Разобраться с warning-ами orphan-config checker. Сейчас ожидаемый warning — `hard_requests.json`: живой файл
   активных запросов мапится через `[ConfigFile("sample_requests")]`.
2. Опубликовать изменённые живые секции через **`Tools → Configs → Editor Window`** в нужное окружение
   (`dev`/`prod`) либо подтвердить, что сервер уже содержит ту же версию.
3. Запустить **`Tools → Configs → Sync Bundled Defaults to StreamingAssets`**. Оно:
   - копирует все `Assets/Configs/*.json` → `Assets/StreamingAssets/Configs/`;
   - перегенерирует `manifest.json` из списка файлов.
4. Проверить:
   - `Assets/StreamingAssets/Configs/*` совпадает с `Assets/Configs/*` для одноимённых файлов;
   - в `manifest.json` перечислены все bundled-конфиги;
   - `.meta` новых `.json` закоммичены.

**Когда нужно.** Каждый раз, когда менялись любые `Assets/Configs/*.json` (`books`, `sample_requests`,
`dialogues`, `days`, `quests`, `locations`, …). Если сомневаешься — просто прогони Sync, он идемпотентный.

## 2. Addressables — собрать контент

Бут гоняет операцию `addressables_update` (`ProdAddressablesWrapper`, `AddressablesUpdateOperation`); окна,
спрайты, префабы и т.п. грузятся через Addressables. Перед билдом:

- либо включить **Build Addressables on Player Build** (Project Settings → Addressables), чтобы контент
  собирался вместе с плеером;
- либо собрать вручную: **Window → Asset Management → Addressables → Groups → Build → New Build / Default
  Build Script**.

Если Addressables-контент не собран/устарел, приложение может стартовать с отсутствующими ассетами.

## 3. Firebase — конфиг для Android

На старте активен `FirebaseRemoteConfigService` (Remote Config, напр. `cfg_books`). Убедиться, что для
Android-таргета на месте конфиг Firebase (`google-services.json` / Firebase Android SDK) и приложение
привязано к нужному проекту. Секреты/ключи — по [SERVICES/SECRETS.md](SERVICES/SECRETS.md), не коммитить в
репозиторий.

**RC-override — этап загрузки (не отдельные конфиги).** RC работает как partial-overlay поверх base
(сервер/bundled): ключ `cfg_<file>` (напр. `cfg_books`) хранит `{"<id>":{...поля...}}` и мёржится в момент
ленивой десериализации секции — **после** фазы `configs_warmup`. Следствия для билда:

- id в RC-ключе должны совпадать с id **опубликованной** секции. Иначе override молча ни к чему не
  применяется — в логе `[RemoteConfigOverrideSource] '<key>' present, but no entry for id='…'` (безвредный
  шум, но признак рассинхрона id между сервером и RC).
- один и тот же ключ **не** держать одновременно в сервере и в RC (см. правило разделения в
  [CONFIG_CACHE_SYSTEM.md §3](SERVICES/CONFIG_CACHE_SYSTEM.md)).

## 4. Android Player Settings

Стандартные настройки под целевой таргет (проверить в `Project Settings → Player` / `Build Settings`):

- **Scripting Backend:** IL2CPP; **Target Architectures:** ARM64 (обязательно для Google Play).
- **Minimum / Target API Level** — под требования стора.
- **Keystore** для подписи (для тестового APK хватает debug keystore; для релиза — release keystore).
- Список сцен в **Build Settings** актуален (bootstrap-сцена первой).

## 5. `BootstrapInstaller.asset` — настройки и ссылки (Dev / Release)

Файл: `Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.asset` — `ScriptableObjectInstaller`,
лежит в Script Installers на `GlobalLifetimeScope.prefab`. Это единственное место, где задаются
глобальные ссылки и флаги старта, поэтому перед каждой сборкой имеет смысл пройтись по таблице.

> **Правило поддержки этого раздела:** здесь описывается **назначение** поля и **требуемое значение**
> для Dev/Release. Текущие значения полей тут не фиксируем — источник правды по ним сам ассет.
> Раньше в этом разделе стояли пометки «сейчас X», и две из них разошлись с реальностью.

### 5.1 Ассеты-ссылки — должны быть назначены всегда

| Поле | Что делает | Если `None` |
|---|---|---|
| `_uiCanvasRootPrefab` | Префаб `UICanvasRoot`, инстанцируется один раз в `DontDestroyOnLoad` | UI-система не поднимется |
| `_gameFlowSettings` | Имена сцен для петли хаб ↔ локация (см. [GameFlowLoop.md](GameFlowLoop.md)) | Переходы между сценами сломаются |
| `_uiSpriteCatalog` | Адреса Addressables-спрайтов newspaper/rewards, предзагружаются на бутстрапе | Спрайты догрузятся позже или не найдутся |
| `_resourceAnimationSettings` | Общие настройки летящих анимаций ресурсов | Анимации наград не отработают |
| `_tutorialOverlaySettings` | Оверлей туториала (затемнение + pointer + панель текста) | Подсветки шагов туториала не отрисуются |
| `_tutorialSettings` | Пер-последовательное вкл/выкл туториалов | **`None` = включены все** зарегистрированные последовательности, а не выключены |

### 5.2 Аналитика (ANL-1)

| Поле | Что делает |
|---|---|
| `_analyticsConfig` | Основной конфиг аналитики (провайдеры, лимиты параметров) |
| `_analyticsRoutingConfig` | Правила «какое событие в какого провайдера» |
| `_analyticsMappingConfig` | Переименование событий/параметров под конкретного провайдера |

Все три должны быть назначены. **Если поле пустое, `RegisterAnalytics` молча падает на
`DefaultAnalyticsConfig` / `DefaultAnalyticsRoutingConfig` / `DefaultAnalyticsMappingConfig`** — аналитика
будет выглядеть работающей, но настройки из ассетов проигнорируются. Ошибки в лог при этом не будет,
проверять глазами.

**Содержимое `AnalyticsConfig.asset` перед релизом менять не нужно.** Он хранит dev defaults
(`_isDebugLoggingEnabled: 1`, `_environment: development`), а `AnalyticsBuildContext` выводит фактическое
поведение из типа сборки:

- Editor и Development Build используют dev defaults — debug provider пишет `[Analytics][Debug]`, и проверка
  через `adb logcat` работает как раньше;
- обычная release-сборка принудительно выключает отладочный лог аналитики и отправляет `environment=production`.

Сериализованный флаг может только **выключить** лог в dev-сборке, но не включить его в релизной.
Подробности — REL-12 в [RELEASE_TASKS.md](RELEASE_TASKS.md).

### 5.3 Поведенческие флаги — под задачу билда

| Поле | Что делает | Значение для релиза |
|---|---|---|
| `_tutorialAutoStart` | Автостарт последовательностей по триггерам и резюм при загрузке. При `0` движок всё равно регистрируется (оверлей + условие `tutorialCompleted`), явный `TryStartAsync` работает | `1` — иначе `tutorial_day_1` не запустится по `locationLoaded` |
| `_startWelcomeWindow` | Показывать ли welcome-окно при первом заходе. Save-флаг `welcome_completed` при выключении не меняется | `1` для продуктового первого опыта |
| `_firstDayEntry` | Путь входа в день 1: `Location` (`1`) — сразу в локацию с авто-стоком полки (продуктовый путь, см. [FTUE.md](FTUE.md)); `Hub` (`0`) — классический флоу через хаб | `1` |

### 5.4 Privacy (REL-5) — релизный блокер

| Поле | Что делает |
|---|---|
| `_privacyPolicyUrl` | Публичная ссылка на политику, открывается с экрана согласия первого запуска |
| `_termsOfUseUrl` | Ссылка на условия использования. Можно оставить пустой — `PrivacyLinkSettings.TermsOfUseUrl` сам падает обратно на privacy-ссылку |

`PrivacyLinksBuildCheck` валит сборку, если `_privacyPolicyUrl` пустой или не начинается с `https://`.
**Но домен он не проверяет.** Сейчас в ассете стоят ссылки на `themergegames.com` — домен другого
проекта, и эту проверку они успешно проходят. Перед релизной сборкой домен нужно сверить глазами.

### 5.5 Debug Start — только Editor

`_useDebugFeatures` (мастер-выключатель) и `_skipFullLoading` (пропуск Addressables update + RemoteConfig
init) объявлены под `#if UNITY_EDITOR`, тело `ApplyDebugFlags()` — тоже. **В плеер эти поля не попадают,
и `DebugStartFlags` в билде всегда `false`** — то есть утечь в релиз debug/cheat-фичи через них не могут.

Держать их в `0` нужно для другого: при `_skipFullLoading = 1` в Editor не отрабатывают критические фазы
бутстрапа, **включая FTUE-сидирование** (`phase_ftue / ftue_bootstrap`), и проверка получится нерепрезентативной.
Экран согласия при этом всё равно показывается — `ConsentGateOperation` намеренно присутствует в обеих ветках.

### 5.6 FTUE

FTUE — save-backed: сидирование (стартовые gold/книги, скриптовый день 1) выполняется, только если
в сейве нет `ftue.applied` (см. [FTUE.md](FTUE.md)). Следствия для проверки билда:

- проверять FTUE нужно на **чистой установке / со сброшенными данными** — иначе в логе будет
  `[FTUE] skip — already applied` и стартовый опыт не проиграется;
- `_skipFullLoading` держать `0`, чтобы фаза `ftue_bootstrap` вообще запускалась.

Статус туториала виден в логе на старте: `[Tutorial] loaded: N sequences, M completed. autoStart=…` —
сверить с ожиданием (например, `autoStart=False` при `_tutorialAutoStart = 0`). Детали — [TUTORIAL_SYSTEM.md](INPROGRESS/TUTORIAL_SYSTEM.md).

## 6. Быстрая проверка после сборки (smoke)

- Приложение стартует, лоадинг проходит все фазы (`[Loading] result=completed …`, `[Bootstrap] Loading complete`).
- `[LocalFolderConfigSource]` в Editor / `[StreamingAssetsConfigSource]` в билде грузит ожидаемое число
  конфигов без ошибок парсинга.
- Активный запрос через мини-игру матчит подходящую книгу (не «всё Failed») — признак, что `sample_requests.json`
  в билде свежий.
- Диалог (`eddy1`) открывается, ветки и кнопки работают.

---

## Быстрый чеклист

- [ ] Разобраться с warning по `Assets/Configs/hard_requests.json` (legacy, живой файл — `sample_requests.json`).
- [ ] `Tools → Configs → Sync Bundled Defaults to StreamingAssets`.
- [ ] `Tools → Configs → Run Pre-Build Validation` — ноль ошибок; warning по `hard_requests.json` ожидаем до удаления legacy-файла.
- [ ] Войти в Play mode хотя бы раз — так отработают рантаймовые валидаторы (`ItemReferenceValidator`, `DecorConfigValidator`), которых нет в гейте.
- [ ] Собрать/включить Addressables.
- [ ] Firebase Android-конфиг на месте.
- [ ] Player Settings: IL2CPP + ARM64, API level, keystore, список сцен.
- [ ] `BootstrapInstaller.asset` — пройти по таблицам [§5](#5-bootstrapinstallerasset--настройки-и-ссылки-dev--release): ассеты-ссылки и три поля аналитики назначены, поведенческие флаги под задачу билда, debug-флаги `0`.
- [ ] `_privacyPolicyUrl` ведёт на **свой** домен — build-check проверяет только `https://`, чужую ссылку он пропустит.
- [ ] `AnalyticsConfig.asset` руками не переключать: release-сборка сама получает silent debug logging и `environment=production`.
- [ ] FTUE проверять на чистой установке (сброшенные данные).
- [ ] Собрать APK → smoke-проверка старта, конфигов, активного запроса, диалога, FTUE/туториала.
