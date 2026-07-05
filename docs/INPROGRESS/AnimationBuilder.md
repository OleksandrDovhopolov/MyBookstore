# AnimationBuilder — архитектура и инфраструктура

> Собрано из проекта `C:\Projects\bigmerge\MD` (Unity, VContainer, DOTween).
> Namespace ядра: `UGI.MD.UI.RewardAnimation`.
> Расположение исходников: `Assets/Scripts/Game/Client/UI/AnimationBuilder/`.

## 1. Назначение

`AnimationBuilder` — центральный сервис для UI-анимаций "летающих" наград/ресурсов:
частицы валюты, ресурсов, опыта, чеков крафта, а также перелёт "актёров" (юнитов/объектов)
из одной точки экрана в другую с последующим "приходом" в HUD-таргет (иконку валюты, панель
ресурсов, кнопку инвентаря и т.д.).

Это DI-синглтон (`IStartable` в VContainer), который:
- хранит канвас для всех аниммаций и пул переиспользуемых партиклов;
- регистрирует набор стратегий анимаций (`BaseUIAnimate`) по типу;
- предоставляет фабрику независимых от `Time.timeScale` DOTween-секвенций;
- делегирует расчёт самой кривой полёта частицы (`ParticleAnimationBase`);
- находит конечную точку анимации через статический реестр таргетов (`AnimationTargets`).

## 2. Регистрация в DI (VContainer)

`Assets/Installers/Scripts/GeneralInstaller.cs`:

```csharp
[SerializeField] private AnimationBuilderSettings _animationBuilderSettings;
...
builder.RegisterEntryPoint<AnimationBuilder>().AsSelf().WithParameter(_animationBuilderSettings);
```

`AnimationBuilder` регистрируется как **entry point** (реализует `IStartable`), а
`AnimationBuilderSettings` (ScriptableObject) передаётся как параметр конструктора через
`[Inject] Install(...)`.

## 3. Основной класс `AnimationBuilder`

Файл: `AnimationBuilder.cs`

```csharp
public class AnimationBuilder : IStartable
{
    private readonly Dictionary<Type, BaseUIAnimate> _dictionaryAnimate = new();

    [Inject]
    public void Install(AnimationBuilderSettings animationBuilderSettings, AudioManager audioManager)
    {
        _canvasAnimationBuilder = GameObject.Instantiate(animationBuilderSettings.CanvasAnimationBuilder);
        _particleAnimationDict = new Dictionary<AnimationType, ParticleAnimationBase>
        {
            { AnimationType.Bezier, new ParticleCurvedAnimation() },
            { AnimationType.Exp,    new ParticleExpAnimation() }
        };
        ...
    }

    public void Start()
    {
        _dictionaryAnimate.Add(TypeOf<AnimateFlyingActor>.Raw, new AnimateFlyingActor(this));
        _dictionaryAnimate.Add(TypeOf<AnimateResources>.Raw,   new AnimateResources(this));
        _dictionaryAnimate.Add(TypeOf<AnimateCurrency>.Raw,    new AnimateCurrency(_audioManager, this));
        _dictionaryAnimate.Add(TypeOf<AnimateExperience>.Raw,  new AnimateExperience(this));
        _dictionaryAnimate.Add(TypeOf<AnimateReceipt>.Raw,     new AnimateReceipt(this));

        AnimationPool = new AnimationPool(Transform, _particleUi, _animatedResourcePrefab);
    }

    public Sequence NewAnimationSequence() => DOTween.Sequence().SetUpdate(true);

    public Tween Animate<T>(BaseUIAnimateArg baseUIAnimateArg) where T : BaseUIAnimate
        => _dictionaryAnimate[TypeOf<T>.Raw].Animate(baseUIAnimateArg);

    public Tween GetAnimationTweenByType(AnimationType animationType, CurvedParticleAnimationInfo animationInfo)
        => _particleAnimationDict[animationType].AnimateCurvedParticle(animationInfo);
}
```

