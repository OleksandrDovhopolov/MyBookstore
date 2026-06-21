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
1. **GameplayHubRoot**: обернуть визуал/UI хаба (фон, Morning/Preparation roots, камера хаба и т.п.) в один
   родительский GameObject **GameplayHubRoot**. НЕ включать в него: `GameplayLifetimeScope`, глобальные сервисы,
   DontDestroyOnLoad-канвас UIManager, глобальный `EventSystem`.
2. **HubRootBinder**: добавить компонент `HubRootBinder` на стабильный (не выключаемый) объект; поле `_hubRoot` = **GameplayHubRoot**.
3. **HubPhaseRouter**: добавить компонент `HubPhaseRouter`; назначить `_morningScreenRoot` и `_preparationScreenRoot`
   (те же корни, что в `GameplaySceneView`).
4. **GameInstaller**: у него больше нет полей customer-anchor (они уехали в `LocationInstaller`). Ничего назначать не нужно;
   `HubRootBinder` и `HubPhaseRouter` он подхватит автоматически (RegisterComponentInHierarchy).
5. Старое поле `PreparationScreenView._salesScreenRoot` больше не нужно в обычном цикле (fallback для debug-сцен);
   можно оставить пустым.

## 4. Камеры / Audio / EventSystem при additive
- В каждый момент времени активна **одна** камера и **один** `AudioListener`. Варианты:
  - камера хаба под `GameplayHubRoot` (гаснет вместе с ним при входе в локацию) + своя камера в `LocationScene`; **или**
  - одна общая камера в хабе, локация без камеры (тогда не класть камеру в LocationScene).
- **EventSystem** — ровно один (глобальный, в boot/DDOL или в хабе вне `GameplayHubRoot`). В `LocationScene` EventSystem НЕ добавлять.

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
