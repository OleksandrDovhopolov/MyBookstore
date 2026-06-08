# Save System — паттерны из прод-проекта

Инструкция: какие паттерны взять из большого прод-проекта (анализ — `docs/SAVE_SYSTEM.md`) и какие изменения внести в **уже перенесённый** код Save в MyBookstore.

**Контекст:** Save-система в MyBookstore уже перенесена из Research по `docs/SAVE_MIGRATION_FROM_RESEARCH.md`. Этот документ — про дополнения поверх перенесённого. Из прод-проекта **код не копируем**, только смотрим как референс.

Документ читается параллельно с прод-проектом: открываем файлы из таблицы "Где смотреть в проде", изучаем подход, реализуем в MyBookstore по своим интерфейсам.

---

## Правило №1 — ничего не копировать

Прод-проект использует MessagePack, 95+ Entry-классов, мультиоблако (iCloud/AWS), дельта-патчи с CRC. Всё это — overkill для MVP и несовместимо с архитектурой "Dictionary модулей с opaque JSON". Из прода берём **только идеи**.

---

## Паттерн 1: Atomic write с .bak — сверить детали

**Зачем:** в Research уже есть `LocalDiskStorage` с atomic write и `.bak`. Прод-проект делает то же самое, но местами тщательнее. Хорошо сверить детали — возможно подсмотрим один-два edge-case.

**Где смотреть в проде:**
- `Assets/Shared/CoreLogic/CoreSaves/Core/Storage/External/FileStorage.cs`
- `Assets/Shared/CoreLogic/CoreSaves/Core/Storage/External/FileSavePaths.cs`

**Что искать:**
- Точный порядок: открытие, запись, fsync(?), close, rename, удаление .bak
- Обработка случаев: `.tmp` уже существует, `.bak` уже существует, `target` отсутствует при первом запуске
- Использование `File.Replace` vs `File.Move`+`File.Delete`

**Что обновить в MyBookstore:**
- `Save/Storage/LocalDiskStorage.cs` — если найдены отсутствующие в Research проверки edge-cases, добавить их. Изменения вида: "если `.tmp` остался от прошлого падения — удалить перед началом записи".

**Что НЕ брать:**
- Шифрование (если есть в проде)
- Множественные slot-файлы (saveSlot1, saveSlot2…) — для MVP не нужно

---

## Паттерн 2: SaveBehaviour — блокировка autosave во время критических операций

**Зачем:** автосейв (debounce 600мс) может сработать посреди критической транзакции — например, во время гача-пулла или начисления покупки. Прод-проект явно блокирует flush на время таких операций.

**Где смотреть в проде:**
- `Assets/Shared/CoreLogic/CoreSaves/Core/SaveBehaviour.cs`

**Что искать:**
- Как enable/disable flush — флаг, счётчик, lease?
- Что делать с уже запланированным сохранением — отменять, дожидаться окончания критической секции?
- Пример использования в боевом коде проекта

**Что добавить в MyBookstore:**
- В `SaveService` — метод `IDisposable BlockAutosave()` через counter (reentrant). Когда счётчик > 0 — debounced save не выполняется, только накапливаются изменения. При `Dispose` последнего lease — если был отложенный save, выполнить.

```csharp
private int _autosaveBlockCount;

public IDisposable BlockAutosave()
{
    Interlocked.Increment(ref _autosaveBlockCount);
    return new AutosaveLease(this);
}

private void ReleaseAutosaveLease()
{
    if (Interlocked.Decrement(ref _autosaveBlockCount) == 0 && _hasPendingSave)
    {
        ScheduleDebouncedSave();
    }
}
```

**Использование (в фичах):**
```csharp
using (_saveService.BlockAutosave())
{
    // критическая операция: gacha pull, IAP grant
    await DoCriticalWork();
}
// после Dispose — отложенный save запустится
```

---

## Паттерн 3: Server sync на старте — timestamp pull/push

**Зачем:** при старте игры нужно понять: что свежее — локальный сейв или серверный? И корректно подтянуть нужное. В Research этого нет, в проде есть.

**Где смотреть в проде:**
- `Assets/Root/Initialization/Server/TryLoadProgressFromServer.cs`

**Что искать:**
- Алгоритм сравнения timestamps
- DTOs (что отправляется/принимается)
- Обработка ProgressReset (сервер просит обнулить локалку)
- Логика "сервер новее → pull", "клиент новее → push", "равны → ничего"

**Что добавить в MyBookstore:**
- Новый компонент `Save/Sync/SaveSyncBootstrap.cs` (запускается из Bootstrap после загрузки локального сейва):

```csharp
public sealed class SaveSyncBootstrap
{
    public async UniTask SyncOnStartupAsync(CancellationToken ct)
    {
        var local = await _local.LoadMetaAsync(ct);  // только meta, не весь сейв
        var server = await _http.LoadMetaAsync(ct);  // отдельный лёгкий endpoint /save/meta

        if (server == null) { await _http.PushAsync(local, ct); return; } // первый запуск на сервере
        if (local == null)  { await _local.SaveAsync(await _http.PullAsync(ct), ct); return; }

        if (server.Revision > local.Revision)
            await _local.SaveAsync(await _http.PullAsync(ct), ct);
        else if (local.Revision > server.Revision)
            await _http.PushAsync(local, ct);
        // равны — ничего
    }
}
```

