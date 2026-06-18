# GameServer API — Public Endpoints

Base URL: `http://localhost:<port>`

---

## Player Save — `/api/v1/save/global`

### POST `/api/v1/save/global`
Сохраняет данные игрока.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `data` | string | JSON-строка с данными сохранения |

**Ответ:** `200 OK`

---

### GET `/api/v1/save/global`
Загружает сохранение игрока.

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `{ data, lastModified }`

---

### DELETE `/api/v1/save/global`
Удаляет сохранение игрока.

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `200 OK`

---

### GET `/api/v1/save/global/meta`
Возвращает метаданные сохранения (время последнего изменения).

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `{ lastModified }`

---

## Resources — `/api/v1/resources`

### POST `/api/v1/resources/adjust`
Изменяет ресурс игрока (Gold, Gems, Energy) на дельту.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `resourceId` | string | Идентификатор ресурса |
| `delta` | int | Изменение (+ или −) |
| `reason` | string | Причина изменения |

**Ответ:** `{ success, errorCode, errorMessage, resources }`

---

## Inventory — `/api/v1/inventory`

### POST `/api/v1/inventory/remove`
Удаляет один вид предмета из инвентаря игрока.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `itemId` | string | Идентификатор предмета |
| `amount` | int | Количество |
| `reason` | string | Причина |

**Ответ:** `{ success, errorCode, errorMessage, playerState }`

---

### POST `/api/v1/inventory/remove-batch`
Удаляет несколько предметов за один запрос.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `items` | array of `{ itemId, amount }` | Список предметов |
| `reason` | string | Причина |

**Ответ:** `{ success, errorCode, errorMessage, playerState }`

---

## Card Packs — `/api/v1/packs`

### POST `/api/v1/packs/open`
Открывает карточный пак и возвращает полученные карточки.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `packId` | string | Идентификатор пака |
| `eventId` | string | ID текущего события (определяет набор карточек) |
| `openPackRequestId` | string | Идемпотентный ключ запроса |

**Ответ:** `{ openedCardIds[] }`

---

## Rewards — `/api/v1/rewards`

### POST `/api/v1/rewards/grant`
Выдаёт награду игроку по ID из каталога.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `rewardId` | string | ID награды из `reward_definitions.json` |
| `rewardSource` | string | Источник (для логирования) |

**Ответ:** `{ success, errorCode, errorMessage }`

---

## Reward Intents — `/api/v1/rewards/intent`

### POST `/api/v1/rewards/intent/create`
Создаёт pending-интент для выдачи награды после просмотра рекламы.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `rewardId` | string | ID награды |

**Ответ:** `{ rewardIntentId, ... }`

---

### GET `/api/v1/rewards/intent/status`
Возвращает текущий статус интента.

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `rewardIntentId` | string | ID интента |

**Ответ:** `{ status, errorCode, ... }`

---

### GET `/api/v1/rewards/intent/callback`
Callback-эндпоинт для рекламных сетей. Подтверждает показ рекламы и выдаёт награду (идемпотентно).

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `rewardIntentId` | string | ID интента (передаётся как `rewardIntentId`) |
| `eventId` | string | ID события рекламной сети |
| `dynamicUserId` | string | User ID из рекламной сети |
| `rewards` | string | Строка наград от сети (опционально) |
| `auctionId` | string | ID аукциона (опционально) |
| `adNetwork` | string | Название рекламной сети |
| `appKey` | string | Ключ приложения (опционально) |
| `itemName` | string | Название предмета (опционально) |
| `placementName` | string | Название плейсмента (опционально) |

**Ответ:** `{ success, errorCode, ... }`

---

## Fortune Wheel — `/api/v1/wheel`

### GET `/api/v1/wheel/rewards`
Возвращает список наград колеса с весами.

**Параметров нет.**

**Ответ:** `{ rewards: [{ id, weight }] }`

---

### GET `/api/v1/wheel/data`
Возвращает состояние колеса для игрока (доступные прокруты, таймеры).

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `{ availableSpins, updatedAt, nextUpdateAt, adSpinAvailable }`

---

