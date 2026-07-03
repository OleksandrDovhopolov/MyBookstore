using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Inventory.API;
using Game.Newspaper.UI;
using Game.UI;
using Game.UI.Common;
using Infrastructure.Audio;
using UnityEngine;
using VContainer;

namespace Game.Decor.UI
{
    /// <summary>
    /// Visual decor placement window (MVP). Decor-first flow: pick a card in the bottom panel,
    /// compatible empty slots highlight, click a slot to place. Click a placed decor for the
    /// Remove HUD; the info button opens a read-only popup. The window renders service state only
    /// (subscribes to <see cref="IDecorPlacementService.PlacementChanged"/>) and never stores its
    /// own placement state. See docs/INPROGRESS/DECOR_PLACEMENT_MVP_PLAN.md.
    /// </summary>
    [Window("DecorPlacementWindow", WindowType.Page)]
    public sealed class DecorPlacementWindow : WindowController<DecorPlacementWindowView>
    {
        private enum State
        {
            Default,
            DecorSelected,
            PlacedSlotSelected,
        }

        private IDecorPlacementService _placement;
        private IConfigsService _configs;
        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;

        private CancellationTokenSource _cts;
        private readonly HashSet<string> _placedSlots = new();

        private State _state = State.Default;
        private string _selectedDecorId;
        private string _selectedSlotId; // placed slot whose HUD is open
        private bool _firstRender;

        // Slot-first inventory filter: when set, RenderInventory shows only decor of this PositionType.
        // Set by clicking a slot, cleared by the HUD backdrop (or window open). Independent of selection.
        private DecorPositionType? _slotTypeFilter;

        [Inject]
        public void InjectServices(
            IDecorPlacementService placement,
            IConfigsService configs,
            IInventoryService inventory,
            IUiSpriteProvider sprites)
        {
            _placement = placement;
            _configs = configs;
            _inventory = inventory;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
            _cts = new CancellationTokenSource();

            if (View.SelectedSlotHud != null) View.SelectedSlotHud.SetActive(false);
            if (View.ReplaceButton != null) View.ReplaceButton.interactable = false; // MVP: no replace flow

            if (View.RemoveButton != null) View.RemoveButton.onClick.AddListener(OnRemoveClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.AddListener(OnBackdropClicked);

            // Anchors are authored in the prefab and live as long as the view — subscribe once.
            if (View.SlotAnchors != null)
            {
                foreach (var anchor in View.SlotAnchors)
                {
                    if (anchor == null) continue;
                    var captured = anchor;
                    captured.OnMarkerClicked += () => OnMarkerClicked(captured);
                    captured.OnPlacedClicked += () => OnPlacedClicked(captured);
                }
            }
        }

        protected override void OnShowStart()
        {
            if (_placement != null) _placement.PlacementChanged += Render;
            if (_inventory != null) _inventory.Changed += OnInventoryChanged;

            // Clean, non-animated re-sync on every open.
            _placedSlots.Clear();
            _slotTypeFilter = null; // fresh open shows the full inventory
            _firstRender = true;
            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_placement != null) _placement.PlacementChanged -= Render;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;

            HideHud();
        }

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (View == null) return;
            if (View.RemoveButton != null) View.RemoveButton.onClick.RemoveListener(OnRemoveClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.RemoveListener(OnBackdropClicked);
        }

        private void OnInventoryChanged(InventoryChangeEvent _) => Render();

        private void Render()
        {
            if (_placement == null || View == null) return;
            RenderSlots();
            RenderInventory();
            HideHud();        // committed state changed → drop transient HUD
            ClearSelection(); // renders reflect committed state → drop transient selection
            _firstRender = false;
        }

        private void RenderSlots()
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;
                var slotId = anchor.SlotId;
                var decorId = _placement.GetDecorInSlot(slotId);
                var nowPlaced = !string.IsNullOrEmpty(decorId);
                var wasPlaced = _placedSlots.Contains(slotId);

