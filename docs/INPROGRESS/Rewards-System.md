# Система наград (Rewards)

**Сборка:** `Assets/Game/Features/Rewards/Rewards.asmdef`

---

## Обзор архитектуры

Система наград реализует два независимых потока:

1. **Рекламный поток (Ads Reward Flow)** — игрок смотрит рекламу и получает награду. Управляется `AdsRewardFlowService`.
2. **Серверный поток (Server Grant Flow)** — прямая выдача награды через сервер без рекламы. Управляется `ServerRewardGrantService`.

Оба потока сходятся к одному интерфейсу `IRewardGrantService`, который применяет изменения к состоянию игрока.

---

## Зависимости сборки

Сборка зависит от:
- `Core`, `Infrastructure`, `Inventory.API` и других смежных сборок
- Тестовая сборка `Rewards.Tests.Editor.asmdef` дополнительно подключает `Game.Crafting`, `CoreResources`, `UniTask`, `R3.Unity`, `UISystem`, `FortuneWheel`, `Core.Models`

---

## Ключевые интерфейсы

### `IRewardGrantService`
Основной интерфейс выдачи наград.

| Метод | Возвращает | Описание |
|---|---|---|
| `TryGrantAsync(rewardId, rewardSource)` | `bool` | Выдать награду, true = успех |
| `TryGrantDetailedAsync(rewardId, rewardSource)` | `RewardGrantDetailedResult` | То же, но с деталями об ошибке |

### `IRewardIntentService`
Управление намерениями (intent) — используется в серверно-подтверждённом рекламном потоке.

| Метод | Описание |
|---|---|
| `CreateAsync(rewardId)` | Создаёт intent на сервере, возвращает `rewardIntentId` |
| `GetStatusAsync(rewardIntentId)` | Опрашивает статус: Pending / Fulfilled / Rejected / Failed / Expired |

### `IRewardPlayerStateSyncService`
Синхронизация состояния игрока после выдачи награды.

| Метод | Описание |
|---|---|
| `SyncFromGlobalSaveAsync(ct)` | Получает и применяет снапшот с сервера |

### `IRewardedAdsProvider`
Абстракция рекламного SDK.

| Метод | Описание |
|---|---|
| `InitializeAsync()` | Инициализация SDK |
| `PreloadAsync(adUnitId)` | Предзагрузка рекламного блока |
| `ShowAsync(adUnitId, rewardIntentId)` | Показ рекламы → `RewardedShowResult` |

### `IRewardResponseApplier`
Применяет ответ сервера (`GrantRewardResponse`) к локальному состоянию игрока.

### `IRewardSpecProvider`
Поиск спецификаций наград по ID.

| Метод | Описание |
|---|---|
| `TryGet(rewardId, out RewardSpec)` | Возвращает полную спецификацию награды |
| `TryGetResourceIcon(resourceId, out Sprite)` | Иконка ресурса для UI |

### `IRewardHandler`
Паттерн обработчика для разных типов наград.

| Метод | Описание |
|---|---|
| `CanHandle(request)` | Может ли обработчик обработать этот тип |
| `HandleAsync(request, ct)` | Применить награду |

---

## Реализации сервисов

### `ServerRewardGrantService`
Выдача наград через сервер.

- Отправляет `POST rewards/grant` с `GrantRewardCommand` (`playerId`, `rewardSource`, `rewardId`)
- Получает `GrantRewardResponse` с флагом успеха и `PlayerStateSnapshotDto`
- Использует `SemaphoreSlim(1,1)` — только один грант одновременно
- Обрабатывает: `WebClientNetworkException`, `WebClientHttpException`, `WebClientException`

### `ServerRewardIntentService`
Управление intent'ами для серверно-подтверждённого рекламного потока.

- `POST rewards/intent/create` → получает `rewardIntentId`
- `GET rewards/intent/status?rewardIntentId=X` → опрашивает статус

### `ServerRewardPlayerStateSyncService`
Синхронизация состояния через глобальное сохранение.

- `GET {ApiConfig.SaveGlobalPath}?playerId=X`
- Парсит вложенный JSON с `Resources` и `InventoryItems`
- Поддерживает оба формата: массив и объект для инвентаря
- Поддерживает альтернативные поля: `"amount"` или `"stackCount"` для количества

### `AdsRewardFlowService`
Основной оркестратор рекламного потока. Реализует конечный автомат:

```
Idle → InitializingAds → LoadingAd → Ready → ShowingAd → WaitingServerGrant → Success
                                                                              → Failed
```

**Режимы работы:**
- **Legacy Flow** — прямая выдача после просмотра рекламы
- **Server-Confirmed Flow** — сначала создаётся intent на сервере, потом реклама, потом опрос статуса

**Ключевые параметры (из `RewardedAdsConfigSO`):**
- `GrantTimeoutSeconds` (по умолчанию 15 сек)
- `GrantConfirmationTimeoutSeconds` (по умолчанию 20 сек)
- `GrantPollingIntervalSeconds` (по умолчанию 1 сек)
- Retry при ошибке предзагрузки — задержка 2 сек

