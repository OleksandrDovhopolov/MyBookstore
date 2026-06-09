# Config System (MyBookstore)

Живой документ системы data-driven конфигов MyBookstore. **Статус: реализовано в проде**
(модуль `Assets/Game/Features/Configs/`, подключён через `BootstrapInstaller`).

- Архитектурное решение и extension paths: [ADR-0002 (client)](adr/0002-config-system-architecture.md).
- Серверный контракт: [CONFIG_SERVER_API.md](CONFIG_SERVER_API.md) (источник истины — на стороне backend).
- Editor-инструмент публикации: [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](CONFIG_EDITOR_WINDOW_MVP_SPEC.md).

Этот документ — практический справочник для разработчика: как устроен runtime-путь
загрузки конфигов в клиенте и как с ним работать.

---

## 1. Что есть и куда смотреть

**Реализованный runtime-флоу:**

```
IConfigsService.Get<T>(id)
        │
        ▼
  ConfigCache<IConfig>    (in-memory, ленивая десериализация по типу)
        │ miss
        ▼  base → override:
  1. Base:     IConfigSource     (LocalFolder в Editor / Server в build)
  2. Override: IConfigOverrideSource  (Firebase RC partials, мёрж поверх base)
```

**Контракт** ([IConfigsService.cs](../Assets/Game/Features/Configs/IConfigsService.cs)):

```csharp
UniTask WarmupAsync(CancellationToken ct);     // прогрев на бутстрапе
T       Get<T>(string id);                      // sync из кэша
bool    TryGet<T>(string id, out T config);
UniTask<T> GetAsync<T>(string id);              // ждёт прогрев и возвращает
bool    IsExists<T>(string id);
IReadOnlyList<T> GetAll<T>();                   // все конфиги типа
```

Все методы — `where T : class, IConfig`. Конфиги immutable после `WarmupAsync` (см. ADR §11
«reactive refresh» — отложено сознательно).

**Конкретные конфиги** (Notion-задача «Data-driven конфиги»):

| Тип | Файл | Путь модели |
|-----|------|-------------|
| `BookConfig` | `books.json` | [Models/BookConfig.cs](../Assets/Game/Features/Configs/Models/BookConfig.cs) |
| `LocationConfig` | `locations.json` | [Models/LocationConfig.cs](../Assets/Game/Features/Configs/Models/LocationConfig.cs) |
| `RequestConfig` | `requests.json` | [Models/RequestConfig.cs](../Assets/Game/Features/Configs/Models/RequestConfig.cs) |
| `EventConfig` | `events.json` | [Models/EventConfig.cs](../Assets/Game/Features/Configs/Models/EventConfig.cs) |

