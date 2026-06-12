# Logging System — Code Review

**Дата ревью:** 2026-06-10
**Состояние:** uncommitted (изменения в working tree)
**Скоуп:** новый модуль логирования по образцу Research-овской системы (доку см. `docs/archive/heroes-logging-system.md` / `docs/INPROGRESS/heroes-logging-system.md`)

## Контекст

В рамках задачи добавления собственного логирования по образцу Research-проекта добавлены:

- `Assets/Game/Infrastructure/Logging/` — новая папка в существующей сборке `Infrastructure`
- 13 файлов рантайма + 1 Editor-меню + asmdef тестов + LoggerSettings.asset

**Изменено в `working tree`:**
- `Assets/Game/Infrastructure/Logging/` (новая папка)
- `Assets/Game/Infrastructure/Infrastructure.asmdef` (M — добавлены refs)
- `Assets/Game/Core/Installers/Features/InfrastructureVContainerBindings.cs` (M — регистрация GameLogger / ILogService / CommandLoggerAdapter)

### Файлы под ревью

| Файл | Назначение |
|---|---|
| `LogLevel.cs` | enum уровней (Trace..Critical) |
| `LogChannel.cs` | static class с 6 nested-классами (Common/Loading/Configs/Save/Infrastructure/Gameplay) |
| `LogEntry.cs` | readonly struct + `ToUnityLogType()` |
| `ILoggerTarget.cs` | контракт target-а (Name + Write) |
| `ILogService.cs` | контракты `IChannelLogger`/`<TChannel>`, `ILogService`, `ILoggerSettingsService` |
| `LoggerSettings.cs` | SO с раздельными профилями Editor/Player |
| `LoggerSettingsService.cs` | runtime-сервис с PlayerPrefs override |
| `GameLogger.cs` | главный сервис + `ChannelLogger<TChannel>` + `CommonChannelLogger` |
| `GameLogHandler.cs` | реализация `UnityEngine.ILogHandler` для перехвата Debug.Log |
| `ConsoleLogTarget.cs` | вывод в Unity Console через `UnityConsoleSink` |
| `FileLogTarget.cs` | rolling-файл в `persistentDataPath/logs/` |
| `ChannelLoggerExtensions.cs` | публичное API (LogTrace/Debug/Info/Warning/Error/Critical + WithPayload) |
| `CommandLoggerAdapter.cs` | мост `Game.Commands.ICommandLogger` → новый канал `Infrastructure` |
| `Editor/LoggerMenu.cs` | toolbar для file-toggle |
| `Tests/Editor/LoggingModuleTests.cs` | 5 тестов |

---

## ⚠️ Серьёзное (баги поведения)

### 1. Debug.Log может замолчать полностью

`GameLogHandler.LogFormat` НЕ дёргает `_previousHandler` — только пишет в наш `_sink`. Маршрут:

```
Debug.Log("foo")
  → GameLogHandler.LogFormat → _sink → GameLogger.Dispatch
       → FileLogTarget.Write   (если File enabled + level прошёл)
       → ConsoleLogTarget.Write (если Console enabled + level прошёл)
                                  → UnityConsoleSink → _previousHandler.LogFormat → Unity Console
```

Если **`IsConsoleEnabled = false`** ИЛИ уровень `Debug.Log` ниже `ConsoleMinimumLevel` — сообщение НЕ попадает в Unity Console вообще. Это поведенческий регресс по сравнению с обычным `Debug.Log`.

В Research-доке об этом сказано прямо: «`PassBackToUnity = true` до записи (защита от рекурсии)» — у них есть pass-through, у нас — нет.

**Сценарий проблемы:** разработчик выключает console через PlayerPrefs override, потом удивляется, что `Debug.Log` ничего не выводит даже в Editor.

**Фикс (направление):** добавить безусловный pass-through к `_previousHandler` в `GameLogHandler.LogFormat`/`LogException` ДО `_sink`, либо — в `ConsoleLogTarget` всегда выводить intercepted-entry в Unity Console (минуя `_settings.IsConsoleEnabled` для source=UnityIntercepted), либо реализовать `PassBackToUnity`-флаг как в Research.

### 2. Race condition в `GameLogger._cache`

```csharp
private readonly Dictionary<Type, IChannelLogger> _cache = new();
...
public IChannelLogger<TChannel> GetLogger<TChannel>()
{
    if (_cache.TryGetValue(key, out var existing)) ...
    _cache[key] = created;
    ...
}
```

