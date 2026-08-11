# Journal Window — окно журнала (5 вкладок)

> Status: historical specification. Current implementation lives in `Assets/Game/Features/Journal/UI` / `Game.Journal.UI`; the `Memories`, `Places`, `Objects`, `People`, and `Quests` tabs already exist in code, and some blockers below are closed.
> Date: 2026-08-02.
> Scope: одно окно-справочник по персонажам, воспоминаниям, локациям, декору и квестам.
> Референс табов из другого проекта: `C:\Projects\bigmerge\MD\...\ShopPage\ShopPageView_Tabs.md`.

> Update 2026-08-03: `CharacterMemoryConfig.Order`, `CharacterJournalMemory.CharacterId/Order`, `PhotoKey`, `FavoriteGenres`, `HiddenInJournal`, manual `TryUnlockMemory`, `UnlockedAtStart`, and the unseen counter are implemented. Memories show only unlocked entries; hidden characters are omitted from People, but their memories remain in the feed. Quest-linked memories unlock strictly on `QuestState.Awarded`, not on `ReadyToAward`.

## Цель

Окно — справочник: игрок открывает его, чтобы вспомнить, кто есть кто, где что спрашивают и какие бонусы
у него сейчас работают. Все вкладки **read-only**, кроме квестов (там уже есть клейм награды).

Окно уже существует и открывается: `[Window("JournalWindow", WindowType.Page)]`
([JournalWindow.cs:14](../../Assets/Game/Features/Characters/UI/JournalWindow.cs)), префаб лежит в
`Assets/Game/Features/Characters/UI/Prefabs/JournalWindow.prefab`, кнопка — 
[GameplaySceneController.cs:406](../../Assets/Game/UI/GameplayScene/GameplaySceneController.cs).
Сейчас это одностраничное окно со списком персонажей. Задача — превратить его в пятивкладочное.

---

## 1. Табы

### Enum

```csharp
public enum JournalTab
{
    Memories = 0,
    Places   = 1,
    Objects  = 2,
    People   = 3,
    Quests   = 4,
}
```

Согласовано: enum, а не данные. Точка стабильная — новая вкладка означает пересборку префаба, так что
динамический список не нужен.

### Механика переключения

**В проекте табов сейчас нет вообще** — `grep ToggleGroup` по `Assets/Game` даёт 0 совпадений. Инфраструктуру
строим с нуля.

Референсный `ShopPageView` из bigmerge использует ленту табов: все страницы лежат в ряд в одном контейнере,
переключение — плавный подъезд контейнера по X с лерпом, `ScrollRect.enabled` включён только у активной
страницы. Плюс `SerializedDictionary<TabType, …>` и `ExtendedScrollRect` — **ни того, ни другого в нашем
проекте нет**.

**Рекомендация: не копировать карусель.** В шопе она была нужна, потому что вкладки там однородные (витрины
одного вида) и между ними свайпают. У нас пять разнородных страниц, свайпа нет — карусель даст лишнюю
анимацию, лишний контейнер и требование одинаковой ширины страниц. Берём простое `SetActive` страниц:

```
JournalWindowView
├── _tabButtons : JournalTabButton[5]   (порядок = порядок enum, индекс = (int)JournalTab)
├── _tabPages   : GameObject[5]         (то же)
└── SelectTab(JournalTab tab)
      ├── все _tabPages[i].SetActive(i == (int)tab)
      ├── _tabButtons[i].SetSelected(i == (int)tab)
      └── TabSelected?.Invoke(tab)  → контроллер рендерит страницу
```

Что берём из референса и что стоит сохранить:
- взаимоисключаемость через Unity `ToggleGroup` в префабе, а не через код;
- контент грузится **не лениво** — при открытии окна рендерятся все пять страниц, чтобы переключение было
  мгновенным (в шопе так же: `UpdateWindow` заполняет все табы заранее);
- сброс `verticalNormalizedPosition = 1f` у страницы при уходе с неё.

