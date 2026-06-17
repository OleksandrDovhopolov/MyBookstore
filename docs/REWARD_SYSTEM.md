# Система наград (Rewards)

**Сборки (Phase 0/1 — реальные):**
- `Game.Rewards.API` — контракты (interfaces + value types).
- `Game.Rewards` — `LocalRewardGrantService`, `BookBoxRewardExpander`, `IRewardRandom` port.
- `Game.Rewards.Tests.Editor` — 15+ unit-тестов.

**Status:** Phase 0/1 локальная реализация ✅ done. Server-authoritative flow (intents/ads/snapshots) описан ниже как target — **не реализован**.

> **Структура документа:** §0 — текущая реализация (что фактически в коде). §1+ ниже — target server-authoritative архитектура (foundation для Phase 2+). При работе с текущим кодом начинай с §0.

---

## 0. Текущая реализация (Phase 0/1)

### Сжатый контракт

```csharp
// Game.Rewards.API/IRewardGrantService.cs
public interface IRewardGrantService {
    UniTask<RewardGrantResult> GrantAsync(RewardSpec spec, string source, CancellationToken ct);
}

// Game.Rewards.API/IRewardSpecExpander.cs — plug-in для трансформации спеки
public interface IRewardSpecExpander {
    bool CanExpand(RewardSpec spec);
    UniTask<RewardSpec> ExpandAsync(RewardSpec spec, CancellationToken ct);
}

// Game.Rewards.API
public sealed class RewardSpec { string Id; IReadOnlyList<RewardItem> Items; }
public readonly struct RewardItem { string Id; string Category; int Amount; RewardKind Kind; }
public enum RewardKind { Resource, InventoryItem }
public readonly struct RewardGrantResult { bool Success; RewardSpec Granted; string FailureReason; }
```

### Pipeline (Phase 0/1)

```
caller.GrantAsync(spec, source, ct)                ← caller: IShopService.BuyAsync, FTUE, etc.
  → LocalRewardGrantService.GrantAsync
     ├─ expanded = await ExpandAsync(spec)         ← цепочка IRewardSpecExpander'ов
     │     └─ BookBoxRewardExpander.CanExpand?      ← spec.Id.StartsWith("book_box_")?
     │           → ExpandAsync ролит N книг        ← weighted-without-replacement по pool rule
     │             returns RewardSpec с реальными InventoryItem'ами
     │
     └─ foreach item in expanded.Items:
        switch item.Kind:
          Resource      → IResourcesService.AddAsync(id, amount, source, ct)
          InventoryItem → IInventoryService.AddAsync(id, category, amount, ct)
        
  → RewardGrantResult.Ok(expanded)                 ← caller получает фактически выданное (не запрос)
```

### Что реализовано (5 файлов в `Game.Rewards`)

| Файл | Назначение |
|---|---|
| [`Services/LocalRewardGrantService.cs`](../../Assets/Game/Features/Rewards/Services/LocalRewardGrantService.cs) | Phase 0/1 диспатчер. Прогоняет spec через цепочку `IRewardSpecExpander[]`, диспатчит каждый `RewardItem` по `RewardKind` в Resources или Inventory. Не транзакционен (Phase 0 debt — см. `SHOP.md §11 §5`). |
| [`Services/BookBoxRewardExpander.cs`](../../Assets/Game/Features/Rewards/Services/BookBoxRewardExpander.cs) | Реализует `IRewardSpecExpander`. Матчит `spec.Id.StartsWith("book_box_")`. Берёт `BookConfig`'и через `IConfigsService.GetAll<BookConfig>()`, фильтрует по правилам (`BookBoxPoolRules`), исключает уже-владеемые (PR5: `_inventory.Has`), ролит N книг weighted-without-replacement через `IRewardRandom`. |
| [`Services/BookBoxPoolRules.cs`](../../Assets/Game/Features/Rewards/Services/BookBoxPoolRules.cs) | Хардкод-таблица 3 правил pool-selection: `book_box_common_15` (все, weight=1-RarityWeight, 15 ролов), `book_box_rare_8` (RarityWeight≥0.6, weight=RarityWeight, 8 ролов), `book_box_genre_dystopic_1` (Fantasy+dark, 1 рол). Phase 2+: вынести в config. |
| [`Services/IRewardRandom.cs`](../../Assets/Game/Features/Rewards/Services/IRewardRandom.cs) | Port-абстракция RNG. Адаптер `UnityRewardRandom` поверх `UnityEngine.Random` в DI. Тесты используют `FakeRewardRandom` (queue-based детерминизм). |
| [`API/*.cs`](../../Assets/Game/Features/Rewards/API/) | Контракты выше (`IRewardGrantService`, `IRewardSpecExpander`, `RewardSpec`, `RewardItem`, `RewardKind`, `RewardGrantResult`). |

