# Система AnimationTarget / AnimationBuilder

Путь в проекте: `Assets/Scripts/Game/Client/UI/AnimationBuilder/`

## 1. Назначение

Система реализует «летающие» UI-анимации наград (иконка ресурса/валюты/опыта/предмета летит из точки сбора к нужному элементу интерфейса и там «приземляется» с отскоком/пульсацией). Это единый переиспользуемый движок для всех сценариев вида «собрал ресурс на карте → иконка летит к счётчику в HUD → счётчик подпрыгивает», используемый в самых разных фичах: тап по ресурсу на карте, открытие сундуков, награды ивентов, контракты, копилка (piggy bank), продукция построек, доставка транспортом и т.д. Каждая фича не реализует полёт/приземление самостоятельно, а обращается к общему `AnimationBuilder`.

## 2. Ключевые классы и роли

### `AnimationBuilder.cs` — оркестратор
`Assets/Scripts/Game/Client/UI/AnimationBuilder/AnimationBuilder.cs`

- `class AnimationBuilder : IStartable` — сервис на VContainer DI.
- `Dictionary<Type, BaseUIAnimate> _dictionaryAnimate` — реестр стратегий анимации, ключ — конкретный тип `BaseUIAnimate` (`AnimateFlyingActor`, `AnimateResources`, `AnimateCurrency`, `AnimateExperience`, `AnimateReceipt`). Заполняется в `Start()`.
- `[Inject] Install(AnimationBuilderSettings, AudioManager)` — создаёт `CanvasAnimationBuilder` (корневой canvas для частиц), строит `Dictionary<AnimationType, ParticleAnimationBase> _particleAnimationDict` (`Bezier → ParticleCurvedAnimation`, `Exp → ParticleExpAnimation`), достаёт префабы частиц и настройки из `AnimationBuilderSettings`.
- **`Tween Animate<T>(BaseUIAnimateArg arg) where T : BaseUIAnimate`** — единая публичная точка входа для геймплейного кода. По `typeof(T)` находит нужную стратегию в `_dictionaryAnimate` и вызывает `.Animate(arg)`.
- `Tween GetAnimationTweenByType(AnimationType, CurvedParticleAnimationInfo)` — диспатч в реестр движения частиц (Bezier/Exp).
- `Sequence NewAnimationSequence()` — `DOTween.Sequence().SetUpdate(true)`, анимации идут независимо от `Time.timeScale` (не встают на паузе).
- `AnimationPool` (вложенный класс) — пулы `GameObjectStackPool<ActorParticleUi>` и `GameObjectStackPool<AnimatedResource>`, прогретые на `VisualParticleAmountLimit = 6` объектов — чтобы не создавать/уничтожать частицы через `Instantiate`.

### `Targets/AnimationTarget.cs` — базовый класс «точки приземления»

```csharp
public abstract class AnimationTarget : MonoBehaviour
```

Любой UI-элемент, к которому может лететь иконка (счётчик в HUD, кнопка инвентаря, off-screen якорь, fallback-точка), наследуется отсюда.

- `Awake()`/`OnDestroy()` — самостоятельная регистрация/дерегистрация в статическом реестре `AnimationTargets`. Просто наличие компонента в сцене делает его доступным для поиска.
- `virtual Vector3 Position` — точка назначения (по умолчанию `transform.position`, наследники переопределяют).
- Виртуальные хуки анимации:
  - `AnimationShow(...)` / `AnimationHide(...)` — показать/скрыть виджет-цель (например, выезд плашки счётчика перед приземлением частицы).
  - `AnimationIn()` — «отскок/пульс» в момент приземления частицы.
  - `OnTargetIn()` — вызывается вместе с приземлением, для служебной логики (обновление очереди/счётчика), отдельно от визуального твина.
- `SetHideAnimationLock(bool)` — блокирует скрытие цели, пока анимация активна.

Это не абстрактная фабрика, а MonoBehaviour-реализация паттерна «интерфейс + шаблонный метод», позволяющая размещать цели прямо в сценах/префабах.

### `Targets/AnimationTargets.cs` — статический реестр/резолвер

Отвечает на вопрос «к какой UI-цели должна лететь конкретная награда»:

