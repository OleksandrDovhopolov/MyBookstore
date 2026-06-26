# Config Editor Window MVP Spec

> **Статус: MVP реализован.** Окно живёт в `Assets/Game/Features/Configs/Editor/` (assembly `Configs.Editor`, Editor-only). Открывается через `Tools/Configs/Editor Window`.
> Спека ниже сохранена как историческая запись требований; реальные отступления и текущее состояние — в §0 ниже.

Связанные документы:
- [ADR-0002 (клиент): config system architecture](adr/0002-config-system-architecture.md) — архитектурное решение, extension paths и оценки сложности
- [CONFIG_CACHE_SYSTEM.md](CONFIG_CACHE_SYSTEM.md) — клиентский runtime конфигов
- [CONFIG_SERVER_API.md](CONFIG_SERVER_API.md) — серверный контракт (источник истины — на стороне backend)

---

## 0. Implementation status

### Реализовано (MVP `done`)

| §  | Возможность | Где живёт |
|----|-------------|-----------|
| 3  | Menu entry `Tools/Configs/Editor Window` | [ConfigEditorWindow.cs](../Assets/Game/Features/Configs/Editor/ConfigEditorWindow.cs) |
| 4.1 | Toolbar: Section / Environment / Pull / Publish / History / Promote / Raw JSON / статус | ConfigEditorWindow |
| 4.2 | Item list со search, Add / Duplicate / Delete | [ItemListPanel.cs](../Assets/Game/Features/Configs/Editor/ItemListPanel.cs) |
| 4.3 | Typed-форма для `books` | [BooksItemDrawer.cs](../Assets/Game/Features/Configs/Editor/BooksItemDrawer.cs) |
| 4.3 | Generic JObject-форма для остальных секций | [GenericItemDrawer.cs](../Assets/Game/Features/Configs/Editor/GenericItemDrawer.cs) |
| 4.4 | Bottom: Publish Comment, Last Operation Result, Last Error | ConfigEditorWindow.DrawBottomPanel |
| 5   | SectionState (selectedSection, env, version, etag, snapshots, dirty, ...) | [SectionState.cs](../Assets/Game/Features/Configs/Editor/SectionState.cs). **Selection хранится по индексу**, не по id — устойчиво к правке `id` в форме (отступление от §5, см. §0.2). |
| 6   | Транспорт admin API через `UnityWebRequest`; Basic auth; ETag-канонизация через `GetConfigCommand.NormalizeEtag` | [AdminApiClient.cs](../Assets/Game/Features/Configs/Editor/AdminApiClient.cs) |
| 7   | Connection foldout: BaseUrl/Username/Password в `EditorPrefs`; Save Locally + Test Connection | [ConfigEditorSettings.cs](../Assets/Game/Features/Configs/Editor/ConfigEditorSettings.cs) |
| 8.1 | Pull (200 / 404 → Empty / 401 / прочие) | ConfigEditorWindow.PullAsync |
| 8.2 | Bootstrap первой публикации с `If-Match: "bootstrap"` | ConfigEditorWindow.PublishAsync |
| 8.3 | Edit + publish целой секции | PublishAsync |
| 8.4–8.6 | Add / Duplicate (с очисткой id) / Delete (с confirm) | ItemListPanel |
| 8.7 | Publish: валидация → PUT → 200/400/412/401-ветки | PublishAsync |
| 8.8 | Conflict: modal `Reload Latest / Cancel` | HandleConflict |
| 8.9 | History — отдельное окно с таблицей версий | [ConfigHistoryWindow.cs](../Assets/Game/Features/Configs/Editor/ConfigHistoryWindow.cs) |
| 8.10 | Rollback с confirm + автоматический Pull в главное окно | ConfigHistoryWindow.RollbackAsync + ConfigEditorWindow.OnRolledBack |
| 8.11 | Promote to Prod (enabled только при env=dev), confirm | PromoteAsync |
| 9   | Validation: array / objects / id (непустой, уникальный) + books extras (title, basePrice ≥ 0, rarityWeight ≥ 0) — блокирует Publish и метит ⚠ в item list | [SectionValidator.cs](../Assets/Game/Features/Configs/Editor/SectionValidator.cs) |
| 10  | Raw JSON mode: Format / Apply (с parse-validation) | [RawJsonDrawer.cs](../Assets/Game/Features/Configs/Editor/RawJsonDrawer.cs) |
| 11  | UI states (Disconnected / Idle / Loading / Loaded / Dirty / Publishing / Conflict / Error / Empty) + переходы | ConfigEditorWindow.* + UpdateDirtyTransition |
| 12  | Buttons enable/disable rules | CanPublish / CanShowHistory / CanPromote / DisabledScope-блоки |
| 14  | Логирование `verb + url + status`; **пароль и `Authorization` не логируются** (закреплено комментарием в AdminApiClient.SendAsync) | AdminApiClient |
| 14  | `Copy Error`, `Copy Current JSON` (через `EditorGUIUtility.systemCopyBuffer`) | DrawBottomPanel |