### DI регистрация ([`RewardsVContainerBindings.cs`](../../Assets/Game/Core/Installers/Features/RewardsVContainerBindings.cs))

```csharp
public static void RegisterRewards(this IContainerBuilder builder)
{
    builder.Register<IRewardRandom, UnityRewardRandom>(Lifetime.Singleton);

    builder.Register<BookBoxRewardExpander>(Lifetime.Singleton);

    // Explicit list (VContainer не auto-resolve'ит коллекции в этом проекте).
    builder.Register<IReadOnlyList<IRewardSpecExpander>>(
        r => new IRewardSpecExpander[] { r.Resolve<BookBoxRewardExpander>() },
        Lifetime.Singleton);

    builder.Register<LocalRewardGrantService>(Lifetime.Singleton).As<IRewardGrantService>();
}
```

Зарегистрирован в `BootstrapInstaller` (Global scope) — `IRewardGrantService` живёт через переходы сцен.

### Текущие consumer'ы

- **`ShopService.BuyAsync`** ([`Shop.Services/ShopService.cs`](../../Assets/Game/Features/Shop/Services/ShopService.cs)) — после списания gold вызывает `_rewards.GrantAsync(spec, $"shop:{lotId}", ct)`.
- Других consumer'ов пока нет. Phase 2+: FTUE (стартовая выдача), BattlePass, FortuneWheel.

### Тесты

[`Rewards.Tests.Editor`](../../Assets/Game/Features/Rewards/Tests/Editor/) — 15+ тестов:
- `LocalRewardGrantServiceTests` — 8 кейсов (Resource grant, InventoryItem grant, multi-item, expander matching/non-matching, zero-amount skip, null spec → Fail).
- `BookBoxRewardExpanderTests` — 9 кейсов (CanExpand true/false, pool builds, rarity filter, genre+mood filter, unknown box id, empty pool, inventory exclusion, all-owned returns empty).

### Что **НЕ** реализовано (из target архитектуры ниже)

