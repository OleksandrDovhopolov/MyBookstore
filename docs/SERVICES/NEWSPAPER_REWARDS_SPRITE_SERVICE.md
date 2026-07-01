# Newspaper / Rewards Sprite Service

**Project**: MyBookstore  
**Date**: 2026-06-24  
**Status**: Draft specification

---

## Context

Сейчас логика спрайтов для newspaper UI живёт внутри оконных view/controller классов.

Из-за этого у `NewspaperWindow` спрайты грузятся при открытии окна, а при закрытии освобождаются.  
Следствие: при следующем открытии окна загрузка повторяется и иконки появляются с задержкой.

Целевое направление: вынести эту ответственность в отдельный долгоживущий инфраструктурный сервис, который:

- загружает нужные спрайты один раз;
- хранит их в памяти между открытиями окон;
- отдаёт уже готовые `Sprite` мгновенно;
- объединяет локальный `ScriptableObject` и Addressables за единым API;
- сам управляет временем жизни и очисткой кэша.

---

## Why Current Logic Delays

Текущая задержка появляется не из-за `await` сама по себе, а из-за жизненного цикла ресурсов:

- окно открывается;
- view запускает загрузку спрайтов;
- окно закрывается;
- view освобождает загруженные спрайты;
- при следующем открытии всё повторяется заново.

Чтобы сделать показ мгновенным, кэш должен жить вне окна.

---

## Goals / Non-goals

### Goals

Сервис является единым инфраструктурным источником UI-спрайтов в приложении.

Сервис должен:

- поддерживать получение спрайтов из локального `ScriptableObject`;
- поддерживать загрузку спрайтов через Addressables;
- кэшировать успешно загруженные Addressables-спрайты;
- объединять оба источника за единым API;
- обеспечивать синхронный доступ к уже доступным спрайтам;
- обеспечивать асинхронную загрузку отсутствующих спрайтов;
- предотвращать повторную параллельную загрузку одного ресурса;
- централизованно управлять освобождением Addressables-ресурсов;
- возвращать fallback-спрайт при ожидаемых ошибках.

### Non-goals

Сервис не должен:

- содержать бизнес-логику Newspaper или Rewards;
- знать о конкретных View, Window или Presenter;
- менять состояние UI-компонентов;
- загружать ресурсы напрямую из `Resources`;
- освобождать спрайты при закрытии отдельного окна;
- использовать `lotId`, `resourceId` или reward id как Addressables address без отдельного маппинга.

---

## Terminology

Нельзя смешивать в инфраструктурном API обычные строки `resourceId`, `lotId` и Addressables address. Иначе сервис быстро превратится в неявный string-based API.

Базовый тип ключа:

```csharp
public readonly record struct SpriteId(string Value);
```

Для ограниченного набора обязательных UI-спрайтов можно использовать enum:

```csharp
public enum UiSpriteId
{
    NewspaperBookOffer,
    NewspaperVintageGlobe,
    NewspaperCoffeePot,
    RewardFallback,
}
```

Для динамических reward/genre лучше оставить `SpriteId`, а не раздувать enum.

Правила терминов:

```text
SpriteId - логический идентификатор спрайта внутри приложения.
Addressables address - деталь конкретного источника данных.
resourceId/lotId - идентификаторы бизнес-домена.

Преобразование resourceId/lotId в SpriteId выполняется вне инфраструктурного
кэша либо через отдельный каталог маппинга.
```

---

## Architecture

Главное архитектурное решение: разделить сервис-кэш и источники данных.

`IUiSpriteService` отвечает за:

- единый публичный API;
- runtime cache;
- дедупликацию одновременных запросов;
- fallback policy;
- общий lifetime и cleanup.

`ScriptableObjectSpriteSource` и `AddressableSpriteSource` отвечают только за получение ресурса своим способом.

```csharp
public interface ISpriteSource
{
    bool TryGet(SpriteId id, out Sprite sprite);

    UniTask<SpriteLoadResult> LoadAsync(
        SpriteId id,
        CancellationToken cancellationToken);
}
```

Такой подход позволит позднее добавить AssetBundle, remote content или generated sprites без изменения окон и потребителей.

---

## Source Resolution Order

Рекомендуемая политика разрешения:

1. Проверить runtime cache.
2. Проверить локальный `ScriptableObject`.
3. Проверить Addressables mapping.
4. Вернуть fallback.
5. Если fallback тоже отсутствует, вернуть явную ошибку или `null` согласно контракту.