### Acceptance Criteria (§15) — статус

- [x] Открыть окно из Unity menu
- [x] Указать URL / username / password
- [x] Pull секции `books/dev`
- [x] Добавить новую книгу и опубликовать секцию
- [x] Изменить поле существующей книги и опубликовать
- [x] Удалить книгу и опубликовать
- [x] Видеть history
- [x] Rollback на старую версию
- [x] Promote `dev → prod`
- [x] `If-Match` / `ETag` полностью скрыты от пользователя
- [x] При `412` — понятная ошибка + диалог `Reload Latest`

### 0.1 Меню-путь

Изменено с **`Tools/Game Server/Config Editor`** (как было в §3 спеки) на **`Tools/Configs/Editor Window`** — выравнивание с уже существующими пунктами `Tools/Configs/Use Server Source` и `Tools/Configs/Clear Server Snapshot`. §3 спеки явно разрешает «внутренний стиль namespace».

### 0.2 Отступления от изначальной спеки

- **`SelectedItemId` (§5) → `SelectedItemIndex` (int).** Хранение по id ломается, как только ГД правит поле `id` в форме — selection теряется. Индекс устойчив к правкам полей; пересоздаётся при Pull/Add/Duplicate/Delete.
- **Auto-clear server snapshot после Publish.** Дополнительно к §8.7: после успешного PUT окно вызывает удаление `Application.persistentDataPath/configs/` (логика идентична `Tools/Configs/Clear Server Snapshot`). Это гарантирует, что следующий Play в редакторе видит свежие данные, а не закэшированный snapshot.
- **`Promote to Prod` требует чистого Loaded** (не Dirty). Спека (§12) требовала только «enabled only for dev». Запрет в Dirty добавлен, чтобы исключить ложное ощущение «я promote'нул свои локальные правки» — promote копирует **серверную** dev-версию, локальные мутации игнорирует.
- **View JSON в History — lazy fetch.** Бэкенд не отдаёт `json` в `/history` (только метаданные). Реализовано через отдельный endpoint `GET /api/admin/configs/{name}/versions/{version}` ([CONFIG_SERVER_API.md §3.5](CONFIG_SERVER_API.md)), который backend уже выкатил. Кнопка `View JSON` грузит контент при первом клике и кэширует на жизнь окна; кэш сбрасывается на Refresh и после Rollback.
- **Single Basic-auth учётка** вместо ролей `editor` / `publisher`. Совпадает с серверной реализацией (`ADMIN_USER`/`ADMIN_PASS` env). Разделение ролей отложено вместе с серверной частью.

### 0.3 Не реализовано (post-MVP) — см. §16

`auto-merge при 412`, `inline diff viewer pulled vs working`, `typed forms для locations/requests/events`, `bulk import/export`, `schema validation per section`, `local autosave draft`. Оценки сложности и точки расширения — в [ADR-0002 §Extension paths](adr/0002-config-system-architecture.md).

---

## 1. Цель

Сделать дешёвый по реализации, но удобный для геймдизайнера Unity Editor tool, который:

- читает конфиги из server source
- показывает секцию как список объектов
- позволяет добавлять, редактировать, удалять элементы
- публикует новую версию секции целиком через существующий backend API
- скрывает от пользователя детали `ETag`, `If-Match` и snapshot-based storage