- `Dictionary<OffscreenTargetType, List<OffScreenAnimationTarget>> _offscreenTargets`
- `Dictionary<ResourcePanelItemType, List<ResourceAnimationTarget>> _resourceRewardTargets`
- `RegisterAnimationTarget(AnimationTarget)` — вызывается из `AnimationTarget.Awake()`. По типу: `ResourceAnimationTarget` → в `_resourceRewardTargets` по `TargetResource`; `OffScreenAnimationTarget` → в `_offscreenTargets` по `OffscreenTargetType`. Остальные типы (`TileAnimationTarget`, `FallbackTarget`) в реестр не попадают — используются напрямую по ссылке.
- `ReleaseAnimationTarget(AnimationTarget)` — вызывается из `OnDestroy()`.
- **`GetTargetSafe(AnimationInfo info, AnimationTarget fallbackTarget)`** — основная точка резолва, используется всеми `Animate*`:
  1. Если задан явный `info.OffscreenTarget` (например, `InventoryWindow`) — резолв напрямую через off-screen словарь.
  2. Иначе `Resource` (может быть `ResourceType`, либо `ActorStaticType`/`ActorDynamicType`) конвертируется в ключ: валютные `ActorStaticType` мапятся в `ResourceType.SoftCurrency`/`HardCurrency` через `ActorKeyToAnimationTargetKey`.
  3. `ResourceType` → конвертируется в `ResourcePanelItemType` через `ResourcePanelHelper.GetBarByResourcesType` и ищется в `_resourceRewardTargets`.
  4. Прочие «предметные» actor-типы по умолчанию уходят в `OffscreenTargetType.Camp`.
  5. Если ничего не найдено — возвращается `fallbackTarget`.
- Если по ключу зарегистрировано несколько целей (например, одна и та же плашка ресурса существует в двух открытых окнах) — выбирается последняя зарегистрированная **активная в иерархии** (`activeInHierarchy`), иначе первая зарегистрированная, иначе fallback.

### Конкретные наследники `AnimationTarget`

| Класс | Файл | Назначение |
|---|---|---|
| `ResourceAnimationTarget` | `Targets/ResourceAnimationTarget.cs` | База для счётчиков ресурсов в HUD (монеты, кристаллы, опыт, энергия, ивент-бары). Регистрируется по `ResourcePanelItemType`. Клэмпит позицию по safe area экрана. Своя show/hide анимация выезда плашки. |
| `CurrencyAnimationTarget : ResourceAnimationTarget` | `Targets/CurrencyAnimationTarget.cs` | Счётчик валюты (soft/hard). `AnimationIn()` — scale-bounce иконки/текста, VFX, анимация «блика» по шейдеру (`_FlareTilingAndOffset`), настройки берутся из `BarsAnimationSettings`. Синхронизирует обновление числового значения через `IAnimationCallbackReceiver`. |
| `ExperienceAnimationTarget : ResourceAnimationTarget` | `Targets/ExperienceAnimationTarget.cs` | Счётчик опыта. `AnimationIn()` спавнит VFX `FX_UI_UpExperiance`, параметры анимации заданы инлайново (не через общий settings-asset). |
| `InventoryAnimationTarget : OffScreenAnimationTarget` | `Targets/InventoryAnimationTarget.cs` | Кнопка инвентаря (off-screen-цель, ключ `OffscreenTargetType.InventoryWindow`). Делегирует show/hide/приземление компоненту `CounterWithSlidePanel`. |
| `TileAnimationTarget : AnimationTarget` | `Targets/TileAnimationTarget.cs` | Пустой класс-маркер для тайлов на карте. В реестре `AnimationTargets` не индексируется, используется напрямую по ссылке. |
| `OffScreenAnimationTarget : AnimationTarget` | `Targets/OffScreenAnimationTarget.cs` | Цель, которая сама умеет выезжать из-за экрана и обратно (например, иконка каравана/кнопка путешествия). Использует `LineAnimation` (старт/конечный `Transform`), ключ — `OffscreenTargetType`. |
| `OffscreenTargetType` (enum) | `Targets/OffscreenTargetType.cs` | `Undefined, Camp, Trailer, Balloon, InventoryWindow, ZooManager, ZooUiMergeItem, TravelButton`. |
| `FallbackTarget : AnimationTarget` | `Targets/FallbackTarget.cs` | Пустая no-op цель — «точка отказа», когда для награды не нашлось реальной цели. Инстанс хранится в `CanvasAnimationBuilder.DefaultFallbackTarget`. |
| `CounterWithSlidePanel` | `Targets/CounterWithSlidePanel.cs` | Не является `AnimationTarget` — переиспользуемый компонент «выезжающий счётчик с очередью». Копит входящие `AnimationInfo` в очередь, схлопывает серию быстрых сборов в один видимый счётчик, который остаётся на экране `_hideCooldown` секунд и уезжает. Используется `InventoryAnimationTarget` и `ResourceAnimationTargetWithSlidePanel`. |
| `ResourceAnimationTargetWithSlidePanel : ResourceAnimationTarget` | `Targets/ResourceAnimationTargetWithSlidePanel.cs` | Вариант ресурсного счётчика, который вместо своей show/hide логики оборачивает `CounterWithSlidePanel` (транзитная плашка «+N», например для ивент-валюты или прогресса копилки). |

