# Save System Migration — из Research в MyBookstore

Инструкция-чек-лист по переносу системы сохранений из Research-проекта в MyBookstore.

**Контекст:** в MyBookstore делаем MVP системы сохранений с .NET бэкендом. Архитектура близка к Research, поэтому берём оттуда ~95% кода. Документ-источник анализа — `docs/RESEARCH_SAVE_SYSTEM.md` (читать перед переносом).

Документ читается в проекте Research при выполнении переноса. Файлы в Research нужно найти, при необходимости адаптировать и положить в указанные целевые пути MyBookstore.

---

## Целевая структура в MyBookstore

Всё кладём под `Assets/Game/Features/Save/` (внутри `Save.asmdef`).

```
Assets/Game/Features/Save/
├── ISaveService.cs                          (новый — Фаза 0)
├── SaveService.cs                           (из Research + фиксы)
├── SaveMode.cs                              (новый enum)
├── Model/
│   ├── SaveData.cs                          (новый — модульная модель)
│   ├── MetaData.cs                          (новый)
│   └── ModulePayload.cs                     (новый)
├── Storage/
│   ├── ISaveStorage.cs                      (новый)
│   ├── LocalDiskStorage.cs                  (из Research, ~as-is)
│   ├── HttpSaveStorage.cs                   (переписать, не копировать)
│   └── SaveGlobalPayloadParser.cs           (из Research, as-is)
├── Identity/
│   ├── IPlayerIdentityProvider.cs           (новый, на основе Research)
│   └── PersistentInstallPlayerIdentityProvider.cs   (из Research)
├── Config/
│   └── ISaveBackendConfig.cs                (новый)
└── Hooks/
    └── ISaveHook.cs                         (новый — async hooks)
```

---

## Список файлов к переносу

### 1. Копировать как есть (с минимальной адаптацией namespace/usings)

| Файл в Research | Целевой путь в MyBookstore | Примечания |
|---|---|---|
| `LocalDiskStorage.cs` | `Save/Storage/LocalDiskStorage.cs` | Привести к интерфейсу `ISaveStorage` (см. ниже). Логика atomic write + `.bak` — без изменений |
| `SaveGlobalPayloadParser.cs` | `Save/Storage/SaveGlobalPayloadParser.cs` | Без изменений |
| `PersistentInstallPlayerIdentityProvider.cs` | `Save/Identity/PersistentInstallPlayerIdentityProvider.cs` | Без изменений |

### 2. Копировать с обязательными фиксами

| Файл в Research | Целевой путь | Что фиксим |
|---|---|---|
| `SaveService.cs` | `Save/SaveService.cs` | Фиксы 1–4 (см. ниже) |
| `HttpSaveStorage.cs` | `Save/Storage/HttpSaveStorage.cs` | **Переписать с нуля** — фиксы 5–6 |

### 3. НЕ переносить

| Файл в Research | Почему не нужен |
|---|---|
| `GameSaveData.cs` | God Object — заменяем на `Dictionary<string, ModulePayload>` в новом `SaveData.cs`. Подробнее — раздел "Адаптация модели данных" |
| `SaveMigrationService.cs` | В MyBookstore нет legacy-данных, мигрировать нечего. Если в будущем понадобится — берём паттерн, а не код |
| Тесты Research для Save | Под старую архитектуру — перепишем под новую |
| Любые подмодели типа `ResourcesModuleSaveData`, `InventoryModuleSaveData` | Это структуры God Object. У каждой фичи MyBookstore будет своя модель в своей сборке. В Save они приходят как opaque JSON |

---

## Фиксы при переносе

### Фикс 1: Rate limit не должен удерживать семафор

В `SaveService.SaveAllAsync` Research-вариант делает Delay внутри `try` после `WaitAsync` — это блокирует параллельные `UpdateModuleAsync` до 500мс.

**Было (Research):**
```csharp
await _semaphore.WaitAsync(cancellationToken);
try
{
    if (elapsed < SaveRateLimit)
    {
        await UniTask.Delay(delay, ...); // ← семафор держится
    }
    // save logic
}
finally { _semaphore.Release(); }
```

**Стало:**
```csharp
if (elapsed < SaveRateLimit)
{
    await UniTask.Delay(delay, ...); // ДО семафора
}
await _semaphore.WaitAsync(cancellationToken);
try
{
    // save logic
}
finally { _semaphore.Release(); }
```

### Фикс 2: God Object → Dictionary модулей

