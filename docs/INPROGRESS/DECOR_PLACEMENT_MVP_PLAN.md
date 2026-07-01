# Decor Placement MVP — план реализации

> Статус выполнения: **[CODE]-шаги 0–8 сделаны** (save-hook fix, `DecorSlotAnchorView`,
> `DecorInventoryCardView`, переработка view+контроллера на decor-first, remove+HUD, info popup,
> DoTween-анимации на анкере, звук place). Остаётся **[EDITOR]-работа**: Шаг 9 (авторинг префаба)
> и Шаг 10 (спрайты/addressables по decor id), плюс опциональная проверка слот↔якорь в валидаторе.
> Карточки инвентаря создаются через `UIListPool<DecorInventoryCardView>` (пул на view).
> Исходная спецификация: `docs/INPROGRESS/DECOR_PLACEMENT_MVP_PROCESS.md`.

## Context

Цель — превратить debug/list-окно `DecorPlacementWindow` в визуальное MVP расстановки декора:
игрок видит комнату с точками, выбирает декор из нижней панели, ставит кликом,
снимает, смотрит info, и после перезахода расстановка восстанавливается.

Доменный слой уже готов и проверен по коду: `IDecorPlacementService`
(`PlaceAsync`/`UnplaceAsync`/`GetAllPlacements`/`GetDecorInSlot`/`GetActiveDecorIds`/
`ClearAllAsync` + событие `PlacementChanged`) с полной валидацией в
`DecorPlacementService.PlaceAsync`. UI не хранит своё состояние — только рендерит
сервис и шлёт команды. Модель view-agnostic (строковые `slotId`/`decorId`), поэтому
делаем размещение на UI-слое, без сцены/world-space.

**Решения, согласованные с пользователем:**
- Две декорации MVP: **`vintage_globe` + `coffee_pot`** (оба `Standing`/`Small`, уже
  есть в `decors.json`, влезают в слоты `cart_table_1..3` = `Standing`/`Medium`).
  Новый конфиг `cactus` из дока НЕ создаём.
- Гейт `ConfirmDialog` для негативного декора **оставляем проведённым** в новом
  click-to-place flow (для MVP-предметов не срабатывает — оба позитивные, но защищает
  будущий негативный декор).

**Что уже готово и НЕ требует работы:**
- Чит выдачи в инвентарь: `DecorationCheatModule` уже авто-генерирует кнопки
  «Add Vintage Globe» / «Add Coffee Pot» (`_inventory.AddAsync(id, Decor, 1, ct)`).
  Работы ноль — только проверить в рантайме.
- Конфиги обеих декораций и слотов уже валидны.

## Разделение работ: код vs Unity Editor

