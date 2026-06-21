# LocationScene + GameFlow — editor setup (handoff)

Чеклист ручной настройки в Unity Editor для функционала из [GameFlowLoop.md](../GameFlowLoop.md).
Код (сервисы, инсталлеры, роутеры) уже написан — ниже только то, что делается руками в редакторе.

## 1. GameFlowSettings asset
1. `Assets → Create → Game → GameFlow Settings` → положить, напр., в `Assets/Game/Core/Installers/`.
2. Поля: `GameplaySceneName = GameplayScene`, `LocationSceneName = LocationScene` (дефолты уже такие).
3. Выделить `BootstrapInstaller.asset` → назначить его в поле **Game Flow → GameFlowSettings**.
   (Если не назначить — GameFlow возьмёт code-defaults с warn-логом, цикл всё равно работает.)

## 2. LocationScene (новая сцена)
1. Создать `Assets/Scenes/LocationScene.unity`.
2. **Build Settings**: добавить `LocationScene` после `GameplayScene` (`File → Build Profiles`).
3. Содержимое сцены:
   - **LocationLifetimeScope** (GameObject + компонент `LocationLifetimeScope`).
     - В `_monoInstallers` добавить компонент **LocationInstaller** (на этом же или соседнем объекте).
     - Родитель скопа задаётся в рантайме (`GameFlowService` через `EnqueueParent(Global)`), вручную `parentReference` НЕ заполнять.
   - **LocationInstaller** — назначить сериализованные ссылки (перенести из старого `GameInstaller` в `GameplayScene`):
     `_customerVisualPrefab`, `_customerSpawnRoot`, `_customerEntryLeft/Right`, `_customerShopApproach`,
     `_customerLaneAnchors[]`, `_customerExitLeft/Right`, `_salesTuningConfig`.
   - **LocationRoot** — корневой объект контента локации (плейсхолдер фон + якоря покупателей). В перспективе — из Addressables.
   - **SalesScreenView** — перенести из `GameplayScene` (его находит `RegisterBookSell` через `RegisterComponentInHierarchy`).
   - **Камера** локации + (опц.) свет.

## 3. GameplayScene (хаб) — правки
1. **GameplayHubRoot**: обернуть визуал/UI хаба (фон, Morning root, камера хаба и т.п.) в один
   родительский GameObject **GameplayHubRoot**. НЕ включать в него: `GameplayLifetimeScope`, глобальные сервисы,
   DontDestroyOnLoad-канвас UIManager, глобальный `EventSystem`.
2. **HubRootBinder**: добавить компонент `HubRootBinder` на стабильный (не выключаемый) объект; поле `_hubRoot` = **GameplayHubRoot**.
3. **HubPhaseRouter**: добавить компонент `HubPhaseRouter`; назначить **только** `_morningScreenRoot`
   (тот же корень, что в `GameplaySceneView`). Подготовка теперь — окно `PreparationWindow`, роутер ею не управляет.
4. **GameInstaller**: у него больше нет полей customer-anchor (они уехали в `LocationInstaller`). Ничего назначать не нужно;
   `HubRootBinder` и `HubPhaseRouter` он подхватит автоматически (RegisterComponentInHierarchy).
5. **Удалить из `GameplayScene` старый объект `PreparationScreen`** (с `PreparationScreenView`) — Подготовка стала окном.
   `PreparationScreenView._salesScreenRoot` тоже удалён из кода.

## 3a. PreparationWindow (окно Подготовки)
Подготовка теперь — окно `WindowController` (как `ResultsWindow`):
1. Создать префаб с компонентами `PreparationWindowView` (+ обязательные для `WindowView`: `RectTransform`/`CanvasGroup`/`Canvas`).
2. Назначить во view: `_dayLabel`, `_locationLabel`, `_slotCountLabel`, `_validationLabel`, `_bookListContainer`,
   `_bookRowPrefab` (`PreparationBookRowView`), `_openShopButton`, `_randomBooksButton`. Перенести вёрстку из старого экрана.
3. Завести префаб в Addressables с адресом **`PreparationWindow`** (как в `[Window("PreparationWindow", WindowType.Page)]`).
   Открывается автоматически из `GameplaySceneController.StartGameAsync` через `UIManager.ShowAsync<PreparationWindow>()`.

## 4. Камеры / Audio / EventSystem при additive (рекомендация: одна общая камера)
В `GameplayScene` уже есть `Main Camera`, `Global Light 2D`, `EventSystem`. Рекомендуемый (самый простой)
вариант — оставить их **persistent**, ВНЕ `GameplayHubRoot`, и НЕ класть камеру/EventSystem/AudioListener в
`LocationScene`:
- одна `Main Camera` рендерит и хаб, и контент локации (2D overlay-канвасы не зависят от камеры; для
  Screen Space - Camera назначить эту же камеру);
- один `AudioListener` (на этой камере), один `EventSystem`, один `Global Light 2D` (URP 2D освещает и спрайты локации).
- Тогда при входе в локацию гаснет только визуал хаба (`GameplayHubRoot`), а камера/ввод/свет продолжают работать.

Альтернатива (если у локации своя камера): держать камеру хаба ВНУТРИ `GameplayHubRoot` и добавить камеру в
`LocationScene`. Минус — на 1 кадр при additive-загрузке могут оказаться активны 2 камеры/2 AudioListener
(варнинг). Если выбираете этот путь — снимите `AudioListener` с одной из камер. По умолчанию берите общую камеру.

## 5. Проверка (smoke-loop)
1. Play из `Bootstrap` → хаб (`GameplayScene`).
2. Start Day → Preparation → Confirm → грузится `LocationScene` (additive), `GameplayHubRoot` гаснет, день идёт.
3. Завершить день (или Sales-cheat «Complete day») → возврат в хаб, открывается `ResultsWindow`.
4. Next Day → Results закрывается, хаб показывает Morning нового дня (без перезагрузки сцены).
5. Лог: `LocationLifetimeScope.Parent == GlobalLifetimeScope` (можно временно вывести в `LocationLifetimeScope`/брейкпоинт).
6. Пройти цикл ≥2 раза: нет задвоения сцен/скопов, камер, EventSystem; нет «disposed resolver».

## Связано
- [GameFlowLoop.md](../GameFlowLoop.md) — обзор цикла и компонентов.
- [SCENE_ARCHITECTURE.md](SCENE_ARCHITECTURE.md) — частично superseded (см. баннер вверху).
