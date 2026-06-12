# ADR-0002: Config system — client architecture

- **Status:** Accepted
- **Date:** 2026-06-09
- **Deciders:** project owner
- **Supersedes:** —
- **Related:** [CONFIG_CACHE_SYSTEM.md](../CONFIG_CACHE_SYSTEM.md), [CONFIG_SERVER_API.md](../CONFIG_SERVER_API.md), [CONFIG_EDITOR_WINDOW_MVP_SPEC.md](../CONFIG_EDITOR_WINDOW_MVP_SPEC.md), server-side [ADR-001-config-section-snapshot-storage](C:\Users\Admin\RiderProjects\GameServer\docs\ADR-001-config-section-snapshot-storage.md)

## Context

Игре нужны data-driven конфиги (`BookConfig`, `LocationConfig`, `RequestConfig`, `EventConfig` — из Notion-задачи), которые:

- редактируются ГД без ребилда клиента,
- версионируются и подлежат rollback,
- поддерживают A/B-эксперименты и таргетинг на soft launch,
- работают оффлайн (после первого fetch),
- не повторяют ошибки старого проекта (Google Sheets → JSON → ребилд; «двое правят один файл»; две несвязанные системы конфигов — статика через Resources + Firebase RC через отдельный провайдер; см. ранее существовавший `docs/config-storage-analysis.md`).

Ограничения, которые формировали решение:

- MVP, один разработчик.
- Свой .NET backend уже есть и используется для сейвов (`gameserver-production-be8b.up.railway.app`).
- Firebase Analytics + Crashlytics уже подключены; Remote Config доступен.
- Unity 6, VContainer для DI, UniTask, Newtonsoft.Json.
- Строгие asmdef-правила: модули не должны зависеть друг от друга прямо, infrastructure не должна зависеть от features.
- Конфиги, скорее всего, будут расти по количеству секций и полей.
- Нужно успеть отрезать функциональность от Editor-инструментов (publish), чтобы прод-код не тащил editor-only зависимости.

## Decision

Принять четырёхслойную архитектуру:

1. **Source-layer** — поставщик сырых JSON-файлов секций: `LocalFolderConfigSource` (Editor/dev) и `ServerConfigSource` (runtime; ETag + disk snapshot fallback + bundled defaults). Контракт — `IConfigSource.GetRaw(string fileName)`.
2. **Override-layer** — Firebase Remote Config как partial-overlay поверх base через `IConfigOverrideSource` (реализация — `RemoteConfigOverrideSource`). RC-ключ `cfg_<fileName>` → JSON `{ "<id>": { ...partial... } }`, мёрджится при ленивой десериализации.
3. **Service-layer** — единый `IConfigsService` с типобезопасным `Get<T>`/`TryGet<T>`/`GetAsync<T>`/`IsExists<T>`/`GetAll<T>`. Реализация (`ConfigsService`) ленива: тип десериализуется при первом обращении.
4. **Editor-layer** — отдельный Editor-only сборочный проект `Configs.Editor` для admin-инструмента (Pull / Publish / History / Rollback / Promote). НЕ переиспользует `Game.Http`-команды: см. отступление ниже.

Production-источник (`ServerConfigSource`) и serverbound команды (`GetConfigsManifestCommand`, `GetConfigCommand`) построены на существующем `AbstractServiceCommand` / `Game.Http`-стеке — те же ретраи и проверки сети, что у сейвов.

Admin-инструмент (`Configs.Editor`) намеренно использует прямой `UnityWebRequest`, потому что:
- `Game.Http.HTTPMethods` поддерживает только `Get`/`Post`, а Admin API требует `PUT` и кастомные заголовки (`Authorization: Basic`, `If-Match`);
- расширять прод-код ради editor-only фичи дороже, чем дублировать ~150 строк HTTP-обёртки;
- `IConnectionService` (internet check, no-internet retry behaviour, signal bus) — это инфраструктура для мобильного игрока, в Editor бессмысленна;
- единая точка истины канонизации ETag вынесена в `GetConfigCommand.NormalizeEtag`, и Editor-сборка ссылается на неё, чтобы поведение совпадало.

## Consequences

### Positive

