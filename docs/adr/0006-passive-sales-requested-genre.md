# ADR-0006: Passive sales — per-customer requested-genre roll

- **Status:** Accepted
- **Date:** 2026-06-25
- **Deciders:** project owner
- **Supersedes:** уточняет **стадию 1** пассивной продажи из [ADR-0004](0004-stock-model-hybrid-sale-chance.md)
  (ролл по всем жанрам полки → ролл по одному жанру из запроса покупателя). Стадия 2 (взвешенный
  `RarityWeight`) и формула шанса (`f(count) × locationMod × decorMod`) — без изменений.
- **Related:** [ADR-0003](0003-customer-simulation.md), [ADR-0004](0004-stock-model-hybrid-sale-chance.md),
  [CORE_LOOP.md](../CORE_LOOP.md)

## Context

Реализация ADR-0004 (`WeightedPassiveSaleSelector`) на **стадии 1** бросала кубик **по каждому жанру,
присутствующему на полке**, собирала «победителей» и выбирала одного. Следствия:

- «Предпочитаемые жанры покупателя», заложенные в ADR-0004, фактически **не управляли** выбором —
  селектор катал все жанры полки независимо от покупателя.
- На **промахе** не было одного «провалившегося жанра» (промах = все жанры полки не прошли), поэтому
  фидбэк над покупателем не мог показать спрайт жанра при неуспехе — только при успехе (через `bookId`).

Нужно: чтобы у пассивной попытки был **один определённый жанр и в успехе, и в провале** — для читаемого
фидбэка (бабл показывает спрайт жанра в обоих случаях) и как фундамент под квестовых персонажей.

## Decision

Каждый покупатель приходит с **профилем желаний** — **1‑3 жанра (обычно 2)**. На каждой пассивной
попытке:

1. Равновероятно выбрать **один** жанр из профиля — среди тех, что **есть на полке** (стоковые).
2. Посчитать шанс **только для этого жанра** (`IBaseSaleChanceCalculator`, формула ADR-0004:
   `f(count) × locationMod × decorMod`; погода добавится тем же множителем) и сделать **один ролл**.
3. Hit → выбрать книгу жанра взвешенно по `RarityWeight` (стадия 2 ADR-0004 — без изменений).
4. Выбранный жанр известен **всегда** (и на hit, и на miss).

Правила:
- **Нет стока** ни по одному запрошенному жанру → miss, но с жанром, выбранным из запроса (для фидбэка).
- Выбор жанра — **равновероятный**; каждая попытка делает **новый** выбор.
- Размер запроса — `SalesTuning.PassiveRequestGenreCount` (дефолт **2**), клампится к доступным жанрам.
- Профиль — **общий «desire profile»** (домен `CustomerProfile`), фундамент под квестовых персонажей и
  под будущую конвергенцию с активным `RequestConfig` (пока — отдельные сущности).

### Архитектура (seam)

- **Общее ядро + стратегия резолва.** Флоу `PassivePurchaseStep` (browse/commit/feedback, reserve-race,
  release-on-exit) остаётся **одним ядром**; «как резолвить кандидата» вынесено за интерфейс
  `IPassivePurchaseResolver.Resolve(self, ctx, available) → PassiveAttemptResult`.
- Результат `PassiveAttemptResult { ResolvedGenre; Success; Book; … }` — выбранный жанр живёт **только**
  в `ResolvedGenre` (не переиспользуем `MatchedGenres`/`PassiveSaleEvent`).
- Две стратегии за одним seam:
  - `RequestedGenrePassiveResolver` — новая модель (этот ADR). **Дефолт.**
  - `LegacyShelfPassiveResolver` — обёртка старого `IPassiveSaleSelector`/`WeightedPassiveSaleSelector`
    (модель ADR-0004). **Сохранена, не зарегистрирована** по умолчанию.
- Профиль строит `ICustomerProfileProvider` (policy-слой; дефолт `LocationDemandProfileProvider` —
  локация как **один из источников**, не «истина»: demand-жанры → фолбэк жанры полки → гарантия ≥1).
  Persona/quest-провайдеры подключаются позже.
- Жанр провала прокинут наружу: `ISalesDaySink.OnPassivePurchaseFailed(customer, genre)` →
  `CustomerPassivePurchaseFailed: Action<Customer,string>`; биндер резолвит `genre → спрайт` и показывает
  его в бабле (`PassiveSaleFailed`).
- Переключение моделей — **явными** функциями `RegisterRequestedGenrePassiveSales()` /
  `RegisterLegacyPassiveSales()` в `BookSellVContainerBindings` (не комментарии-тумблеры).

## Consequences

### Positive
- Жанр определён **и в успехе, и в провале** → читаемый фидбэк (спрайт жанра в обоих случаях).
- Предпочтения покупателя **реально** управляют пассивом; основа под квестовых персонажей (общий профиль).
- Старая модель сохранена за тем же seam → откат одной строкой.
- Формула шанса и декор/локация/погода переиспользуются без изменений (ADR-0004).

### Negative
- Изменены **общие контракты ядра** (`CustomerContext`, `SalesDayController` + событие, `ISalesDaySink`,
  шаг и его тесты) — не «чисто аддитивно».
- Пассив теперь зависит от профиля → нужна политика генерации (location/persona/quest) и её балансировка.
- Семантика «промаха» отличается от ADR-0004 (один жанр vs все жанры) — влияет на ощущение/баланс.

### Что НЕ выбрано
- Оставить ролл по всем жанрам полки (ADR-0004) — у промаха нет одного жанра, фидбэк беднее.
- Класть выбранный жанр в `PassiveSaleEvent.MatchedGenres` — размыло бы документированный смысл поля
  (demand-match); вместо этого отдельное `ResolvedGenre`.

## Implementation notes

- Домен: `CustomerProfile`, `PassiveAttemptResult`. Сервисы: `IPassivePurchaseResolver`,
  `RequestedGenrePassiveResolver`, `LegacyShelfPassiveResolver`, `GenreShelfPicker`,
  `ICustomerProfileProvider`, `LocationDemandProfileProvider`.
- `SalesTuning.PassiveRequestGenreCount` (дефолт 2). Профиль задаётся спавнером при создании покупателя.
- Тесты: `RequestedGenrePassiveResolverTests`, `LocationDemandProfileProviderTests`; существующие
  flow-тесты переведены на legacy-резолвер (поведение ADR-0004 сохранено).
- Язык кода — английский (LANGUAGE_POLICY).