Массив вместо `SerializedDictionary` — потому что enum плотный и начинается с 0, индекс = значение.

### Открытые вопросы по табам

- Запоминается ли последняя открытая вкладка между открытиями окна? (в шопе — да, `_activeTab` живёт в
  контроллере)
- Нужно ли открывать окно сразу на конкретной вкладке (например, из туториала или по клику «новое
  воспоминание»)? Если да — нужен `JournalWindowArgs` с целевой вкладкой, по образцу
  [LocationWindowArgs.cs](../../Assets/Game/Features/Location/UI/LocationWindowArgs.cs).
- На скринах есть стрелка «вперёд» справа внизу (скрины 1 и 3). Что она делает — листает вкладки, листает
  страницы внутри вкладки, или это что-то третье? **Не описано.**

---

## 2. Вкладка Memories

Вертикальный скролл воспоминаний, новое сверху. Минимум данных: `Title`, описание, ключ спрайта для
Addressables (скрин 2).

### Что уже есть

Модель воспоминания описана полностью — [CharacterJournalEntry.cs:23](../../Assets/Game/Features/Characters/API/CharacterJournalEntry.cs):

```csharp
public sealed class CharacterJournalMemory
{
    public string MemoryId;  public bool Unlocked;  public bool IsGolden;
    public string TitleKey;  public string DescriptionKey;  public string PhotoKey;
    public string LinkedQuestId;  public QuestState LinkedQuestState;
}
```

Есть и UI-модель [JournalMemoryItemModel.cs](../../Assets/Game/Features/Characters/UI/JournalMemoryItemModel.cs) —
правда, **без `PhotoKey`**, хотя в API он есть. Надо добавить.

### Чего не хватает — блокеры

**M1. Данных нет вообще.** В `Assets/StreamingAssets/Configs/characters.json` четыре персонажа
(eddi, millie, tara, captain) и **ни одного воспоминания** — строка `memories` в файле не встречается ни разу.
Вкладка сегодня отрендерит пустой список. Нужен контент от геймдизайна: id, titleKey, descriptionKey,
photoKey и привязка к квесту (`questId` или `questChainId`).

**M2. Нет порядка «новое сверху».** Ни `CharacterMemoryConfig`, ни `CharacterJournalMemory`, ни
`ICharacterMemory` не хранят времени разблокировки или sort-order. Факт разблокировки выводится из состояния
квеста **на чтение** (`ICharactersService` §Stage 1 is event-free), «когда разблокировали» нигде не
сохраняется. То есть требование «самое новое отображается вначале» сейчас нереализуемо. Развилка:

- (а) сохранять в сейв `UnlockedAtDay` (номер игрового дня) в момент разблокировки — честное «новое сверху»,
  но требует перехода на событийную модель (см. M4) и миграции сейва;
- (б) авторский `order` в `CharacterMemoryConfig` — сортировка по замыслу дизайнера, без сейва; «новое»
  становится приблизительным;
- (в) сортировать по порядку квестов в цепочке — не требует новых полей, но ломается на параллельных ветках.

Рекомендация: **(б)** на первый заход, **(а)** — когда появится событийная модель.

**M3. Модель вложенная, а вкладке нужен плоский список.** `CharacterJournalEntry.Memories` — на персонажа.
Для сквозной ленты нужен агрегат. Варианта два: добавить `IReadOnlyList<CharacterJournalMemory> GetAllMemories()`
в [ICharactersService](../../Assets/Game/Features/Characters/API/ICharactersService.cs) (правильнее — фича сама
собирает своё read-model, как уже делает `GetJournalEntry`), либо склеивать в UI-билдере. Берём первое.

**M4. Live-refresh не работает.** `CharacterDiscovered` / `MemoryUnlocked` объявлены, но по комментарию в
интерфейсе «Not raised in Stage 1». `JournalWindow.OnShowStart` на них подписывается вхолостую. Для
справочника терпимо (перерисовка на открытие), но открытому окну новое воспоминание не прилетит.

