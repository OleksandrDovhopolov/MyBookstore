# Research Save System — Анализ

Анализ системы сохранений из проекта Research. Документ описывает архитектуру, сильные стороны и проблемы — как справочник при проектировании аналогичной системы для MyBookstore.

---

## Архитектура

```
SaveService
  ├── ISaveStorage
  │     ├── LocalDiskStorage   (атомарные файловые операции)
  │     └── HttpSaveStorage    (REST-бэкенд, playerId-авторизация)
  ├── SaveMigrationService     (legacy → unified формат)
  └── GameSaveData             (единый корневой объект)
        ├── MetaData           (версия схемы, hash, revision)
        ├── ResourcesModuleSaveData
        ├── InventoryModuleSaveData
        ├── CraftingModuleSaveData
        ├── FishingModuleSaveData
        ├── FortuneWheelModuleSaveData
        ├── List<CardCollectionModuleSaveData>
        ├── List<EventStateSaveData>
        └── Dictionary<string,string> CustomModulesJson
```

### Ключевые классы

| Файл | Назначение |
|---|---|
| `SaveService.cs` | Оркестратор: load/save/update, debounce, rate limit, semaphore |
| `ISaveStorage.cs` | Абстракция хранилища: SaveAsync, LoadAsync, Exists, Delete, GetLastModified |
| `LocalDiskStorage.cs` | Файловое хранилище с атомарными записями и .bak |
| `HttpSaveStorage.cs` | HTTP хранилище, REST API, playerId в query string |
| `SaveMigrationService.cs` | Миграция legacy-файлов в единый GameSaveData |
| `GameSaveData.cs` | Корневая модель и все sub-модели |
| `IPlayerIdentityProvider.cs` | Интерфейс получения playerId |
| `PersistentInstallPlayerIdentityProvider.cs` | UUID из PlayerPrefs, генерируется один раз при установке |
| `SaveGlobalPayloadParser.cs` | Нормализация HTTP-ответа (data-string / data-json / raw-json) |

---

## Что сделано хорошо

### Thread safety — реализован правильно

`SemaphoreSlim(1,1)` последовательно защищает все операции над `_data`. Паттерн `WaitAsync → try/finally Release` соблюдён везде без исключений.

### LocalDiskStorage — production-grade атомарность

Схема записи: `write .tmp → File.Replace(.tmp, target, .bak)`. Гарантирует, что частичная запись (прерывание питания, краш) никогда не оставит сломанный основной файл. Наличие `.bak` позволяет вручную восстановить данные.

### Debounce + Rate Limit

600 мс debounce и 500 мс минимальный интервал между сохранениями — правильное решение против "thrashing" при быстрых изменениях состояния (например, несколько UpdateModuleAsync подряд за один кадр).

### Graceful degradation

- Поломанный JSON → fallback на defaults без краша
- HTTP 404 → null → новый пользователь, создаётся default
- Hash-mismatch → `LogWarning`, данные не отбрасываются

### CloneDetached через JSON

`GetReadonlyModuleAsync` и `UpdateModuleAsync` изолируют внутренний `_data` от изменений снаружи через JSON-клонирование. Ни одна фича не держит прямую ссылку на внутренний объект.

### SHA256 + Revision

`ComputeHash(data)` вычисляется при сохранении и проверяется при загрузке. Позволяет обнаружить внешнее редактирование файла. `Revision` монотонно растёт — позволяет детектировать потерянные сохранения.

### Payload breakdown telemetry

При каждом сохранении логируется разбивка по модулям (байт) с предупреждением при превышении 30 KB. Практически незаменимо при диагностике разросшихся сохранений в production. Отдельный dump для CustomModulesJson и CardCollections как главных источников роста.

### ValidateData при загрузке

Нормализация при загрузке: clamp отрицательных значений ресурсов, фильтрация null-записей в инвентаре и задачах крафта, дедупликация карточек. Защищает от битых данных пришедших с сервера или после бага.

---

## Проблемы

### 1. `SaveService` без интерфейса — главная архитектурная проблема

```csharp
// BootstrapInstaller.cs
builder.Register<SaveService>(Lifetime.Singleton);
// Нет ISaveService — все фичи зависят от конкретного класса
```

Все потребители (Fishing, Crafting, Inventory и т.д.) принимают в конструктор конкретный `SaveService`. Следствия:
- нельзя замокировать в юнит-тестах без Unity-окружения
- нельзя подменить реализацию без правки всех потребителей

`SaveMigrationService` — та же проблема.

---

### 2. Rate Limit удерживает семафор во время ожидания

```csharp
// SaveService.cs — SaveAllAsync()
await _semaphore.WaitAsync(cancellationToken);
try
{
    if (elapsed < SaveRateLimit)
    {
        await UniTask.Delay(delay, ...); // ← семафор держится до 500мс
    }
    // ...
}
finally { _semaphore.Release(); }
```

Пока идёт `Delay`, любой вызов `UpdateModuleAsync` из другого потока заблокирован. При активной игровой сессии это создаёт задержки в обновлении состояния. `Delay` должен выполняться **до** `WaitAsync`.

---

### 3. `GameSaveData` — God Object

Каждая новая фича требует изменения трёх мест одновременно:
1. Добавить поле в `GameSaveData`
2. Добавить инициализацию в `EnsureDefaults()`
3. Добавить валидацию в `ValidateData()`

