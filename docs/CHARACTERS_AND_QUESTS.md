# Characters and Quests

Как персонажи (`Game.Characters`) связаны с квестами (`Game.Quest`) в MyBookstore. Архитектура персонажей подробно описана в [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md), система квестов — в [QUESTS.md](QUESTS.md). Этот документ — про **точку связи** и про то, как авторить character-привязанные цепочки.

> Все `Character_NN`, `quest_NN`, `chain_NN`, `memory_NN` ниже — **примеры-плейсхолдеры**. Реальные id задаются в `characters.json` / `quests.json`.

> **Статус:** `Game.Characters` реализован (Этап 1–3). `Game.Quest` про персонажей ничего не знает — связь идёт через пассивное поле `CharacterId` и обратный индекс на стороне `Characters`.

---

## 1. Принцип связи

- Прогресс истории персонажа = квесты в `Game.Quest`. Персонаж их **не владеет**, а проецирует.
- `QuestConfig.CharacterId` / `IQuest.CharacterId` — пассивное nullable-поле (метаданные). `Game.Quest` его не интерпретирует.
- Маршрутизация «квест → персонаж» живёт в `Game.Characters`: сервис строит обратный индекс `questId/chainId → characterId` из `CharacterConfig` (discovery-связи + memory-квесты) и читает только generic `IQuestsService.GetQuestState(id)` / `GetChain(id)` + события `QuestStarted`/`QuestAwarded`.
- `Game.Quest` остаётся переносимым: никаких character-aware методов в его API.

---

## 2. Что чем владеет

`Game.Quest`: состояния квестов и задач, condition-деревья, прогресс, награды, permanent world-effects, переходы `NextQuestIds`, события.

`Game.Characters`: профиль персонажа (из конфига), persisted `Discovered`, леджер открытых memory, связка `characterId ↔ quests/chains`, read-model для Journal.

Подробные правила и read-models — в [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) §3, §6–§9.

---

## 3. Discovery и Memories

- **Discovery**: персонаж открыт, когда стартовал (`!= Pending`) любой из `CharacterConfig.DiscoveryQuestIds`/`DiscoveryQuestChainIds` или любой memory-квест. Это покрывает intro/dialogue-квесты без memory.
- **Memory** привязывается к квесту (`QuestId`) или цепочке (`QuestChainId`) и открывается, когда квест/финал цепочки переходит в `Awarded`.
- read-model `Unlocked = questDerived || ledger`; `Discovered` — persisted-флаг. События `CharacterDiscovered`/`MemoryUnlocked` фаярятся один раз (леджер + reconcile-on-load без переигрывания).
- **Golden memory** — флаг `IsGolden` (значимость для UI). Награды (в т.ч. предметы) выдаёт стандартный quest-reward flow, не `Characters`.

---

## 4. Авторинг character-цепочки

1. Завести квесты/цепочку в `quests.json`; проставить `CharacterId` (метка владельца) на квестах цепочки.
2. В `characters.json` у персонажа описать:
   - `DiscoveryQuestIds` / `DiscoveryQuestChainIds` — чем персонаж «открывается» (часто это intro-квест или первая цепочка);
   - `Memories[]` — какие milestones показывать, с привязкой к `QuestId` или `QuestChainId` и флагом `IsGolden`.
3. Условия активации/завершения квестов авторятся в `Game.Conditions` (см. [QUESTS.md](QUESTS.md) §11) — `Characters` условий не считает.

Пример (плейсхолдеры):

```json
// characters.json
{
  "id": "character_01",
  "displayNameKey": "character.character_01.name",
  "portraitKey": "portrait_character_01",
  "discoveryQuestIds": ["quest_intro_01"],
  "memories": [
    { "id": "memory_01", "questChainId": "chain_01", "isGolden": false },
    { "id": "memory_01_golden", "questId": "quest_01_finale", "isGolden": true }
  ]
}
```

```text
// quests.json (схематично)
chain_01 = [ quest_01_a, quest_01_b, quest_01_finale ]   // у всех CharacterId = "character_01"
```

---

## 5. Архетипы цепочек (иллюстрация)

Без привязки к конкретному контенту — типовые «формы» character-цепочек и какие системы они задействуют:

| Архетип | Типовые условия | Постоянный эффект/награда |
|---|---|---|
| mentor / endgame | доставки, серия milestones, финальный «наследник» | unlock контента, репутация магазина |
| декор/погода-зависимая ветка | экипировать декор, дождливые/ясные дни, локации | декор-награда, визуальный апгрейд |
| публикации/печать | собрать предметы, рекомендации, печать | unlock фичи публикаций |
| vendor-локация | покупки у NPC, продажи на локации, дневной челлендж | улучшение лавки / world-state локации |
| коллекция-исследование | собрать N фрагментов, открыть подлокацию | глобальное completion, unlock локации |

Замечания:

- У персонажа обычно одна golden memory в конце цепочки.
- «Общая memory у двух персонажей» — отложенный shared-memory кейс (одна memory сейчас принадлежит одному персонажу).
- Кросс-персонажные мета-гейты («N golden memory сделано») авторятся как условия над состоянием квестов — см. [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) §14 (отдельный будущий шаг).

---

## 6. Modifiers / presence (отложено)

Слой «эффекты от присутствия персонажа в локации» (`ICharacterPresenceService` / `ICharacterModifierProvider`) — **не реализован** и ждёт дизайн-решения. Спецификация и точки интеграции (по образцу `IDecorModifierProvider` в `Book.Sell.API`) — в [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) §15.

---

## 7. Порядок развития

1. ✅ `Game.Quest` остаётся character-agnostic (`CharacterId` — пассивные данные).
2. ✅ Фича `Characters` + `Characters.API`: профиль, discovery, memories поверх квестов.
3. ✅ Journal-классы (секция Characters).
4. ⏸ presence/modifiers (после дизайн-решения).
5. ⏸ кросс-персонажные condition-фабрики, shared memory.
