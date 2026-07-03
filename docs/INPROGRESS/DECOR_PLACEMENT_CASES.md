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
- **Availability visual** = whole-point dimming for unavailable targets. It applies to
  empty, preview, and occupied points and is separate from button interactability.

Core rule:

- The domain service remains the source of truth for committed placement.
- Preview state lives only in `DecorPlacementWindow` / `DecorSlotAnchorView`.
- `PlaceAsync` is called only from `Apply`.
- Occupied point tools are opened only by clicking already placed decor.

## Stage 1 Scope: Empty Placement + Occupied Availability Prep

This stage covers first placement into an empty point plus the visual/filter groundwork
needed for a future replace flow.

Included:

- Click empty point.
- Filter inventory to items available for that point.
- Click item to preview it in the selected point.
- Click another item to replace the current preview.
- Click item first, then choose a compatible point for preview.
- Dim unavailable points, including occupied points.
- Click occupied point to open tools and filter inventory by that point type.
- `Cancel` resets the flow.
- `Apply` commits the preview.

Not included:

- Full replace flow for an occupied point.
- Drag and drop.
- Purchase flow.
- World-space placement.

## Availability Rules

When an item is selected, all points are evaluated by `PositionType`.

Expected point visual:

- compatible empty points stay available and may show target highlight;
- incompatible empty points become non-interactable and dimmed;
- compatible occupied points stay visually available;
- incompatible occupied points are dimmed but remain clickable for tools.

When a point is selected, inventory is filtered by that point's `PositionType`. All other
points are dimmed; empty non-selected points are not interactable during point-first
selection.

Current UI filter:

- item exists in the player's decor inventory;
- `DecorConfig.PositionType == DecorSlot.PositionType`;
- already placed items may still be shown with their existing placed badge/disabled
  selection behavior.

Domain validation remains stricter than the UI and still rejects `SizeMismatch`,
`SlotOccupied`, and `AlreadyPlaced` on `Apply`.

## Case 1: Window Open

Initial state:

- No point is selected.
- No item is selected.
- No preview is visible.
- All empty point markers are interactable.
- Occupied points show their committed decor.
- All points use normal availability visual.
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
- The clicked point stays normal and outlined.
- All other points, including occupied points, are dimmed.
- Other empty point markers become non-interactable.
- Preview `Cancel` and `Apply` stay hidden until an item is selected for preview.
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
- Preview `Cancel` and `Apply` are shown.
- `Apply` becomes enabled.
- `Cancel` remains visible.
- No committed placement is changed.

Important:

- Preview must not call `SetPlaced` in a way that enables occupied-slot behavior.
- Preview click should not open the remove/replace HUD.
- Preview is UI-only and must disappear on cancel, close, or failed reset.

## Case 4: Click Item First

Action:

- Player clicks an inventory item with no point selected.

Expected behavior:

- The clicked item becomes selected.
- Compatible points stay visually available.
- Incompatible points are dimmed.
- Incompatible empty point markers become non-interactable.
- Occupied points remain clickable for tools.
- Preview actions stay hidden until a compatible empty point is selected.
- No committed placement is changed.

## Case 5: Click Compatible Empty Point After Item

Precondition:

- An item is selected.
- At least one compatible empty point is available.

Action:

- Player clicks a compatible empty point.

Expected behavior:

- The selected item appears as preview in that point.
- That point becomes selected and outlined.
- All other points are dimmed.
- Preview `Cancel` and `Apply` are shown.
- `Apply` is enabled.
- No committed placement is changed.

## Case 6: Click Another Item During Preview

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
- If the new item is not compatible with the selected point, the selected point is cleared
  and points are filtered by the newly selected item.
- No committed placement is changed.

## Case 7: Cancel Preview

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
- Availability visual is reset for all points.
- Empty point markers return to their default available state.
- Window returns to the same practical state as immediately after opening.
- No committed placement is changed.

Expected state:

- `State.Default`.
- slot filter is `null`.
- selected point id is `null`.
- preview decor id is `null`.

## Case 8: Apply Preview

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

## Case 9: Click Occupied Point

Action:

- Player clicks a point that already has committed decor.

Expected behavior for current stage:

- Empty-point preview flow is not started.
- Any active preview/filter/card selection is reset first.
- Existing occupied-point HUD behavior remains.
- Remove remains available.
- Replace remains visible but disabled.
- Inventory is filtered by the clicked point's `PositionType`.
- Clicked occupied point stays normal and outlined.
- All other points are dimmed.
- Occupied points remain clickable for tools.

Future:

- Full replace flow can reuse the same preview model as
  `PlacedSlotSelected -> choose item -> replacement preview -> Apply`.
- `Cancel` should restore the original committed visual.
- `Apply` should commit the replacement through a domain-supported operation or explicit
  unplace/place sequence after the replacement rules are defined.

## Case 10: Backdrop / Outside Click

Current behavior:

- `HudBackdrop` acts like transient reset.
- Preview is cleared.
- Inventory filter is cleared.
- Availability visual is reset for all points.
- Placed-slot HUD is closed.
- No committed placement is changed.

## Case 11: Window Close / Hide

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
- `PointSelected`
- `DecorSelected`
- `Preview`
- `PlacedSlotSelected`

Suggested transient fields:

- selected point id;
- selected/preview decor id;
- slot type/slot id filter;
- apply in progress flag;
- preview icon cancellation token.

## Verification Checklist

1. Open window: all empty points available, full inventory, no preview actions.
2. Select a Wall item while non-Wall occupied points exist: non-Wall occupied points dim.
3. Select a Standing/Table item: incompatible Wall points dim, compatible points stay normal.
4. Click empty point: selected point normal/outlined, all others dim, inventory filtered.
5. Click item after point: preview appears, `Apply` enables.
6. Click item first, then compatible point: preview appears and no instant placement occurs.
7. Click occupied point: tools open, inventory filters by point type, Replace disabled.
8. Click backdrop/cancel/close: preview/filter/dimming/tools all reset.
9. Apply preview: placement commits only through `Apply` and survives re-render.
