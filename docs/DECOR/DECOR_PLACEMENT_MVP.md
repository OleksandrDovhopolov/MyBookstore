# Decor Placement MVP

> Объединяет прежние `DECOR_PLACEMENT_MVP_PROCESS.md` (спецификация/UX) и
> `DECOR_PLACEMENT_MVP_PLAN.md` (план реализации). Актуально по коду на момент завершения
> этапа «выбор декораций».
> Пооперационные UX-кейсы (окно открыто / клик точки / клик предмета / replace / cancel / apply /
> backdrop / close) вынесены в **`DECOR_PLACEMENT_CASES.md`** — здесь обзор и статус.

## Статус

- **[CODE] сделано:** save-hook fix, `DecorSlotAnchorView`, `DecorInventoryCardView`, полный
  preview-контроллер `DecorPlacementWindow` (point-first + inventory-first + replace), remove-HUD,
  info-popup, DoTween-анимации place/remove на анкере, звук на place, доменный `ReplaceAsync`.
- **[EDITOR] остаётся:** авторинг префаба под финальную иерархию (точки, HUD-кнопки
  Cancel/Apply/Remove, preview-actions root) и addressables-спрайты по decor id; опционально —
  проверка слот↔якорь в валидаторе.

## Цель

Визуальное MVP расстановки декора в комнате: игрок видит комнату с точками, выбирает декор из
нижней панели, ставит его **через preview-подтверждение** (`Apply`), снимает, заменяет, смотрит
info; после перезахода расстановка восстанавливается.

## Ключевое архитектурное решение: точки на UI, не на сцене

Точки размещения — UI-слой внутри `DecorPlacementWindow`, а не world-space на сцене. Причина:
референс — полноэкранная 2D-иллюстрация комнаты с UI-точками поверх. Для MVP это дешевле и
безопаснее по адаптации под устройства (нет камеры, world-raycast, 2D/3D-сортировки).

Решение обратимое: модель размещения view-agnostic — `IDecorPlacementService` оперирует
строковыми `slotId`/`decorId` и ничего не знает про пиксели. Позже презентацию можно заменить на
сцену, не трогая сервис, сейв и валидацию.

Известные UI-ограничения (для MVP не блокеры):
- **Aspect-fit фона.** Якоря привязывать к контейнеру, повторяющему *видимое* изображение
  (`_roomImageRect`), а не к `RectTransform` фона — иначе на других соотношениях точки уползут.
  (См. также `SafeAreaInverseStretch` для full-bleed фона под safe area.)
- **Позиция/масштаб слота живут в префабе**, а связь с конфигом — по строковому `slotId`. Легко
  рассинхронить; кандидат на проверку в `DecorConfigValidator` (гоняется на boot).
- **Occlusion** без ручного разбиения фона на слои не поддерживается; для стоячих предметов не нужен.

## Домен — источник правды (готов, не переписывать)

`IDecorPlacementService` / `DecorPlacementService`:
- `PlaceAsync` / `UnplaceAsync` / `ReplaceAsync` / `GetAllPlacements` / `GetDecorInSlot` /
  `GetActiveDecorIds` / `ClearAllAsync` + событие `PlacementChanged`.
- Валидация размещения: `DecorConfig.PositionType == DecorSlot.PositionType`;
  `DecorConfig.Size <= DecorSlot.MaxSize`; слот свободен (для place); декор есть в инвентаре; не стоит
  уже в другом слоте. `ReplaceAsync` — те же проверки, но целевой слот должен быть занят
  (`SlotEmpty` иначе), и атомарно меняет `decorId` в существующей записи с **одним** `PlacementChanged`
  и одним сохранением.

UI не хранит committed-состояние — только рендерит сервис (`PlacementChanged` → `Render`) и шлёт
команды. `PlaceAsync`/`ReplaceAsync`/`UnplaceAsync` вызываются только из `Apply`/`Remove`.

## UI: окно, view и компоненты

- **`DecorPlacementWindow`** (`WindowController<DecorPlacementWindowView>`) — вся логика/состояния.
- **`DecorPlacementWindowView`** — serialized-ссылки: `_roomImageRect`, `DecorSlotAnchorView[]`,
  `UIListPool<DecorInventoryCardView> _cardsPool`, `_selectedSlotHud` (панель tools) с
  `_removeButton`/`_replaceButton`/`_previewActionsRoot`(`_cancelPreviewButton`/`_applyPreviewButton`),
  `_selectedDecorNameLabel`/`_selectedDecorImage`, `_hudBackdrop`, аудио-клипы place/remove.
- **`DecorSlotAnchorView`** — «тупой» view точки: `SetEmpty`/`SetPlaced(sprite)`/`SetPreview(sprite)`
  (UI-only превью, `_placedButton` неинтерактивен), `SetSelectedOutline`, `SetMarkerInteractable`,
  `SetAvailabilityVisual` (диммирование недоступных точек), `SetHighlighted`, place/remove-твины,
  `CurrentPlacedSprite` (снимок оригинала для восстановления).
- **`DecorInventoryCardView`** — карточка: `Bind(config, isPlaced, selectable, sprites, onSelect,
  onInfo)`, `SetSelected`; иконка грузится **по decor id** (`GetSpriteAsync(decorId)`, не по
  `IconAddress`); `isPlaced`-бейдж — «used».

### Состояния контроллера

`Default`, `PointSelected`, `DecorSelected`, `Preview`, `PlacedSlotSelected`.

