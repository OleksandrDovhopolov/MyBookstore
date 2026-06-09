# Config Server API

Контракт серверных методов конфигов и инструкция для клиента.
**Статус: public + admin реализованы на backend.**

- **Сервер:** `https://gameserver-production-be8b.up.railway.app`
- **Public** (читает игровой клиент): `…/api/v1/configs/...` — версионированный путь.
- **Admin** (правит ГД): `…/api/admin/configs/...` — **без** `v1` в пути.
- **Клиент**: `Assets/Game/Features/Configs/Server/` (`ServerConfigSource`, `GetConfigsManifestCommand`, `GetConfigCommand`) — подключён к public-методам. Editor-окно Pull/Publish под admin-методы пока не написано (см. §11), но эндпоинты живые и доступны через Postman/curl уже сейчас.
- Формат данных — **JSON**. Транспорт — HTTPS.

---

## 1. Что делает клиент (контекст)

На старте игры клиент выполняет delta-синхронизацию:

```
1. GET /configs/manifest                 → [{ name, version, etag }, ...]
2. для каждой секции сравнивает etag с локальным снапшотом (persistentDataPath/configs/)
3. если etag совпал — пропускает (ничего не качает)
4. если отличается — GET /configs/{name}  с заголовком If-None-Match: <localEtag>
       200 → берёт тело + заголовок ETag, перезаписывает снапшот
       304 → оставляет локальный снапшот
5. сеть недоступна → работает на локальном снапшоте
```

Конфиг = **JSON-массив** объектов одного типа, у каждого объекта есть поле `id`.
Сейчас в игре 4 секции: `books`, `locations`, `requests`, `events`.

> **Критично для трафика:** `etag` в манифесте для секции ДОЛЖЕН совпадать с заголовком
> `ETag`, который возвращает `GET /configs/{name}` для текущей версии. Иначе клиент будет
> перекачивать секцию при каждом запуске.

---

## 2. Public API (читает игровой клиент)

Доступ — без аутентификации (конфиги не секретны), либо за общим app-level токеном, если он
уже есть на других public-ручках. Согласовать с тем, как защищён остальной public API.

### 2.1 `GET /configs/manifest`

Список актуальных версий всех секций для окружения (см. §4).

**Response `200`:**
```json
[
  { "name": "books",     "version": 13, "etag": "a1b2c3" },
  { "name": "locations", "version": 4,  "etag": "d4e5f6" },
  { "name": "requests",  "version": 7,  "etag": "77aa11" },
  { "name": "events",    "version": 2,  "etag": "0f0f0f" }
]
```

- `name` — имя секции (= имя файла без расширения).
- `version` — целое (int64), монотонно растёт при каждой публикации секции. Информационное/для admin.
- `etag` — строка, идентификатор текущего контента секции (см. §5). Клиент сравнивает строки на равенство.

### 2.2 `GET /configs/{name}`

Контент одной секции. Пример: `GET /configs/books`.

**Request headers (опционально):**
```
If-None-Match: "a1b2c3"
```

**Response `200`** (контент изменился или заголовок не прислан):
```
ETag: "a1b2c3"
Content-Type: application/json
```
```json
[
  { "id": "book_dune",   "title": "Dune",       "author": "Frank Herbert", "genre": "sci-fi",  "basePrice": 120, "rarityWeight": 0.2 },
  { "id": "book_hobbit", "title": "The Hobbit", "author": "J.R.R. Tolkien","genre": "fantasy", "basePrice": 90,  "rarityWeight": 0.5 }
]
```

**Response `304 Not Modified`** (присланный `If-None-Match` совпал с текущим etag): тело пустое.

**Response `404`** — секции с таким `name` нет.

Тело — **массив** (а не объект-обёртка). Поля объектов соответствуют моделям клиента
(`BookConfig`, `LocationConfig`, `RequestConfig`, `EventConfig`); парсинг case-insensitive,
так что `id`/`Id` оба ок, но держим единый стиль (camelCase).

---

## 3. Admin API (editor-publish для геймдизайнеров)

Базовый путь: `/api/admin/configs/...` (**без `v1`**, в отличие от public). Все методы
защищены **Basic Auth** (см. §6). Валидация payload при `PUT`/`POST`: тело-массив, у каждого
элемента непустой уникальный `id` — иначе `400`.

### 3.1 `GET /api/admin/configs/{name}?environment=<env>`
Текущая версия секции для редактирования.

**Response `200`:**
```
ETag: "a1b2c3"
```
```json
{ "name": "books", "environment": "dev", "version": 13, "etag": "a1b2c3", "json": [ /* массив конфигов */ ] }
```

### 3.2 `PUT /api/admin/configs/{name}?environment=<env>` — публикация
Optimistic concurrency: клиент шлёт etag версии, на основе которой редактирует.

**Request:**
```
If-Match: "a1b2c3"
Content-Type: application/json
Authorization: Basic <base64(user:pass)>
```
```json
{ "json": [ /* новый массив конфигов */ ], "comment": "balance pass tier 2" }
```