Инструмент не меняет backend API и не требует новых серверных endpoint-ов.

## 2. Scope MVP

В MVP входят:

- Unity Editor menu entry
- одно EditorWindow
- работа с секциями `books`, `locations`, `requests`, `events`
- environments: `dev`, `prod`
- pull текущей секции
- редактирование списка элементов
- add / duplicate / delete item
- publish с comment
- history просмотр
- rollback на выбранную версию
- promote `dev -> prod`
- raw JSON fallback

В MVP не входят:

- совместное редактирование
- автоматический merge конфликтов
- section-specific сложные формы для всех типов
- schema generator
- inline diff viewer с подсветкой полей
- batch publish нескольких секций

## 3. Entry Point

> **Реализовано:** `Tools/Configs/Editor Window` (выравнивание с существующими `Tools/Configs/*` пунктами; §3 разрешает «внутренний стиль»).

При нажатии открывается `ConfigEditorWindow`.

Дополнительно допустим второй путь:

```text
Window/Game Server/Config Editor
```

Если в проекте уже есть внутреннее меню tooling, использовать его стиль и namespace.

## 4. Window Layout

Окно делится на 4 зоны.

### 4.1 Toolbar

Поля и кнопки сверху:

- `Section` dropdown
  - `books`
  - `locations`
  - `requests`
  - `events`
- `Environment` dropdown
  - `dev`
  - `prod`
- `Pull`
- `Publish`
- `History`
- `Promote to Prod`
- `Raw JSON`

Справа статус:

- `Version: <n>`
- `ETag: <value>`
- `State: Clean / Dirty / Loading / Publishing / Conflict / Error`

### 4.2 Left Pane: Item List

Список элементов текущей секции.

Для каждого элемента:

- `id`
- краткое второе поле для удобства
  - для `books`: `title`
  - для других секций: первое человекочитаемое поле, если есть

Действия:

- поиск по `id`
- выбор элемента
- `Add`
- `Duplicate`
- `Delete`

### 4.3 Right Pane: Item Details

Редактор выбранного элемента.

Минимальный MVP:

- generic key/value rendering через `SerializedObject` или JSON-backed form
- поддержка строк, чисел, bool
- массивы и вложенные объекты можно показывать в foldout

Для `books` желательно сделать человеко-удобные поля:

- `id`
- `title`
- `author`
- `genre`
- `basePrice`
- `rarityWeight`

Для остальных секций допустим generic editor.

### 4.4 Bottom Panel

Нижняя панель содержит:

- поле `Publish Comment`
- read-only `Last Error`
- read-only `Last Operation Result`

## 5. Data Model Inside Editor

Окно должно хранить:

- `selectedSection`
- `selectedEnvironment`
- `currentVersion`
- `currentEtag`
- `pulledSnapshotJson`
- `workingSnapshotJson`
- `isDirty`
- `lastLoadedAt`
- `selectedItemId`
- `lastError`

Также нужно хранить список parsed items текущей секции для list view.

Snapshot model:

- `pulledSnapshotJson` — последняя версия, пришедшая с сервера
- `workingSnapshotJson` — локально редактируемая версия

`isDirty = pulledSnapshotJson != workingSnapshotJson`

## 6. Backend Contract Used by Editor

Editor использует только существующие endpoint-ы:

- `GET /api/admin/configs/{name}?environment=...`
- `PUT /api/admin/configs/{name}?environment=...`
- `GET /api/admin/configs/{name}/history?environment=...`
- `POST /api/admin/configs/{name}/rollback?environment=...&to=...`
- `POST /api/admin/configs/{name}/promote?from=dev&to=prod`

Auth:

- Basic auth
- credentials берутся из Editor settings или локального config file

Editor должен сам:

- подставлять `Authorization: Basic ...`
- хранить и передавать `If-Match`
- читать `ETag` из ответа

Пользователь не должен работать с `ETag` вручную.

## 7. Authentication UX

В окне должен быть foldout `Connection`.

Поля:

- `Base Url`
  - default: `https://gameserver-production-be8b.up.railway.app`
- `Username`
- `Password`