### Открытые вопросы

- Показывать ли **заблокированные** воспоминания заглушкой? На скрине 2 у «Знакомство с Милли» пустая рамка —
  похоже, что да. Нужно подтверждение и вид заглушки.
- Группировать ли по персонажам или сплошной лентой? Скрин 2 — сплошная лента.
- Фото слева/справа чередуется (скрин 2). Это два префаба строки или один с флагом?

---

## 3. Вкладка Places (локации)

Вертикальный скролл: id локации, спрайт, `demandGenres` (скрин 3).

### Что уже есть — переиспользуем целиком

- `IConfigsService.GetAll<LocationConfig>()` → `Id`, `DisplayName`, `DemandGenres`
  ([LocationConfig.cs:22](../../Assets/Game/Features/Configs/Models/LocationConfig.cs));
- спрайт локации грузится **по id локации**: `sprites.GetSpriteAsync(locationId, ct)` —
  [LocationRowView.cs:122](../../Assets/Game/Features/Location/UI/LocationRowView.cs);
- готовая ячейка жанра [LocationDemandGenreItemView.cs](../../Assets/Game/Features/Location/UI/LocationDemandGenreItemView.cs) —
  `Bind(BookGenre genre, Sprite icon)`, реализует `ICleanup`, ложится в `UIListPool`;
- парсинг строки жанра в enum — `BookGenreExtensions.TryParseGenre`, иконка грузится по `genre.ToConfigValue()`.

Фактически это упрощённая копия `LocationRowView` без кнопок Start/Unlock и без условий разблокировки.

### Чего не хватает

**P1. Не описано, что делать с заблокированными локациями.** Показывать все пять? Только разблокированные?
Заблокированные — с заглушкой вместо `demandGenres`? Данные для проверки есть —
`ILocationUnlockService.GetStatus(id)`.

**P2. Заголовок на скрине 3 — `LATEN-PARK`**, а `locations.json` содержит `id: loc_park` и `displayName`.
Показываем `DisplayName`? (в проекте нет локализации, см. блокер L1).

---

## 4. Вкладка Objects (установленный декор)

Сверху горизонтальный скролл установленного декора, снизу вертикальный скролл суммарных бонусов (скрин 4).

### Что уже есть

- `IDecorPlacementService.GetActiveDecorIds()` / `GetAllPlacements()` + событие `PlacementChanged`
  ([IDecorPlacementService.cs](../../Assets/Game/Features/Decor/API/IDecorPlacementService.cs)) — верхний скролл
  берётся отсюда напрямую;
- иконка декора грузится по его id через `IUiSpriteProvider` (см. `DecorInventoryCardView`);
- **готовая строка бонуса** [DecorBonusItemView.cs](../../Assets/Game/Features/Decor/UI/DecorBonusItemView.cs) —
  иконка жанра + текст + цветной процент, `Bind(genre, percent, color, sprites)`, `ICleanup`. Переиспользуем
  как есть;
- `ConfigBasedDecorModifierProvider.GetGenreMultiplier(genre, activeDecorIds)` — уже перемножает множители по
  всем активным декорам и клампит в `[0.1, 3.0]`.

### Чего не хватает — блокеры

**O1. Агрегатора «Total effects» нет.** `GetGenreMultiplier` считает **один жанр за вызов** и возвращает
множитель (`1.5`), а UI показывает список процентов (`+17% Chance Crime sale`). Нужен сервис уровня
`IDecorTotalEffectsProvider`, который вернёт список всех ненейтральных эффектов сразу. Он должен звать
существующий `GetGenreMultiplier` по каждому жанру, а не считать заново — иначе кламп `[0.1, 3.0]` разъедется
с тем, что реально применяется в продаже.

**O2. Формула «множитель → процент» не определена.** `1.5×` — это «+50%»? Тогда откуда `+17%` и `+2%` на
скрине? Похоже, там показан результат перемножения нескольких декоров, но `+2% Classic` не получается ни из
одного значения в `decors.json` (там `1.3`, `1.2`). Нужно однозначное правило и подтверждение, что скрин —
это макет, а не реальный расчёт.