Ключевые моменты:

- **`Install`** (Inject) — конструирует зависимость от `AnimationBuilderSettings` и `AudioManager`,
  инстанцирует `CanvasAnimationBuilder` (префаб с канвасом и fallback-таргетом).
- **`Start`** (`IStartable`, вызывается VContainer'ом после построения графа) — регистрирует
  все конкретные стратегии анимаций в `_dictionaryAnimate` (registry по типу-ключу) и создаёт
  `AnimationPool`.
- **`Animate<T>`** — точка входа для клиентского кода: по типу-строгому дженерику `T`
  (например `AnimateCurrency`) находит соответствующую стратегию и запускает её `Animate(arg)`.
  Такой подход — по сути `Dictionary<Type, Strategy>`, где тип — "тег" стратегии.
- **`GetAnimationTweenByType`** — второй уровень диспетчеризации: сама "форма" полёта частицы
  (кривая Безье или прямое движение для опыта) выбирается через `AnimationType` enum
  (`Bezier` / `Exp`), не зависит от `AnimateXxx`-стратегии сверху.
- **`NewAnimationSequence()`** — все анимации билдера всегда создаются через этот метод,
  что гарантирует `SetUpdate(true)`, т.е. независимость от `Time.timeScale` (важно, если игра
  ставится на паузу, а UI-анимация наград должна доиграть).

### `AnimationPool`

```csharp
public class AnimationPool
{
    public const int VisualParticleAmountLimit = 6;
    public GameObjectStackPool<ActorParticleUi> ParticlesPool { get; }
    public GameObjectStackPool<AnimatedResource> AnimatedResourcePool { get; }
}
```

Обёртка над двумя пулами объектов (`GameObjectStackPool<T>` из `UGI.Utils.Pool`):
- пул визуальных партиклов-"иконок" (`ActorParticleUi`) — то, что летает по экрану;
- пул `AnimatedResource` (наследник `ActorParticleUi` с доп. `_fx`) — для более "богатых"
  анимаций (используется в отдельных сценариях, не во всех стратегиях).

`VisualParticleAmountLimit = 6` — это же значение используется как "потолок" количества
одновременно видимых партиклов в стратегиях (`AnimateResources`, `AnimateCurrency` и т.д.):
если в награде 50 монет, реально летит максимум 6 иконок, остальное "виртуально".

## 4. Settings / конфигурация (ScriptableObject-инфраструктура)

### `AnimationBuilderSettings` (`Settings/AnimationBuilderSettings.cs`)

```csharp
public class AnimationBuilderSettings : ScriptableObject
{
    public CanvasAnimationBuilder CanvasAnimationBuilder;
    public BarsAnimationSettings BarsAnimationSettings;
    public ParticleCurvedAnimationSettings ParticleCurvedAnimationSettings;
    public ActorParticleUi ParticleUi;
    public AnimatedResource AnimatedResourcePrefab;
}
```

Единая точка конфигурации, ассет: `Assets/Scripts/Game/Client/UI/AnimationBuilder/Settings/AnimationBuilderSettings.asset`.
Ссылается на:
- префаб канваса (`CanvasAnimationBuilder`),
- параметры анимации "баров"/иконок ресурсов (`BarsAnimationSettings`),
- параметры кривых полёта партиклов (`ParticleCurvedAnimationSettings`),
- префабы партиклов (`ActorParticleUi`, `AnimatedResource`).

### `CanvasAnimationBuilder` (`Settings/CanvasAnimationBuilder.cs`)

```csharp
public class CanvasAnimationBuilder : MonoBehaviour
{
    public Canvas Canvas;
    public FallbackTarget DefaultFallbackTarget;
}
```

Простой MonoBehaviour-держатель, инстанцируется в `Install()`. Даёт `AnimationBuilder`
доступ к `Canvas` (сортировка) и `DefaultFallbackTarget` — таргету "по умолчанию", если для
ресурса не найден активный конкретный таргет в сцене.
Prefab: `Assets/Prefabs/Global/ProjectContextControllers/ResourceAnimation/CanvasAnimationBuilder.prefab`.

### `ParticleCurvedAnimationSettings` / `ParticleAnimationParameters`

`ScriptableObject`, хранит `SerializableDictionaryBase<ResourceType, ParticleAnimationParameters>`
— то есть для каждого `ResourceType` (и `None` как fallback) настраивается отдельный набор
параметров кривой полёта: время анимации, кривые ускорения/масштаба, диапазон "разброса"
кривой Безье, дилей между партиклами и т.п. (`GetResourceAnimationParameters` — точка доступа,
c fallback на `ResourceType.None`, если конкретный тип не задан).

### `BarsAnimationSettings`

`ScriptableObject` с параметрами анимации "бара" (иконка ресурса в HUD, которая подпрыгивает и
мигает при получении ресурса): длительность fade, flare (блик), масштаб иконки/текста.

## 5. Стратегии анимаций (`BaseUIAnimate` + наследники)

### Базовый класс `BaseUIAnimate` (`Animations/BaseUIAnimate.cs`)

```csharp
public abstract class BaseUIAnimate
{
    public abstract Tween Animate(BaseUIAnimateArg baseUIAnimateArg);

    protected AnimationBuilder _animationBuilder;
    protected AnimationPool animationPool => _animationBuilder.AnimationPool;
    protected GameObjectStackPool<ActorParticleUi> _particlesPool => animationPool.ParticlesPool;

    protected ActorParticleUi InstantiateAvatarParticle(AnimationInfo info) { ... }
    protected ActorParticleUi InstantiateParticle(AnimationInfo info) { ... }
    protected AnimationTarget GetProperAnimationTarget(AnimationInfo info, FallbackTarget fallbackTarget = null) { ... }
    protected GameObject InstantiateFX(Transform root, string vfx, ...) { ... }
}
```

Общий шаблон для всех стратегий: конструктор берёт `AnimationBuilder` (доступ к канвасу, пулу,
настройкам), а протектед-методы дают переиспользуемые операции: взять партикл из пула,
навесить на него VFX, найти правильный `AnimationTarget` через статический реестр
`AnimationTargets`.

`BaseUIAnimateArg` (`Animations/BaseUIAnimateArg.cs`) — базовый класс для аргументов каждой
стратегии, содержит только `SortingOrderGroup SortingType` (порядок отрисовки/канваса).
Каждая стратегия использует свой `ArgAnimateXxx : BaseUIAnimateArg`.

### Реестр стратегий → `enum AnimationType` (Bezier/Exp) — это другой уровень!

Важно не путать два разных enum/словаря:
1. `Dictionary<Type, BaseUIAnimate> _dictionaryAnimate` в `AnimationBuilder` — "что за
   сценарий" (валюта, ресурсы, опыт, чек, летящий актёр). Ключ — C#-тип стратегии.
2. `Dictionary<AnimationType, ParticleAnimationBase> _particleAnimationDict` — "какой траекторией
   летит частица" (`Bezier` — кривая Безье со случайным изгибом, `Exp` — прямое движение по
   прямой с масштабом для опыта). Ключ — enum `AnimationType`.

