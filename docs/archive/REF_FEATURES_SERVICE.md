# IFeaturesService — Feature Flag Management

## Overview

`IFeaturesService` — сервис управления фича-флагами в проекте. Позволяет включать/отключать игровые возможности (аватары, таланты, туториалы и т.д.) на основе клиентских блокировок, серверных директив или выполнения игровых условий.

---

## Интерфейс

**Файл:** `Assets/Shared/CoreApi.Pure/Features/IFeaturesService.cs`

```csharp
public interface IFeaturesService : IServiceWithInitialization
{
    IEnumerable<FeatureId> AllFeatures { get; }

    bool IsFeatureAvailable(FeatureId featureId);

#if UNITY_BUILD
    void SetClientStateLock(FeatureId featureId, FeatureStateLock stateLock);
    FeatureStateLock GetClientStateLock(FeatureId featureId);
    void SubscribeOnActivation(FeatureId featureId, Action onActivated);
    void UnsubscribeOnActivation(FeatureId featureId, Action onActivated);
#endif
}
```

| Метод / Свойство | Назначение |
|---|---|
| `AllFeatures` | Все зарегистрированные фичи (`IEnumerable<FeatureId>`) |
| `IsFeatureAvailable(id)` | Проверить, активна ли фича прямо сейчас |
| `SetClientStateLock(id, lock)` | Принудительно заблокировать/разблокировать фичу на клиенте |
| `GetClientStateLock(id)` | Получить текущий клиентский lock-стейт |
| `SubscribeOnActivation(id, cb)` | Подписаться на событие активации фичи |
| `UnsubscribeOnActivation(id, cb)` | Отписаться от события активации фичи |

> Методы `SetClientStateLock`, `GetClientStateLock`, `Subscribe/UnsubscribeOnActivation` доступны только в `UNITY_BUILD`.

---

## Реализации

### FeaturesService (основная)

**Файл:** `Assets/Core/Features/FeaturesService.cs`  
**Атрибут:** `[InjectGenerate]`

Полноценная реализация для клиента (Unity). Управляет состоянием каждой фичи через три уровня приоритета:

```
1. Клиентский lock (storage)           ← наивысший приоритет
2. Серверный force-state (static data) ← второй приоритет
3. Игровые условия (GameEvents)        ← третий приоритет
```

#### Зависимости конструктора

| Параметр | Роль |
|---|---|
| `FeaturesEntry` | Хранение клиентских lock-состояний |
| `IStaticDataService` | Конфигурация фич из static data |
| `IGameEventsService` | Проверка условий активации |
| `IEventsObserverFactory` | Создание наблюдателей за событиями |
| `ILogService` | Логирование |
| `ISignalBus` | Публикация сигналов (`FeatureActivatedSignal`, `FeatureLockStateChangedSignal`) |
| `IClientServerSyncDataService` | Синхронизация клиент/сервер |
| `IAppStartSettings` | Начальные настройки приложения |

#### Жизненный цикл

```
Init()
  └─ Загружает все фичи из static data и appStartSettings
  └─ Для каждой фичи вызывает UpdateFeatureStatus()

UpdateFeatureStatus(featureId)
  ├─ Если ClientLock.Enabled → активировать
  ├─ Если ClientLock.Disabled → деактивировать
  ├─ Если ServerForceState.Enabled → активировать
  ├─ Если ServerForceState.Disabled → деактивировать
  └─ Иначе → CheckFeatureAvailabilityByCondition()

CheckFeatureAvailabilityByCondition(featureId)
  └─ Создаёт IEventsObserver для условий фичи
  └─ Подписывается на изменения → повторно вызывает UpdateFeatureStatus

MarkFeatureAsActivated(featureId)
  ├─ Добавляет в _activatedFeatures
  ├─ Вызывает подписанные колбэки
  └─ Публикует FeatureActivatedSignal
```

---

### FeaturesServiceStub (заглушка)

**Файл:** `Assets/Shared/CoreLogic/Features/FeaturesServiceStub.cs`