## 3. `Animate*` vs `Arg*` — стратегия + объект-параметр

`BaseUIAnimate` (`Animations/BaseUIAnimate.cs`) — абстрактная стратегия:
```csharp
public abstract Tween Animate(BaseUIAnimateArg arg);
```
Также содержит общие хелперы (доступ к canvas, пулам частиц, `VisualParticleAmountLimit`): `InstantiateAvatarParticle`, `InstantiateParticle`, `GetProperAnimationTarget` (обёртка над `AnimationTargets.GetTargetSafe`), `InstantiateFX`.

`BaseUIAnimateArg` — параллельный объект-параметр, база с полем `SortingOrderGroup SortingType`.

**Почему оба существуют:** `Animate<T>` — универсальный по типу стратегии (`T : BaseUIAnimate`), чтобы `AnimationBuilder` мог найти нужный инстанс в словаре по `typeof(T)`. Но сигнатура `BaseUIAnimate.Animate` принимает нетипизированный `BaseUIAnimateArg`, поэтому каждая конкретная стратегия сама делает даункаст с проверкой (`if (arg is not ArgAnimateXxx typed) { LogError; return; }`). Так реализована пара «стратегия (без состояния, создаётся один раз) + аргумент (данные конкретного вызова, часто строится fluent-билдером `With...`)».

Конкретные пары:

| Стратегия | Аргумент | Назначение |
|---|---|---|
| `AnimateResources` | `ArgAnimateResources` (fluent: `WithAnimationInfo`, `WithPosition`, `WithPartAnimationCallback`, `WithSortingOrder`) | Универсальная анимация одного/нескольких ресурсов. Если задан `Position` — одиночный полёт в фикс. точку экрана без show/hide хореографии цели. Иначе — полный цикл: показать цель → долететь → скрыть цель. |
| `AnimateCurrency` (+ `ArgAnimateCurrency` в том же файле) | ctor `(startScreenPosition, resourceType, amount, sortingType, callback)` | Валюта/энергия/опыт с явным `ResourceType`. Резолвит цель один раз, спавнит до 6 частиц со ступенчатой задержкой, свой ручной кубический безье-путь (`GetPath`), проигрывает SFX сбора через `AudioManager`. Используется при тапе по ресурсу на карте и в общей выдаче наград (`DropFlow`). |
| `AnimateExperience` (+ `ArgAnimateExperience`) | ctor `(startScreenPosition, callback, amount)` | Специализация для опыта, тип движения `AnimationType.Exp` (без безье). Блокирует скрытие цели на время полёта (`SetHideAnimationLock`). |
| `AnimateReceipt` (+ `ArgAnimateReceipt`) | `Receipt: RecipeConfig`, `Destination`, `StartScale`, `Multiplier` | Анимация «расходуемых ингредиентов» рецепта — летят не к HUD-счётчику, а к точке потребления (постройке). Не использует `AnimationTargets` вовсе. |
| `AnimateFlyingActor` (+ `ArgAnimateFlyingActor`) | `ObjectType, StartPosition, EndPosition, CompleteCallback, CancellationTokenSource, ControlScreenBorder, ...` | Самый общий примитив «из точки A в точку B». `EndPosition` задаётся вызывающим кодом напрямую, `AnimationTargets` не используется. Поддерживает отмену через `CancellationTokenSource`, автоматически подстраивает угол дуги для почти вертикальных путей. Применяется, например, для полёта предмета в виджет транспорта. |