Это делает `Core.Models` зависимой от всех фич. `CustomModulesJson: Dictionary<string,string>` — escape hatch, который уже есть в коде — признак того, что авторы сами чувствовали ограничение.

---

### 4. `HttpSaveStorage.Exists()` всегда возвращает `true`

```csharp
// HttpSaveStorage.cs
public bool Exists()
{
    return true; // всегда
}
```

Логика в `LoadAllAsync`:

```csharp
if (_storage.Exists())      // true — всегда заходим сюда
{
    var json = await _storage.LoadAsync(...); // HTTP GET → 404 → null
    _data = DeserializeOrDefault(json, ...); // null → CreateDefault
}
else
{
    // ← этот блок НИКОГДА не выполняется при HTTP-хранилище
    var migrated = await _migrationService.TryMigrateLegacyAsync(...);
}
```

При HTTP-хранилище миграция никогда не запускается, даже если на диске есть legacy-данные. Потенциальная потеря прогресса у пользователей, перешедших с LocalDiskStorage на HTTP.

---

### 5. Нет offline-fallback для `HttpSaveStorage`

Если `SaveAsync` по HTTP падает — исключение летит наверх и сохранение теряется. Нет локального кэша, нет retry с persistent queue. В нестабильных сетях (мобайл) это означает потерю прогресса между сессиями.

---

### 6. `OnBeforeSave` / `OnAfterLoad` — синхронные события

```csharp
public event Action OnBeforeSave;
public event Action OnAfterLoad;
```

Если фиче нужно выполнить async-работу перед сохранением (например, flush незавершённой транзакции) — это невозможно без хаков. Правильный паттерн — `Func<UniTask>` или зарегистрированный список hook-обработчиков.

---

### 7. `CloneDetached` — двойная сериализация при каждом чтении

```csharp
private static T CloneDetached<T>(T source)
{
    var json = JsonConvert.SerializeObject(source, Formatting.None);
    return JsonConvert.DeserializeObject<T>(json);
}
```

Вызывается при каждом `GetReadonlyModuleAsync`. Для модулей с большим числом записей (большой инвентарь, много рыбы) — ощутимые аллокации на каждый read. Дополнительно: в `ComputeHash` объект тоже сериализуется, а затем снова в `SaveAllAsync` — итого три сериализации за один save.

---

### 8. `IDisposable` не зарегистрирован в контейнере

```csharp
// BootstrapInstaller.cs
builder.Register<SaveService>(Lifetime.Singleton);
// Нет: .As<IDisposable>()
```

`SaveService` реализует `IDisposable` (отменяет debounce CTS, освобождает семафор). VContainer не вызовет `Dispose()` при разрушении scope без явной регистрации. При перезагрузке сцены или в тестах — утечка ресурсов.

---

### 9. URL жёстко прописан в `HttpSaveStorage`

```csharp
// HttpSaveStorage.cs
_fullUrl = ApiConfig.BaseUrl + ApiConfig.SaveGlobalPath;
```

Не инжектируется — тестировать без настоящего сервера невозможно. Нет поддержки staging/production переключения через конфиг.

---

### 10. Нет conflict resolution для мульти-девайс

`GetLastModifiedTimestampAsync` есть в интерфейсе `ISaveStorage`, но нигде не используется в `SaveService`. При игре на двух устройствах второй сейв перезапишет первый без предупреждения. Инфраструктура для решения заложена, логика не реализована.

---

## Итоговая оценка

| Аспект | Оценка | Комментарий |
|---|---|---|
| Thread safety | Хорошо | Правильный SemaphoreSlim, но rate limit удерживает его |
| Надёжность записи (local) | Отлично | Атомарные операции + .bak |
| Надёжность записи (HTTP) | Слабо | Нет offline cache, нет retry |
| Тестируемость | Плохо | Нет ISaveService / ISaveMigrationService |
| Расширяемость | Удовлетворительно | God Object + CustomModulesJson как костыль |
| Observability | Хорошо | Payload breakdown, prefix-логи на каждую операцию |
| Integrity | Хорошо | SHA256 + Revision |
| Миграция | Хорошо (local) / Не работает (HTTP) | Проблема с Exists() |
| Multi-device sync | Нет | Метод есть, логики нет |

**Резюме:** ядро системы — `SaveService` + `LocalDiskStorage` — написано уверенно и закрывает большинство production-рисков для single-device сценария. Узкие места — архитектурные решения (отсутствие интерфейса, God Object), которые при росте проекта создают трение, и конкретный баг с `HttpSaveStorage.Exists()`, способный привести к потере данных при миграции с локального хранилища на HTTP.

---

## Рекомендации для MyBookstore

При проектировании системы сохранений для MyBookstore следует:

1. **Ввести `ISaveService`** — все потребители зависят от интерфейса, не от класса.
2. **Модульная модель данных** — вместо God Object использовать `Dictionary<string, string> Modules` как основной механизм (каждая фича — своя JSON-строка), а не escape hatch.
3. **Rate limit вне семафора** — `Delay` до `WaitAsync`, не внутри.
4. **Offline write-through кэш** — при HTTP-хранилище всегда дублировать последний успешный save на диск; при ошибке сети — retry из кэша.
5. **Async hooks** — `List<Func<UniTask>> PreSaveHooks` вместо `event Action`.
6. **Регистрировать `IDisposable`** — `builder.Register<SaveService>(...).As<IDisposable>()`.
