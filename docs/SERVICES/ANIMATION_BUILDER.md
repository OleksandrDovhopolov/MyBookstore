# Resource Animation System v1

Документ описывает текущую систему UI-анимаций ресурсов в MyBookstore. Старый `AnimationBuilder` из проекта-референса больше не является источником правды: из него оставлены только идеи отдельного animation root, пула частиц, SO-настроек, target registry и DOTween-полёта по кривой.

## Назначение

Система показывает "летящие" ресурсы между точками интерфейса и мира:

- `World -> Screen`: из world-position/transform к UI-цели, например в HUD gold.
- `Screen -> World`: из UI-точки к screen-проекции world-position/transform.
- `Screen -> Screen`: между двумя UI-точками, например reward card -> HUD.

В v1 все полёты визуально живут в overlay UI canvas. World-точки не создают world-space particle; они проецируются камерой в screen/canvas coordinates.

## Runtime Shape

Публичный API и authoring primitives лежат в `Assets/Game/Infrastructure/ResourceAnimations`:

- `IResourceAnimationService`
- `ResourceAnimationRequest`
- `ResourceAnimationEndpoint`
- `IResourceAnimationTargetRegistry`
- `ResourceAnimationSettings`
- `ResourceParticleView`
- `ResourceAnimationTargetIds`

Runtime-реализация, которой нужен `IUICanvasRoot`, лежит в `Assets/Game/Core/UI/ResourceAnimations`:

- `ResourceAnimationService`
- `ResourceAnimationEndpointResolver`
- `ResourceParticlePool`

Такой split оставляет `Infrastructure` местом хранения API/settings/view primitives, но не создаёт цикл `Infrastructure <-> Game.Core.UI`: `Game.Core.UI` зависит от `Infrastructure`, а не наоборот.

## DI / Bootstrap

Регистрация находится в `ResourceAnimationsVContainerBindings` и вызывается из `BootstrapInstaller` после UI/sprite сервисов:

```csharp
builder.RegisterUiSystem(_uiCanvasRootPrefab);
...
builder.RegisterUiSprites(_uiSpriteCatalog);
builder.RegisterResourceAnimations(_resourceAnimationSettings);
```

Регистрируются:

- `ResourceAnimationSettings` instance или runtime default, если asset не назначен.
- `IResourceAnimationTargetRegistry -> ResourceAnimationTargetRegistry`.
- Build callback привязывает registry к static facade `ResourceAnimationTargets`, чтобы prefab/scene
  `ResourceAnimationTargetTag` компоненты могли регистрироваться без DI.
- `IResourceAnimationService -> ResourceAnimationService`.

В `BootstrapInstaller` должен быть назначен `ResourceAnimationSettings` asset. Если asset не назначен, сервис не падает, но без `ParticlePrefab` анимация будет no-op с warning.

## Request API

Базовый вызов:

```csharp
await _resourceAnimations.PlayAsync(
    new ResourceAnimationRequest(
        resourceId: ResourceIds.Gold,
        amount: 25,
        from: ResourceAnimationEndpoint.Rect(rewardRect),
        to: ResourceAnimationEndpoint.RegisteredTarget(
            ResourceAnimationTargetIds.Resource(ResourceIds.Gold))),
    ct);
```

`ResourceAnimationRequest` содержит:

- `ResourceId`: доменный id ресурса.
- `Amount`: количество; `0` означает no-op.
- `From`: начальная точка.
- `To`: конечная точка.
- `SpriteId`: optional override. Если пусто, sprite id = `ResourceId`.

`IResourceAnimationService` не меняет ресурсы и не подписывается на `IResourcesService.Changed`. Сервис только проигрывает визуал. Причина: `ResourceChangeEvent` знает `ResourceId/Delta/Reason`, но не знает source/target позицию.

## Endpoints

`ResourceAnimationEndpoint` поддерживает:

- `ScreenPoint(Vector2)`: уже готовая screen-точка.
- `WorldPosition(Vector3, Camera camera = null)`: world-точка, проецируется через указанную camera или `Camera.main`.
- `WorldTransform(Transform, Camera camera = null)`: позиция transform, проецируется так же.
- `Rect(RectTransform)`: центр UI rect.
- `RegisteredTarget(string targetId)`: цель из `IResourceAnimationTargetRegistry`.

`ResourceAnimationEndpointResolver` приводит любой endpoint к local point внутри `ResourceAnimationRoot`. Если точку нельзя разрешить (нет camera, null transform, отсутствует registered target), сервис пишет warning и завершает вызов без exception.

## Animation Root

`ResourceAnimationService` лениво создаёт `ResourceAnimationRoot` под `IUICanvasRoot.WindowsRoot` (fallback: `HudRoot`).

Root получает отдельный `Canvas`:

- `overrideSorting = true`
- `sortingOrder = ResourceAnimationSettings.SortingOrder`
- default sorting order: `3500`