**Bootstrap первой публикации.** Если секции ещё нет в указанном `environment`, etag для
сравнения отсутствует. Сервер сейчас принимает любой непустой `If-Match` — мы условно
используем `If-Match: "bootstrap"`. После первой публикации работает обычный flow с реальным etag.

**Responses:**
- `200` — опубликовано. `version = old+1`, новый `etag`. Тело — как в 3.1.
- `412 Precondition Failed` — на сервере уже более новая версия. Клиент должен пул и
  смёрджить руками; **не перезаписывать молча**.
- `400` — невалидный JSON / нарушена схема (не массив / пустой/дублирующийся `id`).
- `401` — нет/неверный Basic-auth.

### 3.3 `GET /api/admin/configs/{name}/history?environment=<env>`
**Response `200`:**
```json
[
  { "version": 13, "etag": "a1b2c3", "updatedBy": "gd_alex", "updatedAt": "2026-06-08T10:00:00Z", "comment": "balance pass tier 2" },
  { "version": 12, "etag": "99ff00", "updatedBy": "gd_kate", "updatedAt": "2026-06-07T18:20:00Z", "comment": "initial" }
]
```

### 3.4 `POST /api/admin/configs/{name}/rollback?environment=<env>&to=<version>`
Создаёт **новую** версию, идентичную указанной (не удаляет историю). Возвращает как 3.1.

### 3.5 `POST /api/admin/configs/{name}/promote?from=dev&to=prod`
Серверное копирование текущей версии из `from` в `to` — гарантирует, что в prod пойдёт
байт-в-байт тот контент, что протестировали в dev (см. провенанс в audit-полях). Прод-publish
с явным подтверждением на клиенте.

---

## 4. Окружения (environments)

Минимум **dev** и **prod** (желательно + **staging**). Разделение — поле `environment` в БД.

- **Решено:** клиент шлёт public-запросы **без** `environment` → сервер отдаёт **prod** по умолчанию
  (и editor, и build). Параметр `?environment=dev` — опциональный ручной запрос для отладки,
  в клиентском коде сейчас не используется.
- Из редактора ГД публикует в dev/staging свободно, в prod — с подтверждением.

---

## 5. ETag и версионирование

- `version` — int64, +1 при каждой публикации секции в данном окружении.
- `etag` — стабильный идентификатор контента. Рекомендация: **хэш канонизированного JSON**
  (например, SHA-256 от минифицированного тела, первые N символов hex). Тогда повторная
  публикация идентичного контента не меняет etag и не вызывает лишних скачиваний.
  Допустимо и `etag = "v" + version`, но тогда любой re-save форсит докачку.
- Формат заголовка — стандартный HTTP ETag в кавычках: `ETag: "a1b2c3"`; клиент эхо-шлёт его в `If-None-Match`.
- **Инвариант:** `manifest[name].etag == ETag` из `GET /configs/{name}` для текущей версии.

---

## 6. Аутентификация

- **Public** (`/api/v1/configs/*`): без auth.
- **Admin** (`/api/admin/configs/*`): **HTTP Basic**. Credentials берутся из env-переменных
  сервера `ADMIN_USER` / `ADMIN_PASS` (fallback есть в `AdminAuth` config, но прод-источник —
  именно env). Передаётся стандартным заголовком:
  ```
  Authorization: Basic <base64(user:pass)>
  ```
- Ролей пока нет — единственная Basic-учётка имеет полный доступ ко всем environment'ам и
  методам. Разделение editor/publisher отложено.
- Аудит: в каждой версии хранятся `updatedBy`, `updatedAt`, `comment`.

> **На клиенте.** Не комитить креды. Хранить в `EditorPrefs` per-машина (`MyBookstore.Configs.AdminUser` / `…AdminPass`), Editor-окно при первом запуске запрашивает их у ГД.

---

## 7. Модель хранения (минимум)

Одна таблица версий (история — это строки, не перезапись):

```sql
configs(
  name        text        not null,   -- books | locations | requests | events | ...
  environment text        not null,   -- dev | staging | prod
  version     bigint      not null,   -- +1 при публикации
  etag        text        not null,   -- хэш контента (§5)
  json        jsonb       not null,   -- сам конфиг: JSON-массив
  updated_by  text        not null,
  updated_at  timestamptz not null,
  comment     text,
  primary key (name, environment, version)
);
```

- `manifest` = строки с максимальной `version` по каждому `(name, environment)`.
- `GET /configs/{name}` отдаёт `json` последней версии + её `etag`.
- (опц.) Валидация схемы при `PUT`: тело — массив, у каждого элемента непустой уникальный `id`.

---

## 8. Что НЕ требуется (отложено)

Сознательно вне MVP — не реализовывать сейчас:

