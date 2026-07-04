# ADR-0008: Quest sales progress persistence

- **Status:** Accepted
- **Date:** 2026-07-04
- **Deciders:** project owner
- **Related:** [ADR-0001](0001-save-data-modular-payload.md), [ADR-0007](0007-quest-system.md), [QUESTS.md](../QUESTS.md)

## Context

Продажные задачи квестов должны считать прогресс **с момента активации задачи**, а не от lifetime-статистики продаж.
Например, `soldGenreAtLocation Fantasy loc_downtown min 15` должен игнорировать `Fantasy`, проданные на
`loc_downtown` до старта этой задачи.

MVP-реализация решила это через per-task baseline: при активации задачи сохраняется `SalesStatsStateDto`,
а условия продаж читаются через scoped-reader (`live - baseline`). Поведение корректное, но save получается
слишком крупным: baseline хранит все жанры, все локации и все дни, даже если конкретной задаче нужен только
один жанр или одна пара `(locationId, genre)`.

В `heroes` используется другая форма: события/observers двигают прогресс задач, а quest-save хранит локальное
состояние квестов и задач, не полный снимок мира. Это хорошее будущее направление для MyBookstore, но текущий
проект ещё pull-based: `QuestsService` переоценивает `ICondition`-деревья при изменениях доменных сервисов, а
sales event сейчас является сигналом "пересчитать условия", не полноценным источником persisted quest progress.

## Decision

Сохраняем текущую pull-based модель conditions, но заменяем полный per-task baseline на **compact baseline DTO**.

Compact baseline должен хранить только те счётчики продаж, которые реально нужны sales-условиям активной задачи:

- `soldGenre` хранит baseline-count для нужного жанра;
- `soldGenreAtLocation` хранит baseline-count для нужной пары `(locationId, genre)`;
- `soldGenreInSingleDay` хранит только данные, нужные чтобы отсечь продажи до активации для нужного жанра:
  границу дня активации и count на момент активации, а не все unrelated дни/жанры.

Публичная семантика не меняется:

- sales-условия в quest completion считают прогресс от активации задачи;
- lifetime-условия вне квестов продолжают читать lifetime stats;
- `Game.Quest` владеет lifecycle, save, task-state и scoped progress;
- `Game.SalesStats` остаётся источником lifetime-счётчиков.

Compact baseline — ближайшая цель, потому что он убирает избыточность save без замены runtime-модели квестов.

## Future Direction

Переходить ближе к `heroes` observer/event-driven модели стоит только после того, как sale events станут
первоклассными domain events. "Первоклассный" здесь означает: событие не просто говорит "что-то поменялось,
пересчитайся", а является устойчивым gameplay-фактом с достаточными данными и гарантиями, чтобы другие системы
могли строить на нём persisted progress.

Минимальные критерии готовности:

- committed sale event содержит нужные измерения: `bookId`, `genre`, `locationId`, `day`, а также amount/count,
  если появятся батчевые продажи;
- событие публикуется из авторитетного commit-path после успешной продажи;
- порядок обновления quest progress и save достаточно надёжен, чтобы crash/reload не терял прогресс молча;
- активные quest tasks умеют сохранять собственный task-local progress, а не зависят от вычитания полного
  world snapshot;
- тесты покрывают reload, защиту от дублей event'ов и семантику "старые продажи не считаются".

Когда эти условия выполнены, sales-задачи смогут стать subscribers/observers: matching sale event инкрементит
локальный progress задачи, а quest-save хранит task progress state. Это будет ближе к `heroes` по духу и уберёт
потребность в baseline для глобальных счётчиков.

## Consequences

### Positive

- Меньше quest-save для активных sales-задач.
- Ниже риск миграции, чем при немедленной замене quest-progress модели.
- Сохраняется текущий UI-путь через `ICondition` / `ConditionResult`.
- Семантика "с момента активации" переживает reload.

### Negative / costs

- Compact baseline требует анализа sales-условий задачи или эквивалентного capture plan.
- `soldGenreInSingleDay` требует аккуратной обработки границы дня активации.
- В проекте временно остаются две формы sales progress: lifetime `SalesStats` и quest-scoped baseline deltas.

### Not Chosen

- Долго хранить полный `SalesStatsStateDto` в каждой sales-задаче: корректно, но раздувает save.
- Сразу перейти на чистый event-driven quest progress: рано, пока sale events не являются стабильным persisted
  gameplay contract.
- Не хранить baseline/progress вообще: reload сломает семантику "с момента активации".
