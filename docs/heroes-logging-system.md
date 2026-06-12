# Система логов (heroes) — анализ архитектуры

## 1. Интерфейсный слой

**`IChannelLogger`** (`Assets/Shared/CoreApi.Pure/Logger/IChannelLogger.cs`) — базовый контракт с единственным методом:

```csharp
void LogInternal<TState>(LogLevel logLevel, EventId eventId, object payload,
    TState state, Exception exception, Func<TState, Exception, string> formatter);
```

Это "низкоуровневый" вход — generic по `TState` для zero-allocation. Публичный API не здесь.

**`IChannelLogger<TChannel>`** — generic-обёртка, параметризованная по каналу. Именно этот тип инжектируется в сервисы:

```csharp
// в сервисе
class BattleService {
    IChannelLogger<LogChannel.Battle> _logger;
}
```

**`LogChannel`** (`Assets/Shared/CoreApi.Pure/Logger/LogChannel.cs`) — иерархия каналов: 30+ вложенных sealed-классов (`Battle`, `UI`, `Audio`, `Clans`, `CloudSaves`, ...). Тип канала кодируется в generic-параметре, а не в строке.

---

## 2. Extension-методы — публичное API

**`ExtensionsOfChannelLogger`** (~2327 строк) — единственный способ, которым код пишет логи:

```csharp
_logger.LogInformation("Player {0} opened chest {1}", playerId, chestId);
_logger.LogWarning(exception, "Failed to load {0}", assetName);
_logger.LogDebugWithPayload(rewardData, "Reward granted: {0}", reward.Id);
```

Ключевые детали:
- `LogTrace` / `LogDebug` — помечены `[Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR"), Conditional("ENTERPRISE")]` — **стрипятся** в prod-сборке на уровне компилятора
- `LogInformation` / `LogWarning` / `LogError` / `LogCritical` — всегда активны
- `WithPayload`-варианты — для структурированного логирования (передают объект как payload, доступный targets)
- Форматирование через **ZString.Format** (zero-allocation, Utf8), строка не строится до вывода

---

## 3. Реализации IChannelLogger

| Класс | Назначение |
|---|---|
| `ChannelLogger<TChannel>` | Основная реализация для Unity (на ZLogger) |
| `NullChannelLogger<TChannel>` | Заглушка для отключённых каналов |
| `WidgetLogger` | Декоратор — дописывает контекст виджета к каждому сообщению |
| `ServerChannelLogger<TChannel>` | Серверная реализация (без Unity) через стандартный `ILogger` |

**`ChannelLogger<TChannel>`** (`Assets/Core/Logging/ChannelLogger.cs`) — основная реализация:
1. Устанавливает `_logHandler.PassBackToUnity = true` до записи (защита от рекурсии)
2. Проверяет спам-детектор для уровней выше Debug
3. Если включён custom stacktrace для этого уровня — генерирует `StackTrace` и добавляет к сообщению
4. Делегирует в `ILogger` (ZLogger)

---

## 4. Перехват Debug.Log

**`GameLogHandler`** (`Assets/Core/Logging/GameLogHandler.cs`) — реализует `UnityEngine.ILogHandler`:

```
Debug.Log("anything")
    → GameLogHandler.LogFormat(...)
        → если PassBackToUnity: пропускаем напрямую в Unity
        → иначе: _logger.Log(logLevel, message)  ← через ChannelLogger<Common>
            → ChannelLogger устанавливает PassBackToUnity = true
            → ZLogger → UnityLogger → Unity Console
```

Это позволяет прокачать через ZLogger все логи, включая сторонние библиотеки, которые пишут напрямую в `Debug.Log`.

---

## 5. Фабрика — GameLogger

**`GameLogger`** (`Assets/Core/Logging/GameLogger.cs`) — центральный сервис (`ILogService`):
- Создаёт `ILoggerFactory` (ZLogger) с набором targets
- Кэширует `IChannelLogger` по типу канала в `Dictionary<Type, IChannelLogger>`
- `GetLogger<TChannel>()` — если канал активен → `ChannelLogger`, иначе → `NullChannelLogger`
- Сам является `IChannelLogger` (делегирует к `LogChannel.Common`)
- При `UseMessageProvider = true` создаёт `LogMessagesProcessor` — программный доступ к потоку логов

---

## 6. Настройки и управление каналами

**`LoggerSettingsService`** (`Assets/Core/Logging/LoggerSettingsService.cs`):
- Хранит список отключённых каналов в `PlayerPrefs` (ключ `Ph.Dev.LoggerSettings.Channels`)
- Позволяет включать/отключать каналы в рантайме (через Dev Panel / читы)
- Хранит пороги уровней для каждого target (изменяются в рантайме)

---

## 7. Targets

Все реализуют `ILoggerTarget.Configure(ILoggingBuilder)`:

| Target | Куда | Формат | Порог |
|---|---|---|---|
| `LoggerConsoleTarget` | Unity Console | `[ChannelName] message` | `ConsoleMinimumLogLevel` |
| `LoggerFileTarget` | Rolling-файл | `[DateTime] [Category] [Level] message` | `FileMinimumLogLevel` |
| `LoggerTeamcityTarget` | TeamCity | — | `TeamCityMinimumLogLevel` |
| *(Sentry, Crashlytics)* | Crash reporting | — | в `LoggerSettings` |

`LoggerFileTarget` — rolling по размеру (`RollSizeKb`) и дате, автоудаление старых сессий.

---

## Схема потока данных

```
Код вызывает:
  logger.LogWarning("msg {0}", arg)
        ↓ ExtensionsOfChannelLogger
  logger.LogInternal(Warning, state, formatter)
        ↓ ChannelLogger<TChannel>
  PassBackToUnity = true
  SpamDetector check
  Optional custom StackTrace
  _msLogger.Log(...)        ← ILogger (ZLogger)
        ↓
  ILoggerFactory (UnityLoggerFactory)
        ├─ LoggerConsoleTarget → Unity Console
        ├─ LoggerFileTarget    → Disk
        └─ LoggerTeamcityTarget → CI
        ↓
  PassBackToUnity = false
```

---

## Ключевые особенности дизайна

1. **Канал = тип, не строка** — `IChannelLogger<LogChannel.Battle>` вместо строкового имени, что даёт type safety и помогает DI
2. **Zero-allocation** — ZString + generic `TState` без boxing, лямбды не вызываются если уровень отфильтрован
3. **Debug/Trace стрипятся в prod** — через `[Conditional]`
4. **Перехват `Debug.Log`** — все Unity-логи проходят через ту же систему
5. **Управление каналами через PlayerPrefs** — можно отключить спамный канал на девайсе без пересборки

---

## Аутлайеры

- **`BotLogger`** (`Assets/Game/Expeditions/Bot/BotLogger.cs`) — написан напрямую через `Debug.Log`, обходит систему
- **`AbstractDevelopmentTraceLogger<T>`** — полезная база для dev-трейсинга с `[Conditional]`