### POST `/api/v1/wheel/spin`
Выполняет прокрутку колеса.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `{ rewardId, availableSpins, updatedAt, nextUpdateAt, adSpinAvailable, rewardGrant }`
**403** если спинов нет: `{ code: "NO_SPINS_AVAILABLE", updatedAt, nextUpdateAt }`

---

## Battle Pass — `/api/v1/battle-pass`

### GET `/api/v1/battle-pass/current`
Возвращает текущий сезон, состояние игрока и уровни.

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `playerId` | string | ID игрока |

**Ответ:** `{ season, products, userState, levels[], serverTimeUtc }`

---

### GET `/api/v1/battle-pass/schedule`
Возвращает расписание всех сезонов Battle Pass.

**Параметров нет.**

**Ответ:** `{ seasons[], items[], serverTimeUtc }`

---

### POST `/api/v1/battle-pass/claim`
Забирает награду за конкретный уровень.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `seasonId` | string | ID сезона |
| `level` | int | Номер уровня |
| `rewardTrack` | string | Трек (`free` / `premium`) |

**Ответ:** `{ success, grantedRewards[], battlePass, errorCode, errorMessage }`

---

### POST `/api/v1/battle-pass/claim-all`
Забирает все доступные награды за один запрос.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `seasonId` | string | ID сезона |

**Ответ:** `{ success, grantedRewards[], battlePass, errorCode, errorMessage }`

---

### POST `/api/v1/battle-pass/xp/add`
Начисляет XP игроку в текущем сезоне.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `amount` | int | Количество XP |

**Ответ:** `{ success, addedXp, battlePass, errorCode, errorMessage }`

---

### POST `/api/v1/battle-pass/dev/grant-battle-pass`
*(Dev-only)* Мгновенно выдаёт Premium Battle Pass без Google Play.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `productId` | string | ID продукта |
| `purchaseToken` | string | Токен покупки (stub) |
| `seasonId` | string | ID сезона |

**Ответ:** `{ success, purchaseStatus, entitlement, battlePass, ... }`

---

## IAP (Google Play) — `/api/v1/iap/google`

### POST `/api/v1/iap/google/verify`
Верифицирует покупку Google Play и активирует Battle Pass.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `productId` | string | ID продукта из Google Play |
| `purchaseToken` | string | Токен покупки |
| `seasonId` | string | ID сезона Battle Pass |

**Ответ:** `{ success, purchaseStatus, googleFinalizeStatus, entitlement, battlePass, errorCode, errorMessage }`

---

## Live Ops — `/api/v1/liveops`

### GET `/api/v1/liveops/schedule`
Возвращает расписание Live Ops событий.

**Параметров нет.**

**Ответ:** `{ items: [{ id, eventType, streamId, startTimeUtc, endTimeUtc, customParams }] }`

---

## Configs — `/api/v1/configs`

### GET `/api/v1/configs/manifest`
Возвращает манифест доступных конфигов для указанного окружения.

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `environment` | string | Окружение (default: `prod`) |

**Ответ:** список записей манифеста

---

### GET `/api/v1/configs/{name}`
Возвращает конфиг по имени. Поддерживает кэширование через `If-None-Match` / `ETag`.

**Route:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `name` | string | Имя конфига |

**Query:**
| Параметр | Тип | Описание |
|----------|-----|----------|
| `environment` | string | Окружение (default: `prod`) |

**Headers:** `If-None-Match: "<etag>"` (опционально)

**Ответ:** `200` JSON-тело конфига + `ETag`, либо `304 Not Modified`

---

## Admin — требует Basic Auth

Все admin-эндпоинты защищены `AdminBasicAuthFilter` (Basic Auth: `AdminAuth:Username/Password`).

---

### POST `/api/admin/inventory/add`
Добавляет предметы в инвентарь игрока.

**Body (JSON):**
| Поле | Тип | Описание |
|------|-----|----------|
| `playerId` | string | ID игрока |
| `itemId` | string | ID предмета |
| `amount` | int | Количество |
| `category` | string | Категория предмета |
| `reason` | string | Причина |

---

### POST `/api/admin/player/{id}/spins`
Корректирует количество спинов колеса фортуны.