Стаб для серверного бэкенда и тестов. Все фичи всегда активны.

```csharp
IsFeatureAvailable(any) → true
GetClientStateLock(any) → FeatureStateLock.None
SetClientStateLock(...)  → no-op
Subscribe/Unsubscribe(…) → no-op
```

---

## DI-регистрация

### Серверный бэкенд (`NetCoreProject/Server.Backend/DependencyResolver.cs`)

```csharp
.AddScoped<IFeaturesService, FeaturesServiceStub>()
```

Lifecycle: **Scoped** — один экземпляр на HTTP-запрос.  
Реализация: `FeaturesServiceStub` (все фичи включены).

### Клиент (Unity)

Сервис инжектируется через dependency-холдеры с атрибутом `[InjectGenerate]`:

- `HeroesDataDependencies` — `public static IFeaturesService FeaturesService`
- `ScriptingDependencies` — `internal static IFeaturesService FeaturesService`
- `OffersDependencies` — `IFeaturesService FeaturesService`

Также доступен через `GameServices.Get<IFeaturesService>()` для окон и систем, не имеющих прямого DI.

---

## Примеры использования

### Проверка доступности фичи

```csharp
// AvatarAbilityHandler
if (!_featuresService.IsFeatureAvailable(FeatureId.Avatar) || isSilent)
    return;

// HeroModelFactory
private bool TalentsIsAvailable =>
    _featuresService.IsFeatureAvailable(FeatureId.Talents);
```

### Подписка на активацию фичи

```csharp
// TutorialsFeatureActivationWatcher
public void Init() {
    _tutorialsService.Pause(this);
    _featuresService.SubscribeOnActivation(FeatureId.Tutorials, OnActivated);
}

public void Reset() {
    _featuresService.UnsubscribeOnActivation(FeatureId.Tutorials, OnActivated);
}

// AdaptiveFpsService
public void Init() =>
    _featuresService.SubscribeOnActivation(FeatureId.AdaptiveFps, OnFeatureActivated);

public void Reset() =>
    _featuresService.UnsubscribeOnActivation(FeatureId.AdaptiveFps, OnFeatureActivated);
```

### Клиентская блокировка фичи (Debug/Admin)

```csharp
// Принудительно включить фичу на клиенте
_featuresService.SetClientStateLock(FeatureId.Avatar, FeatureStateLock.Enabled);

// Вернуть управление условиям
_featuresService.SetClientStateLock(FeatureId.Avatar, FeatureStateLock.None);
```

---

## Известные клиенты

| Файл | Использование |
|---|---|
| `AvatarAbilityHandler` | Гейт по `FeatureId.Avatar` |
| `HeroModelFactory` | Гейт по `FeatureId.Talents` |
| `ArmorPlateService` | Гейт на гир-механику |
| `DeficitService` | Гейт на дефицит |
| `TreasureMapsService` | Гейт на карты сокровищ |
| `TutorialsFeatureActivationWatcher` | Пауза туториалов до активации |
| `AdaptiveFpsService` | Отложенный старт после активации |
| `QuestBookWindowModel`, `SettingsWindow`, `RiftWindow`, `WindowsService` | Проверки через `GameServices.Get<>()` |

---

## Тестирование

В тестах сервис мокируется через **NSubstitute** (~16 тест-классов):

```csharp
_featuresService = Substitute.For<IFeaturesService>();
_featuresService.IsFeatureAvailable(FeatureId.Talents).Returns(true);
```

Файлы: `TreasureMapsServiceTests`, `LocalProfileTests`, `PlayerDossierTests`, `BaseOffersTests`, `DailyBonusTestsBase`, `AbstractCampaignTests`, `AdaptiveFpsServiceTests`, `DeficitServiceTests` и др.

---

## Сигналы

| Сигнал | Когда публикуется |
|---|---|
| `FeatureActivatedSignal` | Фича переходит из неактивной в активную (`MarkFeatureAsActivated`) |
| `FeatureLockStateChangedSignal` | Изменился клиентский lock-стейт (`SetClientStateLock`) |
