# Pet-gap: что есть в Research, чего нет в MyBookstore

Анализ проведён: 2026-06-09. Источник: `C:\Projects\Research\Assets\Game\Core\`.

**Главный вывод:** Research уже содержит полноценную оркестрацию загрузки (Phase → Group → Operation, retry, timeout, parallel/sequential, агрегация прогресса). Это закрывает бóльшую часть того, что планировалось тащить из Pet#2.

---

## 1. Подсистема Loading (приоритет: СЕЙЧАС)

Папка: `C:\Projects\Research\Assets\Game\Core\Bootstrap\Loading\`

### Ядро оркестрации (копировать как есть)

| Файл | Назначение | Совместимость |
|---|---|---|
| `LoadingOrchestrator.cs` | Запуск фаз, retry, timeout, прогресс, события | UniTask — есть в MyBookstore ✓ |
| `LoadingProgressAggregator.cs` | Сглаженный прогресс по операциям с весами | Изолирован ✓ |
| `LoadingPhase.cs` | Фаза = набор групп | Изолирован ✓ |
| `LoadingGroup.cs` | Группа = набор операций | Изолирован ✓ |
| `LoadingGroupExecutionMode.cs` | enum Sequential/Parallel | Изолирован ✓ |
| `ILoadingOperation.cs` | Интерфейс операции | Изолирован ✓ |
| `LoadingOperationBase.cs` | База для операций | Изолирован ✓ |
| `LoadingOperationStatus.cs` | enum статусов | Изолирован ✓ |
| `LoadingFailure.cs` | Описание падения операции | Изолирован ✓ |
| `LoadingRetryPolicy.cs` | Политика повторов | Изолирован ✓ |
| `LoadingRunResult.cs` | Результат RunAsync | Изолирован ✓ |

**Объём:** ~11 файлов, ~1000 строк. Все в одном namespace `Game.Bootstrap.Loading`. Внешних зависимостей нет кроме UniTask и UnityEngine.Debug.

### Операции (Operations/)

| Операция | Зависит от | Действие при переносе |
|---|---|---|
| `AddressablesUpdateOperation` | `Infrastructure.AddressablesUpdater` (в Research) | **Адаптировать** → у нас `IAddressablesCatalogService.InitializeAndUpdateAsync()` |
| `FirebaseDependenciesOperation` | `RemoteConfigLoader` (есть в обоих) | **Сверить API** — у нас тоже есть `RemoteConfigLoader.cs` |
| `RemoteConfigFetchOperation` | `RemoteConfigLoader` | **Сверить** — наш `FirebaseRemoteConfigService` под другим интерфейсом |
| `SaveDataLoadOperation` | `SaveService` | **Сверить** — у нас `ISaveService.EnsureLoadedAsync()` |
| `WarmupOperation` | (probably none / shaders) | **Скопировать как есть** (после ревью) |
| `UiManagerConfigureOperation` | `UIManager`, `WindowFactoryDI` | **Отложить** — UIManager в Phase 2 (по твоему решению) |
| `AuthorizationGateOperation` | `IAuthorizationGate` | **Отложить** — нет auth-подсистемы в MyBookstore |
| `SceneTransitionOperation` | `TransitionAnimationService` | **Отложить** — депенденси на UI |

### Связанные (auth — отложить, но скопировать заглушки если нужно)

- `IAuthorizationService.cs`, `IAuthorizationGate.cs`, `LoadingAuthorizationGate.cs`, `AuthorizationLoginMethod.cs`, `MockAuthorizationService.cs` — **не тащить сейчас**, пока нет реальной авторизации.

### UI часть

- `C:\Projects\Research\Assets\Game\UI\UIShared\Scripts\Loading\LoadingScreenView.cs` — **тащить вместе с орхестратором**, иначе нечем показывать прогресс. Префаб понадобится — найти отдельно.

### Тесты

- `LoadingOrchestratorTests.cs` — **скопировать**, это бесплатная защита от регрессии при адаптации.

---

## 2. Точка входа — `Bootstrap.cs`

`C:\Projects\Research\Assets\Game\Core\Bootstrap\Bootstrap.cs` — **не копировать как есть**, но использовать как референс.

Причина: Research-овский Bootstrap — MonoBehaviour с `[Inject]`, который вручную инжектит UIManager/WindowFactory/Auth. В MyBookstore такого набора зависимостей пока нет — будет ломать компиляцию.

**План:** написать свой минимальный entry point по образцу — он заменит три текущих `IAsyncStartable` (`AddressablesWarmupEntryPoint`, `ConfigsWarmupEntryPoint`, `BookDuneProbeEntryPoint`). См. шаг в обновлённом [migration-plan.md](migration-plan.md).

---

## 3. UI Manager / WindowFactory / TransitionAnimationService (приоритет: ПОЗЖЕ)

По твоему решению — в последующих итерациях. Файлы есть в Research (используются в `Bootstrap.cs`), но в pet-gap не разворачиваю. Вернёмся, когда дойдём до Phase 2 / UI.

---

## 4. Analytics (приоритет: ПОЗЖЕ)

Аналогично — не в этой итерации.

---

## 5. Чего в Research нет (или не нашёл)

- `LinearLoadingTime` / отдельных метрик времени фаз — нет такого класса. Орхестратор сам логирует `duration_ms` по каждой операции, этого достаточно на старте.
- `GameStateService` (enum состояний игры) — не искал, не приоритет.
- Debug-флаги Editor-старта (`SkipLoading`, `StartFromScene`) — у Research есть `_minimumLoadingSeconds` и `_globalLoadingTimeoutSeconds` в `Bootstrap.cs`, но именно debug-fast-start как у Pet#2 — нет.

Эти пункты остаются в Phase 2 (источник Pet#2).

---

## 6. Риски адаптации

1. **API расхождения по сервисам.** `AddressablesUpdater` (Research) vs `IAddressablesCatalogService` (MyBookstore), `RemoteConfigLoader` (разные подписи), `SaveService` (разные методы). Каждая операция-донор требует ~10-30 строк правки.
2. **Namespace.** Research использует `Game.Bootstrap.Loading`, `Infrastructure`, `UIShared.Loading`. У MyBookstore namespace другой (`Bookstore.*`?). Нужно решить: оставить Research-овские namespace или перебрасывать в свои. Рекомендация: оставить `Game.Bootstrap.Loading` для копируемых файлов — меньше правок.
3. **Asmdef.** В Research `Bootstrap.asmdef` ссылается на `UIShared`, `UISystem`, `Infrastructure`. У нас этих сборок нет. Решение: создать новую сборку `Game.Bootstrap.Loading` (или вложить в существующую `Core.Installers`), без UI-зависимостей.
4. **Префаб LoadingScreenView.** Кодовый класс легко перенести, префаб — нужно либо взять `.prefab` файл, либо собрать руками. Это блокер для визуальной верификации.

---

## 7. Рекомендуемая последовательность переноса (для нового Phase 1)

1. Создать сборку/папку `Assets/Game/Core/Bootstrap/Loading/` (зеркало Research).
2. Скопировать 11 файлов ядра + 8 файлов операций (с пометкой источника в комментарии).
3. Скопировать `LoadingOrchestratorTests.cs`.
4. Адаптировать операции под наши сервисы: Addressables, RemoteConfig, Save. Операции на UI/Auth — выкинуть или закомментировать.
5. Скопировать `LoadingScreenView.cs` + найти/собрать префаб.
6. Написать новый минимальный `BootstrapEntryPoint` (по образцу Research-овского `Bootstrap.cs`, но без UIManager/Auth).
7. Зарегистрировать `LoadingOrchestrator` и операции в `GameLoadingVContainerBindings` (заменяет TODO-стабы).
8. Удалить старые `AddressablesWarmupEntryPoint`, `ConfigsWarmupEntryPoint`, `BookDuneProbeEntryPoint` (или оставить как fallback на одну итерацию для отката).
9. Smoke-тест в Editor: запустить, увидеть прогресс-бар.

Это закрывает большую часть Phase 1 + шаги 3, 4, 6, 7, 8 из старого Phase 2 одним заходом.
