# Decor Placement Cases

## Purpose

This document describes user-facing decor placement cases for `DecorPlacementWindow`.
It complements:

- `docs/INPROGRESS/DECOR_PLACEMENT_MVP_PLAN.md`
- `docs/INPROGRESS/DECOR_PLACEMENT_MVP_PROCESS.md`

Terms:

- **Point** = `DecorSlotAnchorView`, a place in the room where decor can be shown.
- **Item** = `DecorInventoryCardView`, a decor card in the bottom inventory.
- **Preview** = temporary UI-only visual in a point. It is not saved and does not call
  `IDecorPlacementService.PlaceAsync` until the player presses `Apply`.

Core rule:

- The domain service remains the source of truth for committed placement.
- Preview state lives only in `DecorPlacementWindow` / `DecorSlotAnchorView`.
- `PlaceAsync` is called only from `Apply`.

## Stage 1 Scope: Empty Point Selection

This stage covers only the first placement into an empty point.

Included:

- Click empty point.
- Filter inventory to items available for that point.
- Click item to preview it in the selected point.
- Click another item to replace the current preview.
- `Cancel` resets the flow.
- `Apply` commits the preview.

Not included:

- Full replace flow for an occupied point.
- Drag and drop.
- Purchase flow.
- World-space placement.

## Availability Rules

When a point is selected, the inventory should show only items that can be placed into
that exact point.

Recommended filter:

- item exists in the player's decor inventory;
- item is not already placed in another point;
- `DecorConfig.PositionType == DecorSlot.PositionType`;
- `DecorConfig.Size <= DecorSlot.MaxSize`.

Reason: this flow says "available items for this point", so the list should avoid items
that would fail on `Apply` with `SizeMismatch`, `PositionTypeMismatch`, or `AlreadyPlaced`.

## Case 1: Window Open

Initial state:

- No point is selected.
- No item is selected.
- No preview is visible.
- All empty point markers are interactable.
- Occupied points show their committed decor.
- Inventory shows all unfiltered decor items.
- `Cancel` and `Apply` are hidden.

Expected state:

- `State.Default`.
- slot filter is `null`.
- selected point id is `null`.
- preview decor id is `null`.

## Case 2: Click Empty Point

Action:

- Player clicks an empty point.

Expected behavior:

- The clicked point becomes the selected empty point.
- Inventory is filtered to items available for that point.
- Any placed-slot HUD is closed.
- Any card-first selection is cleared.
- `Cancel` and `Apply` are shown.
- `Apply` is disabled until an item is selected for preview.
- No committed placement is changed.

Visual notes:

- The selected point may show an outline or active marker state.
- Other empty points should stay visible, but the current flow is bound to the selected point.

## Case 3: Click Item After Empty Point

Precondition:

- An empty point is selected.
- Inventory is filtered for that point.

Action:

- Player clicks an item card.

Expected behavior:

- The clicked item becomes selected.
- The item sprite is shown as preview in the selected point.
- `Apply` becomes enabled.
- `Cancel` remains visible.
- No committed placement is changed.

Important:

- Preview must not call `SetPlaced` in a way that enables occupied-slot behavior.
- Preview click should not open the remove/replace HUD.
- Preview is UI-only and must disappear on cancel, close, or failed reset.

## Case 4: Click Another Item During Preview

Precondition:

- An empty point is selected.
- One item is already shown as preview.

Action:

- Player clicks another available item.

Expected behavior:

- Previous preview is replaced by the new item preview.
- Previous card loses selected state.
- New card gets selected state.
- `Apply` stays enabled.
- No committed placement is changed.

## Case 5: Cancel Preview

Precondition:

- Empty point flow is active.
- Preview may or may not exist.

Action:

- Player clicks `Cancel`.

Expected behavior:

- Preview is cleared.
- Selected item is cleared.
- Selected point is cleared.
- Inventory filter is cleared.
- `Cancel` and `Apply` are hidden.
- Empty point markers return to their default available state.
- Window returns to the same practical state as immediately after opening.
- No committed placement is changed.

Expected state:

- `State.Default`.
- slot filter is `null`.
- selected point id is `null`.
- preview decor id is `null`.

## Case 6: Apply Preview

Precondition:

- Empty point flow is active.
- Preview decor id is set.
- Selected point id is set.

Action:

- Player clicks `Apply`.

Expected behavior:

- If the decor has a negative effect, show the existing `ConfirmDialog` before committing.
- If confirmed, call `PlaceAsync(previewDecorId, selectedPointId)`.
- On success:
  - preview state is cleared;
  - inventory filter is cleared;
  - `Cancel` and `Apply` are hidden;
  - committed placement is rendered through `PlacementChanged -> Render`;
  - place animation and sound may play as in the current committed placement flow.
- On failure:
  - log the result;
  - keep the UI in a recoverable state;
  - recommended MVP behavior: keep preview active so the player can cancel or choose another item.

## Case 7: Click Occupied Point

Action:

- Player clicks a point that already has committed decor.

Expected behavior for current stage:

- Existing occupied-point HUD behavior remains.
- Remove remains available.
- Replace remains disabled or out of scope.
- Empty-point preview flow is not started.

Future:

- Full replace flow can reuse the same preview model, but with an original committed decor id.
- `Cancel` should restore the original committed visual.
- `Apply` should commit the replacement through a domain-supported operation or explicit
  unplace/place sequence after the replacement rules are defined.

## Case 8: Backdrop / Outside Click

Current behavior:

- `HudBackdrop` closes the placed-slot HUD and clears the slot-first inventory filter.

Recommended behavior with preview:

- If preview flow is active, backdrop should act like `Cancel` only if this feels intentional
  in testing.
- Safer MVP behavior: backdrop resets inventory filter and closes HUD, but `Cancel` is the
  explicit way to discard preview.

Decision needed before implementation:

- Should tapping outside cancel an active preview, or should only the `Cancel` button do that?

## Case 9: Window Close / Hide

Action:

- Player closes the window or the window is hidden.

Expected behavior:

- Any preview is cleared.
- Selected point and selected item are cleared.
- Inventory filter is cleared.
- `Cancel` and `Apply` are hidden.
- No uncommitted preview is saved.

## Suggested Controller State

Current states:

- `Default`
- `DecorSelected`
- `PlacedSlotSelected`

Suggested addition for stage 1:

- `EmptySlotPreview`

Suggested transient fields:

- selected empty slot id;
- preview decor id;
- slot type/slot id filter;
- possibly a cached selected anchor reference, if useful.

## Verification Checklist

1. Open window: all empty points available, full inventory, no `Cancel` / `Apply`.
2. Click empty Standing point: inventory shows only decor valid for that point.
3. Click item: preview appears in the clicked point, `Apply` enables.
4. Click another item: preview updates, only the new card is selected.
5. Click `Cancel`: preview disappears, full inventory returns, buttons hide.
6. Repeat point -> item -> `Apply`: decor is committed and survives window re-render.
7. Close window during preview and reopen: no preview remains.
8. Click occupied point: existing Remove HUD still works; preview flow does not start.
9. Try negative-effect decor later: confirm dialog appears before `PlaceAsync`.