Файл секции = JSON-массив объектов одного типа; индексируется по `id` (case-insensitive).
Шаблоны — [Assets/Configs/*.json](../Assets/Configs/).

---

## 2. Источники (`IConfigSource`)

| Реализация | Когда | Что делает |
|---|---|---|
| `LocalFolderConfigSource` | Editor по умолчанию | Читает `Assets/Configs/*.json`. Правка JSON → Play → видно сразу |
| `ServerConfigSource` | Build всегда; Editor через тумблер | Слои: bundled defaults → disk snapshot → server (manifest + delta по ETag) |

**Server-флоу** ([ServerConfigSource.cs](../Assets/Game/Features/Configs/Server/ServerConfigSource.cs)):

1. Warmup bundled defaults (baseline).
2. Overlay disk snapshot из `Application.persistentDataPath/configs/`.
3. `GET /api/v1/configs/manifest` → для каждой секции сравниваем etag со snapshot'ом → качаем изменившееся через `GET /api/v1/configs/{name}` с `If-None-Match` (304 = пропуск).
4. Перезаписываем snapshot. На сеть-фейле остаёмся на snapshot. Без snapshot'а — bundled defaults.

Транспорт — `Game.Http`-стек: `GetConfigsManifestCommand` / `GetConfigCommand`,
internet-check и ретраи через `ConnectionService`.

**ETag канонизируется** ([GetConfigCommand.NormalizeEtag](../Assets/Game/Features/Configs/Server/Commands/GetConfigCommand.cs)) — срез `W/` и обрамляющих кавычек. Нужно, потому что сервер шлёт etag в кавычках в заголовке и без кавычек в манифесте; одна точка истины используется и в Editor.

**Тумблер для Editor:** `Tools/Configs/Use Server Source` (EditorPref). Default — local folder.
**Сброс снапшота:** `Tools/Configs/Clear Server Snapshot` (после правки сервера руками).

---

## 3. Override-слой (Firebase Remote Config)

RC — **не вторая система конфигов**, а partial-overlay поверх base. A/B, таргетинг по
аудиториям, фиче-флаги, gradual rollout — на стороне Firebase Console, без клиентского
кода на эксперимент.

- Контракт отвязан от Firebase SDK через `IRemoteConfigService` ([Remote/IRemoteConfigService.cs](../Assets/Game/Features/Configs/Remote/IRemoteConfigService.cs)).
- `RemoteConfigOverrideSource` ([Remote/RemoteConfigOverrideSource.cs](../Assets/Game/Features/Configs/Remote/RemoteConfigOverrideSource.cs)) мёржит partial поверх base при десериализации.
- **Конвенция ключей RC:** ключ `cfg_<fileName>` хранит JSON `{ "<id>": { ...partial... } }`. Подчёркивание, не точка — Firebase RC допускает только буквы/цифры/`_`.
- Firebase-реализация (`FirebaseRemoteConfigService`, [Bootstrap/FirebaseRemoteConfigService.cs](../Assets/Game/Core/Installers/Bootstrap/FirebaseRemoteConfigService.cs)) и `RemoteConfigLoader.FetchAndActivateAsync` — **за define `BOOKSTORE_FIREBASE_RC`**. До его включения активна `NullRemoteConfigService` (нет override'ов, сборка не ломается).

**Правило разделения:** большой структурный контент → сервер; эксперименты/флаги/скаляры → RC. Один ключ **не дублируется** в обоих слоях.

---

## 4. Прогрев и DI

[ConfigsWarmupEntryPoint.cs](../Assets/Game/Core/Installers/Bootstrap/ConfigsWarmupEntryPoint.cs) — `IAsyncStartable`:
1. `IRemoteConfigService.InitializeAsync` (Firebase fetch + activate — если включён define).
2. `IConfigsService.WarmupAsync` (manifest + delta + snapshot).
3. После этого `Get<T>` работает синхронно из кэша; RC-override'ы применяются при ленивой десериализации.

Если в сцене entry points не диспатчатся — `GetAsync<T>` прогреет источник лениво при первом обращении (страховка для тестов и нестандартных сцен).

Регистрация — `RegisterConfigs` в [ConfigsVContainerBindings.cs](../Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs), вызывается из [BootstrapInstaller.cs](../Assets/Game/Core/Installers/Bootstrap/BootstrapInstaller.cs) (GlobalLifetimeScope, переживает смену сцен). Всё через DI (VContainer), без статических `Instance`.

---

## 5. Режимы работы

| Режим | Base source | Override-слой |
|-------|-------------|---------------|
| Editor (Play), default | `LocalFolderConfigSource` | RC (если define включён) |
| Editor (Play), тумблер | `ServerConfigSource` | RC |
| Build (runtime) | `ServerConfigSource` (всегда) | RC |

Build читает `prod`. Dev доступен только через ручной query в URL (см. ADR §extension 2).

---

## 6. Как обновить конфиг

| Ситуация | Что делать |
|----------|-----------|
| Локальная разработка | Поправить JSON в `Assets/Configs/`, перезапустить Play (источник = LocalFolder) |
| Проверить серверный флоу локально | `Tools/Configs/Use Server Source` → Play (источник = Server) |
| Опубликовать игрокам | Editor Window: `Tools → Configs → Editor Window` → Pull/правка/Publish (см. spec) |
| Эксперимент / флаг / таргетинг | Firebase Console, RC-ключ `cfg_<file>`, без ребилда |
| Откат правки | Editor Window → History → Rollback (новая версия = копия выбранной) |
| Промоут dev → prod | Editor Window → `Promote to Prod` |

После publish окно автоматически очищает локальный server snapshot — следующий Play
сразу видит свежее значение.

---

## 7. Карта файлов

| Компонент | Путь |
|-----------|------|
| Контракт сервиса | `Assets/Game/Features/Configs/IConfigsService.cs` |
| Реализация | `Assets/Game/Features/Configs/ConfigsService.cs` |
| Кэш | `Assets/Game/Features/Configs/ConfigCache.cs` |
| Базовый контракт конфига | `Assets/Game/Features/Configs/IConfig.cs`, `ConfigFileAttribute.cs` |
| Источник: локальная папка | `Assets/Game/Features/Configs/LocalFolderConfigSource.cs` |
| Источник: сервер + snapshot | `Assets/Game/Features/Configs/Server/ServerConfigSource.cs` |
| Серверные команды | `Assets/Game/Features/Configs/Server/Commands/*.cs` |
| Серверный конфиг URL/путей | `Assets/Game/Features/Configs/Server/ConfigsBackendConfig.cs` |
| Override / RC | `Assets/Game/Features/Configs/Remote/*.cs` + `Bootstrap/FirebaseRemoteConfigService.cs` |
| Модели | `Assets/Game/Features/Configs/Models/*.cs` |
| Примеры JSON | `Assets/Configs/*.json` |
| DI-регистрация + тумблеры | `Assets/Game/Core/Installers/Features/ConfigsVContainerBindings.cs` |
| Прогрев | `Assets/Game/Core/Installers/Bootstrap/ConfigsWarmupEntryPoint.cs` |
| Assembly | `Assets/Game/Features/Configs/Configs.asmdef` |
| Editor-окно | `Assets/Game/Features/Configs/Editor/*.cs` (`Configs.Editor.asmdef`, Editor-only) |

---

## 8. Что сознательно отложено (deferred)

По ADR-0002, не делаем сейчас. Каждое — взять, когда понадобится:

| Фича | Зачем может понадобиться | Сложность |
|------|--------------------------|-----------|
| Per-section CRC + delta-синк | Когда секций 100+ и важна именно дельта | High |
| MessagePack + Addressables-паки | Когда конфиги станут жирными | High |
| LRU-кэш паков (`LimitedCache`) | Только вместе с Addressables-паками | Medium (часть выше) |
| Force-invalidation по `Application.version` | Breaking-смены модели данных | Low |
| Existence-dictionary (кэш наличия) | Микрооптимизация после профайлера | Trivial |
| Большой типизированный `StaticData` (`[Key(N)]`×100) | Закрытый набор секций, известный на compile-time | High |
| Reactive refresh в runtime | Push новой версии без перезапуска | High (см. ADR §extension 9) |
| Schema-валидация секций | Хочется JSON Schema + codegen | High (см. ADR §extension 8) |

Полные критерии «когда пересматривать» — в [ADR-0002 §When to revisit](adr/0002-config-system-architecture.md).

---

## 9. Включение Firebase RC

1. Импортировать `FirebaseRemoteConfig_*.unitypackage`.
2. Добавить define `BOOKSTORE_FIREBASE_RC` (Project Settings → Player → Scripting Define Symbols).
3. Пересобрать — регистрация автоматически переключится на `FirebaseRemoteConfigService`, `RemoteConfigLoader.FetchAndActivateAsync` станет рабочим.

После этого в Firebase Console публикуй ключи `cfg_<fileName>` со значением вида
`{"<id>":{ "<field>": <value> }}`. См. RC-override-слой §3.
