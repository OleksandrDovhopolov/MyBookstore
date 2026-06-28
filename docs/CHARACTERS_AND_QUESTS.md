# Characters and Quest Seeds

Reference-документ по персонажам Tiny Bookshop и их квестовым зацепкам для MyBookstore.

Функционал `Characters` **не входит в текущую итерацию**. Текущая итерация квестов (`Game.Quest`) должна работать без персонажей: `characterId` в `QuestChain`/`QuestConfig` остаётся nullable. В следующей итерации `Characters` сможет стать владельцем личных цепочек, shop-modifiers, memory-прогресса и появлений NPC.

Источники:

- [Tiny Bookshop Wiki / Characters](https://tiny-bookshop.fandom.com/wiki/Characters)
- [Tilde](https://tiny-bookshop.fandom.com/wiki/Tilde)
- [Anne](https://tiny-bookshop.fandom.com/wiki/Anne)
- [Fern](https://tiny-bookshop.fandom.com/wiki/Fern)
- [Walt](https://tiny-bookshop.fandom.com/wiki/Walt)
- [Klaus](https://tiny-bookshop.fandom.com/wiki/Klaus)
- [Maryam](https://tiny-bookshop.fandom.com/wiki/Maryam)
- [Moira](https://tiny-bookshop.fandom.com/wiki/Moira)
- [Harper](https://tiny-bookshop.fandom.com/wiki/Harper)

---

## 1. Вывод для архитектуры

В Tiny Bookshop персонажи устроены как связка:

- профиль персонажа: имя, роль, день рождения, локации появления;
- жанровые предпочтения;
- временный shop modifier, пока персонаж находится в магазине;
- набор `Memories` — фактически личные квестовые milestones с условиями;
- связи между персонажами, которые открывают совместные цепочки.

Для MyBookstore это значит:

- `Game.Quest` не должен ждать готовой системы персонажей;
- `QuestConfig.CharacterId` и `QuestChain.CharacterId` должны быть optional;
- `Characters` в следующей итерации сможет подключиться поверх квестов, не меняя модель задач;
- `Memories` лучше моделировать как завершённые квесты/цепочки или как отдельный слой над `Game.Quest`;
- character modifiers должны быть отдельными эффектами, похожими на temporary `QuestWorldEffect`.

---

## 2. Будущий `CharacterConfig`

Черновая форма данных:

```csharp
CharacterConfig
{
    string Id;
    string DisplayNameKey;
    string OccupationKey;
    string Birthday; // "Spring 18" / future calendar value
    string[] AppearsAtLocationIds;
    string[] FavouriteGenreIds;
    CharacterRelationConfig[] Relations;
    CharacterModifierConfig[] ShopModifiers;
    string[] QuestChainIds;
    string[] MemoryQuestIds;
}
```

`Characters` не должен считать условия сам. Условия появления, memories и квестов должны идти через существующий `Game.Conditions` (`ICondition`, `ConditionResult`, `IConditionFactory`) и `Game.Quest`.

---

## 3. Персонажи Tiny Bookshop

| Character | Role | Appears in | Favourite genres | Shop modifier / hook |
|---|---|---|---|---|
| `tilde` | retired bookshop owner | Waterfront Square, Flea Market | Classic, Crime | boosts Classic/Crime sale chance while present |
| `anne` | plant science student | Far Beach | Drama, Fantasy | boosts Drama sale chance while present |
| `fern` | journalist for Bookstonbury Review | Waterfront Square | Fantasy, Travel | boosts Fact sale chance while present |
| `walt` | sailor / shop owner | Waterfront Square | Travel, Classic | shop/vendor and tourist hooks |
| `klaus` | musician | Méga Marché | Fact, Travel | boosts all book sale chance while present |
| `maryam` | cafe owner | Café Liberté | Classic, Fact | adds money per sale while present |
| `moira` | student / cafe manager / spooky hooks | Lighthouse | Travel, Crime | boosts Fantasy and spooky effects while present |
| `harper` | child explorer / sandcastle architect | Far Beach | Fantasy, Fact | boosts Fact/Fantasy sale chance while present |

---

## 4. Quest Seeds by Character

### Tilde

Narrative role: бывшая владелица книжного и местный авторитет. Хороший кандидат на early-game mentor chain.

Quest seeds:

- `delivery_for_tilde`: забрать и доставить коробки Тильды на Flea Market.
- `visiting_hours`: принести настольную игру в Hospital.
- `a_nose_for_business`: вступить/помочь локальной бизнес-ассоциации.
- `the_peoples_bookseller`: получить несколько golden memories с друзьями.
- `all_the_gear_and_no_idea`: купить 4 полки.
- `a_worthy_heir`: завершить несколько предыдущих memories.
- `almost_flying_solo`: открыть storefront bookshop.

MyBookstore use:

- tutorial/mentor questline;
- unlock полок и storefront;
- permanent effect на доверие/репутацию магазина.

### Anne

Narrative role: студентка-ботаник, хорошо связывает квесты с декором-растениями, погодой и локациями.

Quest seeds:

- `villose_pitcher_plant`: экипировать растение в городских локациях.
- `bleeding_heart`: экипировать саженец Anne в дождливые дни.
- `bane_of_the_wolf`: экипировать растение в coastal locations.
- `language_of_flowers`: экипировать 7 растений.
- `annes_research_project`: помочь создать необычные мутации.
- `annes_graduation`: посетить выпуск Anne в University.

MyBookstore use:

- первая weather-driven цепочка через `weatherIs`;
- decor condition `decorEquipped`;
- future `plant`/`van visual upgrade` reward.

### Fern

Narrative role: журналист, газета, рукописи и печатный станок.

Quest seeds:

- `patron_of_print`: собрать 300 donations.
- `stories_of_bookstonbury`: собрать 3 manuscripts.
- `its_all_about_popular`: посетить все clubs в Rye Park.
- `fresh_off_the_presses`: напечатать что-то с Fern в University.
- `telling_stories`: использовать printing press.
- `spread_the_written_word`: порекомендовать 7 hand-printed books.

MyBookstore use:

- связка `Newspaper` + `Quest`;
- квесты на рекомендации/продажи особых книг;
- unlock печатного станка или newspaper feature.

### Walt

Narrative role: моряк, продавец старых вещей, туристы и Waterfront.

Quest seeds:

- `clearance_sale`: купить 7 старых предметов Walt.
- `hot_new_deals`: продать 30 книг за день на Waterfront.
- `spin_a_yarn`: вдохновить Walt на письмо.
- `the_cruise_ship_must_go`: отпугнуть туристов через Raw Fish на Waterfront во время туристического сезона.
- `treasured_shop`: превратить лавку Walt в новое место для моряков и рыбаков.

MyBookstore use:

- prerequisite для `soldGenreAtLocation` / `soldAnyAtLocationToday`;
- shop/vendor unlock;
- location world state для Waterfront.

### Klaus

Narrative role: музыкант, группа, постеры, концерт и ремонт фургона.

Relations:

- uncle of Harper;
- bandmate of Moira.

Quest seeds:

- `album_for_the_ages`: поговорить с Klaus 3 раза, пока он не вдохновится.
- `going_stage_diving`: найти сцену для группы.
- `calling_all_fans`: продвинуть Silens Libri через постер до 100 fans.
- `concert_coordinator`: помочь группе найти аудиторию и сцену.
- `too_loud_for_the_library`: посетить концерт Silens Libri.
- `rusty_proposition`: принести детали для ремонта фургона Klaus.
- `goodbye_klaus`: попрощаться с Klaus на Waterfront Square.

MyBookstore use:

- social/progression questline;
- poster/decor-as-condition;
- audience counter as condition;
- van repair chain.

### Maryam

Narrative role: владелица Café Liberté, бизнес-ассоциация и семейная ветка с Moira.

Quest seeds:

- `emotional_support`: поддержать Maryam в уязвимый момент.
- `a_course_completed`: вступить/закончить B.L.A.B.L.A.
- `a_family_matter`: помирить Maryam и Moira.

MyBookstore use:

- Cafe questline;
- money-per-sale temporary modifier;
- relationship quest between two characters.

### Moira

Narrative role: Lighthouse, spooky setup, Winter Market, связь с Maryam и Klaus.

Quest seeds:

- `whos_afraid_of_books`: посетить Fear Fest со spooky setup.
- `a_chat_and_a_cuppa`: поговорить с Moira на Winter Market.
- `secret_seventh`: попросить помощи у business owners и найти lost rule из B.L.A.B.L.A. compendium.
- `family_matters`: помирить Maryam и Moira.

MyBookstore use:

- Lighthouse/spooky questline;
- `decorTagEquipped(spooky)` condition;
- shared quest chain with Maryam;
- future seasonal event once seasons exist.

### Harper

Narrative role: Far Beach, исследование, песчаный замок, cave и St. Bookston crest.

Relations:

- niece of Klaus.

Quest seeds:

- `on_seashell_varieties`: собрать 12 seashells.
- `sandcastle_architect`: построить/завершить sandcastle designs.
- `mysterious_noises`: исследовать шумы из Cave на Far Beach.
- `restore_the_crest`: найти 4 fragments of St. Bookston's family crest.
- `one_for_the_history_books`: восстановить crest и посетить University вместе.

MyBookstore use:

- первая большая Far Beach chain;
- permanent state: построенный замок;
- unlock Cave;
- global collection quest: 4 crest fragments.

---

## 5. Character Modifiers

Character modifier — временный эффект, активный пока персонаж находится в магазине/локации.

Примеры из Tiny Bookshop:

| Character | Modifier candidate |
|---|---|
| Tilde | `SaleChance(Classic, +3%)`, `SaleChance(Crime, +3%)` |
| Anne | `SaleChance(Drama, +5%)` |
| Fern | `SaleChance(Fact, +5%)` |
| Klaus | `SaleChance(AllBooks, +2%)` |
| Maryam | `MoneyPerSale(+1)` |
| Moira | `SaleChance(Fantasy, +2%)`, `SpookyEffect(+50%)` |
| Harper | `SaleChance(Fact, +3%)`, `SaleChance(Fantasy, +3%)` |

Для MyBookstore это не должно жить внутри `Game.Quest`. Возможный владелец — будущая фича `Characters`, а `Book.Sell`/`SalesStats` должны уметь читать активные modifiers через API.

---

## 6. Связь с `Game.Quest`

До появления `Characters`:

```text
QuestChain.CharacterId = null
QuestConfig.CharacterId = null
```

После появления `Characters`:

```text
CharacterConfig.QuestChainIds -> Game.Quest
CharacterConfig.MemoryQuestIds -> Game.Quest
Character presence -> CharacterModifierProvider -> Sales systems
```

`Game.Quest` остаётся владельцем:

- состояний квестов;
- задач;
- condition trees;
- наград;
- permanent world effects;
- переходов между квестами.

`Characters` становится владельцем:

- профиля персонажа;
- появления персонажа;
- отношений между персонажами;
- временных shop modifiers;
- привязки личных цепочек и memories к конкретному персонажу.

---

## 7. Квесты, которые стоит заложить в backlog

| Chain | Owner later | Core conditions | Permanent / reward |
|---|---|---|---|
| `far_beach_sandcastle` | Harper | visit Far Beach, sell Fantasy, collect donations, one-day sales challenge | built sandcastle, +clients on Far Beach, unlock Cave |
| `far_beach_cave_crest` | Harper | unlock Cave, collect 4 crest fragments, visit University | global exploration completion |
| `anne_botanical_research` | Anne | equip plants, rainy day, coastal/inner-city locations, collect/equip 7 plants | plant decor, botanical visual upgrade |
| `fern_printing_press` | Fern | collect manuscripts, use printing press, recommend printed books | unlock printing/newspaper content |
| `walt_waterfront_shop` | Walt | buy old items, sell 30 books in a day at Waterfront, manipulate tourist flow | Walt shop upgrade / Waterfront state |
| `klaus_band_concert` | Klaus + Moira | talk count, poster promotion, audience counter, stage location | concert event, fan/customer archetype |
| `maryam_moira_family` | Maryam + Moira | dialogue milestones, business-owner help, shared relationship state | reconciled family state, cafe modifier |
| `moira_lighthouse_spooky` | Moira | spooky setup, Lighthouse visit, winter/festival hook later | spooky effects / Lighthouse story state |

---

## 8. Implementation Order

1. Current iteration: keep `Game.Quest` character-agnostic (`characterId = null`).
2. Add `Game.Quest` support for `CharacterId` fields in configs/save as passive data only.
3. Next iteration: create `Characters` feature and `Characters.API`.
4. Move character profile data into configs.
5. Add character presence/spawn rules and temporary modifiers.
6. Link character memories to `Game.Quest` chains.
7. Add UI surfaces: character journal, memories list, relationship/chain progress.