Любая стратегия сверху может использовать любую из двух траекторий снизу.

### Конкретные стратегии (`Animations/*.cs`)

| Класс | Arg | Что делает |
|---|---|---|
| `AnimateFlyingActor` | `ArgAnimateFlyingActor` | Перелёт одного партикла (актёра) из точки А в точку Б по кривой Безье (`AnimationType.Bezier`). Есть спец-обработка вертикального перелёта (когда старт/финиш почти на одной X — `useAngledCurve`, угол 90°), поддержка `CancellationTokenSource`, кастомного масштаба (`CustomScale`), `ControlScreenBorder` (не дать кривой уйти за экран). |
| `AnimateResources` | `ArgAnimateResources` | Самая сложная стратегия. Два режима: (1) если задан `Position` — просто летит в конкретную мировую точку (используется, например, при "клике по ресурсу"); (2) если `Position == null` — полный цикл: `inAnimation` (показ таргетов, `AnimationShow`) → `resourceAnimation` (полёт партиклов к каждому таргету, кол-во партиклов ограничено `VisualParticleAmountLimit`, лишние — с нарастающим интервалом `ParticleDelayCoefficient`) → `outAnimation` (скрытие таргетов, `AnimationHide`). Строится через fluent `ArgAnimateResources` (`WithAnimationInfo`, `WithPosition`, `WithAnimation`, `WithPartAnimationCallback`, `WithSortingOrder`). |
| `AnimateCurrency` | `ArgAnimateCurrency` | Полёт монет/кристаллов с экрана в HUD-бар валюты. Кастомная траектория `GetPath` (ручная кубическая кривая Безье с рандомным изгибом в сторону, зависящую от знака X), fade иконки перед концом полёта, звук через `AudioManager` (`hard_currency_gained` / `soft_currency_gained`), максимум `ViewsMaxAmount = 6` видимых партиклов со `SpawnDelay` между ними. |
| `AnimateExperience` | `ArgAnimateExperience` | Полёт "опыта" в таргет опыта (`AnimationType.Exp` — прямое перемещение, без кривой). До `ExpParticleCap = 5` партиклов с шагом `DelayStep = 0.15f`. Блокирует скрытие таргета на время анимации (`SetHideAnimationLock`), потом сам инициирует `AnimationHide`. |
| `AnimateReceipt` | `ArgAnimateReceipt` | Анимация "списания ингредиентов" рецепта крафта (`RecipeConfig.GetRecipeInfos()`): для каждого игредиента — партикл с текстом `-N`, масштаб от `StartScale` до 0 и движение вверх на фиксированный оффсет, с шагом `0.5f` между ингредиентами. |