**O3. Половины бонусов со скрина не существует.** В `DecorConfig` есть только `GenreMultipliers`,
`CustomerTrafficPercentDelta`, `VisitCostDelta`, `BasePrice`, `AtmosphereTags`. На скрине 4:
- `+3% Total tips` — **поля нет, механики нет**;
- `+3% Chance to buy additional book` — **поля нет, механики нет**.

Плюс `visitCostDelta` не заполнен ни у одного из 17 декоров в `decors.json`, а
`customerTrafficPercentDelta` — только у одного (`vintage_globe: 0.1`). То есть даже существующие эффекты
почти нечего показывать.

Решение: на первый заход показываем только то, что реально работает — жанровые множители и трафик. Строки
про чаевые и доп. книгу — отдельная задача (данные + механика + UI), не входит в это окно.

**O4. Иконки для нежанровых бонусов.** `DecorBonusItemView` грузит иконку **по имени жанра**
(`sprites.GetSpriteAsync(genre, ct)`). Для «+10% посетителей» жанра нет — нужен либо отдельный ключ спрайта,
либо второй `Bind`-оверлоад с готовым `Sprite` (он уже есть: `Bind(Sprite icon, …)`).

---

## 5. Вкладка People (персонажи)

Вертикальный скролл всех персонажей: спрайт (Addressables) + жанры, которые покупает (скрин 1).

### Что уже есть — почти всё

Это единственная вкладка с готовой реализацией:
- [JournalWindow.cs](../../Assets/Game/Features/Characters/UI/JournalWindow.cs) — контроллер,
- [JournalWindowView.cs](../../Assets/Game/Features/Characters/UI/JournalWindowView.cs) — `UIListPool<JournalCharacterRowView>`,
- [JournalCharacterRowView.cs](../../Assets/Game/Features/Characters/UI/JournalCharacterRowView.cs) — портрет по
  `PortraitKey` через `IUiSpriteProvider`, `_lockedPanel` для неоткрытых, отмена загрузки через `CancellationTokenSource`,
- [JournalCharactersViewModelBuilder.cs](../../Assets/Game/Features/Characters/UI/JournalCharactersViewModelBuilder.cs) —
  чистый маппер, покрыт тестами (`JournalCharactersViewModelBuilderTests`).

Данные заполнены: 4 персонажа с `portraitKey` и `favoriteGenres`.

### Чего не хватает

**C1. Жанров нет в цепочке моделей.** `CharacterConfig.FavoriteGenres` заполнен
(`eddi: [Fact, Travel]`, `millie: [Drama, Crime, Classic]`, …), но их не пробрасывает ни
`CharacterJournalEntry`, ни `JournalCharacterItemModel`, ни строка. Надо добавить поле в оба read-model и
поле-пул `UIListPool<LocationDemandGenreItemView>` в строку (ячейка жанра переиспользуется из Location).

**C2. Скрин 1 показывает больше, чем есть.** «День рождения?», «Родственники и Друзья?», «Чем занимался?» —
это, судя по знакам вопроса, будущие разблокируемые факты о персонаже. Ни полей, ни механики. По
договорённости сейчас **только спрайт и жанры** — остальное фиксируем как будущую задачу.

**C3. Вложенная навигация.** На скрине 1 — карточка одного персонажа, а не список. То есть у вкладки, видимо,
два состояния: список → детали. **Не описано.** Сейчас реализован только список.

---

## 6. Вкладка Quests

Не описываем в этой итерации.

Важно знать: квесты — это уже **отдельное самостоятельное окно**
[QuestWindow.cs](../../Assets/Game/Features/Quest/UI/QuestWindow.cs) со своим `QuestWindowView`,
`QuestViewModelBuilder`, подпиской на шесть событий `IQuestsService` и логикой клейма награды с открытием
`RewardsWindow`. Встроить его пятой вкладкой — это не «добавить страницу», а перенос контроллера внутрь
чужого окна либо вложенное окно. Решать отдельно.