```text
Default resolution order:

Memory cache
    -> ScriptableObject source
    -> Addressables source
    -> fallback sprite
```

`ScriptableObject` в этом варианте может переопределять Addressables. Это удобно для обязательных ресурсов и локальной разработки.

Один `SpriteId` может одновременно находиться в двух источниках, но конфигурационная валидация должна выдавать warning, если это не отмечено как явный override.

---

## Public API

Публичный контракт должен явно разделять синхронный доступ, lazy-load, preload и cleanup.

```csharp
public interface IUiSpriteService
{
    bool TryGet(SpriteId id, out Sprite sprite);

    Sprite GetOrFallback(SpriteId id);

    UniTask<Sprite> GetAsync(
        SpriteId id,
        CancellationToken cancellationToken = default);

    UniTask PreloadAsync(
        IReadOnlyCollection<SpriteId> ids,
        CancellationToken cancellationToken = default);

    bool IsLoaded(SpriteId id);

    void Release(SpriteId id);

    void ReleaseAll();
}
```

Семантика:

- `TryGet` никогда не запускает загрузку.
- `GetOrFallback` никогда не запускает загрузку.
- `GetAsync` сначала проверяет cache и синхронные источники, затем Addressables.
- `PreloadAsync` не должен падать целиком из-за одного отсутствующего спрайта, если выбран tolerant-режим.
- `Release` освобождает только ресурс, которым сервис действительно владеет.
- `ReleaseAll` освобождает все Addressables handles, которыми владеет сервис.
- Отмена ожидания вызывающим кодом не обязана отменять общую загрузку, которой уже ждут другие потребители.

Последний пункт особенно важен при дедупликации запросов.

---

## ScriptableObject Configuration

Базовый вариант - единый каталог, который проще валидировать:

```csharp
[CreateAssetMenu(menuName = "Infrastructure/UI Sprite Catalog")]
public sealed class UiSpriteCatalog : ScriptableObject
{
    [SerializeField]
    private List<LocalSpriteEntry> _localSprites;

    [SerializeField]
    private List<AddressableSpriteEntry> _addressableSprites;

    [SerializeField]
    private Sprite _fallbackSprite;
}

[Serializable]
public sealed class LocalSpriteEntry
{
    public string Id;
    public Sprite Sprite;
}

[Serializable]
public sealed class AddressableSpriteEntry
{
    public string Id;
    public AssetReferenceSprite Reference;
}
```

Если источники должны подключаться независимо, лучше разделить конфигурацию:

- `LocalSpriteCatalog`
- `AddressableSpriteCatalog`
- `UiSpriteServiceConfig`

---

## Addressables Source

`AddressableSpriteSource` получает маппинг `SpriteId -> AssetReferenceSprite` или `SpriteId -> address`.

Характеристики источника:

- загружает спрайты асинхронно;
- кэширует operation handle или эквивалентный ownership token из Addressables wrapper;
- объединяет одновременные запросы одного ресурса;
- освобождает только те handles, которые загрузил сам;
- хранит ошибки отдельно от успешного кэша;
- не освобождает ресурс при закрытии UI-окна.

Кэшировать только `Sprite` недостаточно: для корректного `Release` нужно сохранить `AsyncOperationHandle<Sprite>` или использовать wrapper с эквивалентным токеном владения.

---

## Cache and Concurrent Loading

Базовая политика кэша:

```text
Cache policy: application/session lifetime.

Все успешно запрошенные спрайты остаются в памяти до ReleaseAll.
LRU, TTL и reference counting на первой итерации не используются.
```

Защита от одновременных загрузок:

```csharp
private readonly Dictionary<SpriteId, UniTask<Sprite>> _inFlight = new();
```

Правило:

```text
Для одного SpriteId в каждый момент существует не более одной операции загрузки.
Все параллельные запросы ожидают одну и ту же операцию.
После завершения операция удаляется из in-flight cache.
```

`CancellationToken` конкретного окна отменяет ожидание этого окна, но не всегда общую загрузку. Общую загрузку можно отменять только если сервис гарантирует, что нет других ожидающих потребителей.

Будущие расширения кэша:

- группы/скоупы;
- лимит памяти;
- LRU;
- release по feature;
- pinned assets.

Не стоит внедрять эти механики заранее без реальной необходимости.

---

## Resource Ownership