Все стратегии одинаково завершают тween через `OnComplete`: возвращают партикл в пул
(`_particlesPool.Return(...)`), вызывают колбэк из arg-объекта и (где применимо)
`destination.OnTargetIn()` + `destination.AnimationIn().Play()` — то есть "приземление"
частицы триггерит анимацию на самом таргете (бар подпрыгивает/мигает).

### `Arg*` классы (`Animations/ArgAnimateXxx.cs`)

Простые POCO/DTO с параметрами конкретного вызова: стартовая позиция, ресурс, количество,
колбэки (`Action`), `SortingOrderGroup`. `ArgAnimateResources` реализован как fluent-builder
(`With...` методы возвращают `this`), остальные — обычные конструкторы.

## 6. Частицы-траектории (`ParticleAnimation/*`)

### `ParticleAnimationBase` (абстракция)

```csharp
public abstract class ParticleAnimationBase
{
    public abstract Tween AnimateCurvedParticle(CurvedParticleAnimationInfo curvedParticleAnimationInfo);
}
```

`CurvedParticleAnimationInfo` — единый DTO для передачи партикла, `AnimationInfo`, конечной
позиции, параметров (`ParticleAnimationParameters`), интервала, флагов (`UseAngledCurve`,
`ControlScreenBorder`).

### `ParticleCurvedAnimation` (`AnimationType.Bezier`)

Строит случайную кривую Безье через `BezierCurve.GenerateRandomCurveWithBackOffset` /
`GenerateRandomCurveWithAngledOffset` (утилита `UGI.MD.Curves`), двигает партикл по кривой
(`DOBezierCurveMove`), опционально проверяет, что кривая не выходит за границы экрана
(`CheckBorderScreen`, семплирование с шагом `1/64`), делает up-scale/down-scale в заданные
моменты времени.

### `ParticleExpAnimation` (`AnimationType.Exp`)

Простое движение по прямой (`DOMove`) со случайным смещением старта по кругу
(`MathUtil.GetRandomPointOnCircle`, радиус 50), без кривой Безье — используется для опыта.

