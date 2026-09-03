# A/B Testing Readiness — аудит конфигов и аналитики

> **Статус: аудит, не спека.** Документ фиксирует, насколько текущая система конфигов и аналитики
> готова к A/B-экспериментам, и что нужно сделать до первого честного теста.
> Дата аудита: 2026-08-12.

Связанные документы:
- [ADR-0002: Config system — client architecture](../adr/0002-config-system-architecture.md) — архитектурное решение и extension paths
- [CONFIG_CACHE_SYSTEM.md](../SERVICES/CONFIG_CACHE_SYSTEM.md) — runtime-путь загрузки конфигов
- [CONFIG_SERVER_API.md](../SERVICES/CONFIG_SERVER_API.md) — серверный контракт
- [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](../SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) — Editor-инструмент публикации

---

## 0. Вердикт

**Фундамент готов, инструмента нет.** Архитектура [ADR-0002](../adr/0002-config-system-architecture.md)
спроектирована под A/B буквально в тексте решения, и слои держат удар. Но сегодня реально
запускается ровно **один класс экспериментов** — подкрутка полей существующих записей в
json-секциях. Самое ценное для баланса находится вне контура, а измерять эффект нечем.

| Слой | Готовность | Комментарий |
|---|---|---|
| Транспорт, слои, изоляция | **готово** | Архитектура специально сделана под это |
| Поверхность эксперимента | **~30%** | Только поля json-секций; весь баланс дня вне контура (§2.1) |
| Измерение | **~50%** | Аналитика настоящая, но нет variant/exposure (§2.2) |
| Операционка (реестр, процесс, видимость) | **~10%** | Нет реестра экспериментов, нет способа посмотреть активные |

---

## 1. Как сейчас устроены конфиги

| Слой | Реализация | Роль в A/B |
|---|---|---|
| Base | `LocalFolderConfigSource` / `StreamingAssetsConfigSource` / `ServerConfigSource` | Версии, history, rollback, promote — но **всем игрокам сразу** |
| Override | [RemoteConfigOverrideSource.cs](../../Assets/Game/Features/Configs/Remote/RemoteConfigOverrideSource.cs) | Ключ `cfg_<fileName>` → `{"<id>": {...partial...}}` — **единственная точка таргетинга** |
| Merge | [ConfigsService.cs:168-181](../../Assets/Game/Features/Configs/ConfigsService.cs) | Мёрж partial поверх base при ленивой десериализации |
| Окно ГД | [ConfigEditorWindow](../SERVICES/CONFIG_EDITOR_WINDOW_MVP_SPEC.md) | Pull / Publish / History / Rollback / Promote — про **версии**, не про варианты |

**Живость RC проверена:** define `BOOKSTORE_FIREBASE_RC` включён для Android
([ProjectSettings.asset:777](../../ProjectSettings/ProjectSettings.asset)). В Android-билде RC
настоящий, не заглушка. В Editor и Standalone активен `NullRemoteConfigService` — то есть
**эксперимент нельзя проверить в редакторе вообще**.

---

## 2. Дыры по убыванию критичности

### 2.1 Самые интересные для A/B числа физически недоступны RC

`SalesTuningConfig` и `SalesTrafficConfig` — это `ScriptableObject`
([SalesTuningConfig.cs:12](../../Assets/Game/Features/BookSell/Services/SalesTuningConfig.cs)),
запекаемые в билд и попадающие в DI через `salesTuningConfig.BuildTuning()`
([BookSellVContainerBindings.cs:153](../../Assets/Game/Core/Installers/Features/BookSellVContainerBindings.cs)).

Значит `PassiveDemandRequestShare`, `PassiveRequestGenreCount`, `Min/MaxPassiveAttempts`,
`BrowseDuration`, `SpawnInterval`, `MaxConcurrentCustomers`, весь трафик — **не проходят через
`IConfigsService`**. A/B на них сегодня = новый билд.