Правила владения:

- Спрайты из `ScriptableObject` не освобождаются сервисом.
- Addressables source хранит `AsyncOperationHandle<Sprite>` или эквивалентный token, а не только `Sprite`.
- Каждый успешно загруженный handle освобождается ровно один раз.
- Сервис не вызывает `Destroy` для загруженных спрайтов.
- Неуспешные handles очищаются согласно правилам Addressables wrapper.
- `ReleaseAll` вызывается инфраструктурным composition root при завершении игровой сессии, смене профиля или shutdown приложения.
- Закрытие UI-окна не влияет на lifetime кэша.

---

## Error and Fallback Policy

Нужно определить поведение для:

- неизвестного `SpriteId`;
- пустой ссылки в `ScriptableObject`;
- невалидного Addressables reference;
- ошибки загрузки;
- отмены;
- отсутствующего fallback;
- дубликатов ID.

Пример результата загрузки:

```csharp
public enum SpriteLoadStatus
{
    Success,
    NotConfigured,
    LoadFailed,
    Cancelled,
}

public readonly record struct SpriteLoadResult(
    SpriteLoadStatus Status,
    Sprite Sprite,
    Exception Exception = null);
```

Рекомендуемая политика:

- ожидаемое отсутствие ресурса: warning + fallback;
- техническая ошибка Addressables: error + fallback;
- отсутствующий fallback: error и `null`;
- отмена ожидания потребителем: не логировать как ошибку;
- в Development Build можно включать strict mode с исключением.

---

## Preload Policy

Preload инициирует инфраструктура или feature initializer, а не отдельные окна.

```text
Bootstrap preload:
- fallback;
- newspaper book-offer icon;
- frequently used reward icons.

Lazy load:
- редкие decor sprites;
- динамические genre/reward icons.

Окна не запускают собственный Addressables preload.
Feature initializer может запросить preload набора SpriteId.
```

В будущем можно добавить группы:

```csharp
UniTask PreloadGroupAsync(
    SpriteGroup group,
    CancellationToken cancellationToken = default);
```

Примеры групп:

- `CoreUi`
- `Newspaper`
- `Rewards`

---

## Lifecycle and Cleanup

Сервис регистрируется на уровне приложения или игровой сессии. Его lifetime длиннее lifetime окон.

Cleanup выполняется в контролируемой точке:

- завершение игровой сессии;
- смена профиля;
- logout;
- shutdown приложения;
- editor-only reset между play mode сессиями, если это требуется.

Оконные `OnDispose`, `OnClose` и `OnHide` не вызывают `Release` для глобально кэшируемых UI-спрайтов.

---

## Dependency Injection

Singleton здесь означает lifetime в DI-контейнере, а не `static Instance`.

```text
Сервис регистрируется в project/application scope DI-контейнера.
Окна получают IUiSpriteService через constructor injection.

Не рекомендуется:
- static singleton;
- Service Locator;
- прямой доступ View к Addressables wrapper.
```

Это сделает сервис тестируемым и не привяжет инфраструктуру к Unity lifecycle конкретного `MonoBehaviour`.

---

## Configuration Validation

Для Unity нужен явный слой валидации конфигурации.

Проверки:

- ID не пустой;
- нет повторяющихся ID;
- локальный `Sprite` не `null`;
- Addressables reference валиден;
- fallback назначен;
- один ID не находится в двух источниках без явного override;
- Addressables entry действительно загружает `Sprite`;
- проверка запускается через `OnValidate` или editor tool;
- build validation может завершать сборку ошибкой для обязательных ресурсов.

---

## Execution Flow

```text
GetAsync(id)
    |
    +-- runtime cache contains id? ------> return cached sprite
    |
    +-- local SO contains id? -----------> cache reference and return
    |
    +-- load already in progress? -------> await existing operation
    |
    +-- addressable mapping exists? -----> load -> cache handle -> return
    |
    +-- fallback exists? ----------------> return fallback
    |
    +-- return failure/null
```

---

## Integration Points

### Newspaper window

- [`NewspaperWindow`](../../Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs)  
  Сейчас открывает окно, запускает фоновую подгрузку и затем обновляет видимые иконки.

- [`NewspaperWindow.LoadSpritesAndRefreshAsync`](../../Assets/Game/Features/Newspaper/UI/NewspaperWindow.cs)  
  Сейчас ждёт `View.PreloadSpritesAsync` и затем обновляет только активные карточки.