## 7. Таргеты анимации (`Targets/*`) — куда прилетает частица

### Базовый класс `AnimationTarget` (MonoBehaviour)

```csharp
public abstract class AnimationTarget : MonoBehaviour
{
    protected virtual void Awake()  => AnimationTargets.RegisterAnimationTarget(this);
    protected virtual void OnDestroy() => AnimationTargets.ReleaseAnimationTarget(this);

    public virtual Vector3 Position => transform.position;
    public virtual Tween AnimationShow(AnimationInfo animationInfo, bool isForced = false) => null;
    public virtual Tween AnimationHide(bool isForced = false) => null;
    public virtual Tween AnimationIn() => null;   // "приземление" — бар подпрыгнул/мигнул
    public virtual void OnTargetIn() {}
    public void SetHideAnimationLock(bool isLocked) { ... }
}
```

Каждый конкретный таргет — MonoBehaviour, размещённый в сцене/префабе HUD (иконка валюты,
панель ресурсов, кнопка инвентаря и т.п.). При `Awake`/`OnDestroy` таргет сам
регистрируется/снимается с регистрации в статическом реестре `AnimationTargets` — типичный
паттерн self-registering component, никакого ручного связывания в коде не требуется.

### Статический реестр `AnimationTargets` (не MonoBehaviour, чисто статический класс)

```csharp
public static class AnimationTargets
{
    private static Dictionary<OffscreenTargetType, List<OffScreenAnimationTarget>> _offscreenTargets;
    private static Dictionary<ResourcePanelItemType, List<ResourceAnimationTarget>> _resourceRewardTargets;

    public static void RegisterAnimationTarget(AnimationTarget t) { ... }
    public static void ReleaseAnimationTarget(AnimationTarget t) { ... }
    public static AnimationTarget GetTargetSafe(AnimationInfo animationInfo, AnimationTarget fallbackTarget = null) { ... }
}
```

Это разновидность **service locator / глобального реестра**, живущего вне DI-контейнера
(статические поля класса, инициализируются в статическом конструкторе). Регистрируются два
вида таргетов:
- `ResourceAnimationTarget` — по ключу `ResourcePanelItemType` (иконки в общей панели ресурсов);
- `OffScreenAnimationTarget` — по ключу `OffscreenTargetType` (лагерь, трейлер, воздушный шар,
  окно инвентаря, зоопарк-менеджер, ZooUI-мердж-айтем, кнопка путешествия).

`GetTargetSafe(info, fallbackTarget)` — центральная точка поиска места назначения:
1. Если у `AnimationInfo` явно задан `OffscreenTarget != Undefined` — ищем в `_offscreenTargets`.
2. Иначе по `info.Resource` определяем "конвертированный" ключ:
   - `ResourceType` конвертится через `ResourcePanelHelper.GetBarByResourcesType` → ищем в
     `_resourceRewardTargets`;
   - `ActorStaticType` (валютные актёры типа `SoftCurrency_01..08`, `HardCurrency_01..06`)
     сначала мапятся в `ResourceType.SoftCurrency`/`HardCurrency` через
     `ActorKeyToAnimationTargetKey`, а любой другой `ActorStaticType`/`ActorDynamicType`
     уходит на `OffscreenTargetType.Camp`.
3. Если таргетов для ключа несколько (несколько активных панелей — например, разные
   вкладки/окна), берётся **последний активный в иерархии** (`LastOrDefault(x => x.gameObject.activeInHierarchy)`),
   иначе — первый зарегистрированный, иначе — `fallbackTarget`.

### Конкретные реализации таргетов