Это дыра №1: система конфигов готова, но ровно та поверхность, ради которой A/B и заводят,
в неё не подключена.

### 2.2 Понятия «вариант эксперимента» нет нигде в коде

Греп по `experiment|variant|cohort|bucket` по всему `Assets/Game` — ноль попаданий в игровом
коде (только `assetBundleVariant` в `.meta`-файлах Unity). Следствия:

- В аналитике нет параметра, по которому можно резать метрики самостоятельно. Firebase A/B
  Testing режет сам, но только внутри своей консоли и по своим метрикам.
- **Нет exposure-события.** Разница между *assignment* (Firebase кинул игрока в группу B на
  старте — происходит со всеми) и *exposure* (игрок реально дошёл до изменённой механики).
  Игроки, назначенные в B но не увидевшие фичу, разбавляют выборку и тест показывает
  «разницы нет» даже когда она есть. Это самая частая причина провальных A/B на малом трафике.
- Нельзя воспроизвести баг: в логах не видно, в каком варианте был игрок.

Крючок для починки уже есть: `SetUserProperty` в
[FirebaseAnalyticsProvider.cs:35](../../Assets/Game/Infrastructure/Analytics/Providers/Firebase/FirebaseAnalyticsProvider.cs)
и common-параметры в
[UnityAnalyticsContextProvider.cs:41](../../Assets/Game/Infrastructure/Analytics/Unity/UnityAnalyticsContextProvider.cs).

### 2.3 RC умеет менять поля, но не состав секции

`ConfigsService.DeserializeType` мёржит partial по `id`, **найденному в базовом массиве**.
Через RC нельзя добавить запись, удалить запись или применить изменение ко всем записям сразу.
`MergeArrayHandling.Replace` ([ConfigsService.cs:22](../../Assets/Game/Features/Configs/ConfigsService.cs))
заменяет массивы *внутри* объекта, но корневой массив секции неприкосновенен.

Для A/B это больнее, чем кажется: большинство контентных экспериментов — «другой набор», а не
«другое число». Закрывается через [ADR-0002 §extension 3](../adr/0002-config-system-architecture.md)
(`cfg_books_global`), оценка **Low ~4 часа**.

**Обход, работающий уже сегодня:** поле `enabled` на записи. У `RequestDefinitionConfig` оно
есть и читается `ConfigActiveRequestRuntimeProvider`. То есть A/B на составе пула запросов
доступен без единой строки кода:

```json
cfg_sample_requests → {"req_kids_01": {"enabled": false}}
```

**Конвенция, которую стоит закрепить:** у каждой записи любой секции должно быть поле `enabled`.
Оно превращает field-override в состав-override и снимает большую часть ограничения.

### 2.4 Гонка на первом запуске загрязняет D0

`ConfigsWarmupEntryPoint` вызывает RC `InitializeAsync` → затем `WarmupAsync`. Порядок правильный.
Но `FetchAndActivateAsync` на первом старте либо успевает по сети, либо нет — во втором случае
игрок первую сессию играет в контроле, а со второй в тесте. Для игры-новинки это загрязняет
ровно D0-метрики, самые важные.

Дополнительно: `MinimumFetchIntervalInMilliseconds = 0`
([FirebaseRemoteConfigService.cs:20](../../Assets/Game/Core/Installers/Bootstrap/FirebaseRemoteConfigService.cs))
— это dev-значение. В проде Firebase будет троттлить (и правильно), но число надо выставить
осознанно.

**Смягчение:** логировать в common-параметрах факт успешной активации RC, чтобы отделять
«игрок в контроле» от «игрок не получил конфиг».

### 2.5 Таргетинга на серверном слое нет

Прод-клиент жёстко читает `prod` — [ADR-0002](../adr/0002-config-system-architecture.md) сам
называет это negative-последствием. Серверный слой даёт rollback, но не даёт сегменты.
Весь таргетинг — только RC.