В Research `SaveService` хранит `_data: GameSaveData` с типизированными подмоделями. В MyBookstore — `Dictionary<string, ModulePayload>` (каждый модуль opaque).

**Было:**
```csharp
private GameSaveData _data;
public async UniTask<T> GetReadonlyModuleAsync<T>(Func<GameSaveData, T> selector) { ... }
public async UniTask UpdateModuleAsync(Action<GameSaveData> updater) { ... }
```

**Стало:**
```csharp
private SaveData _data; // { MetaData, Dictionary<string, ModulePayload> Modules }

public async UniTask<T?> GetModuleAsync<T>(string moduleKey)
{
    if (!_data.Modules.TryGetValue(moduleKey, out var payload)) return default;
    return JsonConvert.DeserializeObject<T>(payload.Json);
}

public async UniTask UpdateModuleAsync<T>(string moduleKey, T newValue, int schemaVersion)
{
    var json = JsonConvert.SerializeObject(newValue);
    _data.Modules[moduleKey] = new ModulePayload { Version = schemaVersion, Json = json };
    ScheduleDebouncedSave();
}
```

Никакого `EnsureDefaults()` и `ValidateData()` в Save — каждая фича сама валидирует свой JSON при чтении.

### Фикс 3: `event Action` → async hooks

**Было:**
```csharp
public event Action OnBeforeSave;
public event Action OnAfterLoad;
```

**Стало:**
```csharp
public interface ISaveHook
{
    UniTask BeforeSaveAsync(CancellationToken ct);
    UniTask AfterLoadAsync(CancellationToken ct);
}

private readonly List<ISaveHook> _hooks = new();
public void RegisterHook(ISaveHook hook) => _hooks.Add(hook);

// внутри SaveAllAsync:
foreach (var hook in _hooks)
    await hook.BeforeSaveAsync(ct);
```

### Фикс 4: IDisposable — регистрация в Bootstrap

Сам класс `IDisposable` реализуем, но регистрация делается уже в MyBookstore Bootstrap:

```csharp
builder.Register<SaveService>(Lifetime.Singleton)
       .As<ISaveService>()
       .AsSelf();
// важно: VContainer вызовет Dispose() автоматически только если зарегистрирован как тип, реализующий IDisposable —
// либо через .AsImplementedInterfaces(), либо .As<IDisposable>()
builder.RegisterEntryPoint<SaveServiceLifecycle>(); // или явная регистрация IDisposable
```

В коде `SaveService` — никаких изменений в `Dispose()` против Research.

### Фикс 5: `HttpSaveStorage.Exists()` всегда `true` — переписать

Research:
```csharp
public bool Exists() => true; // баг
```

Это ломает legacy-миграцию и логику "новый пользователь". В MVP `Exists()` нам по сути не нужен — в новой архитектуре решение "новый/старый" принимаем по результату `LoadAsync` (404/null → новый).

Рекомендация: убрать `Exists()` из `ISaveStorage`, оставить только `LoadAsync` который возвращает `null` если данных нет.

### Фикс 6: `HttpSaveStorage` — write-through кэш + URL через конфиг

Research:
```csharp
_fullUrl = ApiConfig.BaseUrl + ApiConfig.SaveGlobalPath; // hardcode
// SaveAsync падает → данные потеряны
```

Стало (упрощённо):
```csharp
public HttpSaveStorage(
    ISaveBackendConfig config,
    ISaveStorage localCache,        // обёртка над LocalDiskStorage
    IPlayerIdentityProvider identity)
{
    _config = config;
    _localCache = localCache;
    _identity = identity;
}

public async UniTask SaveAsync(string json, CancellationToken ct)
{
    // 1. сначала локально (атомарно, гарантия)
    await _localCache.SaveAsync(json, ct);

    // 2. потом push на сервер с retry
    try
    {
        await PushToServerAsync(json, ct);
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[Save] HTTP push failed, will retry on next save: {ex.Message}");
        // не пробрасываем — локально сохранено, прогресс не потерян
    }
}

public async UniTask<string?> LoadAsync(CancellationToken ct)
{
    // приоритет — сервер (свежее), при ошибке — локальный кэш
    try
    {
        var fromServer = await PullFromServerAsync(ct);
        if (fromServer != null)
        {
            await _localCache.SaveAsync(fromServer, ct); // обновить кэш
            return fromServer;
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[Save] HTTP load failed, falling back to local: {ex.Message}");
    }
    return await _localCache.LoadAsync(ct);
}
```