- **Одна публичная точка** (`IConfigsService.Get<T>`) для потребителя — фича не знает, откуда пришло значение (локальная папка, сервер, snapshot, RC). Это явно решает проблему «двух несвязанных систем».
- **Слои изолированы**: смена транспорта (`HTTP` → `gRPC`/`WebSocket`) — это новая реализация `IConfigSource`. Смена override-механизма (RC → in-house feature flags) — новая `IConfigOverrideSource`. Ни service-layer, ни потребители не меняются.
- **Свобода эксперимента без ребилда** через RC покрывает скаляры/флаги/таргетинг; большой контент держится на сервере с историей и rollback.
- **Offline-first**: snapshot на диске + bundled defaults гарантируют, что Get никогда не вернёт «ничего», даже без сети.
- **DI вместо синглтонов** — нет `Instance` антипаттерна, который засветился в анализируемом legacy-проекте.
- **Editor-tool отрезан** от прод-кода через свой asmdef (`includePlatforms: ["Editor"]`) — в билд не попадает.

### Negative

- **Дублирование HTTP-логики** в Editor (UnityWebRequest напрямую) — ~150 строк, потенциальный drift с прод-командами. Снижено тем, что нормализация ETag вынесена в shared utility.
- **Lazy-десериализация на главном потоке** — для текущих размеров секций (десятки KB) приемлемо; на крупных секциях парсинг может дать кадровый просадк.
- **RC ключ-конвенция жёстко зашита** (`cfg_<fileName>`) — это договорённость, нет схемного контракта. Опечатка ключа в Firebase Console не валидируется.
- **«Один большой массив на секцию»** — наследие из server-side ADR-001: добавление одной книги = publish всего массива. На клиенте это значит более жирные PUT-payloads на больших секциях.
- **Один уровень environment** (dev/prod) определяется через editor-side выбор, прод-клиент жёстко читает `prod`. Сложнее поднять staging-сегмент игроков без правки клиента.

### Что НЕ выбрано и почему

1. **Жёстко типизированный `StaticData` с `[Key(N)]`×100 + MessagePack-pack** (как в legacy big-game проекте). Дороже на инфраструктуру (binary delta, CRC per-section), оправдано только на сотнях секций.
2. **Конфиги через ScriptableObject в Resources/Addressables.** Не решает версии/rollback/parallel-editing, и любая правка ассета требует ребилда.
3. **`IConfigsService` без override-слоя + отдельный `IFeatureFlags` для RC.** Возвращает к проблеме «двух несвязанных систем»: потребители должны вызывать обе и мёрджить руками.
4. **Реализация Admin-API через `Game.Http`-команды.** См. выше, отступление обоснованное; пересмотр имеет смысл, если появится не-editor admin-сценарий (например, in-game админ-панель для саппорта).

## Extension paths and complexity

Архитектура заложена так, чтобы расширение шло **через новые реализации интерфейсов**, без правки потребителей. Ниже — конкретные сценарии, что нужно добавить и насколько это дорого.

### 1. Новый тип конфига (например, `ShopConfig`)
**Что делать:** POCO с `Id` + `[ConfigFile("shop")]`, JSON-файл, publish через Editor Window.
**Сложность:** **Trivial (~1 час)**. Изменений в инфраструктуре ноль.

### 2. Новый environment (`staging`)
**Что делать на клиенте:** добавить enum/option в `ConfigEditorWindow` toolbar; для рантайма — параметризовать `ServerConfigSource` (сейчас хардкодит `prod`). На бэке `staging` уже работает без изменений (whitelist отсутствует, см. server API §4).
**Сложность:** **Low (~3 часа)**. Главное — придумать, как клиент выбирает environment (build-flag, query string, env variable). После выбора — ~10 строк правок.

### 3. Лента «реальных» partial-конфигов в RC (не только {id: {…}})
**Что делать:** усложнить `RemoteConfigOverrideSource` — поддерживать ключи вроде `cfg_books_global` (применяется ко всем элементам) или `cfg_global` (мёржится поверх всех секций). Это вопрос правил мёрджа в `ConfigsService.DeserializeType`.
**Сложность:** **Low (~4 часа)** при ясном definition правил мёрджа. Нужно тщательно покрыть merge-семантику тестами на edge cases (null, массив vs объект).

### 4. Multi-language / localization секций
**Что делать:** добавить `IConfigSource`-слой `LocalizedConfigSource`, который мёржит языковой overlay (`books_ru.json`) поверх base. Либо — RC-overrides под язык как audience target.
**Сложность:** **Medium (~1 день)** если файлы; **Low (~3 часа)** если через RC audience targeting.

### 5. Editor: typed-форма для остальных секций (locations/requests/events)
**Что делать:** написать дополнительные `*ItemDrawer` по аналогии с `BooksItemDrawer`. Точка расширения уже есть — `ConfigEditorWindow.DrawRightPane` switch'ит по `Section`.
**Сложность:** **Low (~2 часа на секцию)** при стабильной модели полей.