---

## 7. Сквозные блокеры

**L1. Локализации в проекте нет.** `grep ILocalization|LocalizationService|Localize` по `Assets/Game` — 0
совпадений. При этом конфиги хранят ключи (`character.eddi.name`), а
[JournalCharacterRowView.cs:30](../../Assets/Game/Features/Characters/UI/JournalCharacterRowView.cs) пишет их
в текст напрямую: `_nameLabel.text = model.DisplayNameKey`. На экран уйдёт `character.eddi.name`, а на скринах
человеческие имена. Затрагивает все пять вкладок. Развилка: заводить локализацию или временно класть в
конфиги готовые строки. **Решение нужно до вёрстки.**

**L2. Статусные тексты захардкожены по-русски** — там же, строки 33-34: `"Персонаж разблокирован"`.

**L3. Размер окна.** Все пять страниц живут одновременно в одном префабе — надо заранее договориться о
фиксированной области контента, иначе вёрстка страниц разъедется.

---

## 8. Порядок работ

1. **Решить L1** (локализация или plain-строки) — блокирует вёрстку.
2. **Инфраструктура табов**: `JournalTab` enum, `JournalTabButton`, перестройка `JournalWindowView` под
   5 страниц + `SelectTab`, `ToggleGroup` в префабе. Контроллер получает `TabSelected` и рендерит страницу.
3. **Вкладка People** — самая дешёвая, код уже есть: пробросить `FavoriteGenres` через
   `CharacterJournalEntry` → `JournalCharacterItemModel` → строку, добавить пул жанровых ячеек.
4. **Вкладка Places** — новый `JournalPlaceRowView` + билдер поверх `GetAll<LocationConfig>()`; ячейку жанра и
   загрузку спрайта по id берём из Location.
5. **Вкладка Objects** — `IDecorTotalEffectsProvider` (O1) поверх существующего
   `ConfigBasedDecorModifierProvider`, затем два скролла; строку бонуса переиспользуем.
6. **Вкладка Memories** — после решения M1 (контент) и M2 (порядок): `GetAllMemories()` в сервисе, плоский
   билдер, строка с фото.
7. **Вкладка Quests** — отдельной задачей.

Тесты: билдеры (`JournalCharactersViewModelBuilder`, новые для Places/Memories/Objects) — чистые POCO, идут в
существующие тестовые сборки по образцу `JournalCharactersViewModelBuilderTests`. Агрегатор бонусов (O1)
тестируется как `ConfigBasedDecorModifierProviderTests`.

---

## 9. Сводка «чего не хватает»

| # | Блокер | Что нужно |
|---|---|---|
| L1 | Локализации нет, в текст уходят ключи | решение: сервис или plain-строки в конфигах |
| M1 | `characters.json` — 0 воспоминаний | контент от GD |
| M2 | нет времени/порядка разблокировки | поле `order` в конфиге или `UnlockedAtDay` в сейве |
| M3 | нет плоского списка воспоминаний | `GetAllMemories()` в `ICharactersService` |
| M4 | события `MemoryUnlocked` не стреляют | Stage 2 характеров (можно отложить) |
| O1 | нет агрегатора суммарных бонусов | новый провайдер поверх `GetGenreMultiplier` |
| O2 | не задана формула «множитель → процент» | решение GD |
| O3 | «tips» и «additional book» не существуют | поля + механика, отдельная задача |
| C1 | жанры персонажа не доходят до UI | поле в `CharacterJournalEntry` и item-model |
| C3 | список vs карточка персонажа | уточнить навигацию |
| P1 | судьба заблокированных локаций | уточнить |
| — | стрелка «вперёд» на скринах | уточнить назначение |
| — | вкладка Quests: перенос существующего окна | отдельная задача |