URL берётся из инжектированного `ISaveBackendConfig`:
```csharp
public interface ISaveBackendConfig
{
    string BaseUrl { get; }
    string SavePath { get; }
    int RequestTimeoutMs { get; }
    int RetryCount { get; }
}
```

---

## Адаптация модели данных

В Research `GameSaveData` — большой типизированный класс. В MyBookstore используем opaque модули.

### Новый `SaveData.cs`

```csharp
public sealed class SaveData
{
    public MetaData Meta { get; set; } = new();
    public Dictionary<string, ModulePayload> Modules { get; set; } = new();
}
```

### Новый `MetaData.cs`

Берём из Research — структура близкая:
```csharp
public sealed class MetaData
{
    public int SchemaVersion { get; set; } = 1; // версия корневой обёртки
    public long Revision { get; set; }          // монотонный счётчик
    public string Hash { get; set; } = "";      // SHA256 от Modules
    public long TimestampUtcMs { get; set; }
}
```

### Новый `ModulePayload.cs`

```csharp
public sealed class ModulePayload
{
    public int Version { get; set; }    // версия схемы конкретного модуля
    public string Json { get; set; } = "";
}
```

### Логика хеша

Из Research берём `ComputeHash` (SHA256), но считаем его от сериализованного `Modules` словаря, не от `GameSaveData`. В коде Research найти метод, который делает `JsonConvert.SerializeObject + SHA256 + Convert.ToBase64String` — копировать его, поменять source на `_data.Modules`.

---

## Namespace и зависимости

### Namespaces

- Все типы в `Save/` — namespace `Save` или `Save.Storage`, `Save.Identity` и т.д.
- Заменить все Research-namespaces на эти при копировании.

### Зависимости (UPM-пакеты)

Сверить версии перед переносом:

| Пакет | Использование в Save |
|---|---|
| `com.cysharp.unitask` | UniTask, async/await |
| `com.unity.nuget.newtonsoft-json` | JsonConvert |
| `jp.hadashikick.vcontainer` | DI-регистрация (только в Bootstrap, не в Save) |

Если в MyBookstore отсутствует `com.unity.nuget.newtonsoft-json` — добавить ДО переноса. Альтернатив (System.Text.Json) на старте MVP не рассматриваем.

### asmdef references

После переноса в `Assets/Game/Features/Save/Save.asmdef` нужно добавить:
```json
"references": [
    "com.cysharp.unitask",
    "Infrastructure"   // если используем Http через Infrastructure
],
"precompiledReferences": [
    "Newtonsoft.Json.dll"
],
"overrideReferences": true
```

---

## Чек-лист на КАЖДЫЙ копируемый файл

- [ ] Namespace заменён
- [ ] Все `using` пересмотрены, лишние удалены
- [ ] Нет ссылок на классы Research, которых нет в списке переноса
- [ ] Нет `UnityEditor.*` (если файл не в Editor-сборке)
- [ ] Применены фиксы из таблиц выше (если файл входит в список)
- [ ] Файл лежит в правильной подпапке `Save/`
- [ ] Сборка проекта зелёная после добавления файла

---

## Порядок переноса (рекомендованный)

1. Создать новые контракты: `ISaveService`, `ISaveStorage`, `ISaveBackendConfig`, `IPlayerIdentityProvider`, `ISaveHook`, `SaveMode`, `SaveData`, `MetaData`, `ModulePayload` — это всё пишется в MyBookstore, в Research не смотрим.
2. Перенести `LocalDiskStorage.cs` — адаптировать под `ISaveStorage`.
3. Перенести `PersistentInstallPlayerIdentityProvider.cs`.
4. Перенести `SaveGlobalPayloadParser.cs`.
5. Перенести `SaveService.cs` — применить **Фиксы 1–4**.
6. Написать `HttpSaveStorage.cs` с нуля — **Фиксы 5–6**.
7. Создать `SaveVContainerBindings` (extension на `IContainerBuilder`) — регистрация всех типов.
8. Smoke-test: один модуль end-to-end (Resources или любой простой).

Каждый шаг — отдельный коммит. Не объединять.

---

## После переноса

Прочитать `docs/SAVE_PATTERNS_FROM_PROD.md` — там дополнительные паттерны из большого прод-проекта, которые надо применить ПОВЕРХ перенесённого кода (без копирования файлов).