Root растягивается на весь parent (`anchorMin = 0`, `anchorMax = 1`, zero offsets) и не блокирует input.

## Particles

`ResourceParticleView` - простой pooled UI particle:

- `RectTransform`
- `CanvasGroup`
- `Image` для иконки
- optional text-like component для количества

Текстовое поле сделано как `MonoBehaviour`, у которого есть writable string property `text`. В prefab можно назначить `TMP_Text`, но `Infrastructure` не получает прямую зависимость от TextMeshPro.

Sprite берётся через существующий `IUiSpriteProvider`:

- `SpriteId`, если указан.
- иначе `ResourceId`.
- если загрузка не дала sprite, используется `ResourceAnimationSettings.FallbackSprite`.

Количество видимых частиц:

```csharp
Clamp(abs(amount), 1, settings.MaxParticles)
```

При `amount == 0` анимация не запускается. Default `MaxParticles = 6`.

Пул отдельный queue-based (`ResourceParticlePool`), а не `UIListPool`, потому что частицы разных анимаций могут завершаться не в порядке выдачи.

## Tweening

Каждая анимация строится через DOTween `Sequence` с `SetUpdate(true)`, чтобы UI-анимация не зависела от `Time.timeScale`.

Для каждой частицы:

- стартовая позиция = resolved `From`;
- конечная позиция = resolved `To`;
- control point строится вокруг midpoint с random spread;
- движение идёт по quadratic Bezier;
- scale tween идёт от `StartScale` до `EndScale`;
- между частицами используется `Stagger`.

При complete, kill или cancellation частицы возвращаются в pool и очищаются.

## Target Registry

`IResourceAnimationTargetRegistry` хранит соответствие:

```text
targetId -> RectTransform
```

Поведение:

- `Register` заменяет старую цель с тем же id.
- `Unregister` снимает цель только если передан тот же `RectTransform`.
- `TryGetTarget` удаляет мёртвые Unity-ссылки и возвращает `false`.

Target id для ресурсов строится через:

```csharp
ResourceAnimationTargetIds.Resource(ResourceIds.Gold) // "resource:Gold"
```

Первый wired target - HUD gold:

- `ResourceAnimationTargetTag` вешается на нужный `RectTransform` в HUD prefab.
- В поле target id указывается `resource:Gold`.
- Компонент регистрирует target через `ResourceAnimationTargets` на `OnEnable` и снимает на `OnDisable`.

Остальные UI-точки могут передавать `RectTransform` напрямую в request или регистрировать свои target id позже.

## Settings

`ResourceAnimationSettings` создаётся через:

```text
Assets -> Create -> Game -> UI -> Resource Animation Settings
```

Поля:

- `ParticlePrefab`
- `MaxParticles` default `6`
- `Duration`
- `Stagger`
- `BezierSpread`
- `StartScale`
- `EndScale`
- `PositionEase`
- `ScaleEase`
- `FallbackSprite`
- `SortingOrder` default `3500`

Минимальная prefab-настройка:

- GameObject с `RectTransform`
- `CanvasGroup`
- `ResourceParticleView`
- child/current `Image`, назначенный в `_icon`
- optional `TMP_Text`, назначенный в `_countLabel`

## What Was Not Ported

Из старого референса намеренно не переносились:

- `AnimationBuilder` как generic strategy registry.
- `BaseUIAnimate`, `AnimateCurrency`, `AnimateResources`, `AnimateExperience`, `AnimateReceipt`.
- actor/experience/receipt/transport доменные сценарии.
- широкий static `AnimationTargets` из референса. В проекте есть только узкий `ResourceAnimationTargets`
  facade над текущим `IResourceAnimationTargetRegistry`, чтобы prefab components могли self-register targets.
- `CanvasAnimationBuilder` prefab как отдельная обязательная сущность.
- старые `ResourceType`, `ActorStaticType`, `IconsLibrary`, `ActorsStorage`.
- slide panels, bars flare/bounce, VFXPool.

Эти части были завязаны на другой проект и сейчас только увеличили бы связность. Если появятся разные классы анимаций с разной логикой, strategy layer можно добавить поверх текущего `IResourceAnimationService`, не ломая request/endpoint API.

## Tests / Verification

EditMode tests находятся в `Assets/Game/Core/UI/Tests/Editor/ResourceAnimations`:

- `ResourceAnimationTargetRegistryTests`
- `ResourceAnimationEndpointResolverTests`
- `ResourceAnimationServiceTests`

Покрыто:

- register/replace/unregister/missing target;
- screen/rect/world endpoint resolving;
- missing registered target;
- particle count clamp;
- default/explicit sprite id.

Manual verification:

- `Screen -> Screen`: reward/window rect -> HUD gold target.
- `World -> Screen`: world transform -> HUD gold target.
- `Screen -> World`: UI rect -> world transform projection.
- cancellation/window close не оставляет активные particle objects.
- missing registered target даёт warning/no-op, не exception.
