# Logging Asmdef Improvement

## Summary

Сейчас `Infrastructure` ссылается на `Game.Commands.Abstractions`, потому что `CommandLoggerAdapter` реализует `ICommandLogger` и лежит внутри infrastructure-модуля.

Это рабочее решение для MVP, но архитектурно оно размывает границы: базовый logging module начинает знать про внешний command port, хотя должен оставаться переиспользуемым низкоуровневым модулем.

Целевое улучшение: оставить core logging в `Infrastructure`, а adapter/wiring код перенести в `Game.Bootstrap`.

## Problem

Текущая зависимость выглядит так:

- `Infrastructure -> Game.Commands.Abstractions`

Причина:

- `CommandLoggerAdapter` находится в `Infrastructure`
- `CommandLoggerAdapter : ICommandLogger`
- `ICommandLogger` объявлен в `Game.Commands.Abstractions`

Из-за этого infrastructure-layer знает про feature/composition-level контракт, хотя по смыслу должен предоставлять только собственный logging API.

## Target Dependency Direction

Целевая схема зависимостей:

- `Game.Bootstrap -> Infrastructure`
- `Game.Bootstrap -> Game.Commands.Abstractions`
- `Game.Bootstrap.Loading -> Infrastructure`
- `Features -> Infrastructure`
- `Infrastructure -X-> Game.Commands.Abstractions`

Главный принцип:

- `Infrastructure` содержит только reusable service modules
- `Game.Bootstrap` содержит composition root, bindings и adapter classes
- feature-порты и bridging-код не должны жить внутри reusable infrastructure module

## Target File Layout

### Keep In Infrastructure

Оставить в `Assets/Game/Infrastructure/Logging/`:

- `ILogService`
- `IChannelLogger`
- `IChannelLogger<TChannel>`
- `ILoggerSettingsService`
- `LogLevel`
- `LogChannel`
- `LogEntry`
- `ChannelLoggerExtensions`
- `LoggerSettings`
- `LoggerSettingsService`
- `ILoggerTarget`
- `ConsoleLogTarget`
- `FileLogTarget`
- `GameLogHandler`
- `GameLogger`
- editor utility classes для самого логгера
- tests самого logging module

Это и есть core logging module.

### Move To Game.Bootstrap

Перенести из `Infrastructure` в bootstrap/integration area:

- `CommandLoggerAdapter`

Рекомендуемое место:

- `Assets/Game/Core/Installers/Infrastructure/CommandLoggerAdapter.cs`

или:

- `Assets/Game/Core/Installers/Adapters/CommandLoggerAdapter.cs`

Оба варианта подходят. Главное, чтобы файл компилировался в `Game.Bootstrap.asmdef`, а не в `Infrastructure.asmdef`.

## Asmdef Rules

### Infrastructure.asmdef

Должен:

- содержать logging module
- ссылаться только на низкоуровневые зависимости, реально нужные infrastructure-коду

Не должен:

- ссылаться на `Game.Commands.Abstractions` только ради адаптера
- ссылаться на `Game.Bootstrap`
- тянуть feature-specific порты, если это можно решить в composition layer

### Game.Bootstrap.asmdef

Может и должен знать о:

- `Infrastructure`
- `Game.Commands.Abstractions`
- `Game.Commands`
- `Game.Http`
- `Game.Bootstrap.Loading`

Именно здесь нормально держать:

- `InfrastructureVContainerBindings`
- `CommandLoggerAdapter`
- DI wiring
- composition-specific adapters

### Game.Bootstrap.Loading.asmdef

Текущее направление зависимости корректно:

- `Game.Bootstrap.Loading -> Infrastructure`

`LoadingOrchestrator` может напрямую использовать `ILogService` и `LogChannel.Loading`, потому что это зависимость feature-layer на reusable infrastructure module.

## Recommended Migration

1. Перенести `CommandLoggerAdapter.cs` из `Assets/Game/Infrastructure/Logging/` в `Game.Bootstrap` folder.
2. Убедиться, что новый путь попадает в `Game.Bootstrap.asmdef`.
3. Удалить reference на `Game.Commands.Abstractions` из `Assets/Game/Infrastructure/Infrastructure.asmdef`, если он больше нигде в infrastructure не нужен.
4. Оставить регистрацию `ICommandLogger` в `InfrastructureVContainerBindings`, но использовать уже bootstrap-версию `CommandLoggerAdapter`.
5. Пересобрать `Infrastructure`, `Game.Bootstrap` и проверить Unity compile.

## Expected Result

После изменения получится:

- logging module остаётся независимым и переиспользуемым
- `Infrastructure` больше не знает про `ICommandLogger`
- adapter layer живёт рядом с DI/configuration code
- зависимости между asmdef становятся чище и проще для дальнейшего роста логгера

## Decision

Для текущего MVP reference `Infrastructure -> Game.Commands.Abstractions` допустим как временное решение.

Финальное рекомендуемое состояние проекта:

- core logging остаётся в `Infrastructure`
- `CommandLoggerAdapter` переносится в `Game.Bootstrap`
- reference на `Game.Commands.Abstractions` из `Infrastructure.asmdef` удаляется