| Класс | База | Особенности |
|---|---|---|
| `FallbackTarget` | `AnimationTarget` | Пустая заглушка — просто "точка на экране", используется, когда ничего конкретного не найдено (`CanvasAnimationBuilder.DefaultFallbackTarget`). |
| `ResourceAnimationTarget` | `AnimationTarget` | Базовая "иконка ресурса в баре": show/hide через `DOAnchorPosY` (выезд/заезд панели), поддержка блокировки (`SetPanelLock`), кэширует позицию только пока она внутри `ScreenHelper.SafeArea` (защита от прыжков позиции при show/hide анимации самого бара). |
| `CurrencyAnimationTarget` | `ResourceAnimationTarget` | "Приземление" валюты: bounce иконки/текста по кривым из `BarsAnimationSettings`, VFX (`_vfx`, `_vfxFront`, `_textVfx`), "flare"-блик через сдвиг `_FlareTilingAndOffset` в шейдере. Троттлинг повторного bounce через `AnimationReplayDelay`. |
| `ExperienceAnimationTarget` | `ResourceAnimationTarget` | Аналогично, плюс проигрывание VFX через `VFXPool.SpawnVFX(VFXType.FX_UI_UpExperiance, ...)`. |
| `OffScreenAnimationTarget` | `AnimationTarget` | Таргет "за пределами экрана" (лагерь, трейлер и т.п.), использует `LineAnimation` (пара точек start/end с гизмо в редакторе) для show/hide-перелёта. |
| `InventoryAnimationTarget` | `OffScreenAnimationTarget` | Инвентарная кнопка со счётчиком (`CounterWithSlidePanel`), достаёт актуальное количество из `InventoryModel` через `PlayerProfile`. |
| `ResourceAnimationTargetWithSlidePanel` | `ResourceAnimationTarget` | Ресурсный таргет с выезжающей панелью-счётчиком (`CounterWithSlidePanel`), количество берёт из `ConsumptionController` или кастомного делегата (`SetCustomResourceGetter`). |
| `TileAnimationTarget` | `AnimationTarget` | Пустой класс-маркер (заготовка/расширение под будущий тип таргета — тайл на карте). |

### `CounterWithSlidePanel`

Не наследник `AnimationTarget`, а вспомогательный компонент, которым управляют
`InventoryAnimationTarget`/`ResourceAnimationTargetWithSlidePanel`. Копит очередь
`AnimationInfo` (`Queue<AnimationInfo>`), на `OnTargetIn()` обновляет иконку/число
(`ActorUI.WithText`), показывает/прячет фон-панель по таймеру (`_hideCooldown`), скрывает всё
принудительно в `OnDisable` (`DOTween.Kill`, чтобы не оставлять зависших твинов на
переиспользуемом объекте).

## 8. Партиклы-вьюхи (`View/*`)

- **`ActorParticleUi`** — базовый визуальный партикл: `Image` + `TextMeshProUGUI` + `Canvas`
  (для `SetSortingOrder`). `SetActorImage(AnimationInfo)` подбирает спрайт через
  `IconsLibrary`/`ActorsStorage` в зависимости от того, `ResourceType` это или
  `ActorStaticType`/`ActorDynamicType`. Внедряется через `[Inject]` (`ResourcesScopeDelegate`,
  `ActorsStorage`, `IconsLibrary`), т.к. лежит в пуле и не проходит обычный DI-инстанс
  из сцены — VContainer всё равно инжектит зависимости в пуловые префабы при их первом
  инстанцировании через `GameObjectStackPool`.
- **`AnimatedResource`** — наследник `ActorParticleUi` с дополнительным `_fx` (GameObject)
  и `RectTransform` — используется там, где нужен доп. эффект поверх партикла ресурса.

## 9. Прочая инфраструктура рядом (не часть ядра, но используется в тех же сценариях)

- **`LineAnimation`** (`Animations/LineAnimation.cs`) — сериализуемая пара точек
  start/end + `DoMoveToStart/DoMoveToEnd`, используется `OffScreenAnimationTarget` для
  описания "пути" в закадровую точку.
