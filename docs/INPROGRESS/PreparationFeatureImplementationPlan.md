# Preparation Feature Implementation Plan

## Summary

Implement the first playable version of the `Preparation` phase: the player chooses a location, selects books for the daily shelf within configurable capacity, optionally selects decor/upgrades, validates the setup, and hands the resulting setup to `Sales`.

First-pass scope:

- Build preparation domain logic and validation.
- Add a simple uGUI/debug screen with step flow: location -> books -> decor -> confirm.
- Keep decor as a placeholder selection with simple ids; no production decor economy yet.
- Integrate with the existing `Configs` and `Save` systems.
- Replace the temporary sales fallback setup with a real preparation-produced setup.

Chosen decisions:

- Implementation level: vertical slice.
- Location selection exists even if only one location is available.
- Book and decor limits are configurable/current-player values, not UI constants.
- Base MVP capacity: `dailyBookSlots = 12`, `minDailyBooks = 1`, `dailyDecorSlots = 1`.

## Key Changes

Add the preparation domain model:

- `PreparationSessionSetup`: `Day`, `SelectedLocationId`, `SelectedBookIds`, `SelectedDecorIds`, `ActiveModifierIds`.
- `PreparationSessionState`: current step, selected location, selected books, selected decor, validation state.
- `PreparationCapacity`: `MinDailyBooks`, `DailyBookSlots`, `DailyDecorSlots`.
- `PreparationValidationResult`: `IsValid`, blocking errors, warnings.
- `SelectableBook`, `SelectableLocation`, `SelectableDecor` view/domain DTOs for UI binding.

Add preparation services:

- `IPreparationSessionService`: start/resume preparation, select location, toggle book, toggle decor, validate, confirm.
- `IPreparationCapacityProvider`: returns current capacity from save/progression with MVP defaults.
- `IPreparationInventoryProvider`: returns owned books/decor; for the first pass, all configured books are owned and decor uses a small static placeholder list.
- `IPreparationRelevanceService`: calculates simple relevance labels for books based on selected location demand and active event modifiers.
- `IPreparationSaveService` or internal adapter over `ISaveService` using module key `preparation.session`.

Expand or reuse config models:

- `BookConfig`: use `Genre`, `Tags`, `Mood`, `BasePrice` from the sales plan.
- `LocationConfig`: use `DisplayName`, `DemandGenres`, `DemandTags`; keep unlock fields.
- `EventConfig`: optionally expose active modifier ids/text later; first pass can use an empty modifier list.
- Add a temporary `DecorConfig` only if needed for the UI; otherwise keep decor as hardcoded placeholder ids in `IPreparationInventoryProvider`.

Update assembly and DI:

- Add `Configs`, `Save`, `UniTask`, and UI references to `Inventory.asmdef` or a new `Preparation`/`Book.Sell` assembly if chosen during implementation.
- Register preparation services in `InventoryVContainerBindings.RegisterInventory()` if the feature lives in `Inventory`.
- Expose a setup contract that `BookSell` can consume instead of its temporary `ISalesSetupProvider` fallback.

Add a simple preparation screen:

- Step 1: location list/map placeholder with locked/unlocked states.
- Step 2: book grid/list with selected count, capacity, genre/tags, and relevance hint.
- Step 3: decor placeholder list with selected count and capacity.
- Step 4: confirmation panel with selected location, book count, decor count, and `Open Shop`/`Start Sales` button.
- Disable confirm until validation passes.
- Persist partial selection after each meaningful change.

## Behavior Details

Location selection:

- Show all configured locations.
- A location is unlocked when `RequiredLevel` and `UnlockCost` conditions are satisfied; first pass can treat `RequiredLevel <= 1` and `UnlockCost <= 0` as unlocked.
- If only one location is unlocked, preselect it but still show the location step.

Book selection:

- The player can select up to `DailyBookSlots`.
- The player must select at least `MinDailyBooks`.
- Duplicate book ids are not allowed.
- Selecting an already selected book toggles it off.
- If capacity is reached, unselected books become disabled until another selected book is removed.
- Relevance hint is informational only and must not auto-pick books.

Decor selection:

- The player can select up to `DailyDecorSlots`.
- `DailyDecorSlots = 0` is valid if progression/config later disables decor.
- Decor is optional in MVP; no minimum decor requirement.
- Decor ids are passed through to sales setup but do not affect sales scoring in the first pass.

Validation:

- Blocking errors: no location, too few books, too many books, too many decor, duplicate ids, selected locked location, selected unknown book/decor.
- Warnings: weak genre coverage for selected location, no decor selected when decor slots are available.
- Confirm produces a stable setup payload and saves `currentPhase = sales`.

Save behavior:

- Save partial preparation state after location/book/decor changes.
- Restore the player to the same preparation step and selection after restart.
- On confirm, save the completed setup separately so `Sales` can start from it.
- Do not clear preparation state until sales has successfully started or consumed the setup.

Integration with Sales:

- Replace sales fallback setup with a provider that first checks for a confirmed preparation setup.
- Fallback setup remains only as defensive/dev behavior and should log a warning when used.
- `SalesSessionSetup.BookIds` must preserve the exact selected book order from preparation.

## Test Plan

Preparation validation EditMode tests:

- Valid setup with one unlocked location, `MinDailyBooks` books, and no decor passes.
- No location fails.
- Too few books fails.
- More than `DailyBookSlots` books fails.
- More than `DailyDecorSlots` decor fails.
- Duplicate book/decor ids fail.
- Locked location fails.

Capacity tests:

- MVP defaults return `MinDailyBooks = 1`, `DailyBookSlots = 12`, `DailyDecorSlots = 1`.
- Book capacity blocks extra selections.
- Decor capacity blocks extra selections.

Save tests with fake `ISaveService`:

- Partial selection persists after each step.
- Restore resumes on the same step with selected ids.
- Confirm writes the completed setup for sales.

Sales integration tests:

- Sales setup provider uses confirmed preparation setup before fallback.
- Sales receives selected location, book ids, decor ids, and active modifiers unchanged.
- Fallback logs warning only when no confirmed preparation setup exists.

Manual Unity check:

- Open preparation screen.
- Select location, select books up to capacity, select optional decor, confirm.
- Verify confirm is disabled for invalid selections.
- Verify selected setup starts the sales screen.
- Restart during preparation and verify selection restores.

## Assumptions

- First implementation can live under `Assets/Game/Features/Inventory` because `Inventory` is currently the closest planned module for owned books and daily shelf selection.
- A dedicated `Preparation` assembly may be introduced later if the feature grows beyond inventory ownership and daily setup.
- All configured books are treated as owned for the first playable slice.
- Decor inventory is placeholder-only in this slice.
- Active morning/event modifiers are accepted as ids but can be empty until the `Morning` feature is implemented.
- Production map visuals and drag-and-drop shelf layout are out of scope.
- Existing `docs/INPROGRESS/Подготовка.md` remains the GDD/spec source; this file is the implementation plan.