                if (nowPlaced && !wasPlaced)
                {
                    _placedSlots.Add(slotId);
                    LoadPlacedAsync(anchor, decorId, animate: !_firstRender, _cts.Token).Forget();
                }
                else if (!nowPlaced && wasPlaced)
                {
                    _placedSlots.Remove(slotId);
                    if (_firstRender) anchor.SetEmpty();
                    else anchor.PlayRemoveTween(() => { if (anchor != null) anchor.SetEmpty(); });
                }
                else if (!nowPlaced && !wasPlaced && _firstRender)
                {
                    anchor.SetEmpty();
                }
                // nowPlaced && wasPlaced → already shown, leave as is.
            }
        }

        private async UniTaskVoid LoadPlacedAsync(DecorSlotAnchorView anchor, string decorId, bool animate, CancellationToken ct)
        {
            Sprite sprite = null;
            if (_sprites != null)
            {
                try { sprite = await _sprites.GetSpriteAsync(decorId, ct); }
                catch (System.OperationCanceledException) { return; }
            }
            if (ct.IsCancellationRequested || anchor == null) return;

            anchor.SetPlaced(sprite);
            if (animate) anchor.PlayPlaceTween();
        }

        private void RenderInventory()
        {
            var pool = View.CardsPool;
            if (pool == null) return;

            pool.DisableAll();
            var items = _inventory.GetByCategory(InventoryCategories.Decor);
            foreach (var item in items)
            {
                var config = _configs.Get<DecorConfig>(item.ItemId);
                if (config == null) continue;
                // Slot-first filter: hide decor that doesn't match the clicked slot's type. Checked
                // before GetNext() so a hidden card never consumes a pooled view.
                if (_slotTypeFilter.HasValue && config.PositionType != _slotTypeFilter.Value) continue;
                var placed = !string.IsNullOrEmpty(FindPlacedSlot(item.ItemId));
                var card = pool.GetNext();
                card.Bind(config, placed, _sprites, OnCardSelect, OnCardInfo);
            }
            pool.DisableNonActive();
        }

        private void OnCardSelect(string decorId)
        {
            HideHud(); // selecting a card dismisses the placed-slot HUD

            // Repeat click on the selected card cancels the selection.
            if (_state == State.DecorSelected && _selectedDecorId == decorId)
            {
                ClearSelection();
                return;
            }

            _selectedDecorId = decorId;
            _state = State.DecorSelected;

            if (View.CardsPool != null)
                foreach (var card in View.CardsPool.ActiveElements())
                    if (card != null) card.SetSelected(card.DecorId == decorId);

            ApplyDecorFocusFilter(decorId);
        }

        // Info is a separate WindowType.Popup shown additively over this window; it does not touch
        // this window's selection or placement.
        private void OnCardInfo(string decorId)
            => UIManager.ShowAsync<DecorInfoPopup>(new DecorInfoPopupArgs(decorId), _cts.Token).Forget();

        // ── Placed-slot HUD (Remove; Replace is disabled for MVP) ─────────────────────────────
        private void OnPlacedClicked(DecorSlotAnchorView anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId))) return;

            ClearSelection(); // switching to a placed slot drops any decor-card selection
            ShowHud(anchor);
            TrySetSlotFilter(anchor.SlotId); // filter inventory to this slot's type
        }

        private void ShowHud(DecorSlotAnchorView anchor)
        {
            if (View.SelectedSlotHud == null) return;

            if (View.SelectedSlotHud.transform is RectTransform hudRect)
            {
                //hudRect.position = anchor.transform.position; // park the HUD next to the slot
            }

            View.SelectedSlotHud.SetActive(true);
            anchor.SetSelectedOutline(true);
            BindSelectedDecorInfo(anchor.SlotId);
            _selectedSlotId = anchor.SlotId;
            _state = State.PlacedSlotSelected;
        }

        // Shows the clicked decor's name + sprite in the side panel. Sprite comes from the shared
        // (cached) provider by decor id, so this is a cheap re-fetch of the already-loaded sprite.
        private void BindSelectedDecorInfo(string slotId)
        {
            var decorId = _placement.GetDecorInSlot(slotId);
            var config = string.IsNullOrEmpty(decorId) ? null : _configs.Get<DecorConfig>(decorId);

            if (View.SelectedDecorNameLabel != null)
                View.SelectedDecorNameLabel.text = config != null ? config.DisplayName ?? config.Id : string.Empty;

            if (View.SelectedDecorImage != null)
            {
                View.SelectedDecorImage.sprite = null;
                if (!string.IsNullOrEmpty(decorId))
                    LoadSelectedDecorImageAsync(decorId, _cts.Token).Forget();
            }
        }

        private async UniTaskVoid LoadSelectedDecorImageAsync(string decorId, CancellationToken ct)
        {
            if (_sprites == null || View == null || View.SelectedDecorImage == null) return;

            Sprite sprite;
            try { sprite = await _sprites.GetSpriteAsync(decorId, ct); }
            catch (System.OperationCanceledException) { return; }

            if (ct.IsCancellationRequested || View == null || View.SelectedDecorImage == null) return;
            View.SelectedDecorImage.sprite = sprite;
        }

        private void HideHud()
        {
            if (View != null && View.SelectedSlotHud != null) View.SelectedSlotHud.SetActive(false);

            if (View != null)
            {
                if (View.SelectedDecorNameLabel != null) View.SelectedDecorNameLabel.text = string.Empty;
                if (View.SelectedDecorImage != null) View.SelectedDecorImage.sprite = null;
            }

            if (!string.IsNullOrEmpty(_selectedSlotId) && View != null && View.SlotAnchors != null)
                foreach (var anchor in View.SlotAnchors)
                    if (anchor != null && anchor.SlotId == _selectedSlotId) anchor.SetSelectedOutline(false);

            _selectedSlotId = null;
            if (_state == State.PlacedSlotSelected) _state = State.Default;
        }

        // The full-screen backdrop is the reset point: clear the slot-first inventory filter, restore
        // the full list, and close the tools panel (its prior sole responsibility).
        private void OnBackdropClicked()
        {
            _slotTypeFilter = null;
            RenderInventory();
            HideHud();
        }

        // Slot-first filter: show only inventory decor of the clicked slot's PositionType. Null-safe
        // against a prefab/config mismatch; re-renders the inventory to apply immediately.
        private void TrySetSlotFilter(string slotId)
        {
            if (!BuildSlotMap().TryGetValue(slotId, out var slot) || slot == null) return;
            _slotTypeFilter = slot.PositionType;
            RenderInventory();
        }

        private void OnRemoveClicked()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;
            var slotId = _selectedSlotId;
            HideHud();
            RemoveAsync(slotId).Forget();
        }

        private async UniTaskVoid RemoveAsync(string slotId)
        {
            try
            {
                // Visual (remove tween → SetEmpty) is handled by PlacementChanged → Render diff.
                await _placement.UnplaceAsync(slotId, _cts.Token);
                PlayUi(View != null ? View.RemoveClip : null);
            }
            catch (System.OperationCanceledException) { }
        }

        // Focus filter: empty slots whose PositionType matches the focused decor stay interactable
        // (and show the target hint); non-matching empty slots grey out. Occupied slots are untouched.
        // Match is by PositionType only — a size-too-big slot stays clickable and PlaceAsync rejects it.
        private void ApplyDecorFocusFilter(string decorId)
        {
            if (View.SlotAnchors == null) return;

            var config = _configs.Get<DecorConfig>(decorId);
            var slotById = BuildSlotMap();

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;
                if (!string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId))) continue; // occupied

                var matching = config != null
                    && slotById.TryGetValue(anchor.SlotId, out var slot)
                    && slot != null
                    && slot.PositionType == config.PositionType;

                anchor.SetMarkerInteractable(matching);
                anchor.SetHighlighted(matching);
            }
        }

        // "All available" state for empty slots: interactable, no hint. Occupied slots are skipped
        // entirely (marker hidden; placed button manages itself).
        private void ResetEmptySlotsAvailable()
        {
            if (View.SlotAnchors == null) return;

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;
                if (!string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId))) continue; // occupied

                anchor.SetMarkerInteractable(true);
                anchor.SetHighlighted(false);
            }
        }

        private void OnMarkerClicked(DecorSlotAnchorView anchor)
        {
            if (anchor == null) return;

            // With a card selected this is a placement click (decor-first). Otherwise it's a
            // slot-first click: filter the inventory to this slot's type.
            if (_state == State.DecorSelected && !string.IsNullOrEmpty(_selectedDecorId))
            {
                PlaceSelectedAsync(anchor.SlotId).Forget();
                return;
            }

            // Clicking an empty slot dismisses the placed-slot tools (and sets the inventory filter).
            HideHud();
            TrySetSlotFilter(anchor.SlotId);
        }

        private async UniTaskVoid PlaceSelectedAsync(string slotId)
        {
            var decorId = _selectedDecorId;
            if (string.IsNullOrEmpty(decorId)) return;

            var config = _configs.Get<DecorConfig>(decorId);
            if (config != null && HasNegativeEffect(config))
            {
                var args = new ConfirmDialogArgs(
                    title: $"Place {config.DisplayName}?",
                    body: BuildNegativeWarning(config),
                    confirmLabel: "Place anyway",
                    cancelLabel: "Cancel");

                var dialog = await UIManager.ShowAsync<ConfirmDialog>(args, _cts.Token);
                if (dialog == null) return;
                var confirm = await dialog.WaitForResultAsync<ConfirmDialogResult>(_cts.Token);
                if (confirm != ConfirmDialogResult.Confirmed) return;
            }

            var result = await _placement.PlaceAsync(decorId, slotId, _cts.Token);
            if (result == DecorPlacementResult.Success)
            {
                // Visual + selection reset are handled by PlacementChanged → Render (diff tween).
                PlayUi(View != null ? View.PlaceClip : null);
            }
            else
            {
                Debug.Log($"[DecorPlacementWindow] Place '{decorId}' → '{slotId}' failed: {result}");
            }
        }

        // Null-safe: no-op if the clip is unassigned or the audio service is not bound. Assigning a
        // clip in the inspector is enough to make it play — no code change needed.
        private static void PlayUi(AudioClip clip)
        {
            if (clip != null) Audio.PlayUi(clip);
        }

        private void ClearSelection()
        {
            _selectedDecorId = null;
            _state = State.Default;

            if (View.CardsPool != null)
                foreach (var card in View.CardsPool.ActiveElements())
                    if (card != null) card.SetSelected(false);

            ResetEmptySlotsAvailable();
        }

        private Dictionary<string, DecorSlot> BuildSlotMap()
        {
            var map = new Dictionary<string, DecorSlot>();
            var shop = _configs.Get<BookShopConfig>(DecorPlacementService.HardcodedBookShopId);
            if (shop?.DecorSlots == null) return map;
            foreach (var slot in shop.DecorSlots)
                if (slot != null && !string.IsNullOrEmpty(slot.Id)) map[slot.Id] = slot;
            return map;
        }

        private string FindPlacedSlot(string decorId)
        {
            foreach (var entry in _placement.GetAllPlacements())
                if (string.Equals(entry.DecorId, decorId, System.StringComparison.OrdinalIgnoreCase))
                    return entry.SlotId;
            return null;
        }

        private static bool HasNegativeEffect(DecorConfig config)
        {
            if (config?.GenreMultipliers == null) return false;
            foreach (var mod in config.GenreMultipliers)
                if (mod != null && mod.Multiplier < 1f) return true;
            return false;
        }

        private static string BuildNegativeWarning(DecorConfig config)
        {
            var sb = new StringBuilder("This decor will REDUCE: ");
            var first = true;
            foreach (var mod in config.GenreMultipliers)
            {
                if (mod == null || mod.Multiplier >= 1f) continue;
                if (!first) sb.Append(", ");
                first = false;
                var percent = Mathf.RoundToInt((1f - mod.Multiplier) * 100f);
                sb.Append($"{mod.Genre} −{percent}%");
            }
            sb.Append(". Continue?");
            return sb.ToString();
        }
    }
}