**Route:** `id` — ID игрока  
**Query:** `count` (int) — дельта спинов (+ или −)

**Ответ:** `{ playerId, availableSpins, updatedAt, nextUpdateAt }`

---

### GET `/api/admin/player/{id}`
Возвращает raw JSON сохранения игрока.

**Route:** `id` — ID игрока

---

### DELETE `/api/admin/player/{id}`
Удаляет все данные игрока.

**Route:** `id` — ID игрока  
**Ответ:** `204 No Content`

---

### POST `/api/admin/battle-pass/seasons`
Создаёт новый сезон Battle Pass.

**Body (JSON):** `{ id, title, startAtUtc, endAtUtc, maxLevel, configVersion, premiumProductId, platinumProductId }`

---

### PUT `/api/admin/battle-pass/seasons/{seasonId}`
Обновляет параметры сезона.

**Body (JSON):** `{ title, startAtUtc, endAtUtc, maxLevel, configVersion, premiumProductId, platinumProductId }`

---

### PUT `/api/admin/battle-pass/seasons/{seasonId}/levels`
Создаёт/обновляет уровни сезона.

**Body (JSON):** `{ levels: [{ level, xpRequired, defaultRewardId, premiumRewardId }] }`

---

### POST `/api/admin/battle-pass/seasons/{seasonId}/publish`
Публикует сезон (переводит в статус Active).

---

### POST `/api/admin/battle-pass/seasons/{seasonId}/archive`
Архивирует сезон.

---

### POST `/api/admin/battle-pass/recreate`
Удаляет и пересоздаёт сезон с уровнями, опционально бутстрапит игроков.

**Body (JSON):** `{ season: {...}, levels: [...], bootstrapPlayerIds: [...] }`

---

### POST `/api/admin/battle-pass/reset-player`
Сбрасывает прогресс игрока в сезоне.

**Body (JSON):** `{ playerId, seasonId, scheduleItemId? }`

---

### GET `/api/admin/battle-pass/player/{playerId}`
Возвращает полное состояние Battle Pass игрока.

**Route:** `playerId`  
**Query:** `seasonId` (опционально)

---

### POST `/api/admin/entitlements/grant`
Выдаёт entitlement (Battle Pass) игроку вручную.

**Body (JSON):** `{ playerId, seasonId, passType }`

---

### POST `/api/admin/entitlements/revoke`
Отзывает entitlement у игрока.

**Body (JSON):** `{ playerId, seasonId, passType? }`

---

### GET `/api/admin/purchases/{purchaseId}`
Возвращает данные покупки по ID.

---

### POST `/api/admin/purchases/{purchaseId}/retry-finalization`
Повторяет финализацию покупки в Google Play.

**Body (JSON):** `{ purchaseToken }`

---

### POST `/api/admin/test/redis/flush`
Очищает Redis (требует `AdminTools:AllowRedisFlush = true`).

**Body (JSON):** `{ ... }` (AdminRedisFlushRequest)

---

### GET `/api/admin/configs/{name}`
Возвращает конфиг для администратора (с метаданными).

**Route:** `name`  
**Query:** `environment` (обязательно)

---

### GET `/api/admin/configs/{name}/versions/{version}`
Возвращает конкретную версию конфига.

**Route:** `name`, `version` (long)  
**Query:** `environment` (обязательно)

---

### PUT `/api/admin/configs/{name}`
Публикует новую версию конфига (оптимистичная блокировка через If-Match).

**Route:** `name`  
**Query:** `environment` (обязательно)  
**Headers:** `If-Match: "<etag>"` (обязательно)  
**Body (JSON):** `{ json, comment? }`

---

### GET `/api/admin/configs/{name}/history`
Возвращает историю версий конфига.

**Route:** `name`  
**Query:** `environment` (обязательно)

---

### POST `/api/admin/configs/{name}/rollback`
Откатывает конфиг к указанной версии.

**Route:** `name`  
**Query:** `environment` (обязательно), `to` — номер версии (long)

---

### POST `/api/admin/configs/{name}/promote`
Копирует конфиг из одного окружения в другое.

**Route:** `name`  
**Query:** `from`, `to` — названия окружений