Кнопки:

- `Test Connection`
- `Save Locally`

Требование:

- credentials сохраняются только локально для пользователя Editor
- не коммитятся в repo

Допустимые варианты локального хранения:

- `EditorPrefs`
- локальный ignored json в `UserSettings/`

MVP recommendation:

- использовать `EditorPrefs`

## 8. Primary User Flows

### 8.1 Pull Section

Шаги:

1. Пользователь выбирает `Section` и `Environment`
2. Нажимает `Pull`
3. Editor делает `GET /api/admin/configs/{name}?environment=...`

Успех:

- обновить `currentVersion`
- обновить `currentEtag`
- сохранить response `json` в `pulledSnapshotJson`
- скопировать его в `workingSnapshotJson`
- пересобрать item list
- сбросить `isDirty = false`

Если `404`:

- показать состояние `Empty section`
- item list пустой
- `currentVersion = 0`
- `currentEtag = null`
- разрешить создать новую секцию

### 8.2 Create First Version

Если секция ещё не существует:

1. пользователь нажимает `Add`
2. создаёт минимум один элемент с корректным `id`
3. вводит `Publish Comment`
4. нажимает `Publish`

Editor должен:

- отправить `PUT`
- передать `If-Match: "bootstrap"` только если `currentEtag == null`

### 8.3 Edit Existing Section

1. Пользователь делает `Pull`
2. Меняет один или несколько элементов
3. Окно помечает состояние как `Dirty`
4. Нажимает `Publish`
5. Editor отправляет полный массив секции в body:

```json
{
  "json": [ ...full section snapshot... ],
  "comment": "..."
}
```

Header:

```text
If-Match: "<currentEtag>"
```

### 8.4 Add Item

1. Кнопка `Add`
2. Создать новый объект по шаблону
3. Выделить его в списке
4. Поставить фокус в `id`

Для `books` default template:

```json
{
  "id": "",
  "title": "",
  "author": "",
  "genre": "",
  "basePrice": 0,
  "rarityWeight": 0
}
```

### 8.5 Duplicate Item

1. Пользователь выбирает элемент
2. Нажимает `Duplicate`
3. Создаётся копия
4. `id` автоматически не копируется как есть

MVP правило:

- либо очищать `id`
- либо ставить `<oldId>_copy`

Recommended:

- очищать `id`, чтобы пользователь явно задал новый

### 8.6 Delete Item

1. Пользователь выбирает элемент
2. Нажимает `Delete`
3. Confirmation dialog
4. После подтверждения элемент удаляется из `workingSnapshotJson`

Удаление не уходит на сервер до `Publish`.

### 8.7 Publish

При нажатии `Publish` Editor:

1. выполняет локальную валидацию
2. сериализует весь snapshot
3. отправляет `PUT`
4. при `200`:
   - обновляет `currentVersion`
   - обновляет `currentEtag`
   - копирует `workingSnapshotJson` в `pulledSnapshotJson`
   - `isDirty = false`
5. при `400`:
   - показать текст ошибки backend
6. при `412`:
   - перейти в состояние `Conflict`
   - показать message:
     - `A newer version exists. Pull latest and reapply your changes.`

### 8.8 Conflict Handling

MVP conflict flow без auto-merge:

При `412` показать modal:

- `Reload Latest`
- `Cancel`

Если `Reload Latest`:

- предупредить, что локальные несохранённые изменения будут потеряны
- затем сделать `Pull`

No auto-merge в MVP.

### 8.9 View History

Кнопка `History` открывает popup/window со списком версий.

Колонки:

- `Version`
- `ETag`
- `UpdatedBy`
- `UpdatedAt`
- `Comment`

Действия:

- `View JSON`
- `Rollback`

### 8.10 Rollback

Из history user выбирает version и нажимает `Rollback`.

Editor:

1. спрашивает подтверждение
2. вызывает `POST /api/admin/configs/{name}/rollback?environment=...&to=...`
3. при успехе делает автоматический refresh current section

### 8.11 Promote to Prod

Кнопка активна только когда:

- selected environment = `dev`

При нажатии:

