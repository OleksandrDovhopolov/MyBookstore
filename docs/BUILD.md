# Сборка билда (APK) — что сделать перед сборкой

Чеклист подготовки к player-сборке (Android/APK). Пункт 1 — **обязательный и проектный** (молча ломает
контент, если пропустить); остальное — стандартные Unity/Android проверки.

> Связано: [SERVICES/CONFIG_CACHE_SYSTEM.md](SERVICES/CONFIG_CACHE_SYSTEM.md) (загрузка конфигов),
> [SERVICES/ADDRESSABLES.md](SERVICES/ADDRESSABLES.md), [SERVICES/FIREBASE_INTEGRATION.md](SERVICES/FIREBASE_INTEGRATION.md),
> [SERVICES/SECRETS.md](SERVICES/SECRETS.md), [BOOTSTRAP_AND_LOADING.md](BOOTSTRAP_AND_LOADING.md).

---

## 1. ⚠️ Синхронизировать конфиги в StreamingAssets (ОБЯЗАТЕЛЬНО)

**Почему.** В плеер-сборке конфиги грузятся из `Assets/StreamingAssets/Configs/` по `manifest.json`
(`StreamingAssetsConfigSource`), а **не** из `Assets/Configs/` — та папка читается только в Editor
(`LocalFolderConfigSource`, `TopDirectoryOnly`). Если не пересинхронизировать, APK уедет со **старыми**
конфигами: активные запросы не сматчатся (`hard_requests.json` со старыми типами `genres`/`qualities`),
диалоги/тексты будут устаревшими и т.п.

**Что сделать:**

1. (Рекомендуется) Удалить мёртвый `sample_requests.json` из `Assets/Configs/` (+`.meta`) — он больше не
   используется (заменён `hard_requests.json`, ни к какому `[ConfigFile]` не привязан). Иначе Sync
   перекладывает его в StreamingAssets и в манифест как «мёртвый» файл. См. `TODO.md → GAME-14`.
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
  `0` — нет. (сейчас `0`.)
- **`_startWelcomeWindow`** — показывать ли welcome-окно на старте. (сейчас `0`.)

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
- [ ] Проверить `manifest.json` + отсутствие расхождений StreamingAssets ↔ Assets/Configs.
- [ ] Собрать/включить Addressables.
- [ ] Firebase Android-конфиг на месте.
- [ ] Player Settings: IL2CPP + ARM64, API level, keystore, список сцен.
- [ ] `BootstrapInstaller.asset`: `_useDebugFeatures=0`, `_skipFullLoading=0`, `_tutorialOverlaySettings` назначен, `_tutorialAutoStart`/`_startWelcomeWindow` — под задачу билда.
- [ ] FTUE проверять на чистой установке (сброшенные данные).
- [ ] Собрать APK → smoke-проверка старта, конфигов, активного запроса, диалога, FTUE/туториала.