`AnimationInfo` (`Animations/AnimationInfo.cs`) — общий payload одного предмета награды: `Resource`, `StartScreenPosition`, `Amount`, `Delay`, `OffscreenTarget` (явный оверрайд цели), `SortingType`, `Vfx`, `Text`, `SkipScaleStage`.

## 4. Механика резолва целей (сводно)

- **Регистрация** автоматическая: любой `ResourceAnimationTarget`/`OffScreenAnimationTarget` в сцене регистрируется в `Awake()`, снимается с регистрации в `OnDestroy()`. Геймплейный код не регистрирует цели вручную.
- **Резолв** — через `AnimationTargets.GetTargetSafe(AnimationInfo, fallbackTarget)`.
- Явный `AnimationInfo.OffscreenTarget` всегда имеет приоритет — так код принудительно указывает «лети в инвентарь» / «лети к кнопке путешествия» независимо от типа ресурса.
- `TileAnimationTarget` и `FallbackTarget` не индексируются в реестре. `AnimateFlyingActor` и `AnimateReceipt` вообще не используют `AnimationTargets`, получая конечную точку напрямую от вызывающего кода.

## 5. Подсистема частиц (`ParticleAnimation/`)

Более низкий уровень — «как именно иконка физически движется»:

- `ParticleAnimationBase` — абстрактная база: `abstract Tween AnimateCurvedParticle(CurvedParticleAnimationInfo)`.
- `ParticleCurvedAnimation` (`AnimationType.Bezier`) — строит `BezierCurve` (`UGI.MD.Curves.BezierCurve.GenerateRandomCurveWithBackOffset`/`GenerateRandomCurveWithAngledOffset`) от старта к цели со случайным перелётом/возвратом, затем `DOBezierCurveMove`. Также «pop»-анимация масштаба в процессе полёта. Поддерживает `ControlScreenBorder` — проверка 64 точек кривой на выход за экран и перегенерация пути при необходимости.
- `ParticleExpAnimation` (`AnimationType.Exp`) — используется для опыта: спавн с небольшим случайным смещением по кругу, прямой `DOMove` без кривой (дешевле, для множества мелких орбов опыта).
- `CurvedParticleAnimationInfo` — DTO-параметр для обеих реализаций.
- **Связь с целями**: `Animate*`-стратегии сначала резолвят `AnimationTarget` (или получают явную `Vector3`), затем строят `CurvedParticleAnimationInfo` с `DestinationPosition = destination.Position` и вызывают `AnimationBuilder.GetAnimationTweenByType(...)`. По завершении полёта стратегия вызывает `destination.OnTargetIn()` и `destination.AnimationIn().Play()`.

Поток: **резолв цели (`AnimationTargets`) → движение частицы (`ParticleAnimationBase`) → callback приземления на `AnimationTarget`.**

## 6. Настройки (ScriptableObject)

- **`AnimationBuilderSettings`** — корневой конфиг, инжектится в `AnimationBuilder.Install`. Ссылки на префаб `CanvasAnimationBuilder`, `BarsAnimationSettings`, `ParticleCurvedAnimationSettings`, префабы частиц `ActorParticleUi`/`AnimatedResource`.
- **`CanvasAnimationBuilder`** (не ScriptableObject, а MonoBehaviour-префаб) — инстанциируется в рантайме, хранит `Canvas` и `FallbackTarget DefaultFallbackTarget` сцены.
- **`ParticleCurvedAnimationSettings`** — таблица `ResourceType → ParticleAnimationParameters` (кривые ускорения/масштаба, `CurveAnimTime`, `CurveRange`/`CurveReturnDistance`/`CurveAngle`, `SpawnDelay` и т.д.), с фолбэком на `ResourceType.None`.
- **`BarsAnimationSettings`** — тюнинг анимации приземления на `CurrencyAnimationTarget`: кривые масштаба текста/иконки, `IconsFadeDuration`, параметры шейдерного блика (`FlareTiling`, `FlareBounds`, `FlareDuration`), `AnimationReplayDelay`, `EnableVfx`.