- per-section CRC + бинарный delta-протокол (хватает per-file ETag/`If-None-Match`/`304`);
- MessagePack / бинарные паки (отдаём обычный JSON);
- сжатие/паки ассетов, CDN-инвалидация по версии приложения.

Эксперименты / A/B / фиче-флаги / таргетинг по аудиториям **не делаются на этом сервере** —
они уже закрыты Firebase Remote Config (override-слой поверх этих конфигов на клиенте).

---

## 9. Состояние реализации

**Backend (done):**
- [x] `GET /api/v1/configs/manifest`, `GET /api/v1/configs/{name}` (public, ETag/304/404).
- [x] `GET /api/admin/configs/{name}` (текущая версия с etag).
- [x] `PUT /api/admin/configs/{name}` с `If-Match`, валидация payload, инкремент version, новый etag, аудит.
- [x] `GET /api/admin/configs/{name}/history`.
- [x] `POST /api/admin/configs/{name}/rollback?to=`.
- [x] `POST /api/admin/configs/{name}/promote?from=&to=`.
- [x] Basic auth для admin (env `ADMIN_USER`/`ADMIN_PASS`).
- [x] Таблица `configs` с историей версиями.

**Client (done):**
- [x] `ServerConfigSource` подключён к public-методам (manifest + delta по ETag).
- [x] Disk snapshot fallback, ETag-канонизация.
- [x] Firebase RC override-слой поверх серверных конфигов.

**Client (todo):**
- [ ] Editor-окно `Tools/Configs/Pull-Publish` под admin-методы (см. §11).
- [ ] Хранение admin-credentials в `EditorPrefs`.

---

## 10. Workflow до готовности Editor-окна — Postman / curl

Промежуточный способ публиковать обновления через CLI/Postman, пока нет UI.

### Pull (посмотреть текущий contents + etag)
```bash
curl -u "$ADMIN_USER:$ADMIN_PASS" \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/books?environment=prod"
```

### Publish (правка существующей секции)
```bash
# 1) Возьми etag из pull выше.
# 2) Сформируй новое тело массива.
curl -X PUT -u "$ADMIN_USER:$ADMIN_PASS" \
  -H 'Content-Type: application/json' \
  -H 'If-Match: "a1b2c3"' \
  -d '{
        "json": [
          { "id": "book_dune", "title": "Dune", "author": "Frank Herbert", "genre": "sci-fi", "basePrice": 150, "rarityWeight": 0.2 }
        ],
        "comment": "balance pass tier 2"
      }' \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/books?environment=prod"
```

### Bootstrap первой публикации секции
```bash
curl -X PUT -u "$ADMIN_USER:$ADMIN_PASS" \
  -H 'Content-Type: application/json' \
  -H 'If-Match: "bootstrap"' \
  -d '{ "json": [ /* первый contents */ ], "comment": "initial" }' \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/events?environment=dev"
```

### Promote dev → prod
```bash
curl -X POST -u "$ADMIN_USER:$ADMIN_PASS" \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/books/promote?from=dev&to=prod"
```

### Rollback
```bash
curl -X POST -u "$ADMIN_USER:$ADMIN_PASS" \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/books/rollback?environment=prod&to=12"
```

### History
```bash
curl -u "$ADMIN_USER:$ADMIN_PASS" \
  "https://gameserver-production-be8b.up.railway.app/api/admin/configs/books/history?environment=prod"
```

После любого `PUT/POST` в Unity: `Tools → Configs → Clear Server Snapshot` → Play.

---

## 11. Editor Pull/Publish flow (для будущего окна на клиенте)

Окно `Tools/Configs/Pull-Publish` (наследник нашего тумблера `Use Server Source` / меню `Clear Server Snapshot`). Шаги ГД:

1. **Settings (один раз):** ввести `ADMIN_USER` / `ADMIN_PASS` — кладутся в `EditorPrefs` per-машину, **в репо не комитятся**.
2. **Choose environment:** dev / prod (radio).
3. **Pull**: `GET /admin/configs/{name}?environment=<env>` → показать текущий contents + сохранить etag в окне.
4. **Edit locally**: открыть JSON во внешнем редакторе или прямо в окне (textarea). По желанию — Play Mode с локальной папкой для теста.
5. **Diff**: показать diff локального contents vs только что pulled с сервера — защита от случайного push'а мусора.
6. **Publish**: `PUT /admin/configs/{name}?environment=<env>` с `If-Match: <pulled etag>`. На `412` показать модалку «на сервере новее, нажми Pull → смёрджи руками».
7. **History**: `GET .../history` — список с возможностью просмотра и **Rollback** (`POST .../rollback?to=`).
8. **Promote dev → prod**: `POST .../promote?from=dev&to=prod` с явным confirm-диалогом «ты публикуешь в прод».
9. После любого write — автоматически `Clear Server Snapshot` для корректного refresh при следующем Play.

Связанные документы: [CONFIG_CACHE_SYSTEM.md](CONFIG_CACHE_SYSTEM.md) — клиентская архитектура целиком.