| §-target | Не реализовано | Когда |
|---|---|---|
| Ads flow (`AdsRewardFlowService`, `IRewardedAdsProvider`) | Полностью отсутствует. | Phase 3+ если появится IAP/ads монетизация. |
| `IRewardIntentService` (intent create + status polling) | Полностью отсутствует. | Phase 2+ с server-authoritative. |
| `ServerRewardGrantService` поверх `POST /api/v1/rewards/grant` | Endpoint описан в [`api-endpoints.md`](../api-endpoints.md), реализация — отсутствует. | Phase 2+ или раньше, если потребуется. |
| `IRewardPlayerStateSyncService` (snapshot apply из глобального save'а) | Полностью отсутствует. | Phase 2+. |
| `IRewardHandler` chain (handler pattern из target архитектуры) | Не реализован — `LocalRewardGrantService` обходится `switch` по `RewardKind`. Plug-in нужен только для трансформаций spec'и, не для финального dispatch. | Если 4+ типов наград появятся. Сейчас 2 (Resource, InventoryItem). |
| `IRewardSpecProvider` (catalog) | Не реализован — спеки строятся inline из `ShopConfig.RewardItems`. | Phase 1+ если появится `reward_specs.json` reuse pattern. |
| `RewardsWindow` UI popup | Реализован в `Game.Newspaper.UI` (PR10) — см. `SHOP.md §0 PR10`. Не в `Rewards` модуле — там UI не живёт. | — |
| Идемпотентность grant'а (idempotency key) | Не нужен в local mode. | Обязательно в Phase 2 для server endpoint'а. |
| Аналитика grant'ов | Не реализована на уровне Rewards. Shop эмитит `item_purchased` через свой `ShopAnalyticsListener` — это покрывает большинство сценариев. | Если FTUE/BattlePass начнут выдавать награды — добавить аналогичный listener. |

### Открытые вопросы / tech debt

1. **`RewardKind` enum vs handler pattern.** Сейчас 2 значения. Если станет 4+ — мигрировать на `IRewardHandler` (см. target §6 ниже). Решение зафиксировать перед добавлением третьего типа.
2. **Атомарность grant'а.** Если `Grant` падает между resources/inventory мутациями — state частично записан. Phase 2 закроется через server snapshot-pattern.
3. **`BookBoxPoolRules` хардкод.** Phase 2+ вынести в `reward_specs.json` через `[ConfigFile]`.
4. **`IRewardRandom` живёт в `Game.Rewards` impl asmdef.** Это OK для текущего использования (только BookBoxRewardExpander), но если несколько модулей захотят shared RNG — подумать о выносе в Infrastructure.
5. **`UnityRewardRandom` не seedable.** Тесты используют `FakeRewardRandom` (queue), а прод-rng непредсказуем. Если потребуется reproducible production runs (debug повторов / save analysis) — добавить seeded mode.

### Связь с Shop

- Shop **не** ссылается на `Game.Rewards` (impl) — только на `Game.Rewards.API`. Это позволяет менять `LocalRewardGrantService` ↔ будущий `ServerRewardGrantService` без пересборки Shop'а.
- Shop передаёт `source` в формате `"shop:<lotId>"` — `LocalRewardGrantService` форвардит его в `IResourcesService.AddAsync(reason)` для аудит-логирования.
- `RewardSpec.Id` совпадает с `ShopLot.RewardId` — это id, по которому матчат expander'ы.

---

## Обзор архитектуры (target — server-authoritative, **не реализовано**)

> Всё ниже описывает целевую state для Phase 2+. При работе с текущим кодом ориентируйся на §0 выше.

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

> **Подтверждение `Fulfilled` приходит от рекламной сети, не от клиента.**
> На бэкенде настроен S2S-callback: Unity LevelPlay дергает публичный API бэкенда после фактического показа рекламы. Клиент в этой схеме только опрашивает статус intent'а — он не может «убедить» сервер, что реклама была показана. Поэтому фрод через подмену callback'a клиентом невозможен; вектор атаки переезжает на сторону LevelPlay (нужен валидный shared secret / подпись запроса в конфигурации callback URL'a).

---

## Замечания и техдолг

Раздел собран по итогам ревью архитектуры. Помечены **🔴 must-fix перед прод-релизом**, **🟡 хорошо бы поправить**, **⚪ long-term hygiene**.

### Server / wire protocol

- 🔴 **Idempotency key в `POST rewards/grant`.** Сейчас в контракте не описан. Сценарий потери ответа из-за таймаута (`GrantTimeoutSeconds = 15s`) + ретрая = двойная выдача награды. Нужно добавить `clientRequestId` / `idempotencyKey` в `GrantRewardCommand` и описать поведение сервера на дублирующий запрос.
- 🔴 **Два источника правды по состоянию игрока.**
  - `GrantRewardResponse.PlayerStateSnapshotDto` — приходит сразу после grant'а.
  - `IRewardPlayerStateSyncService.SyncFromGlobalSaveAsync` — забирает полный save с сервера.
  
  Не описано: какой выигрывает при конфликте, когда сервис sync вызывается (на boot? после каждого grant'а? по событию?), может ли отстать local snapshot. Без чёткой политики получим расхождения между «локально применённой» наградой и состоянием после следующего sync.
- 🟡 **Снапшот терпит два формата** (массив/объект инвентаря, `amount`/`stackCount`). Это признак того, что серверная схема уже дрейфовала, и backward-compat утёк в клиента. На проводе должен жить один нормализованный формат; нормализация — ответственность сервера.
- 🟡 **Polling `GET intent/status` 1s × 20s = 20 запросов/реклама.** Терпимо в MVP, но при 30 показах в день на игрока — лишние ~600 запросов. Long-poll / push через тот же канал, что грузит UI, снимает нагрузку.

### Reward model

- 🔴 **`TotalAmountForUi` в `RewardSpec` — рудимент. Удалить.** Это авторская сумма поверх `Resources` со своими `Amount` — производная величина, которая обязана быть посчитана, а не сохранена. Любое редактирование `Resources` без обновления `TotalAmountForUi` даст UI враньё. При удалении: проверить всех потребителей (BattlePass, RewardsWindow, RewardItemView) и заменить на `Resources.Sum(r => r.Amount)` или явный `Display.GetTotal(spec)` хелпер.
- 🟡 **`RewardKind` слишком узкий: `Resource | InventoryItem | Unknown`.** Не покрывает: unlock фич/контента, косметику вне инвентаря, прогресс/XP если он не «ресурс», разделение soft vs hard currency (которое часто требует разной политики отображения и подтверждения). Расширение дешёвое сейчас, болезненное — после первой live-награды нового типа.
- 🟡 **`RewardSources` — строковые константы (`"client"`, `"crafting"`).** Уходят в аналитику и server audit log. Опечатка в новом источнике скомпилируется. Переезд на enum + сериализация в строку на границе.
- ⚪ **`IRewardSpecProvider.TryGet` — sync API.** Если спецификации поедут на сервер (hot-config для балансировки), придётся ломать сигнатуру у всех потребителей. Заложить `UniTask<RewardSpec>` сразу — или явно зафиксировать в доке, что специфики **всегда** локальные.

### Ads flow / state machine

- 🟡 **Нет `Canceled` / `Aborted` состояний в `RewardAdFlowState`.** Что происходит при сворачивании приложения в `WaitingServerGrant`? При отмене показа? `RewardedShowResult.Canceled` есть, но в state machine, видимо, всё попадает в `Failed`. Это смазывает аналитику (`AdCanceled` ≠ `AdFailed`) и UX (разные сообщения, разная retry-логика).
- 🟡 **Двойная защита от параллелизма.** `AdsRewardFlowService` использует и `SemaphoreSlim`, и `Interlocked` для `IsFlowInProgress`. Это либо избыточно, либо защищает две разные критические секции (UI-флаг vs реальный mutex). Если второе — нужно явно разделить и переименовать; если первое — оставить одно.
- 🟡 **Mock-провайдер «может эмулировать серверные callback'и».** Мок SDK не должен играть роль сервера — это два разных мока. Сейчас грань размыта, тесты на integration становятся непредсказуемыми. Развести: `MockRewardedAdsProvider` отдаёт только SDK-результат; серверный mock — отдельная фикстура.

### Offline / fault tolerance

- 🔴 **Offline policy не описана.** Что произойдёт, если игрок офлайн и хочет посмотреть рекламу?
  - Legacy flow выдаст награду локально? (тогда фрод)
  - Server-confirmed flow покажет `NetworkError`?
  - Накапливаются ли intent'ы для досылки?
  
  Для ad-monetised игры это критично: офлайн-игроки видят закешированную рекламу и ждут награду. Стратегия «нет сети — нет награды» = тикеты саппорта.
- 🟡 **`RewardGrantFailureType` и `RewardGrantFlowResultType` пересекаются** (`Network` vs `NetworkError`, `Rejected` vs `ServerFailed`). Один — низкоуровневая transport-ошибка, второй — результат бизнес-флоу. Либо явное двухуровневое разделение в коде, либо один enum с подкатегориями.

### SRP / архитектурные мелочи

- 🟡 **`TODO` в `ResourcePlayerStateSnapshotHandler` про анимацию и UI-окно.** Snapshot-аппликатор не должен знать про UI. Анимация должна реагировать на событие применения снапшота (`OnSnapshotApplied`), а не сидеть внутри обработчика.
- ⚪ **Тестовая asmdef тянет `Game.Crafting`, `FortuneWheel`, `CardCollection`.** Это интеграционные тесты, замаскированные под unit-тесты ядра. Разделить на `Rewards.Tests.Unit` (только Rewards + моки) и `Rewards.Tests.Integration` (с реальными интеграциями).
- ⚪ **Префабы `IconFrame.prefab` / `RewardsWindow.prefab` в доке упомянуты, но не сказано, кто их грузит** (Addressables / Resources / прямая ссылка из SO). Для конфиг-driven системы — пробел.

### Что добавить в этот документ

1. **Контракт серверного API**: payload `POST rewards/grant`, формат `PlayerStateSnapshotDto`, схема ошибок, требования к idempotency.
2. **Политика consistency состояния**: когда применять snapshot из grant'a, когда дёргать global sync, кто win'ит при конфликте.
3. **Требования к S2S callback'у LevelPlay**: URL endpoint, формат payload, как настроен shared secret / подпись. Сейчас зафиксировано «есть на бэкенде» — для онбординга нового разработчика этого мало.
4. **Offline policy** — что показывает UI, что делает сервис, можно ли отложить grant.
5. **Аналитика**: какие события эмиттятся (client-side + server-side), payload, корреляция через `rewardIntentId`.
6. **Worst-case latency** — таблица «максимальное время от клика до окна наград» по каждой ветке.

### Сильные стороны (фиксируем, чтобы не сломать при рефакторе)

- Чистое разделение слоёв: SDK / транспорт / оркестратор / UI.
- Server-authoritative snapshot model — единственно правильный выбор для ad-rewards.
- Server-confirmed flow через intent + S2S callback закрывает классический фрод.
- Handler pattern (`IRewardHandler`) даёт расширяемость без правки ядра.
- Изоляция платформенного кода через conditional compilation + фабрику.
