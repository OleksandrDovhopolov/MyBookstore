# Core loop — статус и план следующих шагов

> Снимок состояния на **2026-06-12**. Источник истины по задачам — Notion БД «✅ Задачи до релиза (MVP)».
> Этот документ — рабочая карта: что сделано, что в работе, что строго на очереди.
> Связанные ADR/доки: `docs/adr/0003-customer-simulation.md`, `docs/adr/0004-stock-model-hybrid-sale-chance.md`, `docs/INPROGRESS/{Утро,Подготовка,Продажа,Итоги}.md`, `docs/INPROGRESS/FTUE — первый вход.md`, `docs/INPROGRESS/customer-simulation-decisions.md`, `docs/REFERENCE_TinyBookshop_Day.md`.

## Что закрыто

| Фаза / задача | Notion | Статус | Дата |
|---|---|---|---|
| Утро (Morning) | [link](https://app.notion.com/p/37b511859db3816b9df7e97e98e8ad02) | Ready | 2026-06-10 |
| Продажа (Sales) — real-time customer simulation (ADR-0003) | [link](https://app.notion.com/p/37b511859db3810c97a6e10d25845259) | Ready | 2026-06-11 |
| Итоги (Results) — summary + idempotent rewards + Next Day | [link](https://app.notion.com/p/37b511859db381e8abf4ec211c469e30) | Ready | 2026-06-12 |
| ADR-0003 customer simulation | `docs/adr/0003-customer-simulation.md` | Accepted | 2026-06-11 |
| ADR-0004 stock model (hybrid) + passive sale chance | `docs/adr/0004-stock-model-hybrid-sale-chance.md` | Accepted | 2026-06-12 |
| Book.Sell.API extraction (первая `.API` в проекте) | — | — | 2026-06-12 |

## Что технически уже работает «бесплатно»

- `Bootstrap.unity → Loading → GameplayScene` — стартовая цепочка.
- `DayProgressService.LoadAsync` — корректно различает «первый запуск» (создаёт дефолт) и «продолжение».
- Подгрузка `MorningScreenView` → переходы Sales (`SalesScreenView`) → Results (`ResultsScreenView`) → `Next Day` → reload сцены.
- Save / DI / Configs / Logging — стек поднят и используется.

То есть **минимальный цикл с дня 1 уже технически стартует с нуля** — без специального FTUE-кода. Не хватает контента «Подготовка» и стартового стока, чтобы цикл был полным и играбельным.

## Что переходит в работу прямо сейчас (по порядку)

Закреплённый владельцем порядок: **B → A → baseSaleChance**.

### B. Подготовка (Preparation)

- **Notion:** [Core loop — Подготовка](https://app.notion.com/p/37b511859db381f0ab9ad1739e927eb7) — статус Backlog → переходит в работу первой.
- **Что делает:** замыкает базовый цикл (Morning → Preparation → Sales → Results → Morning). Без неё игроку нечего делать между Итогами и Продажей следующего дня.
- **Почему первой:** без stocking-фазы стартовый пресет (A) некуда «налить», а `baseSaleChance` — нечего рассчитывать.
- **Опирается на:** ADR-0004 (per-genre-count + per-title гибрид) — stocking оперирует genre-grouped книгами + capacity (`In Shop X/40`).
- **Спека:** `docs/INPROGRESS/Подготовка.md`, план: `docs/INPROGRESS/PreparationFeatureImplementationPlan.md`.

### A. FTUE first-launch preset

- **Notion:** [FTUE — first-launch preset](https://app.notion.com/p/37d511859db381a6a90dd55eb2f4ba8a) — Backlog (только что заведено).
- **Что делает:** при первом запуске (нет модуля `day_progress` в Save) заливает стартовое состояние: золото (60 по рефу), книги по жанрам (Fantasy 5, Crime 5, Travel 3, Drama 6, Kids 2, Classic 3, Fact 3 — пример из TB).
- **Почему после B:** Подготовка определяет, **куда** льётся пресет (структура стока: per-genre-count в storage; capacity на полке). Без модели стока пресет некуда писать.
- **Опирается на:** ADR-0004, фаза Подготовки.

### baseSaleChance (вероятностный пассив + декор)

- **Notion:** [Sales — пассивные через baseSaleChance](https://app.notion.com/p/37c511859db381bf89e6f89b7e1ce95a) — Backlog.
- **Что делает:** переход пассивных продаж с детерминированного матча (текущая модель) на вероятностную (`baseSaleChance × locationMod × decorMod → roll → weighted-random by RarityWeight`). Возвращает прогрессионный крючок TB: «оптимизируй декор/полку ради sale chance».
- **Почему третьим:** нужен реальный сток (A) и место для контента (B), иначе формула применяется к нулю.
- **Опирается на:** ADR-0004 (модель + двухстадийная логика зафиксированы).

После этих трёх шагов **core loop полностью играбелен** с TB-like ощущением (sale chance, декор-эффекты, прогрессия через стокинг).

## Что отложено / orthogonal

| Задача | Notion | Почему ждёт |
|---|---|---|
| Intro / splash screen | [link](https://app.notion.com/p/37d511859db381898368ef505d8dc61f) | Ждёт арт; ортогонально core loop. Не блокер. |
| Logging per-channel (мутить каналы Loading/Configs/Gameplay) | [link](https://app.notion.com/p/37c511859db381a0b4a5cea3a4163039) | Инфра; не блокер MVP. |
| Tutorial HUD system (текст-стрелки, гейтинг по клику) | не заведено по решению владельца | Понадобится для FTUE и газеты, но пока не оформляем. |
| Sales↔Results restart routing (`DayPhase` в Book.Sell.API) | известное ограничение Results | Идемпотентность держится, UX-edge case; чинится мелкой правкой позже. |
| Скриптованный день 1 (важный персонаж, предсказуемые исходы) | не заведено | Делается **после** A+B+baseSaleChance, когда инфра готова. См. `FTUE — первый вход.md`. |
| Газета между днями + рестокинг в Classifieds | не заведено | Часть будущего «утреннего» хаба. Зависит от модели стока. |
| Анти-чит / server-side bounds | задокументировано в ADR-0003 | Отложено до релиза, MVP не нужно. |

## Известные ограничения текущего кода

1. **Restart посреди Results** возвращает игрока в Sales (не в Results). Идемпотентность не страдает (`results.applied_rewards` модуль), только UX. Чинится выносом `DayPhase` enum в `Book.Sell.API` + `DayProgressService.SetPhaseAsync(Results)` при открытии Results.
2. **Декор пока заглушка** — `DecorIds` пробрасываются в `SalesSessionSetup`, но в scoring и в пассив не влияют. Подключение — в задаче `baseSaleChance`.
3. **Capacity Подготовки захардкожена** — `PreparationSessionService` использует константы `DefaultMinDailyBooks = 1` и `DefaultDailyBookSlots = 12` (см. `Assets/Game/Features/Preparation/Services/PreparationSessionService.cs`). По спеке (`docs/INPROGRESS/Подготовка.md`, раздел «Лимиты») это должны быть параметры состояния игрока (`CurrentBookCapacity`), растущие через улучшения лавки — диапазон 12–20. **Не оставлять как константы** на этапе экономики/прогрессии: миграция в `EconomyConfig` / `DayProgressState.CurrentBookCapacity` — отдельная задача (вместе с `baseSaleChance`).

## Что у владельца следующее по моей рекомендации

1. **Подготовка (B)** — начнём с планирования (EnterPlanMode), потому что туда заходит и модель стока ADR-0004, и UI стокинга, и capacity, и storage/shop split.
2. После завершения B — мелкая задача **A** (стартовый пресет) на 30 минут.
3. После A — **baseSaleChance** (по утверждённому ADR-0004).
4. Параллельно по готовности арта — **Intro**.
