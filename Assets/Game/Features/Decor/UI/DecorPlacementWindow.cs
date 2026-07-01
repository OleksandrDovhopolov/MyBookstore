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
            InfoPopupOpen,
        }

        private IDecorPlacementService _placement;
        private IConfigsService _configs;
        private IInventoryService _inventory;
        private IUiSpriteProvider _sprites;

        private CancellationTokenSource _cts;
        private CancellationTokenSource _infoIconCts;
        private readonly HashSet<string> _placedSlots = new();

        private State _state = State.Default;
        private string _selectedDecorId;
        private string _selectedSlotId; // placed slot whose HUD is open
        private bool _firstRender;

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
            if (View.InfoPopupRoot != null) View.InfoPopupRoot.SetActive(false);
            if (View.ReplaceButton != null) View.ReplaceButton.interactable = false; // MVP: no replace flow

            if (View.CloseButton != null) View.CloseButton.onClick.AddListener(OnCloseClicked);
            if (View.RemoveButton != null) View.RemoveButton.onClick.AddListener(OnRemoveClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.AddListener(HideHud);
            if (View.InfoCloseButton != null) View.InfoCloseButton.onClick.AddListener(HideInfo);

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
            _firstRender = true;
            Render();
        }

        protected override void OnHideStart(bool isClosed)
        {
            if (_placement != null) _placement.PlacementChanged -= Render;
            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;

            HideHud();
            HideInfo();
        }

        protected override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            CancelInfoIconLoad();

            if (View == null) return;
            if (View.CloseButton != null) View.CloseButton.onClick.RemoveListener(OnCloseClicked);
            if (View.RemoveButton != null) View.RemoveButton.onClick.RemoveListener(OnRemoveClicked);
            if (View.HudBackdrop != null) View.HudBackdrop.onClick.RemoveListener(HideHud);
            if (View.InfoCloseButton != null) View.InfoCloseButton.onClick.RemoveListener(HideInfo);
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

            HighlightCompatibleEmptySlots(decorId);
        }

        // ── Info popup (read-only overlay; does not change selection or placement) ────────────
        private void OnCardInfo(string decorId) => ShowInfo(decorId);

        private void ShowInfo(string decorId)
        {
            var config = _configs.Get<DecorConfig>(decorId);
            if (config == null || View.InfoPopupRoot == null) return;

            if (View.InfoNameLabel != null) View.InfoNameLabel.text = config.DisplayName ?? config.Id;
            if (View.InfoBonusesLabel != null)
                View.InfoBonusesLabel.text = DecorSlotRowView.FormatEffects(config, View.PositiveColor, View.NegativeColor);
            if (View.InfoDescriptionLabel != null) View.InfoDescriptionLabel.text = BuildDescription(config);

            if (View.InfoIcon != null)
            {
                View.InfoIcon.sprite = null;
                LoadInfoIconAsync(decorId).Forget();
            }

            // Pure overlay: does NOT touch _state, so the underlying decor selection survives.
            View.InfoPopupRoot.SetActive(true);
        }

        private void HideInfo()
        {
            CancelInfoIconLoad();
            if (View != null && View.InfoPopupRoot != null) View.InfoPopupRoot.SetActive(false);
        }

        private async UniTaskVoid LoadInfoIconAsync(string decorId)
        {
            if (_sprites == null || View == null || View.InfoIcon == null) return;

            CancelInfoIconLoad();
            _infoIconCts = new CancellationTokenSource();
            var ct = _infoIconCts.Token;
            try
            {
                var sprite = await _sprites.GetSpriteAsync(decorId, ct);
                if (ct.IsCancellationRequested) return;
                if (View != null && View.InfoIcon != null) View.InfoIcon.sprite = sprite;
            }
            catch (System.OperationCanceledException) { }
        }

        private void CancelInfoIconLoad()
        {
            if (_infoIconCts == null) return;
            _infoIconCts.Cancel();
            _infoIconCts.Dispose();
            _infoIconCts = null;
        }

        private static string BuildDescription(DecorConfig config)
        {
            var sb = new StringBuilder();
            sb.Append($"{config.PositionType} · {config.Size} · {config.Rarity}");
            if (config.AtmosphereTags != null && config.AtmosphereTags.Length > 0)
                sb.Append('\n').Append(string.Join(", ", config.AtmosphereTags));
            return sb.ToString();
        }

        // ── Placed-slot HUD (Remove; Replace is disabled for MVP) ─────────────────────────────
        private void OnPlacedClicked(DecorSlotAnchorView anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId))) return;

            ClearSelection(); // switching to a placed slot drops any decor-card selection
            ShowHud(anchor);
        }

        private void ShowHud(DecorSlotAnchorView anchor)
        {
            if (View.SelectedSlotHud == null) return;

            if (View.SelectedSlotHud.transform is RectTransform hudRect)
                hudRect.position = anchor.transform.position; // park the HUD next to the slot

            View.SelectedSlotHud.SetActive(true);
            anchor.SetSelectedOutline(true);
            _selectedSlotId = anchor.SlotId;
            _state = State.PlacedSlotSelected;
        }

        private void HideHud()
        {
            if (View != null && View.SelectedSlotHud != null) View.SelectedSlotHud.SetActive(false);

            if (!string.IsNullOrEmpty(_selectedSlotId) && View != null && View.SlotAnchors != null)
                foreach (var anchor in View.SlotAnchors)
                    if (anchor != null && anchor.SlotId == _selectedSlotId) anchor.SetSelectedOutline(false);

            _selectedSlotId = null;
            if (_state == State.PlacedSlotSelected) _state = State.Default;
        }

        private void OnRemoveClicked()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;
            var slotId = _selectedSlotId;
            HideHud();
            // Visual (remove tween → SetEmpty) is handled by PlacementChanged → Render diff.
            _placement.UnplaceAsync(slotId, _cts.Token).Forget();
        }

        private void HighlightCompatibleEmptySlots(string decorId)
        {
            if (View.SlotAnchors == null) return;

            var config = _configs.Get<DecorConfig>(decorId);
            var slotById = BuildSlotMap();

            foreach (var anchor in View.SlotAnchors)
            {
                if (anchor == null) continue;
                var empty = string.IsNullOrEmpty(_placement.GetDecorInSlot(anchor.SlotId));
                var compatible = empty
                    && config != null
                    && slotById.TryGetValue(anchor.SlotId, out var slot)
                    && slot != null
                    && config.PositionType == slot.PositionType
                    && (int)config.Size <= (int)slot.MaxSize;
                anchor.SetHighlighted(compatible);
            }
        }

        private void OnMarkerClicked(DecorSlotAnchorView anchor)
        {
            if (_state != State.DecorSelected || string.IsNullOrEmpty(_selectedDecorId)) return;
            PlaceSelectedAsync(anchor.SlotId).Forget();
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
                if (View != null && View.PlaceClip != null) Audio.PlayUi(View.PlaceClip);
            }
            else
            {
                Debug.Log($"[DecorPlacementWindow] Place '{decorId}' → '{slotId}' failed: {result}");
            }
        }

        private void ClearSelection()
        {
            _selectedDecorId = null;
            _state = State.Default;

            if (View.CardsPool != null)
                foreach (var card in View.CardsPool.ActiveElements())
                    if (card != null) card.SetSelected(false);

            if (View.SlotAnchors != null)
                foreach (var anchor in View.SlotAnchors)
                    if (anchor != null) anchor.SetHighlighted(false);
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

        private void OnCloseClicked() => CloseAsync().Forget();
    }
}