Без лока. Логи могут вызываться с воркер-потоков (UniTask continuation, async-цепочки) — `Dictionary` не thread-safe. При конкуррентном чтении/записи бросит `InvalidOperationException` или вернёт мусор.

**Фикс:** заменить на `ConcurrentDictionary<Type, IChannelLogger>` + `GetOrAdd(key, t => new ChannelLogger<TChannel>(this))`.

### 3. Несогласованность уровней для перехваченных исключений

В `GameLogHandler.LogException` уровень захардкожен `LogLevel.Error`:

```csharp
public void LogException(Exception exception, UnityEngine.Object context)
{
    var entry = new LogEntry(..., LogLevel.Error, ...);
```

Но `Map(LogType.Exception)` возвращает `LogLevel.Critical`. Один и тот же exception, прилетевший разными путями (`Debug.LogException` vs `Debug.LogFormat(LogType.Exception, ...)`), даст разные уровни в `LogEntry`.

**Фикс:** заменить `LogLevel.Error` на `LogLevel.Critical` в `LogException`.

### 4. `FileLogTarget.Dispose()` не идемпотентен

```csharp
public void Dispose() {
    lock (_sync) {
        _writer?.Dispose();
        _writer = null;
    }
}
```

После Dispose `_writer = null`. Но если кто-то всё ещё держит `IChannelLogger` после Dispose `GameLogger` (cached экземпляры живы), и вызовет `Log(...)` → `Dispatch` → `FileLogTarget.Write` → `EnsureWriter()` создаст **новый** writer и продолжит писать в файл. Файл «воскресает».

**Фикс:** добавить `_disposed` флаг и ранний return из `Write()` если `_disposed == true`.

### 5. Потеря fidelity отображения исключений в Unity Console

`Debug.LogException(ex)` → наш `GameLogHandler.LogException` → `ConsoleLogTarget.Write` → `UnityConsoleSink.Log(LogType.Exception, exceptionString)` → `_previousHandler.LogFormat(LogType.Exception, null, "{0}", exceptionString)`.

Unity ожидает Exception object через `LogException(ex, context)`, а не строку через `LogFormat`. Через `LogFormat(LogType.Exception, ...)` Unity покажет, но без специальной разметки и клика-в-стектрейс.

**Фикс:** `UnityConsoleSink` должен принимать опциональный Exception object и вызывать `_handler.LogException(ex, null)` когда type == LogType.Exception. Потребует прокинуть Exception из `LogEntry` через сигнатуру sink-а.

---

## 🟡 Средние

### 6. Молчаливый fallback `Resources.Load<LoggerSettings>` → null

В `InfrastructureVContainerBindings`:
```csharp
new LoggerSettingsService(Resources.Load<LoggerSettings>("LoggerSettings"))
```

Если asset потеряется (rename, удаление meta, забыли положить в Resources/), `Resources.Load` вернёт `null`, `LoggerSettingsService` тихо создаст дефолтный SO. Никакого warning'а. Долгая отладка «почему дефолты, я же поменял?».

**Фикс:** `Debug.LogWarning("[Logging] LoggerSettings asset not found in Resources/. Using built-in defaults.")` при `null`.

### 7. `CommandLoggerAdapter.Map` — default → `Information`

```csharp
return level switch {
    CommandLogLevel.Trace => LogLevel.Trace,
    CommandLogLevel.Debug => LogLevel.Debug,
    CommandLogLevel.Warning => LogLevel.Warning,
    CommandLogLevel.Error => LogLevel.Error,
    _ => LogLevel.Information   ← все неизвестные → Info
};
```

Если в `CommandLogLevel` появится новый член (Critical/Fatal) — он молча уйдёт в Info.

**Фикс:** либо явно перечислить все случаи, либо `throw new ArgumentOutOfRangeException(...)` для неизвестных, либо `Debug.LogWarning` на default.

### 8. Editor menu — только file toggle

В `LoggerMenu.cs` есть Enable/Disable/Clear для **File** override и Print Log Directory. Console-override отсутствует, хотя API `SetConsoleEnabledOverride` есть.

**Фикс:** добавить симметричные пункты `Tools/Logging/Enable/Disable/Clear Console Logging Override`.

---

## 🟢 Низкие

### 9. `string.Format` allocations на каждый лог

Research-дока упоминает `ZString.Format` (zero-allocation, Utf8). У нас `string.Format(CultureInfo.InvariantCulture, format, args)` — аллоцирует строку каждый раз, плюс boxing value-типов в `params object[] args`. Для частых логов (например, в game loop) — GC pressure.