Transient-поля: `_previewPointId`, `_previewDecorId`, `_replaceOriginalDecorId` +
`_replaceOriginalSprite` (дискриминатор/restore для replace), `_selectedSlotId`, `_slotTypeFilter`
(type-only), `_applyInProgress`, `_previewIconCts` (гонка загрузок), `_placedDecorBySlot` (диф слотов
по decorId — чтобы замена в той же точке перерисовалась).

### Потоки (детально — в `DECOR_PLACEMENT_CASES.md`)

- **Point-first:** клик пустой точки → `PointSelected`, инвентарь фильтруется по её `PositionType`,
  прочие точки диммятся; клик совместимого предмета → `Preview` (спрайт в точке) + `Cancel`/`Apply`.
- **Inventory-first:** клик предмета без выбранной точки → `DecorSelected`, совместимые пустые точки
  подсвечены/интерактивны, несовместимые диммятся; клик совместимой точки → `Preview`.
- **Replace:** клик занятой точки → `PlacedSlotSelected`, tools (Remove), инвентарь фильтруется по
  типу, оригинал сохранён; клик другого предмета → `Preview` (замена), Remove скрыт; `Apply` →
  `ReplaceAsync`.
- **Apply:** гейт `ConfirmDialog` для негативного декора (`GenreMultipliers < 1`), затем
  `PlaceAsync`/`ReplaceAsync`. Успех — коммит через `PlacementChanged → Render`; провал — лог, preview
  остаётся recoverable.
- **Cancel / Backdrop / Close:** сброс preview (для replace — восстановление оригинального спрайта
  синхронно из снимка), очистка фильтра/диммирования/tools, возврат в `Default`. `Render` тоже всегда
  сбрасывает transient к committed (устойчиво к тому, что `PlacementChanged` летит внутри await).

Фильтр инвентаря **type-only** by design (Size ловит домен на `Apply` → `SizeMismatch`).

## Сохранение после перезахода (fix сделан)

`DecorPlacementService` регистрирует save-hook только в конструкторе, а резолвится лениво — раньше
на втором запуске расстановка не грузилась (P1). Фикс: форс-конструкция сервиса в `Bootstrap` до
`SaveDataLoadOperation` (по паттерну `IQuestsService`/`IInventoryService`). TODO в
`DecorVContainerBindings` закрыт.

Загрузка: `GetAllPlacements()` → по каждому `(slotId, decorId)` найти anchor → `SetPlaced(sprite)`.

## Анимации и звук

- Place: scale `0.85→1.08→1.0`, alpha `0→1`, ~0.2–0.3s. Remove: scale `1.0→0.85`, alpha `1→0`, затем
  очистка слота. DoTween, только визуал; состояние меняет сервис. Паттерн — `AnimatedShowHidePanel.cs`.
- Звук на успешный place: `Audio.PlayUi(View.PlaceClip)` (no-op если клип пуст). Remove-клип — есть
  поле, опционально.

## Критические файлы

| Файл | Статус |
|---|---|
| `Bootstrap.cs` / `DecorVContainerBindings.cs` | [CODE] save-hook fix — ✅ |
| `DecorSlotAnchorView.cs` | [CODE] ✅ (empty/placed/preview/availability/твины) |
| `DecorInventoryCardView.cs` | [CODE] ✅ (`selectable`-гейт, иконка по id) |
| `DecorPlacementWindow.cs` | [CODE] ✅ preview/replace-контроллер |
| `DecorPlacementWindowView.cs` | [CODE] ✅ поля под HUD/preview/pool |
| `Prefab/DecorPlacementWindow.prefab` | [EDITOR] иерархия, HUD-кнопки, назначение полей |
| addressables/спрайты декора | [EDITOR] ключи по decor id |
| `DecorConfigValidator` (слот↔якорь) | [CODE] опционально |

Переиспользуемое (не переписывать): `IDecorPlacementService` (вся валидация/сейв),
`IUiSpriteProvider.GetSpriteAsync(id)`, `Audio.PlayUi`, DoTween-паттерн, `DecorationCheatModule`
(выдача декора в инвентарь), `ConfirmDialog`/`ConfirmDialogArgs`, `WindowController<TView>`.

## Verification (end-to-end)

1. **Boot:** валидатор без ошибок; `DecorPlacementService` сконструирован до `SaveDataLoadOperation`.
2. **Выдача:** чит «Add Vintage Globe»/«Add Coffee Pot» → в панели две карточки с иконками.
3. **Place (point-first и inventory-first):** превью в точке → `Apply` → декор с твином + звук.
4. **Replace:** клик занятой точки → Remove/фильтр; клик другого предмета → превью замены; `Apply` →
   `ReplaceAsync`, без мигания пустого слота.
5. **Remove:** Remove → твин исчезновения, marker вернулся, предмет остался в инвентаре.
6. **Cancel/Backdrop/Close:** превью/фильтр/диммирование/tools сброшены; для replace оригинал вернулся.
7. **Save:** поставить оба декора → перезайти → оба на местах (проходит после save-hook fix).
8. **Negative decor:** `ConfirmDialog` перед `Apply`.

## Не входит в MVP / будущее

- Drag & drop; покупка декора; world-space точки/отдельная сцена; сложные per-item эффекты.
- Настенный декор (`WallAnchor_*`) и occlusion-слои — при переезде на сцену.
