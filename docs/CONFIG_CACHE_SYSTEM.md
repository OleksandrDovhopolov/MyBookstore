# Config System (MyBookstore)

Живой документ системы data-driven конфигов MyBookstore. Покрывает: реализованный
функционал, сознательно отложенное, и спецификацию серверных методов для backend.

Часть Notion-задачи **«Data-driven конфиги (BookConfig, LocationConfig, RequestConfig, EventConfig)»**.

> История: ранее тут лежал референс чужого прод-проекта (`ConfigCache<T>`, `StaticData`,
> Addressables-паки). Полезные идеи перенесены в этот документ; неактуальное вынесено
> в раздел «Отложено».

---

## 1. Архитектура (реализовано)

**Один контракт, три слоя, два источника.** Всё резолвится за единым `IConfigsService` —
потребитель не знает, откуда пришло значение. Это устраняет проблему «двух несвязанных
систем конфигов».

```
IConfigsService.Get<T>(id)
        │
        ▼
  ConfigCache<IConfig>   (in-memory, ленивая десериализация по типу)
        │ miss
        ▼  base → override:
  1. Base layer   = IConfigSource  (LocalFolder в Editor / Server в build)
  2. Override layer = IConfigOverrideSource (Firebase RC partial, мёрж поверх base)
```

### Контракт

`IConfigsService` ([IConfigsService.cs](../Assets/Game/Features/Configs/IConfigsService.cs)):

```csharp
UniTask WarmupAsync(CancellationToken ct);          // прогрев источника на бутстрапе
T       Get<T>(string id);                           // sync из кэша
bool    TryGet<T>(string id, out T config);
UniTask<T> GetAsync<T>(string id);                   // гарантирует прогрев, затем Get
bool    IsExists<T>(string id);
IReadOnlyList<T> GetAll<T>();                         // все конфиги типа (напр. все книги)
```

Все методы — `where T : class, IConfig`. Конфиги immutable после `WarmupAsync`.

### Конфиги

Каждый конфиг — POCO с `Id`, помеченный `[ConfigFile("<name>")]` (маппинг тип → файл):

| Тип | Файл | Путь модели |
|-----|------|-------------|
| `BookConfig` | `books.json` | [Models/BookConfig.cs](../Assets/Game/Features/Configs/Models/BookConfig.cs) |
| `LocationConfig` | `locations.json` | [Models/LocationConfig.cs](../Assets/Game/Features/Configs/Models/LocationConfig.cs) |
| `RequestConfig` | `requests.json` | [Models/RequestConfig.cs](../Assets/Game/Features/Configs/Models/RequestConfig.cs) |
| `EventConfig` | `events.json` | [Models/EventConfig.cs](../Assets/Game/Features/Configs/Models/EventConfig.cs) |

> Поля моделей сейчас иллюстративны — финальный набор выравнивается со спекой Notion-задачи.

Файл конфига = JSON-массив объектов одного типа; индексируется по `id` (case-insensitive).
Пример: [Assets/Configs/books.json](../Assets/Configs/books.json).

### Режимы загрузки

| Режим | Base source | Поведение |
|-------|-------------|-----------|
| Editor (Play), по умолчанию | `LocalFolderConfigSource` | Читает `Assets/Configs/*.json` напрямую. Правка JSON → Play → видно сразу |
| Editor (Play), тумблер | `ServerConfigSource` | `Tools/Configs/Use Server Source` (EditorPref) — проверка интеграции/версий/fallback |
| Build (runtime) | `ServerConfigSource` | Сервер + disk snapshot fallback, всегда |

Выбор источника — в [ConfigsVContainerBindings.cs](../Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs) через `#if UNITY_EDITOR`.

### Источники (`IConfigSource`)

- **`LocalFolderConfigSource`** ([файл](../Assets/Game/Features/Configs/LocalFolderConfigSource.cs)) — читает все `*.json` из папки (по умолчанию `Application.dataPath/Configs`).
- **`ServerConfigSource`** ([файл](../Assets/Game/Features/Configs/Server/ServerConfigSource.cs)) — слои от младшего к старшему: **bundled defaults → disk snapshot → server**.
  - Bootstrap: warmup bundled defaults → overlay snapshot (`persistentDataPath/configs/`) → GET `/configs/manifest` → для изменившихся секций GET `/configs/{name}` c `If-None-Match` (304 = оставить snapshot) → перезапись snapshot.
  - Сеть упала → snapshot; снапшота нет → bundled defaults.
  - Транспорт — существующий стек `Game.Http` (`AbstractServiceCommand`, `ConnectionService`: проверка интернета, ретраи). Команды: [GetConfigsManifestCommand](../Assets/Game/Features/Configs/Server/Commands/GetConfigsManifestCommand.cs), [GetConfigCommand](../Assets/Game/Features/Configs/Server/Commands/GetConfigCommand.cs).