Практический вывод: **любой A/B обязан укладываться в partial-field-override.** Других
вариантов в текущей архитектуре нет.

### 2.6 Immutability после warmup — это плюс, но с неявной зависимостью

`IConfigsService` неизменен после прогрева (reactive refresh отложен сознательно,
[ADR-0002 §extension 9](../adr/0002-config-system-architecture.md)). Для эксперимента это
**хорошо**: вариант не может смениться посреди дня.

Но `_cache` кэширует тип при первом `Get<T>`, и RC читается именно в этот момент
([ConfigsService.cs:153](../../Assets/Game/Features/Configs/ConfigsService.cs)). Если RC
активируется после первого `Get<T>` какого-то типа — тип уже закэширован без override'ов.
Порядок в `ConfigsWarmupEntryPoint` правильный, но это неявная зависимость, которую легко
сломать при рефакторинге бутстрапа. Защита от «до прогрева» есть
([ConfigsService.cs:119-123](../../Assets/Game/Features/Configs/ConfigsService.cs)), но она про
другой случай.

### 2.7 Диагностики почти нет — но одна строчка правильная

```
[ConfigsService] 'sample_requests': applied 2 of 3 RC override(s)
```

([ConfigsService.cs:199](../../Assets/Game/Features/Configs/ConfigsService.cs)) — ловит
классический баг «RC таргетит id, которого нет в каталоге». Всё остальное про эксперименты в
рантайме не видно, и Editor Window про них не знает.

---

## 3. Как правильно проводить A/B тесты

**Ответ: Firebase, не сервер.** Роли разделяются так:

| Слой | Роль | Почему не наоборот |
|---|---|---|
| Свой сервер | **Baseline/контроль.** Полный контент, версии, rollback, promote | Не умеет таргетинг (см. §2.5). Чтобы отдавать разное разным, пришлось бы строить своё бакетирование, sticky-хранилище назначений и exposure-API — это недели работы |
| Firebase RC + A/B Testing | **Движок назначения.** Детерминированное бакетирование, липкое назначение, автоматическая связь с Firebase Analytics | Сделан ровно для этого, уже включён для Android |

### 3.1 Правильная схема (код менять не нужно)

1. На сервере лежит **контроль** — полная секция, например `books.json`.
2. В Firebase RC лежит только **дельта** варианта B: ключ `cfg_books` со значением
   `{"<реальный id>": {"<поле>": <значение>}}`.
3. В Firebase Console создаётся **A/B Test** (именно эксперимент, не просто RC-условие):
   вариант A — ключ отсутствует/дефолт, вариант B — ключ с дельтой.
4. Клиент мёржит это сам в
   [ConfigsService.cs:168](../../Assets/Game/Features/Configs/ConfigsService.cs).

### 3.2 Текущее состояние конфигов — известная проблема

На сервере сейчас **только `books.json`**, и единственный RC-ключ таргетит `id`, которого в
серверном массиве нет. Это ровно тот случай, под который написан лог из §2.7 — мёрж молча не
срабатывает, `applied 0 of 1`.

**Чинится:** поставить в RC `id` реально существующей книги и проверить по логу, что
`applied 1 of 1`.

### 3.3 Запасной вариант — клиентское бакетирование

`hash(install_id) % 100`. Дёшево, работает оффлайн, детерминированно, **нет гонки на первом
запуске** (§2.4). Минусы: нет удалённого kill-switch, соотношение групп зашито в билд,
логировать вариант надо самому.

Как основной механизм брать не стоит, но как способ закрыть D0-проблему — рабочий.

---

## 4. Аналитика — что есть и чего не хватает

### Есть (инфраструктура готова)

`CompositeAnalyticsService`, `FirebaseAnalyticsProvider` (`LogEvent` / `SetUserId` /
`SetUserProperty`), routing/mapping конфиги, event factory, очередь, consent-сиам, тесты.
Common-параметры: `app_version`, `platform`, `device_model`, `os_version`, `language`,
`country`, `install_id`, `session_id`, `session_number`, `days_since_install`, `environment`.