**Статус:** известный trade-off, согласован пометкой «немного урезан» в исходной доке.

### 10. Reflection на private field в тесте

`FileLogTarget_Rolls_When_Size_Limit_Is_Reached`:
```csharp
typeof(LoggerSettings).GetField("_rollSizeKb", BindingFlags.NonPublic | ...)
    ?.SetValue(asset, 1);
```

Если переименовать `_rollSizeKb` → тест **тихо пройдёт** (`?.SetValue` на null без эффекта), но реально проверять будет дефолтное значение, не 1.

**Фикс:** `internal`-сеттер на `LoggerSettings` под `InternalsVisibleTo` либо тестовый сабкласс.

### 11. Multi-instance handler restoration

`GameLogger.Dispose` восстанавливает `_previousHandler` только если current handler === self. Это правильно. Но создание двух `GameLogger` подряд (бывает в тестах): второй захватит первого как `_previousHandler`, Dispose второго восстановит первого, Dispose первого — ничего не сделает, оригинальный Unity handler потерян.

**Статус:** в single-DI-singleton флоу не страшно. В тестах может укусить — следить за Dispose-порядком.

### 12. `internal RawSettings` утечка SO наружу

`LoggerSettingsService.RawSettings` — `internal` для `FileLogTarget`. Архитектурный смолл: target лезет внутрь сервиса вместо того, чтобы получать значения через интерфейс (`RollSizeKb`, `FilePrefix`, `MaxRetainedFiles`).

**Фикс:** расширить `ILoggerSettingsService` геттерами для этих полей.

---

## 📋 Notes (не баги)

- **Каналов всего 6** (`Common`, `Loading`, `Configs`, `Save`, `Infrastructure`, `Gameplay`) против 30+ у Research. Согласовано исходно. По мере появления фич — добавлять.
- **Нет `[Conditional("ENTERPRISE")]`** — у Research Trace/Debug стрипятся через 3 define'а (`DEVELOPMENT_BUILD`, `UNITY_EDITOR`, `ENTERPRISE`). У нас два первых. В release-сборке Debug-уровень вычислится в no-op, но zero-strip отсутствует.
- **Нет `SpamDetector`** — частые повторы (например, лог в `Update`) могут засрать file/console. Acceptable для MVP.
- **Нет `WidgetLogger`** / структурного payload-форматирования — `payload.ToString()` пишется как есть. Если payload — сложный объект, в логе будет только имя типа без полей. В проде обычно используют JSON-serialization.
- **`GameLogger` сам не реализует `IChannelLogger`** — в Research реализует и делегирует к `LogChannel.Common`. У нас отдельный `CommonChannelLogger`. Не баг, чуть отличается API.

---

## ✅ Что чисто

- `LogEntry` — readonly struct, `in`-параметры в `Write` — правильно для GC.
- Asmdef тестов в порядке (refs: `Infrastructure`, `Game.Commands.Abstractions`, `UnityEngine.TestRunner`).
- `LoggerSettings` SO с двумя профилями (Editor/Player) — хорошее разделение.
- `LoggerSettingsService` с PlayerPrefs override + Clear — runtime control без пересборки.
- `CommandLoggerAdapter` тонкий и понятный — мост корректный.
- `RegisterBuildCallback(resolver => resolver.Resolve<ILogService>())` — умное принудительное создание, чтобы handler-интерсепт активировался сразу при build контейнера.
- Тесты покрывают: PlayerPrefs overrides, file write, file rolling, command adapter mapping, typed logger caching.

---

## 🎯 Приоритет починки

| # | Issue | Effort | Impact |
|---|---|---|---|
| 1 | Pass-through Debug.Log в `GameLogHandler` | S | High — меняет поведение Unity |
| 2 | `ConcurrentDictionary` вместо `Dictionary` в `_cache` | XS | High — раз в день, но крашит |
| 3 | `LogException` → `LogLevel.Critical` | XS | Low — семантическая правка |
| 4 | `FileLogTarget._disposed` флаг | XS | Low — профилактика |
| 5 | Exception fidelity через `LogException` | M | Low — UX в Console |
| 6 | Warning при `Resources.Load == null` | XS | Low — DX |
| 7 | Editor menu для Console toggles | S | Low — DX |

**Рекомендация:** закрыть 1-4 одним фиксом перед коммитом. 5-7 — отдельной итерацией / в backlog.