### Override-слой (Firebase RC)

RC — **не вторая система конфигов**, а override-слой поверх base (концепт `_partial`/merge).
A/B, таргетинг по аудиториям, фиче-флаги, gradual rollout, дашборды — на стороне Firebase
console, без клиентского кода на эксперимент.

- Контракт отвязан от Firebase SDK через `IRemoteConfigService` ([файл](../Assets/Game/Features/Configs/Remote/IRemoteConfigService.cs)).
- `RemoteConfigOverrideSource` ([файл](../Assets/Game/Features/Configs/Remote/RemoteConfigOverrideSource.cs)) мёржит partial поверх base при десериализации.
- **Конвенция ключей RC**: ключ `cfg_<fileName>` хранит JSON `{ "<id>": { ...partial... } }`. Подчёркивание, а не точка — ключи Firebase RC допускают только буквы/цифры/`_`.
- Firebase-реализация (`FirebaseRemoteConfigService`, [файл](../Assets/Game/Core/Installers/Bootstrap/FirebaseRemoteConfigService.cs)) и `RemoteConfigLoader.FetchAndActivateAsync` — за define **`BOOKSTORE_FIREBASE_RC`**. До его включения активна `NullRemoteConfigService` (нет override'ов, сборка не ломается).

**Правило разделения:** большой структурный контент → сервер; эксперименты/флаги/скаляры → RC.
Один ключ не дублируется в обоих.

### Прогрев и DI

`ConfigsWarmupEntryPoint` ([файл](../Assets/Game/Core/Installers/Bootstrap/ConfigsWarmupEntryPoint.cs)) — `IAsyncStartable`:
сначала `IRemoteConfigService.InitializeAsync` (RC активируется до первого Get), затем
`IConfigsService.WarmupAsync`. Если entry points не диспатчатся в сцене — `GetAsync` прогреет
источник лениво.

Регистрация — `RegisterConfigs` в [ConfigsVContainerBindings.cs](../Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs),
вызывается из [BootstrapInstaller.cs](../Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.cs)
(GlobalLifetimeScope, переживает смену сцен). Всё через DI (VContainer), без статических `Instance`.

### Карта файлов

| Компонент | Путь |
|-----------|------|
| Контракт сервиса | `Assets/Game/Features/Configs/IConfigsService.cs` |
| Реализация | `Assets/Game/Features/Configs/ConfigsService.cs` |
| Кэш | `Assets/Game/Features/Configs/ConfigCache.cs` |
| Базовый контракт конфига | `Assets/Game/Features/Configs/IConfig.cs`, `ConfigFileAttribute.cs` |
| Источник: локальная папка | `Assets/Game/Features/Configs/LocalFolderConfigSource.cs` |
| Источник: сервер + snapshot | `Assets/Game/Features/Configs/Server/ServerConfigSource.cs` |
| Серверные команды | `Assets/Game/Features/Configs/Server/Commands/*.cs` |
| Серверный конфиг (URL/пути) | `Assets/Game/Features/Configs/Server/ConfigsBackendConfig.cs` |
| Override / RC | `Assets/Game/Features/Configs/Remote/*.cs` + `Bootstrap/FirebaseRemoteConfigService.cs` |
| Модели | `Assets/Game/Features/Configs/Models/*.cs` |
| Примеры JSON | `Assets/Configs/*.json` |
| DI-регистрация + тумблер | `Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs` |
| Прогрев | `Assets/Game/Core/Installers/Bootstrap/ConfigsWarmupEntryPoint.cs` |
| Assembly | `Assets/Game/Features/Configs/Configs.asmdef` |

---

## 2. Отложено (deferred) — что НЕ делаем в MVP

Сознательно упрощено относительно большого прод-проекта. Каждое — взять, когда реально упрёмся:

| Фича | Зачем может понадобиться |
|------|--------------------------|
| Per-section CRC + delta-синк | Когда секций 100+ и важно качать строго дельту. Сейчас хватает ETag/`If-None-Match` per-file |
| MessagePack + Addressables-паки | Когда конфиги станут жирными и JSON-трафик/парсинг начнёт мешать |
| LRU-кэш паков (`LimitedCache`) | Только вместе с Addressables-паками (защита от OOM) |
| Force-invalidation по `Application.version` | Когда breaking-меняется модель данных. Сейчас закрывается бампом version в manifest |
| Existence-dictionary (кэш наличия) | Микрооптимизация; добавить после профайлера |
| Два хаба (Json + Scriptables) | Когда появятся SO-конфиги наряду с JSON |
| Большой типизированный `StaticData` (`[Key(N)]`×100) | Паттерн под закрытый набор секций, известный на compile-time. Сейчас проще `name → JSON` + ленивая десериализация |

---

## 3. Backend API spec (для передачи backend)

> **Полная самодостаточная спека для backend-разработчика:** [CONFIG_SERVER_API.md](CONFIG_SERVER_API.md).
> Ниже — краткая выжимка.

Сервер: `https://gameserver-production-be8b.up.railway.app` (тот же, что для сейвов).
**Public-методы реализованы и подключены** — `ServerConfigSource` ходит на `/api/v1/configs/*`
(environment по умолчанию `prod`). ETag канонизируется на клиенте (срез кавычек/`W/`),
т.к. сервер отдаёт его в кавычках в заголовке и без кавычек в манифесте.
**Admin-методы (`/api/admin/configs/*`) тоже реализованы**: `GET / PUT (If-Match) / history /
rollback / promote`, Basic auth (env `ADMIN_USER`/`ADMIN_PASS`). До появления Editor-окна
правки публикуются через Postman/curl — рабочие примеры в [CONFIG_SERVER_API.md §10](CONFIG_SERVER_API.md).

### Public (читает клиент)

| Метод | Запрос | Ответ |
|-------|--------|-------|
| `GET /configs/manifest` | — | `200` → `[{ "name": "books", "version": 13, "etag": "..." }, ...]` |
| `GET /configs/{name}` | заголовок `If-None-Match: <etag>` (опц.) | `200` + заголовок `ETag` → JSON-массив конфигов; `304` если не изменился |

Клиент сравнивает `version`/`etag` манифеста с локальным snapshot и качает только изменившееся.

### Admin (editor-publish, прод-grade) — реализуется вместе с Editor-окном Pull/Publish

| Метод | Назначение |
|-------|-----------|
| `PUT /admin/configs/{name}` | Публикация. Заголовок `If-Match: <etag>` — **optimistic concurrency**: если на сервере уже новее → `412 Precondition Failed`, клиент должен пул+мёрдж |
| `GET /admin/configs/{name}/history` | Список версий |
| `POST /admin/configs/{name}/rollback?to=<version>` | Откат |

Требования к admin: dev/staging/prod environments; версии + история + rollback; аудит
(`updated_by`, `updated_at`, `comment`); per-GD auth-токен; diff перед публикацией;
прод-publish с явным подтверждением.

### Модель хранения (минимум)

Одна таблица:

```
configs(
  name        text,
  environment text,        -- dev | staging | prod
  version     bigint,      -- инкремент при публикации
  etag        text,        -- хэш контента
  json        text,        -- сам конфиг (JSON-массив)
  updated_by  text,
  updated_at  timestamptz,
  comment     text,
  PRIMARY KEY (name, environment, version)
)
```

`manifest` = последняя version каждого `name` в нужном `environment`.
**Отложено на бэке** (не требуется сейчас): per-section CRC/delta, бинарные паки.

---

## 4. Как обновить конфиг

| Ситуация | Что делать |
|----------|-----------|
| Локальная разработка | Поправить JSON в `Assets/Configs/`, перезапустить Play (источник = LocalFolder) |
| Проверить серверный флоу локально | `Tools/Configs/Use Server Source` → Play |
| Выкатить игрокам (после реализации admin-методов) | Editor: Pull → правка → Publish в dev → QA → Promote в prod |
| Эксперимент / флаг / таргетинг | Firebase console (RC-ключ `cfg.<file>`), без ребилда |

---

## 5. Включение Firebase RC

1. Импортировать `FirebaseRemoteConfig_*.unitypackage`.
2. Добавить define `BOOKSTORE_FIREBASE_RC` (Project Settings → Player → Scripting Define Symbols).
3. Пересобрать — регистрация автоматически переключится на `FirebaseRemoteConfigService`,
   `RemoteConfigLoader.FetchAndActivateAsync` станет рабочим.