**Потокобезопасность:** `Interlocked` для флага `IsFlowInProgress`, предотвращает параллельные потоки.

---

## Провайдеры рекламы

### `RewardedAdsProviderFactory`
Фабрика, создающая нужный провайдер по `RewardedAdsMode`:

| Режим | Провайдер | Условие компиляции |
|---|---|---|
| `Mock` | `MockRewardedAdsProvider` | Всегда |
| `UnityAdsTestMode` | `UnityAdsRewardedAdsProvider` | `#if UNITY_ADS` |
| `LevelPlay` | `LevelPlayRewardedAdsProvider` | `#if UNITY_LEVELPLAY` |

### `MockRewardedAdsProvider`
Для тестирования. Симулирует задержки, может эмулировать серверные callback'и.

---

## Обработчики наград (Handler Pattern)

### `ResourceRewardHandler`
- `CanHandle()` → `RewardKind == Resource`
- Вызывает `IResourceOperationsService.AddAsync()` с причиной `ResourceManager`

### `InventoryRewardHandler`
- `CanHandle()` → `RewardKind == InventoryItem`
- Вызывает `IInventoryService.AddItemAsync()` с `InventoryItemDelta`
- Перехватывает `NotSupportedException` в серверно-авторитетном режиме

### `GrantBackedCraftingRewardApplier`
Реализует `ICraftingRewardApplier` для интеграции с системой крафта.
- Вызывает `IRewardGrantService.TryGrantDetailedAsync()` с `RewardSources.Crafting`

---

## Снапшоты состояния игрока

### `PlayerStateSnapshotApplier`
Цепочка обработчиков, применяющих снапшот целиком.

### `ResourcePlayerStateSnapshotHandler`
- Вызывает `ResourceManager.ApplySnapshotAsync()`
- Содержит TODO: анимация и отображение UI-окна

### `InventoryPlayerStateSnapshotHandler`
- Вызывает `IInventorySnapshotService.ApplySnapshotAsync()`
- Использует `IInventoryItemCategoryResolver` для категоризации предметов

### `RewardResponseApplier`
Применяет весь `GrantRewardResponse` (обёртка над `PlayerStateSnapshotApplier`).

---

## Спецификации наград (Data Layer)

### `RewardSpec` (в `RewardSpecsConfigSO`)

| Поле | Тип | Описание |
|---|---|---|
| `RewardId` | `string` | Уникальный идентификатор |
| `Icon` | `Sprite` | Иконка для UI |
| `TotalAmountForUi` | `int` | Отображаемое суммарное количество |
| `Resources` | `List<RewardSpecResource>` | Состав награды |

### `RewardSpecResource`

| Поле | Тип | Описание |
|---|---|---|
| `ResourceId` | `string` | ID ресурса |
| `Kind` | `RewardKind` | Resource или InventoryItem |
| `Amount` | `int` | Количество |
| `Category` | `string` | Категория (для инвентаря) |
| `Icon` | `Sprite` | Иконка ресурса |

### `RewardSpecProvider`
- Индексирует все `RewardSpec` по `RewardId`
- Строит вторичный индекс `ResourceId → Icon` для быстрого поиска иконок

### `RewardSpecInventoryItemCategoryResolver`
Маппит ID предметов инвентаря к категориям из спецификаций.

---

## Перечисления и константы

| Тип | Значения |
|---|---|
| `RewardKind` | Unknown, Resource, InventoryItem |
| `RewardSources` | Константы: `"client"`, `"crafting"` |
| `RewardAdFlowState` | Idle, InitializingAds, LoadingAd, Ready, ShowingAd, WaitingServerGrant, Success, Failed |
| `RewardedAdsMode` | Mock, UnityAdsTestMode, LevelPlay |
| `RewardedShowResult` | Completed, Canceled, Failed |
| `RewardIntentStatus` | Unknown, Pending, Fulfilled, Rejected, Failed, Expired |
| `RewardGrantFailureType` | None, Rejected, Network, Http, InvalidResponse, Unknown |
| `RewardGrantFlowResultType` | Success, AdNotReady, AdCanceled, AdFailed, ServerFailed, NetworkError, UnknownError |

---

## UI слой

### `RewardedAdButtonPresenter` + `RewardedAdButtonView`
Кнопка запуска рекламного потока.
- Отображает статус: Loading / Unavailable / Checking / Failed / Success
- Показывает индикатор загрузки во время потока
- Реагирует на изменения состояния оркестратора

### `RewardsWindowController` + `RewardsWindowView`
Всплывающее окно с полученными наградами.
- Использует `UIListPool` для отображения `RewardItemView`
- Запускает анимацию через `IAnimationService`
- Инжектируется через `IRewardSpecProvider`

### `RewardItemView`
Отдельный элемент награды.
- Отображает иконку и количество
- Предоставляет начальную позицию для анимации