Все настройки — сериализованные ассеты, привязанные через инспектор, отдельно как Addressables/Resources не регистрируются.

## 7. TransportAnimations — смежная, но независимая система

`TransportAnimationsUI` и `TransportAnimationsActor` (`TransportAnimations/`) — оба ScriptableObject-фабрики твин-последовательностей. Они **не** наследуют `AnimationTarget`/`BaseUIAnimate`, не используют `AnimationTargets`/`AnimationBuilder`, лежат в этой папке только по тематической близости (визуалы транспорта):

- `TransportAnimationsUI` — появление, «дыхание» в простое, пульс предмета, «бамп» сбора, скрытие, полёт иконки к кнопке путешествия (`GetFlyToTravelBtnAnimation`, использует ту же `BezierCurve`-утилиту).
- `TransportAnimationsActor` — аналог для мирового (world-space) представления транспорта: появление, простой, спавн предмета, взрыв, скрытие.

Мост между системами — `TransportWidgetController`: он держит инжектированный `AnimationBuilder` для полёта собранных предметов **в** транспорт (через `AnimateFlyingActor`), а `TransportAnimationsUI`/`Actor` отвечают за собственную «личность» виджета (idle/appear/hide), не касаясь `AnimationTarget`.

## 8. Пример полного пути (end-to-end)

### Тап по ресурсу на карте (валюта)

1. `ActorResourceSystem.OnTap()` → `OnConfirm()` (`Assets/Scripts/Game/Client/Gameplay/Actors/ActorComponents/ActorResourceComponent/ActorResourceSystem.cs`).
2. Считает экранную точку и вызывает:
   ```csharp
   builder.Animate<AnimateCurrency>(new ArgAnimateCurrency(position, _resourceType, _resourceCount, SortingOrderGroup.UIBack));
   ```
3. `AnimationBuilder.Animate<AnimateCurrency>` находит стратегию в `_dictionaryAnimate`, вызывает `.Animate(arg)`.
4. `AnimateCurrency.Animate` строит `AnimationInfo`, резолвит цель через `AnimationTargets.GetTargetSafe` (для `SoftCurrency`/`HardCurrency` — через `ResourcePanelHelper.GetBarByResourcesType` до `ResourcePanelItemType.coin_bar`/`crystal_bar`), спавнит до 6 частиц с задержкой, каждая летит по кастомному безье-пути.
5. По завершении полёта каждой частицы: частица возвращается в пул, вызывается `destination.AnimationIn()` (для `CurrencyAnimationTarget` — bounce иконки/текста + VFX + блик), проигрывается SFX сбора.

### Сбор предмета инвентаря на карте

1. `ActorInventoryCollectableSystem.Collect()` создаёт `AnimationInfo` с явным `OffscreenTarget = OffscreenTargetType.InventoryWindow` и вызывает `Animate<AnimateResources>` с fluent-аргументом.
2. `AnimateResources.Animate` (без `Position`) выполняет полный цикл show → полёт → hide, резолвя `InventoryAnimationTarget` через off-screen словарь.
3. По приземлению `InventoryAnimationTarget.OnTargetIn()`/`AnimationIn()` делегируют в `CounterWithSlidePanel`, который обновляет счётчик и проигрывает slide-in/бамп.

### Выдача наград пачкой (сундуки, ивенты, контракты, копилка)

`DropFlow.SpawnResources` для валютных типов наград (`SoftCurrency`, `HardCurrency`, `EnergyCurrency`, `CampExp`) вызывает тот же `AnimateCurrency` — единая точка входа для всех источников наград.

### Продукция построек (расход рецепта)

`ActorProductionBuildingSystem` вызывает `Animate<AnimateReceipt>` — ингредиенты рецепта летят внутрь постройки, без обращения к `AnimationTargets`.

### Полёт предмета в виджет транспорта