- [`NewspaperWindowView`](../../Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs)  
  Сейчас хранит временные address-поля для newspaper спрайтов.

- [`NewspaperWindowView.PreloadSpritesAsync`](../../Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs)  
  Сейчас грузит спрайты через `ProdAddressablesWrapper`.

- [`NewspaperWindowView.ReleaseLoadedSprites`](../../Assets/Game/Features/Newspaper/UI/NewspaperWindowView.cs)  
  Сейчас освобождает спрайты при закрытии окна. После миграции этот cleanup должен уйти из view.

Для `NewspaperWindow` сервис должен уметь:

- вернуть общий спрайт для book offers;
- вернуть decor-спрайт по `lotId` через маппинг `lotId -> SpriteId`;
- поддержать текущие decor keys:
  - `newspaper_decor_vintage_globe`
  - `newspaper_decor_coffee_pot`

### Rewards window

- [`RewardsWindow`](../../Assets/Game/Features/Newspaper/UI/RewardsWindow.cs)  
  Сейчас передаёт в builder resolver `View.GetIconForReward`.

- [`RewardWindowView`](../../Assets/Game/Features/Newspaper/UI/Rewards/RewardWindowView.cs)  
  Сейчас хранит reward/genre/fallback спрайты прямо во view через serialized fields.

- [`RewardWindowView.GetIconForReward`](../../Assets/Game/Features/Newspaper/UI/Rewards/RewardWindowView.cs)  
  Сейчас содержит локальный маппинг `resourceId -> Sprite`.

Для `RewardsWindow` сервис должен уметь:

- вернуть иконку reward/genre по `resourceId` через маппинг `resourceId -> SpriteId`;
- вернуть fallback-иконку для non-book reward;
- не раскрывать `Addressables` API наружу во view или builder.

---

## Migration Plan

1. Ввести `SpriteId`, `SpriteLoadResult` и `IUiSpriteService`.
2. Создать конфигурационный `ScriptableObject` или отдельные catalogs для local/addressable/fallback entries.
3. Реализовать `ScriptableObjectSpriteSource`.
4. Реализовать `AddressableSpriteSource` с хранением ownership token/handle.
5. Реализовать сервис-кэш с resolution order, fallback policy и `_inFlight`.
6. Зарегистрировать сервис в application/project scope DI.
7. Перевести `NewspaperWindow` на `IUiSpriteService`.
8. Убрать загрузку и release из `NewspaperWindowView`.
9. Перевести `RewardsWindow` и `RewardWindowView` на `IUiSpriteService` или внешний resolver, который использует сервис.
10. Добавить editor/build validation для конфигурации.
11. Добавить тесты и smoke-сценарии.

При переносе в сервис окна не должны:

- сами грузить спрайты;
- сами кэшировать спрайты;
- сами вызывать `Release` на каждый `OnDispose`.

Окна должны только запрашивать уже готовую иконку у сервиса и обновлять UI.

---

## Testing Strategy

Покрыть unit/edit mode тестами:

- resolution order;
- fallback policy;
- отсутствие загрузки в `TryGet`;
- отсутствие загрузки в `GetOrFallback`;
- дедупликацию одновременных `GetAsync`;
- корректный `ReleaseAll`;
- отсутствие release для local SO sprites;
- tolerant `PreloadAsync`;
- поведение при отмене ожидания;
- валидацию конфигурации.

Отдельно полезен integration/smoke test для повторного открытия `NewspaperWindow`.

---

## Acceptance Criteria

- Повторное открытие `NewspaperWindow` не запускает повторную загрузку спрайтов.
- Два одновременных запроса одного Addressables-спрайта создают одну load operation.
- Закрытие окна не вызывает `Release` загруженных спрайтов.
- `ReleaseAll` освобождает каждый принадлежащий сервису handle ровно один раз.
- Локальный SO-спрайт возвращается без async operation.
- При ошибке Addressables возвращается fallback.
- Неизвестный ID логируется.
- После `ReleaseAll` Addressables-спрайт может быть загружен повторно.
- Сервис не освобождает ссылки, полученные из `ScriptableObject`.
- View и Window не зависят от Addressables API.

---

## Future Extensions

- Feature-scoped sprite groups.
- `PreloadGroupAsync`.
- Memory budget.
- LRU cache.
- Pinned assets.
- Remote content source.
- Generated sprites source.
- AssetBundle source.