### 6. Editor: diff viewer pulled vs working
**Что делать:** новое окно/панель с side-by-side рендером двух JArray. Самый трудный кусок — UI рендеринга diff.
**Сложность:** **Medium (~2 дня)**. Использовать существующий `pulledSnapshotJson` для левой колонки и `WorkingArray` для правой.

### 7. Auto-merge при `412`
**Что делать:** при конфликте Pull свежей версии + структурный merge по `id` (3-way merge: base = последний наш pulled, local = working, remote = свежий). Конфликты по полям одного объекта остаются manual.
**Сложность:** **High (~3–5 дней)**. Сложность не в коде, а в edge cases: переименование `id`, удаление в одной стороне + правка в другой, конфликты типов.

### 8. Schema-валидация секций (например, через JSON Schema)
**Что делать:** новый `ISectionSchema` per-section с серверными или embedded-схемами; валидация перед `PUT` и при `GET`. На бэке — отдельный endpoint для схем.
**Сложность:** **High (~1 неделя)** с учётом серверной части. На клиенте использовать `JSchema` (Newtonsoft.Json.Schema).

### 9. Reactive refresh — повторный pull во время работы игры
**Что делать:** `IConfigsService` уже immutable после warmup; нужно или ввести `IObservable<ConfigsChanged>`, или делать смену через `IRuntimeReload` + перерегистрировать DI. На бэке — push-уведомление о новой версии или polling manifest.
**Сложность:** **High (~1 неделя)**. Требует пересмотра immutability-гарантий. Сейчас этого избегаем сознательно (см. CONFIG_CACHE_SYSTEM.md).

### 10. Editor admin-токен на пользователя (вместо общего env-credentials)
**Что делать:** заменить single Basic на per-GD JWT. На клиенте — JWT-flow с refresh. На бэке — auth-сервис.
**Сложность:** **High (~1–2 недели)** при необходимости в полноценном auth-флоу; **Medium (~3 дня)** если можно ограничиться long-lived bearer без refresh.

### 11. Заменить Editor-side HTTP на `Game.Http`-команды
**Что делать:** расширить `HTTPMethods` (PUT/DELETE), добавить headers-API в `IRequest`, написать `PutConfigCommand`/`PromoteConfigCommand`/etc. Editor-окно переезжает на них.
**Сложность:** **Medium (~2 дня)**. Имеет смысл только если появится in-game admin-инструмент.

### 12. Server: переход к pluggable storage (Redis, blob storage)
**Что делать:** не на клиенте. См. server-side ADR-001 «когда пересматривать».
**Сложность для клиента:** **Zero** — контракт API не меняется.

## When to revisit

Архитектуру (этот ADR) стоит пересмотреть, если:

- **Количество секций превысит ~30** — может потребоваться авто-регистрация типов или генерация моделей.
- **Появится сервер-аутентификативная логика, которая читает конкретные поля конфига** — opaque JSON станет узким местом, потребуется shared schema (см. ADR-0001 параллель).
- **Размер секции стабильно превысит 100 KB** — встанет вопрос о delta-протоколе и pack-сжатии.
- **Игроки начнут страдать от парсинг-просадков** — придётся переходить на background-десериализацию или MessagePack.
- **Появится в нескольких клиентах (web, ops dashboard)** — admin API понадобится OpenAPI-спека и codegen; editor-only UnityWebRequest будет одной из реализаций.

## Implementation notes

- Решение реализовано в `Assets/Game/Features/Configs/` (модуль `Configs`) + `Assets/Game/Features/Configs/Editor/` (модуль `Configs.Editor`, Editor-only).
- DI-регистрация — `ConfigsVContainerBindings.RegisterConfigs` в `BootstrapInstaller` (GlobalLifetimeScope).
- Прогрев на старте: `ConfigsWarmupEntryPoint` (`IAsyncStartable`) — RC `InitializeAsync` → `IConfigsService.WarmupAsync`. После этого `Get<T>` работает синхронно из кэша.
- Конвенции, которые лучше не ломать без надобности:
  - `[ConfigFile("name")]` на каждом POCO-типе — единая точка маппинга тип → файл.
  - RC-ключ всегда `cfg_<fileName>`, payload — словарь `id → partial JSON`.
  - ETag везде канонизируется через `GetConfigCommand.NormalizeEtag` (срез кавычек/`W/`).
  - Editor хранит admin-credentials только в `EditorPrefs` (per-машина), не комитит в репо.
