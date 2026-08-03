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
валидатор из списка вернул ошибку.

### Список валидаторов в гейте

Источник правды — массив `PreBuildValidationGate.Validators`. Эта таблица — его человекочитаемая копия;
**при добавлении валидатора обновлять оба места.**

| Валидатор | Что ловит | Почему это не видно иначе |
|---|---|---|
| `CollectBundledConfigErrors` (внутри гейта) | Файл есть в `Assets/Configs`, но не в StreamingAssets; лежит в StreamingAssets, но удалён из источника; содержимое одноимённых файлов различается; файл забыт в `manifest.json` | В плеере `Directory.GetFiles` недоступен — не перечисленный в манифесте файл невидим, даже если физически попал в APK |
| `ActiveRequestValidator` | Активный запрос, который не может удовлетворить ни одна книга каталога; жанр, ни одна книга которого не способна получить `Excellent` (тогда квест с `activePickGenre <жанр>` непроходим) | Синтаксически корректный запрос спавнится и просто никогда не решается |
| `DialogueDeliveredConditionReferenceValidator` | Условие `dialogueDelivered` в квесте ссылается на несуществующий `dialogueId` (или не указывает его) | Условие fail-closed → квест молча никогда не стартует |
| `BookBoxPoolValidator` | Лот-книжная коробка, чей пул не матчит ни одной книги (или матчит меньше, чем `rolls`); лот с `rewardId` вида `book_box_*`, для которого нет правила | Правила пула читают поля `BookConfig` напрямую: если в каталоге поля нет, книга садится на C#-дефолт, пул пустеет — ни ошибки парсинга, ни битой ссылки. Так `book_box_rare_8` (`RarityWeight >= 0.6`) сломался при замене каталога на тот, где нет `rarityWeight`: все книги получили дефолтные `0.5`, и лот начал брать золото, не выдавая ничего |

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

## 1. ⚠️ Синхронизировать конфиги в StreamingAssets (ОБЯЗАТЕЛЬНО)

**Почему.** В плеер-сборке конфиги грузятся из `Assets/StreamingAssets/Configs/` по `manifest.json`
(`StreamingAssetsConfigSource`), а **не** из `Assets/Configs/` — та папка читается только в Editor
(`LocalFolderConfigSource`, `TopDirectoryOnly`). Если не пересинхронизировать, APK уедет со **старыми**
конфигами: активные запросы не сматчатся с каталогом книг, диалоги/тексты будут устаревшими и т.п.

**Что сделать:**

1. (Рекомендуется) Удалить из `Assets/Configs/` (+`.meta`) конфиги, не привязанные ни к одному
   `[ConfigFile]` — Sync перекладывает их в StreamingAssets и в манифест как мёртвый груз, а гейт потом
   сверяет их побайтово. Сейчас в эту категорию попадают **`hard_requests.json`** (`RequestDefinitionConfig`
   переключён на `sample_requests`). После переезда каталога книг `books.json` — живой файл `BookConfig`;
   `books_converted.json` не должен оставаться в `Assets/Configs`.
   Проверять так: файл живой, если его имя встречается в `[ConfigFile("…")]` у какого-нибудь DTO в
   `Assets/Game/Features/Configs/Models/`. См. `TODO.md → GAME-14`.
2. Запустить меню **`Tools → Configs → Sync Bundled Defaults to StreamingAssets`**. Оно:
   - копирует все `Assets/Configs/*.json` → `Assets/StreamingAssets/Configs/`;
   - перегенерирует `manifest.json` из списка файлов.
3. Проверить:
   - `Assets/StreamingAssets/Configs/*` совпадает с `Assets/Configs/*` (нет расхождений в содержимом);
   - в `manifest.json` перечислены все нужные конфиги;
   - `.meta` новых `.json` закоммичены.

**Когда нужно.** Каждый раз, когда менялись любые `Assets/Configs/*.json` (`books`, `hard_requests`,
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

## 5. FTUE / Tutorial — флаги в `BootstrapInstaller.asset`

Файл: `Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.asset`. Перед релизным билдом проверить поля:

- **`_useDebugFeatures` = 0 (выкл)** — для релиза строго `0`, иначе в билд утекут debug/cheat-фичи.
- **`_skipFullLoading` = 0 (выкл)** — дев-шорткат, обрезающий полный лоадинг. Должен быть `0`, иначе не
  отработают критические фазы бутстрапа, **включая FTUE-сидирование** (`phase_ftue / ftue_bootstrap`).
- **`_tutorialOverlaySettings` — назначен** (не `None`) — ассет настроек оверлея туториала (pointer +
  затемнение). Без него подсветки шагов туториала не отрисуются. (сейчас назначен.)
- **`_tutorialAutoStart`** — под задачу билда: `1` — туториал стартует автоматически на первом заходе,
  `0` — нет. (сейчас `1`; нужно `1`, иначе `tutorial_day_1` не запустится по `locationLoaded`.)
- **`_startWelcomeWindow`** — показывать ли welcome-окно на старте. (сейчас `0`.)
- **`_firstDayEntry`** — путь входа в день 1: `Location` (`1`) — сразу в локацию с авто-стоком полки
  (продуктовый путь, см. [FTUE.md](FTUE.md)); `Hub` (`0`) — классический флоу через хаб. (сейчас `1`.)

**FTUE.** FTUE — save-backed: сидирование (стартовые gold/книги, скриптовый день 1) выполняется, только если
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
- Активный запрос через мини-игру матчит подходящую книгу (не «всё Failed») — признак, что `hard_requests.json`
  в билде свежий.
- Диалог (`eddy1`) открывается, ветки и кнопки работают.

---

## Быстрый чеклист

- [ ] Удалить `Assets/Configs/sample_requests.json` (если ещё лежит).
- [ ] `Tools → Configs → Sync Bundled Defaults to StreamingAssets`.
- [ ] `Tools → Configs → Run Pre-Build Validation` — зелёный (иначе билд всё равно упадёт, см. §0).
- [ ] Войти в Play mode хотя бы раз — так отработают рантаймовые валидаторы (`ItemReferenceValidator`, `DecorConfigValidator`), которых нет в гейте.
- [ ] Собрать/включить Addressables.
- [ ] Firebase Android-конфиг на месте.
- [ ] Player Settings: IL2CPP + ARM64, API level, keystore, список сцен.
- [ ] `BootstrapInstaller.asset`: `_useDebugFeatures=0`, `_skipFullLoading=0`, `_tutorialOverlaySettings` назначен, `_tutorialAutoStart`/`_startWelcomeWindow`/`_firstDayEntry` — под задачу билда.
- [ ] FTUE проверять на чистой установке (сброшенные данные).
- [ ] Собрать APK → smoke-проверка старта, конфигов, активного запроса, диалога, FTUE/туториала.