### `RewardedAdsRewardOrchestrator`
Мост между `AdsRewardFlowService` и UI.
- При успехе открывает `RewardsWindow`
- Публикует: `IsReady`, `State`, событие `StateChanged`

---

## Конфигурация (ScriptableObjects)

### `RewardedAdsConfigSO`
Путь: `AdsConfig/RewardedAdsConfig.asset`

```
Mode                              — Mock / UnityAdsTestMode / LevelPlay
Android/iOS Game IDs              — для Unity Ads legacy
Android/iOS LevelPlay App Keys
Android/iOS LevelPlay Ad Unit IDs
RewardId                          — ID награды за рекламу
TestMode                          — флаг тестового режима
UseServerConfirmedGrantFlow       — серверно-подтверждённый поток

GrantTimeoutSeconds               — таймаут выдачи (по умолч. 15)
GrantConfirmationTimeoutSeconds   — таймаут подтверждения intent (по умолч. 20)
GrantPollingIntervalSeconds       — интервал опроса (по умолч. 1)

MockOutcome                       — исход мок-провайдера
UseRandomMockDelay                — случайная задержка
MockDelayRangeSeconds             — диапазон задержки
```

### `RewardSpecsConfigSO`
Путь: `Data/RewardSpecsConfig.asset`
- Список всех `RewardSpec` определений

---

## Интеграция с другими системами

| Система | Что использует |
|---|---|
| **BattlePass** | `IRewardSpecProvider` — поиск спецификаций, количеств ресурсов и иконок |
| **CardCollection** | `IRewardGrantService` + `IRewardSpecProvider` — выдача наград за группы/коллекции |
| **FortuneWheel** | `AdsRewardFlowService` — рекламный поток с `FortuneWheelConfig.Gameplay.AdSpinRewardId` |
| **Crafting** | `ICraftingRewardApplier` (реализован `GrantBackedCraftingRewardApplier`) |

---

## Регистрация в DI (VContainer)

`GameInstaller` регистрирует:
- Все `IRewardHandler` реализации
- `ServerRewardGrantService`, `ServerRewardIntentService`, `ServerRewardPlayerStateSyncService`
- `RewardSpecProvider` из `RewardSpecsConfigSO`
- `RewardSpecInventoryItemCategoryResolver`
- Нужный `IRewardedAdsProvider` через фабрику
- `AdsRewardFlowService`, `RewardedAdsRewardOrchestrator`

---

## Обработка ошибок

| Ситуация | Результат |
|---|---|
| Сетевая ошибка | `RewardGrantFlowResultType.NetworkError` |
| Сервер отклонил | `RewardGrantFlowResultType.ServerFailed` |
| Реклама не готова | `RewardGrantFlowResultType.AdNotReady` |
| Реклама не показана | `RewardGrantFlowResultType.AdFailed` / `AdCanceled` |
| Неизвестная ошибка | `RewardGrantFlowResultType.UnknownError` |

Детальный результат включает `ErrorCode` (напр. `"NETWORK_ERROR"`, `"TIMEOUT"`, `"REWARD_REJECTED"`) и `ErrorMessage`.

---

## Ключевые паттерны реализации

1. **Handler Pattern** — `IRewardHandler` с `CanHandle`/`HandleAsync` для расширяемости типов наград
2. **Snapshot Pattern** — `PlayerStateSnapshotDto` передаёт дельта-изменения состояния
3. **State Machine** — `AdsRewardFlowService` управляет состояниями потока
4. **Factory Pattern** — `RewardedAdsProviderFactory` выбирает реализацию SDK
5. **Mutex Protection** — `SemaphoreSlim(1,1)` предотвращает параллельные гранты
6. **Atomic Operations** — `Interlocked` для потокобезопасных флагов потока
7. **Conditional Compilation** — `#if UNITY_ADS` / `#if UNITY_LEVELPLAY` для платформо-специфичного кода

---

## Префабы

| Префаб | Описание |
|---|---|
| `UI/IconFrame.prefab` | Фрейм отдельной награды в UI |
| `UI/RewardsWindow.prefab` | Окно отображения наград |

---

## Схема потока (Server-Confirmed Ad Flow)

```
Player                AdsRewardFlowService         Server
  |                          |                        |
  |--- StartFlow() --------->|                        |
  |                          |-- POST intent/create ->|
  |                          |<-- rewardIntentId ---  |
  |                          |                        |
  |                          |-- ShowAd(intentId) --->|  (реклама показана)
  |                          |<-- Completed ----------|
  |                          |                        |
  |                          |-- GET intent/status -->|  (опрос)
  |                          |<-- Fulfilled ----------|
  |                          |                        |
  |                          |-- POST rewards/grant ->|
  |                          |<-- Snapshot + success--|
  |                          |                        |
  |                          |-- ApplySnapshot() ---->|  (локально)
  |<-- RewardsWindow --------|                        |
```