- **`ResourceTargetBounceAnimation`** (`Animations/ResourceTargetBounceAnimation.cs`,
  namespace `Game.Client.UI.AnimationBuilder`, отдельный от `RewardAnimation`!) — простой
  bounce-компонент (`DOBounce()`), массив `_bouncers` есть в `ResourceAnimationTarget`, но в
  прочитанном коде `ResourceAnimationTarget` не вызывает `DOBounce()` напрямую — вероятно,
  используется внешним кодом/конкретными сценами.
  Обратите внимание: у этого файла **другой namespace**, отличный от остальной папки — исторический
  нюанс организации кода.
- **`TransportAnimationsUI` / `TransportAnimationsActor`** (`TransportAnimations/*.cs`,
  namespace `UGI.MD.UI`) — самостоятельные `ScriptableObject` с наборами DOTween-анимаций
  для "транспорта" (появление, полёт, коллект дропа, взрыв, скрытие, перелёт к кнопке
  путешествия). **Не завязаны на `AnimationBuilder`/`AnimationTarget`** — отдельная,
  параллельная система анимаций для другого игрового объекта (транспорт), просто лежит
  физически рядом в той же папке.

## 10. Как это используется снаружи (типовой flow)

1. Система/контроллер получает `AnimationBuilder` через `[Inject]` (обычный VContainer DI,
   т.к. `AnimationBuilder` зарегистрирован как singleton entry point).
2. Собирает `ArgAnimateXxx` с нужными параметрами (позиция старта, ресурс, количество, колбэк).
3. Вызывает `animationBuilder.Animate<AnimateXxx>(arg)`, получает `Tween`, обычно оборачивает
   в собственную секвенцию (`animationBuilder.NewAnimationSequence().Append(...)`) и `.Play()`.

Пример (`ActorResourceSystem.OnConfirm`, упрощённо):

```csharp
var builder = _animationBuilder;
var animation = builder.NewAnimationSequence();
var position = _cameraBehaviour.Camera.WorldToScreenPoint(_target.Transform.WorldPosition);
animation.Append(builder.Animate<AnimateCurrency>(
    new ArgAnimateCurrency(position, _resourceType, _resourceCount, SortingOrderGroup.UIBack)));
animation.Play();
```

Пример (`RewardDropControllerBase`, упрощённо):

```csharp
var animateArg = new ArgAnimateCurrency(
    View.GetRewardDropItem(reward).transform.position,
    resourceType, reward.Count, SortingOrderGroup.UIBack);
_animationBuilder.Animate<AnimateCurrency>(animateArg);
```

Целевая точка (бар валюты, панель ресурсов и т.д.) при этом никак явно не передаётся —
она находится автоматически через `AnimationTargets.GetTargetSafe` внутри стратегии, на
основе `ResourceType`/`ActorStaticType` и текущей активной сцены HUD.

Найдено также использование `ArgAnimateResources`-стратегии (fluent-конфигурация) в других
местах UI (страницы наград, попапы), а также прямых потребителей `AnimationBuilder` в:
`GrandRewardPage`, `DiscoveryRewardController`, `ActorProductionBuildingSystem`,
`ContractShopCard`, `PiggyBankActiveState`, `MergeRushActiveState`, `EventCompletePageController`,
`ActorInventoryCollectableSystem`, `TransportWidgetController`, `AccountController`,
`MissedResourcesPopupController`, `CheatsResourcesModule` и др. — то есть сервис используется
практически во всех местах игры, где нужно "показать награду/ресурс, летящий в HUD".

## 11. Краткая карта файлов