1. confirmation modal
2. запрос:
   - `POST /api/admin/configs/{name}/promote?from=dev&to=prod`
3. при успехе показать:
   - `Promoted to prod`

## 9. Validation Rules in Editor

Перед publish делать локальную проверку.

Обязательные MVP правила:

- root section is array
- каждый item — object
- у каждого item есть `id`
- `id` не пустой
- `id` уникален внутри секции

Для `books` дополнительно желательно:

- `title` не пустой
- `basePrice >= 0`
- `rarityWeight >= 0`

Validation errors показывать:

- внизу окна
- и рядом с item в списке, если возможно

При наличии validation errors publish disabled.

## 10. Raw JSON Mode

Кнопка `Raw JSON` переключает правую часть окна в текстовый режим.

Назначение:

- fallback для нестандартных секций
- ручная правка редких полей
- диагностика ответа сервера

Требования:

- кнопка `Format`
- кнопка `Apply JSON`
- parse validation before apply

Если raw JSON невалиден:

- не применять изменения в working state

## 11. UI States

Окно должно поддерживать явные состояния:

- `Disconnected`
- `Idle`
- `Loading`
- `Loaded`
- `Dirty`
- `Publishing`
- `Conflict`
- `Error`
- `Empty`

Минимальные переходы:

- `Pull` -> `Loading`
- successful pull -> `Loaded`
- local edit -> `Dirty`
- `Publish` -> `Publishing`
- publish success -> `Loaded`
- publish 412 -> `Conflict`
- request fail -> `Error`

## 12. Buttons Enable/Disable Rules

- `Pull`
  - enabled when credentials and section chosen
- `Publish`
  - enabled only when loaded or empty and `isDirty == true`
- `History`
  - enabled only when section loaded
- `Promote to Prod`
  - enabled only for `dev`
- `Add`
  - enabled when section loaded or empty
- `Delete`
  - enabled when item selected
- `Duplicate`
  - enabled when item selected

## 13. Tech Notes for Unity Implementation

Recommended MVP implementation:

- `EditorWindow`
- backend transport через `UnityWebRequest`
- JSON model через `Newtonsoft.Json.Linq` или существующий project serializer

Recommendation:

- использовать JSON DOM representation, а не жёстко typed models для всех секций
- typed helper разрешён только для `books`, если нужен быстрый удобный UX

Причина:

- минимизация стоимости реализации
- меньше поддержки при изменении структуры секций

## 14. Logging and Diagnostics

Окно должно логировать:

- pull request url
- publish start / success / fail
- current section/environment
- HTTP status code

Не логировать:

- password
- полный Authorization header

Дополнительно:

- кнопка `Copy Error`
- кнопка `Copy Current JSON`

## 15. Acceptance Criteria

MVP считается готовым, если:

- пользователь может открыть окно из Unity menu
- пользователь может указать URL, username, password
- пользователь может сделать `Pull` секции `books/dev`
- пользователь может добавить новую книгу и опубликовать секцию
- пользователь может изменить поле существующей книги и опубликовать секцию
- пользователь может удалить книгу и опубликовать секцию
- пользователь видит history
- пользователь может rollback на старую версию
- пользователь может promote `dev -> prod`
- `If-Match` и `ETag` полностью скрыты от пользователя
- при `412` пользователь получает понятную ошибку

## 16. Post-MVP Improvements

После MVP можно добавить (оценки сложности — в [ADR-0002 §Extension paths](adr/0002-config-system-architecture.md)):

| Фича | Сложность | См. в ADR |
|------|-----------|-----------|
| auto-merge по `id` при `412` | **High** (3–5 дней) | §7 |
| inline diff view pulled vs working | **Medium** (~2 дня) | §6 |
| typed forms для locations / requests / events | **Low** (~2 часа на секцию) | §5 |
| bulk import / export JSON | Trivial-Low | — |
| validation schema per section (JSON Schema) | **High** (~неделя) | §8 |
| локальный autosave draft | Low | — |
| Editor admin-токен per-GD (JWT вместо общего Basic) | **High** | §10 |
| Заменить Editor HTTP на `Game.Http`-команды (если появится in-game admin) | **Medium** (~2 дня) | §11 |