Значительная часть MVP — авторинг префаба и ассетов в Unity Editor, что агент
кодом сделать не может. Ниже явно помечено:
- **[CODE]** — пишу я (C#-скрипты).
- **[EDITOR]** — делает пользователь в Unity Editor (префаб-иерархия, якоря,
  спрайты/addressables, назначение serialized-полей).

---

## Шаг 0 — [CODE] Фикс save-hook (первым, блокер теста сохранения) ✅ СДЕЛАНО

Известный P1-баг (см. TODO в `DecorVContainerBindings.RegisterDecor`,
`Assets/Game/Core/Installers/Features/DecorVContainerBindings.cs:22-30`):
`DecorPlacementService` регистрирует save hook только в конструкторе
(`save.RegisterHook(this)`), но резолвится лениво — ничто не строит сервис до
`SaveDataLoadOperation.LoadAsync`. Итог: на втором запуске `AfterLoadAsync` не
вызывается, расстановка не грузится, следующее сохранение затирает старую.

**Фикс — по существующему паттерну проекта.** `Bootstrap.Construct`
(`Assets/Game/Core/Installers/Bootstrap/Bootstrap.cs:80-111`) уже форс-конструирует
save-hook-сервисы, просто инжектя их (`IQuestsService`, `IInventoryService`,
`ILocationUnlockService` — см. комментарий на строках 63-71). Добавить туда
`IDecorPlacementService`:
- новое поле рядом с `_quests`, в блоке «Injected to force construction»;
- параметр в сигнатуру `Construct(...)`;
- присваивание в теле.

Убрать/обновить TODO-комментарий в `DecorVContainerBindings` — баг закрыт.

Файлы: `Bootstrap.cs`, `DecorVContainerBindings.cs`.

---

## Шаг 1 — [CODE] Новый компонент `DecorSlotAnchorView`

Новый файл `Assets/Game/Features/Decor/UI/DecorSlotAnchorView.cs` (MonoBehaviour).
Его ещё нет. Один экземпляр на слот, живёт в префабе, позиция/масштаб авторятся в
Editor. Связь с конфигом — по строковому `slotId`.

Serialized-поля:
- `string _slotId` — обязан совпадать с id из `bookshops.json` (`cart_table_1` и т.д.);
- `Button _markerButton` — пустой слот (pin/marker), кликается для размещения;
- `GameObject _highlight` — подсветка совместимого пустого слота;
- `Image _placedDecorImage` — визуал установленной декорации;
- `Button _placedButton` — клик по установленному декору (открывает HUD);
- (опц.) `GameObject _selectedOutline`.

Публичное API (view «тупой», логика — в контроллере):
- `string SlotId`;
- события/коллбэки `OnMarkerClicked`, `OnPlacedClicked` (Action);
- `SetEmpty()` — показать marker, скрыть decor image;
- `SetPlaced(Sprite sprite)` — показать decor image, скрыть/приглушить marker;
- `SetHighlighted(bool)` — вкл/выкл `_highlight` и интерактивность marker;
- `PlayPlaceTween()` / `PlayRemoveTween(Action onComplete)` — см. шаг 7.

Паттерн selected/highlight — как в `BookCardView` (`SetSelected` через `SetActive`
на highlight-GameObject), `Assets/Game/Features/BookSell/UI/BookCardView.cs`.

---

## Шаг 2 — [CODE] Карточка декора в нижней панели `DecorInventoryCardView`

Новый файл `Assets/Game/Features/Decor/UI/DecorInventoryCardView.cs`. Заменяет
текстовый `DecorInventoryRowView` на визуальную карточку.

Serialized-поля: `Image _icon`, `TextMeshProUGUI _nameLabel`, `Button _selectButton`,
`Button _infoButton`, `GameObject _selectedHighlight`, `GameObject _placedBadge`
(пометка «уже стоит»).

API: `Bind(DecorConfig, bool isPlaced)`, `SetSelected(bool)`, коллбэки
`OnSelect`/`OnInfo`. Иконку грузить через `IUiSpriteProvider.GetSpriteAsync(decorId, ct)`
— **по decor id, НЕ по `IconAddress`** (поле помечено TODO на удаление).

---

## Шаг 3 — [CODE] Переработка `DecorPlacementWindowView`

`Assets/Game/Features/Decor/UI/DecorPlacementWindowView.cs`. Убрать debug-поля
(summary/capHint/slotList/inventoryList-templates для текстовых строк). Добавить
serialized-ссылки под новую иерархию (см. шаг 9):
- `RectTransform _roomImageRect` — контейнер, повторяющий видимое изображение фона
  (для якоря слотов при aspect-fit);
- `DecorSlotAnchorView[] _slotAnchors` — все якоря из префаба;
- `Transform _inventoryItemsRoot` + `DecorInventoryCardView _cardTemplate`;
- `GameObject _selectedSlotHud` + `Button _replaceButton` + `Button _removeButton`;
- `GameObject _infoPopupRoot` + поля info-popup (icon/name/bonuses/desc/close) — либо
  отдельный маленький view-класс `DecorInfoPopupView`;
- `Button _closeButton`, (опц.) `AudioClip _placeClip`.

Экспонировать через публичные свойства (как в текущем view и `ClassicShopWindowView`).

---

## Шаг 4 — [CODE] Переработка контроллера `DecorPlacementWindow` (click-to-place)

`Assets/Game/Features/Decor/UI/DecorPlacementWindow.cs`. Сохранить каркас
(`WindowController<DecorPlacementWindowView>`, `[Inject] InjectServices`, подписка на
`PlacementChanged` + `inventory.Changed`, `_cts`, `CloseAsync`). Добавить в инжект
`IUiSpriteProvider`.

**Состояния окна** (enum): `Default`, `DecorSelected`, `PlacedSlotSelected`,
`InfoPopupOpen`. `ReplacePreview` — не в MVP.

**Инверсия модели: было slot-first, станет decor-first.** Удалить `OnPlaceClicked(slot)`
+ `PickFirstCompatibleUnplaced(slot)`. Новый flow:
1. `Render()` из сервиса: для каждого anchor — `GetDecorInSlot(slotId)`; пусто →
   `SetEmpty()`, занято → грузим спрайт по decorId и `SetPlaced(sprite)`. Нижняя панель
   — карточки из `inventory.GetByCategory(Decor)` с пометкой isPlaced.
2. Клик карточки → `DecorSelected`: `SetSelected` на карточке; подсветить **только
   совместимые пустые** слоты новым хелпером `GetCompatibleEmptySlots(decorId)`
   (для decorId вернуть anchors, где слот пуст, `PositionType` совпадает,
   `Size <= MaxSize`); остальные — `SetHighlighted(false)`/disabled. Повторный клик по
   выбранной карточке → отмена (`Default`).
3. Клик подсвеченного слота → `PlaceAsync(decorId, slotId, ct)`. Перед вызовом
   сохранить гейт `ConfirmDialog` при `HasNegativeEffect(config)` (перенести
   существующие `HasNegativeEffect`/`BuildNegativeWarning` из текущего файла). При
   успехе: `PlayPlaceTween`, звук (шаг 8), снять selected, выключить подсветку → `Default`.
   Провал → лог + возврат в понятное состояние (без error popup).

**Хелпер** `GetCompatibleEmptySlots(string decorId)` — новый, заменяет slot-first
`PickFirstCompatibleUnplaced`. Правила совместимости брать те же, что в
`PlaceAsync` (не дублировать доменную логику сверх необходимого).

---

## Шаг 5 — [CODE] Remove flow + HUD

- Клик по установленному декору (`OnPlacedClicked` из anchor) → `PlacedSlotSelected`:
  показать `_selectedSlotHud` рядом со слотом, кнопки `Remove` (активна) и `Replace`
  (видима, но disabled/без действия — MVP).
- `Remove` → `UnplaceAsync(slotId, ct)`; при успехе `PlayRemoveTween` → по завершении
  `SetEmpty()` (marker обратно). Предмет остаётся в инвентаре.
- HUD закрывается: клик вне него / закрытие окна / выбор карточки из нижней панели.

## Шаг 6 — [CODE] Info popup

- Кнопка `info` на карточке → `InfoPopupOpen`: overlay поверх текущего состояния
  (`_infoPopupRoot`), показывает иконку, название, бонусы (`GenreMultipliers`),
  описание, одна кнопка закрытия. Не меняет выбор декора и не сбрасывает расстановку.

## Шаг 7 — [CODE] DoTween-анимации

Паттерн из `Assets/Game/Infrastructure/UIShared/AnimatedShowHidePanel.cs` (`DOTween.To` +
`Sequence().Join(...).SetEase(...)`, kill предыдущего твина, kill в `OnDestroy`).
Реализовать в `DecorSlotAnchorView`:
- Place: scale `0.85→1.08→1.0`, alpha `0→1`, ~0.2–0.3s;
- Remove: scale `1.0→0.85`, alpha `1→0`, затем `onComplete` → очистка слота.
Анимация только визуальная; состояние меняет сервис, view обновляется по результату.

## Шаг 8 — [CODE] Звук на успешный place

Через существующий фасад: `Audio.PlayUi(View.PlaceClip)` (см.
`Assets/Game/Infrastructure/Audio/Audio.cs`, пример `UiButtonClickAudio`). Клип —
serialized-поле на view; если пуст — no-op (проверка `!= null`). Отдельную аудио-систему
не заводим. Remove-звук — на будущее.

---

## Шаг 9 — [EDITOR] Авторинг префаба `DecorPlacementWindow.prefab`

Пользователь в Unity Editor перестраивает
`Assets/Game/Features/Decor/UI/Prefab/DecorPlacementWindow.prefab` под иерархию:

```
DecorPlacementWindow
  RoomRoot
    RoomBackground        (Image/RawImage, Preserve Aspect)
    RoomImageRect         (RectTransform, повторяет ВИДИМОЕ изображение фона)
      SlotsRoot
        DecorSlotAnchorView cart_table_1
        DecorSlotAnchorView cart_table_2
        DecorSlotAnchorView cart_table_3
  BottomInventoryPanel
    ItemsRoot  (+ DecorInventoryCardView template)
  SelectedSlotHud (Replace[disabled] / Remove)
  InfoPopupRoot
  CloseButton
```

Ключевое:
- Якоря слотов привязывать к `RoomImageRect` (видимое изображение), НЕ к RectTransform
  фона — иначе при другом aspect-ratio точки уползут (aspect-fit леттербокс).
- `_slotId` каждого anchor = id из `bookshops.json`. Для MVP достаточно трёх
  `Standing`-слотов (`cart_table_1..3`).
- Назначить все serialized-поля view (шаг 3) и anchor-полей (шаг 1).

## Шаг 10 — [EDITOR] Спрайты/addressables декора

Убедиться, что addressable-спрайты для декора доступны **по ключу = decor id**
(`vintage_globe`, `coffee_pot`), т.к. загрузка идёт `GetSpriteAsync(decorId)`.
Если сейчас ассеты помечены только `Decor/VintageGlobe`/`Decor/CoffeePot` (как в
`iconAddress`), добавить addressable-ключ по id либо адрес, который отдаёт
`IUiSpriteProvider`. Проверить: иконки грузятся в карточках и в занятых слотах.

## Опционально — [CODE] проверка слот↔якорь в валидаторе

По доку: расширить `DecorConfigValidator` проверкой, что для всех слотов
`main_bookshop` из `bookshops.json` есть якорь в префабе (защита от рассинхрона
`slotId`). Полезно, но не блокер MVP — согласовать, делать ли сейчас.

---

## Критические файлы

| Файл | Действие |
|---|---|
| `Assets/Game/Core/Installers/Bootstrap/Bootstrap.cs` | [CODE] инжект `IDecorPlacementService` для форс-конструкции — ✅ |
| `Assets/Game/Core/Installers/Features/DecorVContainerBindings.cs` | [CODE] убрать/обновить TODO о баге — ✅ |
| `Assets/Game/Features/Decor/UI/DecorSlotAnchorView.cs` | [CODE] новый |
| `Assets/Game/Features/Decor/UI/DecorInventoryCardView.cs` | [CODE] новый |
| `Assets/Game/Features/Decor/UI/DecorPlacementWindow.cs` | [CODE] переработка на decor-first |
| `Assets/Game/Features/Decor/UI/DecorPlacementWindowView.cs` | [CODE] новые serialized-поля |
| `Assets/Game/Features/Decor/UI/Prefab/DecorPlacementWindow.prefab` | [EDITOR] новая иерархия |
| addressables/спрайты декора | [EDITOR] ключи по decor id |

Используемое существующее (не переписывать):
- `IDecorPlacementService` / `DecorPlacementService.PlaceAsync` — вся валидация и сейв.
- `IUiSpriteProvider.GetSpriteAsync(id, ct)` (`UiSpriteProvider`) — иконки по id.
- `Audio.PlayUi(clip)` (`Audio.cs`).
- DoTween-паттерн из `AnimatedShowHidePanel.cs`.
- `DecorationCheatModule` — выдача декора в инвентарь (уже есть).
- `ConfirmDialog` / `ConfirmDialogArgs` — гейт негативного декора.
- Паттерн окна: `WindowController<TView>`, пример `ClassicShopWindow`.

## Verification (end-to-end)

1. **Boot**: запустить сцену — `DecorConfigValidator` не кидает ошибок; в логе видно,
   что `DecorPlacementService` сконструирован до `SaveDataLoadOperation` (лог ctor
   «hook registered» до загрузки сейва).
2. **Выдача**: через чит-меню «Add Vintage Globe» и «Add Coffee Pot» → открыть окно,
   в нижней панели две карточки с иконками.
3. **Place**: клик карточки → подсвечиваются только пустые `Standing`-слоты
   (`cart_table_*`); клик по слоту → декор появляется с tween + звук, карточка теряет
   selected, подсветка гаснет.
4. **Info**: кнопка info на карточке → popup с иконкой/названием/бонусами/описанием;
   закрытие не сбрасывает выбор/расстановку.
5. **Remove**: клик по установленному декору → HUD (Remove активна, Replace disabled);
   Remove → tween исчезновения, marker возвращается, предмет остался в инвентаре.
6. **Сохранение (приёмочный тест доп. §8)**: поставить оба декора → закрыть игру →
   открыть → открыть окно → оба на тех же местах. (Проходит только при выполненном шаге 0.)
7. Прогнать существующие decor-тесты (если есть) после правок домена/бута.