```
Assets/Scripts/Game/Client/UI/AnimationBuilder/
├── AnimationBuilder.cs                     — ядро: реестр стратегий, фабрика Sequence, AnimationPool
├── Settings/
│   ├── AnimationBuilderSettings.cs         — ScriptableObject-конфиг (ссылки на префабы/настройки)
│   └── CanvasAnimationBuilder.cs           — MonoBehaviour-держатель Canvas + DefaultFallbackTarget
├── Animations/
│   ├── BaseUIAnimate.cs                    — абстрактная стратегия анимации
│   ├── BaseUIAnimateArg.cs                 — базовый DTO аргументов
│   ├── AnimationInfo.cs                    — DTO с параметрами конкретного ресурса/партикла
│   ├── AnimateFlyingActor.cs / ArgAnimateFlyingActor.cs
│   ├── AnimateResources.cs / ArgAnimateResources.cs
│   ├── AnimateCurrency.cs (+ArgAnimateCurrency внутри)
│   ├── AnimateExperience.cs / ArgAnimateExperience.cs
│   ├── AnimateReceipt.cs / ArgAnimateReceipt.cs
│   ├── LineAnimation.cs                    — линия start→end для offscreen-таргетов
│   └── ResourceTargetBounceAnimation.cs    — bounce-компонент (др. namespace)
├── ParticleAnimation/
│   ├── ParticleAnimationBase.cs            — абстракция траектории партикла
│   ├── ParticleCurvedAnimation.cs (+CurvedParticleAnimationInfo) — Bezier-траектория
│   ├── ParticleExpAnimation.cs             — прямая траектория (для опыта)
│   ├── ParticleCurvedAnimationSettings.cs  — ScriptableObject с параметрами по ResourceType
│   └── BarsAnimationSettings.cs            — ScriptableObject с параметрами анимации баров
├── Targets/
│   ├── AnimationTarget.cs                  — базовый self-registering MonoBehaviour-таргет
│   ├── AnimationTargets.cs                 — статический реестр + поиск таргета
│   ├── ResourceAnimationTargets.cs         — вспомогательный класс поиска (обёртка над словарём)
│   ├── FallbackTarget.cs                   — таргет-заглушка
│   ├── ResourceAnimationTarget.cs          — базовый таргет ресурсной панели
│   ├── CurrencyAnimationTarget.cs          — таргет валюты (bounce/VFX/flare)
│   ├── ExperienceAnimationTarget.cs        — таргет опыта (VFX через VFXPool)
│   ├── ResourceAnimationTargetWithSlidePanel.cs
│   ├── CounterWithSlidePanel.cs            — компонент выезжающей панели-счётчика
│   ├── OffScreenAnimationTarget.cs         — таргет "за экраном" (через LineAnimation)
│   ├── OffscreenTargetType.cs              — enum типов offscreen-таргетов
│   ├── InventoryAnimationTarget.cs         — offscreen-таргет инвентаря
│   └── TileAnimationTarget.cs              — пустой класс-заготовка
├── View/
│   ├── ActorParticleUi.cs                  — базовый визуальный партикл (Image+Text+Canvas)
│   └── AnimatedResource.cs                 — партикл с доп. FX
└── TransportAnimations/                    — параллельная, не связанная с ядром система
    ├── TransportAnimationsUI.cs
    └── TransportAnimationsActor.cs
```

## 12. Наблюдения / потенциальные особенности для дальнейшей работы

- `AnimationTargets` — статический класс с состоянием, живущим вне DI и вне жизненного цикла
  сцены явно (кроме `Register`/`Release` в `Awake`/`OnDestroy` самих таргетов). При смене сцен
  важно, чтобы все таргеты корректно вызывали `OnDestroy`, иначе в реестре останутся мёртвые
  ссылки (частично защищено проверкой `!animationTarget` и `activeInHierarchy` при поиске).
- Два независимых уровня диспетчеризации (`AnimateXxx` по типу и `AnimationType` Bezier/Exp)
  можно спутать — они решают разные задачи (что анимировать vs как двигать частицу).
- `TileAnimationTarget` — пустой класс, вероятно задел на будущее.
- `ResourceTargetBounceAnimation` находится в другом namespace (`Game.Client.UI.AnimationBuilder`
  вместо `UGI.MD.UI.RewardAnimation`) — исторический артефакт, не критично, но стоит иметь в виду
  при рефакторинге пространств имён.
- `TransportAnimations*` физически лежат в той же папке, но архитектурно не относятся к
  `AnimationBuilder` — отдельный набор ScriptableObject-анимаций для транспорта.