`TransportWidgetController.AddItemsIntoTransport` вызывает `Animate<AnimateFlyingActor>` с `EndPosition = View.transform.position` — цель передаётся напрямую, без резолва через реестр.

## 9. Краткая справочная таблица файлов

| Файл | Роль |
|---|---|
| `AnimationBuilder.cs` | Оркестратор: `Animate<T>`, диспатч движения частиц, пулы, доступ к canvas/настройкам |
| `Targets/AnimationTarget.cs` | Абстрактная база «точки приземления», самостоятельная регистрация |
| `Targets/AnimationTargets.cs` | Статический реестр + резолвер целей |
| `Targets/ResourceAnimationTarget.cs` | База HUD-счётчика ресурса |
| `Targets/ResourceAnimationTargets.cs` | Дублирующий инстанс-хелпер резолва (избыточен) |
| `Targets/ResourceAnimationTargetWithSlidePanel.cs` | Ресурсный счётчик на базе `CounterWithSlidePanel` |
| `Targets/CurrencyAnimationTarget.cs` | Счётчик валюты, bounce+VFX+блик при приземлении |
| `Targets/ExperienceAnimationTarget.cs` | Счётчик опыта, bounce+VFX |
| `Targets/InventoryAnimationTarget.cs` | Кнопка инвентаря на базе `CounterWithSlidePanel` |
| `Targets/TileAnimationTarget.cs` | Пустой маркер для тайлов, не индексируется |
| `Targets/OffScreenAnimationTarget.cs` | Off-screen цель со своим маршрутом (`LineAnimation`) |
| `Targets/OffscreenTargetType.cs` | Enum-ключи off-screen целей |
| `Targets/FallbackTarget.cs` | No-op цель последнего шанса |
| `Targets/CounterWithSlidePanel.cs` | Переиспользуемый выезжающий счётчик с очередью |
| `Animations/BaseUIAnimate.cs` | База стратегии + общие хелперы |
| `Animations/BaseUIAnimateArg.cs` | База объекта-параметра |
| `Animations/AnimateResources.cs` / `ArgAnimateResources.cs` | Универсальная анимация ресурсов |
| `Animations/AnimateCurrency.cs` (+ `ArgAnimateCurrency`) | Полёт валюты с кастомным путём и SFX |
| `Animations/AnimateExperience.cs` / `ArgAnimateExperience.cs` | Полёт опыта, блокировка скрытия цели |
| `Animations/AnimateReceipt.cs` / `ArgAnimateReceipt.cs` | Визуал расхода ингредиентов рецепта |
| `Animations/AnimateFlyingActor.cs` / `ArgAnimateFlyingActor.cs` | Общий полёт «точка → точка», без реестра целей |
| `Animations/AnimationInfo.cs` | Общий DTO награды |
| `Animations/LineAnimation.cs` | Хелпер пути старт/финиш для `OffScreenAnimationTarget` |
| `Animations/ResourceTargetBounceAnimation.cs` | Отдельный компонент scale-bounce |
| `ParticleAnimation/ParticleAnimationBase.cs` | База стратегии движения частицы |
| `ParticleAnimation/ParticleCurvedAnimation.cs` | Безье-полёт + DTO `CurvedParticleAnimationInfo` |
| `ParticleAnimation/ParticleExpAnimation.cs` | Прямолинейный полёт (для опыта) |
| `ParticleAnimation/ParticleCurvedAnimationSettings.cs` | Настройки безье/масштаба по `ResourceType` |
| `ParticleAnimation/BarsAnimationSettings.cs` | Настройки bounce/блика для валютного счётчика |
| `Settings/AnimationBuilderSettings.cs` | Корневой конфиг: префабы + ссылки на настройки |
| `Settings/CanvasAnimationBuilder.cs` | Scene-root: canvas + fallback-цель |
| `View/ActorParticleUi.cs` | Пуловая частица (картинка+текст+сортировка) |
| `View/AnimatedResource.cs` | `ActorParticleUi` + доп. слот под FX |
| `TransportAnimations/TransportAnimationsUI.cs` | Независимые твины для UI-виджета транспорта |
| `TransportAnimations/TransportAnimationsActor.cs` | Независимые твины для world-space актора транспорта |