Потребители: `ShopAnalyticsListener`, `TutorialAnalyticsSteps`.

### Нет — покрытия основного цикла

Задача формулируется не как «добавить аналитику», а как **«покрыть событиями дневной цикл»**,
и это надо сделать **до первого A/B**, иначе мерить нечем.

Минимальный набор под текущую игру:

| Событие | Параметры | Зачем |
|---|---|---|
| `day_started` | day, location_id, gold_before | Знаменатель для всех дневных метрик |
| `day_completed` | day, location_id, gold_earned, customers_total, passive_sales, active_sales | Основная метрика успеха дня |
| `minigame_shown` | request_id, customer_genres | **Exposure** для экспериментов над активной продажей |
| `minigame_result` | request_id, success, book_id, genre | Успешность миниигры |
| `customer_left_empty` | reason | Диагностика провалов пассива |
| `quest_completed` / `location_unlocked` | id | Якоря прогрессии для retention |

### Два блокера, всплывающие именно на A/B

- [IPlayerIdentityProvider.cs:3](../../Assets/Game/Infrastructure/Analytics/IPlayerIdentityProvider.cs)
  — `TODO`, фолбэк на `SystemInfo.deviceUniqueIdentifier`. Стабильность идентификатора
  определяет, останется ли игрок в своей группе между сессиями и переустановками.
  Решить **до** первого эксперимента.
- `StubAnalyticsConsentService` — согласие заглушка. До релиза в стор (GDPR) должно стать
  настоящим.

---

## 5. План действий по ROI

### 1. Вынести `SalesTuning` + `SalesTrafficSettings` в секцию конфигов

Новый POCO `[ConfigFile("sales_tuning")]` с одной записью `id: "default"`; `ScriptableObject`
остаётся fallback-ом при отсутствии секции.

По собственной шкале [ADR-0002 §extension 1](../adr/0002-config-system-architecture.md) это
«новый тип конфига → **Trivial, ~1 час**». Одним движением открывает под RC весь баланс дня.
**Лучший ход в списке с большим отрывом.**

### 2. `exp_variant` в common-параметры + событие `experiment_exposure`

~полдня, `SetUserProperty` уже есть. Без этого пункт 1 бесполезен: крутить сможешь, мерить нет.

### 3. Логировать `rc_source` (Remote / Static) в common-params

Уже почти есть — сейчас это `Debug.Log` внутри `FirebaseRemoteConfigService.TryGetString`,
надо поднять в контекст аналитики. Закрывает §2.4: видно, кто из игроков реально получил вариант.

### 4. Покрыть дневной цикл событиями из §4

Делать до первого эксперимента, не после.

### 5. Починить мёртвый RC-ключ `cfg_books` (§3.2)

Тривиально, но пока не сделано — RC-контур не проверен end-to-end ни разу.

### Чего делать НЕ надо

**Reactive refresh** ([ADR-0002 §extension 9](../adr/0002-config-system-architecture.md), High).
Для A/B он не просто не нужен — он вреден: вариант обязан быть стабильным внутри сессии, а
текущая immutability после warmup это гарантирует бесплатно.

---

## 6. Когда пересматривать этот документ

- После выноса `SalesTuning` в конфиги — переписать §2.1 и оценку «поверхность эксперимента».
- После добавления `exp_variant` — переписать §2.2 и оценку «измерение».
- Если появится потребность в экспериментах над **составом** секций (а не полями) — поднимать
  [ADR-0002 §extension 3](../adr/0002-config-system-architecture.md) (`cfg_<file>_global`).
- Если Firebase A/B Testing окажется недостаточным (нужен эксперимент, невыразимый через
  partial-override) — тогда и только тогда обсуждать серверное бакетирование.