**Что упростить против прода:**
- Без дельта-патчей. Всегда переливаем целиком — у нас сейв должен быть < 30 KB (см. payload telemetry из Research).
- Без отдельного `RequestGetSaveData`/`RequestSetSaveData` — два endpoint'а: `GET /save` и `POST /save`.
- Резолв конфликтов — last-write-wins по `Revision`. Никакого user-facing UI выбора.

**На сервере (.NET):**
- Опционально добавить `GET /save/meta` который возвращает только meta-объект — экономит трафик при синке.

---

## Паттерн 4: SaveMode enum (Regular / ForceClientOnly / Force)

**Зачем:** иногда нужно сохранить локально без попытки достучаться до сервера (например, оффлайн-режим осознанно). Иногда — наоборот, форсировать push даже если debounce ещё не сработал.

**Где смотреть в проде:**
- Поиском `SaveMode` или `enum.*Save` в кодовой базе. Обычно используется в вызовах вида `saveController.Save(SaveMode.Force)`.

**Что добавить в MyBookstore:**

`Save/SaveMode.cs`:
```csharp
public enum SaveMode
{
    Regular,          // обычный сейв (debounce + push на сервер если онлайн)
    ForceLocalOnly,   // принудительно локально, сервер не трогаем
    ForceWithSync     // мгновенный flush + push на сервер (минуя debounce и rate limit)
}
```

В `SaveService.SaveAllAsync`:
```csharp
public async UniTask SaveAllAsync(SaveMode mode = SaveMode.Regular, CancellationToken ct = default)
{
    // ForceWithSync → пропускаем rate limit и debounce
    if (mode != SaveMode.ForceWithSync && elapsed < SaveRateLimit) { /* delay */ }

    // ForceLocalOnly → используем LocalDiskStorage напрямую, минуя HTTP
    var storage = mode == SaveMode.ForceLocalOnly ? _localStorage : _activeStorage;
    await storage.SaveAsync(json, ct);
}
```

---

## Паттерн 5 (опционально): Payload telemetry — детализация по модулям

**Зачем:** в Research уже есть payload telemetry с warning > 30 KB. В проде она помельче гранулирована — отдельно логирует каждый Entry. У нас Entry нет, но модули есть.

**Что обновить в MyBookstore:**
- При каждом save логировать размер каждого `ModulePayload.Json` в байтах, warning если суммарно > 30 KB или один модуль > 5 KB.
- Уже частично есть из Research — нужно убедиться, что разбивка делается по `_data.Modules.Keys`, а не по полям GameSaveData (которого у нас нет).

---

## Что НЕ брать из прода

| Паттерн | Почему |
|---|---|
| `AbstractEntry<TData>` + 95+ наследников | Микро-модульность — overengineering для MVP. У нас модуль = одна JSON-строка |
| MessagePack сериализация | Бинарь дороже в дебаге и миграциях. Если упрёмся в размер — добавим позже |
| CRC per-entry | У нас один SHA256 на всю обёртку — достаточно |
| Дельта-патчи (`SaveDataEntryPatch`) | Нужны при сейве сотен KB. У нас цель < 30 KB |
| iCloud / AWS S3 / `CloudSavePacker` | У нас только свой .NET сервер |
| `ChangeCloudCommand` | Нет смены провайдеров |
| Два направления (платформа + свой сервер) | Дублирование conflict resolution — выкинуть |
| MemoryStorage / BackupStorage отдельными классами | У нас всё in-memory в `SaveService._data`, бэкап делает `LocalDiskStorage` через `.bak` |

---

## Порядок применения паттернов

1. **Сначала Паттерн 1** (atomic write — самое безопасное, точечные правки в `LocalDiskStorage`).
2. **Потом Паттерн 4** (SaveMode — расширение API `SaveService`, не ломает существующее).
3. **Потом Паттерн 2** (BlockAutosave — добавление новой публичной возможности).
4. **Потом Паттерн 3** (Server sync — требует наличия .NET endpoint'ов).
5. **В последнюю очередь Паттерн 5** (telemetry — косметика).

Каждый паттерн — отдельный коммит с понятным сообщением (`feat(save): add SaveMode enum`, `feat(save): block autosave during critical sections` и т.д.).

---

## Чек-лист после применения всех паттернов

- [ ] `LocalDiskStorage` обрабатывает stale `.tmp` и `.bak` от прошлых падений
- [ ] `SaveService.BlockAutosave()` работает reentrant (нестинг lease)
- [ ] `SaveSyncBootstrap.SyncOnStartupAsync` корректно отрабатывает 4 случая: новый сервер / новый клиент / сервер свежее / клиент свежее
- [ ] `SaveMode.ForceWithSync` действительно минует debounce и rate-limit
- [ ] Payload telemetry разбивается по `Modules`, не по полям несуществующего God Object
- [ ] Ни одного `using UnityEngine.iOS` / AWS / MessagePack в Save-сборке
