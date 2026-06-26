# Commands & Http — переносимый модуль команд

Система команд (`ICommand`) как единая «единица работы» для всех систем/фич + REST/HTTP-слой на UnityWebRequest.
Зависимости (логгер/багтрекер/профайлер/DI) подключаются через тонкие порты, что делает модуль переносимым между проектами без переписывания core-логики.

## Сборки (.asmdef)

| Сборка | Папка | Назначение | Зависимости |
|---|---|---|---|
| `Game.Commands.Abstractions` | `Commands/Abstractions` | интерфейсы, ошибки, enum'ы, **порты** | UniTask |
| `Game.Commands` | `Commands/Runtime` | базовые классы, очереди, BoxCommand, фабрика, обёртки | Abstractions, UniTask |
| `Game.Http` | `Http` | REST-команды + адаптер UnityWebRequest | Abstractions, Game.Commands, UniTask |

## Порты (то, что подменяется под проект)

- `ICommandLogger` — логирование. Готовая реализация: `UnityCommandLogger` (поверх `Debug`).
- `ICommandErrorReporter` — репорт ошибок (багтрекер). Заглушка: `NoOpCommandErrorReporter`.
- `IConnectionService` (Http) — соединение. Готовая реализация: `ConnectionService` + `UnityWebRequestFactory`.

## Bootstrap (DI-агностично, вручную)

```csharp
using Game.Commands;
using Game.Http;

// 1) порты
ICommandLogger logger = new UnityCommandLogger(CommandLogLevel.Info);
ICommandErrorReporter reporter = new NoOpCommandErrorReporter();

// 2) фабрика очередей — используется во всех системах
ICommandsFactory commands = new CommandsFactory(logger, reporter);

// 3) http
IRequestFactory requestFactory = new UnityWebRequestFactory();
IConnectionService connection = new ConnectionService(requestFactory);
```

В VContainer (когда понадобится) — то же самое в `LifetimeScope`:

```csharp
builder.Register<ICommandLogger, UnityCommandLogger>(Lifetime.Singleton);
builder.Register<ICommandErrorReporter, NoOpCommandErrorReporter>(Lifetime.Singleton);
builder.Register<ICommandsFactory, CommandsFactory>(Lifetime.Singleton);
builder.Register<IRequestFactory, UnityWebRequestFactory>(Lifetime.Singleton);
builder.Register<IConnectionService, ConnectionService>(Lifetime.Singleton);
```

## Примеры использования

Своя команда фичи:

```csharp
public class SaveProfileCommand : AbstractCommand {
    public SaveProfileCommand(ICommandLogger l, ICommandErrorReporter r) : base(l, r) {}
    protected override void ExecInternal() {
        // ... синхронная работа ...
        NotifyComplete();              // или NotifyComplete(error)
    }
}

await new SaveProfileCommand(logger, reporter).ExecuteAsync();
```

Очередь с прогрессом (как загрузка локации в оригинале):

```csharp
var queue = commands.GetProgressQueueCommand("Loading", CommandFailBehaviour.Terminate);
queue.AddProgress(new WaitSecondsCommand(1f, logger, reporter), new ProgressSettings(30));
queue.AddProgress(new DownloadBytesCommand(connection, logger, reporter, url), new ProgressSettings(70));
queue.AddProgressHandler((_, percent) => Debug.Log($"Loading {percent}%"));
await queue.ExecuteAsync();
```

REST-запрос:

```csharp
var cmd = new DownloadBytesCommand(connection, logger, reporter, "https://example.com/data.json");
await cmd.ExecuteAsync();
if (cmd.IsSucceed) {
    var json = System.Text.Encoding.UTF8.GetString(cmd.LoadedBytes);
}
```

Свою REST-команду наследуй от `AbstractServiceCommand` и переопредели `ProcessSuccessResponse` /
`GetRequestTextData` (поля POST) — всё остальное (таймауты, ретраи, проверка интернета, коды статусов) уже есть.

## Что НЕ перенесено (намеренно, вне «минимума»)

- Команды конкретных фич (их пишешь поверх — как в оригинале).
- Hub/SignalR-запросы, дисковое кэширование загрузок, загрузка текстур, потоковые команды, WaitSignal (нужен свой SignalBus), профайлер.
- BestHTTP — заменён на UnityWebRequest. Чтобы сменить бэкенд, достаточно заменить `Backend/*` (адаптер `IRequest`/`IResponse`/`IRequestFactory`), команды не меняются.
```
